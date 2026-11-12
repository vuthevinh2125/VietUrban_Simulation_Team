using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Âm thanh của game (tự tạo, không cần đặt vào scene). File lấy từ Resources/Audio theo tên:
/// scan_beep, cash_register, coin, card_beep, door_slide, box_pickup, place_item, ui_click, level_up, store_music (thu âm có sẵn);
/// rain_loop, storm_loop, thunder_1-3, city_loop, crowd_loop, horn_1-2, footstep_1-4, box_drop, bat_swing, bat_hit, sweep,
/// wet_squish (tổng hợp bằng code: Tools/AudioGen/generate_sounds.py).
/// Thiếu file nào thì bỏ qua file đó. Âm lượng tổng chỉnh trong Settings (AudioListener.volume).
/// Âm nền tự đổi theo chỗ người chơi đứng: ngoài trời nghe mưa / phố to, trong cửa hàng nghe nhỏ lại và nghe tiếng
/// khách xì xào theo số khách; ngoài trời thỉnh thoảng có còi xe máy / ô tô ở xa.
/// Nhạc nền như loa trong siêu thị: 5 bài (store_music thu sẵn + 4 bài tổng hợp lo-fi, bossa nova, jazz đêm, pop),
/// người chơi chọn trong Settings (ngẫu nhiên / một bài / tắt, âm lượng nhạc); trong cửa hàng nghe rõ, ra ngoài phố còn 1/4.
///  - AudioManager.Play("tên")            : âm thanh 2D (giao diện, tay người chơi)
///  - AudioManager.PlayAt("tên", vị trí)  : âm thanh 3D trong cửa hàng (quầy, cửa...)
/// Tự phát tiếng click khi bấm bất kỳ nút giao diện nào, và nhạc nền lặp lại.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Range(0f, 1f)] public float musicVolume = 0.38f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> missing = new HashSet<string>();
    private AudioSource sfx2D, music;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    // Âm nền lặp (mưa, phố, tiếng người): mỗi tên một nguồn, âm lượng trượt dần tới mức đích
    private readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, float> loopTarget = new Dictionary<string, float>();
    private AudioListener listener;
    private float nextAmbience, nextHorn;
    private bool listenerIndoors = true;
    /// <summary>Tên hiển thị của các bài nhạc nền, theo thứ tự file store_music, store_music_2..5.</summary>
    public static readonly string[] TrackNames = { "Classic", "Lo-fi", "Bossa Nova", "Night Jazz", "Sunny Pop" };
    private static readonly string[] TrackFiles = { "store_music", "store_music_2", "store_music_3", "store_music_4", "store_music_5" };
    private readonly AudioClip[] tracks = new AudioClip[5];
    private int track = -1;
    private float musicLevel = 1f; // 1 trong cửa hàng, 0.25 ngoài phố (trượt dần)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Create();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Create();

    private static void Create()
    {
        if (Instance == null && SupermarketScene.IsActive) new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    void Awake()
    {
        Instance = this;
        sfx2D = gameObject.AddComponent<AudioSource>();
        sfx2D.playOnAwake = false;
        sfx2D.spatialBlend = 0f;
        sfx2D.ignoreListenerPause = true; // tiếng click vẫn kêu khi đang tạm dừng

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = musicVolume;
        music.ignoreListenerPause = true;
        for (int i = 0; i < TrackFiles.Length; i++) tracks[i] = Get(TrackFiles[i]);
        GameSettings.OnMusicChanged += ApplyMusicChoice;
        ApplyMusicChoice();
    }

    void OnDestroy()
    {
        GameSettings.OnMusicChanged -= ApplyMusicChoice;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        UpdateAmbience();
        musicLevel = Mathf.MoveTowards(musicLevel, listenerIndoors ? 1f : 0.25f, 0.6f * Time.unscaledDeltaTime);
        music.volume = musicVolume * GameSettings.MusicVolume * musicLevel;
        // Ngẫu nhiên: hết bài thì sang một bài khác
        if (GameSettings.Music == 0 && !music.isPlaying && Application.isFocused) PlayTrack(NextRandomTrack());
        // Bấm vào nút giao diện nào cũng có tiếng click (máy tính, menu, bảng giá, thanh toán...)
        if (Input.GetMouseButtonDown(0) && Cursor.visible && EventSystem.current != null && PointerOnButton())
            PlayClip(Get("ui_click"), 0.6f);
    }

    private bool PointerOnButton()
    {
        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        foreach (RaycastResult r in uiHits)
        {
            Selectable s = r.gameObject.GetComponentInParent<Selectable>();
            if (s != null) return s.interactable;
        }
        return false;
    }

    // ================= API =================
    public static void Play(string name, float volume = 1f)
    {
        if (Instance != null) Instance.PlayClip(Instance.Get(name), volume);
    }

    public static void PlayAt(string name, Vector3 position, float volume = 1f)
    {
        if (Instance == null) return;
        AudioClip clip = Instance.Get(name);
        if (clip == null) return;
        var go = new GameObject("SFX_" + name);
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;            // 3D: nghe to khi đứng gần
        src.minDistance = 2f;
        src.maxDistance = 25f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.volume = volume * Instance.sfxVolume;
        src.pitch = Random.Range(0.96f, 1.04f);
        src.Play();
        Destroy(go, clip.length / Mathf.Max(0.5f, src.pitch) + 0.1f);
    }

    // ================= Nhạc nền =================
    private void ApplyMusicChoice()
    {
        int choice = GameSettings.Music;
        if (choice >= GameSettings.MusicOff) { music.Stop(); track = -1; return; }
        if (choice == 0) { music.loop = false; if (!music.isPlaying || track < 0) PlayTrack(NextRandomTrack()); return; }
        music.loop = true;
        if (track != choice - 1 || !music.isPlaying) PlayTrack(choice - 1);
    }

    private int NextRandomTrack()
    {
        var options = new List<int>();
        for (int i = 0; i < tracks.Length; i++) if (tracks[i] != null && i != track) options.Add(i);
        if (options.Count == 0) return track;
        return options[Random.Range(0, options.Count)];
    }

    private void PlayTrack(int i)
    {
        if (i < 0 || i >= tracks.Length || tracks[i] == null) return;
        track = i;
        music.clip = tracks[i];
        music.Play();
    }

    /// <summary>Phát sau một khoảng trễ (tiếng sấm tới sau tia chớp).</summary>
    public static void PlayDelayed(string name, float delay, float volume = 1f)
    {
        if (Instance != null) Instance.StartCoroutine(Instance.Delayed(name, delay, volume));
    }

    private IEnumerator Delayed(string name, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        PlayClip(Get(name), volume);
    }

    /// <summary>Đặt mức âm lượng đích cho một âm nền lặp (0 = tắt dần).</summary>
    public static void SetLoop(string name, float volume)
    {
        if (Instance != null) Instance.loopTarget[name] = Mathf.Clamp01(volume);
    }

    // ================= Âm nền =================
    private void UpdateAmbience()
    {
        if (Time.unscaledTime >= nextAmbience)
        {
            nextAmbience = Time.unscaledTime + 0.25f;
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            Vector3 at = listener != null ? listener.transform.position : Vector3.zero;
            listenerIndoors = WeatherSystem.IsSheltered(at);
            bool storm = WeatherSystem.CurrentWeather == WeatherSystem.Weather.Storm;
            bool rainy = WeatherSystem.CurrentWeather == WeatherSystem.Weather.Rain;
            loopTarget["rain_loop"] = rainy ? (listenerIndoors ? 0.05f : 0.18f) : 0f;
            loopTarget["storm_loop"] = storm ? (listenerIndoors ? 0.08f : 0.28f) : 0f;
            loopTarget["city_loop"] = listenerIndoors ? 0.06f : 0.22f;
            int customers = NPCShopper.Active.Count;
            loopTarget["crowd_loop"] = customers == 0 ? 0f : Mathf.Min(0.35f, 0.06f + 0.04f * customers) * (listenerIndoors ? 1f : 0.25f);

            // Còi xe xa xa khi đứng ngoài trời
            if (!listenerIndoors && listener != null && Time.unscaledTime >= nextHorn)
            {
                nextHorn = Time.unscaledTime + Random.Range(15f, 40f);
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(14f, 24f);
                PlayAt(Random.value < 0.7f ? "horn_1" : "horn_2", at + new Vector3(off.x, 0f, off.y), 0.35f);
            }
        }

        foreach (var kv in loopTarget)
        {
            if (!loops.TryGetValue(kv.Key, out AudioSource src))
            {
                if (kv.Value <= 0f) continue;
                AudioClip clip = Get(kv.Key);
                if (clip == null) continue;
                src = gameObject.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.time = Random.Range(0f, clip.length); // các âm nền không bắt đầu cùng một chỗ
                src.Play();
                loops[kv.Key] = src;
            }
            src.volume = Mathf.MoveTowards(src.volume, kv.Value * sfxVolume, 0.5f * Time.unscaledDeltaTime);
            if (src.volume <= 0f && src.isPlaying) src.Pause();
            else if (src.volume > 0f && !src.isPlaying) src.UnPause();
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null) return;
        sfx2D.pitch = 1f;
        sfx2D.PlayOneShot(clip, volume * sfxVolume);
    }

    private AudioClip Get(string name)
    {
        if (string.IsNullOrEmpty(name) || missing.Contains(name)) return null;
        if (clips.TryGetValue(name, out AudioClip c)) return c;
        c = Resources.Load<AudioClip>("Audio/" + name);
        if (c == null) { missing.Add(name); Debug.Log($"[Audio] Không có file Resources/Audio/{name} - bỏ qua âm thanh này."); return null; }
        clips[name] = c;
        return c;
    }
}
