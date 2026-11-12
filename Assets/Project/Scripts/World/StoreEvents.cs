using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Sự kiện ngẫu nhiên trong ngày để game bớt đơn điệu (tự tạo khi vào game). Mỗi sự kiện mở đầu bằng một đoạn cắt cảnh
/// ngắn 2.5-3.5 s (camera riêng, khung đen điện ảnh, dải tiêu đề kiểu bản tin) rồi người chơi tự xử lý:
///  - THIEF: mỗi khi có kẻ trộm bỏ chạy -> còi chống trộm, camera đuổi theo kẻ trộm, chậm hình một nhịp. Sự kiện
///    "băng trộm": 3 khách vào cùng lúc, đều sẽ trộm.
///  - HEALTH INSPECTION: thanh tra vào cửa hàng, đi xem 3 kệ. Có vết bẩn / hàng hết hạn -> phạt $50 + $15 / vết bẩn
///    + $10 / món hỏng và một đánh giá kém; sạch sẽ -> thưởng $100 và đánh giá 5 sao.
///  - TOUR GROUP: đoàn 8 khách du lịch xếp hàng dọc đi vào, tay không (không giỏ / xe), mua 1-3 món lưu niệm.
/// Mỗi sự kiện có một "đoạn phim nhỏ" 5-8 s gồm 2-3 cú máy cắt nối (toàn cảnh mặt tiền, bám theo nhân vật, cận mặt kèm lời thoại),
/// giống cảnh mở đầu nhưng ngắn; cảnh trộm bỏ chạy chỉ 3.5 s và chậm hình vì có thể xảy ra nhiều lần.
/// Đoạn phim của mỗi loại chỉ chiếu lần đầu tiên loại đó xuất hiện (lưu trong file save, New Game thì xem lại từ đầu);
/// các lần sau chỉ có thông báo và bong bóng thoại.
/// Từ ngày 2, mỗi ngày 65% có một sự kiện, xảy ra ở 20-70% thời gian mở cửa, không lặp lại sự kiện của hôm trước.
/// Phím thử (chỉ bản Editor / Development): Shift + F10 gọi ngay sự kiện kế tiếp.
/// </summary>
public class StoreEvents : MonoBehaviour
{
    public enum Kind { ThiefGang, Inspector, TourGroup }

    public static StoreEvents Instance { get; private set; }
    public static bool CutsceneRunning { get; private set; }

    [Range(0f, 1f)] public float dailyChance = 0.65f;
    public int firstDay = 2;
    public float thiefCutsceneCooldown = 45f;

    private float eventAtProgress = -1f;   // thời điểm (tỉ lệ ngày) sẽ xảy ra sự kiện hôm nay, -1 = không có
    private int plannedDay = -1;
    private Kind lastKind = Kind.TourGroup;
    private int debugIndex;
    private float nextThiefCut;
    private readonly HashSet<NPCShopper> seenThieves = new HashSet<NPCShopper>();
    private float nextThiefScan;

    // Đoạn phim của mỗi loại sự kiện chỉ chiếu MỘT lần duy nhất (lần đầu nhóm đối tượng đó xuất hiện), lưu theo file save.
    // Các lần sau sự kiện vẫn xảy ra nhưng chỉ có thông báo + bong bóng thoại, không cắt cảnh.
    private static readonly HashSet<string> seenCutscenes = new HashSet<string>();
    public static List<string> ExportSeen() => new List<string>(seenCutscenes);
    public static void ImportSeen(List<string> list) { seenCutscenes.Clear(); if (list != null) seenCutscenes.UnionWith(list); }
    public static void ResetSeen() => seenCutscenes.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Create();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Create();
    private static void Create() { if (Instance == null && SupermarketScene.IsActive) new GameObject("StoreEvents").AddComponent<StoreEvents>(); }

    void Awake() { Instance = this; doorCached = false; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || IntroCutscene.Playing) return;

