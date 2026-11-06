using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gợi ý nhập hàng (tab Advisor trên máy tính): với mỗi mặt hàng cửa hàng đang bán, dự báo sẽ bán bao nhiêu từ bây giờ
/// tới hết NGÀY MAI rồi đề xuất số thùng cần đặt thêm, kèm lý do.
///
/// Các bước:
///  1. Học bộ dự báo DemandForecaster từ lịch sử bán (BusinessStats.Days) - không đọc công thức nhu cầu của game.
///  2. Nhu cầu nhóm hàng = phần còn lại của hôm nay (thời tiết hôm nay đã biết x tỉ trọng các giờ còn lại)
///     + cả ngày mai (theo BẢN TIN dự báo, đúng ~80%).
///  3. Chia cho từng mặt hàng theo tỉ trọng bán 14 ngày gần nhất trong nhóm (làm mịn +1).
///  4. Mức hàng mục tiêu Q = μ + z·σ, với σ = sqrt(μ + (0.25μ)²) (nhiễu ngẫu nhiên + sai số dự báo ~25% đo được):
///     - hàng không hỏng: z = 1.28 (đủ hàng 90% số ngày; để dư không mất gì ngoài tiền vốn);
///     - hàng tươi: mô hình NEWSVENDOR - mức phục vụ tối ưu = Cu / (Cu + Co), Cu = lãi mất khi hết hàng (giá bán - giá nhập),
///       Co = phần vốn có thể mất khi dư hàng. Newsvendor gốc giả định hàng dư hỏng hết sau 1 kỳ (Co = giá nhập); ở đây hàng
///       còn bán được tới hết hạn L ngày nên Co ≈ giá nhập / L (mô phỏng: dùng Co = giá nhập làm tỉ lệ đáp ứng tụt 96% -> 93%).
///  5. Cần đặt = Q - (hàng trên kệ + trong thùng), làm tròn lên số thùng (hàng tươi: làm tròn gần nhất, ít nhất 1 thùng khi thiếu &gt;= nửa thùng).
/// Các hàm Quantile / TargetStock / Boxes là hàm thuần để test EditMode.
/// </summary>
public static class RestockAdvisor
{
    public const float ForecastErrorShare = 0.25f; // sai số dự báo tương đối (WAPE đo được ~24%, ForecastEvaluationTests)
    public const float ServiceLevelDry = 0.9f;

    public class Suggestion
    {
        public ItemData item;
        public ProductGroup group;
        public int onHand, boxes;
        public float forecast, target;
        public bool perishable;
        public float serviceLevel;
        public string reason;
        public float Urgency => forecast <= 0f ? 0f : Mathf.Clamp01((target - onHand) / Mathf.Max(1f, target));
    }

    public class Report
    {
        public readonly List<Suggestion> items = new List<Suggestion>();
        public int daysOfHistory;
        public WeatherSystem.DayWeather tomorrow;
        public float hoursLeftToday;
    }

    // ================= Hàm thuần =================
    /// <summary>Phân vị chuẩn tắc (xấp xỉ Acklam, sai số &lt; 1e-3 trong 0.02-0.98).</summary>
    public static float NormalQuantile(float p)
    {
        p = Mathf.Clamp(p, 0.001f, 0.999f);
        double q = p - 0.5;
        if (Math.Abs(q) <= 0.425)
        {
            double r = 0.180625 - q * q;
            return (float)(q * (((((((2509.0809287301226727 * r + 33430.575583588128105) * r + 67265.770927008700853) * r + 45921.953931549871457) * r + 13731.693765509461125) * r + 1971.5909503065514427) * r + 133.14166789178437745) * r + 3.387132872796366608)
                / (((((((5226.495278852545925 * r + 28729.085735721942674) * r + 39307.89580009271061) * r + 21213.794301586595867) * r + 5394.1960214247511077) * r + 687.1870074920579083) * r + 42.313330701600911252) * r + 1.0));
        }
        double rr = Math.Sqrt(-Math.Log(q < 0 ? p : 1 - p));
        double val;
        if (rr <= 5)
        {
            rr -= 1.6;
            val = (((((((7.7454501427834140764e-4 * rr + 0.0227238449892691845833) * rr + 0.24178072517745061177) * rr + 1.27045825245236838258) * rr + 3.64784832476320460504) * rr + 5.7694972214606914055) * rr + 4.6303378461565452959) * rr + 1.42343711074968357734)
                / (((((((1.05075007164441684324e-9 * rr + 5.475938084995344946e-4) * rr + 0.0151986665636164571966) * rr + 0.14810397642748007459) * rr + 0.68976733498510000455) * rr + 1.6763848301838038494) * rr + 2.05319162663775882187) * rr + 1.0);
        }
        else
        {
            rr -= 5;
            val = (((((((2.01033439929228813265e-7 * rr + 2.71155556874348757815e-5) * rr + 0.0012426609473880784386) * rr + 0.026532189526576123093) * rr + 0.29656057182850489123) * rr + 1.7848265399172913358) * rr + 5.4637849111641143699) * rr + 6.6579046435011037772)
                / (((((((2.04426310338993978564e-15 * rr + 1.4215117583164458887e-7) * rr + 1.8463183175100546818e-5) * rr + 7.868691311456132591e-4) * rr + 0.0148753612908506148525) * rr + 0.13692988092273580531) * rr + 0.59983220655588793769) * rr + 1.0);
        }
        return (float)(q < 0 ? -val : val);
    }

