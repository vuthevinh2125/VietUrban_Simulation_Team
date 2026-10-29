using UnityEngine;
using UnityEngine.UI; // Thư viện cho thanh Search (Legacy)
using TMPro;          // Thư viện cho 2 Dropdown (TextMeshPro)
using System.Collections.Generic;

public class ShopFilterManager : MonoBehaviour
{
    [Header("UI References")]
    public InputField searchInput;         // Kéo thanh Search vào đây
    public TMP_Dropdown displayDropdown;   // Kéo menu DISPLAY vào đây
    public TMP_Dropdown categoryDropdown;  // Kéo menu CATEGORY vào đây

    [Header("Content")]
    public Transform contentParent;        // Kéo cục Content vào đây

    private List<ProductCardUI> allProducts = new List<ProductCardUI>();

    void Start()
    {
        // 1. Quét tìm tất cả các thẻ sản phẩm đang có trong Content
        foreach (Transform child in contentParent)
        {
            ProductCardUI card = child.GetComponent<ProductCardUI>();
            if (card != null)
            {
                allProducts.Add(card);
            }
        }

        // 2. Kích hoạt tự động: Cứ gõ chữ hoặc chọn menu là tự chạy bộ lọc ngay lập tức
        searchInput.onValueChanged.AddListener((string val) => { ApplyFilters(); });
        displayDropdown.onValueChanged.AddListener((int val) => { ApplyFilters(); });
        categoryDropdown.onValueChanged.AddListener((int val) => { ApplyFilters(); });
    }

    // ==========================================
    // BỘ LỌC TỔNG HỢP (Kết hợp 3 điều kiện)
    // ==========================================
    public void ApplyFilters()
    {
        // Lấy dữ liệu người chơi đang nhập/chọn
        string searchText = searchInput.text.ToLower().Trim();
        string selectedDisplay = displayDropdown.options[displayDropdown.value].text;
        string selectedCategory = categoryDropdown.options[categoryDropdown.value].text;

        // Quét qua từng thẻ sản phẩm để kiểm tra
        foreach (ProductCardUI card in allProducts)
        {
            string pName = card.productName != null ? card.productName.ToLower() : "";

            // Điều kiện 1: Xem tên có khớp với chữ đang gõ không?
            bool matchSearch = string.IsNullOrEmpty(searchText) || pName.Contains(searchText);

            // Điều kiện 2: Xem trạng thái trưng bày có khớp không (hoặc đang chọn All)
            bool matchDisplay = (selectedDisplay == "All") || (card.displayType == selectedDisplay);

            // Điều kiện 3: Xem danh mục có khớp không (hoặc đang chọn All)
            bool matchCategory = (selectedCategory == "All") || (card.category == selectedCategory);

            // TỔNG DUYỆT: Phải đúng CẢ 3 ĐIỀU KIỆN thì thẻ mới được phép hiện lên!
            if (matchSearch && matchDisplay && matchCategory)
            {
                card.gameObject.SetActive(true);
            }
            else
            {
                card.gameObject.SetActive(false);
            }
        }
    }
}