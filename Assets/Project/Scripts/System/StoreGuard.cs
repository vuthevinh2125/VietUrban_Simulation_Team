using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bảo vệ thuê qua tab Staff: đứng gác ngoài cửa ra vào. Thấy kẻ trộm đang chạy thì đuổi theo, tới sát thì tóm
/// (lấy lại tiền hàng như khi người chơi đánh trúng bằng gậy), xong quay về chỗ gác.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class StoreGuard : MonoBehaviour
{
    public float detectRadius = 28f;
    public float catchDistance = 1.4f;
    public float chaseSpeed = 4.2f;
    public float walkSpeed = 1.8f;

    private NavMeshAgent agent;
    private Animator animator;
    private bool hasSpeed;
    private Vector3 post;
    private Quaternion postRot;
    private NPCShopper target;
    private float nextScan, nextRepath;

    public void SetPost(Vector3 position, Quaternion rotation)
    {
        post = position;
        postRot = rotation;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = true;
        agent.areaMask = NavMesh.AllAreas;
        agent.stoppingDistance = 0.3f;
        agent.angularSpeed = 540f;
        agent.acceleration = 14f;
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == "Speed") hasSpeed = true;
        post = transform.position;
        postRot = transform.rotation;
    }

    void Update()
    {
        if (hasSpeed) animator.SetFloat("Speed", agent.enabled ? agent.velocity.magnitude : 0f);
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (target != null && (!target.IsFleeingThief)) target = null; // đã bị bắt / đã thoát

        if (target == null && Time.time >= nextScan)
        {
            nextScan = Time.time + 0.3f;
            float best = detectRadius;
            foreach (NPCShopper n in NPCShopper.Active)
            {
                if (n == null || !n.IsFleeingThief) continue;
                float d = Vector3.Distance(n.transform.position, transform.position);
                if (d < best) { best = d; target = n; }
            }
            if (target != null)
            {
                SpeechBubble.Show(transform, "Stop right there!", new Color(0.4f, 0.75f, 1f), agent.height + 0.4f, 3f);
                agent.speed = chaseSpeed;
            }
        }

        if (target != null)
        {
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.2f;
                agent.SetDestination(target.transform.position);
            }
            Vector3 d = target.transform.position - transform.position; d.y = 0f;
            if (d.magnitude <= catchDistance)
            {
                target.Catch();
                SpeechBubble.Show(transform, "Gotcha!", new Color(0.4f, 1f, 0.5f), agent.height + 0.4f, 2.5f);
                target = null;
                agent.speed = walkSpeed;
                agent.SetDestination(post);
            }
            return;
        }

        // Về chỗ gác và đứng quay mặt ra đường
        Vector3 toPost = post - transform.position; toPost.y = 0f;
        if (toPost.magnitude > 0.6f)
        {
            agent.speed = walkSpeed;
            if (!agent.hasPath || Vector3.Distance(agent.destination, post) > 0.5f) agent.SetDestination(post);
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, postRot, 240f * Time.deltaTime);
        }
    }
}
