using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Một giấy phép mặt hàng: mở khoá một nhóm sản phẩm trên máy tính.</summary>
public class ProductLicense
{
    public string id;
    public string name;
    public int requiredLevel;
    public float price;
    public string[] keys; // tiền tố tên prefab sản phẩm thuộc nhóm này

    public ProductLicense(string id, string name, int level, float price, params string[] keys)
    {
        this.id = id; this.name = name; requiredLevel = level; this.price = price; this.keys = keys;
    }
}

/// <summary>
/// Giấy phép mặt hàng (giống Supermarket Simulator): đầu game chỉ bán được nhóm hàng cơ bản,
/// muốn bán thêm nhóm khác thì đạt đủ cấp cửa hàng và mua giấy phép trên máy tính.
/// Mặt hàng chưa có giấy phép sẽ không hiện trên máy tính.
/// </summary>
public static class LicenseManager
{
    public const string StarterId = "basic";

    public static readonly List<ProductLicense> All = new List<ProductLicense>
    {
        new ProductLicense(StarterId, "Basic Groceries", 1, 0f,
            "Cereals", "Pasta", "RicePack", "FlourPack", "Sugar", "Salt", "WaterBottle", "Soda", "SliceBread", "SlicedBread", "TinCan"),
        new ProductLicense("snacks", "Snacks & Sweets", 2, 200f,
            "Chips", "CandyPack", "SweetJar", "ChocolateSpread", "ChocolateBox", "Cake"),
        new ProductLicense("pantry", "Pantry", 3, 300f,
            "Oil", "Sauce", "Pepper", "MashPotatoe", "PotatoeBag", "OldMetalBox"),
        new ProductLicense("drinks", "Hot & Cold Drinks", 4, 400f,
            "JuiceBrick", "MilkBrick", "Milk", "WaterPack", "CansPack", "Coffee", "Capsule", "Teabox", "ChocolatePowder"),
        new ProductLicense("cleaning", "Cleaning", 5, 500f,
            "Bleach", "Cleanser", "Laundry", "PaperTowel", "ToiletPaper"),
        new ProductLicense("care", "Personal Care", 6, 600f,
            "Shampoo", "Soap", "Toothpaste"),
        new ProductLicense("dairy", "Dairy & Deli", 7, 800f,
            "Butter", "Cheese", "Cream", "Ham", "CrabSticks", "EggBox", "Yoghurt"),
        new ProductLicense("pet", "Pet Supplies", 8, 700f,
            "PetFood"),
        new ProductLicense("frozen", "Frozen & Meat", 9, 1200f,
            "FrozenPack", "Frozen", "Pizza", "Sushis", "Chicken", "MeatTray"),
        new ProductLicense("alcohol", "Alcohol", 10, 1500f,
            "BeerKeg", "BeerPack", "Beer", "Alcohol", "WineBottle"),
    };

    private static readonly HashSet<string> owned = new HashSet<string> { StarterId };
    private static readonly Dictionary<ItemData, ProductLicense> cache = new Dictionary<ItemData, ProductLicense>();
    private static readonly Regex ImportedName = new Regex(@"^[A-Za-z0-9]+_[A-Za-z0-9]+");

    public static event Action OnLicensesChanged;

    public static bool Owns(ProductLicense l) => l != null && owned.Contains(l.id);

    /// <summary>Giấy phép của mặt hàng (khớp theo tên prefab gốc lưu trong ItemData.description, khoá dài khớp trước).</summary>
    public static ProductLicense LicenseOf(ItemData item)
    {
        if (item == null) return All[0];
        if (cache.TryGetValue(item, out ProductLicense hit)) return hit;

        ProductLicense best = All[0];
        string src = item.description ?? "";
        if (ImportedName.IsMatch(src)) // hàng nhập từ gói ModernSupermarket (tên kiểu "Chips_w4tja8")
        {
            int bestLen = 0;
            foreach (ProductLicense l in All)
                foreach (string k in l.keys)
                    if (k.Length > bestLen && src.StartsWith(k, StringComparison.OrdinalIgnoreCase)) { best = l; bestLen = k.Length; }
        }
        // Mặt hàng cũ tự làm (CocoCereals, Milk...) thuộc nhóm cơ bản
        cache[item] = best;
        return best;
    }

    public static bool IsUnlocked(ItemData item) => Owns(LicenseOf(item));

    public static bool CanBuy(ProductLicense l, out string reason)
    {
        reason = "";
        if (l == null) return false;
        if (Owns(l)) { reason = "Owned"; return false; }
        if (StoreProgress.Level < l.requiredLevel) { reason = $"Requires store level {l.requiredLevel}"; return false; }
        if (EconomyManager.Instance == null || !EconomyManager.Instance.CanAfford(l.price)) { reason = "Not enough money"; return false; }
        return true;
    }

    public static bool TryBuy(ProductLicense l)
    {
        if (!CanBuy(l, out _)) return false;
        if (!EconomyManager.Instance.TrySpend(l.price, MoneyCategory.Upgrades, $"License: {l.name}")) return false;
        owned.Add(l.id);
        Debug.Log($"[License] Đã mua giấy phép {l.name}.");
        OnLicensesChanged?.Invoke();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        owned.Clear();
        owned.Add(StarterId);
        cache.Clear();
        OnLicensesChanged = null;
    }
}
