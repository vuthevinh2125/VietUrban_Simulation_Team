using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Nhóm hàng dùng cho mô hình nhu cầu (suy ra từ tên mặt hàng, không cần sửa 94 file dữ liệu).</summary>
public enum ProductGroup { Drinks, HotDrinks, Alcohol, Breakfast, Snacks, Fresh, Frozen, Pantry, Household, Pet }

/// <summary>
/// Nhu cầu theo giờ trong ngày, thời tiết, nhiệt độ, mùa và thứ trong tuần.
///  - Hệ số 1 = bình thường, &gt; 1 = bán chạy hơn, &lt; 1 = ế hơn (giới hạn 0.4 - 2.5).
///  - Khách chọn kệ theo trọng số nhu cầu, chịu được giá cao hơn khi nhu cầu cao, và mua thêm khi nhu cầu rất cao.
///  - Thẻ sản phẩm trên máy tính hiện nhóm hàng + mức nhu cầu; mỗi sáng / khi đổi thời tiết có thông báo hàng nào đang chạy.
/// Các hàm GroupOf(string) và Multiplier(...) là hàm thuần (không đọc trạng thái game) để viết test tự động.
/// </summary>
public static class DemandModel
{
    public const float Min = 0.4f, Max = 2.5f;

    /// <summary>Gọi khi mức nhu cầu có thể đã đổi (sang giờ mới, đổi thời tiết, sang ngày mới).</summary>
    public static event Action OnChanged;

    private static readonly Dictionary<ItemData, ProductGroup> cache = new Dictionary<ItemData, ProductGroup>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { OnChanged = null; cache.Clear(); }

    internal static void RaiseChanged() => OnChanged?.Invoke();

    // ================= Nhóm hàng =================
    public static ProductGroup GroupOf(ItemData item)
    {
        if (item == null) return ProductGroup.Pantry;
        if (!cache.TryGetValue(item, out ProductGroup g))
        {
            g = GroupOf(item.itemName);
            cache[item] = g;
        }
        return g;
    }

    /// <summary>Suy nhóm hàng từ tên (thứ tự luật quan trọng: "chocolate powder" trước "chocolate").</summary>
    public static ProductGroup GroupOf(string itemName)
    {
        string n = (itemName ?? "").ToLowerInvariant();
        if (Has(n, "pet")) return ProductGroup.Pet;
        // Đồ gia dụng xét sớm: "shampoo" có chứa "ham"
        if (Has(n, "soap", "shampoo", "toothpaste", "paper", "toilet", "bleach", "cleanser", "laundry")) return ProductGroup.Household;
        if (Has(n, "beer", "wine", "alcohol")) return ProductGroup.Alcohol;
        if (Has(n, "coffee", "tea", "capsule", "chocolate powder")) return ProductGroup.HotDrinks;
        if (Has(n, "water", "soda", "juice", "cans pack")) return ProductGroup.Drinks;
        if (Has(n, "frozen", "pizza")) return ProductGroup.Frozen;
        if (Has(n, "chocolate spread", "milk", "bread", "cereal", "egg", "yoghurt", "yogurt", "butter", "cream")) return ProductGroup.Breakfast;
        if (Has(n, "chips", "candy", "chocolate", "sweet", "cake", "metal box")) return ProductGroup.Snacks;
        if (Has(n, "meat", "chicken", "ham", "sushi", "cheese", "crab", "apple", "fish")) return ProductGroup.Fresh;
        return ProductGroup.Pantry; // gạo, mì, bột, đường, muối, dầu, đồ hộp, khoai...
    }

    private static bool Has(string s, params string[] keys)
    {
        foreach (string k in keys) if (s.Contains(k)) return true;
        return false;
    }

    public static string GroupName(ProductGroup g) => g switch
    {
        ProductGroup.Drinks => "Drinks",
        ProductGroup.HotDrinks => "Hot drinks",
        ProductGroup.Alcohol => "Alcohol",
        ProductGroup.Breakfast => "Breakfast",
        ProductGroup.Snacks => "Snacks",
        ProductGroup.Fresh => "Fresh food",
        ProductGroup.Frozen => "Frozen",
        ProductGroup.Household => "Household",
        ProductGroup.Pet => "Pet",
        _ => "Pantry",
    };

