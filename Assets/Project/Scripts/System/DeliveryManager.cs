using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Giao hàng tận nhà: thỉnh thoảng có đơn đặt hàng qua điện thoại (cần cửa hàng cấp 2).
///  [J] nhận đơn -> hàng được lấy từ kệ, đóng thành 1 gói hàng đặt ở chỗ giao hàng trước cửa hàng.
///  Tới gần gói hàng bấm [E] để cầm -> lên xe máy / ô tô ([F]) chạy tới cột sáng vàng chỗ người đặt.
///  Tới nơi là giao xong: nhận tiền hàng + phí ship + tip (giao nhanh thì tip cao). [K] từ chối đơn.
/// Tự tạo khi vào game.
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance { get; private set; }

    public int requiredLevel = 2;
    public Vector2 offerInterval = new Vector2(70f, 140f);
    public float offerTimeout = 40f;
    public float minDistance = 40f;
    public float arriveRadius = 7f;
    public float pickupRange = 2.4f;

    private class Order
    {
        public readonly List<(ItemData item, int count)> lines = new List<(ItemData, int)>();
        public string placeName;
        public Vector3 destination;
        public float distance;
        public float goodsValue;
        public float fee;
        public float allowedTime;
        public float acceptedAt;
        public string Summary
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var l in lines) { if (sb.Length > 0) sb.Append(", "); sb.Append($"{l.count}x {l.item.itemName}"); }
                return sb.ToString();
            }
        }
    }

    private enum Stage { Idle, Offer, PickUp, Delivering }
    private Stage stage = Stage.Idle;
    private Order order;
    private float nextOfferAt, offerExpiresAt;
    private float nextHudAt;          // chữ HUD (đếm giờ, khoảng cách) dựng lại 5 lần/giây thay vì mỗi frame
    private Stage hudStage = Stage.Idle;

    /// <summary>true khi tới lúc cập nhật chữ HUD (hoặc vừa đổi giai đoạn đơn hàng).</summary>
    private bool HudTick()
    {
        if (stage == hudStage && Time.unscaledTime < nextHudAt) return false;
        hudStage = stage;
        nextHudAt = Time.unscaledTime + 0.2f;
        return true;
    }
    private GameObject package, beacon, carried;
    private TMP_Text hudText;
    private GameObject hudPanel;

    /// <summary>Mục tiêu hiện tại cho bản đồ / minimap: gói hàng cần lấy hoặc chỗ giao hàng.</summary>
    public static bool TryGetTarget(out Vector3 position, out string label, out bool isPackage)
    {
        position = default; label = null; isPackage = false;
        DeliveryManager m = Instance;
        if (m == null || m.order == null) return false;
        if (m.stage == Stage.PickUp && m.package != null)
        {
            position = m.package.transform.position; label = "Package"; isPackage = true;
            return true;
        }
        if (m.stage == Stage.Delivering)
        {
            position = m.order.destination; label = m.order.placeName;
            return true;
        }
        return false;
    }

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
        if (Instance == null && FindFirstObjectByType<GameManager>() != null) new GameObject("DeliveryManager").AddComponent<DeliveryManager>();
    }

    void Awake()
    {
        Instance = this;
        BuildHud();
        nextOfferAt = Time.time + Random.Range(offerInterval.x * 0.5f, offerInterval.y * 0.5f);
        GameManager.OnNewDay += OnNewDay;
    }

    void OnDestroy()
    {
        GameManager.OnNewDay -= OnNewDay;
        if (Instance == this) Instance = null;
        Cleanup();
    }

    private void OnNewDay(int day)
    {
        if (stage == Stage.PickUp || stage == Stage.Delivering) ProgressHUD.Toast("The delivery order was cancelled (new day).");
        Cleanup();
        stage = Stage.Idle;
        nextOfferAt = Time.time + Random.Range(offerInterval.x, offerInterval.y) * 0.5f;
    }

    // ================= Mỗi frame =================
    void Update()
    {
        bool uiFree = !UIState.IsBlockingUIOpen;
        switch (stage)
        {
            case Stage.Idle:
                hudPanel.SetActive(false);
                if (Time.time >= nextOfferAt)
                {
                    nextOfferAt = Time.time + Random.Range(offerInterval.x, offerInterval.y);
                    if (StoreProgress.Level >= requiredLevel && TryCreateOrder(out order))
                    {
                        stage = Stage.Offer;
                        offerExpiresAt = Time.time + offerTimeout;
                        AudioManager.Play("card_beep", 0.8f);
                        ProgressHUD.Toast("New delivery order! [J] Accept   [K] Decline");
                    }
                }
                break;

            case Stage.Offer:
                float left = offerExpiresAt - Time.time;
                if (HudTick()) ShowHud($"<b><color=#FFD54A>DELIVERY ORDER</color></b>  <size=80%>({left:0}s)</size>\n{order.Summary}\nTo: <b>{order.placeName}</b>  ·  {order.distance:0} m\n" +
                        $"Pay {EconomyManager.Format(order.goodsValue)} + delivery fee {EconomyManager.Format(order.fee)}\n<color=#8CFF8C>[J] Accept</color>   <color=#FF8A7A>[K] Decline</color>");
                if (left <= 0f || (uiFree && Input.GetKeyDown(KeyCode.K))) { order = null; stage = Stage.Idle; }
                else if (uiFree && Input.GetKeyDown(KeyCode.J)) Accept();
                break;

            case Stage.PickUp:
                Transform player = PlayerTransform();
                bool near = player != null && package != null && Vector3.Distance(player.position, package.transform.position) <= pickupRange;
                if (HudTick()) ShowHud($"<b><color=#FFD54A>DELIVERY</color></b>  {order.Summary}\n" +
                        (near ? "<color=#8CFF8C>[E] Pick up the package</color>" : $"Pick up the package at the delivery spot  {Arrow(package != null ? package.transform.position : Vector3.zero)}") +
                        $"\nTime left {TimeLeft()}");
                if (near && uiFree && Input.GetKeyDown(KeyCode.E)) PickUpPackage();
                CheckLate();
                break;

            case Stage.Delivering:
                Transform p = PlayerTransform();
                float dist = p != null ? Flat(p.position - order.destination) : 999f;
                if (HudTick()) ShowHud($"<b><color=#FFD54A>DELIVERY</color></b> to <b>{order.placeName}</b>\n{dist:0} m  {Arrow(order.destination)}   ·   Time left {TimeLeft()}\n<size=80%>Follow the route on the minimap · [M] Map · [F] near a bike / car to ride</size>");
                if (dist <= arriveRadius) Complete();
                else CheckLate();
                break;
        }
    }

    // ================= Đơn hàng =================
    private bool TryCreateOrder(out Order o)
    {
        o = null;
        // Hàng đang có trên kệ
        var stock = new Dictionary<ItemData, int>();
        foreach (Shelf sh in StockManager.Shelves)
        {
            if (sh == null || sh.CurrentItem == null || sh.Count <= 0) continue;
            stock.TryGetValue(sh.CurrentItem, out int c);
            stock[sh.CurrentItem] = c + sh.Count;
        }
        var items = new List<ItemData>();
        foreach (var kv in stock) if (kv.Value >= 2) items.Add(kv.Key);
        if (items.Count == 0) return false;
        if (!PickDestination(out string place, out Vector3 dest, out float dist)) return false;

        o = new Order { placeName = place, destination = dest, distance = dist };
        int kinds = Mathf.Min(items.Count, Random.Range(1, 3));
        for (int i = 0; i < kinds; i++)
        {
            int idx = Random.Range(0, items.Count);
            ItemData it = items[idx];
            items.RemoveAt(idx);
            int count = Mathf.Clamp(Random.Range(2, 6), 1, stock[it]);
            o.lines.Add((it, count));
            o.goodsValue += PriceManager.GetSellPrice(it) * count;
        }
        o.fee = Mathf.Round(8f + dist * 0.06f);
        o.allowedTime = 75f + dist / 3.5f;
        return true;
    }

    /// <summary>Chọn một toà nhà quanh khu (nhà dân, bệnh viện, đồn công an...) đủ xa cửa hàng, lấy điểm trên mặt đất trước toà nhà.</summary>
    private bool PickDestination(out string place, out Vector3 point, out float distance)
    {
        place = null; point = default; distance = 0f;
        Vector3 store = StoreCenter();
        var candidates = new List<(string, Vector3, float)>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (!root.activeInHierarchy) continue;
            string n = root.name.ToLowerInvariant();
            bool building = n.Contains("building") || n.Contains("hospital") || n.Contains("police") || n.Contains("fire") || n.Contains("house") || n.Contains("apartment");
            if (!building || n.Contains("supermarket")) continue;
            if (!Bounds(root, out Bounds b) || b.size.x < 4f || b.size.z < 4f) continue;

            // Điểm trên mép toà nhà gần cửa hàng nhất, đẩy ra ngoài 3m
            Vector3 edge = b.ClosestPoint(new Vector3(store.x, b.min.y, store.z));
            Vector3 outDir = new Vector3(store.x - edge.x, 0f, store.z - edge.z);
            if (outDir.sqrMagnitude < 0.01f) continue;
            Vector3 p = edge + outDir.normalized * 3f;
            if (!Physics.Raycast(new Vector3(p.x, b.max.y + 5f, p.z), Vector3.down, out RaycastHit hit, b.size.y + 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.7f) continue;
            float d = Flat(hit.point - store);
            if (d < minDistance) continue;
            candidates.Add((Pretty(root.name), hit.point, d));
        }
        // Địa chỉ ở khu phố mới (có số nhà + tên đường): ưu tiên để đi giao xa hơn, đa dạng hơn
        var addresses = new List<(string, Vector3, float)>();
        foreach (DeliveryAddress a in DeliveryAddress.All)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            float d = Flat(a.doorPoint - store);
            if (d >= minDistance) addresses.Add((a.address, a.doorPoint, d));
        }
        if (addresses.Count > 0 && (candidates.Count == 0 || Random.value < 0.75f)) candidates = addresses;
        if (candidates.Count == 0) return false;
        var c = candidates[Random.Range(0, candidates.Count)];
        place = c.Item1; point = c.Item2; distance = c.Item3;
        return true;
    }

    private void Accept()
    {
        // Lấy hàng khỏi kệ
        float value = 0f;
        foreach (var line in order.lines)
        {
            int need = line.count;
            foreach (Shelf sh in StockManager.Shelves)
            {
                if (need <= 0) break;
                if (sh == null || sh.CurrentItem != line.item) continue;
                while (need > 0 && sh.TryTakeProduct(out GameObject prod)) { if (prod != null) Destroy(prod); need--; }
            }
            value += PriceManager.GetSellPrice(line.item) * (line.count - need);
        }
        if (value <= 0f)
        {
            ProgressHUD.Toast("Not enough stock on the shelves for this order.");
            order = null; stage = Stage.Idle;
            return;
        }
        order.goodsValue = value;
        StockManager.NotifyChanged();
        order.acceptedAt = Time.time;
        package = CreatePackage(PackageSpot());
        stage = Stage.PickUp;
        AudioManager.Play("box_pickup", 0.8f);
        ProgressHUD.Toast("Order packed! Pick up the package outside the store.");
    }

    private void PickUpPackage()
    {
        if (package != null) Destroy(package);
        package = null;
        ItemPickup hands = PlayerTransform() != null ? PlayerTransform().GetComponentInChildren<ItemPickup>(true) : null;
        Camera cam = hands != null && hands.playerCamera != null ? hands.playerCamera : Camera.main;
        if (cam != null)
        {
            // Gói hàng kẹp bên tay trái (thấy khi đi bộ)
            carried = CreatePackageModel("CarriedPackage");
            foreach (Collider c in carried.GetComponentsInChildren<Collider>()) Destroy(c);
            carried.transform.SetParent(cam.transform, false);
            carried.transform.localPosition = new Vector3(-0.42f, -0.38f, 0.7f);
            carried.transform.localRotation = Quaternion.Euler(10f, 20f, 0f);
            carried.transform.localScale = Vector3.one * 0.6f;
        }
        beacon = CreateBeacon(order.destination);
        stage = Stage.Delivering;
        AudioManager.Play("box_pickup", 0.8f);
    }

    private void Complete()
    {
        float elapsed = Time.time - order.acceptedAt;
        bool fast = elapsed <= order.allowedTime;
        float tip = fast ? Mathf.Round((order.fee * 0.5f + 5f) * WeatherSystem.DeliveryTipMultiplier) : 0f; // mưa / giông tip nhiều hơn
        float total = order.goodsValue + order.fee + tip;
        BusinessStats.RecordDelivery(total);
        EconomyManager.Instance.AddIncome(total, MoneyCategory.Sales, $"Delivery to {order.placeName}");
        StoreProgress.AddXP(fast ? 8 : 4);
        AudioManager.Play("cash_register", 0.8f);
        ProgressHUD.Toast(fast
            ? $"Delivered on time! +{EconomyManager.Format(total)} (tip {EconomyManager.Format(tip)}{(WeatherSystem.DeliveryTipMultiplier > 1f ? ", rain bonus" : "")})"
            : $"Delivered (late, no tip). +{EconomyManager.Format(total)}");
        Cleanup();
        order = null;
        stage = Stage.Idle;
    }

    private void CheckLate()
    {
        // Quá gấp đôi thời gian cho phép: khách huỷ đơn
        if (Time.time - order.acceptedAt <= order.allowedTime * 2f) return;
        ProgressHUD.Toast("The customer cancelled the delivery - it took too long.");
        Cleanup();
        order = null;
        stage = Stage.Idle;
    }

    private string TimeLeft()
    {
        float left = order.allowedTime - (Time.time - order.acceptedAt);
        if (left <= 0f) return "<color=#FF8A7A>late</color>";
        return $"{Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";
    }

    private void Cleanup()
    {
        if (package != null) Destroy(package);
        if (beacon != null) Destroy(beacon);
        if (carried != null) Destroy(carried);
        package = beacon = carried = null;
        if (hudPanel != null) hudPanel.SetActive(false);
    }

    // ================= Vị trí =================
    private static Transform cachedPlayer;

    /// <summary>Người chơi (đang ngồi xe thì bị ẩn nhưng vẫn là con của xe -> vị trí đi theo xe).</summary>
    private static Transform PlayerTransform()
    {
        if (cachedPlayer != null) return cachedPlayer;
        foreach (ItemPickup hands in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (hands == null || hands.playerCamera == null) continue; // bỏ ItemPickup thừa trên UI máy tính
            CharacterController cc = hands.GetComponentInParent<CharacterController>(true);
            cachedPlayer = cc != null ? cc.transform : hands.transform;
            return cachedPlayer;
        }
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }

    /// <summary>Camera đang hiển thị (camera người chơi hoặc camera xe).</summary>
    private static Camera ActiveCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled) return main;
        foreach (Camera c in Camera.allCameras) if (c != null && c.isActiveAndEnabled && c.targetTexture == null) return c;
        return null;
    }

    private static Vector3 StoreCenter()
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (Shelf sh in StockManager.Shelves) if (sh != null) { sum += sh.transform.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    private Vector3 PackageSpot()
    {
        MarketManager market = FindFirstObjectByType<MarketManager>();
        Vector3 p;
        if (market != null && market.deliveryPoint != null) p = market.deliveryPoint.position + market.deliveryPoint.right * 1.2f;
        else
        {
            GameObject dp = GameObject.Find("DeliveryPoint");
            Transform player = PlayerTransform();
            p = dp != null ? dp.transform.position + Vector3.right * 1.2f : (player != null ? player.position + player.forward * 1.5f : Vector3.zero);
        }
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
        return p;
    }

    // ================= Hình ảnh =================
    private static GameObject CreatePackageModel(string name)
    {
        var root = new GameObject(name);
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(root.transform, false);
        box.transform.localScale = new Vector3(0.45f, 0.32f, 0.35f);
        box.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        RuntimeMaterials.Apply(box.GetComponent<Renderer>(), new Color(0.74f, 0.55f, 0.33f));
        GameObject tape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(tape.GetComponent<Collider>());
        tape.transform.SetParent(root.transform, false);
        tape.transform.localScale = new Vector3(0.46f, 0.33f, 0.07f);
        tape.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        RuntimeMaterials.Apply(tape.GetComponent<Renderer>(), new Color(0.95f, 0.8f, 0.25f));
        GameObject label = new GameObject("Label");
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.325f, 0f);
        label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMeshPro t = label.AddComponent<TextMeshPro>();
        t.text = "DELIVERY";
        t.fontSize = 1.1f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.2f, 0.1f, 0.05f);
        t.rectTransform.sizeDelta = new Vector2(0.45f, 0.2f);
        return root;
    }

    private static GameObject CreatePackage(Vector3 at)
    {
        GameObject pkg = CreatePackageModel("DeliveryPackage");
        pkg.transform.position = at;
        // Cột sáng nhỏ để dễ thấy gói hàng
        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(pkg.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        glow.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
        Renderer r = glow.GetComponent<Renderer>();
        r.sharedMaterial = RuntimeMaterials.Ghost(new Color(1f, 0.85f, 0.2f, 0.25f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return pkg;
    }

    private static GameObject CreateBeacon(Vector3 at)
    {
        var root = new GameObject("DeliveryBeacon");
        root.transform.position = at;
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pillar.GetComponent<Collider>());
        pillar.transform.SetParent(root.transform, false);
        pillar.transform.localPosition = new Vector3(0f, 25f, 0f);
        pillar.transform.localScale = new Vector3(2.2f, 25f, 2.2f);
        Renderer r = pillar.GetComponent<Renderer>();
        r.sharedMaterial = RuntimeMaterials.Ghost(new Color(1f, 0.85f, 0.15f, 0.3f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ring.transform.localScale = new Vector3(7f, 0.02f, 7f);
        Renderer rr = ring.GetComponent<Renderer>();
        rr.sharedMaterial = RuntimeMaterials.Ghost(new Color(1f, 0.85f, 0.15f, 0.35f));
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return root;
    }

    // ================= HUD =================
    private void BuildHud()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, 55);
        hudPanel = UIKit.Panel("DeliveryPanel", canvas.transform, new Vector2(560, 190), Vector2.zero, new Color(0.08f, 0.1f, 0.14f, 0.82f));
        var rt = (RectTransform)hudPanel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-20f, 120f);
        hudText = UIKit.Text(hudPanel.transform, "", 24, new Vector2(530, 175), Vector2.zero, TextAlignmentOptions.Left);
        hudPanel.SetActive(false);
    }

    private void ShowHud(string text)
    {
        if (!hudPanel.activeSelf) hudPanel.SetActive(true);
        hudText.text = text;
    }

    /// <summary>Mũi tên chỉ hướng tới mục tiêu so với hướng camera đang nhìn.</summary>
    private static string Arrow(Vector3 target)
    {
        Camera cam = ActiveCamera();
        if (cam == null) return "";
        Vector3 to = target - cam.transform.position; to.y = 0f;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        if (to.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.01f) return "";
        float a = Vector3.SignedAngle(fwd, to, Vector3.up);
        string[] dirs = { "ahead", "ahead-right", "right", "behind-right", "behind", "behind-left", "left", "ahead-left" };
        int i = Mathf.RoundToInt(((a + 360f) % 360f) / 45f) % 8;
        return $"<color=#FFD54A>[{dirs[i]}]</color>";
    }

    // ================= Tiện ích =================
    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    private static bool Bounds(GameObject go, out Bounds b)
    {
        b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    private static string Pretty(string raw)
    {
        string n = raw.Replace("_prefab", "").Replace(" prefab", "").Replace("_", " ").Trim();
        if (n.ToLowerInvariant().StartsWith("building")) return "Apartment " + n.Substring(8).Trim().ToUpperInvariant();
        return n;
    }
}
