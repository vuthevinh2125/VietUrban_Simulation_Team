using System;
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
        if (day != appliedDay) ApplyDay(day);

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