    // ================= Hệ số nhu cầu =================
    /// <summary>
    /// Hệ số nhu cầu của một nhóm hàng. hour: giờ trong ngày (8.5 = 8:30); tempC: nhiệt độ °C.
    /// </summary>
    public static float Multiplier(ProductGroup g, float hour, WeatherSystem.Weather weather, WeatherSystem.Season season, int tempC, DayOfWeek day)
    {
        bool wet = weather == WeatherSystem.Weather.Rain || weather == WeatherSystem.Weather.Storm;
        bool hot = tempC >= 32, cool = tempC <= 22;
        bool weekend = day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
        bool nightOut = day == DayOfWeek.Friday || day == DayOfWeek.Saturday;
        bool morning = hour < 10.5f;
        bool lunch = hour >= 11f && hour < 14f;
        bool afternoon = hour >= 14f && hour < 17f;
        bool evening = hour >= 17f;

        float m = 1f;
        switch (g)
        {
            case ProductGroup.Drinks:       // trời nóng uống nhiều, trưa / chiều cao điểm
                m *= 1f + Mathf.Clamp((tempC - 27) * 0.07f, -0.3f, 0.6f);
                if (lunch || afternoon) m *= 1.2f;
                if (wet) m *= 0.85f;
                if (season == WeatherSystem.Season.Summer) m *= 1.1f;
                break;
            case ProductGroup.HotDrinks:    // cà phê / trà buổi sáng, trời lạnh hoặc mưa
                m *= 1f + Mathf.Clamp((26 - tempC) * 0.06f, -0.4f, 0.5f);
                if (morning) m *= 1.5f; else if (evening) m *= 0.8f;
                if (wet) m *= 1.25f;
                break;
            case ProductGroup.Alcohol:      // buổi tối, cuối tuần
                if (morning) m *= 0.4f; else if (evening) m *= 1.6f;
                if (nightOut) m *= 1.3f;
                if (hot) m *= 1.15f;
                break;
            case ProductGroup.Breakfast:    // sáng sớm
                if (morning) m *= 1.8f; else if (hour >= 14f) m *= 0.6f;
                break;
            case ProductGroup.Snacks:       // chiều tan học, tối; mưa ở nhà ăn vặt
                if (morning) m *= 0.7f;
                if (afternoon) m *= 1.3f; else if (evening) m *= 1.2f;
                if (wet) m *= 1.2f;
                break;
            case ProductGroup.Fresh:        // đi chợ chiều chuẩn bị bữa tối
                if (hour >= 16f && hour < 19.5f) m *= 1.5f; else if (morning) m *= 1.1f;
                if (wet) m *= 0.85f;
                if (weekend) m *= 1.15f;
                break;
            case ProductGroup.Frozen:       // kem, đồ đông lạnh khi nóng
                if (hot) m *= 1.3f; else if (cool) m *= 0.8f;
                if (evening) m *= 1.1f;
                break;
            case ProductGroup.Pantry:       // mưa bão thì tích trữ; cuối tuần đi chợ lớn
                if (wet) m *= 1.25f;
                if (weather == WeatherSystem.Weather.Storm) m *= 1.15f;
                if (weekend) m *= 1.2f;
                break;
            case ProductGroup.Household:
                if (weekend) m *= 1.3f;
                if (wet) m *= 0.9f;
                break;
        }
        return Mathf.Clamp(m, Min, Max);
    }

    /// <summary>Hệ số nhu cầu hiện tại trong game.</summary>
    public static float Current(ProductGroup g)
    {
        GameManager gm = GameManager.Instance;
        float hour = gm != null ? gm.ClockHours : 12f;
        DayOfWeek dow = GameManager.DateOfDay(gm != null ? gm.currentDay : 1).DayOfWeek;
        return Multiplier(g, hour, WeatherSystem.CurrentWeather, WeatherSystem.CurrentSeason, WeatherSystem.TemperatureC, dow);
    }

