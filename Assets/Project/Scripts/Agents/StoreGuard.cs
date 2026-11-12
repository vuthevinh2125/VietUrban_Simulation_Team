using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bảo vệ thuê qua tab Staff: đứng gác ngoài cửa ra vào. Chỉ đuổi kẻ trộm khi nó đã ra khỏi cửa hàng (còi chống trộm
/// đã kêu - không lao vào trong bắt), chạy theo, tới sát thì tóm (lấy lại tiền hàng như khi người chơi đánh trúng
/// bằng gậy), xong đi bộ về chỗ gác. Lúc đuổi thì rút gậy (cùng model gậy của người chơi) cầm tay phải, tới sát thì
/// giơ gậy vụt (xoay cánh tay phải sau animation) rồi mới tóm. Animation: đi bộ / chạy theo Speed + Run (Tools > Store Setup > Update Character Animations).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class StoreGuard : MonoBehaviour
{
    /// <summary>Các bảo vệ đang có trong scene (minimap đọc danh sách này thay vì tìm mỗi frame).</summary>
    public static readonly System.Collections.Generic.List<StoreGuard> All = new System.Collections.Generic.List<StoreGuard>();

    public float detectRadius = 28f;
    public float catchDistance = 1.4f;
    public float chaseSpeed = 4.2f;
    public float walkSpeed = 1.8f;

    private NavMeshAgent agent;
    private Animator animator;
    private bool hasSpeed, hasRun;
    private Vector3 post;
    private Quaternion postRot;
    private NPCShopper target;
    private float nextScan, nextRepath;

    // Gậy: hiện khi đuổi trộm và vài giây sau khi bắt xong
    private const float SwingTime = 0.45f;
    private Transform bat, hand, upperArm;
    private float swingT = -1f, batUntil;
    private bool swingHit;

    public void SetPost(Vector3 position, Quaternion rotation)
    {
        post = position;
        postRot = rotation;
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

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
            {
                if (p.name == "Speed") hasSpeed = true;
                if (p.name == "Run") hasRun = true;
            }
        post = transform.position;
        postRot = transform.rotation;

        if (animator != null && animator.isHuman)
        {
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }
        bat = PlayerTools.BuildBat(transform);
        bat.gameObject.SetActive(false);
    }

    void Update()
    {
        if (hasSpeed) animator.SetFloat("Speed", agent.enabled ? agent.velocity.magnitude : 0f);
        if (hasRun) animator.SetBool("Run", target != null);
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (swingT >= 0f) { UpdateSwing(); return; }
        if (target != null && (!target.IsFleeingThief)) target = null; // đã bị bắt / đã thoát
        if (bat.gameObject.activeSelf != (target != null || Time.time < batUntil)) bat.gameObject.SetActive(target != null || Time.time < batUntil);

        if (target == null && Time.time >= nextScan)
        {
            nextScan = Time.time + 0.3f;
            float best = detectRadius;
            foreach (NPCShopper n in NPCShopper.Active)
            {
                if (n == null || !n.HasLeftStore) continue; // còn trong cửa hàng thì chưa đuổi
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
                // Tới sát: đứng lại, quay về phía kẻ trộm, vụt gậy (trúng ở giữa cú vụt)
                agent.isStopped = true;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
                swingT = 0f;
                swingHit = false;
                AudioManager.PlayAt("bat_swing", transform.position, 0.8f);
            }
            return;
        }

        // Về chỗ gác (đi bộ) và đứng quay mặt ra đường
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

    private void UpdateSwing()
    {
        swingT += Time.deltaTime;
        if (!swingHit && swingT >= SwingTime * 0.55f)
        {
            swingHit = true;
            if (target != null && target.IsFleeingThief)
            {
                target.Catch();
                AudioManager.PlayAt("bat_hit", transform.position, 0.9f);
                SpeechBubble.Show(transform, "Gotcha!", new Color(0.4f, 1f, 0.5f), agent.height + 0.4f, 2.5f);
            }
        }
        if (swingT < SwingTime) return;
        swingT = -1f;
        target = null;
        batUntil = Time.time + 3f;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.SetDestination(post);
    }

    // Sau animation: vụt gậy bằng cách xoay cánh tay phải (giơ ra sau rồi quật xuống), gậy bám theo bàn tay
    void LateUpdate()
    {
        if (bat == null || !bat.gameObject.activeSelf) return;
        float angle = 0f;
        if (swingT >= 0f)
        {
            float t = Mathf.Clamp01(swingT / SwingTime);
            angle = t < 0.35f ? Mathf.Lerp(0f, -110f, t / 0.35f) : Mathf.Lerp(-110f, 55f, (t - 0.35f) / 0.65f);
            if (upperArm != null) upperArm.rotation = Quaternion.AngleAxis(angle, transform.right) * upperArm.rotation;
        }
        Vector3 dir = Quaternion.AngleAxis(angle * 0.8f, transform.right) * (transform.up * 0.9f + transform.forward * 0.45f).normalized;
        Vector3 grip = hand != null ? hand.position : transform.position + transform.up * 1.0f + transform.right * 0.35f;
        bat.SetPositionAndRotation(grip + dir * 0.13f, Quaternion.LookRotation(Vector3.Cross(transform.right, dir), dir));
    }
}
