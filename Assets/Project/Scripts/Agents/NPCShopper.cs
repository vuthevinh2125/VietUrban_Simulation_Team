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

    public enum NPCState { Entering, Shopping, GoToCheckout, Queueing, Paying, Leaving, Fleeing, Done }

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

    [Header("Kẻ trộm (giống Supermarket Simulator)")]
    [Tooltip("Tỉ lệ khách lấy hàng rồi bỏ chạy không trả tiền (từ ngày 2 trở đi)")]
    [Range(0f, 1f)] public float thiefChance = 0.07f;
    [Tooltip("Đi tay không (không lấy giỏ / xe đẩy) - vd. khách du lịch chỉ mua vài món lưu niệm")]
    public bool handsFree;
    [Tooltip("Kẻ trộm chạy nhanh gấp bao nhiêu lần")]
    public float fleeSpeedMultiplier = 1.05f;
    [Tooltip("Tốc độ chạy trốn tối thiểu (m/s) sau khi ra khỏi cửa hàng và còi chống trộm kêu")]
    public float runSpeed = 3.6f;
    [Tooltip("Ra khỏi cửa rồi kẻ trộm còn chạy thêm bao xa (m) mới biến mất - thời gian để người chơi đuổi theo")]
    public float fleeDistance = 30f;
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

    [Header("Kiểu khách (CustomerSpawner gán khi sinh ra)")]
    public CustomerType customerType = CustomerType.Homemaker;
    private CustomerProfile profile;
    public CustomerProfile Profile => profile;

    /// <summary>Áp hồ sơ kiểu khách: số món, độ nhạy giá, tốc độ, thời gian xem kệ, độ kiên nhẫn xếp hàng (gọi trước Start).</summary>
    public void ApplyProfile(CustomerProfile p)
    {
        if (p == null) return;
        profile = p;
        customerType = p.type;
        minItemsToBuy = p.minItems;
        maxItemsToBuy = Mathf.Max(p.minItems, p.maxItems);
        maxPriceRatio *= p.priceFactor;
        browseTime *= p.browseFactor;
        NavMeshAgent a = agent != null ? agent : GetComponent<NavMeshAgent>();
        if (a != null) a.speed *= p.speedFactor;
        visit.customerType = p.label;
        visit.queueTolerance = p.queueTolerance;
    }

    public IReadOnlyList<ItemData> Basket => basket;
    /// <summary>Bán kính chiếm chỗ của khách (để quầy giãn hàng chờ cho đủ chỗ đứng).</summary>
    public float Radius => agent != null ? agent.radius : 0.4f;

    private NavMeshAgent agent;
    private Animator animator;
    private ShoppingCarrier carrier;
    private bool hasSpeedParam, hasRunParam;
    private bool running;        // kẻ trộm đang chạy thục mạng (animation Running)
    private float lookYaw;       // góc quay đầu thêm (độ) khi dòm ngó
    private float walkSpeed;     // tốc độ đi bộ trước khi bỏ chạy (trả lại khi bị bắt)
    private Transform headBone, spineBone, chestBone;
    private float hunch;         // độ cúi người hiện tại (độ): lén lút cúi nhẹ, chạy trốn cúi thấp
    private float hunchTarget;
    private readonly List<ItemData> basket = new List<ItemData>();
    private readonly ReviewManager.Visit visit = new ReviewManager.Visit(); // ghi lại trải nghiệm để chấm điểm cửa hàng

    private CheckoutCounter counter;
    private Vector3 queueTarget;
    private bool queueTargetDirty;
    private bool isFirstInQueue;
    private bool paid;
    private bool caught;
    private bool alarmed;

    /// <summary>Đang ôm hàng chạy ra cửa mà chưa bị bắt.</summary>
    public bool IsFleeingThief => currentState == NPCState.Fleeing && !caught;

    /// <summary>Kẻ trộm đã ra khỏi cửa hàng (còi đã kêu): lúc này bảo vệ mới đuổi theo.</summary>
    public bool HasLeftStore => IsFleeingThief && alarmed;

    private static int stuckWarnings;

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        hasSpeedParam = animator != null && HasParameter(animator, "Speed");
        hasRunParam = animator != null && HasParameter(animator, "Run");
        headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        spineBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
        chestBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;

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
        if (hasRunParam) animator.SetBool("Run", running);
    }

    // Sau khi animation đã chạy: cúi người (kẻ trộm lén lút / chạy trốn) và quay đầu dòm ngó
    void LateUpdate()
    {
        hunch = Mathf.MoveTowards(hunch, hunchTarget, 90f * Time.deltaTime);
        if (hunch > 0.5f)
        {
            Vector3 right = transform.right;
            if (spineBone != null) spineBone.rotation = Quaternion.AngleAxis(hunch * 0.55f, right) * spineBone.rotation;
            if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(hunch * 0.45f, right) * chestBone.rotation;
            if (headBone != null) headBone.rotation = Quaternion.AngleAxis(-hunch * 0.6f, right) * headBone.rotation; // vẫn ngẩng nhìn phía trước
        }
        if (headBone != null && Mathf.Abs(lookYaw) > 0.5f)
            headBone.rotation = Quaternion.AngleAxis(lookYaw, Vector3.up) * headBone.rotation;
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
        else if (StoreFlow.EntryRoute(out Vector3[] entryRoute))
        {
            // Vào bằng cửa phía cổng vào, không đi xuyên khu thu ngân (StoreFlow)
            SetState(NPCState.Entering);
            foreach (Vector3 wpIn in entryRoute) yield return MoveTo(wpIn);
        }

        // --- Mua sắm ---
        SetState(NPCState.Shopping);
        int wanted = Random.Range(minItemsToBuy, maxItemsToBuy + 1);
        visit.wanted = wanted;
        if (!handsFree)
        {
            carrier = GetComponent<ShoppingCarrier>();
            if (carrier == null) carrier = gameObject.AddComponent<ShoppingCarrier>();
            carrier.Init(wanted); // xách giỏ hoặc đẩy xe
        }
        var visited = new HashSet<Shelf>();
        int attempts = 0, missed = 0;

        while (basket.Count + missed < wanted && attempts < wanted * 3)
        {
            attempts++;
            Shelf shelf = PickShelf(visited, profile);
            if (shelf == null) { visit.noStock = basket.Count < wanted; break; }
            visited.Add(shelf);

            Vector3 stand = ShelfStandPoint(shelf);
            yield return MoveTo(stand);
            if (FlatDistance(transform.position, stand) > reachDistance)
            {
                if (debugLogs && GameDebug.Verbose) Debug.Log($"[Customer] {name} không tới được kệ {shelf.name}, bỏ qua.", this);
                continue;
            }
            Face(shelf.transform.position);
            visit.maxDirt = Mathf.Max(visit.maxDirt, DirtManager.Count);
            yield return new WaitForSeconds(browseTime * Random.Range(0.7f, 1.4f));

            // Kệ bán hết (hoặc chỉ còn hàng hết hạn): khách không tìm được món định mua -> tính là thiếu hàng khi đánh giá
            if (shelf.IsEmpty || shelf.SellableCount <= 0)
            {
                missed++;
                if (missed == 1) SpeechBubble.Show(transform, "Sold out?!", new Color(1f, 0.6f, 0.3f), agent.height + 0.35f);
                if (shelf.IsEmpty) continue;
            }
            // Hàng hết hạn trên kệ: khách không lấy, và thấy là bực (trừ điểm sạch sẽ khi đánh giá)
            if (shelf.SpoiledCount > 0)
            {
                if (visit.sawSpoiled == 0) SpeechBubble.Show(transform, "Expired food?!", new Color(1f, 0.45f, 0.3f), agent.height + 0.35f);
                visit.sawSpoiled++;
            }
            if (shelf.SellableCount <= 0) continue;
            ItemData item = shelf.CurrentItem;
            int take = Random.Range(1, 3);
            if (item != null)
            {
                // So giá trên nhãn với giá thị trường: đắt thì có thể bỏ qua, rẻ thì lấy thêm.
                // Nhu cầu cao (vd. nước ngọt ngày nắng 35°C) thì khách chịu giá cao hơn và mua thêm (DemandModel).
                float demand = DemandModel.Current(item);
                float sell = PriceManager.GetSellPrice(item);
                float ratio = PriceManager.PriceRatio(item, sell);
                float felt = sell / DemandModel.PriceTolerance(demand);
                float chance = PriceManager.BuyChance(item, felt, maxPriceRatio) * DirtManager.SatisfactionFactor; // cửa hàng bẩn thì ít mua
                if (demand >= 1.5f && Random.value < 0.5f) take++;
                if (Random.value > chance)
                {
                    bool dirty = DirtManager.Count >= 4 && Random.value < 0.6f;
                    if (dirty) visit.dirtySkips++; else visit.tooExpensive++;
                    SpeechBubble.Show(transform, dirty ? "This store is dirty!" : "Too expensive!", new Color(1f, 0.35f, 0.3f), agent.height + 0.35f);
                    if (debugLogs && GameDebug.Verbose) Debug.Log($"[Customer] {name}: {item.itemName} đắt quá ({EconomyManager.Format(sell)}, thị trường {EconomyManager.Format(item.MarketPrice)}), bỏ qua.", this);
                    continue;
                }
                if (ratio <= PriceManager.CheapRatio)
                {
                    take++;
                    wanted++;
                    visit.greatPrice++;
                    SpeechBubble.Show(transform, "Great price!", new Color(0.4f, 1f, 0.45f), agent.height + 0.35f);
                }
                else if (ratio > 1.15f)
                {
                    visit.pricey++;
                    SpeechBubble.Show(transform, "Pricey...", new Color(1f, 0.8f, 0.3f), agent.height + 0.35f);
                }
            }
            for (int i = 0; i < take && basket.Count < wanted; i++)
                if (shelf.TryTakeForCustomer(out ItemData got) && got != null)
                {
                    basket.Add(got);
                    if (carrier != null) carrier.AddItem(got);
                }
        }

        // --- Thanh toán (hoặc ăn trộm) ---
        GameManager gm = GameManager.Instance;
        bool canSteal = gm != null && (gm.currentDay >= 2 || thiefChance >= 1f); // băng trộm (StoreEvents) trộm cả ngày 1
        if (basket.Count > 0 && canSteal && Random.value < thiefChance)
        {
            yield return FleeRoutine();
            yield break;
        }
        visit.got = basket.Count;
        if (basket.Count == 0 && missed > 0) visit.noStock = true;
        if (basket.Count > 0) yield return CheckoutRoutine();
        if (carrier != null) carrier.Release(); // không mua gì cũng trả giỏ / xe trước khi ra về

        // --- Đánh giá cửa hàng trước khi về ---
        try
        {
            visit.maxDirt = Mathf.Max(visit.maxDirt, DirtManager.Count);
            ReviewManager.Review review = ReviewManager.Submit(visit);
            Color rc = review.stars >= 4 ? new Color(0.4f, 1f, 0.45f) : review.stars == 3 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.35f);
            SpeechBubble.Show(transform, $"{review.stars}/5 stars", rc, agent.height + 0.35f, 3f);
        }
        catch (System.Exception e) { Debug.LogException(e, this); } // lỗi đánh giá không được làm khách kẹt lại

        // --- Ra về ---
        SetState(NPCState.Leaving);
        // Ra bằng cửa ra phía sau làn thu ngân rồi đi đường ngoài vỉa hè (StoreFlow), không quay lại lối vào
        if (StoreFlow.ExitRoute(out Vector3[] exitRoute))
            foreach (Vector3 wpOut in exitRoute) yield return MoveTo(wpOut);
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
            if (waited > (profile != null ? profile.queueGiveUp : 20f)) { basket.Clear(); counter = null; visit.gaveUpQueue = true; yield break; } // hàng quá dài, bỏ về
            yield return new WaitForSeconds(1f);
        }

        SetState(NPCState.Queueing);
        visit.queueStart = Time.time;
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
                    if (carrier != null) carrier.Release(); // bày hàng ra quầy, để lại giỏ / xe đẩy của siêu thị ở quầy
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

    // ================= Kẻ trộm =================
    /// <summary>
    /// Kẻ trộm (giống Supermarket Simulator): đứng lại dòm ngó xung quanh, đi bình thường ra cửa (chốc chốc liếc ra sau),
    /// bước ra khỏi cửa hàng thì cổng chống trộm mới kêu và kẻ trộm bỏ chạy thục mạng. Bảo vệ chỉ đuổi khi đã ra ngoài.
    /// Người chơi vẫn bắt được bằng gậy bất cứ lúc nào.
    /// </summary>
    private IEnumerator FleeRoutine()
    {
        SetState(NPCState.Fleeing);
        float baseSpeed = agent.speed;
        walkSpeed = baseSpeed;

        // 1. Cúi người, dòm ngó xem có ai để ý không
        hunchTarget = 20f;
        yield return LookAround();

        // 2. Đi bình thường ra cửa rồi ra xa cửa hàng (người chơi còn đuổi kịp ngoài vỉa hè)
        Vector3 escape = EscapePoint();
        StartCoroutine(MoveTo(escape));

        float t = 0f, nextGlance = Random.Range(0.8f, 1.6f);
        while (!caught && t < moveTimeout * 2f)
        {
            t += Time.deltaTime;
            if (!alarmed)
            {
                nextGlance -= Time.deltaTime;
                if (nextGlance <= 0f) { StartCoroutine(Glance()); nextGlance = Random.Range(1.5f, 3f); }
                if (OutsideStore())
                {
                    // 3. Ra khỏi cửa: còi kêu, cúi thấp người bỏ chạy
                    RaiseAlarm();
                    running = true;
                    hunchTarget = 32f;
                    lookYaw = 0f;
                    agent.speed = Mathf.Max(baseSpeed * fleeSpeedMultiplier, runSpeed);
                    agent.acceleration *= 2f;
                }
            }
            if (FlatDistance(transform.position, escape) < 1.5f) break;
            yield return null;
        }
        running = false;
        hunchTarget = 0f;
        if (caught) yield break; // Catch() lo phần còn lại

        float value = StolenValue();
        ProgressHUD.Toast($"A shoplifter got away with {EconomyManager.Format(value)} of goods!");
        Debug.Log($"[Customer] {name} trộm {basket.Count} món ({EconomyManager.Format(value)}) và chạy thoát.");
        SetState(NPCState.Done);
        Destroy(gameObject);
    }

    /// <summary>
    /// Điểm chạy trốn: cách cửa ra khoảng fleeDistance mét, đi tới được (đường đi hoàn chỉnh) và đủ xa cửa,
    /// để người chơi có thời gian đuổi theo ngoài vỉa hè.
    /// </summary>
    private Vector3 EscapePoint()
    {
        Vector3 exit = exitDoor != null ? exitDoor.position : transform.position;
        Vector3 store = Vector3.zero;
        int n = 0;
        foreach (Shelf sh in StockManager.Shelves) if (sh != null) { store += sh.transform.position; n++; }
        Vector3 outDir = n > 0 ? exit - store / n : transform.forward;
        outDir.y = 0f;
        if (outDir.sqrMagnitude < 0.01f) outDir = transform.forward;
        outDir.Normalize();

        var path = new NavMeshPath();
        Vector3 best = exit;
        float bestScore = -1f;
        // Thử nhiều hướng quanh hướng "ra khỏi cửa hàng", lấy điểm xa cửa nhất mà vẫn đi tới được
        for (int i = 0; i < 12; i++)
        {
            float angle = (i % 2 == 0 ? 1 : -1) * (i / 2) * 25f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * outDir;
            for (float f = 1f; f >= 0.4f; f -= 0.3f)
            {
                Vector3 p = exit + dir * fleeDistance * f;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, agent.areaMask)) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float score = FlatDistance(hit.position, exit);
                if (score > bestScore) { bestScore = score; best = hit.position; }
                break;
            }
            if (bestScore >= fleeDistance * 0.7f) break;
        }
        if (debugLogs && GameDebug.Verbose) Debug.Log($"[Customer] {name} chạy trốn tới {best:F1} (cách cửa {bestScore:F0}m).", this);
        return best;
    }

    /// <summary>Đứng lại, quay người trái - phải, đầu liếc theo (khoảng 2 - 3 giây).</summary>
    private IEnumerator LookAround()
    {
        if (!agent.isOnNavMesh) yield break;
        agent.isStopped = true;
        Quaternion start = transform.rotation;
        foreach (float yaw in new[] { -55f, 60f, -20f })
        {
            Quaternion from = transform.rotation, to = start * Quaternion.Euler(0f, yaw * 0.6f, 0f);
            float headFrom = lookYaw;
            for (float d = 0f; d < 1f; d += Time.deltaTime / 0.35f)
            {
                transform.rotation = Quaternion.Slerp(from, to, d);
                lookYaw = Mathf.Lerp(headFrom, yaw * 0.6f, d);
                yield return null;
            }
            yield return new WaitForSeconds(Random.Range(0.4f, 0.75f));
        }
        for (float d = 0f; d < 1f; d += Time.deltaTime / 0.3f) { lookYaw = Mathf.Lerp(lookYaw, 0f, d); yield return null; }
        lookYaw = 0f;
        if (agent.isOnNavMesh) agent.isStopped = false;
    }

    /// <summary>Liếc nhanh sang một bên rồi quay lại (vừa đi vừa liếc).</summary>
    private IEnumerator Glance()
    {
        float side = Random.value < 0.5f ? -1f : 1f;
        for (float d = 0f; d < 1f && !running; d += Time.deltaTime / 0.25f) { lookYaw = 70f * side * d; yield return null; }
        float hold = 0.5f;
        while (hold > 0f && !running) { hold -= Time.deltaTime; yield return null; }
        for (float d = 0f; d < 1f && !running; d += Time.deltaTime / 0.25f) { lookYaw = 70f * side * (1f - d); yield return null; }
        lookYaw = 0f;
    }

    /// <summary>
    /// Đã ra khỏi toà nhà: không còn dưới mái siêu thị / phòng mở rộng (WeatherSystem.IsSheltered - khung nhà + mái phía
    /// trên). Bản cũ lấy khoảng cách tới cánh cửa gần nhất nên còi kêu khi kẻ trộm còn ở trong cửa hàng.
    /// </summary>
    private bool OutsideStore() => WeatherSystem.Instance != null
        ? !WeatherSystem.IsSheltered(transform.position + Vector3.up * 0.2f)
        : exitDoor != null && FlatDistance(transform.position, exitDoor.position) < 2.5f;

    private void RaiseAlarm()
    {
        alarmed = true;
        AudioManager.Play("alarm", 0.85f); // chuông chống trộm của cả cửa hàng: nghe rõ ở mọi nơi, không bị tường chắn
        SpeechBubble.Show(transform, "THIEF!", new Color(1f, 0.25f, 0.2f), agent.height + 0.4f, 6f);
        ProgressHUD.Toast("Anti-theft alarm! A shoplifter is running away - catch them!");
    }

    /// <summary>Người chơi bắt được kẻ trộm: lấy lại tiền hàng, kẻ trộm xin lỗi rồi đi về.</summary>
    public void Catch()
    {
        if (!IsFleeingThief) return;
        caught = true;
        running = false;
        lookYaw = 0f;
        hunchTarget = 0f;
        StopAllCoroutines();
        float value = StolenValue();
        if (value > 0f) EconomyManager.Instance.AddIncome(value, MoneyCategory.Sales, "Recovered stolen goods");
        basket.Clear();
        if (carrier != null) carrier.Release();
        SpeechBubble.Show(transform, "Sorry!", Color.white, agent.height + 0.4f, 3f);
        ProgressHUD.Toast($"Thief caught! Recovered {EconomyManager.Format(value)}.");
        AudioManager.PlayAt("cash_register", transform.position, 0.5f); // tại chỗ bắt được kẻ trộm, không phải tiếng 2D
        if (walkSpeed > 0f) agent.speed = walkSpeed; // về tốc độ đi bộ
        if (agent.isOnNavMesh) agent.isStopped = false;
        StartCoroutine(LeaveAfterCaught());
    }

    private IEnumerator LeaveAfterCaught()
    {
        SetState(NPCState.Leaving);
        yield return new WaitForSeconds(1.5f);
        if (exitDoor != null) yield return MoveTo(exitDoor.position);
        SetState(NPCState.Done);
        Destroy(gameObject);
    }

    private float StolenValue()
    {
        float v = 0f;
        foreach (ItemData it in basket) v += PriceManager.GetSellPrice(it);
        return Mathf.Round(v * 100f) / 100f;
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
        visit.checkedOut = true;
        if (visit.queueStart >= 0f) visit.queueSeconds = Time.time - visit.queueStart;
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
        int sidesteps = 0;
        while (true)
        {
            if (!agent.isOnNavMesh) yield break; // bị đẩy khỏi NavMesh (xe / người chơi đẩy): nơi gọi tự kiểm tra khoảng cách
            elapsed += Time.deltaTime;
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance + 0.35f) break;

                still = IsStill() ? still + Time.deltaTime : 0f;
                if (still > 2f && agent.pathStatus != NavMeshPathStatus.PathComplete) break; // đã đi hết mức có thể
                // Đứng đối mặt với khách / nhân viên khác ở lối hẹp: né sang bên rồi đi tiếp (tối đa 2 lần mỗi chặng)
                if (still > 1.2f && sidesteps < 2 && BlockedByOther())
                {
                    sidesteps++;
                    yield return Sidestep();
                    if (!agent.isOnNavMesh) yield break;
                    agent.SetDestination(dest);
                    still = 0f;
                    continue;
                }
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

    /// <summary>Có người (khách, nhân viên) đứng sát trước mặt không.</summary>
    private bool BlockedByOther()
    {
        foreach (NavMeshAgent other in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (other == agent || !other.isActiveAndEnabled) continue;
            Vector3 d = other.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1.4f * 1.4f) return true;
        }
        return false;
    }

    /// <summary>Né sang trái hoặc phải khoảng 1 m, tạm nhường đường cho người kia, rồi quay lại đường cũ.</summary>
    private IEnumerator Sidestep()
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        Vector3 side = Vector3.Cross(Vector3.up, transform.forward).normalized;
        Vector3 back = -transform.forward * 0.4f;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(transform.position + side * sign * 0.9f + back, out hit, 0.6f, NavMesh.AllAreas) &&
            !NavMesh.SamplePosition(transform.position - side * sign * 0.9f + back, out hit, 0.6f, NavMesh.AllAreas))
        {
            yield return new WaitForSeconds(Random.Range(0.3f, 0.8f)); // không có chỗ né: chờ một chút cho người kia đi qua
            yield break;
        }
        int priority = agent.avoidancePriority;
        agent.avoidancePriority = Mathf.Min(99, priority + 20);
        agent.SetDestination(hit.position);
        float t = 0f;
        while (t < 1.2f && agent.isOnNavMesh && (agent.pathPending || agent.remainingDistance > 0.15f)) { t += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(Random.Range(0.2f, 0.6f));
        agent.avoidancePriority = priority;
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
        // Đứng lệch ngẫu nhiên dọc theo chiều dài kệ: 2 khách cùng xem một kệ không chen vào đúng một điểm (chống kẹt)
        if (n > 1)
        {
            Vector3 a = shelf.transform.right, b = shelf.transform.forward;
            a.y = 0f; b.y = 0f; a.Normalize(); b.Normalize();
            float aMin = 0f, aMax = 0f, bMin = 0f, bMax = 0f;
            foreach (Transform s in shelf.slots)
            {
                if (s == null) continue;
                Vector3 d = s.position - center;
                float da = Vector3.Dot(d, a), db = Vector3.Dot(d, b);
                aMin = Mathf.Min(aMin, da); aMax = Mathf.Max(aMax, da);
                bMin = Mathf.Min(bMin, db); bMax = Mathf.Max(bMax, db);
            }
            bool alongA = aMax - aMin >= bMax - bMin; // chiều dài kệ = trục các ô trải rộng nhất
            float half = Mathf.Max(0f, (alongA ? aMax - aMin : bMax - bMin) * 0.5f - 0.25f);
            center += (alongA ? a : b) * Random.Range(-half, half);
        }
        return Sample(center);
    }

    /// <summary>Số khách đang ở gần kệ (2 m): kệ đông người thì ít được chọn, để lối đi hẹp không bị dồn ứ.</summary>
    private static int CrowdNear(Shelf shelf)
    {
        int n = 0;
        Vector3 p = shelf.transform.position;
        foreach (NPCShopper c in Active)
            if (c != null && FlatDistance(c.transform.position, p) < 2f) n++;
        return n;
    }

    private static readonly List<Shelf> pickCandidates = new List<Shelf>();
    private static readonly List<float> pickWeights = new List<float>();

    /// <summary>
    /// Chọn kệ của một mặt hàng cửa hàng đang bán: trọng số = nhu cầu lúc này (DemandModel) x độ ưa thích nhóm hàng của
    /// kiểu khách (CustomerProfile). Kệ đã bán hết vẫn được chọn (kệ nhớ mặt hàng cũ - Shelf.LastItem): khách tới nơi thấy
    /// hết hàng thì đó là một món không mua được (trước đây khách chỉ chọn kệ còn hàng nên kệ trống không làm giảm điểm
    /// "Stock" - lỗi phát hiện khi chạy test case T10).
    /// </summary>
    private static Shelf PickShelf(HashSet<Shelf> visited, CustomerProfile who)
    {
        pickCandidates.Clear();
        pickWeights.Clear();
        float total = 0f;
        foreach (Shelf s in StockManager.Shelves)
        {
            if (s == null || visited.Contains(s)) continue;
            ItemData it = s.LastItem;
            if (it == null) continue; // kệ chưa từng bày hàng
            float w = DemandModel.Current(it);
            if (who != null) w *= who.Preference(DemandModel.GroupOf(it));
            w /= 1f + 2f * CrowdNear(s);
            pickCandidates.Add(s);
            pickWeights.Add(w);
            total += w;
        }
        if (pickCandidates.Count == 0) return null;
        float r = Random.value * total;
        for (int i = 0; i < pickCandidates.Count; i++)
        {
            r -= pickWeights[i];
            if (r <= 0f) return pickCandidates[i];
        }
        return pickCandidates[pickCandidates.Count - 1];
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
