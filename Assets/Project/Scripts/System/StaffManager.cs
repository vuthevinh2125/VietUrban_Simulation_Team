using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nhân viên (giống Supermarket Simulator):
///  - Thu ngân: đứng ở quầy, tự quét hàng và nhận tiền (bật Auto Scan của quầy). Lương $60/ngày, cần cấp 2.
///  - Nhân viên xếp kệ: tự lấy thùng hàng mang tới kệ đang bán đúng mặt hàng đó và xếp lên. Lương $50/ngày, cần cấp 3.
/// Lương trừ vào cuối mỗi ngày (mục Wages trong báo cáo). Thuê / cho nghỉ trên máy tính (tab Staff).
/// </summary>
public class StaffManager : MonoBehaviour
{
    public static StaffManager Instance { get; private set; }

    public const float CashierWage = 60f, RestockerWage = 50f;
    public const int CashierLevel = 2, RestockerLevel = 3, MaxRestockers = 3;

    private readonly List<(CheckoutCounter counter, GameObject body)> cashiers = new List<(CheckoutCounter, GameObject)>();
    private readonly List<GameObject> restockers = new List<GameObject>();

    public int CashierCount => cashiers.Count;
    public int RestockerCount => restockers.Count;
    public int MaxCashiers => CheckoutCounter.All.Count;
    public float DailyWages => cashiers.Count * CashierWage + restockers.Count * RestockerWage;

    public static event System.Action OnStaffChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance == null) new GameObject("StaffManager").AddComponent<StaffManager>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnStaffChanged = null;

    // ================= Thuê / cho nghỉ =================
    public bool CanHireCashier(out string reason)
    {
        reason = "";
        if (StoreProgress.Level < CashierLevel) { reason = $"Requires level {CashierLevel}"; return false; }
        if (cashiers.Count >= MaxCashiers) { reason = "All counters staffed"; return false; }
        return true;
    }

    public bool CanHireRestocker(out string reason)
    {
        reason = "";
        if (StoreProgress.Level < RestockerLevel) { reason = $"Requires level {RestockerLevel}"; return false; }
        if (restockers.Count >= MaxRestockers) { reason = "Max restockers"; return false; }
        return true;
    }

    public bool HireCashier(bool ignoreRequirements = false)
    {
        if (!ignoreRequirements && !CanHireCashier(out _)) return false;
        CheckoutCounter counter = null;
        foreach (CheckoutCounter c in CheckoutCounter.All)
            if (c != null && !cashiers.Exists(x => x.counter == c)) { counter = c; break; }
        if (counter == null) return false;

        counter.autoScan = true;
        GameObject body = SpawnWorker("Cashier", CashierSpot(counter, out Quaternion rot), rot);
        if (body != null)
        {
            NavMeshAgent agent = body.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false; // đứng yên sau quầy
        }
        cashiers.Add((counter, body));
        Debug.Log($"[Staff] Thuê thu ngân cho quầy {counter.name}.");
        OnStaffChanged?.Invoke();
        return true;
    }

    public void FireCashier()
    {
        if (cashiers.Count == 0) return;
        var last = cashiers[cashiers.Count - 1];
        cashiers.RemoveAt(cashiers.Count - 1);
        if (last.counter != null) last.counter.autoScan = false;
        if (last.body != null) Destroy(last.body);
        OnStaffChanged?.Invoke();
    }

    public bool HireRestocker(bool ignoreRequirements = false)
    {
        if (!ignoreRequirements && !CanHireRestocker(out _)) return false;
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        Transform at = spawner != null && spawner.entrancePoint != null ? spawner.entrancePoint
                     : (spawner != null && spawner.spawnPoint != null ? spawner.spawnPoint : transform);
        Vector3 pos = NavMesh.SamplePosition(at.position, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : at.position;
        GameObject body = SpawnWorker($"Restocker_{restockers.Count + 1}", pos, at.rotation);
        if (body == null) return false;
        body.AddComponent<Restocker>();
        restockers.Add(body);
        Debug.Log("[Staff] Thuê nhân viên xếp kệ.");
        OnStaffChanged?.Invoke();
        return true;
    }

    public void FireRestocker()
    {
        if (restockers.Count == 0) return;
        GameObject last = restockers[restockers.Count - 1];
        restockers.RemoveAt(restockers.Count - 1);
        if (last != null)
        {
            Restocker r = last.GetComponent<Restocker>();
            if (r != null) r.PrepareToLeave();
            Destroy(last);
        }
        OnStaffChanged?.Invoke();
    }

    /// <summary>Trả lương cuối ngày (GameManager gọi trước khi chốt sổ).</summary>
    public void PayWages()
    {
        float total = DailyWages;
        if (total > 0f) EconomyManager.Instance.Charge(total, MoneyCategory.Wages, $"Staff wages ({cashiers.Count} cashier, {restockers.Count} restocker)");
    }

    /// <summary>Nạp từ file lưu.</summary>
    public void LoadCounts(int cashierCount, int restockerCount)
    {
        while (cashiers.Count > 0) FireCashier();
        while (restockers.Count > 0) FireRestocker();
        for (int i = 0; i < cashierCount; i++) HireCashier(true);
        for (int i = 0; i < restockerCount; i++) HireRestocker(true);
    }

    // ================= Tạo nhân vật =================
    /// <summary>Dùng chung model với khách (prefab của CustomerSpawner) nhưng bỏ "bộ não" khách hàng.</summary>
    private GameObject SpawnWorker(string name, Vector3 pos, Quaternion rot)
    {
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        GameObject prefab = null;
        if (spawner != null && spawner.customerPrefabs != null && spawner.customerPrefabs.Length > 0)
            prefab = spawner.customerPrefabs[Random.Range(0, spawner.customerPrefabs.Length)];
        if (prefab == null) { Debug.LogWarning("[Staff] Không có prefab nhân vật (CustomerSpawner.customerPrefabs)."); return null; }

        // Tạo dưới một gốc đang tắt để gỡ NPCShopper TRƯỚC khi nó kịp chạy
        var holder = new GameObject("_StaffHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(prefab, pos, rot, holder.transform);
        foreach (NPCShopper shopper in go.GetComponentsInChildren<NPCShopper>(true)) DestroyImmediate(shopper);
        go.transform.SetParent(null, true);
        Destroy(holder);
        go.name = name;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        return go;
    }

    /// <summary>Chỗ thu ngân đứng: phía bên kia quầy so với khách, quay mặt về phía khách.</summary>
    private static Vector3 CashierSpot(CheckoutCounter counter, out Quaternion rot)
    {
        Vector3 center = counter.transform.position;
        Vector3 customer = counter.customerPoint != null ? counter.customerPoint.position : center + counter.transform.forward;
        Vector3 dir = center - customer;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -counter.transform.forward;
        dir.Normalize();
        Vector3 pos = center + dir * 0.9f;
        pos.y = customer.y; // điểm khách đứng nằm trên sàn
        rot = Quaternion.LookRotation(-dir);
        return pos;
    }
}
