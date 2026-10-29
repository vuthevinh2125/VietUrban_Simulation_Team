using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tồn kho. Không lưu con số riêng (nên không bao giờ bị lệch): mỗi lần hỏi
/// nó đếm trực tiếp từ các kệ (Shelf) và thùng hàng (Interactable) đang có trong scene.
/// Kệ và thùng tự đăng ký khi bật / huỷ đăng ký khi tắt.
/// </summary>
public static class StockManager
{
    private static readonly List<Shelf> shelves = new List<Shelf>();
    private static readonly List<Interactable> boxes = new List<Interactable>();

    /// <summary>Gọi mỗi khi số lượng hàng thay đổi (UI tab Kho lắng nghe để vẽ lại).</summary>
    public static event Action OnStockChanged;

    public static IReadOnlyList<Shelf> Shelves => shelves;
    public static IReadOnlyList<Interactable> Boxes => boxes;

    // ---------- Đăng ký ----------
    public static void Register(Shelf shelf)
    {
        if (shelf != null && !shelves.Contains(shelf)) { shelves.Add(shelf); NotifyChanged(); }
    }

    public static void Unregister(Shelf shelf)
    {
        if (shelves.Remove(shelf)) NotifyChanged();
    }

    public static void Register(Interactable box)
    {
        if (box != null && !boxes.Contains(box)) { boxes.Add(box); NotifyChanged(); }
    }

    public static void Unregister(Interactable box)
    {
        if (boxes.Remove(box)) NotifyChanged();
    }

    public static void NotifyChanged() => OnStockChanged?.Invoke();

    // ---------- Truy vấn ----------
    public static int GetShelfCount(ItemData item)
    {
        int total = 0;
        foreach (Shelf s in shelves)
            if (s != null && s.CurrentItem == item) total += s.Count;
        return total;
    }

    public static int GetShelfCapacity(ItemData item)
    {
        int total = 0;
        foreach (Shelf s in shelves)
            if (s != null && s.CurrentItem == item) total += s.Capacity;
        return total;
    }

    public static int GetBoxCount(ItemData item)
    {
        int total = 0;
        foreach (Interactable b in boxes)
            if (b != null && b.itemData == item) total += b.productCount;
        return total;
    }

    public static int GetTotalCount(ItemData item) => GetShelfCount(item) + GetBoxCount(item);

    /// <summary>Tất cả mặt hàng đang có trong cửa hàng (trên kệ hoặc trong thùng).</summary>
    public static List<ItemData> GetKnownItems()
    {
        var result = new List<ItemData>();
        foreach (Shelf s in shelves)
            if (s != null && s.CurrentItem != null && !result.Contains(s.CurrentItem)) result.Add(s.CurrentItem);
        foreach (Interactable b in boxes)
            if (b != null && b.itemData != null && !result.Contains(b.itemData)) result.Add(b.itemData);
        return result;
    }

    /// <summary>Một kệ ngẫu nhiên còn hàng (cho khách NPC). item = null nghĩa là loại nào cũng được.</summary>
    public static Shelf GetRandomStockedShelf(ItemData item = null)
    {
        var candidates = new List<Shelf>();
        foreach (Shelf s in shelves)
            if (s != null && !s.IsEmpty && (item == null || s.CurrentItem == item)) candidates.Add(s);
        return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shelves.Clear();
        boxes.Clear();
        OnStockChanged = null;
    }
}
