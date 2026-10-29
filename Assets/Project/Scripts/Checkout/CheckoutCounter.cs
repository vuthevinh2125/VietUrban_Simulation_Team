using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quầy thu ngân. Khách xếp hàng phía sau customerPoint, khách đầu tiên đặt hàng lên quầy (itemDropPoint),
/// người chơi click từng món để QUÉT, quét hết thì khách trả tiền -> EconomyManager.RecordSale.
/// </summary>
public class CheckoutCounter : MonoBehaviour
{
    public static readonly List<CheckoutCounter> All = new List<CheckoutCounter>();

    [Header("Points")]
    [Tooltip("Chỗ khách đứng khi thanh toán (mặt hướng vào quầy)")]
    public Transform customerPoint;
    [Tooltip("Chỗ khách đặt hàng lên mặt quầy")]
    public Transform itemDropPoint;

    [Header("Queue")]
    [Tooltip("Hướng hàng chờ kéo dài, tính theo trục local của customerPoint (mặc định: sau lưng khách)")]
    public Vector3 queueDirection = Vector3.back;
    public float queueSpacing = 0.9f;
    public int maxQueue = 6;

    [Header("Items on counter")]
    public float itemSpacing = 0.22f;
    public int itemsPerRow = 4;

    [Header("Cashier")]
    [Tooltip("Tự quét hàng (để test, hoặc sau này thuê thu ngân)")]
    public bool autoScan = false;
    public float autoScanInterval = 0.6f;
    [Tooltip("Thời gian khách trả tiền sau khi quét xong")]
    public float paymentDelay = 1.2f;
    public AudioClip scanSound;

    private readonly List<NPCShopper> queue = new List<NPCShopper>();
    private readonly List<CheckoutItem> itemsOnCounter = new List<CheckoutItem>();
    private NPCShopper current;
    private float runningTotal;
    private int scannedCount;
    private bool paying;
    private AudioSource audioSource;

    public NPCShopper Current => current;
    public int QueueLength => queue.Count;
    public float RunningTotal => runningTotal;
    public int ItemsLeft => itemsOnCounter.Count;
    public bool IsPaying => paying;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    [Header("Drop point")]
    [Tooltip("Tự tìm bục đặt hàng (mặt phẳng rộng nhất trong khoảng độ cao bên dưới). Tắt nếu muốn tự kéo ItemDropPoint.")]
    public bool autoSnapDropPoint = true;
    [Tooltip("Độ cao thấp nhất / cao nhất của bục đặt hàng so với chân quầy (mét)")]
    public float minDropHeight = 0.55f;
    public float maxDropHeight = 1.0f;

    void Awake()
    {
        if (customerPoint == null) customerPoint = transform;
        if (itemDropPoint == null) itemDropPoint = transform;
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (autoSnapDropPoint) SnapDropPointToCounterTop();
    }

