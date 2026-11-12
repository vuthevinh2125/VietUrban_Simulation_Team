using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Người đi bộ trên phố: dùng các model khách hàng (bỏ script mua sắm), đi giữa các toà nhà ở khu phố mới
/// và vỉa hè trước siêu thị, tới nơi đứng một lúc rồi đi tiếp. Trời mưa / giông / ban đêm thì ít người hơn.
/// Tự tạo khi vào game (chỉ khi có khu phố mới).
/// </summary>
public class PedestrianSystem : MonoBehaviour
{
    public static PedestrianSystem Instance { get; private set; }

    public int maxWalkers = 22;
    public Vector2 walkSpeed = new Vector2(1.1f, 1.6f);

    private class Walker
    {
        public NavMeshAgent agent;
        public Animator animator;
        public bool hasSpeed;
        public float waitUntil;
    }
    private readonly List<Walker> walkers = new List<Walker>();
    private readonly List<Vector3> targets = new List<Vector3>();
    private float nextCheck;
    // Chỉ đi trên vỉa hè (vùng NavMesh "Sidewalks"): trước dùng mọi vùng nên người đi bộ băng qua giữa đường / giữa ngã tư
    // đang có xe chạy. Sang đường chỉ ở chỗ vỉa hè nối liền (vạch sang đường được bake là Sidewalks).
    private int walkMask = NavMesh.AllAreas;
    private float vendorChance;
    private static NavMeshPath path;

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
        if (Instance == null && (GameObject.Find("CityExpansion_East") != null || FindAnyObjectByType<VietDistrict>() != null) && SupermarketScene.IsActive)
            new GameObject("PedestrianSystem").AddComponent<PedestrianSystem>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        int sidewalks = NavMesh.GetAreaFromName("Sidewalks");
        if (sidewalks >= 0) walkMask = 1 << sidewalks;
        if (path == null) path = new NavMeshPath();
        if (FindAnyObjectByType<VietDistrict>() != null) { maxWalkers = Mathf.Max(maxWalkers, 70); vendorChance = 0.3f; } // phố Việt đông người hơn
        // Điểm đến: cửa các toà nhà khu mới + một dãy điểm trên vỉa hè đại lộ cũ trước siêu thị
        foreach (DeliveryAddress a in DeliveryAddress.All) if (a != null) targets.Add(a.doorPoint);
        // Đường dạo quanh Hồ Gươm, lối đi công viên: thêm nhiều lần để người đi bộ hay tới đi dạo
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (city != null) for (int k = 0; k < 6; k++) targets.AddRange(city.WalkPoints);
        for (float x = -170f; x <= 10f; x += 20f) { targets.Add(new Vector3(x, 0.2f, 18f)); targets.Add(new Vector3(x, 0.2f, -2f)); }
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (NavMesh.SamplePosition(targets[i], out NavMeshHit hit, 3f, walkMask)) targets[i] = hit.position;
            else targets.RemoveAt(i);
        }
        Debug.Log($"[Pedestrian] {targets.Count} điểm đến trên vỉa hè (vùng Sidewalks = {sidewalks}), tối đa {maxWalkers} người đi bộ");
    }

    void Update()
    {
        if (targets.Count < 2) return;

        // Số người mong muốn theo thời tiết và giờ
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            int want = Mathf.RoundToInt(maxWalkers * Density());
            RecycleFar();
            for (int k = 0; k < 3 && walkers.Count < want; k++) Spawn();
            if (walkers.Count > want + 1) Despawn(walkers.Count - 1);
        }

        foreach (Walker w in walkers)
        {
            if (w.agent == null || !w.agent.isOnNavMesh) continue;
            if (w.hasSpeed) w.animator.SetFloat("Speed", w.agent.velocity.magnitude);
            if (w.agent.pathPending || w.agent.remainingDistance > 0.6f) continue;
            if (w.waitUntil == 0f) { w.waitUntil = Time.time + Random.Range(1.5f, 6f); continue; }
            if (Time.time < w.waitUntil) continue;
            w.waitUntil = 0f;
            w.agent.SetDestination(NextTarget(w.agent.transform.position));
        }
    }

    private static float Density()
    {
        float d = WeatherSystem.CurrentWeather switch
        {
            WeatherSystem.Weather.Rain => 0.45f,
            WeatherSystem.Weather.Storm => 0.15f,
            WeatherSystem.Weather.Fog => 0.7f,
            _ => 1f,
        };
        float hour = GameManager.Instance != null ? GameManager.Instance.ClockHours : 12f;
        if (hour >= 19f || hour < 6f) d *= 0.5f;
        return d;
    }

    private Vector3 NextTarget(Vector3 from)
    {
        // Ưu tiên chỗ không quá xa (đi bộ 20-120m) và đi tới được chỉ bằng vỉa hè
        Vector3 fallback = from;
        for (int i = 0; i < 12; i++)
        {
            Vector3 t = targets[Random.Range(0, targets.Count)];
            if (!NavMesh.CalculatePath(from, t, walkMask, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float d = Vector3.Distance(t, from);
            if (d > 20f && d < 120f) return t;
            fallback = t;
        }
        return fallback;
    }

    private void Spawn()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null || spawner.customerPrefabs == null || spawner.customerPrefabs.Length == 0) return;
        GameObject prefab = spawner.RandomCustomerPrefab(); // bỏ model dành riêng cho nhân viên lau nhà
        if (prefab == null) return;
        Vector3 at = NearPlayer();

        // Tạo dưới gốc đang tắt để gỡ script mua sắm trước khi nó chạy
        var holder = new GameObject("_WalkerHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), holder.transform);
        MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--) if (scripts[i] != null) DestroyImmediate(scripts[i]);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        go.AddComponent<PedestrianWalker>();
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent == null) agent = go.AddComponent<NavMeshAgent>();
        go.transform.SetParent(transform, true);
        Destroy(holder);
        go.name = "Pedestrian";
        go.SetActive(true);

        agent.enabled = true;
        agent.areaMask = walkMask;
        agent.speed = Random.Range(walkSpeed.x, walkSpeed.y);
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.3f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        if (NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, walkMask)) agent.Warp(hit.position);

        var w = new Walker { agent = agent, animator = go.GetComponentInChildren<Animator>() };
        if (w.animator != null) foreach (AnimatorControllerParameter p in w.animator.parameters) if (p.name == "Speed") w.hasSpeed = true;
        if (agent.isOnNavMesh) agent.SetDestination(NextTarget(at));
        // Phố Việt: ~30% người đi bộ là hàng rong (gánh hàng / dắt xe đạp bán hoa), đi chậm hơn
        if (vendorChance > 0f && Random.value < vendorChance)
        {
            StreetVendor.Dress(go, Random.value < 0.35f, Random.Range(0, 100000));
            agent.speed = Random.Range(0.75f, 1.05f);
            go.name = "Hang rong";
        }
        walkers.Add(w);
    }

    /// <summary>Thành phố rộng: người đi bộ xuất hiện quanh người chơi (35 - 110 m, ngoài tầm mắt gần) cho phố luôn đông.</summary>
    private Vector3 NearPlayer()
    {
        Camera cam = Camera.main;
        Vector3 any = targets[Random.Range(0, targets.Count)];
        if (cam == null) return any;
        Vector3 c = cam.transform.position;
        for (int i = 0; i < 40; i++)
        {
            Vector3 t = targets[Random.Range(0, targets.Count)];
            float d = Vector3.Distance(new Vector3(t.x, 0f, t.z), new Vector3(c.x, 0f, c.z));
            if (d > 35f && d < 110f) return t;
        }
        return any;
    }

    /// <summary>Người đi bộ đã ở quá xa người chơi (> 170 m) thì bỏ để sinh lại gần hơn.</summary>
    private void RecycleFar()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        int removed = 0;
        for (int i = walkers.Count - 1; i >= 0 && removed < 3; i--)
        {
            Walker w = walkers[i];
            if (w.agent != null && (w.agent.transform.position - c).sqrMagnitude < 170f * 170f) continue;
            Despawn(i);
            removed++;
        }
    }

    private void Despawn(int index)
    {
        Walker w = walkers[index];
        walkers.RemoveAt(index);
        if (w.agent != null) Destroy(w.agent.gameObject);
    }
}
