using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Đánh giá bộ dự báo nhu cầu (DemandForecaster) bằng mô phỏng, chạy trong Test Runner (EditMode).
///
/// Cách làm:
///  1. Mô phỏng N ngày bán hàng. Nhu cầu THẬT mỗi giờ của nhóm g = tỉ lệ mua của nhóm x số khách (theo thời tiết, mùa,
///     cửa hàng lớn dần 1% / ngày) x hệ số DemandModel.Multiplier; số món bán = biến ngẫu nhiên Poisson quanh nhu cầu đó.
///     Thời tiết lấy từ WeatherSystem.Actual (giống trong game), bản tin dự báo từ WeatherSystem.Forecast (đúng 80%).
///  2. Mỗi ngày (sau 14 ngày khởi động), các phương pháp dự báo số món cả ngày của từng nhóm TRƯỚC khi biết số bán,
///     rồi so với số bán thật. Bộ dự báo chỉ được học từ số bán các ngày trước (không đọc DemandModel).
///  3. Thước đo: WAPE = tổng |dự báo - thật| / tổng thật (sai số tuyệt đối theo tỉ lệ, không bị chia 0 như MAPE khi
///     một nhóm bán 0 món).
/// Các phương pháp: "như hôm qua" (naive), "như cùng thứ tuần trước" (seasonal naive), trung bình 7 ngày (MA7),
/// bộ dự báo với thời tiết dự báo (dùng thật trong game), bộ dự báo nếu biết trước thời tiết thật, và giới hạn dưới
/// (biết nhu cầu thật, chỉ còn sai số ngẫu nhiên Poisson - không phương pháp nào làm tốt hơn).
/// Kết quả ghi vào forecast_eval.txt / forecast_eval_daily.csv trong thư mục save (Application.persistentDataPath).
/// </summary>
public class ForecastEvaluationTests
{
    private static readonly float[] Rate = { 0.5f, 0.3f, 0.15f, 0.25f, 0.45f, 0.35f, 0.2f, 0.4f, 0.2f, 0.1f }; // món / khách / giờ (tỉ lệ)
    private const int OpenHour = 8, CloseHour = 18;
    private static readonly string[] Methods = { "Naive (yesterday)", "Seasonal naive (same weekday last week)", "Moving average 7 days", "Forecaster + weather forecast", "Forecaster + actual weather", "Lower bound (true demand)" };

    private static float Lambda(int day, int g, int hour, WeatherSystem.DayWeather wx)
    {
        DayOfWeek dow = GameManager.DateOfDay(day).DayOfWeek;
        float growth = 1f + 0.01f * (day - 1);
        return Rate[g] * 4f * growth * WeatherSystem.CustomerMultiplierFor(wx.weather, wx.season)
               * DemandModel.Multiplier((ProductGroup)g, hour + 0.5f, wx.weather, wx.season, wx.tempC, dow);
    }

