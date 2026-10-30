using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Đo hiệu năng cho phần đánh giá trong report (chỉ trong Editor / Development Build).
/// Bấm F11: đo 60 giây, rồi hiện FPS trung bình và "1% low" (FPS của 1% khung hình chậm nhất)
/// và ghi thêm một dòng vào fps_log.csv trong thư mục save (kèm giờ, thời tiết, số khách, giới hạn bóng đèn).
/// </summary>
public class FpsLogger : MonoBehaviour
{
    private const float Duration = 60f;

    private static FpsLogger instance;
    private readonly List<float> frameTimes = new List<float>(8000);
    private bool measuring;
    private float started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!GameDebug.CheatsEnabled || instance != null) return;
        var go = new GameObject("FpsLogger");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FpsLogger>();
    }

    void Update()
    {
        if (!measuring)
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                frameTimes.Clear();
                started = Time.unscaledTime;
                measuring = true;
                ProgressHUD.Toast("FPS test started - keep still for 60 seconds.");
            }
            return;
        }

        frameTimes.Add(Time.unscaledDeltaTime);
        if (Time.unscaledTime - started >= Duration) Finish();
    }

    private void Finish()
    {
        measuring = false;
        if (frameTimes.Count == 0) return;

        float total = 0f;
        foreach (float t in frameTimes) total += t;
        float avgFps = frameTimes.Count / Mathf.Max(0.0001f, total);

        // 1% low = FPS tính trên 1% khung hình lâu nhất
        var sorted = new List<float>(frameTimes);
        sorted.Sort((a, b) => b.CompareTo(a));
        int n = Mathf.Max(1, sorted.Count / 100);
        float worst = 0f;
        for (int i = 0; i < n; i++) worst += sorted[i];
        float lowFps = n / Mathf.Max(0.0001f, worst);

        var inv = CultureInfo.InvariantCulture;
        string clock = GameManager.Instance != null ? GameManager.Instance.ClockHours.ToString("0.00", inv) : "";
        string weather = WeatherSystem.WeatherName(WeatherSystem.CurrentWeather);
        int customers = NPCShopper.Active.Count;
        var budget = FindAnyObjectByType<LightShadowBudget>();
        string shadows = budget != null ? budget.maxShadowLights.ToString(inv) : "";

        ProgressHUD.Toast($"FPS test: average {avgFps.ToString("0.0", inv)}, 1% low {lowFps.ToString("0.0", inv)} ({frameTimes.Count} frames)");

        try
        {
            string path = Path.Combine(Application.persistentDataPath, "fps_log.csv");
            bool header = !File.Exists(path);
            using (var w = new StreamWriter(path, true))
            {
                if (header) w.WriteLine("time,avg_fps,low1_fps,frames,clock,weather,customers,max_shadow_lights,resolution,quality");
                w.WriteLine(string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv),
                    avgFps.ToString("0.0", inv), lowFps.ToString("0.0", inv), frameTimes.Count.ToString(inv),
                    clock, weather, customers.ToString(inv), shadows,
                    $"{Screen.width}x{Screen.height}", QualitySettings.names[QualitySettings.GetQualityLevel()]));
            }
            Debug.Log($"[FpsLogger] Saved to {path}");
        }
        catch (Exception e) { Debug.LogWarning($"[FpsLogger] Could not write fps_log.csv: {e.Message}"); }
    }
}
