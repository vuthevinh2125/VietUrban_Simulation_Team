using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Minimap tròn góc trên phải (xoay theo hướng người chơi / xe) + bản đồ lớn ([M]).
/// Đánh dấu: người chơi, cửa hàng, gói hàng / chỗ giao hàng, kẻ trộm đang chạy, bảo vệ.
/// Đang có đơn giao hàng thì vẽ lộ trình (đường đi trên NavMesh) từ người chơi tới đích trên cả minimap và bản đồ lớn.
/// Bản đồ lớn: lăn chuột để phóng to / thu nhỏ, [M] / [Esc] để đóng. Tự tạo khi vào game.
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    public static MinimapSystem Instance { get; private set; }
    public static bool IsBigOpen => Instance != null && Instance.bigOpen;
    public static int ClosedFrame { get; private set; } = -1;

    [Header("Minimap")]
    public float miniSize = 280f;
    public float miniZoom = 42f;          // nửa chiều cao vùng nhìn (mét)
    public float camHeight = 160f;
    [Header("Big map")]
    public float bigZoom = 110f;
    public Vector2 bigZoomRange = new Vector2(30f, 260f);

    private static readonly Color RouteColor = new Color(1f, 0.78f, 0.1f, 0.95f);
    private static readonly Color PlayerColor = new Color(0.25f, 0.85f, 1f);
    private static readonly Color StoreColor = new Color(0.2f, 0.75f, 0.35f);
    private static readonly Color TargetColor = new Color(1f, 0.8f, 0.1f);
    private static readonly Color ThiefColor = new Color(1f, 0.25f, 0.2f);
    private static readonly Color GuardColor = new Color(0.35f, 0.55f, 1f);

    private class MapView
    {
        public Camera cam;
        public RectTransform image;      // RawImage hiển thị camera
        public RectTransform routeLayer; // lộ trình (bị cắt theo khung)
        public RectTransform markerLayer;
        public readonly List<Image> routePool = new List<Image>();
        public readonly List<RectTransform> markerPool = new List<RectTransform>();
        public int usedMarkers;
        public bool circular;
    }

    private MapView mini, big;
    private GameObject miniRoot, bigRoot;
    private TMP_Text bigInfo;
    private bool bigOpen;
    private Transform player;
    private readonly List<Vector3> route = new List<Vector3>();
    private float nextRouteAt;
    private Vector3 routeTarget;
    private bool hasTarget;
    private static Sprite circleSprite, arrowSprite, squareSprite;

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
        if (Instance == null && FindFirstObjectByType<GameManager>() != null) new GameObject("MinimapSystem").AddComponent<MinimapSystem>();
    }

    void Awake()
    {
        Instance = this;
        circleSprite = MakeCircleSprite();
        arrowSprite = MakeArrowSprite();
        squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        BuildMinimap();
        BuildBigMap();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (mini?.cam != null) mini.cam.targetTexture?.Release();
        if (big?.cam != null) big.cam.targetTexture?.Release();
        UIState.Close(this);
    }

    // ================= Mỗi frame =================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M) && (bigOpen || !UIState.IsBlockingUIOpen)) SetBigOpen(!bigOpen);
        else if (bigOpen && Input.GetKeyDown(KeyCode.Escape)) SetBigOpen(false);
        if (bigOpen)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) bigZoom = Mathf.Clamp(bigZoom * (scroll > 0 ? 0.85f : 1.18f), bigZoomRange.x, bigZoomRange.y);
        }
    }

    void LateUpdate()
    {
        if (player == null) player = FindPlayer();
        if (player == null) { miniRoot.SetActive(false); return; }

        // Ẩn minimap khi đang ở menu chính / màn hình khác che (vẫn hiện khi chỉ có bản đồ lớn)
        bool showMini = !bigOpen && (!UIState.IsBlockingUIOpen || UIState.IsOpenedBy(this));
        if (miniRoot.activeSelf != showMini) miniRoot.SetActive(showMini);

        Vector3 p = player.position;
        float yaw = player.eulerAngles.y;

        // Camera minimap: trên đầu người chơi, xoay để hướng người chơi đang nhìn luôn là phía trên
        mini.cam.orthographicSize = miniZoom;
        mini.cam.transform.SetPositionAndRotation(new Vector3(p.x, p.y + camHeight, p.z), Quaternion.Euler(90f, yaw, 0f));
        if (bigOpen)
        {
            big.cam.orthographicSize = bigZoom;
            big.cam.transform.SetPositionAndRotation(new Vector3(p.x, p.y + camHeight, p.z), Quaternion.Euler(90f, 0f, 0f));
        }

        UpdateRoute(p);
        if (showMini) DrawView(mini, p, yaw, true);
        if (bigOpen) DrawView(big, p, yaw, false);
    }

    // ================= Lộ trình =================
    private void UpdateRoute(Vector3 from)
    {
        hasTarget = DeliveryManager.TryGetTarget(out Vector3 target, out _, out _);
        if (!hasTarget) { route.Clear(); return; }
        bool targetMoved = (target - routeTarget).sqrMagnitude > 1f;
        if (Time.time < nextRouteAt && !targetMoved) return;
        nextRouteAt = Time.time + 0.6f;
        routeTarget = target;
        route.Clear();

        var path = new NavMeshPath();
        bool okA = NavMesh.SamplePosition(from, out NavMeshHit a, 6f, NavMesh.AllAreas);
        bool okB = NavMesh.SamplePosition(target, out NavMeshHit b, 10f, NavMesh.AllAreas);
        if (okA && okB && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
        {
            route.Add(from);
            route.AddRange(path.corners);
            if (path.status != NavMeshPathStatus.PathComplete || (path.corners[path.corners.Length - 1] - target).sqrMagnitude > 4f) route.Add(target);
        }
        else
        {
            route.Add(from);
            route.Add(target);
        }
    }

    // ================= Vẽ =================
    private void DrawView(MapView v, Vector3 playerPos, float yaw, bool isMini)
    {
        Rect r = v.image.rect;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;

        // Lộ trình
        int seg = 0;
        if (hasTarget && route.Count > 1)
        {
            Vector2 prev = Project(v, route[0]);
            for (int i = 1; i < route.Count; i++)
            {
                Vector2 cur = Project(v, route[i]);
                // gom các đoạn quá ngắn
                if ((cur - prev).sqrMagnitude < 9f && i < route.Count - 1) continue;
                Image line = Pooled(v.routePool, v.routeLayer, seg++);
                float w = isMini ? 6f : 7f;
                Vector2 d = cur - prev;
                var rt = line.rectTransform;
                rt.sizeDelta = new Vector2(d.magnitude + w * 0.6f, w);
                rt.anchoredPosition = (prev + cur) * 0.5f;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                prev = cur;
            }
        }
        for (int i = seg; i < v.routePool.Count; i++) if (v.routePool[i].gameObject.activeSelf) v.routePool[i].gameObject.SetActive(false);

        // Đánh dấu
        v.usedMarkers = 0;
        Vector3 store = StoreCenter(out bool hasStore);
        if (hasStore) Marker(v, store, StoreColor, 26f, "S", circleSprite, 0f, true, radius);
        foreach (NPCShopper n in NPCShopper.Active)
            if (n != null && n.IsFleeingThief) Marker(v, n.transform.position, ThiefColor, 16f, null, circleSprite, 0f, true, radius);
        foreach (StoreGuard g in FindObjectsByType<StoreGuard>(FindObjectsSortMode.None))
            Marker(v, g.transform.position, GuardColor, 12f, null, circleSprite, 0f, false, radius);
        if (DeliveryManager.TryGetTarget(out Vector3 target, out _, out bool isPackage))
            Marker(v, target, TargetColor, isPackage ? 22f : 30f, isPackage ? "P" : "!", isPackage ? squareSprite : circleSprite, 0f, true, radius);
        // Người chơi: minimap luôn hướng lên; bản đồ lớn quay theo hướng nhìn
        Marker(v, playerPos, PlayerColor, isMini ? 26f : 30f, null, arrowSprite, isMini ? 0f : -yaw, true, radius);
        for (int i = v.usedMarkers; i < v.markerPool.Count; i++) if (v.markerPool[i].gameObject.activeSelf) v.markerPool[i].gameObject.SetActive(false);

        if (!isMini && bigInfo != null)
        {
            string goal = DeliveryManager.TryGetTarget(out Vector3 t, out string label, out _) ? $"<color=#FFD54A>Delivery: {label} ({Vector3.Distance(playerPos, t):0} m)</color>   " : "";
            bigInfo.text = $"{goal}<color=#40D9FF>Arrow = You</color>   <color=#33C05A>S = Store</color>   <color=#FFD54A>P / ! = Delivery</color>   <color=#FF4033>Red = Thief</color>   <color=#5A8CFF>Blue = Guard</color>   ·   Scroll: zoom   [M] Close";
        }
    }

    private void Marker(MapView v, Vector3 world, Color color, float size, string letter, Sprite sprite, float rotZ, bool clampToEdge, float radius)
    {
        Vector2 pos = Project(v, world);
        Rect r = v.image.rect;
        bool outside;
        if (v.circular)
        {
            float max = radius - size * 0.5f - 2f;
            outside = pos.magnitude > max;
            if (outside) { if (!clampToEdge) return; pos = pos.normalized * max; }
        }
        else
        {
            float hx = r.width * 0.5f - size * 0.5f, hy = r.height * 0.5f - size * 0.5f;
            outside = Mathf.Abs(pos.x) > hx || Mathf.Abs(pos.y) > hy;
            if (outside) { if (!clampToEdge) return; pos = new Vector2(Mathf.Clamp(pos.x, -hx, hx), Mathf.Clamp(pos.y, -hy, hy)); }
        }

        RectTransform m;
        if (v.usedMarkers < v.markerPool.Count) m = v.markerPool[v.usedMarkers];
        else { m = NewMarker(v.markerLayer); v.markerPool.Add(m); }
        v.usedMarkers++;
        if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
        m.SetAsLastSibling();
        m.anchoredPosition = pos;
        m.sizeDelta = new Vector2(size, size);
        m.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        Image img = m.GetComponent<Image>();
        img.sprite = sprite;
        img.color = outside ? new Color(color.r, color.g, color.b, 0.75f) : color;
        TMP_Text t = m.GetComponentInChildren<TMP_Text>(true);
        t.gameObject.SetActive(!string.IsNullOrEmpty(letter));
        if (!string.IsNullOrEmpty(letter)) { t.text = letter; t.fontSize = size * 0.62f; t.rectTransform.sizeDelta = new Vector2(size, size); }
    }

    /// <summary>Toạ độ thế giới -> toạ độ trong khung bản đồ (gốc ở giữa).</summary>
    private static Vector2 Project(MapView v, Vector3 world)
    {
        Vector3 vp = v.cam.WorldToViewportPoint(world);
        Rect r = v.image.rect;
        return new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
    }

    // ================= Mở / đóng bản đồ lớn =================
    private void SetBigOpen(bool open)
    {
        bigOpen = open;
        bigRoot.SetActive(open);
        big.cam.enabled = open;
        if (open) UIState.Open(this);
        else { UIState.Close(this); ClosedFrame = Time.frameCount; }
    }

    // ================= Dựng giao diện =================
    private void BuildMinimap()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, 45);
        miniRoot = UIKit.Rect("Minimap", canvas.transform, new Vector2(miniSize + 12f, miniSize + 12f));
        var rt = (RectTransform)miniRoot.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);

        // Viền tròn
        Image border = miniRoot.AddComponent<Image>();
        border.sprite = MakeCircleSprite();
        border.color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

        // Khung tròn cắt ảnh camera + lộ trình
        GameObject maskGo = UIKit.Rect("Mask", miniRoot.transform, new Vector2(miniSize, miniSize));
        Image maskImg = maskGo.AddComponent<Image>();
        maskImg.sprite = MakeCircleSprite();
        maskGo.AddComponent<Mask>().showMaskGraphic = false;

        mini = CreateView(maskGo.transform, miniSize, miniSize, 384, "MinimapCamera", miniRoot.transform);
        mini.circular = true;

        // Chữ N (hướng bắc) không cần vì minimap xoay theo người chơi; ghi chú phím mở bản đồ
        TMP_Text hint = UIKit.Text(miniRoot.transform, "[M] Map", 18, new Vector2(120, 24), new Vector2(0, -(miniSize * 0.5f + 16f)), TextAlignmentOptions.Center, true, new Color(1f, 1f, 1f, 0.85f));
        hint.raycastTarget = false;
    }

    private void BuildBigMap()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, 560);
        bigRoot = UIKit.FullScreen("BigMap", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
        GameObject window = UIKit.Panel("Window", bigRoot.transform, new Vector2(1040, 1000), Vector2.zero, UIKit.Window);
        GameObject header = UIKit.Panel("Header", window.transform, new Vector2(1040, 60), new Vector2(0, 470), UIKit.Header);
        UIKit.Text(header.transform, "MAP", 32, new Vector2(1000, 56), Vector2.zero, TextAlignmentOptions.Center, true);
        UIKit.Btn(header.transform, "X", new Vector2(46, 46), new Vector2(490, 0), () => SetBigOpen(false), UIKit.Red);

        GameObject frame = UIKit.Rect("Frame", window.transform, new Vector2(1000, 860), new Vector2(0, -15));
        frame.AddComponent<Image>().color = Color.black;
        frame.AddComponent<RectMask2D>();
        big = CreateView(frame.transform, 1000, 860, 1024, "BigMapCamera", frame.transform);
        big.cam.enabled = false;
        bigInfo = UIKit.Text(window.transform, "", 22, new Vector2(1000, 40), new Vector2(0, -470), TextAlignmentOptions.Center);
        bigRoot.SetActive(false);
    }

    private MapView CreateView(Transform imageParent, float w, float h, int texSize, string camName, Transform markerParent)
    {
        var v = new MapView();
        var camGo = new GameObject(camName);
        camGo.transform.SetParent(transform, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = camHeight + 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.2f, 0.17f);
        cam.cullingMask = ~(1 << 5); // bỏ layer UI
        cam.aspect = w / h;
        cam.depth = -50;
        int th = Mathf.RoundToInt(texSize * h / w);
        var rtx = new RenderTexture(texSize, th, 16) { name = camName + "_RT" };
        cam.targetTexture = rtx;
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.requiresDepthTexture = false;
        data.requiresColorTexture = false;
        v.cam = cam;

        GameObject img = UIKit.Rect("MapImage", imageParent, new Vector2(w, h));
        RawImage raw = img.AddComponent<RawImage>();
        raw.texture = rtx;
        raw.raycastTarget = false;
        v.image = (RectTransform)img.transform;

        GameObject routeLayer = UIKit.Rect("Route", imageParent, new Vector2(w, h));
        v.routeLayer = (RectTransform)routeLayer.transform;
        GameObject markerLayer = UIKit.Rect("Markers", markerParent, new Vector2(w, h));
        v.markerLayer = (RectTransform)markerLayer.transform;
        return v;
    }

    private static Image Pooled(List<Image> pool, RectTransform parent, int index)
    {
        while (pool.Count <= index)
        {
            GameObject go = UIKit.Rect("RouteSeg", parent);
            Image img = go.AddComponent<Image>();
            img.color = RouteColor;
            img.raycastTarget = false;
            pool.Add(img);
        }
        Image i = pool[index];
        if (!i.gameObject.activeSelf) i.gameObject.SetActive(true);
        return i;
    }

    private static RectTransform NewMarker(RectTransform parent)
    {
        GameObject go = UIKit.Rect("Marker", parent, new Vector2(20, 20));
        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        // Viền tối cho dễ nhìn trên nền bản đồ
        Outline o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
        TMP_Text t = UIKit.Text(go.transform, "", 14, new Vector2(20, 20), Vector2.zero, TextAlignmentOptions.Center, true, new Color(0.08f, 0.08f, 0.08f));
        t.gameObject.SetActive(false);
        return (RectTransform)go.transform;
    }

    // ================= Tiện ích =================
    private static Transform FindPlayer()
    {
        foreach (ItemPickup hands in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (hands == null || hands.playerCamera == null) continue;
            CharacterController cc = hands.GetComponentInParent<CharacterController>(true);
            return cc != null ? cc.transform : hands.transform;
        }
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }

    private static Vector3 StoreCenter(out bool ok)
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (Shelf sh in StockManager.Shelves) if (sh != null) { sum += sh.transform.position; n++; }
        ok = n > 0;
        return ok ? sum / n : Vector3.zero;
    }

    private static Sprite MakeCircleSprite()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        float c = (s - 1) * 0.5f, rad = s * 0.5f - 1f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(rad - d + 0.5f) * 255);
                px[y * s + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Mũi tên tam giác chỉ lên (hướng người chơi).</summary>
    private static Sprite MakeArrowSprite()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        Vector2 a = new Vector2(32, 62), b = new Vector2(6, 4), c = new Vector2(58, 4), notch = new Vector2(32, 18);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = InTri(p, a, b, notch) || InTri(p, a, notch, c);
                px[y * s + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    private static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
}
