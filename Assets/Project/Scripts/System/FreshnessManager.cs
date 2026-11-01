using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Theo dõi hạn dùng hàng tươi trên kệ (tự tạo khi vào game).
///  - Mỗi giây kiểm tra các kệ đang bày hàng tươi (chỉ kệ Perishable, đồ khô bỏ qua): món quá hạn đổi màu,
///    khách không mua, nhìn thấy thì trừ điểm "sạch sẽ" khi đánh giá.
///  - Món hỏng được tính là hao hụt (theo giá nhập) vào báo cáo ngày.
///  - Vứt bỏ: người chơi nhìn vào kệ bấm [R], nhân viên lau nhà tự dọn, hoặc nhân viên đóng cửa dọn hết lúc cuối ngày.
/// </summary>
public class FreshnessManager : MonoBehaviour
{
    public static FreshnessManager Instance { get; private set; }

    public float checkInterval = 1f;
    private float nextCheck;
    private float nextToast;
    private readonly List<Shelf> buffer = new List<Shelf>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Create();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Create();
    private static void Create() { if (Instance == null) new GameObject("FreshnessManager").AddComponent<FreshnessManager>(); }

    void Awake()
    {
        Instance = this;
        GameManager.OnStoreOpenChanged += OnStoreOpenChanged;
    }

    void OnDestroy()
    {
        GameManager.OnStoreOpenChanged -= OnStoreOpenChanged;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Thử nghiệm: F10 làm hàng tươi trên mọi kệ già thêm 1 ngày (chỉ bản Editor / Development)
        if (GameDebug.CheatsEnabled && Input.GetKeyDown(KeyCode.F10))
        {
            foreach (Shelf s in StockManager.Shelves) if (s != null && s.Perishable) s.DebugAge(1f);
            ProgressHUD.Toast("[Debug] Fresh food aged by 1 day.");
            nextCheck = 0f;
        }
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        double now = Freshness.Now;
        string firstName = null;
        int spoiledNow = 0;
        float value = 0f;
        buffer.Clear();
        buffer.AddRange(StockManager.Shelves); // UpdateFreshness có thể báo thay đổi kho -> không duyệt trực tiếp danh sách gốc
        foreach (Shelf s in buffer)
        {
            if (s == null || !s.Perishable) continue;
            ItemData item = s.CurrentItem;
            int n = s.UpdateFreshness(now);
            if (n <= 0) continue;
            spoiledNow += n;
            value += n * (item != null ? item.unitPrice : 0f);
            if (firstName == null && item != null) firstName = item.itemName;
        }
        if (spoiledNow == 0) return;

        RecordWaste(spoiledNow, value);
        if (Time.time >= nextToast)
        {
            nextToast = Time.time + 20f;
            ProgressHUD.Toast($"{spoiledNow} item(s) went bad ({firstName}{(spoiledNow > 1 ? "..." : "")}). Look at the shelf and press [R] to throw them away.");
        }
    }

    /// <summary>Đóng cửa: nhân viên dọn hết hàng hỏng trên kệ.</summary>
    private void OnStoreOpenChanged(bool open)
    {
        if (open) return;
        int n = 0;
        buffer.Clear();
        buffer.AddRange(StockManager.Shelves);
        foreach (Shelf s in buffer) if (s != null) n += s.RemoveSpoiled();
        if (n > 0) ProgressHUD.Toast($"Closing time: {n} expired item(s) were thrown away.");
    }

    private static void RecordWaste(int items, float value)
    {
        EconomyManager eco = EconomyManager.Instance;
        if (eco == null || eco.Today == null) return;
        eco.Today.spoiledItems += items;
        eco.Today.spoiledValue += value;
    }

    /// <summary>Người chơi / nhân viên vứt hàng hỏng của một kệ.</summary>
    public static int ThrowAway(Shelf shelf)
    {
        if (shelf == null) return 0;
        int n = shelf.RemoveSpoiled();
        if (n > 0) AudioManager.PlayAt("place_item", shelf.transform.position, 0.6f);
        return n;
    }
}