    public static float Current(ItemData item) => Current(GroupOf(item));

    /// <summary>
    /// Khi nhu cầu cao khách chịu trả cao hơn: giá "cảm nhận" = giá bán / hệ số này
    /// (nhu cầu 2.0 -&gt; chấp nhận đắt hơn khoảng 20%; nhu cầu thấp -&gt; khó tính hơn).
    /// </summary>
    public static float PriceTolerance(float demand) => Mathf.Clamp(1f + 0.2f * (demand - 1f), 0.85f, 1.3f);

    /// <summary>Chữ ngắn cho thẻ sản phẩm.</summary>
    public static string Tag(float demand)
    {
        if (demand >= 1.6f) return "<color=#6BE07A>demand VERY HIGH</color>";
        if (demand >= 1.2f) return "<color=#A6E36B>demand high</color>";
        if (demand <= 0.65f) return "<color=#FF8A7A>demand low</color>";
        if (demand <= 0.85f) return "<color=#FFC46B>demand below normal</color>";
        return "<color=#B8C2CC>demand normal</color>";
    }

    /// <summary>Nhãn rất ngắn cho thẻ sản phẩm (bình thường thì để trống cho gọn).</summary>
    public static string CardTag(float demand)
    {
        if (demand >= 1.6f) return "<color=#6BE07A><b>HOT</b></color>";
        if (demand >= 1.2f) return "<color=#A6E36B>high</color>";
        if (demand <= 0.65f) return "<color=#FF8A7A><b>LOW</b></color>";
        if (demand <= 0.85f) return "<color=#FFC46B>low</color>";
        return "";
    }

    /// <summary>Câu tóm tắt: 2 nhóm bán chạy nhất và nhóm ế nhất lúc này.</summary>
    public static string Summary()
    {
        ProductGroup best = ProductGroup.Pantry, second = ProductGroup.Pantry, worst = ProductGroup.Pantry;
        float b1 = -1f, b2 = -1f, w = 99f;
        foreach (ProductGroup g in (ProductGroup[])Enum.GetValues(typeof(ProductGroup)))
        {
            float m = Current(g);
            if (m > b1) { b2 = b1; second = best; b1 = m; best = g; }
            else if (m > b2) { b2 = m; second = g; }
            if (m < w) { w = m; worst = g; }
        }
        string weather = WeatherSystem.WeatherName(WeatherSystem.CurrentWeather);
        return $"{weather} {WeatherSystem.TemperatureC}°C: {GroupName(best)} and {GroupName(second)} sell fast; {GroupName(worst)} is slow right now.";
    }
}

/// <summary>
/// Theo dõi giờ / thời tiết / ngày và báo DemandModel.OnChanged khi có thay đổi (kiểm tra mỗi giây).
/// Đầu ngày hoặc khi đổi thời tiết thì hiện thông báo nhóm hàng đang chạy.
/// </summary>
public class DemandWatcher : MonoBehaviour
{
    private static DemandWatcher instance;
    private int lastHour = -1, lastDay = -1, lastTemp = int.MinValue;
    private WeatherSystem.Weather lastWeather;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance != null) return;
        var go = new GameObject("DemandWatcher");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DemandWatcher>();
        instance.nextCheck = Time.unscaledTime + 2f;
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        GameManager gm = GameManager.Instance;
        if (gm == null) { lastDay = -1; nextCheck = Time.unscaledTime + 2f; return; } // ở menu chính; vào game thì chờ thời tiết khởi tạo xong

        int hour = Mathf.FloorToInt(gm.ClockHours);
        int day = gm.currentDay;
        var weather = WeatherSystem.CurrentWeather;
        int temp = WeatherSystem.TemperatureC;
        if (hour == lastHour && day == lastDay && weather == lastWeather && temp == lastTemp) return;

        bool announce = lastDay != -1 && (day != lastDay || weather != lastWeather);
        bool first = lastDay == -1;
        lastHour = hour; lastDay = day; lastWeather = weather; lastTemp = temp;
        DemandModel.RaiseChanged();
        if (announce || first) ProgressHUD.Toast(DemandModel.Summary());
    }
}
