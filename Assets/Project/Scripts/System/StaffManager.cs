using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


public class StaffManager : MonoBehaviour
{
    public static StaffManager Instance { get; private set; }

    public const float CashierWage = 60f, RestockerWage = 50f, GuardWage = 70f, JanitorWage = 40f;
    public const int CashierLevel = 2, RestockerLevel = 3, MaxRestockers = 3, GuardLevel = 4, MaxGuards = 2, JanitorLevel = 2, MaxJanitors = 2;
    /// <summary>Độ ưu tiên tránh va chạm của nhân viên đi lại (số lớn = kém ưu tiên): khách mặc định 50 nên nhân viên nhường đường.</summary>
    public const int StaffAvoidancePriority = 70;
    /// <summary>
    /// Model khách được dành riêng cho nhân viên lau nhà (khách và người đi bộ không dùng model này nữa).
    /// Muốn đổi người khác thì sửa tên prefab ở đây (một trong Customer Prefabs của CustomerSpawner).
    /// </summary>
    public const string JanitorModel = "NPC_Ch26";

    private readonly List<(CheckoutCounter counter, GameObject body)> cashiers = new List<(CheckoutCounter, GameObject)>();
    private readonly List<GameObject> restockers = new List<GameObject>();
    private readonly List<GameObject> guards = new List<GameObject>();
    private readonly List<GameObject> janitors = new List<GameObject>();

    public int CashierCount => cashiers.Count;
    /// <summary>Thu ngân đang làm ở quầy nào (dùng cho công cụ lưu vị trí đứng).</summary>
    public IReadOnlyList<(CheckoutCounter counter, GameObject body)> Cashiers => cashiers;
    public int RestockerCount => restockers.Count;
    public int GuardCount => guards.Count;
    public int JanitorCount => janitors.Count;
    public const int MaxCashierLimit = 4;
    /// <summary>Mỗi quầy đang mở 1 thu ngân, tối đa 4.</summary>
    public int MaxCashiers => Mathf.Min(MaxCashierLimit, CheckoutCounter.All.Count);
    public float DailyWages => cashiers.Count * CashierWage + restockers.Count * RestockerWage + guards.Count * GuardWage + janitors.Count * JanitorWage;

    public static event System.Action OnStaffChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        CreateIfMissing();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => CreateIfMissing();

    private static void CreateIfMissing()
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

    // Thuê / cho nghỉ
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

    // Bảo vệ 
    public bool CanHireGuard(out string reason)
    {
        reason = "";
        if (StoreProgress.Level < GuardLevel) { reason = $"Requires level {GuardLevel}"; return false; }
        if (guards.Count >= MaxGuards) { reason = "Max guards"; return false; }
        return true;
    }

    public bool HireGuard(bool ignoreRequirements = false)
    {
        if (!ignoreRequirements && !CanHireGuard(out _)) return false;
        Vector3 pos = GuardPost(guards.Count, out Quaternion rot);
        GameObject body = SpawnWorker($"Guard_{guards.Count + 1}", "Staff_Worker", pos, rot, new Color(0.55f, 0.65f, 0.9f));
        if (body == null) return false;
        NavMeshAgent agent = body.GetComponent<NavMeshAgent>();
        if (agent == null) agent = body.AddComponent<NavMeshAgent>();
        agent.enabled = true;
        StoreGuard guard = body.AddComponent<StoreGuard>();
        guard.SetPost(pos, rot);
        guards.Add(body);
        Debug.Log($"[Staff] Thuê bảo vệ, gác tại {pos:F1}.");
        OnStaffChanged?.Invoke();
        return true;
    }

    public void FireGuard()
    {
        if (guards.Count == 0) return;
        GameObject last = guards[guards.Count - 1];
        guards.RemoveAt(guards.Count - 1);
        if (last != null) Destroy(last);
        OnStaffChanged?.Invoke();
    }

    public void LoadGuards(int count)
    {
        while (guards.Count > 0) FireGuard();
        for (int i = 0; i < count; i++) HireGuard(true);
    }

    // ================= Nhân viên lau nhà =================
    public bool CanHireJanitor(out string reason)
    {
        reason = "";
        if (StoreProgress.Level < JanitorLevel) { reason = $"Requires level {JanitorLevel}"; return false; }
        if (janitors.Count >= MaxJanitors) { reason = "Max janitors"; return false; }
        return true;
    }

    public bool HireJanitor(bool ignoreRequirements = false)
    {
        if (!ignoreRequirements && !CanHireJanitor(out _)) return false;
        Vector3 pos = TryIdleSpot(transform.position, janitors.Count, out Vector3 idle) ? idle : StaffEntrance(out _);
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        GameObject model = spawner != null ? spawner.FindPrefab(JanitorModel) : null;
        GameObject body = model != null
            ? SpawnFromPrefab(model, $"Janitor_{janitors.Count + 1}", pos, Quaternion.identity)
            : SpawnWorker($"Janitor_{janitors.Count + 1}", "Staff_Janitor", pos, Quaternion.identity, new Color(0.7f, 0.95f, 0.75f));
        if (body == null) return false;
        NavMeshAgent agent = body.GetComponent<NavMeshAgent>();
        if (agent == null) body.AddComponent<NavMeshAgent>();
        body.AddComponent<Janitor>();
        janitors.Add(body);
        Debug.Log("[Staff] Thuê nhân viên lau nhà.");
        OnStaffChanged?.Invoke();
        return true;
    }

