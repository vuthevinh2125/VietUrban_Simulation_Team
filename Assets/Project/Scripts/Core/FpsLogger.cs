using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

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
///
/// Chế độ đo cố định (benchmark) để các lần đo so sánh được với nhau:
///  - Shift + F11: "bench-store"  - trong cửa hàng nhìn vào các dãy kệ, trời nắng, 12:00.
///  - Ctrl + F11:  "bench-street" - ngoài phố nhìn về mặt tiền cửa hàng và đường, giông, 21:00 (cảnh nặng nhất).
///  - Ctrl + Shift + F11: "bench-crowd" - như bench-store nhưng mở cửa và giữ đúng CrowdSize khách trong cửa hàng suốt 60 s
///    (đo tải AI khách: NavMesh, chọn kệ, animation). Kệ đang bày hàng được bổ sung đầy (chỉ là công cụ thử).
/// Game đưa người chơi tới vị trí / hướng nhìn cố định, khoá điều khiển, đặt thời tiết + giờ, chờ 3 giây cho
/// ánh sáng / bóng ổn định rồi mới đo 60 giây; xong trả lại điều khiển. Muốn tự chọn chỗ đứng: đặt GameObject rỗng
/// tên "BenchmarkPoint_Store" / "BenchmarkPoint_Street" trong scene (vị trí + hướng của nó được dùng thay mặc định).
/// Cột "mode" trong csv ghi free / bench-store / bench-street.
/// </summary>
public class FpsLogger : MonoBehaviour
{
    private const float Duration = 60f;
    private const string Header =
        "time,avg_fps,low1_fps,frames,clock,weather,customers,max_shadow_lights,resolution,quality," +
        "cpu_main_ms_avg,cpu_main_ms_p99,gc_kb_frame_avg,gc_frames_pct,draw_calls_avg,setpass_avg,mode,max_frame_ms,frames_over_50ms";

