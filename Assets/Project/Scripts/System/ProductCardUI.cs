using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ProductCardUI : MonoBehaviour
{
    [Header("Data References")]
    public ItemData itemData;

    // ==========================================
    // DỮ LIỆU DÀNH CHO BỘ LỌC (ShopFilterManager)
    // ==========================================
    [Header("Filter Settings")]
    [Tooltip("Gõ danh mục của sản phẩm này để thanh Dropdown Category lọc (VD: Food, Furniture)")]
    public string category = "Food";

    // Tự động lấy Tên và Trạng thái từ ItemData gốc, không cần nhập lại!
    public string productName => itemData != null ? itemData.itemName : "";
    public string displayType => itemData != null ? itemData.displayType : "";
    // ==========================================

    [Header("UI - Left Section (Info)")]
    public TextMeshProUGUI productNameText;
    public TextMeshProUGUI brandText;
    public Image iconImage;
    public TextMeshProUGUI displayTypeText;
    public TextMeshProUGUI unitPriceText;
    public TextMeshProUGUI shelfCapacityText;
    public TextMeshProUGUI boxCapacityText;

    [Header("UI - Right Section (Action)")]
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI totalPriceText;

    private int currentBoxAmount = 1;
    private float pricePerBox;

    void Start()
    {
        if (itemData != null)
        {
            SetupCard(itemData);
        }
    }

    void OnEnable()
    {
        DemandModel.OnChanged += RefreshDemand;
        RefreshDemand();
    }

    void OnDisable() => DemandModel.OnChanged -= RefreshDemand;

    // Nhãn tạo lúc chạy: mức nhu cầu ở bên phải tên sản phẩm
    private TextMeshProUGUI demandText;

    /// <summary>
    /// Dòng thương hiệu kèm nhóm hàng ("VietUrban · Breakfast", chữ được tràn sang phải vì khung gốc vừa khít)
    /// và mức nhu cầu lúc này (HOT / high / low / LOW; bình thường thì để trống).
    /// </summary>
    private void RefreshDemand()
    {
        if (itemData == null) return;
        if (brandText != null)
        {
            brandText.richText = true;
            brandText.textWrappingMode = TextWrappingModes.NoWrap;
            brandText.overflowMode = TextOverflowModes.Overflow;
            brandText.text = $"{itemData.brand}  <color=#9FB0C0>· {DemandModel.GroupName(DemandModel.GroupOf(itemData))}</color>";
        }
        TextMeshProUGUI d = Badge(ref demandText, productNameText, "DemandTag");
        if (d != null) d.text = DemandModel.CardTag(DemandModel.Current(itemData));
    }

    /// <summary>Nhân bản một dòng chữ có sẵn (cùng vị trí, cùng font), căn phải. Thẻ được nhân bản từ thẻ mẫu nên tìm lại theo tên trước.</summary>
    private TextMeshProUGUI Badge(ref TextMeshProUGUI field, TextMeshProUGUI source, string objName)
    {
        if (field != null) return field;
        if (source == null) return null;
        Transform existing = source.transform.parent.Find(objName);
        GameObject go = existing != null ? existing.gameObject : Instantiate(source.gameObject, source.transform.parent);
        go.name = objName;
        field = go.GetComponent<TextMeshProUGUI>();
        field.alignment = TextAlignmentOptions.Right;
        field.textWrappingMode = TextWrappingModes.NoWrap;
        field.overflowMode = TextOverflowModes.Overflow;
        field.richText = true;
        field.raycastTarget = false;
        field.text = "";
        return field;
    }

    public void SetupCard(ItemData data)
    {
        itemData = data;

        // Calculate price per box = unit price * items per box
        pricePerBox = itemData.BoxPrice;

        // Fill left UI info
        if (productNameText != null) productNameText.text = itemData.itemName;
        if (iconImage != null)
        {
            // Không có icon thì ẩn ảnh đi, tránh giữ lại ảnh của thẻ mẫu (mọi thẻ cùng một hình)
            iconImage.sprite = itemData.icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = itemData.icon != null;
        }

        if (displayTypeText != null) displayTypeText.text = itemData.displayType;
        RefreshDemand();
        if (unitPriceText != null) unitPriceText.text = EconomyManager.Format(itemData.unitPrice);
        if (shelfCapacityText != null) shelfCapacityText.text = itemData.shelfCapacity.ToString();
        if (boxCapacityText != null) boxCapacityText.text = itemData.itemsPerBox.ToString();

        currentBoxAmount = 1;
        UpdateActionUI();
    }

    // Hook this to the [+] Button OnClick()
    public void IncreaseAmount()
    {
        currentBoxAmount++;
        UpdateActionUI();
    }

    // Hook this to the [-] Button OnClick()
    public void DecreaseAmount()
    {
        if (currentBoxAmount > 1)
        {
            currentBoxAmount--;
            UpdateActionUI();
        }
    }

    private void UpdateActionUI()
    {
        if (amountText != null) amountText.text = currentBoxAmount.ToString();

        float total = pricePerBox * currentBoxAmount;
        if (totalPriceText != null) totalPriceText.text = EconomyManager.Format(total);
    }

    // Hook this to the [Add To Cart] Button OnClick()
    public void OnAddToCartClicked()
    {
        MarketManager manager = MarketManager.Instance != null ? MarketManager.Instance : FindFirstObjectByType<MarketManager>();
        if (manager != null && itemData != null)
        {
            manager.AddToCart(itemData, currentBoxAmount);
        }

        // Reset amount back to 1 after adding to cart
        currentBoxAmount = 1;
        UpdateActionUI();
    }
}
