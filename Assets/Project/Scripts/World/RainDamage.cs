using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thùng hàng để ngoài trời lúc mưa / giông bị ướt (tự tạo khi vào game siêu thị).
///  - Ngoài mái che (WeatherSystem.IsSheltered), kể cả đang cầm trên tay: dầm mưa 30 giây thì thùng sẫm màu + cảnh báo,
///    từ giây thứ 60, cứ 15 giây có thể hỏng 1 món (giông nhanh gấp 1.5 lần).
///  - Đồ giấy / khô (bánh kẹo, ngũ cốc, bột, giấy...) dễ hỏng; đồ đóng chai, lon gần như không sao (Sensitivity).
///  - Hàng hỏng tính vào hao hụt trong báo cáo ngày, như hàng hết hạn.
///  - Chỉ khô dần khi đã ở dưới mái che hoặc hết mưa (chậm gấp 4 lần lúc ướt); nhặt lên thả lại ngoài mưa không làm thùng khô.
/// </summary>
public class RainDamage : MonoBehaviour
{
    public const float WetAfter = 30f;      // giây dầm mưa thì thùng ướt (đổi màu + cảnh báo)
    public const float DamageAfter = 60f;   // giây dầm mưa thì bắt đầu hỏng hàng
    public const float DamageEvery = 15f;   // mỗi bao lâu có thể hỏng thêm 1 món

    public static RainDamage Instance { get; private set; }

    private const float Tick = 0.5f;
    private float nextTick, nextToast;
    private readonly Dictionary<Interactable, float> wet = new Dictionary<Interactable, float>();
    private readonly Dictionary<Interactable, float> nextLoss = new Dictionary<Interactable, float>();
    private readonly List<Interactable> buffer = new List<Interactable>();
    private readonly List<Interactable> dry = new List<Interactable>();
    private static MaterialPropertyBlock mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Create();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Create();
    private static void Create() { if (Instance == null && SupermarketScene.IsActive) new GameObject("RainDamage").AddComponent<RainDamage>(); }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Mức dễ hỏng khi ướt theo nhóm hàng (0 = không sao, 1 = hỏng mỗi lần tới lượt).</summary>
    public static float Sensitivity(ProductGroup g) => g switch
    {
        ProductGroup.Drinks => 0.1f,
        ProductGroup.Alcohol => 0.1f,
        ProductGroup.Frozen => 0.4f,
        ProductGroup.Fresh => 0.6f,
        _ => 1f, // bánh kẹo, ngũ cốc, bột, cà phê, đồ gia dụng giấy, đồ cho thú cưng
    };

    void Update()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + Tick;
        bool raining = WeatherSystem.IsRaining;
        float rate = WeatherSystem.CurrentWeather == WeatherSystem.Weather.Storm ? 1.5f : 1f;
        int lostNow = 0;
        float lostValue = 0f;
        string firstName = null;
        bool newlyWet = false;

        buffer.Clear();
        buffer.AddRange(StockManager.Boxes); // hỏng hàng có thể báo thay đổi kho -> không duyệt trực tiếp danh sách gốc
        foreach (Interactable box in buffer)
        {
            if (box == null) continue;
            wet.TryGetValue(box, out float t);
            // Cầm thùng đi dưới mưa thì thùng vẫn ướt (lỗi cũ: nhặt lên thả lại là thùng khô ngay, không hỏng)
            bool exposed = raining && !box.IsEmpty && !WeatherSystem.IsSheltered(box.transform.position);
            float before = t;
            t = exposed ? t + Tick * rate : Mathf.Max(0f, t - Tick * 0.25f);
            if (t <= 0f) { if (before > 0f) dry.Add(box); continue; }
            wet[box] = t;

            if (before < WetAfter && t >= WetAfter) { Tint(box, true); newlyWet = true; }
            else if (before >= WetAfter * 0.5f && t < WetAfter * 0.5f) Tint(box, false); // khô hẳn mới hết sẫm màu

            if (!exposed || t < DamageAfter) continue;
            if (!nextLoss.TryGetValue(box, out float due)) { nextLoss[box] = t + DamageEvery; continue; }
            if (t < due) continue;
            nextLoss[box] = t + DamageEvery;
            ItemData item = box.itemData;
            float chance = item != null ? Sensitivity(DemandModel.GroupOf(item)) : 1f;
            if (Random.value > chance || !box.RemoveOne()) continue;
            lostNow++;
            lostValue += item != null ? item.unitPrice : 0f;
            if (firstName == null) firstName = box.DisplayName;
        }
        foreach (Interactable b in dry) { wet.Remove(b); nextLoss.Remove(b); if (b != null) Tint(b, false); }
        dry.Clear();
        if (wet.Count > 64) Cleanup();

        if (lostNow > 0)
        {
            EconomyManager eco = EconomyManager.Instance;
            if (eco != null && eco.Today != null) { eco.Today.spoiledItems += lostNow; eco.Today.spoiledValue += lostValue; }
            ProgressHUD.Toast($"Rain ruined {lostNow} item(s) ({firstName}). Carry the boxes inside!");
            nextToast = Time.time + 20f;
        }
        else if (newlyWet && Time.time >= nextToast)
        {
            ProgressHUD.Toast("Boxes outside are getting wet! Carry them inside before the goods are ruined.");
            nextToast = Time.time + 20f;
        }
    }

    /// <summary>Thùng đã bị ướt chưa (cho giao diện / test).</summary>
    public static bool IsWet(Interactable box) => Instance != null && box != null && Instance.wet.TryGetValue(box, out float t) && t >= WetAfter * 0.5f;

    private void Cleanup()
    {
        dry.Clear();
        foreach (var kv in wet) if (kv.Key == null) dry.Add(kv.Key);
        foreach (Interactable b in dry) { wet.Remove(b); nextLoss.Remove(b); }
        dry.Clear();
    }

    // Thùng ướt sẫm màu (chỉ vỏ thùng, không đổi màu hàng bên trong)
    private static void Tint(Interactable box, bool on)
    {
        if (box == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        var items = new HashSet<Transform>();
        if (box.visualItems != null) foreach (GameObject v in box.visualItems) if (v != null) items.Add(v.transform);
        foreach (Renderer r in box.GetComponentsInChildren<Renderer>(true))
        {
            if (IsUnder(r.transform, items, box.transform)) continue;
            if (!on) { r.SetPropertyBlock(null); continue; }
            Material m = r.sharedMaterial;
            if (m == null) continue;
            int id = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : -1;
            if (id < 0) continue;
            Color c = m.GetColor(id);
            mpb.Clear();
            mpb.SetColor(id, new Color(c.r * 0.55f, c.g * 0.5f, c.b * 0.45f, c.a));
            r.SetPropertyBlock(mpb);
        }
    }

    private static bool IsUnder(Transform t, HashSet<Transform> roots, Transform stop)
    {
        for (; t != null && t != stop; t = t.parent) if (roots.Contains(t)) return true;
        return false;
    }
}
