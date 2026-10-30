using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Lưu / nạp game (giống Supermarket Simulator: tự lưu khi sang ngày mới).
/// Lưu: ngày, tiền, cấp cửa hàng, giấy phép, giá bán, giá thị trường, nhân viên, hàng trên từng kệ, nội thất đã mua (vị trí + hàng trên đó),
/// thùng hàng đang nằm trong cửa hàng, thùng nội thất chưa lắp.
/// Vào game: menu chính có nút Continue để nạp, cửa hàng bắt đầu ngày đã lưu lúc giờ mở cửa.
/// [F5] lưu nhanh (chỉ khi không còn khách trong cửa hàng) · [F9] nạp lại · File: Application.persistentDataPath/save.json
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    public static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    // ================= Dữ liệu =================
    [Serializable] public class PriceEntry { public string item; public float price; }
    [Serializable] public class ShelfEntry { public string path; public string name; public Vector3 pos; public string item; public int count; }
    [Serializable] public class FurnitureEntry { public string data; public Vector3 pos; public Quaternion rot; public string item; public int count; }
    [Serializable] public class BoxEntry { public string item; public int count; public Vector3 pos; public Quaternion rot; }
    [Serializable] public class FurnitureBoxEntry { public string data; public Vector3 pos; public Quaternion rot; }
    [Serializable] public class BuiltInEntry { public string id; public Vector3 pos; public Quaternion rot; }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAt;
        public int day;
        public float money;
        public int level;
        public int xp;
        public List<string> licenses = new List<string>();
        public List<PriceEntry> prices = new List<PriceEntry>();
        public List<ShelfEntry> shelves = new List<ShelfEntry>();
        public List<FurnitureEntry> furniture = new List<FurnitureEntry>();
        public List<BoxEntry> boxes = new List<BoxEntry>();
        public List<FurnitureBoxEntry> furnitureBoxes = new List<FurnitureBoxEntry>();
        public List<string> checkouts = new List<string>();
        public List<string> expansions = new List<string>();
        public int cashiers;
        public int restockers;
        public int guards;
        public List<BuiltInEntry> builtins = new List<BuiltInEntry>();
        public List<BusinessStats.DayRecord> stats = new List<BusinessStats.DayRecord>();
        public List<ReviewManager.Review> reviews = new List<ReviewManager.Review>();
        public List<MarketPrices.Entry> market = new List<MarketPrices.Entry>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Create();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Create();

    private static void Create()
    {
        if (Instance == null) new GameObject("SaveManager").AddComponent<SaveManager>();
    }

    void Awake()
    {
        Instance = this;
        GameManager.OnNewDay += OnNewDay;
    }

    void OnDestroy()
    {
        GameManager.OnNewDay -= OnNewDay;
        if (Instance == this) Instance = null;
    }

    // Nạp file lưu do menu chính quyết định (nút Continue), không tự nạp nữa


    void Update()
    {
        if (UIState.IsBlockingUIOpen) return;
        if (Input.GetKeyDown(KeyCode.F5)) TryManualSave();
        if (Input.GetKeyDown(KeyCode.F9) && File.Exists(SavePath)) Load();
    }

    private void OnNewDay(int day) => Save(); // tự lưu khi sang ngày mới

    /// <summary>
    /// Lưu tay (F5). Giống Supermarket Simulator: không lưu khi còn khách trong cửa hàng,
    /// vì hàng khách đã lấy khỏi kệ mà chưa trả tiền sẽ bị mất (khách không được lưu).
    /// </summary>
    public bool TryManualSave()
    {
        int customers = NPCShopper.Active.Count;
        if (customers > 0)
        {
            ProgressHUD.Toast($"Can't save while {customers} customer(s) are in the store.\nClose the store and serve them first.");
            Debug.Log($"[Save] Chưa lưu: còn {customers} khách trong cửa hàng.");
            return false;
        }
        Save();
        return true;
    }

    // ================= Lưu =================
    public void Save()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver) return;
        EconomyManager eco = EconomyManager.Instance;

        var d = new SaveData
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            day = gm != null ? gm.currentDay : 1,
            money = eco.Money,
            level = StoreProgress.Level,
            xp = StoreProgress.XP,
            licenses = LicenseManager.GetOwnedIds(),
            checkouts = LockedCheckout.GetUnlockedIds(),
            expansions = StoreExpansion.GetUnlockedIds(),
            cashiers = StaffManager.Instance != null ? StaffManager.Instance.CashierCount : 0,
            restockers = StaffManager.Instance != null ? StaffManager.Instance.RestockerCount : 0,
            guards = StaffManager.Instance != null ? StaffManager.Instance.GuardCount : 0,
            market = MarketPrices.Export(),
            stats = BusinessStats.Export(),
            reviews = ReviewManager.Export()
        };

        foreach (var kv in PriceManager.AllCustomPrices)
            if (kv.Key != null) d.prices.Add(new PriceEntry { item = kv.Key.name, price = kv.Value });

        foreach (Shelf shelf in FindObjectsByType<Shelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            PlacedFurniture pf = shelf.GetComponent<PlacedFurniture>();
            string item = shelf.Count > 0 && shelf.CurrentItem != null ? shelf.CurrentItem.name : "";
            if (pf != null)
            {
                if (pf.data == null) continue;
                d.furniture.Add(new FurnitureEntry { data = pf.data.name, pos = pf.transform.position, rot = pf.transform.rotation, item = item, count = shelf.Count });
            }
            else if (shelf.Count > 0)
            {
                d.shelves.Add(new ShelfEntry { path = PathOf(shelf.transform), name = shelf.name, pos = shelf.transform.position, item = item, count = shelf.Count });
            }
        }
        // Nội thất đã mua nhưng không có Shelf (phòng hờ)
        foreach (PlacedFurniture pf in FindObjectsByType<PlacedFurniture>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (pf.data != null && pf.GetComponent<Shelf>() == null)
                d.furniture.Add(new FurnitureEntry { data = pf.data.name, pos = pf.transform.position, rot = pf.transform.rotation });

        // Kệ có sẵn đã bị dời (người chơi tự sắp xếp, hoặc phần mở rộng dời ra tường mới)
        foreach (BuiltInFurniture b in BuiltInFurniture.All)
        {
            if (b == null) continue;
            Transform t = b.transform;
            if (Vector3.Distance(t.position, b.HomePosition) < 0.01f && Quaternion.Angle(t.rotation, b.HomeRotation) < 0.5f) continue;
            d.builtins.Add(new BuiltInEntry { id = b.Id, pos = t.position, rot = t.rotation });
        }

        foreach (Interactable box in StockManager.Boxes)
        {
            if (box == null || box.itemData == null || box.IsEmpty) continue;
            d.boxes.Add(new BoxEntry { item = box.itemData.name, count = box.productCount, pos = box.transform.position, rot = box.transform.rotation });
        }

        foreach (FurnitureBox fb in FindObjectsByType<FurnitureBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (fb.data != null)
                d.furnitureBoxes.Add(new FurnitureBoxEntry { data = fb.data.name, pos = fb.transform.position, rot = fb.transform.rotation });

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(d, true), Encoding.UTF8);
            Debug.Log($"[Save] Đã lưu ngày {d.day}: {EconomyManager.Format(d.money)}, {d.shelves.Count} kệ có hàng, {d.furniture.Count} nội thất, {d.boxes.Count} thùng -> {SavePath}");
            ProgressHUD.Toast("Game saved");
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] Không ghi được file lưu: " + e.Message);
        }
    }

    // ================= Nạp =================
    public void Load()
    {
        SaveData d;
        try { d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath, Encoding.UTF8)); }
        catch (Exception e) { Debug.LogError("[Save] File lưu bị hỏng: " + e.Message); return; }
        if (d == null) return;

        var items = Registry<ItemData>();
        var furnitures = Registry<FurnitureData>();
        int missing = 0;

        // Tiến trình
        EconomyManager.Instance.LoadState(d.money, d.day);
        if (GameManager.Instance != null) GameManager.Instance.LoadDay(d.day);
        StoreProgress.Load(d.level, d.xp);
        LicenseManager.LoadOwned(d.licenses);
        MarketPrices.Import(d.market, items);
        BusinessStats.Import(d.stats);
        ReviewManager.Import(d.reviews);

        PriceManager.ClearCustomPrices();
        foreach (PriceEntry p in d.prices)
            if (items.TryGetValue(p.item, out ItemData it)) PriceManager.SetSellPrice(it, p.price); else missing++;

        // Kệ có sẵn về chỗ cũ, mở lại phần mở rộng (dời kệ sát tường ra tường mới), rồi đặt kệ theo vị trí đã lưu
        foreach (BuiltInFurniture b in BuiltInFurniture.All)
            if (b != null) b.transform.SetPositionAndRotation(b.HomePosition, b.HomeRotation);
        // Phần mở rộng (nhà kho, phòng mới) mở lại trước, để kệ / thùng trong đó được tìm thấy
        StoreExpansion.LoadUnlocked(d.expansions);
        if (d.builtins != null)
            foreach (BuiltInEntry e in d.builtins)
            {
                BuiltInFurniture b = BuiltInFurniture.Find(e.id);
                if (b != null) b.transform.SetPositionAndRotation(e.pos, e.rot);
                else missing++;
            }

        // Xoá trạng thái hiện tại: khách, hàng trên kệ, nội thất đã mua, thùng hàng, thùng nội thất
        for (int i = NPCShopper.Active.Count - 1; i >= 0; i--)
            if (NPCShopper.Active[i] != null) Destroy(NPCShopper.Active[i].gameObject);
        foreach (PlacedFurniture pf in FindObjectsByType<PlacedFurniture>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(pf.gameObject);
        foreach (Shelf shelf in FindObjectsByType<Shelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (shelf.GetComponent<PlacedFurniture>() == null) shelf.ClearAll();
        foreach (Interactable box in new List<Interactable>(StockManager.Boxes))
            if (box != null && !box.IsHeld) Destroy(box.gameObject);
        foreach (FurnitureBox fb in FindObjectsByType<FurnitureBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(fb.gameObject);

        // Kệ có sẵn trong cửa hàng: nhận ra bằng VỊ TRÍ (đã đặt lại đúng chỗ đã lưu ở trên),
        // đường dẫn trong Hierarchy chỉ để dự phòng vì thứ tự object có thể đổi khi chơi
        var sceneShelves = new List<Shelf>();
        foreach (Shelf shelf in FindObjectsByType<Shelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (shelf.GetComponent<PlacedFurniture>() == null) sceneShelves.Add(shelf);
        var filled = new HashSet<Shelf>();
        foreach (ShelfEntry s in d.shelves)
        {
            Shelf target = null;
            float best = 0.2f;
            foreach (Shelf shelf in sceneShelves)
            {
                if (filled.Contains(shelf)) continue;
                float dist = Vector3.Distance(shelf.transform.position, s.pos);
                if (dist < best && (string.IsNullOrEmpty(s.name) || shelf.name == s.name)) { best = dist; target = shelf; }
            }
            if (target == null) target = sceneShelves.Find(sh => !filled.Contains(sh) && PathOf(sh.transform) == s.path);
            if (target != null && items.TryGetValue(s.item, out ItemData it))
            {
                target.FillInstant(it, s.count);
                filled.Add(target);
            }
            else
            {
                missing++;
                Debug.LogWarning($"[Save] Không tìm thấy kệ '{s.name}' tại {s.pos:F2} (hàng {s.item} x{s.count}).");
            }
        }

        // Nội thất đã mua
        foreach (FurnitureEntry f in d.furniture)
        {
            if (!furnitures.TryGetValue(f.data, out FurnitureData fd) || fd.prefab == null) { missing++; continue; }
            GameObject go = Instantiate(fd.prefab, f.pos, f.rot);
            go.name = fd.displayName;
            PlacedFurniture pf = go.GetComponent<PlacedFurniture>();
            if (pf == null) pf = go.AddComponent<PlacedFurniture>();
            pf.data = fd;
            if (!string.IsNullOrEmpty(f.item) && f.count > 0 && items.TryGetValue(f.item, out ItemData it))
            {
                Shelf shelf = go.GetComponent<Shelf>();
                if (shelf != null) shelf.FillInstant(it, f.count);
            }
        }

        // Thùng hàng
        foreach (BoxEntry b in d.boxes)
        {
            if (!items.TryGetValue(b.item, out ItemData it) || it.cratePrefab == null) { missing++; continue; }
            GameObject go = Instantiate(it.cratePrefab, b.pos + Vector3.up * 0.05f, b.rot);
            Interactable box = go.GetComponent<Interactable>();
            if (box == null) continue;
            box.SetItem(it, false);
            box.productCount = Mathf.Clamp(b.count, 0, box.Capacity);
            box.UpdateVisuals();
        }

        // Thùng nằm đúng chỗ trên kệ kho thì cất lại vào kệ (khoá vật lý, không rơi)
        foreach (StorageRack rack in StorageRack.All) rack.AdoptNearbyBoxes(StockManager.Boxes);

        foreach (FurnitureBoxEntry fbE in d.furnitureBoxes)
        {
            if (furnitures.TryGetValue(fbE.data, out FurnitureData fd)) FurnitureBox.Spawn(fd, fbE.pos - Vector3.up * 0.35f, fbE.rot);
            else missing++;
        }

        LockedCheckout.LoadUnlocked(d.checkouts); // mở lại các quầy đã mua TRƯỚC khi xếp thu ngân
        if (StaffManager.Instance != null)
        {
            StaffManager.Instance.LoadCounts(d.cashiers, d.restockers);
            StaffManager.Instance.LoadGuards(d.guards);
        }

        StockManager.NotifyChanged();
        Debug.Log($"[Save] Đã nạp ngày {d.day} (lưu lúc {d.savedAt}): {EconomyManager.Format(d.money)}, cấp {d.level}" +
                  (missing > 0 ? $", {missing} mục không tìm thấy (scene / dữ liệu đã đổi)." : "."));
        ProgressHUD.Toast($"Welcome back! Day {d.day}");
    }

    /// <summary>Xoá file lưu (bắt đầu game mới ở lần chơi sau).</summary>
    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        Debug.Log("[Save] Đã xoá file lưu.");
    }

    // ================= Tiện ích =================
    /// <summary>Đường dẫn ổn định của object trong scene (tên + thứ tự anh em ở mỗi cấp).</summary>
    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder();
        for (Transform c = t; c != null; c = c.parent)
            sb.Insert(0, "/" + c.name + "#" + c.GetSiblingIndex());
        return sb.ToString();
    }

    /// <summary>Tra asset theo tên (ItemData, FurnitureData đang được scene tham chiếu).</summary>
    private static Dictionary<string, T> Registry<T>() where T : ScriptableObject
    {
        var map = new Dictionary<string, T>();
        foreach (T a in Resources.FindObjectsOfTypeAll<T>())
            if (a != null && !map.ContainsKey(a.name)) map[a.name] = a;
        return map;
    }
}
