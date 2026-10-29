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

    public static int XPForLevel(int level) => 100 + 60 * (level - 1);

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Level = 1;
        XP = 0;
        OnChanged = null;
        OnLevelUp = null;
    }
}
