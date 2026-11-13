using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Không khí ma mị khi trời tối hẳn (từ ~1 giờ sau hoàng hôn), chỉ khi người chơi ở ngoài trời (tự tạo khi vào game):
///  - Thỉnh thoảng (25-60 s) có tiếng chó tru hoặc cú kêu vọng lại từ xa (âm 3D, hướng ngẫu nhiên), lâu lâu có cơn gió rít.
///  - Đèn đường quanh người chơi chập chờn: mỗi vài giây một cột đèn trong bán kính 45 m nháy tắt / sáng chập chờn 1-2 s.
/// Tắt trong lúc chiếu đoạn phim. Âm thanh tạo dần từ chiều tối (HorrorAudio.Prewarm) để không khựng game.
/// </summary>
public class NightAmbience : MonoBehaviour
{
    public static NightAmbience Instance { get; private set; }

    public Vector2 soundInterval = new Vector2(25f, 60f);
    public Vector2 flickerInterval = new Vector2(3f, 8f);
    public float flickerRadius = 45f;

    private float nextSound, nextFlicker, nextPrewarm;
    private bool prewarmed;
    private AudioSource wind;

    private class Flicker { public Light l; public float baseI, until, next; public bool on = true; }
    private readonly List<Flicker> flickers = new List<Flicker>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Create();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Create();
    private static void Create() { if (Instance == null && SupermarketScene.IsActive) new GameObject("NightAmbience").AddComponent<NightAmbience>(); }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; StopFlickers(); }

    /// <summary>Đã tối hẳn chưa (dùng chung cho các sự kiện kinh dị).</summary>
    public static bool DeepNight
    {
        get
        {
            float h = DayNightCycle.DebugHourOverride >= 0f ? DayNightCycle.DebugHourOverride
                    : GameManager.Instance != null ? GameManager.Instance.ClockHours : 12f;
            float sunset = DayNightCycle.Instance != null ? DayNightCycle.Instance.sunsetHour : 18.5f;
            return h >= sunset + 0.9f || h < 4.5f;
        }
    }

    private static bool Evening
    {
        get
        {
            float h = GameManager.Instance != null ? GameManager.Instance.ClockHours : 12f;
            float sunset = DayNightCycle.Instance != null ? DayNightCycle.Instance.sunsetHour : 18.5f;
            return h >= sunset - 1.5f;
        }
    }

    void Update()
    {
        // Tạo dần âm thanh từ chiều tối, mỗi 0.4 s một clip
        if (!prewarmed && Evening && Time.unscaledTime >= nextPrewarm)
        {
            nextPrewarm = Time.unscaledTime + 0.4f;
            prewarmed = HorrorAudio.Prewarm();
        }

        Camera cam = Camera.main;
        bool active = DeepNight && cam != null && !NightRobbery.Playing && !StoreEvents.CutsceneRunning && !IntroCutscene.Playing
                      && Time.timeScale > 0f && !WeatherSystem.IsSheltered(cam.transform.position);
        UpdateFlickers(active);
        if (wind != null) wind.volume = Mathf.MoveTowards(wind.volume, active ? 0.12f : 0f, Time.unscaledDeltaTime * 0.05f);
        if (!active) { nextSound = Mathf.Max(nextSound, Time.time + 8f); return; }

        Vector3 p = cam.transform.position;
        if (Time.time >= nextSound)
        {
            nextSound = Time.time + Random.Range(soundInterval.x, soundInterval.y);
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            float r = Random.value;
            if (r < 0.45f) HorrorAudio.Play3D(HorrorAudio.DogHowl(), p + dir * Random.Range(35f, 70f) + Vector3.up * 2f, 0.55f, false, 20f, 160f);
            else if (r < 0.85f) HorrorAudio.Play3D(HorrorAudio.Owl(), p + dir * Random.Range(18f, 35f) + Vector3.up * 8f, 0.45f, false, 12f, 90f);
            else if (wind == null) wind = HorrorAudio.Play2D(HorrorAudio.Wind(), 0f, true);
        }
        if (Time.time >= nextFlicker)
        {
            nextFlicker = Time.time + Random.Range(flickerInterval.x, flickerInterval.y);
            StartFlicker(p);
        }
    }

    // ================= Đèn đường chập chờn =================
    private void StartFlicker(Vector3 near)
    {
        var cands = new List<Light>();
        foreach (NightLight nl in NightLight.All)
        {
            if (nl == null) continue;
            Light l = nl.GetComponent<Light>();
            if (l == null || !l.enabled) continue;
            if ((l.transform.position - near).sqrMagnitude > flickerRadius * flickerRadius) continue;
            bool busy = false;
            foreach (Flicker f in flickers) if (f.l == l) { busy = true; break; }
            if (!busy) cands.Add(l);
        }
        if (cands.Count == 0) return;
        Light pick = cands[Random.Range(0, cands.Count)];
        flickers.Add(new Flicker { l = pick, baseI = pick.intensity, until = Time.time + Random.Range(1f, 2.2f), next = 0f });
    }

    private void UpdateFlickers(bool active)
    {
        for (int i = flickers.Count - 1; i >= 0; i--)
        {
            Flicker f = flickers[i];
            if (f.l == null) { flickers.RemoveAt(i); continue; }
            if (!active || Time.time >= f.until)
            {
                f.l.intensity = f.baseI;
                flickers.RemoveAt(i);
                continue;
            }
            if (Time.time >= f.next)
            {
                f.on = !f.on;
                f.next = Time.time + (f.on ? Random.Range(0.05f, 0.35f) : Random.Range(0.03f, 0.18f));
                f.l.intensity = f.on ? f.baseI * Random.Range(0.6f, 1f) : f.baseI * 0.05f;
            }
        }
    }

    private void StopFlickers()
    {
        foreach (Flicker f in flickers) if (f.l != null) f.l.intensity = f.baseI;
        flickers.Clear();
    }
}
