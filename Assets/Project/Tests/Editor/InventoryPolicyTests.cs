using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Đánh giá CHÍNH SÁCH NHẬP HÀNG bằng mô phỏng (EditMode): gợi ý nhập hàng có giúp kinh doanh tốt hơn không,
/// chứ không chỉ "dự báo đúng hơn".
///
/// Mô phỏng 5 lần x 120 ngày, 10 nhóm hàng (mỗi nhóm một mặt hàng đại diện; giá nhập khác nhau, giá bán = 1.6 x giá nhập,
/// 12 món / thùng, hàng tươi 6 món / thùng). Sữa (Breakfast) hạn 4 ngày, đồ tươi (Fresh) hạn 2 ngày. Nhu cầu như
/// ForecastEvaluationTests (thời tiết, mùa, thứ, cửa hàng lớn 1% / ngày, Poisson theo giờ).
/// Mỗi tối, chính sách quyết định số thùng đặt cho ngày mai (hàng về trước giờ mở cửa). Khách đến mua theo giờ; hết hàng
/// thì mất lượt bán. Hàng bán theo thứ tự cũ trước (FIFO); quá hạn thì hỏng (mất vốn).
/// Bộ dự báo chỉ thấy SỐ BÁN (không thấy nhu cầu bị mất khi hết hàng) và các giờ còn hàng - như trong game.
///
/// Chính sách:
///  1. "Như hôm qua + 20%" (ít nhất 1 thùng)  - kiểu người chơi đặt theo cảm tính;
///  2. "Trung bình 7 ngày + 20%" (ít nhất 1 thùng) - quy tắc kinh nghiệm phổ biến;
///  3. Advisor (RestockAdvisor: bộ dự báo + bản tin thời tiết + newsvendor cho hàng tươi);
///  4. Biết trước nhu cầu thật (cùng quy tắc mức phục vụ) - giới hạn trên.
/// Thước đo (14 ngày đầu khởi động bằng chính sách 2 cho mọi chính sách): tỉ lệ đáp ứng (bán / nhu cầu), % lượt
/// (ngày x nhóm) bị hết hàng, vốn mất vì hàng hỏng, tồn kho trung bình (vốn bị giữ), lãi = doanh thu - tiền nhập + tồn kho cuối.
/// Kết quả ghi vào inventory_eval.txt trong thư mục save.
/// </summary>
public class InventoryPolicyTests
{
    private static readonly float[] Cost = { 0.8f, 1.5f, 2f, 1f, 0.7f, 2f, 2.5f, 1.2f, 2f, 3f };
    private static readonly float[] Life = { 0, 0, 0, 4, 0, 2, 0, 0, 0, 0 };
    private static readonly int[] PerBox = { 12, 12, 12, 12, 12, 6, 12, 12, 12, 12 };
    private const float Markup = 1.6f;
    private static readonly string[] Names = { "Same as yesterday +20%", "7-day average +20%", "Restock Advisor", "Perfect knowledge (upper bound)" };

    private class Lot { public int age, qty; }

    public struct Result { public double fill, stockout, spoiled, avgInventory, profit; }

    private static double TrueDayDemand(int day, int g)
    {
        var wx = WeatherSystem.Actual(day);
        double s = 0;
        for (int h = ForecastEvaluationTests.OpenHour; h < ForecastEvaluationTests.CloseHour; h++) s += ForecastEvaluationTests.Lambda(day, g, h, wx);
        return s;
    }