    public void FireJanitor()
    {
        if (janitors.Count == 0) return;
        GameObject last = janitors[janitors.Count - 1];
        janitors.RemoveAt(janitors.Count - 1);
        if (last != null) Destroy(last);
        OnStaffChanged?.Invoke();
    }

    public void LoadJanitors(int count)
    {
        while (janitors.Count > 0) FireJanitor();
        for (int i = 0; i < count; i++) HireJanitor(true);
    }

    // ================= Chỗ nghỉ của nhân viên =================
    private Vector3 StaffEntrance(out Quaternion rot)
    {
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        Transform at = spawner != null && spawner.entrancePoint != null ? spawner.entrancePoint
                     : (spawner != null && spawner.spawnPoint != null ? spawner.spawnPoint : transform);
        rot = at.rotation;
        return NavMesh.SamplePosition(at.position, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : at.position;
    }

    /// <summary>
    /// Chỗ nhân viên (xếp kệ, lau nhà) đứng chờ khi hết việc, để không chắn lối đi của khách:
    /// cạnh một kệ kho (StorageRack, thường ở nhà kho), nếu chưa có thì cạnh chỗ nhận hàng, lệch sang một bên.
    /// index khác nhau để nhiều nhân viên không đứng chồng lên nhau.
    /// </summary>
    public static bool TryIdleSpot(Vector3 from, int index, out Vector3 spot)
    {
        spot = default;
        var racks = new List<StorageRack>();
        foreach (StorageRack r in StorageRack.All) if (r != null && r.isActiveAndEnabled) racks.Add(r);
        if (racks.Count > 0)
        {
            StorageRack rack = racks[Mathf.Abs(index) % racks.Count];
            spot = rack.StandPoint(from);
            Vector2 jitter = Random.insideUnitCircle * 0.6f;
            Vector3 p = spot + new Vector3(jitter.x, 0f, jitter.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit h, 1f, NavMesh.AllAreas)) spot = h.position;
            return true;
        }
        MarketManager market = MarketManager.Instance;
        if (market != null && market.deliveryPoint != null)
        {
            Transform d = market.deliveryPoint;
            Vector3 p = d.position + d.right * (3f + (Mathf.Abs(index) % 3) * 1.2f);
            if (NavMesh.SamplePosition(p, out NavMeshHit h, 3f, NavMesh.AllAreas)) { spot = h.position; return true; }
        }
        return false;
    }

    /// <summary>
    /// Chỗ gác: ngoài cửa ra vào, lệch sang hai bên cửa (người thứ 2 đứng phía bên kia), quay mặt ra đường.
    /// </summary>
    private Vector3 GuardPost(int index, out Quaternion rot)
    {
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
        Vector3 exit = spawner != null && spawner.exitPoint != null ? spawner.exitPoint.position
                     : (spawner != null && spawner.spawnPoint != null ? spawner.spawnPoint.position : transform.position);

        // Cửa ra vào gần điểm ra về nhất
        Vector3 door = exit;
        float best = float.MaxValue;
        foreach (EntranceDoor d in FindObjectsByType<EntranceDoor>(FindObjectsSortMode.None))
        {
            float dist = Vector3.Distance(d.transform.position, exit);
            if (dist < best) { best = dist; door = d.transform.position; }
        }

        Vector3 store = Vector3.zero; int n = 0;
        foreach (Shelf sh in StockManager.Shelves) if (sh != null) { store += sh.transform.position; n++; }
        Vector3 outward = n > 0 ? door - store / n : Vector3.forward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
        outward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, outward) * (index % 2 == 0 ? 1f : -1f);

        Vector3 p = door + outward * 2.2f + side * 2.6f;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) p = hit.position;
        rot = Quaternion.LookRotation(outward);
        return p;
    }

    /// <summary>Trả lương cuối ngày (GameManager gọi trước khi chốt sổ).</summary>
    public void PayWages()
    {
        float total = DailyWages;
        if (total > 0f) EconomyManager.Instance.Charge(total, MoneyCategory.Wages, $"Staff wages ({cashiers.Count} cashier, {restockers.Count} restocker, {guards.Count} guard, {janitors.Count} janitor)");
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

        GameObject go = SpawnFromPrefab(prefab, name, pos, rot);

        // Chưa có model riêng: nhuộm màu áo để phân biệt thu ngân / xếp kệ
        if (!ownModel) foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.materials)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                else if (m.HasProperty("_Color")) m.color *= tint;
            }
        return go;
    }

    /// <summary>Tạo nhân viên từ một prefab nhân vật: gỡ script mua sắm (nếu là model khách) trước khi nó kịp chạy.</summary>
    private static GameObject SpawnFromPrefab(GameObject prefab, string name, Vector3 pos, Quaternion rot)
    {
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
