using UnityEngine;
using System.Collections;

/// <summary>
/// Kệ hàng. Mỗi kệ chỉ chứa MỘT loại hàng: khoá theo món đầu tiên được xếp lên
/// (hoặc theo assignedItem nếu gán sẵn), tự mở khoá khi kệ trống.
/// </summary>
public class Shelf : MonoBehaviour
{
    [Header("Shelf Slots")]
    public Transform[] slots;

    [Header("Product Type")]
    [Tooltip("Để trống = kệ nhận mọi loại hàng, khoá theo món đầu tiên được xếp lên.")]
    public ItemData assignedItem;
    [Tooltip("Để trống = nhận mọi loại. Ví dụ \"Fresh\" thì chỉ hàng có displayType = Fresh mới xếp được.")]
    public string acceptsDisplayType = "";

    [Header("Animation")]
    public float placeAnimDuration = 0.25f;

    [Header("Auto Fit (tự phóng món hàng cho vừa ô kệ)")]
    public bool autoFitProducts = true;
    [Tooltip("Món hàng chiếm bao nhiêu phần bề ngang/chiều sâu của một ô")]
    [Range(0.5f, 1f)] public float fillRatio = 0.9f;
    [Tooltip("Món hàng cao tối đa bao nhiêu phần khoảng cách giữa 2 tầng kệ")]
    [Range(0.3f, 1f)] public float heightRatio = 0.8f;
    public float minFitScale = 0.6f;
    public float maxFitScale = 2.5f;

    private GameObject[] occupied;   // occupied[i] = món đang nằm ở slots[i]
    private double[] placedAt;       // thời điểm (ngày game, Freshness.Now) món ở slots[i] được xếp lên kệ
    private bool[] spoiled;          // món ở slots[i] đã quá hạn (hàng tươi)
    private ItemData currentItem;
    private Vector3 cell;            // kích thước 1 ô (x: ngang, y: khoảng tầng, z: sâu), mét
    private bool cellReady;

    // ================= Thông tin =================
    public ItemData CurrentItem => assignedItem != null ? assignedItem : currentItem;
    public int Capacity => slots != null ? slots.Length : 0;
    public bool IsFull => Count >= Capacity;
    public bool IsEmpty => Count == 0;
    public string DisplayName => CurrentItem != null ? CurrentItem.itemName : "Empty";

    // ================= Hạn dùng (hàng tươi) =================
    /// <summary>Số ngày hàng trên kệ còn tươi (0 = không hỏng).</summary>
    public float ShelfLife => Freshness.ShelfLifeDays(CurrentItem);
    public bool Perishable => ShelfLife > 0f;

    /// <summary>Số món đã quá hạn đang nằm trên kệ.</summary>
    public int SpoiledCount
    {
        get
        {
            EnsureArray();
            int n = 0;
            for (int i = 0; i < occupied.Length; i++) if (occupied[i] != null && spoiled[i]) n++;
            return n;
        }
    }

    /// <summary>Số món khách còn mua được (chưa quá hạn).</summary>
    public int SellableCount => Count - SpoiledCount;

    /// <summary>Số ngày còn lại của món cũ nhất chưa hỏng (-1 nếu không có / không hỏng).</summary>
    public float DaysLeft(double now)
    {
        float life = ShelfLife;
        int i = OldestFreshSlot();
        if (life <= 0f || i < 0) return -1f;
        return Mathf.Max(0f, life - (float)(now - placedAt[i]));
    }

    /// <summary>Đánh dấu các món vừa quá hạn (đổi màu); trả về số món vừa hỏng.</summary>
    public int UpdateFreshness(double now)
    {
        float life = ShelfLife;
        if (life <= 0f) return 0;
        EnsureArray();
        int n = 0;
        for (int i = 0; i < occupied.Length; i++)
        {
            if (occupied[i] == null || spoiled[i] || now - placedAt[i] < life) continue;
            spoiled[i] = true;
            TintSpoiled(occupied[i]);
            n++;
        }
        if (n > 0) StockManager.NotifyChanged();
        return n;
    }

    /// <summary>Vứt bỏ mọi món đã quá hạn; trả về số món đã vứt.</summary>
    public int RemoveSpoiled()
    {
        EnsureArray();
        int n = 0;
        for (int i = 0; i < occupied.Length; i++)
        {
            if (occupied[i] == null || !spoiled[i]) continue;
            Destroy(occupied[i]);
            occupied[i] = null;
            spoiled[i] = false;
            n++;
        }
        if (n > 0)
        {
            if (IsEmpty && assignedItem == null) currentItem = null;
            StockManager.NotifyChanged();
        }
        return n;
    }

