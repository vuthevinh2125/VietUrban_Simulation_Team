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

    [Tooltip("Khách chỉ lấy hàng khi đứng cách điểm đứng của kệ trong khoảng này (mét)")]
    public float reachDistance = 2.5f;

    [Header("Debug")]
    [Tooltip("Ghi log mỗi khi khách đổi trạng thái / đi tới đâu")]
    public bool debugLogs = true; // chỉ in khi GameDebug.Verbose = true

    [Header("Queue")]
    [Tooltip("Tới gần chỗ xếp hàng trong khoảng này thì dừng hẳn, đứng yên chờ")]
    public float queueArriveRadius = 0.3f;

    public IReadOnlyList<ItemData> Basket => basket;
    /// <summary>Bán kính chiếm chỗ của khách (để quầy giãn hàng chờ cho đủ chỗ đứng).</summary>
    public float Radius => agent != null ? agent.radius : 0.4f;

    private NavMeshAgent agent;
    private Animator animator;
    private ShoppingCarrier carrier;
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

        // Prefab NPC dạo phố chỉ được đi vùng "Sidewalks" -> mở thêm vùng sàn trong cửa hàng ("Walkable")
        agent.areaMask |= AreaBit("Walkable") | AreaBit("Sidewalks") | AreaBit("Jump");
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
        carrier = GetComponent<ShoppingCarrier>();
        if (carrier == null) carrier = gameObject.AddComponent<ShoppingCarrier>();
        carrier.Init(wanted); // xách giỏ hoặc đẩy xe
        var visited = new HashSet<Shelf>();
        int attempts = 0;

        while (basket.Count < wanted && attempts < wanted * 3)
        {
            attempts++;
            Shelf shelf = PickShelf(visited);
            if (shelf == null) break;
            visited.Add(shelf);

            Vector3 stand = ShelfStandPoint(shelf);
            yield return MoveTo(stand);
            if (FlatDistance(transform.position, stand) > reachDistance)
            {
                if (debugLogs && GameDebug.Verbose) Debug.Log($"[Customer] {name} không tới được kệ {shelf.name}, bỏ qua.", this);
                continue;
            }
            Face(shelf.transform.position);
            yield return new WaitForSeconds(browseTime * Random.Range(0.7f, 1.4f));

            if (shelf.IsEmpty) continue;
            ItemData item = shelf.CurrentItem;
            int take = Random.Range(1, 3);
            if (item != null)
            {
                // So giá trên nhãn với giá thị trường: đắt thì có thể bỏ qua, rẻ thì lấy thêm
                float sell = PriceManager.GetSellPrice(item);
                float ratio = PriceManager.PriceRatio(item, sell);
                if (Random.value > PriceManager.BuyChance(item, sell, maxPriceRatio))
                {
                    SpeechBubble.Show(transform, "Too expensive!", new Color(1f, 0.35f, 0.3f), agent.height + 0.35f);
                    if (debugLogs && GameDebug.Verbose) Debug.Log($"[Customer] {name}: {item.itemName} đắt quá ({EconomyManager.Format(sell)}, thị trường {EconomyManager.Format(item.MarketPrice)}), bỏ qua.", this);
                    continue;
                }
                if (ratio <= PriceManager.CheapRatio)
                {
                    take++;
                    wanted++;
                    SpeechBubble.Show(transform, "Great price!", new Color(0.4f, 1f, 0.45f), agent.height + 0.35f);
                }
                else if (ratio > 1.15f)
                    SpeechBubble.Show(transform, "Pricey...", new Color(1f, 0.8f, 0.3f), agent.height + 0.35f);
            }
            for (int i = 0; i < take && basket.Count < wanted; i++)
                if (shelf.TryTakeForCustomer(out ItemData got) && got != null)
                {
                    basket.Add(got);
                    carrier.AddItem(got);
                }
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
        bool standing = false;
        int movePriority = agent.avoidancePriority;

        while (!paid)
        {
            Vector3 spot = Sample(queueTarget);
            float dist = FlatDistance(transform.position, spot);

            if (queueTargetDirty)
            {
                // Người phía trước đã đi -> bước lên chỗ mới
                queueTargetDirty = false;
                standing = false;
            }

            if (!standing)
            {
                if (dist <= queueArriveRadius)
                {
                    // Tới chỗ: dừng hẳn, đứng yên, quay mặt về phía quầy.
                    // Ưu tiên tránh cao nhất (0) để người khác đi vòng qua chứ không đẩy mình.
                    StandStill();
                    agent.avoidancePriority = 0;
                    standing = true;
                    if (counter.customerPoint != null)
                        Face(isFirstInQueue && counter.itemDropPoint != null ? counter.itemDropPoint.position : counter.customerPoint.position);
                }
                else if (agent.isOnNavMesh && !agent.isOnOffMeshLink)
                {
                    agent.avoidancePriority = movePriority;
                    agent.stoppingDistance = 0f;
                    if (agent.isStopped || !agent.hasPath || FlatDistance(agent.destination, spot) > 0.05f)
                    {
                        agent.isStopped = false;
                        agent.SetDestination(spot);
                    }
                }
            }
            else if (dist > 0.8f) standing = false; // bị đẩy lệch khỏi chỗ -> đi lại về chỗ

            if (isFirstInQueue && counter.Current == null)
            {
                if (firstSince < 0f) firstSince = Time.time;
                bool arrived = standing || dist <= 0.6f;
                if (!arrived && Time.time - firstSince > moveTimeout)
                {
                    WarnStuck("quầy thu ngân");
                    agent.Warp(Sample(queueTarget));
                    arrived = true;
                }
                if (arrived)
                {
                    StandStill();
                    standing = true;
                    SetState(NPCState.Paying);
                    Face(counter.itemDropPoint != null ? counter.itemDropPoint.position : counter.transform.position);
                    counter.StartCheckout(this, basket);
                    if (carrier != null) carrier.ClearItems(); // bày hàng từ giỏ ra quầy
                }
            }
            else if (!isFirstInQueue) firstSince = -1f;

            yield return null;
        }
        agent.avoidancePriority = movePriority;
    }

    private void StandStill()
    {
        if (!agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
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
        if (debugLogs && GameDebug.Verbose && currentState != state)
            Debug.Log($"[Customer] {name}: {currentState} -> {state} at {transform.position:F1} (basket {basket.Count})", this);
        currentState = state;
    }

    // ================= Di chuyển =================
    private IEnumerator MoveTo(Vector3 target)
    {
        if (!agent.isOnNavMesh) yield break;

        Vector3 dest = Sample(target);

        // Đang đi trên cầu nối (NavMeshLink) thì chờ đi hết đã
        float linkWait = 0f;
        while (agent.isOnOffMeshLink && linkWait < 5f) { linkWait += Time.deltaTime; yield return null; }

        // Đường đi chưa tính được (vừa sinh ra, đứng sát cầu nối...) -> thử lại vài lần
        var test = new NavMeshPath();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (agent.CalculatePath(dest, test) && test.status != NavMeshPathStatus.PathInvalid) break;
            if (attempt == 4 && NavMesh.SamplePosition(transform.position, out NavMeshHit near, 2f, NavMesh.AllAreas))
                agent.Warp(near.position + (near.position - dest).normalized * 0.1f);
            yield return new WaitForSeconds(0.1f);
        }
        if (debugLogs && GameDebug.Verbose) Debug.Log($"[Customer] {name} -> {dest:F1} path={test.status} corners={test.corners.Length}", this);
        if (test.status == NavMeshPathStatus.PathInvalid)
        {
            WarnStuck(target.ToString("F1") + " (không tính được đường - kiểm tra NavMesh)");
            yield break; // KHÔNG dịch chuyển: nơi gọi tự kiểm tra khoảng cách và bỏ qua
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

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

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
            Debug.LogWarning($"[Customer] {name} không đi tới được {where}. Nếu hay gặp: kiểm tra NavMesh ở khu đó.", this);
    }

    private static int AreaBit(string areaName)
    {
        int area = NavMesh.GetAreaFromName(areaName);
        return area >= 0 ? 1 << area : 0;
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