    private static FpsLogger instance;
    private readonly List<float> frameTimes = new List<float>(8000);
    private readonly List<float> mainMs = new List<float>(8000);
    private double gcBytes, drawCalls, setPass;
    private int gcFrames, gcSamples, drawSamples, setPassSamples;
    private ProfilerRecorder mainThread, gcAlloc, drawCallsRec, setPassRec;
    private bool measuring, preparing;
    private float started;
    private string mode = "free";
    public const int CrowdSize = 15;     // số khách giữ trong cửa hàng khi đo bench-crowd (game thường tối đa 4-7)
    private bool keepCrowd;
    private float nextCrowdSpawn;
    private CustomerSpawner crowdSpawner;
    private long crowdSum; private int crowdSamples;
    private PlayerController frozenPlayer;

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
            if (!preparing && Input.GetKeyDown(KeyCode.F11))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (shift && ctrl) StartCoroutine(Benchmark(2));
                else if (shift) StartCoroutine(Benchmark(0));
                else if (ctrl) StartCoroutine(Benchmark(1));
                else { mode = "free"; Begin(); }
            }
            return;
        }

        frameTimes.Add(Time.unscaledDeltaTime);
        if (keepCrowd) { KeepCrowd(); crowdSum += NPCShopper.Active.Count; crowdSamples++; }
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

    void OnDestroy()
    {
        StopRecorders();
        PlayerController.InputFrozen = false;
    }

    // ================= Chế độ đo cố định =================
    private IEnumerator Benchmark(int kind)
    {
        bool store = kind != 1, crowd = kind == 2;
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null || !pc.isActiveAndEnabled)
        {
            ProgressHUD.Toast("Benchmark: get off the vehicle and stand in the city first.");
            yield break;
        }
        if (!BenchmarkPose(store, out Vector3 pos, out float yaw, out float pitch))
        {
            ProgressHUD.Toast("Benchmark: could not find the store entrance.");
            yield break;
        }
        preparing = true;
        mode = crowd ? "bench-crowd" : store ? "bench-store" : "bench-street";
        frozenPlayer = pc;
        PlayerController.InputFrozen = true;

        WeatherSystem.ForceWeather(store ? WeatherSystem.Weather.Sunny : WeatherSystem.Weather.Storm);
        // Giờ làm việc của cửa hàng có thể kết thúc trước khi trời tối hẳn (vd. đóng cửa 18:00) nên cảnh đêm
        // chỉ ép giờ của ánh sáng (DayNightCycle) là 21:00, còn đồng hồ game đặt trong giờ làm việc.
        if (GameManager.Instance != null) GameManager.Instance.DebugSetClock(12f);
        DayNightCycle.DebugHourOverride = store ? 12f : 21f;
        pc.SetPose(pos, yaw, pitch);
        ProgressHUD.Toast($"Benchmark ({mode}): settling 3 s, then measuring 60 s - don't touch anything.");

        float until = Time.unscaledTime + 3f;
        if (crowd)
        {
            // Mở cửa, bổ sung đầy các kệ đang bày hàng, rồi cho khách vào dần (1 người / 0.5 s) và chờ 20 s cho họ vào trong
            crowdSpawner = FindAnyObjectByType<CustomerSpawner>();
            if (crowdSpawner == null) { ProgressHUD.Toast("Benchmark: no CustomerSpawner in the scene."); preparing = false; PlayerController.InputFrozen = false; frozenPlayer = null; DayNightCycle.DebugHourOverride = -1f; yield break; }
            if (GameManager.Instance != null && !GameManager.Instance.IsStoreOpen) GameManager.Instance.SetStoreOpen(true, force: true);
            int topped = 0;
            foreach (Shelf sh in StockManager.Shelves)
                if (sh != null && sh.LastItem != null && sh.SpoiledCount == 0) topped += sh.FillInstant(sh.LastItem, sh.Capacity - sh.Count);
            keepCrowd = true;
            crowdSum = 0; crowdSamples = 0;
            until = Time.unscaledTime + 20f;
            ProgressHUD.Toast($"Benchmark (bench-crowd): {CrowdSize} customers coming in ({topped} items topped up). Measuring starts in 20 s.");
        }
        while (Time.unscaledTime < until)
        {
            pc.SetPose(pos, yaw, pitch); // giữ nguyên chỗ (vật lý có thể đẩy nhẹ)
            if (keepCrowd) KeepCrowd();
            yield return null;
        }
        preparing = false;
        Begin();
    }

    /// <summary>bench-crowd: luôn giữ đủ CrowdSize khách (khách mua xong ra về thì cho người mới vào), tối đa 2 người / giây.</summary>
    private void KeepCrowd()
    {
        if (crowdSpawner == null || Time.unscaledTime < nextCrowdSpawn) return;
        if (NPCShopper.Active.Count >= CrowdSize) return;
        nextCrowdSpawn = Time.unscaledTime + 0.5f;
        crowdSpawner.Spawn();
    }

    /// <summary>Vị trí đo: BenchmarkPoint_* nếu có trong scene, nếu không thì tính từ cửa ra vào và tâm các dãy kệ.</summary>
    private static bool BenchmarkPose(bool store, out Vector3 pos, out float yaw, out float pitch)
    {
        pos = default; yaw = 0f; pitch = 0f;
        GameObject marker = GameObject.Find(store ? "BenchmarkPoint_Store" : "BenchmarkPoint_Street");
        if (marker != null)
        {
            pos = marker.transform.position;
            Vector3 e = marker.transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
            return true;
        }

        Vector3 center = Vector3.zero; int n = 0;
        foreach (Shelf sh in StockManager.Shelves) if (sh != null) { center += sh.transform.position; n++; }
        EntranceDoor door = null; float best = float.MaxValue;
        foreach (EntranceDoor d in FindObjectsByType<EntranceDoor>())
        {
            float dist = n > 0 ? Vector3.Distance(d.transform.position, center / n) : 0f;
            if (dist < best) { best = dist; door = d; }
        }
        if (door == null || n == 0) return false;
        center /= n;
        Vector3 doorPos = door.transform.position;
        Vector3 inward = center - doorPos; inward.y = 0f;
        if (inward.sqrMagnitude < 0.01f) return false;
        inward.Normalize();

        Vector3 want = store ? doorPos + inward * 3f : doorPos - inward * 10f;
        if (!NavMesh.SamplePosition(want, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;
        pos = hit.position + Vector3.up * 0.05f;
        // Trong nhà: nhìn vào các dãy kệ. Ngoài phố: đứng cách cửa 10 m, quay lại nhìn mặt tiền (cả hai đều nhìn theo hướng "vào trong")
        yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
        pitch = store ? 8f : 0f;
        return true;
    }

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
        if (frozenPlayer != null) { PlayerController.InputFrozen = false; frozenPlayer = null; }
        float avgCrowd = crowdSamples > 0 ? crowdSum / (float)crowdSamples : -1f;
        keepCrowd = false; crowdSamples = 0;
        float lightHour = DayNightCycle.DebugHourOverride; // giờ ánh sáng bị ép trong chế độ đo cố định (nếu có)
        DayNightCycle.DebugHourOverride = -1f;
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
        int spikes = 0;
        foreach (float t in frameTimes) if (t > 0.05f) spikes++;   // frame lâu hơn 50 ms = khựng thấy rõ
        float maxMs = sorted[0] * 1000f;

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

        string clock = lightHour >= 0f ? lightHour.ToString("0.00", inv)
                     : GameManager.Instance != null ? GameManager.Instance.ClockHours.ToString("0.00", inv) : "";
        string weather = WeatherSystem.WeatherName(WeatherSystem.CurrentWeather);
        // bench-crowd: số khách TRUNG BÌNH trong lúc đo; chế độ khác: số khách lúc kết thúc
        string customers = avgCrowd >= 0f ? avgCrowd.ToString("0.0", CultureInfo.InvariantCulture) : NPCShopper.Active.Count.ToString(CultureInfo.InvariantCulture);
        var budget = FindAnyObjectByType<LightShadowBudget>();
        string shadows = budget != null ? budget.maxShadowLights.ToString(inv) : "";

        ProgressHUD.Toast($"FPS test [{mode}]: average {avgFps.ToString("0.0", inv)}, 1% low {lowFps.ToString("0.0", inv)} ({frameTimes.Count} frames)" +
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
                    clock, weather, customers, shadows,
                    $"{Screen.width}x{Screen.height}", QualitySettings.names[QualitySettings.GetQualityLevel()],
                    cpuAvg, cpuP99, gcKb, gcPct, dc, sp, mode, F(maxMs, "0.0"), spikes.ToString(inv)));
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
