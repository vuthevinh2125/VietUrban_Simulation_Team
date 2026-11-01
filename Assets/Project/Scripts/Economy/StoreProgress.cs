using System;
using UnityEngine;

/// <summary>
/// Cấp độ cửa hàng (giống Supermarket Simulator): mỗi lần khách thanh toán được kinh nghiệm (XP),
/// đủ XP thì lên cấp. Cấp càng cao càng mua được nhiều giấy phép mặt hàng.
/// </summary>
public static class StoreProgress
{
    public static int Level { get; private set; } = 1;
    public static int XP { get; private set; }

    /// <summary>XP cần để lên cấp kế tiếp.</summary>
    public static int XPToNext => XPForLevel(Level);
    public static float Progress01 => Mathf.Clamp01(XP / (float)Mathf.Max(1, XPToNext));

    public static event Action OnChanged;
    public static event Action<int> OnLevelUp;

    /// <summary>
    /// XP cần để lên cấp kế tiếp. Cân bằng 01/11: trước là 100 + 60(L-1) (tuyến tính, Lv1 chậm, cấp cao quá dễ);
    /// thử 60 + 30(L-1) + 8(L-1)^2 thì quá nhanh (Lv4 ngay ngày 1);
    /// nay 80 + 100(L-1) + 30(L-1)^2: Lv1 80, Lv2 210, Lv3 400, Lv4 650, Lv5 960 ... Lv9 2800
    /// (~Lv2 giữa ngày 1, Lv3 cuối ngày 1, Lv4 ngày 2, Lv5 ngày 3, sau đó mỗi cấp 1.5-3 ngày).
    /// </summary>
    public static int XPForLevel(int level)
    {
        int k = Mathf.Max(0, level - 1);
        return 80 + 100 * k + 30 * k * k;
    }

    /// <summary>XP cho một lượt thanh toán: 1 XP mỗi $1 + 1 XP mỗi món.</summary>
    public static int XPForSale(float total, int items) => Mathf.Max(1, Mathf.RoundToInt(total) + items);

    public static void AddXP(int amount)
    {
        if (amount <= 0) return;
        XP += amount;
        while (XP >= XPToNext)
        {
            XP -= XPToNext;
            Level++;
            Debug.Log($"[Progress] Lên cấp cửa hàng {Level}!");
            OnLevelUp?.Invoke(Level);
        }
        OnChanged?.Invoke();
    }

    /// <summary>Nạp từ file lưu.</summary>
    public static void Load(int level, int xp)
    {
        Level = Mathf.Max(1, level);
        XP = Mathf.Max(0, xp);
        OnChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Level = 1;
        XP = 0;
        OnChanged = null;
        OnLevelUp = null;
    }
}
