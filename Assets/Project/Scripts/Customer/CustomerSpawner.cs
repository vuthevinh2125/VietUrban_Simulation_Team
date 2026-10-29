using UnityEngine;
using UnityEngine.AI;

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

    private float nextSpawnTime;
    private int spawnedCount;

    void Start() => nextSpawnTime = Time.time + 3f;

    void Update()
    {
        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);

        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGameOver || gm.DayProgress01 > stopSpawningAt)) return;
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
        Vector3 pos = NavMesh.SamplePosition(from.position, out NavMeshHit hit, 5f, NavMesh.AllAreas) ? hit.position : from.position;

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
