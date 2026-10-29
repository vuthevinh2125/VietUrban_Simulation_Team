using UnityEngine;
using System.Collections;

/// <summary>
/// Thùng hàng người chơi cầm được. Chứa productCount món của mặt hàng itemData.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Interactable : MonoBehaviour
{
    public string promptMessage = "Pick up box";

    [Header("Item Data Reference")]
    public ItemData itemData; // Thùng này chứa mặt hàng gì

    [Header("Crate Inventory")]
    public GameObject productPrefab;
    public int productCount = 10;
    public GameObject[] visualItems;

    [Header("Auto Visuals (thùng dùng chung)")]
    [Tooltip("Thùng không có sẵn hàng mẫu bên trong: tự sinh các món hàng thật vào lòng thùng theo ItemData")]
    public bool autoGenerateVisuals = true;
    [Tooltip("Phần lòng thùng dùng để xếp hàng (so với kích thước thùng)")]
    public Vector3 innerFill = new Vector3(0.84f, 0.8f, 0.84f);
    [Tooltip("Phóng to tối đa món hàng trong thùng")]
    public float maxVisualScale = 1.6f;

    private bool visualsGenerated;
    private Transform visualsRoot;
    private Bounds crateLocalBounds;

    [Header("Flap Animation (Rigged)")]
    public Transform flap1; public Vector3 flap1OpenRot;
    public Transform flap2; public Vector3 flap2OpenRot;
    public Transform flap3; public Vector3 flap3OpenRot;
    public Transform flap4; public Vector3 flap4OpenRot;

    private bool isOpened = false;
    private Rigidbody rb;
    private Collider coll;

    // Góc quay ban đầu của nắp (trạng thái đóng)
    private Quaternion f1Closed, f2Closed, f3Closed, f4Closed;

    // ================= Thông tin =================
    /// <summary>Sức chứa của thùng: theo số món hiển thị, nếu không có thì theo ItemData.itemsPerBox.</summary>
    public int Capacity
    {
        get
        {
            if (visualItems != null && visualItems.Length > 0) return visualItems.Length;
            if (itemData != null && itemData.itemsPerBox > 0) return itemData.itemsPerBox;
            return Mathf.Max(1, productCount);
        }
    }

    public bool IsEmpty => productCount <= 0;
    public bool IsFull => productCount >= Capacity;
    public bool IsHeld { get; private set; }
    public string DisplayName => itemData != null ? itemData.itemName : "Unknown item";

    /// <summary>Prefab món hàng để xếp lên kệ (ưu tiên của thùng, không có thì lấy từ ItemData).</summary>
    public GameObject ProductPrefab => productPrefab != null ? productPrefab : (itemData != null ? itemData.productPrefab : null);

    // ================= Unity =================
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collider>();

        if (flap1 != null) f1Closed = flap1.localRotation;
        if (flap2 != null) f2Closed = flap2.localRotation;
        if (flap3 != null) f3Closed = flap3.localRotation;
        if (flap4 != null) f4Closed = flap4.localRotation;

        crateLocalBounds = MeasureCrate();
        if (itemData != null) BuildGeneratedVisuals();

        productCount = Mathf.Clamp(productCount, 0, Capacity);
        UpdateVisuals();

        if (itemData == null)
            Debug.LogWarning($"[Box] '{name}' chưa gán ItemData - kệ sẽ không biết đây là hàng gì.", this);
    }

    void OnEnable() => StockManager.Register(this);
    void OnDisable() => StockManager.Unregister(this);

    // ================= API =================
    public void SetItem(ItemData data) => SetItem(data, false);

    /// <summary>Gán mặt hàng cho thùng. fillBox = true: đổ đầy thùng theo sức chứa (thùng mới giao).</summary>
    public void SetItem(ItemData data, bool fillBox)
    {
        if (data == null) return;
        bool changed = itemData != data || (visualsGenerated == false && (visualItems == null || visualItems.Length == 0));
        itemData = data;
        if (changed) BuildGeneratedVisuals();
        if (fillBox) productCount = Capacity;
        productCount = Mathf.Clamp(productCount, 0, Capacity);
        UpdateVisuals();
        StockManager.NotifyChanged();
    }

    public void OnPickedUp(Transform holdPosition)
    {
        IsHeld = true;
        rb.isKinematic = true;
        if (coll != null) coll.enabled = false;

        if (holdPosition != null)
        {
            transform.SetParent(holdPosition);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        if (!isOpened)
        {
            StopAllCoroutines();
            StartCoroutine(OpenFlapsAnimation());
            isOpened = true;
        }
    }

    public void OnDropped()
    {
        IsHeld = false;
        transform.SetParent(null);
        rb.isKinematic = false;
        if (coll != null) coll.enabled = true;

        if (isOpened)
        {
            StopAllCoroutines();
            StartCoroutine(CloseFlapsAnimation());
            isOpened = false;
        }
    }

    /// <summary>Lấy 1 món ra khỏi thùng (để xếp lên kệ).</summary>
    public bool RemoveOne()
    {
        if (IsEmpty) return false;
        productCount--;
        UpdateVisuals();
        StockManager.NotifyChanged();
        return true;
    }

    /// <summary>Bỏ 1 món vào lại thùng (lấy từ kệ xuống).</summary>
    public bool AddOne()
    {
        if (IsFull) return false;
        productCount++;
        UpdateVisuals();
        StockManager.NotifyChanged();
        return true;
    }

    /// <summary>Vứt thùng (thường là thùng rỗng).</summary>
    public void Discard()
    {
        StopAllCoroutines();
        Destroy(gameObject);
    }

    public void UpdateVisuals()
    {
        if (visualItems == null) return;
        for (int i = 0; i < visualItems.Length; i++)
        {
            if (visualItems[i] != null) visualItems[i].SetActive(i < productCount);
        }
    }

    // ================= Hàng hiển thị trong thùng dùng chung =================
    /// <summary>Kích thước thùng trong toạ độ local: ưu tiên BoxCollider, không có thì đo mesh.</summary>
    private Bounds MeasureCrate()
    {
        if (coll is BoxCollider bc) return new Bounds(bc.center, bc.size);
        return ProductFit.LocalBounds(transform);
    }

    /// <summary>
    /// Sinh itemsPerBox bản sao món hàng xếp lưới gọn trong lòng thùng (tầng dưới trước),
    /// dùng làm visualItems để lấy ra / bỏ vào hiện đúng số lượng như các thùng có sẵn.
    /// </summary>
    private void BuildGeneratedVisuals()
    {
        if (!autoGenerateVisuals) return;
        if (!visualsGenerated && visualItems != null && visualItems.Length > 0) return; // thùng đã có hàng mẫu sẵn

        if (visualsRoot != null) Destroy(visualsRoot.gameObject);
        visualsRoot = null;
        visualItems = new GameObject[0];
        visualsGenerated = false;

        GameObject prefab = ProductPrefab;
        if (prefab == null || itemData == null) return;
        int n = itemData.itemsPerBox > 0 ? itemData.itemsPerBox : Mathf.Max(1, productCount);

        // Gốc đặt ở đáy lòng thùng, scale thế giới = 1 để tính bằng mét
        Bounds cb = crateLocalBounds;
        Vector3 ls = transform.lossyScale;
        visualsRoot = new GameObject("ProductVisuals").transform;
        visualsRoot.SetParent(transform, false);
        visualsRoot.localPosition = new Vector3(cb.center.x, cb.min.y + cb.size.y * 0.06f, cb.center.z);
        visualsRoot.localRotation = Quaternion.identity;
        visualsRoot.localScale = new Vector3(1f / Safe(ls.x), 1f / Safe(ls.y), 1f / Safe(ls.z));
        Vector3 inner = Vector3.Scale(Vector3.Scale(cb.size, ls), innerFill);

        // Đo 1 món
        Vector3 wanted = itemData.shelfScale != Vector3.zero ? itemData.shelfScale : Vector3.one;
        GameObject probe = Instantiate(prefab, visualsRoot, false);
        probe.transform.localRotation = Quaternion.identity;
        Bounds lb = ProductFit.LocalBounds(probe.transform);
        DestroyImmediate(probe);
        Vector3 u = Vector3.Scale(lb.size, wanted);

        // Chọn cách xếp (cột x hàng x tầng) cho món hàng to nhất mà vẫn vừa thùng
        int bx = 1, bz = 1, by = n; float best = 0f;
        for (int nx = 1; nx <= n; nx++)
            for (int nz = 1; nx * nz <= n * 2 && nz <= n; nz++)
            {
                int ny = Mathf.CeilToInt(n / (float)(nx * nz));
                float sc = Mathf.Min(inner.x / (nx * u.x), inner.z / (nz * u.z), inner.y / (ny * u.y));
                if (sc > best + 0.0001f) { best = sc; bx = nx; bz = nz; by = ny; }
            }
        float s = Mathf.Min(best, maxVisualScale);
        Vector3 size = u * s;
        Vector3 scale = wanted * s;

        visualItems = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            int layer = i / (bx * bz);
            int r = i % (bx * bz);
            int ix = r % bx, iz = r / bx;
            Vector3 pos = new Vector3(
                (ix - (bx - 1) * 0.5f) * (inner.x / bx),
                layer * size.y,
                (iz - (bz - 1) * 0.5f) * (inner.z / bz));
            pos -= new Vector3(lb.center.x * scale.x, lb.min.y * scale.y, lb.center.z * scale.z);

            GameObject v = Instantiate(prefab, visualsRoot, false);
            v.name = $"{prefab.name}_{i:00}";
            ProductFit.MakeVisualOnly(v);
            v.transform.localPosition = pos;
            v.transform.localRotation = Quaternion.identity;
            v.transform.localScale = scale;
            visualItems[i] = v;
        }
        visualsGenerated = true;
    }

    private static float Safe(float v) => Mathf.Approximately(v, 0f) ? 1f : v;

    // ================= Animation nắp thùng =================
    private IEnumerator OpenFlapsAnimation()
    {
        float elapsed = 0f;
        float duration = 0.4f;

        Quaternion f1Start = flap1 != null ? flap1.localRotation : Quaternion.identity;
        Quaternion f2Start = flap2 != null ? flap2.localRotation : Quaternion.identity;
        Quaternion f3Start = flap3 != null ? flap3.localRotation : Quaternion.identity;
        Quaternion f4Start = flap4 != null ? flap4.localRotation : Quaternion.identity;

        Quaternion f1End = f1Closed * Quaternion.Euler(flap1OpenRot);
        Quaternion f2End = f2Closed * Quaternion.Euler(flap2OpenRot);
        Quaternion f3End = f3Closed * Quaternion.Euler(flap3OpenRot);
        Quaternion f4End = f4Closed * Quaternion.Euler(flap4OpenRot);

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            if (flap1 != null) flap1.localRotation = Quaternion.Lerp(f1Start, f1End, progress);
            if (flap2 != null) flap2.localRotation = Quaternion.Lerp(f2Start, f2End, progress);
            if (flap3 != null) flap3.localRotation = Quaternion.Lerp(f3Start, f3End, progress);
            if (flap4 != null) flap4.localRotation = Quaternion.Lerp(f4Start, f4End, progress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flap1 != null) flap1.localRotation = f1End;
        if (flap2 != null) flap2.localRotation = f2End;
        if (flap3 != null) flap3.localRotation = f3End;
        if (flap4 != null) flap4.localRotation = f4End;
    }

    private IEnumerator CloseFlapsAnimation()
    {
        float elapsed = 0f;
        float duration = 0.4f;

        Quaternion f1Start = flap1 != null ? flap1.localRotation : Quaternion.identity;
        Quaternion f2Start = flap2 != null ? flap2.localRotation : Quaternion.identity;
        Quaternion f3Start = flap3 != null ? flap3.localRotation : Quaternion.identity;
        Quaternion f4Start = flap4 != null ? flap4.localRotation : Quaternion.identity;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            if (flap1 != null) flap1.localRotation = Quaternion.Lerp(f1Start, f1Closed, progress);
            if (flap2 != null) flap2.localRotation = Quaternion.Lerp(f2Start, f2Closed, progress);
            if (flap3 != null) flap3.localRotation = Quaternion.Lerp(f3Start, f3Closed, progress);
            if (flap4 != null) flap4.localRotation = Quaternion.Lerp(f4Start, f4Closed, progress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flap1 != null) flap1.localRotation = f1Closed;
        if (flap2 != null) flap2.localRotation = f2Closed;
        if (flap3 != null) flap3.localRotation = f3Closed;
        if (flap4 != null) flap4.localRotation = f4Closed;
    }
}
