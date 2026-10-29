using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Giá thị trường dao động theo ngày (giống Supermarket Simulator): mỗi sáng giá thị trường của từng mặt hàng
/// lên / xuống vài %, trong khoảng 85% - 120% giá gốc. Khách so giá kệ với giá thị trường HÔM NAY,
/// nên người chơi phải theo dõi và chỉnh giá.
/// </summary>
public static class MarketPrices
{
    public const float MinFactor = 0.85f, MaxFactor = 1.2f, DailySwing = 0.06f;

    private static readonly Dictionary<ItemData, float> factor = new Dictionary<ItemData, float>();
    private static readonly Dictionary<ItemData, float> previous = new Dictionary<ItemData, float>();

    public static event Action OnChanged;

    /// <summary>Giá thị trường hôm nay của 1 món.</summary>
    public static float Get(ItemData item)
    {
        if (item == null) return 0f;
        float f = factor.TryGetValue(item, out float v) ? v : 1f;
        return Mathf.Round(item.MarketPrice * f * 100f) / 100f;
    }

    /// <summary>So với hôm qua: +1 tăng, -1 giảm, 0 không đổi.</summary>
    public static int Trend(ItemData item)
    {
        if (item == null) return 0;
        float now = factor.TryGetValue(item, out float a) ? a : 1f;
        float before = previous.TryGetValue(item, out float b) ? b : now;
        if (now > before + 0.001f) return 1;
        if (now < before - 0.001f) return -1;
        return 0;
    }

    /// <summary>Sang ngày mới: mọi mặt hàng đang bán (có trên máy tính) đổi giá ngẫu nhiên.</summary>
    public static void RollNewDay()
    {
        var all = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (ItemData item in all)
        {
            if (item == null) continue;
            float f = factor.TryGetValue(item, out float v) ? v : 1f;
            previous[item] = f;
            f *= 1f + UnityEngine.Random.Range(-DailySwing, DailySwing);
            // kéo nhẹ về giá gốc để không trôi mãi về một phía
            f = Mathf.Lerp(f, 1f, 0.1f);
            factor[item] = Mathf.Clamp(f, MinFactor, MaxFactor);
        }
        OnChanged?.Invoke();
    }

    // ================= Lưu / nạp =================
    [Serializable] public class Entry { public string item; public float factor; public float previous; }

    public static List<Entry> Export()
    {
        var list = new List<Entry>();
        foreach (var kv in factor)
            if (kv.Key != null)
                list.Add(new Entry { item = kv.Key.name, factor = kv.Value, previous = previous.TryGetValue(kv.Key, out float p) ? p : kv.Value });
        return list;
    }

    public static void Import(List<Entry> entries, Dictionary<string, ItemData> items)
    {
        factor.Clear();
        previous.Clear();
        if (entries != null)
            foreach (Entry e in entries)
                if (e != null && items.TryGetValue(e.item, out ItemData it))
                {
                    factor[it] = Mathf.Clamp(e.factor, MinFactor, MaxFactor);
                    previous[it] = Mathf.Clamp(e.previous, MinFactor, MaxFactor);
                }
        OnChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        factor.Clear();
        previous.Clear();
        OnChanged = null;
    }
}
