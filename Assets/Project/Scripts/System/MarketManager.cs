using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Tab ĐẶT HÀNG trên máy tính: giỏ hàng + thanh toán + giao thùng hàng.
/// Tiền được trừ qua EconomyManager (ví duy nhất).
/// </summary>
public class MarketManager : MonoBehaviour
{
    public static MarketManager Instance { get; private set; }
    public static event Action OnCartChanged;

    [Header("UI")]
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI cartTotalText;
    [Tooltip("(Tuỳ chọn) Dòng chữ báo kết quả: đặt hàng thành công, thiếu tiền...")]
    public TextMeshProUGUI statusText;

    [Header("Delivery Settings")]
    public Transform deliveryPoint;
    [Tooltip("Độ cao (toạ độ Y thế giới) của lớp thùng đầu tiên")]
    public float fixedGroundY = 1.5f;
    [Tooltip("Số cột thùng theo chiều ngang")]
    public int boxesPerRow = 3;
    [Tooltip("Số hàng thùng theo chiều sâu")]
    public int rowsPerLayer = 3;
    [Tooltip("Khoảng cách giữa các thùng")]
    public float boxSpacing = 0.7f;
    [Tooltip("Độ cao mỗi tầng khi phải chồng thùng")]
    public float layerHeight = 0.6f;

    private readonly Dictionary<ItemData, int> cart = new Dictionary<ItemData, int>();

    public IReadOnlyDictionary<ItemData, int> Cart => cart;
    public float CartTotal { get; private set; }
    public int CartBoxCount { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Market] Có 2 MarketManager trong scene.");
            return;
        }
        Instance = this;
    }

    void OnEnable() => EconomyManager.OnMoneyChanged += RefreshMoney;
    void OnDisable() => EconomyManager.OnMoneyChanged -= RefreshMoney;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        RefreshMoney(EconomyManager.Instance.Money);
        RecalculateCart();
    }

    // ================= Giỏ hàng =================
    public void AddToCart(ItemData item, int boxAmount)
    {
        if (item == null || boxAmount <= 0)
        {
            Debug.LogWarning("[Cart] AddToCart failed: item null or boxAmount <= 0");
            return;
        }

        cart[item] = cart.TryGetValue(item, out int current) ? current + boxAmount : boxAmount;
        RecalculateCart();
        SetStatus($"Added {boxAmount} box(es) of {item.itemName}");
    }

    public void RemoveFromCart(ItemData item, int boxAmount = int.MaxValue)
    {
        if (item == null || !cart.TryGetValue(item, out int current)) return;

        int left = current - boxAmount;
        if (left > 0) cart[item] = left;
        else cart.Remove(item);
        RecalculateCart();
    }

    public void ClearCart()
    {
        cart.Clear();
        RecalculateCart();
    }

    private void RecalculateCart()
    {
        CartTotal = 0f;
        CartBoxCount = 0;
        foreach (var kvp in cart)
        {
            CartTotal += kvp.Key.BoxPrice * kvp.Value;
            CartBoxCount += kvp.Value;
        }

        if (cartTotalText != null) cartTotalText.text = EconomyManager.Format(CartTotal);
        OnCartChanged?.Invoke();
    }

    // ================= Thanh toán =================
    /// <summary>Gắn vào nút Checkout (OnClick).</summary>
    public void Checkout()
    {
        if (cart.Count == 0) { SetStatus("Cart is empty!"); return; }

        if (deliveryPoint == null)
        {
            Debug.LogError("[Checkout] Chưa gán Delivery Point cho MarketManager!");
            SetStatus("No delivery point set up");
            return;
        }

        foreach (var kvp in cart)
        {
            if (kvp.Key.cratePrefab == null)
            {
                Debug.LogError($"[Checkout] Mặt hàng '{kvp.Key.itemName}' chưa gán Crate Prefab!");
                SetStatus($"{kvp.Key.itemName} cannot be delivered");
                return;
            }
        }

        if (!EconomyManager.Instance.TrySpend(CartTotal, MoneyCategory.Supplies, $"Order {CartBoxCount} box(es)"))
        {
            SetStatus($"Not enough money! Need {EconomyManager.Format(CartTotal)}");
            return;
        }

        int delivered = SpawnDeliveries();
        cart.Clear();
        RecalculateCart();
        SetStatus($"Order placed! {delivered} box(es) delivered.");
    }

    /// <summary>Sinh thùng hàng thành lưới quanh deliveryPoint, ưu tiên cột đang thấp nhất.</summary>
    private int SpawnDeliveries()
    {
        int columns = Mathf.Max(1, boxesPerRow) * Mathf.Max(1, rowsPerLayer);
        int[] stackHeight = new int[columns];

        // Đếm thùng cũ còn nằm ở khu giao hàng để không sinh đè lên
        for (int c = 0; c < columns; c++)
        {
            Vector3 p = ColumnPosition(c);
            foreach (Interactable box in StockManager.Boxes)
            {
                if (box == null || box.IsHeld) continue;
                Vector3 d = box.transform.position - p;
                d.y = 0f;
                if (d.magnitude < boxSpacing * 0.5f) stackHeight[c]++;
            }
        }

        int spawned = 0;
        foreach (var kvp in cart)
        {
            for (int i = 0; i < kvp.Value; i++)
            {
                int col = LowestColumn(stackHeight);
                Vector3 pos = ColumnPosition(col) + Vector3.up * (stackHeight[col] * layerHeight);
                stackHeight[col]++;

                GameObject crate = Instantiate(kvp.Key.cratePrefab, pos, deliveryPoint.rotation);
                Interactable box = crate.GetComponent<Interactable>();
                if (box != null && box.itemData == null) box.SetItem(kvp.Key);
                spawned++;
            }
        }
        return spawned;
    }

    private Vector3 ColumnPosition(int column)
    {
        int perRow = Mathf.Max(1, boxesPerRow);
        int row = column / perRow;
        int col = column % perRow;

        float offsetX = (col - (perRow - 1) * 0.5f) * boxSpacing;
        float offsetZ = row * boxSpacing;

        Vector3 pos = deliveryPoint.position + deliveryPoint.rotation * new Vector3(offsetX, 0f, offsetZ);
        pos.y = fixedGroundY;
        return pos;
    }

    private static int LowestColumn(int[] heights)
    {
        int best = 0;
        for (int i = 1; i < heights.Length; i++)
            if (heights[i] < heights[best]) best = i;
        return best;
    }

    // ================= UI =================
    public void UpdateUI()
    {
        RefreshMoney(EconomyManager.Instance.Money);
        RecalculateCart();
    }

    private void RefreshMoney(float money)
    {
        if (moneyText != null) moneyText.text = EconomyManager.Format(money);
    }

    private void SetStatus(string message)
    {
        Debug.Log("[Market] " + message);
        if (statusText != null) statusText.text = message;
    }

    void OnDrawGizmosSelected()
    {
        if (deliveryPoint == null) return;
        Gizmos.color = Color.cyan;
        int columns = Mathf.Max(1, boxesPerRow) * Mathf.Max(1, rowsPerLayer);
        for (int c = 0; c < columns; c++)
            Gizmos.DrawWireCube(ColumnPosition(c), Vector3.one * boxSpacing * 0.8f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnCartChanged = null;
    }
}
