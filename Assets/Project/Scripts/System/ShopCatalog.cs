using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    void Awake()
    {
        BuildCards();
        HookLicenseButton();
        HookFurnitureButton();
        HookStaffButton();
    }

    /// <summary>Tab "Paints" (chưa dùng) trên thanh máy tính đổi thành "Staff": thuê nhân viên.</summary>
    private void HookStaffButton()
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.GetComponentInParent<ProductCardUI>() != null) continue;
            if (LabelOf(b).IndexOf("paint", StringComparison.OrdinalIgnoreCase) < 0) continue;
            SetLabel(b, "Staff");
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(StaffPanel.Open);
            b.interactable = true;
            return;
        }
        Debug.LogWarning("[ShopCatalog] Không thấy tab 'Paints' trên máy tính để gắn bảng nhân viên.", this);
    }

    /// <summary>Tab "Furnitures" trên thanh máy tính mở bảng mua nội thất (FurnitureCatalog).</summary>
    private void HookFurnitureButton()
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.GetComponentInParent<ProductCardUI>() != null) continue;
            if (LabelOf(b).IndexOf("furnit", StringComparison.OrdinalIgnoreCase) < 0) continue;
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(() =>
            {
                FurnitureCatalog cat = GetComponent<FurnitureCatalog>();
                FurniturePanel.Open(cat != null ? cat.furniture : null);
            });
            b.interactable = true;
            return;
        }
        Debug.LogWarning("[ShopCatalog] Không thấy tab 'Furnitures' trên máy tính để gắn bảng nội thất.", this);
    }

    /// <summary>
    /// Nút "Licenses" trên máy tính: dùng lại tab "Tools" ở thanh trên (đổi chữ thành Licenses);
    /// không tìm thấy thì tự thêm một nút ở góc trên trái giao diện.
    /// </summary>
    private void HookLicenseButton()
    {
        Button target = null;
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.GetComponentInParent<ProductCardUI>() != null) continue;
            if (LabelOf(b).IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0) { target = b; break; }
        }

        if (target == null)
        {
            var go = new GameObject("Button_Licenses", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(170, 46);
            rt.anchoredPosition = new Vector2(16, -16);
            go.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f);
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            ((RectTransform)tgo.transform).sizeDelta = rt.sizeDelta;
            var t = tgo.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 24;
            t.fontStyle = FontStyles.Bold;
            target = go.GetComponent<Button>();
        }

        SetLabel(target, "Licenses");
        target.onClick = new Button.ButtonClickedEvent(); // bỏ hành động cũ của tab Tools
        target.onClick.AddListener(LicensePanel.Open);
        target.interactable = true;
    }

    private static string LabelOf(Button b)
    {
        TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) return tmp.text ?? "";
        Text legacy = b.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text ?? "" : b.name;
    }

    private static void SetLabel(Button b, string label)
    {
        TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) { tmp.text = label; return; }
        Text legacy = b.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = label;
    }

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
