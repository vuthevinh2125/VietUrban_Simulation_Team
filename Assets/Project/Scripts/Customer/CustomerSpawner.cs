using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Sinh khách hàng định kỳ trong giờ mở cửa. Chỉ sinh khi cửa hàng có hàng trên kệ.
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    [Header("Customers")]
    public GameObject[] customerPrefabs;
    [Tooltip("Tốc độ đi bộ của khách")]
    public float walkSpeed = 1.6f;

    [Header("Points")]
    public Transform spawnPoint;
    [Tooltip("(Tuỳ chọn) điểm ngay trong cửa")]
    public Transform entrancePoint;
    public Transform exitPoint;

    [Header("Rules")]
    public int maxCustomers = 4;
    [Tooltip("Khoảng thời gian giữa 2 khách (giây, ngẫu nhiên trong khoảng)")]
    public Vector2 spawnInterval = new Vector2(8f, 18f);
    [Tooltip("Ngừng đón khách khi ngày đã trôi qua quá tỉ lệ này (0.9 = 90% ngày)")]
    [Range(0f, 1f)] public float stopSpawningAt = 0.9f;
    [Tooltip("Chỉ sinh khách khi có ít nhất một kệ còn hàng")]
    public bool onlyWhenStocked = true;
    [Tooltip("Khách xuất hiện rải ngẫu nhiên trong bán kính này quanh điểm sinh (mét)")]
    public float spawnScatter = 1.2f;
    [Tooltip("Điểm sinh/ra về phải cách đầu cầu nối NavMeshLink ở cửa ít nhất khoảng này (mét)")]
    public float minDistanceFromLinks = 2f;

    private float nextSpawnTime;
    private int spawnedCount;

    void Start()
    {
        nextSpawnTime = Time.time + 3f;
        KeepAwayFromLinks(spawnPoint);
        if (exitPoint != spawnPoint) KeepAwayFromLinks(exitPoint);
    }

    /// <summary>Đứng ngay đầu cầu nối thì agent không tính được đường -> lùi điểm ra xa cửa.</summary>
    private void KeepAwayFromLinks(Transform point)
    {
        if (point == null) return;
        foreach (NavMeshLink link in FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None))
        {
            Vector3 a = link.transform.TransformPoint(link.startPoint);
            Vector3 b = link.transform.TransformPoint(link.endPoint);
            Vector3 nearEnd = (point.position - a).sqrMagnitude < (point.position - b).sqrMagnitude ? a : b;
            Vector3 farEnd = nearEnd == a ? b : a;
            Vector3 d = point.position - nearEnd; d.y = 0f;
            if (d.magnitude >= minDistanceFromLinks) continue;

            Vector3 away = nearEnd - farEnd; away.y = 0f; // hướng từ trong ra ngoài
            Vector3 target = nearEnd + away.normalized * (minDistanceFromLinks + 0.5f);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) target = hit.position;
            Debug.Log($"[CustomerSpawner] '{point.name}' quá sát cửa, dời tới {target:F1}.", this);
            point.position = target;
        }
    }

    void Update()
    {
        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);

        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGameOver || !gm.IsStoreOpen || gm.DayProgress01 > stopSpawningAt)) return; // chỉ đón khách khi đang mở cửa
        if (NPCShopper.Active.Count >= maxCustomers) return;
        if (onlyWhenStocked && StockManager.GetRandomStockedShelf() == null) return;

        Spawn();
    }

    /// <summary>Sinh ngay một khách (gọi được từ nút test).</summary>
    [ContextMenu("Spawn Customer Now")]
    public NPCShopper Spawn()
    {
        if (customerPrefabs == null || customerPrefabs.Length == 0)
        {
            Debug.LogWarning("[CustomerSpawner] Chưa gán Customer Prefabs.", this);
            return null;
        }

        GameObject prefab = customerPrefabs[Random.Range(0, customerPrefabs.Length)];
        if (prefab == null) return null;

        Transform from = spawnPoint != null ? spawnPoint : transform;
        Vector2 r = Random.insideUnitCircle * spawnScatter;
        Vector3 wanted = from.position + new Vector3(r.x, 0f, r.y);
        Vector3 pos = NavMesh.SamplePosition(wanted, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position
                    : NavMesh.SamplePosition(from.position, out hit, 5f, NavMesh.AllAreas) ? hit.position : from.position;

        GameObject go = Instantiate(prefab, pos, from.rotation);
        go.name = $"Customer_{++spawnedCount:00}";

        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = walkSpeed;

        NPCShopper customer = go.GetComponent<NPCShopper>();
        if (customer == null) customer = go.AddComponent<NPCShopper>();
        customer.exitDoor = exitPoint != null ? exitPoint : from;
        customer.entrancePoint = entrancePoint;
        return customer;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (spawnPoint != null) Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
        Gizmos.color = Color.magenta;
        if (exitPoint != null) Gizmos.DrawWireSphere(exitPoint.position, 0.4f);
        Gizmos.color = Color.white;
        if (entrancePoint != null) Gizmos.DrawWireSphere(entrancePoint.position, 0.3f);
    }
}
