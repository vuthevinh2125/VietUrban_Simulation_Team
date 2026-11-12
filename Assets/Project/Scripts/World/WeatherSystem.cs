using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Thời tiết + 4 mùa theo lịch trong game (ngày 1 = 09/02/2026).
///  - Mùa: Xuân (tháng 2-4), Hạ (5-7), Thu (8-10), Đông (11-1). Mỗi mùa đổi màu cây cỏ (thu vàng cam, đông nhạt màu)
///    và nhiệt độ.
///  - Mỗi ngày một kiểu thời tiết (nắng / nhiều mây / mưa / giông / sương mù) tuỳ mùa, cố định theo số ngày
///    (nạp lại game vẫn giống). Mưa có hạt mưa rơi (dừng ở mái nhà), giông có chớp, sương mù giảm tầm nhìn.
///  - Ảnh hưởng: trời đẹp đông khách, mưa / giông vắng khách; giao hàng lúc mưa được tip nhiều hơn.
/// Tự tạo khi vào game. Góc trên trái hiện ngày tháng, mùa, thời tiết, nhiệt độ.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    public enum Season { Spring, Summer, Autumn, Winter }
    public enum Weather { Sunny, Cloudy, Rain, Storm, Fog }

    public static WeatherSystem Instance { get; private set; }
    public static Season CurrentSeason => Instance != null ? Instance.season : Season.Spring;
    public static Weather CurrentWeather => Instance != null ? Instance.weather : Weather.Sunny;
    /// <summary>0 = trời quang, 1 = mây đen kín.</summary>
    public static float Cloudiness => Instance != null ? Instance.cloudiness : 0f;
    /// <summary>Nhân với tốc độ khách tới (1 = bình thường).</summary>
    public static float CustomerMultiplier => Instance != null ? Instance.customerMul : 1f;
    public static float DeliveryTipMultiplier => Instance == null ? 1f : Instance.weather == Weather.Storm ? 2f : Instance.weather == Weather.Rain ? 1.5f : 1f;
    public static int TemperatureC => Instance != null ? Instance.temperature : 25;
    public static bool IsRaining => CurrentWeather == Weather.Rain || CurrentWeather == Weather.Storm;

    /// <summary>
    /// Điểm này có mái che thật không: nằm trong khung nhà (siêu thị, phòng mở rộng, nhà kho, hộp RainShelter) VÀ có
    /// mái / trần / mái hiên ngay phía trên. Khung nhà rộng hơn tường (gồm cả sân trước cửa) nên chỉ xét khung thì
    /// thùng hàng và nhân viên đứng ngoài sân vẫn bị coi là trong nhà (lỗi thùng không ướt, nhân viên đứng dưới mưa).
    /// </summary>
    public static bool IsSheltered(Vector3 p)
    {
        if (Instance == null) return true;
        if (Time.time - Instance.indoorBuiltAt > 15f) Instance.BuildIndoorVolumes();
        bool inside = false;
        foreach (Bounds b in Instance.indoor)
            if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y < b.max.y) { inside = true; break; }
        if (!inside) return false;
        float roof = Instance.RoofAbove(p);
        return roof > p.y + 1.5f; // không đo được mái (scene khác) -> RoofAbove trả về vô cực, theo khung như cũ
    }

    // Lưới mái che 0.5 m phủ các khung nhà: mỗi ô lưu đáy mái thấp nhất phía trên (vô cực = không có mái / chưa đo được)
    private const float RoofCell = 0.5f;
    private float[] roofGrid;
    private float gridX, gridZ;
    private int gridW, gridH;

    private float RoofAbove(Vector3 p)
    {
        if (roofGrid == null) return float.PositiveInfinity;
        int x = Mathf.FloorToInt((p.x - gridX) / RoofCell), z = Mathf.FloorToInt((p.z - gridZ) / RoofCell);
        if (x < 0 || z < 0 || x >= gridW || z >= gridH) return float.NegativeInfinity;
        return roofGrid[z * gridW + x];
    }

    private void BuildRoofGrid()
    {
        roofGrid = null;
        if (roofs.Count == 0 || indoor.Count == 0) return;
        Bounds all = indoor[0];
        foreach (Bounds b in indoor) all.Encapsulate(b);
        gridX = all.min.x; gridZ = all.min.z;
        gridW = Mathf.Clamp(Mathf.CeilToInt(all.size.x / RoofCell), 1, 1024);
        gridH = Mathf.Clamp(Mathf.CeilToInt(all.size.z / RoofCell), 1, 1024);
        roofGrid = new float[gridW * gridH];
        for (int i = 0; i < roofGrid.Length; i++) roofGrid[i] = float.NegativeInfinity;
        foreach (Bounds r in roofs)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((r.min.x - gridX) / RoofCell)), x1 = Mathf.Min(gridW - 1, Mathf.FloorToInt((r.max.x - gridX) / RoofCell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((r.min.z - gridZ) / RoofCell)), z1 = Mathf.Min(gridH - 1, Mathf.FloorToInt((r.max.z - gridZ) / RoofCell));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = z * gridW + x;
                    if (roofGrid[i] == float.NegativeInfinity || r.min.y < roofGrid[i]) roofGrid[i] = r.min.y;
                }
        }
    }

    // Mái / trần: các mảnh mô hình bắt đầu từ trên đầu người (đáy > 2.2 m, tường thì bắt đầu từ sàn nên bị loại) và đủ rộng (> 1 m2)
    private readonly List<Bounds> roofs = new List<Bounds>();
    private readonly List<Bounds> storeRoofs = new List<Bounds>();
    private bool storeRoofsKnown;

    private void CollectRoofs()
    {
        if (!storeRoofsKnown)
        {
            GameObject sm = GameObject.Find("Supermarket_full");
            if (sm != null) { AddRoofs(sm, storeRoofs); storeRoofsKnown = true; }
        }
        roofs.Clear();
        roofs.AddRange(storeRoofs);
        foreach (string name in new[] { "StoreExpansion_1", "StoreExpansion_2", "Warehouse" })
        {
            GameObject root = GameObject.Find(name);
            if (root != null) AddRoofs(root, roofs);
        }
    }

    private static void AddRoofs(GameObject root, List<Bounds> into)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = r.bounds;
            if (b.min.y > 2.2f && b.size.x * b.size.z > 1f) into.Add(b);
        }
    }
    public static event Action OnWeatherChanged;

    private Season season;
    private Weather weather;
    private float cloudiness, customerMul, rainIntensity, fogDensity;
    private int temperature, appliedDay = -1;

    private ParticleSystem rain;
    private ParticleSystem.Particle[] rainBuf;
    // Vùng trong nhà (cửa hàng, phần mở rộng, nhà kho): hạt mưa rơi vào dưới mái thì xoá ngay
    private readonly List<Bounds> indoor = new List<Bounds>();
    private float indoorBuiltAt = -999f;
    // Khung siêu thị mặc định (toạ độ thế giới) khi scene không có RainShelter nào
    public static readonly Vector3 DefaultStoreMin = new Vector3(-127.5f, -5f, 18.5f), DefaultStoreMax = new Vector3(-102.5f, 7f, 51.5f);
    private bool storeBoundsKnown;          // khung siêu thị chính chỉ cần đo một lần (quét hàng nghìn renderer)
    private Bounds storeBounds;
    private int hudDay = -1, hudTemp = int.MinValue; // chỉ dựng lại chữ HUD khi giá trị đổi (tránh tạo chuỗi mỗi frame)
    private Season hudSeason;
    private Weather hudWeather;
    private int forcedWeather = -1;
    private TMP_Text hudText;
    private float nextFlash, flashUntil;
    private Light flashLight;

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
        if (Instance == null && SupermarketScene.IsActive) new GameObject("WeatherSystem").AddComponent<WeatherSystem>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnWeatherChanged = null;

    void Awake()
    {
        Instance = this;
        BuildRain();
        BuildHud();
        var flashGo = new GameObject("LightningFlash");
        flashGo.transform.SetParent(transform, false);
        flashGo.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
        flashLight = flashGo.AddComponent<Light>();
        flashLight.type = LightType.Directional;
        flashLight.color = new Color(0.8f, 0.85f, 1f);
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
        flashLight.enabled = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        int day = gm != null ? gm.currentDay : 1;
        if (day != appliedDay) { forcedWeather = -1; ApplyDay(day); }

        // Phím test F6: đổi thời tiết lần lượt (chỉ Editor / Development Build)
        if (GameDebug.CheatsEnabled && Input.GetKeyDown(KeyCode.F6))
        {
            forcedWeather = ((int)weather + 1) % 5;
            ApplyDay(day);
        }

        // Hạt mưa đi theo camera đang dùng
        Camera cam = ActiveCamera();
        if (cam != null && rain != null) rain.transform.position = cam.transform.position + Vector3.up * 14f + cam.transform.forward * 6f;

        // Chớp khi giông
        if (weather == Weather.Storm)
        {
            if (Time.time >= nextFlash)
            {
                nextFlash = Time.time + UnityEngine.Random.Range(6f, 16f);
                flashUntil = Time.time + 0.18f;
            }
            // Đèn chớp luôn BẬT suốt cơn giông, chỉ đổi cường độ: bật / tắt một đèn directional làm đổi số đèn
            // trong cảnh -> URP đổi biến thể shader mỗi lần chớp (gây khựng). Đổi cường độ thì không.
            bool on = Time.time < flashUntil;
            if (!flashLight.enabled) flashLight.enabled = true;
            flashLight.intensity = on ? 2.2f : 0f;
        }
        else if (flashLight.enabled) flashLight.enabled = false;

        if (gm != null && hudText != null && (day != hudDay || season != hudSeason || weather != hudWeather || Mathf.RoundToInt(temperature) != hudTemp))
        {
            hudDay = day; hudSeason = season; hudWeather = weather; hudTemp = Mathf.RoundToInt(temperature);
            DateTime d = GameManager.DateOfDay(day);
            hudText.text = $"{d.ToString("ddd dd/MM/yyyy", CultureInfo.InvariantCulture)}  ·  {SeasonName(season)}  ·  {WeatherName(weather)} {temperature}°C";
        }
    }

    // ================= Tính thời tiết của ngày =================
    /// <summary>Đặt thời tiết cho hôm nay (như phím F6; dùng cho chế độ đo hiệu năng cố định).</summary>
    public static void ForceWeather(Weather w)
    {
        if (Instance == null) return;
        Instance.forcedWeather = (int)w;
        GameManager gm = GameManager.Instance;
        Instance.ApplyDay(gm != null ? gm.currentDay : Mathf.Max(1, Instance.appliedDay));
    }

    // ================= Dự báo thời tiết (dùng cho gợi ý nhập hàng) =================
    /// <summary>Thời tiết một ngày: loại, mùa, nhiệt độ.</summary>
    public struct DayWeather
    {
        public Weather weather;
        public Season season;
        public int tempC;
        public override string ToString() => $"{WeatherName(weather)} {tempC}°C";
    }

    /// <summary>Hệ số số khách theo thời tiết và mùa (hàm thuần; ApplyDay và mô phỏng đánh giá dự báo dùng chung).</summary>
    public static float CustomerMultiplierFor(Weather w, Season s)
    {
        float m = w switch { Weather.Sunny => 1.15f, Weather.Cloudy => 1f, Weather.Rain => 0.75f, Weather.Storm => 0.55f, _ => 0.85f };
        return m * (s == Season.Summer ? 1.1f : s == Season.Winter ? 0.9f : 1f);
    }

    /// <summary>Độ chính xác của bản tin dự báo (xác suất đoán đúng loại thời tiết của ngày mai).</summary>
    public const float ForecastAccuracy = 0.8f;

    /// <summary>
    /// Thời tiết THẬT của một ngày. Hàm thuần: cùng ngày luôn ra cùng kết quả (hạt giống theo số ngày), không đọc
    /// trạng thái game -> ApplyDay dùng chung và test EditMode kiểm tra được.
    /// </summary>
    public static DayWeather Actual(int day, int forced = -1)
    {
        DateTime date = GameManager.DateOfDay(day);
        Season s = SeasonOf(date.Month);
        var rng = new System.Random(day * 7919 + 17);
        Weather w = Roll(s, rng.NextDouble());
        if (forced >= 0) w = (Weather)forced;
        int baseTemp = s == Season.Spring ? 24 : s == Season.Summer ? 33 : s == Season.Autumn ? 27 : 17;
        int t = baseTemp + rng.Next(-3, 4) - (w == Weather.Rain || w == Weather.Storm ? 3 : 0);
        return new DayWeather { weather = w, season = s, tempC = t };
    }

    /// <summary>
    /// Bản tin dự báo cho một ngày: như ngoài đời, đúng khoảng 80% (ForecastAccuracy). Sai thì ra một loại thời tiết
    /// khác theo xác suất của mùa đó; nhiệt độ lệch ±2°C. Hạt giống riêng -> dự báo cố định, xem lại không đổi.
    /// Gợi ý nhập hàng dựa trên bản tin này, nên sai số dự báo thời tiết là một phần sai số của gợi ý (đo được).
    /// </summary>
    public static DayWeather Forecast(int day)
    {
        DayWeather real = Actual(day);
        var rng = new System.Random(day * 104729 + 3);
        DayWeather f = real;
        if (rng.NextDouble() >= ForecastAccuracy)
        {
            for (int i = 0; i < 8 && f.weather == real.weather; i++) f.weather = Roll(real.season, rng.NextDouble());
            if (f.weather == real.weather) f.weather = real.weather == Weather.Sunny ? Weather.Cloudy : Weather.Sunny;
            bool wasWet = real.weather == Weather.Rain || real.weather == Weather.Storm;
            bool isWet = f.weather == Weather.Rain || f.weather == Weather.Storm;
            f.tempC += (wasWet ? 3 : 0) - (isWet ? 3 : 0);
        }
        f.tempC += rng.Next(-2, 3);
        return f;
    }

    private void ApplyDay(int day)
    {
        appliedDay = day;
        DateTime date = GameManager.DateOfDay(day);
        DayWeather today = Actual(day, forcedWeather);
        season = today.season;
        weather = today.weather;
        temperature = today.tempC;

        switch (weather)
        {
            case Weather.Sunny: cloudiness = 0f; rainIntensity = 0f; fogDensity = 0f; customerMul = 1.15f; break;
            case Weather.Cloudy: cloudiness = 0.45f; rainIntensity = 0f; fogDensity = 0.002f; customerMul = 1f; break;
            case Weather.Rain: cloudiness = 0.75f; rainIntensity = 0.55f; fogDensity = 0.006f; customerMul = 0.75f; break;
            case Weather.Storm: cloudiness = 0.95f; rainIntensity = 1f; fogDensity = 0.01f; customerMul = 0.55f; break;
            case Weather.Fog: cloudiness = 0.55f; rainIntensity = 0f; fogDensity = 0.022f; customerMul = 0.85f; break;
        }
        customerMul = CustomerMultiplierFor(weather, season);

        // Mưa
        var emission = rain.emission;
        emission.rateOverTime = 2200f * rainIntensity;
        if (rainIntensity > 0f) { if (!rain.isPlaying) rain.Play(); }
        else rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Sương mù
        RenderSettings.fog = fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = weather == Weather.Fog ? new Color(0.72f, 0.74f, 0.76f) : new Color(0.45f, 0.48f, 0.52f);

        ApplySeasonColors();
        if (DayNightCycle.Instance != null) DayNightCycle.Instance.Refresh();
        Debug.Log($"[Weather] Ngày {day} ({date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}): {season}, {weather}, {temperature}°C.");
        OnWeatherChanged?.Invoke();
    }

    private static Season SeasonOf(int month) =>
        month >= 2 && month <= 4 ? Season.Spring : month >= 5 && month <= 7 ? Season.Summer : month >= 8 && month <= 10 ? Season.Autumn : Season.Winter;

    private static Weather Roll(Season s, double r)
    {
        // xác suất cộng dồn: nắng, mây, mưa, giông, sương
        double[] p = s switch
        {
            Season.Spring => new[] { 0.45, 0.30, 0.17, 0.03, 0.05 },
            Season.Summer => new[] { 0.45, 0.15, 0.25, 0.15, 0.00 },
            Season.Autumn => new[] { 0.50, 0.28, 0.15, 0.05, 0.02 },
            _ => new[] { 0.30, 0.35, 0.13, 0.02, 0.20 },
        };
        double acc = 0;
        for (int i = 0; i < p.Length; i++) { acc += p[i]; if (r < acc) return (Weather)i; }
        return Weather.Sunny;
    }

    // ================= Màu cây cỏ theo mùa =================
    private MaterialPropertyBlock mpb;

    private void ApplySeasonColors()
    {
        Color leaf = season switch
        {
            Season.Spring => new Color(0.82f, 1.05f, 0.78f),
            Season.Summer => Color.white,
            Season.Autumn => new Color(1.3f, 0.72f, 0.32f),
            _ => new Color(0.82f, 0.86f, 0.9f),
        };
        Color ground = season switch
        {
            Season.Spring => new Color(0.38f, 0.5f, 0.3f),
            Season.Summer => new Color(0.36f, 0.45f, 0.3f),
            Season.Autumn => new Color(0.5f, 0.45f, 0.28f),
            _ => new Color(0.55f, 0.58f, 0.56f),
        };
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (Renderer r in FindObjectsByType<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            string n = r.name.ToLowerInvariant();
            bool plant = n.Contains("tree") || n.Contains("bush") || n.Contains("hedge") || n.Contains("grass") || n.Contains("flower") || n.Contains("plant");
            bool isGround = r.name == "Ground" && r.transform.parent != null && r.transform.parent.name.StartsWith("CityExpansion");
            if (!plant && !isGround) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", isGround ? ground : leaf);
            mpb.SetColor("_Color", isGround ? ground : leaf);
            r.SetPropertyBlock(mpb);
        }
    }

    // ================= Chặn mưa trong nhà =================
    void LateUpdate()
    {
        if (rain == null || !rain.isPlaying) return;
        if (Time.time - indoorBuiltAt > 15f) BuildIndoorVolumes();
        if (indoor.Count == 0) return;
        int max = rain.main.maxParticles;
        if (rainBuf == null || rainBuf.Length < max) rainBuf = new ParticleSystem.Particle[max];
        int n = rain.GetParticles(rainBuf);
        bool changed = false;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = rainBuf[i].position;
            for (int k = 0; k < indoor.Count; k++)
            {
                Bounds b = indoor[k];
                if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y < b.max.y && p.y < RoofAbove(p))
                {
                    rainBuf[i].remainingLifetime = 0f;
                    changed = true;
                    break;
                }
            }
        }
        if (changed) rain.SetParticles(rainBuf, n);
    }

    private void BuildIndoorVolumes()
    {
        indoorBuiltAt = Time.time;
        indoor.Clear();
        CollectRoofs();

        // Có hộp RainShelter đặt trong scene thì dùng chúng thay cho khung siêu thị mặc định bên dưới
        if (RainShelter.All.Count > 0)
        {
            foreach (RainShelter s in RainShelter.All) indoor.Add(s.WorldBounds);
            AddExpansionVolumes();
            return;
        }

        // Siêu thị chính: khung nhà x -127..-103, z 19..51; mái lấy theo vật thể cao nhất trong khung (tối đa 12 m).
        // Toà nhà không đổi trong lúc chơi nên chỉ đo một lần; phòng mở rộng / nhà kho thì đo lại vì có thể vừa mở khoá.
        if (storeBoundsKnown) { indoor.Add(storeBounds); AddExpansionVolumes(); return; }
        var store = new Bounds();
        store.SetMinMax(DefaultStoreMin, DefaultStoreMax);
        GameObject sm = GameObject.Find("Supermarket_full");
        if (sm != null)
        {
            float top = 0f;
            foreach (Renderer r in sm.GetComponentsInChildren<Renderer>())
            {
                Vector3 c = r.bounds.center;
                if (c.x > store.min.x && c.x < store.max.x && c.z > store.min.z && c.z < store.max.z && r.bounds.max.y < 12f)
                    top = Mathf.Max(top, r.bounds.max.y);
            }
            if (top > 2f) store.SetMinMax(store.min, new Vector3(store.max.x, top + 0.3f, store.max.z));
            storeBounds = store;
            storeBoundsKnown = true;
        }
        indoor.Add(store);
        AddExpansionVolumes();
    }

    private void AddExpansionVolumes()
    {
        // Phòng mở rộng / nhà kho dựng bằng tool: lấy khung theo sàn + trần
        foreach (string name in new[] { "StoreExpansion_1", "StoreExpansion_2", "Warehouse" })
        {
            GameObject root = GameObject.Find(name);
            if (root == null) continue;
            bool has = false;
            var b = new Bounds();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Ceiling", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) continue;
            b.SetMinMax(new Vector3(b.min.x - 0.3f, b.min.y - 5f, b.min.z - 0.3f), new Vector3(b.max.x + 0.3f, Mathf.Max(b.max.y, b.min.y + 3f) + 0.3f, b.max.z + 0.3f));
            indoor.Add(b);
        }
        BuildRoofGrid(); // mọi nhánh của BuildIndoorVolumes đều kết thúc ở đây
    }

    // ================= Dựng hạt mưa / HUD =================
    private void BuildRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = rain.main;
        main.loop = true;
        main.startLifetime = 1.3f;
        main.startSpeed = 0f;
        main.startSize = 0.035f;
        main.maxParticles = 6000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.45f);
        main.gravityModifier = 0f;

        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46f, 1f, 46f);

        var vel = rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(-20f);
        vel.z = new ParticleSystem.MinMaxCurve(0.3f);

        // Hạt mưa chạm mái nhà / mặt đất thì biến mất (không mưa trong nhà)
        var col = rain.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision3D;
        col.quality = ParticleSystemCollisionQuality.Medium;
        col.lifetimeLoss = 1f;
        col.radiusScale = 0.5f;

        var emission = rain.emission;
        emission.rateOverTime = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 6f;
        r.velocityScale = 0.02f;
        r.sharedMaterial = RuntimeMaterials.Ghost(new Color(0.75f, 0.82f, 0.95f, 0.45f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void BuildHud()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, 50);
        GameObject panel = UIKit.Panel("WeatherInfo", canvas.transform, new Vector2(300, 30), Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -126f); // ngay dưới khung ngày / giờ / tiền (rộng 300, x 10)
        hudText = UIKit.Text(panel.transform, "", 16, new Vector2(284, 28), Vector2.zero, TextAlignmentOptions.Left);
        hudText.enableAutoSizing = true; hudText.fontSizeMin = 11; hudText.fontSizeMax = 16;
        hudText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static Camera ActiveCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled) return main;
        foreach (Camera c in Camera.allCameras) if (c != null && c.isActiveAndEnabled && c.targetTexture == null) return c;
        return null;
    }

    public static string SeasonName(Season s) => s switch { Season.Spring => "Spring", Season.Summer => "Summer", Season.Autumn => "Autumn", _ => "Winter" };
    public static string WeatherName(Weather w) => w switch { Weather.Sunny => "Sunny", Weather.Cloudy => "Cloudy", Weather.Rain => "Rain", Weather.Storm => "Storm", _ => "Fog" };
}
