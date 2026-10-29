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
        if (Instance == null && FindFirstObjectByType<GameManager>() != null) new GameObject("WeatherSystem").AddComponent<WeatherSystem>();
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
            bool on = Time.time < flashUntil;
            flashLight.enabled = on;
            flashLight.intensity = on ? 2.2f : 0f;
        }
        else if (flashLight.enabled) flashLight.enabled = false;

        if (gm != null && hudText != null)
        {
            DateTime d = GameManager.DateOfDay(day);
            hudText.text = $"{d.ToString("ddd dd/MM/yyyy", CultureInfo.InvariantCulture)}  ·  {SeasonName(season)}  ·  {WeatherName(weather)} {temperature}°C";
        }
    }

    // ================= Tính thời tiết của ngày =================
    private void ApplyDay(int day)
    {
        appliedDay = day;
        DateTime date = GameManager.DateOfDay(day);
        season = SeasonOf(date.Month);
        var rng = new System.Random(day * 7919 + 17);
        weather = Roll(season, rng.NextDouble());
        if (forcedWeather >= 0) weather = (Weather)forcedWeather;

        int baseTemp = season == Season.Spring ? 24 : season == Season.Summer ? 33 : season == Season.Autumn ? 27 : 17;
        temperature = baseTemp + rng.Next(-3, 4) - (weather == Weather.Rain || weather == Weather.Storm ? 3 : 0);

        switch (weather)
        {
            case Weather.Sunny: cloudiness = 0f; rainIntensity = 0f; fogDensity = 0f; customerMul = 1.15f; break;
            case Weather.Cloudy: cloudiness = 0.45f; rainIntensity = 0f; fogDensity = 0.002f; customerMul = 1f; break;
            case Weather.Rain: cloudiness = 0.75f; rainIntensity = 0.55f; fogDensity = 0.006f; customerMul = 0.75f; break;
            case Weather.Storm: cloudiness = 0.95f; rainIntensity = 1f; fogDensity = 0.01f; customerMul = 0.55f; break;
            case Weather.Fog: cloudiness = 0.55f; rainIntensity = 0f; fogDensity = 0.022f; customerMul = 0.85f; break;
        }
        customerMul *= season == Season.Summer ? 1.1f : season == Season.Winter ? 0.9f : 1f;

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
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
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
                if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y < b.max.y)
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

        // Siêu thị chính: khung nhà x -127..-103, z 19..51; mái lấy theo vật thể cao nhất trong khung (tối đa 12 m)
        var store = new Bounds();
        store.SetMinMax(new Vector3(-127.5f, -5f, 18.5f), new Vector3(-102.5f, 7f, 51.5f));
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
        }
        indoor.Add(store);

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
        GameObject panel = UIKit.Panel("WeatherInfo", canvas.transform, new Vector2(400, 34), Vector2.zero, new Color(0f, 0f, 0f, 0.7f));
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -172f);
        hudText = UIKit.Text(panel.transform, "", 17, new Vector2(390, 32), Vector2.zero, TextAlignmentOptions.Center);
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
