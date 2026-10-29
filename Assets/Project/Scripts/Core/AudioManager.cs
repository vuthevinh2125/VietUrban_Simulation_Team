using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Âm thanh của game (tự tạo, không cần đặt vào scene). File lấy từ Resources/Audio theo tên:
/// scan_beep, cash_register, coin, card_beep, door_slide, box_pickup, place_item, ui_click, level_up, store_music.
/// Thiếu file nào thì bỏ qua file đó. Âm lượng tổng chỉnh trong Settings (AudioListener.volume).
///  - AudioManager.Play("tên")            : âm thanh 2D (giao diện, tay người chơi)
///  - AudioManager.PlayAt("tên", vị trí)  : âm thanh 3D trong cửa hàng (quầy, cửa...)
/// Tự phát tiếng click khi bấm bất kỳ nút giao diện nào, và nhạc nền lặp lại.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Range(0f, 1f)] public float musicVolume = 0.18f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly HashSet<string> missing = new HashSet<string>();
    private AudioSource sfx2D, music;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

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
        if (Instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
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
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = musicVolume;
        music.ignoreListenerPause = true;
        AudioClip bgm = Get("store_music");
        if (bgm != null) { music.clip = bgm; music.Play(); }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        music.volume = musicVolume;
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
