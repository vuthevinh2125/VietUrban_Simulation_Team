using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh mục hàng bán trên máy tính. Khi giao diện mở lần đầu, tự sinh một thẻ sản phẩm (ProductCardUI)
/// cho mỗi mặt hàng, dùng thẻ đầu tiên có sẵn trong Content làm mẫu.
/// Gắn trên cùng object với ShopFilterManager (ComputerUI) để bộ lọc thấy được các thẻ mới.
/// </summary>
public class ShopCatalog : MonoBehaviour
{
    [Tooltip("Các mặt hàng bán trên máy tính (công cụ Import Products tự điền)")]
    public List<ItemData> items = new List<ItemData>();

    [Tooltip("Nơi chứa các thẻ sản phẩm (Content của Scroll View). Để trống = lấy từ ShopFilterManager.")]
    public Transform contentParent;

    [Tooltip("Giữ lại các mặt hàng đang có trên thẻ cũ (sữa, ngũ cốc...) và thêm vào danh mục")]
    public bool keepExistingCardItems = true;

    void Awake() => BuildCards();

    public void BuildCards()
    {
        if (contentParent == null)
        {
            ShopFilterManager filter = GetComponent<ShopFilterManager>();
            if (filter != null) contentParent = filter.contentParent;
        }
        if (contentParent == null) { Debug.LogWarning("[ShopCatalog] Chưa có Content Parent.", this); return; }

        var existing = new List<ProductCardUI>();
        foreach (Transform child in contentParent)
        {
            ProductCardUI card = child.GetComponent<ProductCardUI>();
            if (card != null) existing.Add(card);
        }
        if (existing.Count == 0) { Debug.LogWarning("[ShopCatalog] Content không có thẻ mẫu ProductCardUI nào.", this); return; }

        // Danh sách hàng cuối cùng (không trùng)
        var finalItems = new List<ItemData>();
        if (keepExistingCardItems)
            foreach (ProductCardUI c in existing)
                if (c.itemData != null && !finalItems.Contains(c.itemData)) finalItems.Add(c.itemData);
        foreach (ItemData item in items)
            if (item != null && !finalItems.Contains(item)) finalItems.Add(item);

        // Ảnh đang hiện trên thẻ cũ (mặt hàng cũ chưa có icon trong ItemData thì dùng lại ảnh này)
        var oldSprites = new Dictionary<ItemData, Sprite>();
        foreach (ProductCardUI c in existing)
            if (c.itemData != null && c.iconImage != null && c.iconImage.sprite != null && !oldSprites.ContainsKey(c.itemData))
                oldSprites[c.itemData] = c.iconImage.sprite;

        // Giữ một bản mẫu, gỡ các thẻ cũ ra khỏi Content (ShopFilterManager chỉ quét con của Content)
        GameObject template = Instantiate(existing[0].gameObject);
        template.SetActive(false);
        foreach (ProductCardUI c in existing)
        {
            c.transform.SetParent(null, false);
            Destroy(c.gameObject);
        }

        foreach (ItemData item in finalItems)
        {
            GameObject go = Instantiate(template, contentParent, false);
            go.name = "Card_" + item.itemName;
            go.SetActive(true);
            ProductCardUI card = go.GetComponent<ProductCardUI>();
            card.category = CategoryOf(item);
            card.SetupCard(item);
            if (item.icon == null && card.iconImage != null && oldSprites.TryGetValue(item, out Sprite old))
            {
                card.iconImage.sprite = old;
                card.iconImage.enabled = true;
            }
        }
        Destroy(template);
        Debug.Log($"[ShopCatalog] {finalItems.Count} mặt hàng trên máy tính.", this);
    }

    private static string CategoryOf(ItemData item)
    {
        string n = (item.itemName + " " + item.description).ToLower();
        string[] household = { "bleach", "cleanser", "laundry", "shampoo", "soap", "toothpaste", "toilet", "paper towel", "pet" };
        foreach (string h in household) if (n.Contains(h)) return "Household";
        return "Food";
    }
}
