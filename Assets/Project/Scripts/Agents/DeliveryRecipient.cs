using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Người nhận hàng đứng chờ ở điểm giao (thay cho chỉ có cột sáng đánh dấu).
/// Sinh ra khi người chơi lấy gói hàng ở cửa hàng; dùng một model khách (bỏ script mua sắm),
/// quay mặt về phía người chơi khi tới gần, gọi "Over here!", nhận hàng bằng [E] rồi cảm ơn và đi vào nhà.
/// </summary>
public class DeliveryRecipient : MonoBehaviour
{
    public const float HandOverRange = 3.5f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly string[] Names = { "Mr. Minh", "Ms. Lan", "Mrs. Hoa", "Mr. Tuan", "Ms. Thu", "Mr. Nam", "Ms. Mai", "Mr. Hung" };

    public string DisplayName { get; private set; }
    public bool Leaving { get; private set; }

    private Animator animator;
    private bool hasSpeed, called;
    private NavMeshAgent agent;
    private Vector3 homeDir;
    private float destroyAt;

    /// <summary>Tạo người nhận tại điểm giao. homeDir: hướng đi vào nhà sau khi nhận (có thể bằng 0).</summary>
    public static DeliveryRecipient Spawn(Vector3 at, Vector3 homeDir)
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        GameObject prefab = spawner != null ? spawner.RandomCustomerPrefab() : null;
        if (prefab == null) return null;

        Vector3 pos = NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : at;
        // Tạo dưới gốc đang tắt để gỡ script mua sắm trước khi nó kịp chạy (giống StaffManager.SpawnFromPrefab)
        var holder = new GameObject("_RecipientHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(prefab, pos, Quaternion.identity, holder.transform);
        foreach (NPCShopper s in go.GetComponentsInChildren<NPCShopper>(true)) DestroyImmediate(s);
        go.transform.SetParent(null, true);
        Destroy(holder);
        go.name = "DeliveryRecipient";

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        DeliveryRecipient r = go.AddComponent<DeliveryRecipient>();
        r.DisplayName = Names[Random.Range(0, Names.Length)];
        r.homeDir = new Vector3(homeDir.x, 0f, homeDir.z).normalized;
        return r;
    }

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == "Speed") hasSpeed = true;
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) { agent.enabled = true; agent.avoidancePriority = 60; }
    }

    void Update()
    {
        if (Leaving)
        {
            if (hasSpeed) animator.SetFloat(SpeedHash, agent != null && agent.enabled ? agent.velocity.magnitude : 0f);
            if (Time.time >= destroyAt) Destroy(gameObject);
            return;
        }
        if (hasSpeed) animator.SetFloat(SpeedHash, 0f);

        Transform player = DeliveryManager.PlayerForRecipient();
        if (player == null) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        float dist = d.magnitude;
        if (dist < 30f && d.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 4f);
        if (!called && dist < 20f)
        {
            called = true;
            SpeechBubble.Show(transform, "Over here!", new Color(1f, 0.85f, 0.3f));
        }
    }

    /// <summary>Nhận hàng: cảm ơn rồi đi vào nhà (hoặc đứng tại chỗ) và biến mất sau vài giây.</summary>
    public void Receive(bool onTime)
    {
        if (Leaving) return;
        Leaving = true;
        SpeechBubble.Show(transform, onTime ? "Thank you!" : "Finally... thanks.", onTime ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.7f, 0.5f));
        destroyAt = Time.time + 4f;
        if (agent != null && agent.isOnNavMesh && homeDir.sqrMagnitude > 0.01f)
        {
            agent.isStopped = false;
            agent.SetDestination(transform.position + homeDir * 4f);
        }
    }
}
