using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cài đặt người chơi (lưu bằng PlayerPrefs): độ nhạy chuột, âm lượng, chất lượng đồ hoạ, toàn màn hình, bóng đèn trần.
/// Bóng đổ của đèn điểm / đèn rọi mặc định TẮT: cửa hàng có hàng trăm đèn trần, bật bóng đổ hết vừa nặng máy
/// vừa sinh cảnh báo "Reduced additional punctual light shadows..." liên tục.
/// </summary>
public static class GameSettings
{
    private const string KSens = "set.sensitivity", KVol = "set.volume", KQual = "set.quality", KFull = "set.fullscreen", KShadow = "set.lampShadows";

    public static float MouseSensitivity { get; private set; } = 2f;
    public static float Volume { get; private set; } = 1f;
    public static int Quality { get; private set; }
    public static bool Fullscreen { get; private set; } = true;
    public static bool LampShadows { get; private set; }

    private static readonly Dictionary<Light, LightShadows> originalShadows = new Dictionary<Light, LightShadows>();

    public static void Load()
    {
        MouseSensitivity = PlayerPrefs.GetFloat(KSens, 2f);
        Volume = PlayerPrefs.GetFloat(KVol, 1f);
        Quality = PlayerPrefs.GetInt(KQual, QualitySettings.GetQualityLevel());
        Fullscreen = PlayerPrefs.GetInt(KFull, Screen.fullScreen ? 1 : 0) == 1;
        LampShadows = PlayerPrefs.GetInt(KShadow, 0) == 1;
        ApplyAll();
    }

    public static void ApplyAll()
    {
        ApplySensitivity();
        AudioListener.volume = Volume;
        if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1), true);
        if (!Application.isEditor) Screen.fullScreen = Fullscreen;
        ApplyLampShadows();
    }

    public static void SetSensitivity(float v) { MouseSensitivity = Mathf.Clamp(v, 0.2f, 10f); PlayerPrefs.SetFloat(KSens, MouseSensitivity); ApplySensitivity(); }
    public static void SetVolume(float v) { Volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KVol, Volume); AudioListener.volume = Volume; }
    public static void SetQuality(int q)
    {
        Quality = Mathf.Clamp(q, 0, QualitySettings.names.Length - 1);
        PlayerPrefs.SetInt(KQual, Quality);
        QualitySettings.SetQualityLevel(Quality, true);
    }
    public static void SetFullscreen(bool v) { Fullscreen = v; PlayerPrefs.SetInt(KFull, v ? 1 : 0); Screen.fullScreen = v; }
    public static void SetLampShadows(bool v) { LampShadows = v; PlayerPrefs.SetInt(KShadow, v ? 1 : 0); ApplyLampShadows(); }

    public static string QualityName => QualitySettings.names.Length > 0 ? QualitySettings.names[Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1)] : "-";

    private static void ApplySensitivity()
    {
        foreach (PlayerController pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            pc.mouseSensitivity = MouseSensitivity;
    }

    /// <summary>Bật / tắt bóng đổ của mọi đèn điểm, đèn rọi (đèn mặt trời giữ nguyên).</summary>
    public static void ApplyLampShadows()
    {
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (!originalShadows.ContainsKey(l)) originalShadows[l] = l.shadows;
            l.shadows = LampShadows ? originalShadows[l] : LightShadows.None;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => originalShadows.Clear();
}