    /// <summary>Tuổi (ngày) của từng món theo thứ tự ô, để lưu file.</summary>
    public System.Collections.Generic.List<float> GetAges(double now)
    {
        EnsureArray();
        var ages = new System.Collections.Generic.List<float>();
        for (int i = 0; i < occupied.Length; i++) if (occupied[i] != null) ages.Add((float)(now - placedAt[i]));
        return ages;
    }

    /// <summary>Đặt lại tuổi từng món sau khi nạp file (cùng thứ tự ô với GetAges).</summary>
    public void SetAges(System.Collections.Generic.List<float> ages, double now)
    {
        if (ages == null) return;
        EnsureArray();
        int k = 0;
        for (int i = 0; i < occupied.Length && k < ages.Count; i++)
        {
            if (occupied[i] == null) continue;
            placedAt[i] = now - ages[k++];
        }
        UpdateFreshness(now);
    }

    /// <summary>Thử nghiệm (phím F10): làm mọi món trên kệ già thêm days ngày.</summary>
    public void DebugAge(float days)
    {
        EnsureArray();
        for (int i = 0; i < occupied.Length; i++) if (occupied[i] != null) placedAt[i] -= days;
    }

    private int OldestFreshSlot()
    {
        EnsureArray();
        int best = -1;
        for (int i = 0; i < occupied.Length; i++)
            if (occupied[i] != null && !spoiled[i] && (best < 0 || placedAt[i] < placedAt[best])) best = i;
        return best;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

    /// <summary>Món hỏng: tối màu, ngả nâu (MaterialPropertyBlock, không tạo material mới).</summary>
    private static void TintSpoiled(GameObject product)
    {
        var block = new MaterialPropertyBlock();
        Color tint = new Color(0.55f, 0.45f, 0.3f);
        foreach (Renderer r in product.GetComponentsInChildren<Renderer>())
        {
            Material m = r.sharedMaterial;
            Color baseC = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m != null && m.HasProperty(ColorId) ? m.color : Color.white;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseC * tint);
            block.SetColor(ColorId, baseC * tint);
            r.SetPropertyBlock(block);
        }
    }

    public int Count
    {
        get
        {
            EnsureArray();
            int n = 0;
            for (int i = 0; i < occupied.Length; i++) if (occupied[i] != null) n++;
            return n;
        }
    }

    [Header("Price label")]
    [Tooltip("Tự tạo nhãn giá ở mép trước mỗi tầng kệ khi chơi")]
    public bool createPriceLabels = true;

    void Awake() => EnsureArray();

    void Start()
    {
        if (createPriceLabels && Capacity > 0 && GetComponent<ShelfPriceLabels>() == null)
            gameObject.AddComponent<ShelfPriceLabels>();
    }
    void OnEnable() => StockManager.Register(this);
    void OnDisable() => StockManager.Unregister(this);