        if (GameDebug.CheatsEnabled && Input.GetKeyDown(KeyCode.F10) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            // Ctrl + Shift + F10: xem lại phim sự kiện (xoá danh sách đã chiếu)
            ResetSeen();
            ProgressHUD.Toast("[Debug] Event cutscenes will play again.");
        }
        else if (GameDebug.CheatsEnabled && Input.GetKeyDown(KeyCode.F10) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            Kind k = (Kind)(debugIndex++ % 3);
            StartEvent(k);
        }

        WatchThieves();

        if (!gm.IsStoreOpen || gm.IsGameOver) return;
        if (plannedDay != gm.currentDay) PlanDay(gm.currentDay);
        if (eventAtProgress >= 0f && gm.DayProgress01 >= eventAtProgress && !CutsceneRunning && !UIState.IsBlockingUIOpen)
        {
            eventAtProgress = -1f;
            Kind k = PickKind();
            lastKind = k;
            StartEvent(k);
        }
    }

    private void PlanDay(int day)
    {
        plannedDay = day;
        eventAtProgress = day >= firstDay && Random.value < dailyChance ? Random.Range(0.2f, 0.7f) : -1f;
    }

    private Kind PickKind()
    {
        Kind k;
        int guard = 0;
        do { k = (Kind)Random.Range(0, 3); } while (k == lastKind && ++guard < 10);
        return k;
    }

    public void StartEvent(Kind k)
    {
        switch (k)
        {
            case Kind.ThiefGang: StartCoroutine(ThiefGang()); break;
            case Kind.Inspector: StartCoroutine(Inspection()); break;
            case Kind.TourGroup: StartCoroutine(TourGroup()); break;
        }
    }

    // ================= Trộm =================
    /// <summary>Kẻ trộm vừa bỏ chạy (kể cả trộm thường): còi + cắt cảnh đuổi theo, tối đa 1 lần / 45 s.</summary>
    private void WatchThieves()
    {
        if (Time.unscaledTime < nextThiefScan) return;
        nextThiefScan = Time.unscaledTime + 0.25f;
        seenThieves.RemoveWhere(n => n == null);
        foreach (NPCShopper n in NPCShopper.Active)
        {
            if (n == null || !n.IsFleeingThief || seenThieves.Contains(n)) continue;
            seenThieves.Add(n);
            AudioManager.Play("alarm", 0.9f);
            if (Time.unscaledTime >= nextThiefCut && !CutsceneRunning && Time.timeScale > 0f)
            {
                nextThiefCut = Time.unscaledTime + thiefCutsceneCooldown;
                StartCoroutine(ThiefCut(n));
            }
            else ProgressHUD.Toast("<color=#FF6B6B>A thief is running away! [3] bat, then swing!</color>");
            break;
        }
    }

    private IEnumerator ThiefCut(NPCShopper thief)
    {
        Color red = new Color(1f, 0.4f, 0.35f);
        yield return Cutscene("STOP THIEF!", "Someone just ran out without paying. Grab the bat [3] and chase them!", new Color(0.85f, 0.15f, 0.12f), 0.45f,
            new Shot(1.3f, () => Face(thief != null ? thief.transform : null, 3f), () => Say(thief, "Thief", "Gotta go, gotta go!", red)),
            new Shot(2.2f, () => Track(thief != null ? thief.transform : null, 5f, 1.5f, 2.2f, 1.1f)));
    }

    private IEnumerator ThiefGang()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null || StockManager.GetRandomStockedShelf() == null) yield break;
        var gang = new List<NPCShopper>();
        for (int i = 0; i < 3; i++)
        {
            NPCShopper n = spawner.Spawn();
            if (n != null) { n.thiefChance = 1f; n.name += "_Gang"; gang.Add(n); }
            yield return new WaitForSeconds(0.6f);
        }
        if (gang.Count == 0) yield break;
        Vector3 door = DoorPoint(out Vector3 outward);
        Color orange = new Color(1f, 0.75f, 0.4f);
        yield return Cutscene("SUSPICIOUS GROUP", "Three strangers are walking in together. Keep an eye on them...", new Color(0.85f, 0.45f, 0.1f), 1f,
            new Shot(2.6f, () => (door + outward * 13f + Vector3.Cross(Vector3.up, outward) * 5f + Vector3.up * 2.5f,
                                  gang[0] != null ? gang[0].transform.position + Vector3.up * 1.2f : door, gang[0] != null)),
            new Shot(2.4f, () => Face(gang[0] != null ? gang[0].transform : null), () => Say(gang[0], "Stranger", "Nobody's watching...", orange)),
            new Shot(2.2f, () => Face(gang[gang.Count - 1] != null ? gang[gang.Count - 1].transform : null, 2.3f),
                     () => Say(gang[gang.Count - 1], "Stranger", "Grab and run. Easy.", orange)));
    }

    // ================= Thanh tra vệ sinh =================
    private IEnumerator Inspection()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        GameObject prefab = spawner != null ? spawner.RandomCustomerPrefab() : null;
        Vector3 door = DoorPoint(out Vector3 outward);
        if (prefab == null) yield break;

        // Thanh tra: model khách (bỏ script mua sắm), đi bằng NavMeshAgent
        Vector3 start = door + outward * 6f;
        if (NavMesh.SamplePosition(start, out NavMeshHit h, 4f, NavMesh.AllAreas)) start = h.position;
        var holder = new GameObject("_InspectorHolder");
        holder.SetActive(false);
        GameObject inspector = Instantiate(prefab, start, Quaternion.LookRotation(-outward), holder.transform);
        foreach (NPCShopper s in inspector.GetComponentsInChildren<NPCShopper>(true)) DestroyImmediate(s);
        // Prefab người đi đường chỉ được đi vùng "Sidewalks": NPCShopper.Awake mở thêm sàn cửa hàng nhưng script đó đã bị bỏ
        // -> phải tự mở, nếu không thanh tra đi tới mép vỉa hè rồi đứng úp mặt vào tường.
        NavMeshAgent pre = inspector.GetComponent<NavMeshAgent>();
        if (pre != null) pre.areaMask = NavMesh.AllAreas;
        inspector.transform.SetParent(null, true);
        Destroy(holder);
        inspector.name = "HealthInspector";
        Rigidbody rb = inspector.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        NavMeshAgent agent = inspector.GetComponent<NavMeshAgent>();
        Animator anim = inspector.GetComponentInChildren<Animator>();
        bool hasSpeed = false;
        if (anim != null) foreach (AnimatorControllerParameter p in anim.parameters) if (p.name == "Speed") hasSpeed = true;
        if (agent != null)
        {
            agent.enabled = true;
            agent.speed = 1.3f;
            agent.avoidancePriority = 20;
            if (!agent.isOnNavMesh || agent.nextPosition != start) agent.Warp(start);
        }

        // Thanh tra bước vào trong lúc quay
        if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(door - outward * 3f, out NavMeshHit inH, 3f, NavMesh.AllAreas)) agent.SetDestination(inH.position);
        Color blue = new Color(0.7f, 0.85f, 1f);
        StartCoroutine(AnimateWhile(agent, anim, hasSpeed, 8f));
        yield return Cutscene("HEALTH INSPECTION", "A city health inspector has arrived. Dirty floors and expired food will cost you!", new Color(0.15f, 0.45f, 0.85f), 1f,
            new Shot(2.6f, () => Track(inspector != null ? inspector.transform : null, -6f, 3f, 1.6f), () => Say(inspector != null ? inspector.transform : null, "Inspector", "Good afternoon. City Health Department.", blue)),
            new Shot(2.6f, () => Face(inspector != null ? inspector.transform : null), () => Say(inspector != null ? inspector.transform : null, "Inspector", "Let's see how clean this place really is.", blue, 3f)),
            new Shot(2.2f, () => (door + outward * 12f + Vector3.up * 3f, door + Vector3.up * 1.6f, true)));

        // Đi xem tối đa 3 kệ đang bán
        var shelves = new List<Shelf>();
        foreach (Shelf s in StockManager.Shelves) if (s != null && s.isActiveAndEnabled) shelves.Add(s);
        for (int i = 0; i < 3 && shelves.Count > 0 && inspector != null; i++)
        {
            Shelf s = shelves[Random.Range(0, shelves.Count)];
            shelves.Remove(s);
            if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(s.transform.position, out NavMeshHit sh, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(sh.position);
                float until = Time.time + 20f;
                while (inspector != null && Time.time < until && (agent.pathPending || agent.remainingDistance > 1.2f))
                {
                    if (hasSpeed) anim.SetFloat("Speed", agent.velocity.magnitude);
                    yield return null;
                }
            }
            if (hasSpeed) anim.SetFloat("Speed", 0f);
            if (inspector != null) SpeechBubble.Show(inspector.transform, s.SpoiledCount > 0 ? "Expired food here!" : "Hmm...", new Color(0.7f, 0.85f, 1f), 2.1f, 2f);
            yield return new WaitForSeconds(2.5f);
        }

        // Kết luận
        int dirt = DirtManager.Count, spoiled = 0;
        foreach (Shelf s in StockManager.Shelves) if (s != null) spoiled += s.SpoiledCount;
        var visit = new ReviewManager.Visit { wanted = 1, got = 1, checkedOut = true, queueTolerance = 35f, customerType = "Inspector", maxDirt = dirt, sawSpoiled = spoiled };
        EconomyManager eco = EconomyManager.Instance;
        if (dirt == 0 && spoiled == 0)
        {
            if (eco != null) eco.AddIncome(100f, MoneyCategory.Other, "Hygiene award");
            if (inspector != null) SpeechBubble.Show(inspector.transform, "Spotless! Well done.", new Color(0.5f, 1f, 0.5f), 2.1f, 3f);
            ProgressHUD.Toast("<color=#6BE37B>Inspection passed - spotless store! +$100 hygiene award.</color>");
        }
        else
        {
            float fine = 50f + 15f * dirt + 10f * spoiled;
            if (eco != null) eco.Charge(fine, MoneyCategory.Other, "Health inspection fine");
            if (inspector != null) SpeechBubble.Show(inspector.transform, "This is not acceptable.", new Color(1f, 0.5f, 0.4f), 2.1f, 3f);
            ProgressHUD.Toast($"<color=#FF8A7A>Inspection failed: {dirt} dirty spot(s), {spoiled} expired item(s). Fine {EconomyManager.Format(fine)}.</color>");
        }
        ReviewManager.Submit(visit);
        AudioManager.Play(dirt == 0 && spoiled == 0 ? "level_up" : "alarm", 0.6f);

        // Ra về rồi biến mất
        if (inspector != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(start + Vector3.Cross(Vector3.up, outward) * 15f);
            float until = Time.time + 12f;
            while (inspector != null && Time.time < until)
            {
                if (hasSpeed) anim.SetFloat("Speed", agent.velocity.magnitude);
                yield return null;
            }
        }
        if (inspector != null) Destroy(inspector);
    }

    // ================= Đoàn khách du lịch =================
    /// <summary>
    /// Đoàn 8 người xếp một hàng dọc trên vỉa hè, đi tay không (không giỏ / xe đẩy), cùng tốc độ, người đi sau nhường
    /// đường người đi trước (avoidancePriority tăng dần) để không chen nhau xoay vòng ở cửa. Người đầu hàng là hướng dẫn viên.
    /// </summary>
    private IEnumerator TourGroup()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null) yield break;
        Vector3 door = DoorPoint(out Vector3 outward);
        Vector3 sideV = Vector3.Cross(Vector3.up, outward);
        var group = new List<NPCShopper>();
        for (int i = 0; i < 8; i++)
        {
            NPCShopper n = spawner.Spawn();
            if (n == null) continue;
            n.handsFree = true;            // khách du lịch: tay không, chỉ mua vài món lưu niệm
            n.minItemsToBuy = 1;
            n.maxItemsToBuy = 3;
            n.thiefChance = 0f;
            n.name += "_Tourist";
            NavMeshAgent ag = n.GetComponent<NavMeshAgent>();
            if (ag != null)
            {
                ag.speed = 1.15f;                 // cùng tốc độ -> giữ được hàng, không ai vượt ai
                ag.avoidancePriority = 30 + i * 3; // người sau nhường người trước
                Vector3 slot = door + outward * 3.5f + sideV * (4.5f + i * 1.3f);
                if (NavMesh.SamplePosition(slot, out NavMeshHit sh, 2.5f, NavMesh.AllAreas))
                {
                    ag.Warp(sh.position);
                    n.transform.rotation = Quaternion.LookRotation(-sideV);
                }
            }
            group.Add(n);
        }
        if (group.Count == 0) yield break;
        NPCShopper guide = group[0], tourist = group[Mathf.Min(3, group.Count - 1)];
        Color green = new Color(0.6f, 1f, 0.6f);
        yield return Cutscene("TOUR GROUP!", "A tour bus just stopped outside. Get the shelves and checkouts ready!", new Color(0.15f, 0.65f, 0.35f), 1f,
            new Shot(3f, () => (door + outward * 9f + sideV * 1f + Vector3.up * 2.2f, door + outward * 3f + sideV * 7f + Vector3.up * 1.1f, true),
                     () => Say(guide, "Tour guide", "This way, everyone! Stay in line, please.", green, 3f)),
            new Shot(2.6f, () => Face(tourist != null ? tourist.transform : null, 2.8f),
                     () => Say(tourist, "Tourist", "Wow, a real Vietnamese grocery! I want to try everything!", green, 3f)),
            new Shot(2.6f, () => (door - outward * 5f + sideV * 2f + Vector3.up * 2.2f, door + outward * 2f + Vector3.up * 1.3f, true)));
    }

    private static IEnumerator AnimateWhile(NavMeshAgent agent, Animator anim, bool hasSpeed, float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end && agent != null)
        {
            if (hasSpeed && anim != null) anim.SetFloat("Speed", agent.velocity.magnitude);
            yield return null;
        }
    }

    // ================= Cắt cảnh ngắn =================
    /// <summary>Một cú máy: shot() trả về vị trí camera + điểm nhìn mỗi frame (bám theo nhân vật đang đi); onStart: lời thoại...</summary>
    private struct Shot
    {
        public System.Func<(Vector3 pos, Vector3 look, bool ok)> frame;
        public float seconds;
        public System.Action onStart;
        public Shot(float seconds, System.Func<(Vector3, Vector3, bool)> frame, System.Action onStart = null)
        { this.seconds = seconds; this.frame = frame; this.onStart = onStart; }
    }

    private IEnumerator Cutscene(string title, string subtitle, float seconds, Color accent, float slowMo, System.Func<(Vector3 pos, Vector3 look, bool ok)> shot)
        => Cutscene(title, subtitle, accent, slowMo, new Shot(seconds, shot));

    /// <summary>
    /// "Đoạn phim nhỏ" cho sự kiện: nhiều cú máy nối nhau (cắt cảnh), camera riêng, khung đen điện ảnh, dải tiêu đề kiểu bản tin.
    /// slowMo &lt; 1: chậm hình. Khoá điều khiển người chơi; bấm Space để bỏ qua.
    /// </summary>
    private IEnumerator Cutscene(string title, string subtitle, Color accent, float slowMo, params Shot[] shots)
    {
        if (shots.Length == 0) yield break;
        if (seenCutscenes.Contains(title))
        {
            // Đã xem đoạn phim này rồi -> chỉ báo bằng thông báo, lời thoại đầu tiên hiện thành bong bóng trên đầu
            NoFilm(title, subtitle, shots[0], "đã chiếu trước đó (lưu trong save)");
            yield break;
        }
        // Đang có phim khác / cảnh mở đầu / cửa sổ máy tính đang mở / game tạm dừng -> chờ tới khi rảnh (tối đa 15 s)
        // thay vì bỏ qua luôn (trước đây phim bị bỏ qua lặng lẽ nên "lúc có lúc không")
        float waitUntil = Time.unscaledTime + 15f;
        while (Busy() && Time.unscaledTime < waitUntil) yield return null;
        if (Busy()) { NoFilm(title, subtitle, shots[0], "đang bận quá 15 s (phim khác / cửa sổ / tạm dừng)"); yield break; }
        var first = shots[0].frame();
        if (!first.ok) { NoFilm(title, subtitle, shots[0], "nhân vật chính của cảnh đã biến mất"); yield break; }
        seenCutscenes.Add(title);
        CutsceneRunning = true;

        Camera like = Camera.main;
        var camGo = new GameObject("EventCamera");
        Camera cam = camGo.AddComponent<Camera>();
        if (like != null) cam.CopyFrom(like);
        cam.targetTexture = null;
        cam.depth = 95f;
        cam.fieldOfView = 50f;
        Camera prevBubbleCam = SpeechBubble.ViewCamera;
        SpeechBubble.ViewCamera = cam;
        bool wasFrozen = PlayerController.InputFrozen;
        PlayerController.InputFrozen = true;
        float prevScale = Time.timeScale;
        Time.timeScale = slowMo;

        // Ẩn HUD (tiền, bản đồ nhỏ, hướng dẫn, thông báo) như cảnh mở đầu
        var hiddenCanvases = new List<Canvas>();
        foreach (Canvas c in FindObjectsByType<Canvas>())
        {
            if (c == null || !c.enabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }

        Canvas canvas = UIKit.CreateCanvas(camGo.transform, 880);
        Bar(canvas.transform, true);
        Bar(canvas.transform, false);
        GameObject banner = UIKit.Panel("Banner", canvas.transform, new Vector2(1920, 110), new Vector2(0, -330), new Color(0f, 0f, 0f, 0.65f));
        UIKit.Panel("Accent", banner.transform, new Vector2(16, 110), new Vector2(-952, 0), accent);
        UIKit.Text(banner.transform, title, 46, new Vector2(1700, 56), new Vector2(0, 22), TextAlignmentOptions.Center, true, accent * 1.3f + new Color(0.2f, 0.2f, 0.2f));
        UIKit.Text(banner.transform, subtitle, 26, new Vector2(1700, 40), new Vector2(0, -28), TextAlignmentOptions.Center, false);
        TMP_Text skip = UIKit.Text(canvas.transform, "[Space] Skip", 20, new Vector2(300, 40), Vector2.zero, TextAlignmentOptions.Right, false, new Color(1f, 1f, 1f, 0.6f));
        RectTransform skipRt = skip.rectTransform;
        skipRt.anchorMin = skipRt.anchorMax = skipRt.pivot = new Vector2(1f, 1f);
        skipRt.anchoredPosition = new Vector2(-30f, -25f);
        // Lời thoại hiện thành phụ đề phía trên dải tiêu đề (bong bóng thoại 3D quá to khi camera đứng sát)
        subLine = UIKit.Text(canvas.transform, "", 30, new Vector2(1500, 90), new Vector2(0, -215), TextAlignmentOptions.Center, false);
        var group = banner.AddComponent<CanvasGroup>();

        float start = Time.unscaledTime;
        bool skipped = false;
        foreach (Shot sh in shots)
        {
            if (skipped) break;
            sh.onStart?.Invoke();
            var f = sh.frame();
            if (!f.ok) continue;
            Vector3 pos = f.pos;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(f.look - pos)); // cắt cảnh: đặt ngay, không lướt
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < sh.seconds)
            {
                if (Input.GetKeyDown(KeyCode.Space)) { skipped = true; break; }
                var s = sh.frame();
                if (s.ok)
                {
                    pos = Vector3.Lerp(pos, s.pos, Time.unscaledDeltaTime * 2.5f);
                    cam.transform.SetPositionAndRotation(pos, Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(s.look - pos), Time.unscaledDeltaTime * 5f));
                }
                group.alpha = Mathf.Clamp01((Time.unscaledTime - start) / 0.3f);
                yield return null;
            }
        }

        Time.timeScale = prevScale > 0f ? prevScale : 1f;
        subLine = null;
        foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
        PlayerController.InputFrozen = wasFrozen;
        SpeechBubble.ViewCamera = prevBubbleCam;
        Destroy(camGo);
        CutsceneRunning = false;
    }

    private static TMP_Text subLine;

    private static bool Busy() =>
        CutsceneRunning || IntroCutscene.Playing || NightRobbery.Playing || Time.timeScale <= 0f || UIState.IsBlockingUIOpen;

    /// <summary>Không chiếu phim: vẫn báo sự kiện bằng thông báo + bong bóng thoại, và ghi lý do ra Console.</summary>
    private static void NoFilm(string title, string subtitle, Shot first, string reason)
    {
        ProgressHUD.Toast($"<b>{title}</b> {subtitle}");
        first.onStart?.Invoke();
        if (GameDebug.CheatsEnabled) Debug.Log($"[StoreEvents] {title}: không chiếu phim - {reason}.");
    }

    private static (Vector3, Vector3, bool) Track(Transform t, float back, float sideOff, float up, float lookUp = 1.4f)
    {
        if (t == null) return (Vector3.zero, Vector3.zero, false);
        Vector3 look = t.position + Vector3.up * lookUp;
        return (Unblocked(look, t.position - t.forward * back + t.right * sideOff + Vector3.up * up), look, true);
    }

    /// <summary>
    /// Cận mặt: camera đứng trước mặt nhân vật, lệch sang một bên. Nếu phía trước là tường / kính (nhân vật đang đi về phía
    /// cửa hàng) thì quay qua vai từ phía sau, để camera không chui vào tường.
    /// </summary>
    private static (Vector3, Vector3, bool) Face(Transform t, float dist = 2.6f)
    {
        if (t == null) return (Vector3.zero, Vector3.zero, false);
        Vector3 head = t.position + Vector3.up * 1.55f;
        Vector3 front = head + t.forward * dist + t.right * 0.9f - Vector3.up * 0.15f;
        if ((Unblocked(head, front) - head).magnitude > dist * 0.8f) return (front, head, true);
        Vector3 behind = head - t.forward * 2.4f + t.right * 0.8f + Vector3.up * 0.2f;
        Vector3 ahead = head + t.forward * 3f - Vector3.up * 0.2f;
        return (Unblocked(head, behind), ahead, true);
    }

    /// <summary>Kéo camera lại gần nếu giữa nhân vật và camera có tường (bỏ qua người / trigger).</summary>
    private static Vector3 Unblocked(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return to;
        float best = len;
        foreach (RaycastHit h in Physics.SphereCastAll(from, 0.2f, d / len, len, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance <= 0f || h.collider.GetComponentInParent<NavMeshAgent>() != null || h.collider.GetComponentInParent<CharacterController>() != null) continue;
            best = Mathf.Min(best, h.distance - 0.15f);
        }
        return from + d / len * Mathf.Max(0.6f, best);
    }

    /// <summary>Lời thoại: trong đoạn phim hiện thành phụ đề "Tên: lời"; ngoài ra là bong bóng thoại trên đầu.</summary>
    private static void Say(Component who, string speaker, string text, Color color, float seconds = 2.5f)
    {
        if (subLine != null) { subLine.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{speaker}:</b></color> {text}"; return; }
        if (who != null) SpeechBubble.Show(who.transform, text, color, 2.1f, seconds);
    }

    private static void Bar(Transform root, bool top)
    {
        GameObject g = UIKit.Rect(top ? "BarTop" : "BarBottom", root);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 90f);
        Image img = g.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    /// <summary>Điểm giữa các cửa ra vào (trên mặt đất) và hướng từ trong cửa hàng ra phố.</summary>
    private static bool doorCached;
    private static Vector3 cachedDoor, cachedOutward;

    private static Vector3 DoorPoint(out Vector3 outward)
    {
        if (doorCached) { outward = cachedOutward; return cachedDoor; }
        outward = Vector3.back;
        EntranceDoor[] doors = FindObjectsByType<EntranceDoor>();
        GameObject store = GameObject.Find("Supermarket_full");
        if (doors.Length == 0) return Vector3.zero;
        Vector3 door = Vector3.zero;
        foreach (EntranceDoor d in doors) door += d.transform.position;
        door /= doors.Length;
        if (store != null)
        {
            Vector3 c = store.transform.position;
            Renderer[] rs = store.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds); c = b.center; door.y = b.min.y; }
            outward = door - c; outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.back;
            outward.Normalize();
        }
        doorCached = true; cachedDoor = door; cachedOutward = outward; // khung toà nhà không đổi -> chỉ tính 1 lần
        return door;
    }
}
