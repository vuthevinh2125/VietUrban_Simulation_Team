using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quầy thu ngân mở rộng (giống Supermarket Simulator): ẩn khi bắt đầu game, đủ cấp cửa hàng + đủ tiền thì mua để mở.
/// Mở theo thứ tự "order". Tối đa 4 quầy (1 quầy chính + 3 quầy mở rộng) -> tối đa 4 thu ngân.
/// Gắn bằng Tools > Store Setup > 11. Setup Extra Checkouts.
/// </summary>
[DefaultExecutionOrder(-100)]
public class LockedCheckout : MonoBehaviour
{
    public static readonly List<LockedCheckout> All = new List<LockedCheckout>();
    private static readonly HashSet<string> unlocked = new HashSet<string>();
    public static event Action OnChanged;

    [Tooltip("Mã nhận dạng (tự tạo theo vị trí) - dùng cho file lưu")]
    public string id;
    public int order = 1;
    public int requiredLevel = 3;
    public float price = 800f;
    public string displayName = "Checkout counter";

    public bool IsUnlocked => unlocked.Contains(id);

    void Awake()
    {
        if (string.IsNullOrEmpty(id)) id = $"{name}@{transform.position.x:F1},{transform.position.z:F1}";
        if (!All.Contains(this)) All.Add(this);
        All.Sort((a, b) => a.order.CompareTo(b.order));
        if (!IsUnlocked) gameObject.SetActive(false); // ẩn tới khi mua
    }

    void OnDestroy() => All.Remove(this);

    /// <summary>Quầy kế tiếp chưa mở (null = đã mở hết).</summary>
    public static LockedCheckout Next()
    {
        foreach (LockedCheckout c in All) if (c != null && !c.IsUnlocked) return c;
        return null;
    }

    public static int UnlockedCount { get { int n = 0; foreach (LockedCheckout c in All) if (c != null && c.IsUnlocked) n++; return n; } }

    public static bool CanUnlockNext(out string reason)
    {
        reason = "";
        LockedCheckout next = Next();
        if (next == null) { reason = "All counters open"; return false; }
        if (StoreProgress.Level < next.requiredLevel) { reason = $"Requires level {next.requiredLevel}"; return false; }
        if (EconomyManager.Instance == null || !EconomyManager.Instance.CanAfford(next.price)) { reason = "Not enough money"; return false; }
        return true;
    }

    public static bool TryUnlockNext()
    {
        if (!CanUnlockNext(out _)) return false;
        LockedCheckout next = Next();
        if (!EconomyManager.Instance.TrySpend(next.price, MoneyCategory.Upgrades, $"Unlock {next.displayName}")) return false;
        next.SetUnlocked(true);
        ProgressHUD.Toast($"{next.displayName} is now open!");
        Debug.Log($"[Checkout] Mở quầy mới: {next.name}.");
        return true;
    }

    private void SetUnlocked(bool value)
    {
        if (value) unlocked.Add(id); else unlocked.Remove(id);
        gameObject.SetActive(value);
        OnChanged?.Invoke();
    }

    // ================= Lưu / nạp =================
    public static List<string> GetUnlockedIds() => new List<string>(unlocked);

    public static void LoadUnlocked(IEnumerable<string> ids)
    {
        unlocked.Clear();
        if (ids != null) foreach (string i in ids) unlocked.Add(i);
        foreach (LockedCheckout c in All) if (c != null) c.gameObject.SetActive(c.IsUnlocked);
        OnChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        unlocked.Clear();
        OnChanged = null;
    }
}
