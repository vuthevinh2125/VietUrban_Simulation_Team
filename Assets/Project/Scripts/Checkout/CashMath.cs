using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Phép tính tiền ở quầy thu ngân, tách thành hàm thuần (không dùng Unity) để test EditMode:
/// khách trả tiền mặt bao nhiêu, tiền thối, khoản lỗ khi thối dư, và kiểm tra số tiền nhập trên máy quẹt thẻ.
/// Mọi số tiền tính bằng XU (cent, số nguyên) để không bị sai số làm tròn của số thực.
/// </summary>
public static class CashMath
{
    private static readonly int[] Notes = { 100, 500, 1000, 2000, 5000, 10000 }; // $1, $5, $10, $20, $50, $100

    /// <summary>Đổi đô la sang xu, làm tròn gần nhất (19.99 -> 1999, không phải 1998).</summary>
    public static int ToCents(float dollars) => (int)Math.Round(dollars * 100.0, MidpointRounding.AwayFromZero);

    /// <summary>Các mức tiền khách có thể đưa: số tiền làm tròn lên tờ $1 / $5 / $10 / $20 / $50 / $100, tăng dần, không trùng.</summary>
    public static List<int> CashOptions(int dueCents)
    {
        var options = new List<int>();
        foreach (int step in Notes)
        {
            int v = (int)Math.Ceiling(dueCents / (double)step) * step;
            if (v >= dueCents && !options.Contains(v)) options.Add(v);
        }
        if (options.Count == 0) options.Add(dueCents);
        return options;
    }

    /// <summary>Tiền phải thối.</summary>
    public static int ChangeDue(int cashGivenCents, int dueCents) => cashGivenCents - dueCents;

    /// <summary>Đủ tiền thối thì mới được xác nhận.</summary>
    public static bool CanConfirm(int cashGivenCents, int dueCents, int givingCents) => givingCents >= ChangeDue(cashGivenCents, dueCents);

    /// <summary>Khoản lỗ khi người chơi thối dư (0 nếu thối đúng).</summary>
    public static int LossFromChange(int cashGivenCents, int dueCents, int givingCents) =>
        Math.Max(0, givingCents - ChangeDue(cashGivenCents, dueCents));

    /// <summary>Số tiền gõ trên máy quẹt thẻ có đúng bằng số phải trả không ("12.34" với 1234 xu).</summary>
    public static bool CardEntryMatches(string entry, int dueCents) =>
        !string.IsNullOrEmpty(entry)
        && float.TryParse(entry, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
        && ToCents(v) == dueCents;
}
