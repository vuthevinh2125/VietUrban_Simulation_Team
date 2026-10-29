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

    public void SetupCard(ItemData data)
    {
        itemData = data;

        // Calculate price per box = unit price * items per box
        pricePerBox = itemData.BoxPrice;

        // Fill left UI info
        if (productNameText != null) productNameText.text = itemData.itemName;
        if (brandText != null) brandText.text = itemData.brand;
        if (iconImage != null)
        {
            // Không có icon thì ẩn ảnh đi, tránh giữ lại ảnh của thẻ mẫu (mọi thẻ cùng một hình)
            iconImage.sprite = itemData.icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = itemData.icon != null;
        }

        if (displayTypeText != null) displayTypeText.text = itemData.displayType;
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
