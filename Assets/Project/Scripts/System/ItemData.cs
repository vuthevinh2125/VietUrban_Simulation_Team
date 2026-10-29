using UnityEngine;

public enum ItemRarity
{
    Normal,
    Good,
    Rare,
    Epic,
    Legendary
}

/// <summary>Dữ liệu của một MẶT HÀNG (sữa, ngũ cốc...).</summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Supermarket/Product (ItemData)")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    [TextArea(3, 5)]
    public string description;

    public ItemRarity rarity;

    // Không còn dùng (đã bỏ Inventory) - giữ lại để không mất dữ liệu cũ
    [HideInInspector] public bool isStackable;
    [HideInInspector] public int maxStack;

    [Header("Display Settings")]
    public Vector3 itemScale = Vector3.one;  // Tỷ lệ riêng khi hiển thị
    public Vector3 shelfScale = Vector3.one; // Tỷ lệ khi đặt lên kệ (1,1,1 = kích thước gốc)

    [Header("Market UI Settings")]
    public string brand;            // Thương hiệu (Vinamilk, Calbee...)
    public string displayType;      // Loại kệ: "Shelf", "Fresh", "Fridge"...
    [Tooltip("Giá NHẬP 1 món từ nhà cung cấp")]
    public float unitPrice;
    public int itemsPerBox;         // Số món trong 1 thùng
    public int shelfCapacity;       // Sức chứa gợi ý trên kệ

    [Header("Selling Price")]
    [Tooltip("Giá bán lẻ thị trường cho 1 món. Để 0 = tự tính bằng giá nhập x 1.3")]
    public float marketPrice;

    [Header("Market Delivery Settings")]
    public GameObject cratePrefab;  // Thùng hàng được giao khi đặt mua
    [Tooltip("(Tuỳ chọn) Prefab 1 món hàng để xếp lên kệ, dùng khi thùng chưa gán productPrefab")]
    public GameObject productPrefab;

    /// <summary>Giá nhập 1 thùng.</summary>
    public float BoxPrice => unitPrice * Mathf.Max(1, itemsPerBox);

    /// <summary>Giá bán lẻ thị trường cho 1 món.</summary>
    public float MarketPrice => marketPrice > 0f ? marketPrice : Mathf.Round(unitPrice * 1.3f * 100f) / 100f;

    /// <summary>Prefab 1 món hàng: lấy productPrefab, nếu trống thì lấy từ thùng hàng (cratePrefab).</summary>
    public GameObject ResolveProductPrefab()
    {
        if (productPrefab != null) return productPrefab;
        if (cratePrefab != null)
        {
            Interactable box = cratePrefab.GetComponent<Interactable>();
            if (box != null && box.productPrefab != null) return box.productPrefab;
        }
        return null;
    }
}
