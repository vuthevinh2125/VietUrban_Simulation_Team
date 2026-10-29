using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phần mở rộng cửa hàng mua bằng tiền (giống Supermarket Simulator): phòng mở rộng siêu thị, nhà kho bên cạnh...
/// Khi CHƯA mua: các object "showOnUnlock" bị ẩn, "hideOnUnlock" (vd. bức tường ngăn) vẫn hiện.
/// Mua xong: hiện phần mở rộng, ẩn tường ngăn -> người và khách đi vào được (tường có NavMeshObstacle nên ẩn là thông đường).
/// Tạo bằng Tools > Store Setup > 15 (nhà kho) / 16 (mở rộng siêu thị). Mua ở tab "Expand" trên máy tính.
/// </summary>
[DefaultExecutionOrder(-90)]
public class StoreExpansion : MonoBehaviour
{
    public static readonly List<StoreExpansion> All = new List<StoreExpansion>();
    private static readonly HashSet<string> unlocked = new HashSet<string>();
    public static event Action OnChanged;

    public string id;
    public string displayName = "Store expansion";
    [TextArea] public string description = "";
    public int order = 1;
    public int requiredLevel = 3;
    public float price = 2000f;
    public GameObject[] showOnUnlock;
    public GameObject[] hideOnUnlock;
    [Tooltip("Kệ/tủ kê sát bức tường được mở: mua xong dời ra sát tường mới (hàng trên kệ đi theo).")]
    public Transform[] moveOnUnlock;
    public Vector3 moveOffset;

    private Vector3[] homePositions;

    public bool IsUnlocked => unlocked.Contains(id);

    void Awake()
    {
        if (string.IsNullOrEmpty(id)) id = $"{name}@{transform.position.x:F0},{transform.position.z:F0}";
        if (!All.Contains(this)) All.Add(this);
        All.Sort((a, b) => a.order.CompareTo(b.order));
        if (moveOnUnlock != null)
        {
            homePositions = new Vector3[moveOnUnlock.Length];
            for (int i = 0; i < moveOnUnlock.Length; i++) if (moveOnUnlock[i] != null) homePositions[i] = moveOnUnlock[i].position;
        }
        Apply();
    }

    void OnDestroy() => All.Remove(this);

    private void Apply()
    {
        bool on = IsUnlocked;
        if (showOnUnlock != null) foreach (GameObject g in showOnUnlock) if (g != null && g != gameObject) g.SetActive(on);
        if (hideOnUnlock != null) foreach (GameObject g in hideOnUnlock) if (g != null && g != gameObject) g.SetActive(!on);
        if (moveOnUnlock != null && homePositions != null)
            for (int i = 0; i < moveOnUnlock.Length; i++)
                if (moveOnUnlock[i] != null) moveOnUnlock[i].position = homePositions[i] + (on ? moveOffset : Vector3.zero);
    }

    public bool CanBuy(out string reason)
    {
        reason = "";
        if (IsUnlocked) { reason = "Owned"; return false; }
        if (StoreProgress.Level < requiredLevel) { reason = $"Requires level {requiredLevel}"; return false; }
        if (EconomyManager.Instance == null || !EconomyManager.Instance.CanAfford(price)) { reason = "Not enough money"; return false; }
        return true;
    }

    public bool TryBuy()
    {
        if (!CanBuy(out _)) return false;
        if (!EconomyManager.Instance.TrySpend(price, MoneyCategory.Upgrades, $"Expansion: {displayName}")) return false;
        unlocked.Add(id);
        Apply();
        ProgressHUD.Toast($"{displayName} is now open!");
        AudioManager.Play("level_up", 0.9f);
        OnChanged?.Invoke();
        return true;
    }

    // ================= Lưu / nạp =================
    public static List<string> GetUnlockedIds() => new List<string>(unlocked);

    public static void LoadUnlocked(IEnumerable<string> ids)
    {
        unlocked.Clear();
        if (ids != null) foreach (string i in ids) unlocked.Add(i);
        foreach (StoreExpansion e in All) if (e != null) e.Apply();
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
