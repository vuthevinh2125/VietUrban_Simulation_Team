using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Đo hiệu năng cho phần đánh giá trong report (chỉ trong Editor / Development Build).
/// Bấm F11: đo 60 giây, rồi hiện FPS trung bình và "1% low" (FPS của 1% khung hình chậm nhất)
/// và ghi thêm một dòng vào fps_log.csv trong thư mục save (kèm giờ, thời tiết, số khách, giới hạn bóng đèn).
///
/// Ngoài FPS còn ghi các bộ đếm của Unity Profiler (ProfilerRecorder, đọc mỗi frame trong lúc đo):
///  - cpu_main_ms: thời gian luồng chính mỗi frame (trung bình và frame chậm nhất 1%), tách được phần CPU khỏi GPU / vsync.
///  - gc_kb_frame: bộ nhớ cấp phát (rác cho GC) trung bình mỗi frame; gc_frames_pct: % số frame có cấp phát.
///  - draw_calls / setpass: số lệnh vẽ trung bình mỗi frame (minimap, bóng đèn ảnh hưởng trực tiếp chỉ số này).
/// Bộ đếm nào không có trong bản đang chạy (vd. bản Release) thì để trống.
/// </summary>
public class FpsLogger : MonoBehaviour
{
    private const float Duration = 60f;
    private const string Header =
        "time,avg_fps,low1_fps,frames,clock,weather,customers,max_shadow_lights,resolution,quality," +
        "cpu_main_ms_avg,cpu_main_ms_p99,gc_kb_frame_avg,gc_frames_pct,draw_calls_avg,setpass_avg";

    private static FpsLogger instance;
    private readonly List<float> frameTimes = new List<float>(8000);
    private readonly List<float> mainMs = new List<float>(8000);
    private double gcBytes, drawCalls, setPass;
    private int gcFrames, gcSamples, drawSamples, setPassSamples;
    private ProfilerRecorder mainThread, gcAlloc, drawCallsRec, setPassRec;
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
            if (Input.GetKeyDown(KeyCode.F11)) Begin();
            return;
        }

        frameTimes.Add(Time.unscaledDeltaTime);
        if (mainThread.Valid && mainThread.LastValue > 0) mainMs.Add(mainThread.LastValue * 1e-6f); // ns -> ms
        if (gcAlloc.Valid)
        {
            long b = gcAlloc.LastValue;
            gcBytes += b; gcSamples++;
            if (b > 0) gcFrames++;
        }
        if (drawCallsRec.Valid) { drawCalls += drawCallsRec.LastValue; drawSamples++; }
        if (setPassRec.Valid) { setPass += setPassRec.LastValue; setPassSamples++; }

        if (Time.unscaledTime - started >= Duration) Finish();
    }

    void OnDestroy() => StopRecorders();

    private void Begin()
    {
        frameTimes.Clear(); mainMs.Clear();
        gcBytes = drawCalls = setPass = 0;
        gcFrames = gcSamples = drawSamples = setPassSamples = 0;
        StopRecorders();
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
        if (!drawCallsRec.Valid) { drawCallsRec.Dispose(); drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1); }
        setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
        started = Time.unscaledTime;
        measuring = true;
        ProgressHUD.Toast("FPS test started - keep still for 60 seconds.");
    }

    private void StopRecorders()
    {
        mainThread.Dispose(); gcAlloc.Dispose(); drawCallsRec.Dispose(); setPassRec.Dispose();
    }

    private void Finish()
    {
        measuring = false;
        StopRecorders();
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
        string F(double v, string fmt = "0.00") => v.ToString(fmt, inv);

        // CPU luồng chính: trung bình và mốc 99% (frame chậm nhất 1%)
        string cpuAvg = "", cpuP99 = "";
        if (mainMs.Count > 0)
        {
            double sum = 0; foreach (float m in mainMs) sum += m;
            mainMs.Sort();
            cpuAvg = F(sum / mainMs.Count);
            cpuP99 = F(mainMs[Mathf.Min(mainMs.Count - 1, Mathf.FloorToInt(mainMs.Count * 0.99f))]);
        }
        string gcKb = gcSamples > 0 ? F(gcBytes / gcSamples / 1024.0, "0.000") : "";
        string gcPct = gcSamples > 0 ? F(100.0 * gcFrames / gcSamples, "0.0") : "";
        string dc = drawSamples > 0 ? F(drawCalls / drawSamples, "0") : "";
        string sp = setPassSamples > 0 ? F(setPass / setPassSamples, "0") : "";

        string clock = GameManager.Instance != null ? GameManager.Instance.ClockHours.ToString("0.00", inv) : "";
        string weather = WeatherSystem.WeatherName(WeatherSystem.CurrentWeather);
        int customers = NPCShopper.Active.Count;
        var budget = FindAnyObjectByType<LightShadowBudget>();
        string shadows = budget != null ? budget.maxShadowLights.ToString(inv) : "";

        ProgressHUD.Toast($"FPS test: average {avgFps.ToString("0.0", inv)}, 1% low {lowFps.ToString("0.0", inv)} ({frameTimes.Count} frames)" +
                          (cpuAvg.Length > 0 ? $"  ·  CPU {cpuAvg} ms" : "") + (gcKb.Length > 0 ? $"  ·  GC {gcKb} KB/frame" : ""));

        try
        {
            string path = Path.Combine(Application.persistentDataPath, "fps_log.csv");
            UpgradeHeader(path);
            bool header = !File.Exists(path);
            using (var w = new StreamWriter(path, true))
            {
                if (header) w.WriteLine(Header);
                w.WriteLine(string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv),
                    avgFps.ToString("0.0", inv), lowFps.ToString("0.0", inv), frameTimes.Count.ToString(inv),
                    clock, weather, customers.ToString(inv), shadows,
                    $"{Screen.width}x{Screen.height}", QualitySettings.names[QualitySettings.GetQualityLevel()],
                    cpuAvg, cpuP99, gcKb, gcPct, dc, sp));
            }
            Debug.Log($"[FpsLogger] Saved to {path}");
        }
        catch (Exception e) { Debug.LogWarning($"[FpsLogger] Could not write fps_log.csv: {e.Message}"); }
    }

    /// <summary>File cũ (chỉ có cột FPS) -> thay dòng tiêu đề bằng tiêu đề mới; các dòng cũ giữ nguyên (cột mới để trống).</summary>
    private static void UpgradeHeader(string path)
    {
        if (!File.Exists(path)) return;
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0 || lines[0] == Header) return;
        lines[0] = Header;
        File.WriteAllLines(path, lines);
    }
}
