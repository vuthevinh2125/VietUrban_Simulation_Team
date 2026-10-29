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

    private GameObject[] occupied;   // occupied[i] = món đang nằm ở slots[i]
    private ItemData currentItem;

    // ================= Thông tin =================
    public ItemData CurrentItem => assignedItem != null ? assignedItem : currentItem;
    public int Capacity => slots != null ? slots.Length : 0;
    public bool IsFull => Count >= Capacity;
    public bool IsEmpty => Count == 0;
    public string DisplayName => CurrentItem != null ? CurrentItem.itemName : "Empty";

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

    void Awake() => EnsureArray();
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

        // Bù trừ độ méo scale của kệ để món hàng giữ đúng kích thước (nhân thêm shelfScale)
        Vector3 wanted = (item != null && item.shelfScale != Vector3.zero) ? item.shelfScale : Vector3.one;
        Vector3 ls = slot.lossyScale;
        product.transform.localScale = new Vector3(
            wanted.x / SafeScale(ls.x),
            wanted.y / SafeScale(ls.y),
            wanted.z / SafeScale(ls.z));

        Rigidbody prb = product.GetComponent<Rigidbody>();
        if (prb != null) prb.isKinematic = true;

        occupied[index] = product;
        if (assignedItem == null && currentItem == null) currentItem = item;

        StartCoroutine(MoveItemSmoothly(product.transform, startPoint, placeAnimDuration));
        StockManager.NotifyChanged();
        return true;
    }

    /// <summary>Lấy món ở slot cuối cùng ra khỏi kệ (người chơi bỏ lại vào thùng).</summary>
    public bool TryTakeProduct(out GameObject product)
    {
        product = null;
        int index = LastUsedSlot();
        if (index < 0) return false;

        product = occupied[index];
        occupied[index] = null;
        product.transform.SetParent(null);
        product.transform.localScale = Vector3.one;

        if (IsEmpty && assignedItem == null) currentItem = null;
        StockManager.NotifyChanged();
        return true;
    }

    /// <summary>Khách lấy 1 món: món bị xoá khỏi kệ, trả về loại hàng để tính tiền.</summary>
    public bool TryTakeForCustomer(out ItemData item)
    {
        item = CurrentItem;
        if (!TryTakeProduct(out GameObject product)) { item = null; return false; }
        Destroy(product);
        return true;
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
            occupied = new GameObject[Capacity];
            if (old != null)
                for (int i = 0; i < Mathf.Min(old.Length, occupied.Length); i++) occupied[i] = old[i];
        }
    }

    private int FirstFreeSlot()
    {
        EnsureArray();
        for (int i = 0; i < occupied.Length; i++) if (occupied[i] == null) return i;
        return -1;
    }

    private int LastUsedSlot()
    {
        EnsureArray();
        for (int i = occupied.Length - 1; i >= 0; i--) if (occupied[i] != null) return i;
        return -1;
    }

    private static float SafeScale(float v) => Mathf.Approximately(v, 0f) ? 1f : v;

    private IEnumerator MoveItemSmoothly(Transform item, Transform startPoint, float duration)
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
            item.localPosition = Vector3.Lerp(startLocalPos, Vector3.zero, t);
            item.localRotation = Quaternion.Lerp(startLocalRot, Quaternion.identity, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (item == null) yield break;
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
    }
}