    public static Result Run(int policy, int days, int seed, int warmup = 14)
    {
        const int G = DemandForecaster.Groups;
        var rng = new System.Random(seed);
        var f = new DemandForecaster();
        var history = new List<int>[G];
        var stock = new List<Lot>[G];
        for (int g = 0; g < G; g++) { history[g] = new List<int>(); stock[g] = new List<Lot> { new Lot { qty = 2 * PerBox[g] } }; }
        double revenue = 0, purchases = 0, spoiled = 0, demandSum = 0, soldSum = 0, inventorySum = 0;
        int stockoutCount = 0, groupDays = 0;

        for (int d = 1; d <= days; d++)
        {
            var wx = WeatherSystem.Actual(d);
            var gh = new int[G * 24];
            var avail = new int[G];
            bool measure = d > warmup;
            for (int g = 0; g < G; g++)
            {
                int demand = 0, sold = 0;
                for (int h = ForecastEvaluationTests.OpenHour; h < ForecastEvaluationTests.CloseHour; h++)
                {
                    if (stock[g].Count > 0) avail[g] |= 1 << h;
                    int n = ForecastEvaluationTests.Poisson(rng, ForecastEvaluationTests.Lambda(d, g, h, wx));
                    demand += n;
                    int k = n;
                    while (k > 0 && stock[g].Count > 0)
                    {
                        Lot lot = stock[g][0];
                        int take = Math.Min(k, lot.qty);
                        lot.qty -= take; k -= take; sold += take;
                        if (lot.qty == 0) stock[g].RemoveAt(0);
                    }
                    gh[g * 24 + h] = n - k;
                }
                history[g].Add(sold);
                if (measure)
                {
                    demandSum += demand; soldSum += sold; revenue += sold * Cost[g] * Markup; groupDays++;
                    if (sold < demand) stockoutCount++;
                }
                // Hết ngày: hàng già thêm 1 ngày, quá hạn thì hỏng
                foreach (Lot lot in stock[g]) lot.age++;
                if (Life[g] > 0f)
                    for (int i = stock[g].Count - 1; i >= 0; i--)
                        if (stock[g][i].age >= Life[g]) { if (measure) spoiled += stock[g][i].qty * Cost[g]; stock[g].RemoveAt(i); }
            }
            f.Observe(new DemandForecaster.Observation
            {
                weather = (int)wx.weather, tempC = wx.tempC, dayOfWeek = GameManager.DateOfDay(d).DayOfWeek,
                groupHour = gh, availableHours = avail,
            });

            // Đặt hàng cho ngày mai
            int next = d + 1;
            var fc = WeatherSystem.Forecast(next);
            DayOfWeek nextDow = GameManager.DateOfDay(next).DayOfWeek;
            int p = d >= warmup ? policy : 1;
            for (int g = 0; g < G; g++)
            {
                int onHand = 0;
                foreach (Lot lot in stock[g]) onHand += lot.qty;
                bool perishable = Life[g] > 0f;
                int boxes;
                if (p == 0 || p == 1)
                {
                    List<int> hs = history[g];
                    double basis = 0;
                    if (p == 0) basis = hs[hs.Count - 1];
                    else { int n = Math.Min(7, hs.Count); for (int i = hs.Count - n; i < hs.Count; i++) basis += hs[i]; basis /= n; }
                    double target = Math.Max(basis * 1.2, PerBox[g]);
                    boxes = target > onHand ? (int)Math.Ceiling((target - onHand) / PerBox[g] - 1e-4) : 0;
                }
                else
                {
                    float mean = p == 2 ? f.PredictDay(g, (int)fc.weather, fc.tempC, nextDow) : (float)TrueDayDemand(next, g);
                    float sl = perishable ? RestockAdvisor.NewsvendorServiceLevel(Cost[g] * Markup, Cost[g], Life[g]) : RestockAdvisor.ServiceLevelDry;
                    boxes = RestockAdvisor.Boxes(RestockAdvisor.TargetStock(mean, sl) - onHand, PerBox[g], perishable);
                }
                if (boxes > 0)
                {
                    stock[g].Add(new Lot { qty = boxes * PerBox[g] });
                    if (d >= warmup) purchases += boxes * PerBox[g] * Cost[g];
                }
                if (measure) { int q = 0; foreach (Lot lot in stock[g]) q += lot.qty; inventorySum += q * Cost[g]; }
            }
        }
        double endInventory = 0;
        for (int g = 0; g < G; g++) foreach (Lot lot in stock[g]) endInventory += lot.qty * Cost[g];
        return new Result
        {
            fill = 100.0 * soldSum / Math.Max(1.0, demandSum),
            stockout = 100.0 * stockoutCount / Math.Max(1, groupDays),
            spoiled = spoiled,
            avgInventory = inventorySum / Math.Max(1, days - warmup),
            profit = revenue - purchases + endInventory,
        };
    }

    [Test]
    public void Advisor_CutsSpoilageWithoutLosingSales()
    {
        int[] seeds = { 1, 2, 3, 4, 5 };
        var mean = new Result[Names.Length];
        for (int p = 0; p < Names.Length; p++)
            foreach (int s in seeds)
            {
                Result r = Run(p, 120, s);
                mean[p].fill += r.fill / seeds.Length; mean[p].stockout += r.stockout / seeds.Length;
                mean[p].spoiled += r.spoiled / seeds.Length; mean[p].avgInventory += r.avgInventory / seeds.Length;
                mean[p].profit += r.profit / seeds.Length;
            }

        var sb = new StringBuilder();
        sb.AppendLine($"Restock policy evaluation ({DateTime.Now:yyyy-MM-dd HH:mm}), {seeds.Length} simulations x 120 days (days 15-120 measured), averages");
        sb.AppendLine($"{"Policy",-34} {"Fill %",7} {"Stockout %",11} {"Spoiled $",10} {"Avg stock $",12} {"Profit $",10}");
        for (int p = 0; p < Names.Length; p++)
            sb.AppendLine($"{Names[p],-34} {mean[p].fill,7:F1} {mean[p].stockout,11:F1} {mean[p].spoiled,10:F1} {mean[p].avgInventory,12:F1} {mean[p].profit,10:F0}");
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "inventory_eval.txt"), sb.ToString());
        Debug.Log(sb.ToString());

        Assert.Less(mean[2].spoiled, mean[1].spoiled * 0.5, "Advisor phải giảm hàng hỏng ít nhất một nửa so với trung bình 7 ngày");
        Assert.Greater(mean[2].fill, mean[0].fill, "Advisor phải đáp ứng nhu cầu tốt hơn cách đặt 'như hôm qua'");
        Assert.GreaterOrEqual(mean[3].profit, mean[2].profit, "Biết trước nhu cầu là giới hạn trên");
    }
}
