using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Giá BÁN của từng mặt hàng (người chơi đặt bằng nhãn giá trên kệ).
/// Chưa đặt thì dùng giá thị trường HÔM NAY (MarketPrices, dao động theo ngày).
/// Cũng quyết định khách phản ứng với giá ra sao (BuyChance).
/// </summary>
public static class PriceManager
{
    private static readonly Dictionary<ItemData, float> sellPrices = new Dictionary<ItemData, float>();

    public static event Action<ItemData, float> OnPriceChanged;

    /// <summary>Giá / giá thị trường dưới mức này thì khách thấy "rẻ quá" và lấy thêm.</summary>
    public const float CheapRatio = 0.85f;
    /// <summary>Giá / giá thị trường vượt mức này thì không ai mua.</summary>
    public const float MaxRatio = 1.5f;

    public static float GetSellPrice(ItemData item)
    {
        if (item == null) return 0f;
        return sellPrices.TryGetValue(item, out float price) ? price : MarketPrices.Get(item);
    }

    public static void SetSellPrice(ItemData item, float price)
    {
        if (item == null) return;
        price = Mathf.Max(0f, Mathf.Round(price * 100f) / 100f);
        sellPrices[item] = price;
        OnPriceChanged?.Invoke(item, price);
    }

    public static bool HasCustomPrice(ItemData item) => item != null && sellPrices.ContainsKey(item);

    /// <summary>Lãi trên mỗi món = giá bán - giá nhập.</summary>
    public static float GetProfitPerUnit(ItemData item) => item == null ? 0f : GetSellPrice(item) - item.unitPrice;

    // ================= Lưu / nạp =================
    public static IReadOnlyDictionary<ItemData, float> AllCustomPrices => sellPrices;

    public static void ClearCustomPrices() => sellPrices.Clear();

    // ================= Khách phản ứng với giá =================
    public static float PriceRatio(ItemData item) => PriceRatio(item, GetSellPrice(item));

    public static float PriceRatio(ItemData item, float price)
    {
        if (item == null) return 1f;
        float market = Mathf.Max(0.01f, MarketPrices.Get(item));
        return price / market;
    }

    public static float BuyChance(ItemData item) => BuyChance(item, GetSellPrice(item));

    /// <summary>
    /// Xác suất khách chịu mua theo giá:
    /// bằng/rẻ hơn giá thị trường: 100% · đắt hơn 20%: 80% · đắt hơn 50%: 15% · đắt hơn nữa: không ai mua.
    /// </summary>
    public static float BuyChance(ItemData item, float price, float maxRatio = MaxRatio)
    {
        float r = PriceRatio(item, price);
        if (r <= 1f) return 1f;
        if (r <= 1.2f) return Mathf.Lerp(1f, 0.8f, (r - 1f) / 0.2f);
        if (r <= maxRatio) return Mathf.Lerp(0.8f, 0.15f, (r - 1.2f) / Mathf.Max(0.01f, maxRatio - 1.2f));
        return 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sellPrices.Clear();
        OnPriceChanged = null;
    }
}
