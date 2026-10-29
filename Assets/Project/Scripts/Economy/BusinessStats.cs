using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Lưu số liệu kinh doanh từng ngày (doanh thu, chi phí theo loại, lãi, số khách, món đã bán, giao hàng, thời tiết)
/// để xem thống kê theo ngày / tuần / tháng trên máy tính (tab Stats). Được lưu vào file save.
/// </summary>
public class BusinessStats : MonoBehaviour
{
    [Serializable] public class ItemCount { public string item; public int count; public float revenue; }

    [Serializable]
    public class DayRecord
    {
        public int day;
        public float income, expenses, sales, supplies, rent, wages, upgrades, other;
        public int customers, items, deliveries;
        public float deliveryRevenue;
        public string weather, season;
        public List<ItemCount> products = new List<ItemCount>();
        public float Profit => income - expenses;
        public DateTime Date => GameManager.DateOfDay(day);
    }

    public static BusinessStats Instance { get; private set; }
    public static IReadOnlyList<DayRecord> Days => Instance != null ? Instance.days : (IReadOnlyList<DayRecord>)Array.Empty<DayRecord>();
    public static event Action OnChanged;

    private readonly List<DayRecord> days = new List<DayRecord>();
    private readonly Dictionary<string, ItemCount> todayItems = new Dictionary<string, ItemCount>();
    private int todayDeliveries;
    private float todayDeliveryRevenue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        CreateIfMissing();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => CreateIfMissing();

    private static void CreateIfMissing()
    {
        if (Instance == null) new GameObject("BusinessStats").AddComponent<BusinessStats>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    void Awake()
    {
        Instance = this;
        EconomyManager.OnDayClosed += OnDayClosed;
    }

    void OnDestroy()
    {
        EconomyManager.OnDayClosed -= OnDayClosed;
        if (Instance == this) Instance = null;
    }

    // ================= Ghi nhận =================
    public static void RecordItemSold(ItemData item, float price)
    {
        if (Instance == null || item == null) return;
        string key = item.itemName;
        if (!Instance.todayItems.TryGetValue(key, out ItemCount c)) Instance.todayItems[key] = c = new ItemCount { item = key };
        c.count++;
        c.revenue += price;
    }

    public static void RecordDelivery(float amount)
    {
        if (Instance == null) return;
        Instance.todayDeliveries++;
        Instance.todayDeliveryRevenue += amount;
    }

    private void OnDayClosed(DailySummary s)
    {
        var r = new DayRecord
        {
            day = s.day,
            income = s.income,
            expenses = s.expenses,
            sales = s.Get(MoneyCategory.Sales),
            supplies = s.Get(MoneyCategory.Supplies),
            rent = s.Get(MoneyCategory.Rent),
            wages = s.Get(MoneyCategory.Wages),
            upgrades = s.Get(MoneyCategory.Upgrades),
            other = s.Get(MoneyCategory.Other),
            customers = s.customersServed,
            items = s.itemsSold,
            deliveries = todayDeliveries,
            deliveryRevenue = todayDeliveryRevenue,
            weather = WeatherSystem.WeatherName(WeatherSystem.CurrentWeather),
            season = WeatherSystem.SeasonName(WeatherSystem.CurrentSeason),
            products = todayItems.Values.OrderByDescending(i => i.revenue).ToList(),
        };
        days.RemoveAll(d => d.day == r.day);
        days.Add(r);
        days.Sort((a, b) => a.day.CompareTo(b.day));
        todayItems.Clear();
        todayDeliveries = 0;
        todayDeliveryRevenue = 0f;
        OnChanged?.Invoke();
    }

    // ================= Lưu / nạp =================
    public static List<DayRecord> Export() => Instance != null ? new List<DayRecord>(Instance.days) : new List<DayRecord>();

    public static void Import(List<DayRecord> list)
    {
        if (Instance == null) CreateIfMissing();
        Instance.days.Clear();
        if (list != null) Instance.days.AddRange(list.Where(d => d != null));
        Instance.days.Sort((a, b) => a.day.CompareTo(b.day));
        Instance.todayItems.Clear();
        Instance.todayDeliveries = 0;
        Instance.todayDeliveryRevenue = 0f;
        OnChanged?.Invoke();
    }

    // ================= Gom nhóm =================
    public class Period
    {
        public string label, title;
        public readonly List<DayRecord> days = new List<DayRecord>();
        public float Income => days.Sum(d => d.income);
        public float Expenses => days.Sum(d => d.expenses);
        public float Profit => Income - Expenses;
        public int Customers => days.Sum(d => d.customers);
        public int Items => days.Sum(d => d.items);
        public int Deliveries => days.Sum(d => d.deliveries);
        public float DeliveryRevenue => days.Sum(d => d.deliveryRevenue);
        public float Category(Func<DayRecord, float> f) => days.Sum(f);
        public List<ItemCount> TopProducts(int n)
        {
            var map = new Dictionary<string, ItemCount>();
            foreach (DayRecord d in days)
                foreach (ItemCount c in d.products)
                {
                    if (!map.TryGetValue(c.item, out ItemCount m)) map[c.item] = m = new ItemCount { item = c.item };
                    m.count += c.count; m.revenue += c.revenue;
                }
            return map.Values.OrderByDescending(m => m.revenue).Take(n).ToList();
        }
        public DayRecord BestDay => days.OrderByDescending(d => d.Profit).FirstOrDefault();
    }

    private static string F(DateTime d, string format) => d.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Từng ngày (nhãn = thứ + ngày).</summary>
    public static List<Period> ByDay(int last)
    {
        return Days.Skip(Math.Max(0, Days.Count - last)).Select(d =>
        {
            var p = new Period { label = $"{F(d.Date, "ddd")}\n{F(d.Date, "dd/MM")}", title = $"Day {d.day} · {F(d.Date, "dddd dd/MM/yyyy")} · {d.season}, {d.weather}" };
            p.days.Add(d);
            return p;
        }).ToList();
    }

    /// <summary>Theo tuần (thứ Hai -> Chủ nhật).</summary>
    public static List<Period> ByWeek(int last)
    {
        var groups = new SortedDictionary<DateTime, Period>();
        foreach (DayRecord d in Days)
        {
            DateTime date = d.Date;
            int offset = ((int)date.DayOfWeek + 6) % 7; // Thứ Hai = 0
            DateTime monday = date.AddDays(-offset).Date;
            if (!groups.TryGetValue(monday, out Period p))
                groups[monday] = p = new Period { label = $"{F(monday, "dd/MM")}", title = $"Week {F(monday, "dd/MM")} - {F(monday.AddDays(6), "dd/MM/yyyy")}" };
            p.days.Add(d);
        }
        return groups.Values.Skip(Math.Max(0, groups.Count - last)).ToList();
    }

    /// <summary>Theo tháng dương lịch.</summary>
    public static List<Period> ByMonth(int last)
    {
        var groups = new SortedDictionary<DateTime, Period>();
        foreach (DayRecord d in Days)
        {
            DateTime m = new DateTime(d.Date.Year, d.Date.Month, 1);
            if (!groups.TryGetValue(m, out Period p))
                groups[m] = p = new Period { label = $"{F(m, "MMM")}\n{F(m, "yyyy")}", title = $"{F(m, "MMMM yyyy")}" };
            p.days.Add(d);
        }
        return groups.Values.Skip(Math.Max(0, groups.Count - last)).ToList();
    }
}