    private static int Poisson(System.Random rng, double l)
    {
        if (l <= 0) return 0;
        if (l > 30)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Math.Max(0, (int)Math.Round(l + z * Math.Sqrt(l)));
        }
        double limit = Math.Exp(-l), p = 1.0;
        int k = 0;
        while (true) { p *= rng.NextDouble(); if (p <= limit) return k; k++; }
    }

    private struct SimDay { public int day; public WeatherSystem.DayWeather wx; public int[] gh; }

    private static List<SimDay> Simulate(int days, int seed)
    {
        var rng = new System.Random(seed);
        var list = new List<SimDay>();
        for (int d = 1; d <= days; d++)
        {
            var wx = WeatherSystem.Actual(d);
            var gh = new int[DemandForecaster.Groups * 24];
            for (int g = 0; g < DemandForecaster.Groups; g++)
                for (int h = OpenHour; h < CloseHour; h++)
                    gh[g * 24 + h] = Poisson(rng, Lambda(d, g, h, wx));
            list.Add(new SimDay { day = d, wx = wx, gh = gh });
        }
        return list;
    }

    private static int DayTotal(SimDay s, int g) { int t = 0; for (int h = 0; h < 24; h++) t += s.gh[g * 24 + h]; return t; }

    /// <summary>Trả về WAPE (%) của từng phương pháp; daily (tuỳ chọn) nhận từng dòng chi tiết.</summary>
    private static double[] Evaluate(int days, int seed, int warmup, StringBuilder daily = null)
    {
        List<SimDay> sim = Simulate(days, seed);
        var f = new DemandForecaster();
        double[] err = new double[Methods.Length];
        double actualSum = 0;
        for (int i = 0; i < sim.Count; i++)
        {
            SimDay s = sim[i];
            DayOfWeek dow = GameManager.DateOfDay(s.day).DayOfWeek;
            if (i >= warmup)
            {
                var fc = WeatherSystem.Forecast(s.day);
                for (int g = 0; g < DemandForecaster.Groups; g++)
                {
                    double actual = DayTotal(s, g);
                    double ma = 0; for (int j = i - 7; j < i; j++) ma += DayTotal(sim[j], g); ma /= 7.0;
                    double truth = 0; for (int h = OpenHour; h < CloseHour; h++) truth += Lambda(s.day, g, h, s.wx);
                    double[] pred =
                    {
                        DayTotal(sim[i - 1], g),
                        DayTotal(sim[i - 7], g),
                        ma,
                        f.PredictDay(g, (int)fc.weather, fc.tempC, dow),
                        f.PredictDay(g, (int)s.wx.weather, s.wx.tempC, dow),
                        truth,
                    };
                    for (int m = 0; m < pred.Length; m++) err[m] += Math.Abs(pred[m] - actual);
                    actualSum += actual;
                    daily?.AppendLine(string.Join(",", seed, s.day, (ProductGroup)g, s.wx.weather, s.wx.tempC, fc.weather, fc.tempC,
                        actual.ToString(CultureInfo.InvariantCulture), pred[2].ToString("F2", CultureInfo.InvariantCulture),
                        pred[3].ToString("F2", CultureInfo.InvariantCulture), pred[4].ToString("F2", CultureInfo.InvariantCulture),
                        truth.ToString("F2", CultureInfo.InvariantCulture)));
                }
            }
            f.Observe(new DemandForecaster.Observation { weather = (int)s.wx.weather, tempC = s.wx.tempC, dayOfWeek = dow, groupHour = s.gh });
        }
        for (int m = 0; m < err.Length; m++) err[m] = 100.0 * err[m] / Math.Max(1.0, actualSum);
        return err;
    }

    [Test]
    public void Forecaster_BeatsSimpleBaselines_Over120SimulatedDays()
    {
        int[] seeds = { 1, 2, 3, 4, 5 };
        var sb = new StringBuilder();
        var daily = new StringBuilder("seed,day,group,weather,tempC,forecastWeather,forecastTempC,actual,ma7,forecaster_forecastWeather,forecaster_actualWeather,trueDemand\n");
        sb.AppendLine($"Demand forecaster evaluation ({DateTime.Now:yyyy-MM-dd HH:mm}), {seeds.Length} simulations x 120 days, first 14 days warm-up, WAPE % (lower is better)");
        foreach (int horizon in new[] { 30, 60, 120 })
        {
            double[] mean = new double[Methods.Length];
            foreach (int seed in seeds)
            {
                double[] e = Evaluate(horizon, seed, horizon <= 30 ? 7 : 14, horizon == 120 ? daily : null);
                for (int m = 0; m < mean.Length; m++) mean[m] += e[m] / seeds.Length;
            }
            sb.AppendLine($"\n{horizon} days:");
            for (int m = 0; m < Methods.Length; m++) sb.AppendLine($"  {Methods[m],-42} {mean[m],6:F1}");
            if (horizon == 120)
            {
                Assert.Less(mean[3], mean[0], "Bộ dự báo phải tốt hơn cách đoán 'như hôm qua'");
                Assert.Less(mean[4], mean[2], "Biết thời tiết thật, bộ dự báo phải tốt hơn trung bình 7 ngày");
                Assert.Less(mean[5], mean[4], "Không phương pháp nào tốt hơn giới hạn dưới");
            }
        }
        string dir = Application.persistentDataPath;
        File.WriteAllText(Path.Combine(dir, "forecast_eval.txt"), sb.ToString());
        File.WriteAllText(Path.Combine(dir, "forecast_eval_daily.csv"), daily.ToString());
        Debug.Log(sb.ToString());
    }

    [Test]
    public void WeatherForecast_IsRightAboutEightyPercentOfDays()
    {
        int right = 0, n = 2000;
        for (int d = 1; d <= n; d++) if (WeatherSystem.Forecast(d).weather == WeatherSystem.Actual(d).weather) right++;
        Assert.That(right / (float)n, Is.InRange(0.75f, 0.85f));
    }

    [Test]
    public void Forecaster_ColdStart_UsesDefaultLevel()
    {
        var f = new DemandForecaster();
        Assert.AreEqual(f.ColdStartLevel, f.PredictDay(0, 0, 25, DayOfWeek.Monday), 1e-4);
        Assert.IsFalse(f.HasData(0));
    }

    [Test]
    public void Forecaster_LearnsThatRainSellsMorePantry()
    {
        // 40 ngày: nắng bán 10 món đồ khô, mưa bán 20 -> hệ số mưa / nắng phải xấp xỉ 2
        var f = new DemandForecaster();
        for (int d = 0; d < 40; d++)
        {
            bool rain = d % 2 == 1;
            var gh = new int[DemandForecaster.Groups * 24];
            gh[(int)ProductGroup.Pantry * 24 + 12] = rain ? 20 : 10;
            f.Observe(new DemandForecaster.Observation { weather = rain ? 2 : 0, tempC = 25, dayOfWeek = (DayOfWeek)(d % 7), groupHour = gh });
        }
        float sunny = f.PredictDay((int)ProductGroup.Pantry, 0, 25, DayOfWeek.Monday);
        float rainy = f.PredictDay((int)ProductGroup.Pantry, 2, 25, DayOfWeek.Monday);
        Assert.That(rainy / sunny, Is.InRange(1.6f, 2.4f));
    }
}
