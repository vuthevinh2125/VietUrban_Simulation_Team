using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Trời sáng / tối theo đồng hồ trong game (GameManager): mặt trời mọc ~5h30, lặn trước giờ đóng cửa nửa tiếng (17h30 nếu đóng cửa 18h), tối hẳn sau đó ~1 tiếng.
/// Điều khiển "Directional Light" có sẵn trong scene (bật lên làm mặt trời), thêm ánh trăng xanh nhạt ban đêm,
/// đổi màu ánh sáng môi trường và độ sáng bầu trời. Đèn xe (CarLights) tự bật khi mặt trời xuống dưới chân trời.
/// Tự tạo khi vào game.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    public float sunriseHour = 5.5f;
    public float sunsetHour = 18.5f;
    public float sunMaxIntensity = 1.25f;
    public float moonIntensity = 0.18f;
    public float sunYaw = 35f;

    [Header("Ambient")]
    public Color daySky = new Color(0.62f, 0.68f, 0.78f);
    public Color dayEquator = new Color(0.52f, 0.54f, 0.56f);
    public Color dayGround = new Color(0.32f, 0.3f, 0.28f);
    public Color duskSky = new Color(0.55f, 0.38f, 0.32f);
    public Color nightSky = new Color(0.07f, 0.09f, 0.17f);
    public Color nightEquator = new Color(0.05f, 0.06f, 0.1f);
    public Color nightGround = new Color(0.03f, 0.03f, 0.05f);

    private Light sun, moon;
    private Material skybox;
    private bool hasExposure, hasTint;
    private float lastHour = -1f;

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
        if (Instance == null && FindFirstObjectByType<GameManager>() != null) new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
    }

    void Awake()
    {
        Instance = this;
        // Ngày làm việc ngắn (đóng cửa sớm) thì mặt trời lặn sớm hơn để giờ cuối ngày vẫn thấy trời chạng vạng / tối
        GameManager gm = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();
        if (gm != null) sunsetHour = Mathf.Clamp(gm.closingHour - 0.5f, 16.5f, 19f);
        sun = FindSun();
        if (sun != null)
        {
            sun.gameObject.SetActive(true);
            sun.enabled = true;
            sun.shadows = LightShadows.Soft;
        }

        var moonGo = new GameObject("MoonLight");
        moonGo.transform.SetParent(transform, false);
        moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.65f, 1f);
        moon.shadows = LightShadows.None;
        moon.intensity = 0f;

        if (RenderSettings.skybox != null)
        {
            skybox = new Material(RenderSettings.skybox); // bản riêng, không sửa asset gốc
            RenderSettings.skybox = skybox;
            hasExposure = skybox.HasProperty("_Exposure");
            hasTint = skybox.HasProperty("_SkyTint");
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        Apply(CurrentHour());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Đèn mặt trời: object "Directional Light" có sẵn (kể cả đang tắt), không có thì tạo mới.</summary>
    private static Light FindSun()
    {
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name == "Directional Light") return l;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name != "MoonLight") return l;
        var go = new GameObject("Directional Light");
        Light sunLight = go.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        return sunLight;
    }

    private static float CurrentHour()
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? gm.ClockHours : 12f;
    }

    void Update()
    {
        float h = CurrentHour();
        if (Mathf.Abs(h - lastHour) < 0.005f) return; // đồng hồ đứng (cửa hàng đóng) thì khỏi tính lại
        Apply(h);
    }

    private void Apply(float hour)
    {
        lastHour = hour;
        float dayLen = sunsetHour - sunriseHour;
        float t = (hour - sunriseHour) / dayLen;                 // 0 = mọc, 1 = lặn
        float elevation = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); // 0..1..0
        bool above = t > 0f && t < 1f;

        // Mặt trời: quay theo giờ (dưới chân trời khi đêm -> CarLights bật đèn xe)
        float sunPitch = Mathf.Lerp(-10f, 190f, (hour - (sunriseHour - 1f)) / (dayLen + 2f));
        if (hour < sunriseHour - 1f || hour > sunsetHour + 1f) sunPitch = -30f; // đêm: chiếu ngược lên
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
            float k = above ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elevation * 3f)) : 0f;
            sun.intensity = sunMaxIntensity * k;
            // Sáng/chiều vàng cam, trưa trắng
            sun.color = Color.Lerp(new Color(1f, 0.62f, 0.38f), new Color(1f, 0.97f, 0.92f), Mathf.Clamp01(elevation * 2f));
            sun.enabled = sun.intensity > 0.01f;
        }

        // Độ "đêm": 0 ban ngày, 1 đêm khuya (chuyển dần trong 1 giờ quanh lúc mọc / lặn)
        float night = 1f - Mathf.Clamp01(Mathf.Min(hour - (sunriseHour - 0.5f), (sunsetHour + 1f) - hour) / 1.5f);
        if (hour < sunriseHour - 0.5f || hour > sunsetHour + 1f) night = 1f;
        moon.intensity = moonIntensity * night;
        moon.enabled = moon.intensity > 0.01f;
        moon.transform.rotation = Quaternion.Euler(50f, sunYaw + 160f, 0f);
        RenderSettings.sun = night > 0.6f ? moon : sun;
        NightLight.SetAll(night > 0.45f); // đèn đường bật khi trời tối

        // Hoàng hôn: ánh sáng môi trường ngả cam
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(hour - sunsetHour) / 1.2f) + Mathf.Clamp01(1f - Mathf.Abs(hour - sunriseHour) / 1.0f);
        dusk = Mathf.Clamp01(dusk) * (1f - night);
        Color sky = Color.Lerp(Color.Lerp(daySky, duskSky, dusk), nightSky, night);
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = Color.Lerp(dayEquator, nightEquator, night);
        RenderSettings.ambientGroundColor = Color.Lerp(dayGround, nightGround, night);

        if (skybox != null)
        {
            if (hasExposure) skybox.SetFloat("_Exposure", Mathf.Lerp(1.25f, 0.12f, night));
            if (hasTint) skybox.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.75f, 0.45f, 0.35f), dusk));
        }
    }
}
