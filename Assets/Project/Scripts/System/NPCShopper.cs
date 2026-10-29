using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Khách hàng: vào cửa hàng -> đi tới các kệ còn hàng, lấy hàng THẬT (bỏ qua món quá đắt)
/// -> xếp hàng ở quầy thu ngân -> chờ người chơi quét -> trả tiền -> ra về.
/// Không bao giờ kẹt vĩnh viễn: đi không tới thì đi hết mức có thể, quá lâu thì dịch chuyển.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCShopper : MonoBehaviour
{
    /// <summary>Tất cả khách đang có trong scene (AutoDoor, CustomerSpawner dùng).</summary>
    public static readonly List<NPCShopper> Active = new List<NPCShopper>();

    public enum NPCState { Entering, Shopping, GoToCheckout, Queueing, Paying, Leaving, Done }

    [Header("Trạng thái hiện tại")]
    public NPCState currentState;

    [Header("Điểm di chuyển")]
    [Tooltip("Không còn dùng (khách tự tìm kệ còn hàng). Giữ lại để không mất dữ liệu cũ.")]
    public Transform[] shelves;
    [Tooltip("Không còn dùng (khách tự tìm CheckoutCounter).")]
    public Transform checkoutCounter;
    [Tooltip("Chỗ khách đi tới khi ra về")]
    public Transform exitDoor;
    [Tooltip("Không còn dùng.")]
    public GameObject checkoutItemsGroup;
    [Tooltip("(Tuỳ chọn) điểm ngay trong cửa, khách đi qua đây trước khi mua sắm")]
    public Transform entrancePoint;

    [Header("Hành vi")]
    public int minItemsToBuy = 1;
    public int maxItemsToBuy = 4;
    [Tooltip("Khách bỏ qua món nếu giá bán > giá thị trường x hệ số này")]
    public float maxPriceRatio = 1.5f;
    [Tooltip("Thời gian đứng xem mỗi kệ (giây)")]
    public float browseTime = 1.5f;
    [Tooltip("Đi quá thời gian này mà chưa tới nơi thì dịch chuyển tới luôn (chống kẹt)")]
    public float moveTimeout = 25f;

    [Header("Debug")]
    [Tooltip("Ghi log mỗi khi khách đổi trạng thái / đi tới đâu")]
    public bool debugLogs = true;

    public IReadOnlyList<ItemData> Basket => basket;

    private NavMeshAgent agent;
    private Animator animator;
    private bool hasSpeedParam;
    private readonly List<ItemData> basket = new List<ItemData>();

    private CheckoutCounter counter;
    private Vector3 queueTarget;
    private bool queueTargetDirty;
    private bool isFirstInQueue;
    private bool paid;

    private static int stuckWarnings;

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        hasSpeedParam = animator != null && HasParameter(animator, "Speed");

        // Rigidbody vật lý đánh nhau với NavMeshAgent -> để kinematic
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }

    void Start() => StartCoroutine(Run());

    void Update()
    {
        if (hasSpeedParam) animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    void OnDestroy()
    {
        if (counter != null) counter.LeaveQueue(this);
    }

    // ================= Kịch bản chính =================
    private IEnumerator Run()
    {
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else { Debug.LogWarning($"[Customer] {name} không đứng trên NavMesh, huỷ.", this); Destroy(gameObject); yield break; }
        }

        if (entrancePoint != null)
        {
            SetState(NPCState.Entering);
            yield return MoveTo(entrancePoint.position);
        }

        // --- Mua sắm ---
        SetState(NPCState.Shopping);
        int wanted = Random.Range(minItemsToBuy, maxItemsToBuy + 1);
        var visited = new HashSet<Shelf>();
        int attempts = 0;

        while (basket.Count < wanted && attempts < wanted * 3)
        {
            attempts++;
            Shelf shelf = PickShelf(visited);
            if (shelf == null) break;
            visited.Add(shelf);

            yield return MoveTo(ShelfStandPoint(shelf));
            Face(shelf.transform.position);
            yield return new WaitForSeconds(browseTime * Random.Range(0.7f, 1.4f));

            if (shelf.IsEmpty) continue;
            ItemData item = shelf.CurrentItem;
            if (item != null && PriceManager.GetSellPrice(item) > item.MarketPrice * maxPriceRatio)
            {
                Debug.Log($"[Customer] {name}: {item.itemName} đắt quá ({EconomyManager.Format(PriceManager.GetSellPrice(item))}), bỏ qua.");
                continue;
            }

            int take = Random.Range(1, 3);
            for (int i = 0; i < take && basket.Count < wanted; i++)
                if (shelf.TryTakeForCustomer(out ItemData got) && got != null) basket.Add(got);
        }

        // --- Thanh toán ---
        if (basket.Count > 0) yield return CheckoutRoutine();

        // --- Ra về ---
        SetState(NPCState.Leaving);
        if (exitDoor != null) yield return MoveTo(exitDoor.position);
        SetState(NPCState.Done);
        Destroy(gameObject);
    }

    private IEnumerator CheckoutRoutine()
    {
        SetState(NPCState.GoToCheckout);
        counter = CheckoutCounter.FindBest(transform.position);
        if (counter == null)
        {
            Debug.LogWarning("[Customer] Không có quầy thu ngân (CheckoutCounter) nào trong scene. Khách bỏ hàng ra về.");
            basket.Clear();
            yield break;
        }

        float waited = 0f;
        while (!counter.JoinQueue(this))
        {
            waited += 1f;
            if (waited > 20f) { basket.Clear(); counter = null; yield break; } // hàng quá dài, bỏ về
            yield return new WaitForSeconds(1f);
        }

        SetState(NPCState.Queueing);
        float firstSince = -1f;

        while (!paid)
        {
            if (queueTargetDirty)
            {
                queueTargetDirty = false;
                if (agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(Sample(queueTarget)); }
            }

            if (isFirstInQueue && counter.Current == null)
            {
                if (firstSince < 0f) firstSince = Time.time;
                bool arrived = Arrived(0.6f) || (Time.time - firstSince > 4f && IsStill());
                if (!arrived && Time.time - firstSince > moveTimeout)
                {
                    WarnStuck("quầy thu ngân");
                    agent.Warp(Sample(queueTarget));
                    arrived = true;
                }
                if (arrived)
                {
                    SetState(NPCState.Paying);
                    Face(counter.itemDropPoint != null ? counter.itemDropPoint.position : counter.transform.position);
                    counter.StartCheckout(this, basket);
                }
            }
            else if (!isFirstInQueue) firstSince = -1f;

            yield return null;
        }
    }

    // ================= Gọi từ CheckoutCounter =================
    public void SetQueueTarget(Vector3 position, bool first)
    {
        queueTarget = position;
        isFirstInQueue = first;
        queueTargetDirty = true;
    }

    public void OnPaid()
    {
        paid = true;
        basket.Clear();
        counter = null;
    }

    private void SetState(NPCState state)
    {
        if (debugLogs && currentState != state)
            Debug.Log($"[Customer] {name}: {currentState} -> {state} at {transform.position:F1} (basket {basket.Count})", this);
        currentState = state;
    }

    // ================= Di chuyển =================
    private IEnumerator MoveTo(Vector3 target)
    {
        if (!agent.isOnNavMesh) yield break;

        Vector3 dest = Sample(target);
        if (debugLogs)
        {
            var test = new NavMeshPath();
            agent.CalculatePath(dest, test);
            Debug.Log($"[Customer] {name} -> {dest:F1} path={test.status} corners={test.corners.Length}", this);
        }
        agent.isStopped = false;
        agent.SetDestination(dest);

        float elapsed = 0f, still = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance + 0.35f) break;

                still = IsStill() ? still + Time.deltaTime : 0f;
                if (still > 2f && agent.pathStatus != NavMeshPathStatus.PathComplete) break; // đã đi hết mức có thể
                if (still > 4f) break;
            }
            if (elapsed > moveTimeout)
            {
                WarnStuck(target.ToString("F1"));
                agent.Warp(dest);
                break;
            }
            yield return null;
        }
    }

    private bool Arrived(float tolerance) =>
        agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + tolerance;

    // Khi đang đi qua NavMeshLink (cầu nối ở cửa) Unity báo velocity = 0, không tính là đứng yên
    private bool IsStill() => !agent.isOnOffMeshLink && agent.velocity.sqrMagnitude < 0.01f;

    private static Vector3 Sample(Vector3 p) =>
        NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

    /// <summary>Điểm đi bộ được gần các ô hàng của kệ nhất.</summary>
    private static Vector3 ShelfStandPoint(Shelf shelf)
    {
        Vector3 center = Vector3.zero;
        int n = 0;
        if (shelf.slots != null)
            foreach (Transform s in shelf.slots)
                if (s != null) { center += s.position; n++; }
        center = n > 0 ? center / n : shelf.transform.position;
        center.y = shelf.transform.position.y;
        return Sample(center);
    }

    private static Shelf PickShelf(HashSet<Shelf> visited)
    {
        var candidates = new List<Shelf>();
        foreach (Shelf s in StockManager.Shelves)
            if (s != null && !s.IsEmpty && !visited.Contains(s)) candidates.Add(s);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    private void Face(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    private void WarnStuck(string where)
    {
        if (stuckWarnings++ < 5)
            Debug.LogWarning($"[Customer] {name} không đi tới được {where}, dịch chuyển tới luôn. Nếu hay gặp: kiểm tra NavMesh (Bake lại) ở khu đó.", this);
    }

    private static bool HasParameter(Animator a, string paramName)
    {
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active.Clear();
        stuckWarnings = 0;
    }
}
