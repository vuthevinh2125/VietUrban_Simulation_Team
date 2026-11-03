using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý phương tiện của người chơi: danh mục xe, sở hữu (lưu trong save), khoá xe chưa mua, gọi xe về cửa hàng.
///  - Honda Cub: xe khởi đầu, có sẵn.
///  - Các xe khác đỗ sẵn ngoài cửa hàng với bảng "FOR SALE"; mua ở máy tính → tab Vehicles.
///  - Xe chưa mua không lên được (bấm F chỉ hiện thông báo).
///  - Mỗi lần bấm F chỉ lên xe GẦN NHẤT (tránh lên 2 xe cùng lúc khi đỗ sát nhau).
/// </summary>
public class VehicleManager : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string id;          // tên object trong scene
        public string name;
        public string type;        // Motorbike / Car
        public string description;
        public float price;
        public int level;
        public float speed, boost; // tốc độ thường / tăng tốc (đơn vị của PlayerCarController)
        public bool starter;
        // Xe "thay vỏ": nhân bản xe mẫu trong scene (template) để có sẵn phần lái / camera / va chạm,
        // rồi thay hình bằng model trong Resources (model). parkSlot: đỗ nối đuôi xe mẫu dọc lề đường
        // (1 = phía sau, 2 = phía trước, mỗi ô 5.5 m); có GameObject "ParkingSpot_<id>" trong scene thì đỗ ở đó.
        public string template, model;
        public int parkSlot;
        public float modelYaw;     // xoay thêm nếu model quay ngược
        // Vị trí mắt góc thứ nhất theo tỉ lệ khung thân xe (x: trái 0 -> phải 1, y: đáy -> nóc, z: đuôi -> đầu); zero = tự tính
        public Vector3 fpEye;
        // Model làm cả thân một vật liệu đục: tách kính theo ô màu trên texture (uMin, vMin, uMax, vMax) + vùng khoang lái (VehicleGlass)
        public Vector4 glassUv;
        public Vector2 glassZ;
        public float glassMinH;
        public string plateTexture;  // thay biển số kiểu Mỹ bằng biển Việt Nam (Resources), vật liệu "Car_Number_Mat"
    }

    public static readonly Entry[] Catalog =
    {
        new Entry
        {
            id = "Honda Cub", name = "Honda Cub", type = "Motorbike", starter = true, price = 0f, level = 1,
            speed = 8f, boost = 12f,
            description = "Your trusty starter bike. Cheap, light and easy to park anywhere.",
        },
        new Entry
        {
            id = "C125 - PB", name = "Honda C125", type = "Motorbike", price = 900f, level = 3,
            speed = 10f, boost = 15f,
            description = "Premium scooter with a stronger engine - finish deliveries before the timer runs out.",
        },
        new Entry
        {
            id = "SedanPlayer", name = "Classic Sedan", type = "Car", price = 1800f, level = 4,
            speed = 11f, boost = 16f, template = "CarPlayer", model = "Vehicles/Sedan", parkSlot = 1, plateTexture = "Vehicles/PlateVN",
            description = "A roomy family sedan. The cheapest way to drive a car on deliveries.",
        },
        new Entry
        {
            id = "CarPlayer", name = "Tocus City Car", type = "Car", price = 2800f, level = 5,
            speed = 12f, boost = 18f,
            description = "Quick and nimble city car. Stable at high speed on the long avenues.",
        },
        new Entry
        {
            id = "SportsPlayer", name = "Sports Coupe", type = "Car", price = 6000f, level = 8,
            speed = 14f, boost = 22f, template = "CarPlayer", model = "Vehicles/SportsCar", parkSlot = 2,
            // Model làm cả thân một vật liệu đục, kính là ô ĐEN trên texture (u 0.148-0.232, v 0.261-0.313).
            // Đọc Car4.fbx: khoang lái từ 19% đến 65% chiều dài (tính từ đuôi), nắp capo cao 71% -> kính = tam giác đen
            // trong vùng đó và cao hơn 72%; tách ra submesh kính trong suốt để nhìn được từ ghế lái như các xe khác.
            glassUv = new Vector4(0.14f, 0.25f, 0.24f, 0.32f), glassZ = new Vector2(0.17f, 0.66f), glassMinH = 0.72f,
            // Model không có nội thất (ngồi trong xe thấy cả lòng vỏ và lốp) -> góc thứ nhất đặt ngay trước chân kính chắn gió,
            // trên nắp capo ("hood cam" như game đua xe): nắp capo từ 65% chiều dài, cao 71% -> mắt ở 68% / 86%, giữa xe
            fpEye = new Vector3(0.5f, 0.86f, 0.68f),
            description = "The fastest car in the city. A reward for a successful store owner.",
        },
    };

    public static VehicleManager Instance { get; private set; }
    public static event Action OnChanged;

    private readonly HashSet<string> owned = new HashSet<string>();
    private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Pose> homes = new Dictionary<string, Pose>();
    private bool ready;

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
        if (Instance == null) new GameObject("VehicleManager").AddComponent<VehicleManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    void Awake()
    {
        Instance = this;
        foreach (Entry e in Catalog) if (e.starter) owned.Add(e.id);
        foreach (Entry e in Catalog) if (!string.IsNullOrEmpty(e.template)) BuildSkinnedVehicle(e);
        // Tìm xe ngay (trước khi giao thông nhân bản mẫu xe) và ghi lại chỗ đỗ ban đầu
        foreach (Entry e in Catalog)
        {
            GameObject go = GameObject.Find(e.id);
            if (go == null) continue;
            objects[e.id] = go;
            homes[e.id] = new Pose(go.transform.position, go.transform.rotation);
        }
    }

    IEnumerator Start()
    {
        yield return null; // đợi các hệ thống khác (giao thông...) khởi tạo xong
        ready = true;
        ApplyAll();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Tạo xe mới từ xe mẫu trong scene: nhân bản (giữ PlayerCarController, CarInteraction, camera xe, va chạm, đèn),
    /// ẩn vỏ cũ, gắn model mới (Resources/model) co giãn cho vừa khung xe mẫu. Đỗ nối đuôi xe mẫu dọc lề (không lấn ra làn xe).
    /// </summary>
    private static void BuildSkinnedVehicle(Entry e)
    {
        if (GameObject.Find(e.id) != null) return;
        GameObject template = GameObject.Find(e.template);
        GameObject model = Resources.Load<GameObject>(e.model);
        if (template == null || model == null) { Debug.LogWarning($"[Vehicles] Không tạo được {e.name}: thiếu xe mẫu '{e.template}' hoặc model 'Resources/{e.model}'."); return; }

        Transform t = template.transform;
        if (!ModelFit.MeshBounds(t, t, out Bounds body)) return;
        Vector3 pos; Quaternion rot;
        GameObject spot = GameObject.Find("ParkingSpot_" + e.id);
        if (spot != null) { pos = spot.transform.position; rot = Quaternion.Euler(0f, spot.transform.eulerAngles.y, 0f); }
        else
        {
            float len = body.size.z + 1.2f;                                   // chiều dài xe + 1.2 m khoảng hở
            pos = t.position + t.forward * (e.parkSlot % 2 == 1 ? -len : len) * ((e.parkSlot + 1) / 2);
            rot = t.rotation;
        }
        GameObject go = Instantiate(template, pos, rot);
        go.name = e.id;
        ModelFit.HideMeshes(go.transform);
        Transform skin = ModelFit.Attach(go.transform, model, body, e.modelYaw);
        if (!string.IsNullOrEmpty(e.plateTexture)) VehicleLivery.Swap(skin, "Car_Number_Mat", e.plateTexture);
        if (e.glassUv != Vector4.zero)
        {
            int n = VehicleGlass.Apply(skin, e.glassUv, e.glassZ, e.glassMinH);
            if (n == 0) Debug.LogWarning($"[Vehicles] {e.name}: không tách được kính (mesh cần bật Read/Write).");
        }
    }

    // ================= Tra cứu =================
    public static bool Owns(string id) => Instance != null && Instance.owned.Contains(id);

    public static Entry Get(string id)
    {
        foreach (Entry e in Catalog) if (e.id == id) return e;
        return null;
    }

    public static bool Exists(string id) => Instance != null && Instance.objects.ContainsKey(id) && Instance.objects[id] != null;

    /// <summary>Object của xe theo id, kể cả khi đang ẩn (xe chưa mua) - giao thông dùng làm mẫu.</summary>
    public static GameObject FindObject(string id)
    {
        if (Instance != null && Instance.objects.TryGetValue(id, out GameObject go) && go != null) return go;
        return GameObject.Find(id);
    }

    public static bool IsBeingDriven(string id)
    {
        if (Instance == null || !Instance.objects.TryGetValue(id, out GameObject go) || go == null) return false;
        PlayerCarController c = go.GetComponent<PlayerCarController>();
        return c != null && c.isDriving;
    }

    public enum BuyResult { Ok, AlreadyOwned, LevelTooLow, NotEnoughMoney, Missing }

    public static BuyResult CanBuy(Entry e)
    {
        if (Owns(e.id)) return BuyResult.AlreadyOwned;
        if (!Exists(e.id)) return BuyResult.Missing;
        if (StoreProgress.Level < e.level) return BuyResult.LevelTooLow;
        if (EconomyManager.Instance == null || !EconomyManager.Instance.CanAfford(e.price)) return BuyResult.NotEnoughMoney;
        return BuyResult.Ok;
    }

    // ================= Mua / gọi xe =================
    public static bool Buy(Entry e)
    {
        if (Instance == null || CanBuy(e) != BuyResult.Ok) return false;
        if (!EconomyManager.Instance.TrySpend(e.price, MoneyCategory.Upgrades, "Vehicle: " + e.name)) return false;
        Instance.owned.Add(e.id);
        Instance.ResetToHome(e.id);
        Instance.Apply(e);
        AudioManager.Play("cash_register");
        ProgressHUD.Toast($"You bought the {e.name}! It's parked outside the store - press [F] to ride.");
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Đưa xe về chỗ đỗ trước cửa hàng (xe bị lật, bỏ quên ở xa...). Không gọi được khi đang lái.</summary>
    public static bool BringToStore(Entry e)
    {
        if (Instance == null || !Owns(e.id) || IsBeingDriven(e.id)) return false;
        Instance.ResetToHome(e.id);
        ProgressHUD.Toast($"{e.name} is parked outside the store.");
        return true;
    }

    private void ResetToHome(string id)
    {
        if (!objects.TryGetValue(id, out GameObject go) || go == null || !homes.TryGetValue(id, out Pose home)) return;
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        go.transform.SetPositionAndRotation(home.position, home.rotation);
        if (rb != null) { rb.position = home.position; rb.rotation = home.rotation; }
    }

    // ================= Khoá / mở xe =================
    private void ApplyAll()
    {
        if (!ready) return;
        foreach (Entry e in Catalog) Apply(e);
    }

    private void Apply(Entry e)
    {
        if (!ready || !objects.TryGetValue(e.id, out GameObject go) || go == null) return;
        bool own = owned.Contains(e.id);

        PlayerCarController ctrl = go.GetComponent<PlayerCarController>();
        if (ctrl != null) { ctrl.normalSpeed = e.speed; ctrl.boostSpeed = e.boost; }

        foreach (MotorcycleInteraction m in go.GetComponents<MotorcycleInteraction>()) m.enabled = own;
        foreach (CarInteraction c in go.GetComponents<CarInteraction>()) c.enabled = own;

        // Xe chưa mua thì ẩn hẳn (không đỗ chắn đường); mua xong mới hiện ở chỗ đỗ. Xem / mua ở máy tính > Vehicles.
        VehicleForSale sign = go.GetComponent<VehicleForSale>();
        if (sign != null) Destroy(sign);
        if (go.activeSelf != own) go.SetActive(own);
    }

    /// <summary>
    /// Chỉ xe gần người chơi nhất (trong các xe đang cho lên) mới được phản hồi phím F,
    /// và không lên xe khi đang mở bảng giao diện.
    /// </summary>
    public static bool IsNearestMountable(Transform vehicle, Vector3 playerPos)
    {
        if (UIState.IsBlockingUIOpen) return false;
        float mine = (vehicle.position - playerPos).sqrMagnitude;
        foreach (MotorcycleInteraction m in FindObjectsByType<MotorcycleInteraction>(FindObjectsSortMode.None))
            if (m.enabled && m.transform != vehicle && (m.transform.position - playerPos).sqrMagnitude < mine) return false;
        foreach (CarInteraction c in FindObjectsByType<CarInteraction>(FindObjectsSortMode.None))
            if (c.enabled && c.transform != vehicle && (c.transform.position - playerPos).sqrMagnitude < mine) return false;
        return true;
    }

    // ================= Lưu / nạp =================
    public static List<string> Export() => Instance != null ? new List<string>(Instance.owned) : new List<string>();

    public static void Import(List<string> list)
    {
        if (Instance == null) CreateIfMissing();
        Instance.owned.Clear();
        foreach (Entry e in Catalog) if (e.starter) Instance.owned.Add(e.id);
        if (list != null) foreach (string id in list) if (!string.IsNullOrEmpty(id)) Instance.owned.Add(id);
        Instance.ApplyAll();
        OnChanged?.Invoke();
    }
}