    // ================= Kiểm tra =================
    public bool CanPlace(ItemData item, out string reason)
    {
        reason = "";
        if (Capacity == 0) { reason = "Shelf has no slots"; return false; }
        if (IsFull) { reason = "Shelf is full"; return false; }
        if (CurrentItem != null && item != CurrentItem) { reason = $"This shelf is for {CurrentItem.itemName}"; return false; }
        if (item != null && !string.IsNullOrEmpty(acceptsDisplayType) &&
            !string.Equals(item.displayType, acceptsDisplayType, System.StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Only {acceptsDisplayType} products here";
            return false;
        }
        return true;
    }

    public bool CanTakeInto(Interactable box, out string reason)
    {
        reason = "";
        if (box == null) return false;
        if (IsEmpty) { reason = "Shelf is empty"; return false; }
        if (SellableCount <= 0) { reason = "Only expired items left"; return false; }
        if (box.IsFull) { reason = "Box is full"; return false; }
        if (box.itemData != CurrentItem && !(box.itemData == null && box.IsEmpty))
        {
            reason = $"Box holds {box.DisplayName}";
            return false;
        }
        return true;
    }

    // ================= Thêm / lấy hàng =================
    /// <summary>Đặt 1 món lên slot trống đầu tiên, bay từ startPoint (tay người chơi) tới slot.</summary>
    public bool TryAddProduct(GameObject product, Transform startPoint, ItemData item)
    {
        if (product == null || !CanPlace(item, out _)) return false;

        int index = FirstFreeSlot();
        if (index < 0 || slots[index] == null) return false;

        Transform slot = slots[index];
        product.transform.SetParent(slot);

        // Kích thước mong muốn = shelfScale x hệ số cho vừa ô; bù trừ độ méo scale của kệ
        Vector3 wanted = (item != null && item.shelfScale != Vector3.zero) ? item.shelfScale : Vector3.one;
        product.transform.localRotation = Quaternion.identity;
        Bounds lb = ProductFit.LocalBounds(product.transform);
        float k = autoFitProducts ? FitFactor(Vector3.Scale(lb.size, wanted)) : 1f;
        Vector3 final = wanted * k;
        Vector3 ls = slot.lossyScale;
        product.transform.localScale = new Vector3(
            final.x / SafeScale(ls.x),
            final.y / SafeScale(ls.y),
            final.z / SafeScale(ls.z));

        // Ô kệ nằm ở mặt kệ: đặt đáy món hàng chạm mặt kệ, căn giữa ô
        Vector3 target = new Vector3(
            -lb.center.x * final.x / SafeScale(ls.x),
            -lb.min.y * final.y / SafeScale(ls.y),
            -lb.center.z * final.z / SafeScale(ls.z));

        Rigidbody prb = product.GetComponent<Rigidbody>();
        if (prb != null) prb.isKinematic = true;

        occupied[index] = product;
        placedAt[index] = Freshness.Now;
        spoiled[index] = false;
        if (assignedItem == null && currentItem == null) currentItem = item;

        StartCoroutine(MoveItemSmoothly(product.transform, startPoint, target, placeAnimDuration));
        StockManager.NotifyChanged();
        return true;
    }

    /// <summary>Lấy món ở slot cuối cùng ra khỏi kệ (người chơi bỏ lại vào thùng).</summary>
    public bool TryTakeProduct(out GameObject product)
    {
        product = null;
        int index = LastUsedSlot(skipSpoiled: true); // món hỏng không lấy lại vào thùng được (chỉ vứt đi)
        if (index < 0) return false;

        product = occupied[index];
        occupied[index] = null;
        spoiled[index] = false;
        product.transform.SetParent(null);
        product.transform.localScale = Vector3.one;

        if (IsEmpty && assignedItem == null) currentItem = null;
        StockManager.NotifyChanged();
        return true;
    }

    /// <summary>Khách lấy 1 món: món bị xoá khỏi kệ, trả về loại hàng để tính tiền.</summary>
    /// <summary>Lấy món CŨ NHẤT chưa hỏng (xoay vòng hàng kiểu FIFO), món hỏng thì khách không lấy.</summary>
    public bool TryTakeForCustomer(out ItemData item)
    {
        item = CurrentItem;
        int index = OldestFreshSlot();
        if (index < 0) { item = null; return false; }
        GameObject product = occupied[index];
        occupied[index] = null;
        spoiled[index] = false;
        Destroy(product);
        if (IsEmpty && assignedItem == null) currentItem = null;
        StockManager.NotifyChanged();
        return true;
    }

    // ---------- Lưu / nạp ----------
    /// <summary>Xoá sạch hàng trên kệ (dùng khi nạp file lưu).</summary>
    public void ClearAll()
    {
        EnsureArray();
        for (int i = 0; i < occupied.Length; i++)
        {
            if (occupied[i] != null) Destroy(occupied[i]);
            occupied[i] = null;
            spoiled[i] = false;
        }
        if (assignedItem == null) currentItem = null;
        StockManager.NotifyChanged();
    }

    /// <summary>Đặt ngay count món của item lên kệ (không cần người chơi xếp).</summary>
    public int FillInstant(ItemData item, int count)
    {
        if (item == null || count <= 0) return 0;
        GameObject prefab = item.ResolveProductPrefab();
        if (prefab == null) return 0;
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            int index = FirstFreeSlot();
            if (index < 0 || slots[index] == null) break;
            GameObject product = Instantiate(prefab, slots[index].position, slots[index].rotation);
            if (!TryAddProduct(product, slots[index], item)) { Destroy(product); break; }
            placed++;
        }
        return placed;
    }

    // ---------- Giữ tương thích với code cũ ----------
    public Transform GetAvailableSlot()
    {
        int i = FirstFreeSlot();
        return i >= 0 ? slots[i] : null;
    }

    public void AddItemToShelf(GameObject product, Transform startPoint, ItemData itemData)
    {
        if (!TryAddProduct(product, startPoint, itemData)) Destroy(product);
    }

