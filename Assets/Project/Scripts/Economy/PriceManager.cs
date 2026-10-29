using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Giá BÁN của từng mặt hàng (người chơi tự đặt trên máy tính).
/// Chưa đặt thì dùng giá thị trường ItemData.MarketPrice.
/// </summary>
public static class PriceManager
{
    private static readonly Dictionary<ItemData, float> sellPrices = new Dictionary<ItemData, float>();

    public static event Action<ItemData, float> OnPriceChanged;

    public static float GetSellPrice(ItemData item)
    {
        if (item == null) return 0f;
        return sellPrices.TryGetValue(item, out float price) ? price : item.MarketPrice;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sellPrices.Clear();
        OnPriceChanged = null;
    }
}
