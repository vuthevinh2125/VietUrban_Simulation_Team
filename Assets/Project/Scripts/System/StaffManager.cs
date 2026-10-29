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
    /// <summary>Thu ngân đang làm ở quầy nào (dùng cho công cụ lưu vị trí đứng).</summary>
    public IReadOnlyList<(CheckoutCounter counter, GameObject body)> Cashiers => cashiers;
    public int RestockerCount => restockers.Count;
    public const int MaxCashierLimit = 4;
    /// <summary>Mỗi quầy đang mở 1 thu ngân, tối đa 4.</summary>
    public int MaxCashiers => Mathf.Min(MaxCashierLimit, CheckoutCounter.All.Count);
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
        if (cashiers.Count >= MaxCashiers) { reason = cashiers.Count >= MaxCashierLimit ? "Max 4 cashiers" : "Unlock more counters"; return false; }
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
        GameObject body = SpawnWorker("Cashier", "Staff_Cashier", CashierSpot(counter, out Quaternion rot), rot, new Color(0.75f, 0.85f, 1f));
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
        GameObject body = SpawnWorker($"Restocker_{restockers.Count + 1}", "Staff_Restocker", pos, at.rotation, new Color(1f, 0.85f, 0.65f));
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
    /// <summary>
    /// Model nhân viên riêng theo vai trò: Resources/Staff_Cashier, Resources/Staff_Restocker
    /// (Tools > Store Setup > 10). Chưa có thì dùng Staff_Worker cũ, rồi tới model khách (khi đó nhuộm màu áo để phân biệt).
    /// </summary>
    private GameObject SpawnWorker(string name, string roleModel, Vector3 pos, Quaternion rot, Color tint)
    {
        GameObject prefab = Resources.Load<GameObject>(roleModel);
        bool ownModel = prefab != null;
        if (prefab == null) prefab = Resources.Load<GameObject>("Staff_Worker");
        if (prefab == null)
        {
            CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
            if (spawner != null && spawner.customerPrefabs != null && spawner.customerPrefabs.Length > 0)
                prefab = spawner.customerPrefabs[Random.Range(0, spawner.customerPrefabs.Length)];
            Debug.LogWarning("[Staff] Chưa có model nhân viên riêng - chạy Tools > Store Setup > 10. Build Staff Model. Tạm dùng model khách.");
        }
        if (prefab == null) { Debug.LogWarning("[Staff] Không có prefab nhân vật."); return null; }

        // Tạo dưới một gốc đang tắt để gỡ NPCShopper (nếu có) TRƯỚC khi nó kịp chạy
        var holder = new GameObject("_StaffHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(prefab, pos, rot, holder.transform);
        foreach (NPCShopper shopper in go.GetComponentsInChildren<NPCShopper>(true)) DestroyImmediate(shopper);
        go.transform.SetParent(null, true);
        Destroy(holder);
        go.name = name;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        // Chưa có model riêng: nhuộm màu áo để phân biệt thu ngân / xếp kệ
        if (!ownModel) foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.materials)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                else if (m.HasProperty("_Color")) m.color *= tint;
            }
        return go;
    }

    /// <summary>
    /// Chỗ thu ngân đứng: CẠNH quầy (bên không có khay đặt hàng), ngoài khung quầy, trên NavMesh và không vướng vật gì;
    /// không được thì ra phía sau quầy. Quay mặt về phía khách.
    /// </summary>
    private static Vector3 CashierSpot(CheckoutCounter counter, out Quaternion rot)
    {
        // Vị trí đã chỉnh tay (CashierPoint) được ưu tiên
        Transform point = counter.cashierPoint != null ? counter.cashierPoint : counter.transform.Find("CashierPoint");
        if (point != null)
        {
            rot = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
            return point.position;
        }

        Bounds b = default; bool any = false;
        foreach (Renderer r in counter.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        Vector3 center = any ? b.center : counter.transform.position;
        Vector3 customer = counter.customerPoint != null ? counter.customerPoint.position : center + counter.transform.forward;
        float floorY = customer.y;

        Vector3 toCustomer = customer - center; toCustomer.y = 0f;
        if (toCustomer.sqrMagnitude < 0.01f) toCustomer = counter.transform.forward;
        toCustomer.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toCustomer);
        // Bên nào xa khay đặt hàng hơn thì ưu tiên
        if (counter.itemDropPoint != null && Vector3.Dot(counter.itemDropPoint.position - center, side) > 0f) side = -side;

        float halfSide = any ? Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z)))) : 0.5f;
        float halfBack = any ? Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(toCustomer.x), 0f, Mathf.Abs(toCustomer.z)))) : 0.5f;

        Vector3[] candidates =
        {
            center + side * (halfSide + 0.45f) + toCustomer * 0.2f,
            center - side * (halfSide + 0.45f) + toCustomer * 0.2f,
            center - toCustomer * (halfBack + 0.45f),
            center + side * (halfSide + 0.45f) - toCustomer * 0.3f,
        };
        foreach (Vector3 c in candidates)
        {
            Vector3 p = new Vector3(c.x, floorY, c.z);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) p = hit.position;
            bool blocked = Physics.CheckCapsule(p + Vector3.up * 0.35f, p + Vector3.up * 1.6f, 0.22f, ~0, QueryTriggerInteraction.Ignore);
            if (blocked) continue;
            Vector3 face = customer - p; face.y = 0f;
            rot = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.identity;
            return p;
        }
        Vector3 fallback = new Vector3(candidates[0].x, floorY, candidates[0].z);
        Vector3 f = customer - fallback; f.y = 0f;
        rot = f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f) : Quaternion.identity;
        return fallback;
    }
}
