using System;
using System.Collections.Generic;

/// <summary>
/// Bộ dự báo nhu cầu cho gợi ý nhập hàng: học từ SỐ BÁN THẬT đã ghi (BusinessStats.DayRecord.groupHour),
/// KHÔNG đọc công thức nhu cầu của game (DemandModel) -> độ chính xác đo được bằng cách so với nhu cầu thật.
///
/// Mô hình nhân (multiplicative), cho từng nhóm hàng g:
///     dự báo(g, ngày) = mức nền(g) x hệ số thời tiết(g, loại thời tiết) x hệ số nhiệt độ(g, mát / vừa / nóng) x hệ số thứ(g, thứ)
///  - Mức nền: làm trơn hàm mũ (exponential smoothing, alpha = 0.2) của số bán đã "khử" các hệ số -> theo kịp cửa hàng lớn dần.
///  - Mỗi hệ số = trung bình các tỉ lệ (số bán thật / phần dự báo còn lại) đã gặp, co về 1 với Shrink ngày giả
///    (ít dữ liệu thì hệ số gần 1, nhiều dữ liệu thì tin dữ liệu) -> không bị lệch khi mới thấy 1-2 ngày mưa.
///  - Tỉ trọng theo giờ: phân bố số bán trong ngày của từng nhóm (để dự báo "từ giờ tới lúc đóng cửa").
///  - Ngày đầu chưa có dữ liệu (cold start): mức nền mặc định ColdStartLevel món / ngày, mọi hệ số = 1.
///  - Ngày nhóm hàng bán 0 món khi chưa từng bán (chưa có giấy phép / chưa bày) thì bỏ qua, không kéo mức nền về 0.
/// Kết quả đánh giá (ForecastEvaluationTests, Unity 5/11, 5 lần mô phỏng x 120 ngày, WAPE): 24.4% với bản tin thời tiết,
/// 21.3% nếu biết trước thời tiết thật; trung bình 7 ngày 29.9%, "như hôm qua" 29.9%; giới hạn dưới do ngẫu nhiên 16.6%.
/// Hàm thuần (không dùng Unity) để chạy trong test EditMode.
/// </summary>
public class DemandForecaster
{
    public const int Groups = 10, Weathers = 5, TempBands = 3, Weekdays = 7, Hours = 24;

    public float Alpha = 0.2f;          // tốc độ cập nhật mức nền
    public float Shrink = 5f;           // số "ngày giả" có hệ số 1 (co hệ số về 1 khi ít dữ liệu)
    public float ColdStartLevel = 4f;   // món / ngày / nhóm khi chưa có dữ liệu
    public int OpenHour = 8, CloseHour = 18; // giờ mở cửa (làm mịn tỉ trọng giờ khi chưa có dữ liệu)
    public float MinAvailableShare = 0.25f;  // còn hàng dưới 25% thời gian bán trong ngày thì bỏ ngày đó (quá ít thông tin)

    /// <summary>Một ngày đã bán: thời tiết thật, nhiệt độ, thứ, số món theo nhóm x giờ (chỉ số g * 24 + giờ).</summary>
    public struct Observation
    {
        public int weather, tempC;
        public DayOfWeek dayOfWeek;
        public int[] groupHour;
        /// <summary>true = chỉ biết tổng cả ngày (save cũ trước khi ghi theo giờ): học mức nền / hệ số, không học tỉ trọng giờ.</summary>
        public bool noHours;
        /// <summary>
        /// Giờ nào nhóm hàng CÒN HÀNG trên kệ: mỗi nhóm một số nguyên, bit h = 1 nếu đầu giờ h còn hàng. null = không biết.
        /// Dùng để "khử kiểm duyệt" (uncensoring): hết hàng thì số bán thấp hơn nhu cầu thật; nếu cứ học số bán thì
        /// dự báo thấp dần -> đặt ít -> càng hết hàng (vòng xoáy, đo được trong InventoryPolicyTests: tỉ lệ đáp ứng 84% thay vì 96%).
        /// </summary>
        public int[] availableHours;
    }

    private readonly float[] level = new float[Groups];
    private readonly bool[] seen = new bool[Groups];
    private readonly float[,] wSum = new float[Groups, Weathers], wCnt = new float[Groups, Weathers];
    private readonly float[,] tSum = new float[Groups, TempBands], tCnt = new float[Groups, TempBands];
    private readonly float[,] dSum = new float[Groups, Weekdays], dCnt = new float[Groups, Weekdays];
    private readonly float[,] hourCount = new float[Groups, Hours];
    public int DaysObserved { get; private set; }

    public static int TempBand(int tempC) => tempC <= 22 ? 0 : tempC >= 32 ? 2 : 1;
    public static string TempBandName(int band) => band == 0 ? "cool" : band == 2 ? "hot" : "mild";