    /// <summary>Mức phục vụ tối ưu newsvendor: Cu / (Cu + Co), Co = giá nhập / số ngày hạn dùng (hàng dư còn bán tiếp được).</summary>
    public static float NewsvendorServiceLevel(float sellPrice, float unitCost, float shelfLifeDays = 1f)
    {
        float cu = Mathf.Max(0f, sellPrice - unitCost), co = Mathf.Max(0.01f, unitCost) / Mathf.Max(1f, shelfLifeDays);
        return Mathf.Clamp(cu / (cu + co), 0.05f, 0.95f);
    }

    /// <summary>Mức hàng nên có: μ + z·σ, σ = sqrt(μ + (sai số tương đối · μ)²).</summary>
    public static float TargetStock(float mean, float serviceLevel)
    {
        if (mean <= 0f) return 0f;
        float sigma = Mathf.Sqrt(mean + (ForecastErrorShare * mean) * (ForecastErrorShare * mean));
        return Mathf.Max(0f, mean + NormalQuantile(serviceLevel) * sigma);
    }

    /// <summary>Số thùng cần đặt cho phần thiếu (hàng tươi làm tròn gần nhất để khỏi dư hàng hỏng).</summary>
    public static int Boxes(float shortfall, int itemsPerBox, bool perishable)
    {
        if (shortfall <= 0f) return 0;
        float perBox = Mathf.Max(1, itemsPerBox);
        float b = shortfall / perBox;
        if (!perishable) return Mathf.CeilToInt(b - 1e-4f);
        return b < 0.5f ? 0 : Mathf.Max(1, Mathf.RoundToInt(b));
    }

    // ================= Dựng báo cáo từ trạng thái game =================
    public static DemandForecaster TrainFromHistory(out int days, int openHour = 8, int closeHour = 18)
    {
        var obs = new List<DemandForecaster.Observation>();
        foreach (BusinessStats.DayRecord r in BusinessStats.Days)
        {
            if (r == null) continue;
            int[] gh = r.groupHour;
            bool noHours = false;
            if (gh == null)
            {
                // Save cũ (trước 4/11) chưa ghi theo nhóm x giờ: dựng tổng theo nhóm từ danh sách mặt hàng đã bán trong ngày
                if (r.products == null || r.products.Count == 0) continue;
                gh = new int[DemandForecaster.Groups * DemandForecaster.Hours];
                foreach (BusinessStats.ItemCount c in r.products) gh[(int)DemandModel.GroupOf(c.item) * DemandForecaster.Hours + 12] += c.count;
                noHours = true;
            }
            Enum.TryParse(r.weather, out WeatherSystem.Weather w);
            obs.Add(new DemandForecaster.Observation
            {
                weather = (int)w,
                tempC = r.tempC != 0 ? r.tempC : 25, // save cũ không có nhiệt độ
                dayOfWeek = GameManager.DateOfDay(r.day).DayOfWeek,
                groupHour = gh,
                noHours = noHours,
                availableHours = r.availableHours,
            });
        }
        days = obs.Count;
        var f = new DemandForecaster { OpenHour = openHour, CloseHour = closeHour };
        foreach (DemandForecaster.Observation o in obs) f.Observe(o);
        return f;
    }