    public GameObject RemoveItemFromShelf() => TryTakeProduct(out GameObject p) ? p : null;

    // ================= Internal =================
    private void EnsureArray()
    {
        if (occupied == null || occupied.Length != Capacity)
        {
            var old = occupied;
            var oldT = placedAt; var oldS = spoiled;
            occupied = new GameObject[Capacity];
            placedAt = new double[Capacity];
            spoiled = new bool[Capacity];
            if (old != null)
                for (int i = 0; i < Mathf.Min(old.Length, occupied.Length); i++)
                {
                    occupied[i] = old[i];
                    if (oldT != null && i < oldT.Length) placedAt[i] = oldT[i];
                    if (oldS != null && i < oldS.Length) spoiled[i] = oldS[i];
                }
        }
    }

    private int FirstFreeSlot()
    {
        EnsureArray();
        for (int i = 0; i < occupied.Length; i++) if (occupied[i] == null) return i;
        return -1;
    }

    private int LastUsedSlot(bool skipSpoiled = false)
    {
        EnsureArray();
        for (int i = occupied.Length - 1; i >= 0; i--) if (occupied[i] != null && !(skipSpoiled && spoiled[i])) return i;
        return -1;
    }

    /// <summary>Hệ số phóng để món hàng (kích thước size, mét) vừa một ô kệ.</summary>
    private float FitFactor(Vector3 size)
    {
        EnsureCell();
        float k = Mathf.Min(
            cell.x * fillRatio / Mathf.Max(0.001f, size.x),
            cell.z * fillRatio / Mathf.Max(0.001f, size.z),
            cell.y * heightRatio / Mathf.Max(0.001f, size.y));
        return Mathf.Clamp(k, minFitScale, maxFitScale);
    }

    /// <summary>Đo khoảng cách giữa các ô cạnh nhau (lấy trung vị) để biết 1 ô rộng bao nhiêu.</summary>
    private void EnsureCell()
    {
        if (cellReady) return;
        cellReady = true;
        var xs = new System.Collections.Generic.List<float>();
        var zs = new System.Collections.Generic.List<float>();
        var ys = new System.Collections.Generic.List<float>();
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null) continue;
            Vector3 right = slots[i].right, fwd = slots[i].forward;
            float bx = float.MaxValue, bz = float.MaxValue, by = float.MaxValue;
            for (int j = 0; j < Capacity; j++)
            {
                if (i == j || slots[j] == null) continue;
                Vector3 d = slots[j].position - slots[i].position;
                float dy = Mathf.Abs(d.y);
                float dx = Mathf.Abs(Vector3.Dot(d, right));
                float dz = Mathf.Abs(Vector3.Dot(d, fwd));
                if (dy < 0.05f)
                {
                    if (dz < 0.06f && dx > 0.05f) bx = Mathf.Min(bx, dx);
                    if (dx < 0.06f && dz > 0.05f) bz = Mathf.Min(bz, dz);
                }
                else if (dx < 0.3f && dz < 0.3f) by = Mathf.Min(by, dy);
            }
            if (bx < float.MaxValue) xs.Add(bx);
            if (bz < float.MaxValue) zs.Add(bz);
            if (by < float.MaxValue) ys.Add(by);
        }
        cell = new Vector3(Median(xs, 0.22f), Median(ys, 0.4f), Median(zs, 0.3f));
        cell.y = Mathf.Clamp(cell.y, 0.15f, 0.8f);
    }

    private static float Median(System.Collections.Generic.List<float> v, float fallback)
    {
        if (v.Count == 0) return fallback;
        v.Sort();
        return v[v.Count / 2];
    }

    private static float SafeScale(float v) => Mathf.Approximately(v, 0f) ? 1f : v;

    private IEnumerator MoveItemSmoothly(Transform item, Transform startPoint, Vector3 targetLocal, float duration)
    {
        if (item == null) yield break;

        if (startPoint != null)
        {
            item.position = startPoint.position;
            item.rotation = startPoint.rotation;
        }

        Vector3 startLocalPos = item.localPosition;
        Quaternion startLocalRot = item.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (item == null) yield break; // khách đã lấy mất trong lúc đang bay
            float t = elapsed / duration;
            item.localPosition = Vector3.Lerp(startLocalPos, targetLocal, t);
            item.localRotation = Quaternion.Lerp(startLocalRot, Quaternion.identity, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (item == null) yield break;
        item.localPosition = targetLocal;
        item.localRotation = Quaternion.identity;
    }
}