    private float Factor(float[,] sum, float[,] cnt, int g, int i) => (Shrink + sum[g, i]) / (Shrink + cnt[g, i]);
    public float WeatherFactor(int g, int weather) => Factor(wSum, wCnt, g, weather);
    public float TempFactor(int g, int tempC) => Factor(tSum, tCnt, g, TempBand(tempC));
    public float WeekdayFactor(int g, DayOfWeek d) => Factor(dSum, dCnt, g, (int)d);
    public float Level(int g) => seen[g] ? level[g] : ColdStartLevel;
    public bool HasData(int g) => seen[g];

    /// <summary>Số món dự báo bán cả ngày của nhóm g.</summary>
    public float PredictDay(int g, int weather, int tempC, DayOfWeek day) =>
        Level(g) * WeatherFactor(g, weather) * TempFactor(g, tempC) * WeekdayFactor(g, day);

    /// <summary>Tỉ trọng số bán của nhóm g trong khoảng giờ [fromHour, toHour) (chưa có dữ liệu: chia đều giờ mở cửa).</summary>
    public float HourShare(int g, int fromHour, int toHour)
    {
        int mask = 0;
        for (int h = Math.Max(0, fromHour); h < Math.Min(Hours, toHour); h++) mask |= 1 << h;
        return MaskShare(g, mask);
    }

    /// <summary>Tỉ trọng số bán của nhóm g trong các giờ có bit = 1.</summary>
    public float MaskShare(int g, int mask)
    {
        float total = 0f, part = 0f;
        for (int h = 0; h < Hours; h++)
        {
            float c = hourCount[g, h] + (h >= OpenHour && h < CloseHour ? 0.5f : 0f); // làm mịn: mỗi giờ mở cửa 0.5 món giả
            total += c;
            if ((mask & (1 << h)) != 0) part += c;
        }
        return total > 0f ? part / total : 0f;
    }

    /// <summary>Học thêm một ngày (gọi theo thứ tự thời gian).</summary>
    public void Observe(Observation o)
    {
        if (o.groupHour == null || o.groupHour.Length < Groups * Hours) return;
        DaysObserved++;
        int w = Clamp(o.weather, 0, Weathers - 1), tb = TempBand(o.tempC), d = (int)o.dayOfWeek;
        for (int g = 0; g < Groups; g++)
        {
            float total = 0f;
            for (int h = 0; h < Hours; h++) total += o.groupHour[g * Hours + h];

            // Khử kiểm duyệt: số bán chỉ đến từ các giờ còn hàng -> nhu cầu cả ngày ≈ số bán / tỉ trọng các giờ đó
            bool skip = false;
            int mask = o.availableHours != null && g < o.availableHours.Length ? o.availableHours[g] : -1;
            if (mask != -1 && !o.noHours)
            {
                float share = MaskShare(g, mask);
                if (share < MinAvailableShare) skip = true;
                else if (share < 0.999f) total /= share;
            }
            for (int h = 0; h < Hours; h++)
                if (!o.noHours && (mask == -1 || (mask & (1 << h)) != 0)) hourCount[g, h] += o.groupHour[g * Hours + h];
            if (skip) continue;
            if (total <= 0f && !seen[g]) continue; // nhóm chưa bán bao giờ (chưa có giấy phép / chưa bày)

            float fw = Factor(wSum, wCnt, g, w), ft = Factor(tSum, tCnt, g, tb), fd = Factor(dSum, dCnt, g, d);
            if (!seen[g])
            {
                seen[g] = true;
                level[g] = total / (fw * ft * fd);
                continue;
            }
            float baseLevel = Math.Max(0.01f, level[g]);
            // Mỗi hệ số học từ tỉ lệ: số bán thật / (mức nền x các hệ số còn lại)
            wSum[g, w] += total / (baseLevel * ft * fd); wCnt[g, w] += 1f;
            tSum[g, tb] += total / (baseLevel * fw * fd); tCnt[g, tb] += 1f;
            dSum[g, d] += total / (baseLevel * fw * ft); dCnt[g, d] += 1f;
            float all = Factor(wSum, wCnt, g, w) * Factor(tSum, tCnt, g, tb) * Factor(dSum, dCnt, g, d);
            level[g] = Alpha * (total / all) + (1f - Alpha) * baseLevel;
        }
    }

    /// <summary>Học lại từ đầu với danh sách ngày (cũ -> mới).</summary>
    public static DemandForecaster Train(IEnumerable<Observation> days)
    {
        var f = new DemandForecaster();
        foreach (Observation o in days) f.Observe(o);
        return f;
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