    public static Report Build(IEnumerable<ItemData> catalog)
    {
        var rep = new Report();
        GameManager gm = GameManager.Instance;
        DemandForecaster f = TrainFromHistory(out rep.daysOfHistory, gm != null ? gm.openingHour : 8, gm != null ? gm.closingHour : 18);
        int today = gm != null ? gm.currentDay : 1;
        float now = gm != null ? gm.ClockHours : 8f;
        int open = gm != null ? gm.openingHour : 8, close = gm != null ? gm.closingHour : 18;
        bool openNow = gm != null && gm.IsStoreOpen && now < close;
        rep.hoursLeftToday = openNow ? Mathf.Max(0f, close - now) : 0f;
        rep.tomorrow = WeatherSystem.Forecast(today + 1);
        DayOfWeek dowToday = GameManager.DateOfDay(today).DayOfWeek, dowTomorrow = GameManager.DateOfDay(today + 1).DayOfWeek;

        // Mặt hàng đang bán: có trên kệ / trong thùng, hoặc đã bán trong 7 ngày gần nhất
        var byName = new Dictionary<string, ItemData>();
        foreach (ItemData it in catalog) if (it != null && !string.IsNullOrEmpty(it.itemName)) byName[it.itemName] = it;
        // Có thể có 2 asset cùng tên (vd. 2 "Milk"): gộp theo tên, đặt hàng bằng asset trong danh mục máy tính,
        // tồn kho cộng của mọi asset cùng tên
        var selling = new List<ItemData>();
        var onHandByName = new Dictionary<string, int>();
        foreach (ItemData known in StockManager.GetKnownItems())
        {
            if (known == null || string.IsNullOrEmpty(known.itemName)) continue;
            onHandByName[known.itemName] = (onHandByName.TryGetValue(known.itemName, out int oh) ? oh : 0) + StockManager.GetTotalCount(known);
            ItemData buyable = byName.TryGetValue(known.itemName, out ItemData cat) ? cat : known;
            if (!selling.Contains(buyable)) selling.Add(buyable);
        }
        var sold14 = new Dictionary<string, int>();
        var days = BusinessStats.Days;
        for (int i = days.Count - 1, n = 0; i >= 0 && n < 14; i--, n++)
            foreach (BusinessStats.ItemCount c in days[i].products)
            {
                sold14[c.item] = (sold14.TryGetValue(c.item, out int v) ? v : 0) + c.count;
                if (n < 7 && byName.TryGetValue(c.item, out ItemData it) && !selling.Contains(it) && !selling.Exists(x => x.itemName == c.item)) selling.Add(it);
            }

        // Tỉ trọng của từng mặt hàng trong nhóm (làm mịn +1 để mặt hàng mới cũng có phần)
        var groupWeight = new float[DemandForecaster.Groups];
        foreach (ItemData it in selling) groupWeight[(int)DemandModel.GroupOf(it)] += 1f + (sold14.TryGetValue(it.itemName, out int s) ? s : 0);

        for (int g = 0; g < DemandForecaster.Groups; g++)
        {
            if (groupWeight[g] <= 0f) continue;
            float restToday = rep.hoursLeftToday > 0f
                ? f.PredictDay(g, (int)WeatherSystem.CurrentWeather, WeatherSystem.TemperatureC, dowToday) * f.HourShare(g, Mathf.FloorToInt(now), close)
                : 0f;
            float tomorrow = f.PredictDay(g, (int)rep.tomorrow.weather, rep.tomorrow.tempC, dowTomorrow);
            string why = Reason(f, g, rep.tomorrow, dowTomorrow);

            foreach (ItemData it in selling)
            {
                if ((int)DemandModel.GroupOf(it) != g) continue;
                float share = (1f + (sold14.TryGetValue(it.itemName, out int s) ? s : 0)) / groupWeight[g];
                float life = Freshness.ShelfLifeDays(it);
                bool perishable = life > 0f;
                // Hàng tươi: chỉ tính nhu cầu trước khi hết hạn (hạn &lt; 1 ngày thì chỉ phần còn lại của hôm nay)
                float mean = (restToday + (perishable && life < 1.5f && restToday > 0f ? 0f : tomorrow)) * share;
                float sl = perishable ? NewsvendorServiceLevel(PriceManager.GetSellPrice(it), it.unitPrice, life) : ServiceLevelDry;
                float target = TargetStock(mean, sl);
                int onHand = onHandByName.TryGetValue(it.itemName, out int oh) ? oh : 0;
                var sug = new Suggestion
                {
                    item = it, group = (ProductGroup)g, onHand = onHand, forecast = mean, target = target,
                    perishable = perishable, serviceLevel = sl,
                    boxes = Boxes(target - onHand, it.itemsPerBox, perishable),
                    reason = why + (perishable ? $"{(why.Length > 0 ? " · " : "")}fresh {life:0}d, stock for {sl * 100f:0}% of days" : ""),
                };
                rep.items.Add(sug);
            }
        }
        // Mặt hàng cần đặt lên trước, trong đó thiếu nhiều (so với mức mục tiêu) lên trước, rồi tới bán nhiều
        rep.items.Sort((a, b) =>
        {
            int c = (b.boxes > 0).CompareTo(a.boxes > 0);
            if (c != 0) return c;
            c = b.Urgency.CompareTo(a.Urgency);
            return c != 0 ? c : b.forecast.CompareTo(a.forecast);
        });
        return rep;
    }

    /// <summary>Lý do ngắn: các hệ số học được lệch &gt; 10% so với bình thường (vd. "Rain x1.3 · Sat x1.2").</summary>
    private static string Reason(DemandForecaster f, int g, WeatherSystem.DayWeather tomorrow, DayOfWeek dow)
    {
        if (!f.HasData(g)) return "no sales data yet - default estimate";
        var parts = new List<string>();
        float fw = f.WeatherFactor(g, (int)tomorrow.weather), ft = f.TempFactor(g, tomorrow.tempC), fd = f.WeekdayFactor(g, dow);
        if (Mathf.Abs(fw - 1f) >= 0.1f) parts.Add($"{WeatherSystem.WeatherName(tomorrow.weather)} x{fw:0.0#}");
        if (Mathf.Abs(ft - 1f) >= 0.1f) parts.Add($"{DemandForecaster.TempBandName(DemandForecaster.TempBand(tomorrow.tempC))} {tomorrow.tempC}°C x{ft:0.0#}");
        if (Mathf.Abs(fd - 1f) >= 0.1f) parts.Add($"{dow.ToString().Substring(0, 3)} x{fd:0.0#}");
        return string.Join(" · ", parts);
    }
}
