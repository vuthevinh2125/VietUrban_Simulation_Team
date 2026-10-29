using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PaymentMethod { Cash, Card }

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
    [Tooltip("(Tuỳ chọn) Chỗ thu ngân đứng + hướng quay mặt. Để trống = tự tìm chỗ cạnh quầy. " +
             "Đặt nhanh: lúc đang Play kéo thu ngân tới chỗ muốn rồi bấm Tools > Store Setup > 14.")]
    public Transform cashierPoint;

    [Header("Queue")]
    [Tooltip("Hướng hàng chờ kéo dài, tính theo trục local của customerPoint (mặc định: sau lưng khách)")]
    public Vector3 queueDirection = Vector3.back;
    public float queueSpacing = 0.9f;
    public int maxQueue = 6;

    [Header("Items on counter")]
    [Tooltip("Kích thước khay đặt hàng (ngang x sâu, mét) quanh ItemDropPoint. Auto Snap sẽ tự đo.")]
    public Vector2 dropAreaSize = new Vector2(0.4f, 0.3f);
    [Tooltip("Món hàng chiếm tối đa bao nhiêu phần một ô trong khay (chừa khe để không chạm nhau)")]
    [Range(0.5f, 1f)] public float itemFill = 0.85f;
    [Tooltip("Lề chừa ra mỗi bên khay (mét), để hàng không chạm thành khay / thân máy")]
    public float dropAreaMargin = 0.03f;

    [Header("Cashier")]
    [Tooltip("Tự quét hàng (để test, hoặc sau này thuê thu ngân)")]
    public bool autoScan = false;
    public float autoScanInterval = 0.6f;
    [Tooltip("Thời gian khách lấy tiền / thẻ ra sau khi quét xong")]
    public float paymentDelay = 1.2f;
    [Tooltip("Tỉ lệ khách trả bằng thẻ (còn lại trả tiền mặt)")]
    [Range(0f, 1f)] public float cardChance = 0.45f;
    public AudioClip scanSound;
    [Tooltip("(Tuỳ chọn) âm thanh khi thanh toán xong")]
    public AudioClip paySound;

    private readonly List<NPCShopper> queue = new List<NPCShopper>();
    private readonly List<CheckoutItem> itemsOnCounter = new List<CheckoutItem>();
    private NPCShopper current;
    private float runningTotal;
    private int scannedCount;
    private bool paying;
    private bool awaitingPayment;
    private PaymentToken token;

    public PaymentMethod Method { get; private set; }
    /// <summary>Số tiền phải trả (cent).</summary>
    public int DueCents { get; private set; }
    /// <summary>Tiền mặt khách đưa (cent).</summary>
    public int CashGivenCents { get; private set; }
    /// <summary>Đã quét xong, đang chờ người chơi nhận tiền / quẹt thẻ.</summary>
    public bool AwaitingPayment => awaitingPayment;
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
    /// Quét lưới tia chiếu từ trên xuống khắp quầy, lấy các điểm chạm mặt phẳng nằm ngang trong khoảng độ cao,
    /// gom thành các mặt liền nhau, chọn mặt liền lớn nhất (khay đặt hàng), rồi đặt ItemDropPoint vào giữa
    /// và đo kích thước khay (dropAreaSize) theo hướng của quầy.
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
        const float step = 0.04f;
        int cols = Mathf.CeilToInt(b.size.x / step), rows = Mathf.CeilToInt(b.size.z / step);
        var grid = new Dictionary<Vector2Int, Vector3>();
        for (int ix = 0; ix < cols; ix++)
            for (int iz = 0; iz < rows; iz++)
            {
                Vector3 origin = new Vector3(b.min.x + (ix + 0.5f) * step, baseY + maxDropHeight + 0.05f, b.min.z + (iz + 0.5f) * step);
                // Mặt CAO NHẤT tại điểm này phải là mặt quầy nằm ngang (không bị vật khác che bên trên)
                float topY = float.NegativeInfinity;
                RaycastHit top = default;
                foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, maxDropHeight + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!h.transform.IsChildOf(transform)) continue;
                    if (h.point.y > topY) { topY = h.point.y; top = h; }
                }
                if (float.IsNegativeInfinity(topY) || top.normal.y < 0.95f) continue;
                float height = topY - baseY;
                if (height < minDropHeight || height > maxDropHeight) continue;
                grid[new Vector2Int(ix, iz)] = top.point;
            }

        if (grid.Count == 0)
        {
            Debug.LogWarning($"[Checkout] Không tìm thấy khay đặt hàng trên '{name}' (quầy cần có collider). Hãy tự kéo ItemDropPoint, chỉnh Drop Area Size và tắt Auto Snap.", this);
            return;
        }

        // Gom các ô liền nhau, cùng độ cao (lệch < 1.5cm) thành từng mặt; lấy mặt lớn nhất
        var visited = new HashSet<Vector2Int>();
        List<Vector3> best = null;
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var start in grid.Keys)
        {
            if (visited.Contains(start)) continue;
            var region = new List<Vector3>();
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            visited.Add(start);
            while (stack.Count > 0)
            {
                Vector2Int c = stack.Pop();
                region.Add(grid[c]);
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int n = c + d;
                    if (visited.Contains(n) || !grid.TryGetValue(n, out Vector3 np)) continue;
                    if (Mathf.Abs(np.y - grid[c].y) > 0.015f) continue;
                    visited.Add(n);
                    stack.Push(n);
                }
            }
            if (best == null || region.Count > best.Count) best = region;
        }

        // Khung chữ nhật của mặt đó theo hướng quầy
        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Quaternion inv = Quaternion.Inverse(rot);
        Vector3 refPoint = best[0];
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, sumY = 0f;
        foreach (Vector3 p in best)
        {
            Vector3 l = inv * (p - refPoint);
            minX = Mathf.Min(minX, l.x); maxX = Mathf.Max(maxX, l.x);
            minZ = Mathf.Min(minZ, l.z); maxZ = Mathf.Max(maxZ, l.z);
            sumY += p.y;
        }
        Vector3 center = refPoint + rot * new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        center.y = sumY / best.Count;

        itemDropPoint.SetPositionAndRotation(center + Vector3.up * 0.002f, rot);
        dropAreaSize = new Vector2(maxX - minX + step, maxZ - minZ + step); // điểm lưới nằm giữa ô -> cộng nửa ô mỗi bên
        Debug.Log($"[Checkout] Khay đặt hàng của '{name}': cao {center.y - baseY:F2}m, rộng {dropAreaSize.x:F2} x {dropAreaSize.y:F2}m.", this);
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
        // Giãn đủ chỗ cho người to nhất trong hàng (2 x bán kính + khe hở), tránh đẩy nhau đi vòng vòng
        float spacing = queueSpacing;
        foreach (NPCShopper c in queue) if (c != null) spacing = Mathf.Max(spacing, c.Radius * 2f + 0.25f);
        return customerPoint.position + dir.normalized * spacing * index;
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

        // Chia khay thành lưới đủ ô cho cả giỏ hàng, mỗi món một ô
        Vector2 area = new Vector2(Mathf.Max(0.05f, dropAreaSize.x - dropAreaMargin * 2f), Mathf.Max(0.05f, dropAreaSize.y - dropAreaMargin * 2f));
        int n = basket.Count;
        int nx = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n * area.x / area.y)), 1, n);
        int nz = Mathf.CeilToInt(n / (float)nx);
        Vector2 cellSize = new Vector2(area.x / nx, area.y / nz);
        for (int i = 0; i < n; i++)
            itemsOnCounter.Add(SpawnCounterItem(basket[i], i % nx, i / nx, nx, nz, cellSize));

        if (autoScan) StartCoroutine(AutoScanRoutine());
    }

    /// <summary>Người chơi click vào một món trên quầy.</summary>
    public bool TryScan(CheckoutItem item)
    {
        if (paying || item == null || !itemsOnCounter.Remove(item)) return false;

        runningTotal += item.price;
        scannedCount++;
        if (scanSound == null) AudioManager.PlayAt("scan_beep", item.transform.position, 0.9f);
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

    /// <summary>Quét xong: khách lấy tiền mặt hoặc thẻ ra đặt lên quầy, chờ người chơi nhận.</summary>
    private IEnumerator CompletePayment()
    {
        paying = true;
        yield return new WaitForSeconds(paymentDelay);

        DueCents = Mathf.RoundToInt(runningTotal * 100f);
        Method = Random.value < cardChance ? PaymentMethod.Card : PaymentMethod.Cash;
        CashGivenCents = Method == PaymentMethod.Cash ? PickCashAmount(DueCents) : 0;
        awaitingPayment = true;

        if (current != null)
        {
            string say = Method == PaymentMethod.Cash ? $"Cash {EconomyManager.Format(CashGivenCents / 100f)}" : "Card";
            SpeechBubble.Show(current.transform, say, Color.white, 2.15f, 4f);
        }

        if (autoScan)
        {
            // Có thu ngân: tự thối tiền / quẹt thẻ
            yield return new WaitForSeconds(autoScanInterval * 2f);
            FinishPayment(0);
            yield break;
        }

        Vector3 pos = itemDropPoint != null ? itemDropPoint.position : transform.position;
        Quaternion rot = itemDropPoint != null ? itemDropPoint.rotation : transform.rotation;
        token = PaymentToken.Create(this, Method, pos, rot);
    }

    /// <summary>
    /// Người chơi đã nhận tiền xong (gọi từ PaymentPanel).
    /// lostCents = số tiền thối dư (bị mất).
    /// </summary>
    public void FinishPayment(int lostCents)
    {
        if (!awaitingPayment) return;
        awaitingPayment = false;
        if (token != null) Destroy(token.gameObject);
        token = null;

        float total = DueCents / 100f;
        EconomyManager.Instance.RecordSale(total, scannedCount, $"Customer paid {scannedCount} item(s) by {Method}");
        if (lostCents > 0) EconomyManager.Instance.Charge(lostCents / 100f, MoneyCategory.Other, "Too much change given");
        StoreProgress.AddXP(StoreProgress.XPForSale(total, scannedCount));
        Debug.Log($"[Checkout] Khách trả {EconomyManager.Format(total)} cho {scannedCount} món ({Method})" +
                  (lostCents > 0 ? $", thối dư mất {EconomyManager.Format(lostCents / 100f)}." : "."));
        AudioManager.PlayAt(Method == PaymentMethod.Card ? "cardpayment" : "cash_register", transform.position, 0.9f);
        if (paySound != null)
        {
            if (audioSource != null) audioSource.PlayOneShot(paySound);
            else AudioSource.PlayClipAtPoint(paySound, transform.position);
        }

        NPCShopper paidCustomer = current;
        current = null;
        paying = false;
        queue.Remove(paidCustomer);
        if (paidCustomer != null) paidCustomer.OnPaid();
        RefreshQueue();
    }

    /// <summary>Khách trả tiền mặt như ngoài đời: đưa vừa đủ, hoặc làm tròn lên tờ 5/10/20/50/100.</summary>
    private static int PickCashAmount(int due)
    {
        var options = new List<int>();
        foreach (int step in new[] { 100, 500, 1000, 2000, 5000, 10000 })
        {
            int v = Mathf.CeilToInt(due / (float)step) * step;
            if (v >= due && !options.Contains(v)) options.Add(v);
        }
        if (options.Count == 0) return due;
        // Ưu tiên các mức nhỏ (khách hay đưa tờ gần với số tiền)
        int pick = Mathf.Min(options.Count - 1, Mathf.FloorToInt(Mathf.Pow(Random.value, 1.6f) * Mathf.Min(options.Count, 4)));
        return options[pick];
    }

    private void ClearCounter()
    {
        awaitingPayment = false;
        if (token != null) Destroy(token.gameObject);
        token = null;
        PaymentPanel.CloseFor(this);
        foreach (CheckoutItem item in itemsOnCounter)
            if (item != null) Destroy(item.gameObject);
        itemsOnCounter.Clear();
    }

    private CheckoutItem SpawnCounterItem(ItemData data, int col, int row, int nx, int nz, Vector2 cellSize)
    {
        GameObject prefab = data != null ? data.ResolveProductPrefab() : null;
        GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Checkout_" + (data != null ? data.itemName : "Item");
        if (prefab == null) RuntimeMaterials.Apply(go.GetComponent<Renderer>(), Color.gray);
        go.transform.rotation = itemDropPoint.rotation;

        // Kích thước như trên kệ, thu nhỏ nếu không vừa ô của khay
        Vector3 wanted = prefab == null ? Vector3.one * 0.15f
            : (data != null && data.shelfScale != Vector3.zero ? data.shelfScale : Vector3.one);
        Bounds lb = ProductFit.LocalBounds(go.transform);
        Vector3 size = Vector3.Scale(lb.size, wanted);
        float k = Mathf.Min(1f, cellSize.x * itemFill / Mathf.Max(0.001f, size.x), cellSize.y * itemFill / Mathf.Max(0.001f, size.z));
        Vector3 scale = wanted * k;
        go.transform.localScale = scale;

        // Tâm ô trong khay, đáy món hàng chạm mặt khay
        Vector3 cellLocal = new Vector3((col - (nx - 1) * 0.5f) * cellSize.x, 0f, (row - (nz - 1) * 0.5f) * cellSize.y);
        Vector3 pivotOffset = new Vector3(-lb.center.x * scale.x, -lb.min.y * scale.y, -lb.center.z * scale.z);
        go.transform.position = itemDropPoint.TransformPoint(cellLocal) + itemDropPoint.rotation * pivotOffset;

        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.useGravity = false; }
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
        if (awaitingPayment) return Method == PaymentMethod.Cash
            ? $"Customer pays cash {EconomyManager.Format(CashGivenCents / 100f)} for {EconomyManager.Format(DueCents / 100f)} - click the cash"
            : $"Customer pays by card {EconomyManager.Format(DueCents / 100f)} - click the card";
        if (paying) return $"Total {EconomyManager.Format(runningTotal)} - customer is getting the money...";
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
            Gizmos.matrix = Matrix4x4.TRS(itemDropPoint.position, itemDropPoint.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.up * 0.01f, new Vector3(dropAreaSize.x, 0.02f, dropAreaSize.y));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
