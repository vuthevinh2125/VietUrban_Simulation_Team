using System;
using System.Collections.Generic;
using UnityEngine;

public enum MoneyCategory { Sales, Supplies, Rent, Wages, Upgrades, Other }

[Serializable]
public struct Transaction
{
    public int day;
    public float amount;            // + thu vào, - chi ra
    public MoneyCategory category;
    public string note;
}

/// <summary>Tổng kết thu/chi của một ngày (dùng cho tab Tài chính trên máy tính).</summary>
[Serializable]
public class DailySummary
{
    public int day;
    public float openingBalance;
    public float closingBalance;
    public float income;
    public float expenses;          // số dương
    public int customersServed;
    public int itemsSold;

    public readonly Dictionary<MoneyCategory, float> byCategory = new Dictionary<MoneyCategory, float>();

    public float Profit => income - expenses;
    public float Get(MoneyCategory category) => byCategory.TryGetValue(category, out float v) ? v : 0f;
}

/// <summary>
/// VÍ TIỀN DUY NHẤT của game. Mọi thứ liên quan tới tiền (đặt hàng, khách trả tiền, tiền thuê...)
/// đều phải đi qua đây. Nếu scene chưa có object EconomyManager thì nó tự tạo khi được gọi lần đầu.
/// </summary>
public class EconomyManager : MonoBehaviour
{
    private static EconomyManager instance;
    private static bool isQuitting;

    public static EconomyManager Instance
    {
        get
        {
            if (instance == null && !isQuitting)
            {
                instance = FindFirstObjectByType<EconomyManager>();
                if (instance == null)
                    instance = new GameObject("EconomyManager").AddComponent<EconomyManager>();
            }
            return instance;
        }
    }

    [Header("Starting Values")]
    [SerializeField] private float startingMoney = 1000f;

    [Header("Daily Costs")]
    [Tooltip("Tiền thuê mặt bằng trừ vào cuối mỗi ngày")]
    public float dailyRent = 100f;

    public float Money { get; private set; }
    public DailySummary Today { get; private set; }
    public IReadOnlyList<DailySummary> History => history;
    public IReadOnlyList<Transaction> Transactions => transactions;

    public static event Action<float> OnMoneyChanged;
    public static event Action<Transaction> OnTransaction;
    public static event Action<DailySummary> OnDayClosed;

    private readonly List<DailySummary> history = new List<DailySummary>();
    private readonly List<Transaction> transactions = new List<Transaction>();
    private int currentDay = 1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[Economy] Có 2 EconomyManager trong scene, bỏ bớt 1 cái.");
            Destroy(this);
            return;
        }
        instance = this;
        Money = startingMoney;
        StartNewDay(1);
    }

    private void OnApplicationQuit() => isQuitting = true;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ================= API =================

    public bool CanAfford(float amount) => Money >= amount;

    /// <summary>Cộng tiền (bán hàng...).</summary>
    public void AddIncome(float amount, MoneyCategory category = MoneyCategory.Sales, string note = "")
    {
        if (amount <= 0f) return;
        Apply(amount, category, note);
    }

    /// <summary>Chi tiền nếu đủ. Trả về false nếu không đủ tiền.</summary>
    public bool TrySpend(float amount, MoneyCategory category, string note = "")
    {
        if (amount < 0f || Money < amount) return false;
        if (amount > 0f) Apply(-amount, category, note);
        return true;
    }

    /// <summary>Trừ tiền bắt buộc (tiền thuê...), có thể làm tiền âm.</summary>
    public void Charge(float amount, MoneyCategory category, string note = "")
    {
        if (amount <= 0f) return;
        Apply(-amount, category, note);
    }

    /// <summary>Ghi nhận một khách thanh toán xong.</summary>
    public void RecordSale(float total, int itemCount, string note = "")
    {
        AddIncome(total, MoneyCategory.Sales, note);
        Today.customersServed++;
        Today.itemsSold += itemCount;
    }

    /// <summary>Chốt sổ cuối ngày: trừ tiền thuê, lưu báo cáo, mở sổ ngày mới.</summary>
    public DailySummary CloseDay()
    {
        Charge(dailyRent, MoneyCategory.Rent, "Daily rent");
        Today.closingBalance = Money;

        DailySummary closed = Today;
        history.Add(closed);
        StartNewDay(closed.day + 1);
        OnDayClosed?.Invoke(closed);
        return closed;
    }

    public static string Format(float amount)
    {
        return (amount < 0f ? "-$" : "$") + Mathf.Abs(amount).ToString("N2");
    }

    /// <summary>Nạp lại số tiền và ngày từ file lưu (SaveManager). Sổ sách trong ngày bắt đầu lại từ đầu.</summary>
    public void LoadState(float money, int day)
    {
        Money = money;
        history.Clear();
        transactions.Clear();
        StartNewDay(Mathf.Max(1, day));
        OnMoneyChanged?.Invoke(Money);
    }

    // ================= Internal =================

    private void StartNewDay(int day)
    {
        currentDay = day;
        Today = new DailySummary { day = day, openingBalance = Money, closingBalance = Money };
    }

    private void Apply(float delta, MoneyCategory category, string note)
    {
        Money += delta;

        if (delta > 0f) Today.income += delta;
        else Today.expenses += -delta;
        Today.byCategory[category] = Today.Get(category) + Mathf.Abs(delta);
        Today.closingBalance = Money;

        var t = new Transaction { day = currentDay, amount = delta, category = category, note = note };
        transactions.Add(t);

        OnTransaction?.Invoke(t);
        OnMoneyChanged?.Invoke(Money);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
        OnMoneyChanged = null;
        OnTransaction = null;
        OnDayClosed = null;
    }
}