    /// <summary>
    /// Quét lưới tia chiếu từ trên xuống khắp mặt quầy, gom các điểm chạm mặt phẳng nằm ngang trong khoảng độ cao,
    /// chọn "tầng" có nhiều điểm nhất (bục rộng nhất) và đặt ItemDropPoint vào giữa tầng đó.
    /// </summary>
    private void SnapDropPointToCounterTop()
    {
        if (itemDropPoint == null || itemDropPoint == transform) return;

        Bounds b = default;
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        float baseY = b.min.y;
        var hits = new List<Vector3>();
        const float step = 0.08f;
        for (float x = b.min.x + step * 0.5f; x < b.max.x; x += step)
            for (float z = b.min.z + step * 0.5f; z < b.max.z; z += step)
            {
                Vector3 origin = new Vector3(x, baseY + maxDropHeight + 0.05f, z);
                float best = float.NegativeInfinity;
                Vector3 bestPoint = Vector3.zero;
                foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, maxDropHeight + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!h.transform.IsChildOf(transform) || h.normal.y < 0.9f) continue;
                    float height = h.point.y - baseY;
                    if (height < minDropHeight || height > maxDropHeight) continue;
                    if (h.point.y > best) { best = h.point.y; bestPoint = h.point; }
                }
                if (!float.IsNegativeInfinity(best)) hits.Add(bestPoint);
            }

        if (hits.Count == 0)
        {
            Debug.LogWarning($"[Checkout] Không tìm thấy bục đặt hàng trên '{name}' (quầy cần có collider). Hãy tự kéo ItemDropPoint và tắt Auto Snap.", this);
            return;
        }

        // Gom theo độ cao (5cm một tầng), lấy tầng nhiều điểm nhất
        var levels = new Dictionary<int, List<Vector3>>();
        foreach (Vector3 p in hits)
        {
            int key = Mathf.RoundToInt(p.y / 0.05f);
            if (!levels.TryGetValue(key, out var list)) levels[key] = list = new List<Vector3>();
            list.Add(p);
        }
        List<Vector3> bestLevel = null;
        foreach (var kv in levels)
            if (bestLevel == null || kv.Value.Count > bestLevel.Count) bestLevel = kv.Value;

        Vector3 center = Vector3.zero;
        foreach (Vector3 p in bestLevel) center += p;
        center /= bestLevel.Count;
        itemDropPoint.position = center + Vector3.up * 0.01f;
        Debug.Log($"[Checkout] Bục đặt hàng của '{name}': cao {center.y - baseY:F2}m, {bestLevel.Count} điểm.", this);
    }

    /// <summary>Quầy ít người chờ nhất (hoà thì gần nhất).</summary>
    public static CheckoutCounter FindBest(Vector3 from)
    {
        CheckoutCounter best = null;
        foreach (CheckoutCounter c in All)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            if (best == null || c.queue.Count < best.queue.Count ||
                (c.queue.Count == best.queue.Count && (c.transform.position - from).sqrMagnitude < (best.transform.position - from).sqrMagnitude))
                best = c;
        }
        return best;
    }

    // ================= Hàng chờ =================
    public bool JoinQueue(NPCShopper customer)
    {
        if (customer == null || queue.Contains(customer)) return true;
        if (queue.Count >= maxQueue) return false;
        queue.Add(customer);
        RefreshQueue();
        return true;
    }

    public void LeaveQueue(NPCShopper customer)
    {
        if (!queue.Remove(customer)) return;
        if (current == customer) { ClearCounter(); current = null; paying = false; StopAllCoroutines(); }
        RefreshQueue();
    }

    public Vector3 GetQueuePosition(int index)
    {
        Vector3 dir = customerPoint.TransformDirection(queueDirection.sqrMagnitude > 0.001f ? queueDirection.normalized : Vector3.back);
        dir.y = 0f;
        return customerPoint.position + dir.normalized * queueSpacing * index;
    }

    private void RefreshQueue()
    {
        for (int i = 0; i < queue.Count; i++)
            if (queue[i] != null) queue[i].SetQueueTarget(GetQueuePosition(i), i == 0);
    }

    // ================= Thanh toán =================
    /// <summary>Khách đầu hàng đã tới quầy: đặt hàng lên quầy để người chơi quét.</summary>
    public void StartCheckout(NPCShopper customer, List<ItemData> basket)
    {
        if (current != null || queue.Count == 0 || queue[0] != customer || basket == null || basket.Count == 0) return;

        current = customer;
        runningTotal = 0f;
        scannedCount = 0;

        for (int i = 0; i < basket.Count; i++)
            itemsOnCounter.Add(SpawnCounterItem(basket[i], i));

        if (autoScan) StartCoroutine(AutoScanRoutine());
    }

    /// <summary>Người chơi click vào một món trên quầy.</summary>
    public bool TryScan(CheckoutItem item)
    {
        if (paying || item == null || !itemsOnCounter.Remove(item)) return false;

        runningTotal += item.price;
        scannedCount++;
        if (scanSound != null)
        {
            if (audioSource != null) audioSource.PlayOneShot(scanSound);
            else AudioSource.PlayClipAtPoint(scanSound, item.transform.position);
        }
        Destroy(item.gameObject);

        if (itemsOnCounter.Count == 0) StartCoroutine(CompletePayment());
        return true;
    }

    private IEnumerator AutoScanRoutine()
    {
        while (itemsOnCounter.Count > 0 && !paying)
        {
            yield return new WaitForSeconds(autoScanInterval);
            if (itemsOnCounter.Count > 0) TryScan(itemsOnCounter[0]);
        }
    }

    private IEnumerator CompletePayment()
    {
        paying = true;
        yield return new WaitForSeconds(paymentDelay);

        EconomyManager.Instance.RecordSale(runningTotal, scannedCount, $"Customer paid {scannedCount} item(s)");
        Debug.Log($"[Checkout] Khách trả {EconomyManager.Format(runningTotal)} cho {scannedCount} món.");

        NPCShopper paidCustomer = current;
        current = null;
        paying = false;
        queue.Remove(paidCustomer);
        if (paidCustomer != null) paidCustomer.OnPaid();
        RefreshQueue();
    }

    private void ClearCounter()
    {
        foreach (CheckoutItem item in itemsOnCounter)
            if (item != null) Destroy(item.gameObject);
        itemsOnCounter.Clear();
    }

    private CheckoutItem SpawnCounterItem(ItemData data, int index)
    {
        int perRow = Mathf.Max(1, itemsPerRow);
        int row = index / perRow;
        int col = index % perRow;
        Vector3 offset = itemDropPoint.right * ((col - (perRow - 1) * 0.5f) * itemSpacing) + itemDropPoint.forward * (row * itemSpacing);

        GameObject prefab = data != null ? data.ResolveProductPrefab() : null;
        GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Checkout_" + (data != null ? data.itemName : "Item");
        // Kích thước giống lúc nằm trên kệ (prefab lấy từ trong thùng hàng có thể mang tỉ lệ của thùng)
        if (prefab == null) go.transform.localScale = Vector3.one * 0.15f;
        else go.transform.localScale = data != null && data.shelfScale != Vector3.zero ? data.shelfScale : Vector3.one;

        go.transform.SetPositionAndRotation(itemDropPoint.position + offset, itemDropPoint.rotation);

        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        CheckoutItem item = go.AddComponent<CheckoutItem>();
        item.item = data;
        item.price = PriceManager.GetSellPrice(data);
        item.counter = this;
        return item;
    }

    public string GetStatusText()
    {
        if (current == null) return queue.Count > 0 ? $"Waiting customers: {queue.Count}" : "No customers";
        if (paying) return $"Customer paying {EconomyManager.Format(runningTotal)}...";
        return $"Total {EconomyManager.Format(runningTotal)} | Items left: {itemsOnCounter.Count}";
    }

    void OnDrawGizmos()
    {
        if (customerPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(customerPoint.position, 0.3f);
            Gizmos.DrawRay(customerPoint.position, customerPoint.forward * 0.6f);
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            for (int i = 1; i < maxQueue; i++) Gizmos.DrawWireSphere(GetQueuePosition(i), 0.2f);
        }
        if (itemDropPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(itemDropPoint.position, new Vector3(itemSpacing * Mathf.Max(1, itemsPerRow), 0.02f, itemSpacing * 2f));
        }
    }
}
