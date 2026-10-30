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
    }

    public static readonly Entry[] Catalog =
    {
        new Entry
        {
            id = "Honda Cub", name = "Honda Cub", type = "Motorbike", starter = true, price = 0f, level = 1,
            speed = 13f, boost = 20f,
            description = "Your trusty starter bike. Cheap, light and easy to park anywhere.",
        },
        new Entry
        {
            id = "C125 - PB", name = "Honda C125", type = "Motorbike", price = 1500f, level = 3,
            speed = 17f, boost = 27f,
            description = "Premium scooter with a stronger engine - finish deliveries before the timer runs out.",
        },
        new Entry
        {
            id = "CarPlayer", name = "Tocus City Car", type = "Car", price = 4500f, level = 5,
            speed = 20f, boost = 31f,
            description = "The fastest way across the city. Stable at high speed on the long avenues.",
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

    // ================= Tra cứu =================
    public static bool Owns(string id) => Instance != null && Instance.owned.Contains(id);

    public static bool Exists(string id) => Instance != null && Instance.objects.ContainsKey(id) && Instance.objects[id] != null;

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

        VehicleForSale sign = go.GetComponent<VehicleForSale>();
        if (!own && sign == null) sign = go.AddComponent<VehicleForSale>();
        if (sign != null)
        {
            if (own) Destroy(sign);
            else sign.Setup(e);
        }
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
