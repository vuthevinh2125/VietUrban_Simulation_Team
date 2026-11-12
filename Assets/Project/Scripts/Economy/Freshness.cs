using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hạn dùng của hàng tươi (tính bằng NGÀY GAME, đếm từ lúc món được xếp lên kệ).
/// Hàng trong thùng / trên kệ kho chưa tính (coi như còn niêm phong, để lạnh) - chỉ hàng bày bán mới bắt đầu hỏng.
///  - Thịt, gà, cá, sushi, thanh cua: 2 ngày
///  - Rau quả mềm (chuối, cà chua, nấm, bơ, xoài, đu đủ, kiwi, ớt, bí ngòi, atisô), bánh mì, bánh ngọt: 3 ngày
///  - Táo, lê, cam, chanh, bưởi, dưa, dứa, bí, cà rốt: 5 ngày
///  - Khoai tây tươi, hành, tỏi, bí ngô: 8 ngày
///  - Sữa tươi, sữa chua, kem tươi, giăm bông: 4 ngày
///  - Phô mai, trứng, bơ: 6 ngày
///  - Còn lại (đồ khô, đồ hộp, sữa hộp tiệt trùng, nước, đồ đông lạnh, gia dụng...): không hỏng
/// ShelfLifeDays(string) là hàm thuần để viết test EditMode.
/// </summary>
public static class Freshness
{
    private static readonly Dictionary<ItemData, float> cache = new Dictionary<ItemData, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    public static float ShelfLifeDays(ItemData item)
    {
        if (item == null) return 0f;
        if (cache.TryGetValue(item, out float d)) return d;
        d = ShelfLifeDays(string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName);
        cache[item] = d;
        return d;
    }

    public static float ShelfLifeDays(string itemName)
    {
        string n = (itemName ?? "").ToLowerInvariant().Replace('_', ' ');
        if (Has(n, "shampoo", "frozen", "pizza", "brick", "powder", "spread")) return 0f; // "shampoo" chứa "ham"; sữa hộp, bột, đồ đông lạnh không hỏng
        if (Has(n, "meat", "chicken", "fish", "sushi", "crab")) return 2f;
        if (Has(n, "fresh potato", "onion", "garlic", "pumpkin")) return 8f;
        if (Has(n, "banana", "tomato", "mushroom", "avocado", "mango", "papaya", "kiwi", "chilli", "zucchini", "artichoke")) return 3f;
        if (Has(n, "apple", "pear", "clementine", "lemon", "grapefruit", "melon", "squash", "carrot")) return 5f;
        if (Has(n, "bread", "cake", "apple", "banana", "fruit", "vegetable", "salad")) return 3f;
        if (Has(n, "milk", "yoghurt", "yogurt", "cream", "ham")) return 4f;
        if (Has(n, "cheese", "egg", "butter")) return 6f;
        return 0f;
    }

    /// <summary>Thời điểm hiện tại tính bằng ngày game (ngày 3 lúc 12:00 = 3.5).</summary>
    public static double Now
    {
        get
        {
            GameManager gm = GameManager.Instance;
            return gm != null ? gm.currentDay + gm.ClockHours / 24.0 : Time.time / 600.0;
        }
    }

    /// <summary>Chữ ngắn cho thẻ sản phẩm: "2d" = tươi 2 ngày (trống nếu không hỏng).</summary>
    public static string CardTag(ItemData item)
    {
        float d = ShelfLifeDays(item);
        return d > 0f ? $"{d:0}d" : ""; // ngắn để vừa thẻ sản phẩm (vd. "3d" = tươi 3 ngày)
    }

    private static bool Has(string s, params string[] keys)
    {
        foreach (string k in keys) if (s.Contains(k)) return true;
        return false;
    }
}
