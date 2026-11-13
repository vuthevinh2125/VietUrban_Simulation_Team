using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sự kiện "giao hàng bị cướp" (lời thoại, tiêu đề, bản tin đổi theo buổi trong ngày và thời tiết lúc xảy ra) (tự tạo khi vào game): đoạn phim có lựa chọn kiểu game nhập vai, VỪA CHẠY XE VỪA CHỌN.
///  - Kích hoạt: đang đi xe máy giao hàng, trời đã tối (từ ~30 phút trước hoàng hôn), còn 50-140 m tới điểm giao;
///    mỗi đơn chỉ xét 1 lần, 60% xảy ra, tối đa 1 lần / ngày.
///  - Trong đoạn phim xe người chơi tự chạy theo làn phải của mạng đường thành phố (VietDistrict), có người lái đứng thay.
///    Hai tên cướp (không mũ bảo hiểm, mũ len, khẩu trang, cầm dao / gậy) đi một xe máy áp sát bên trái, chạy song song đe doạ.
///  - Lựa chọn 1 (8 s, hình chậm x0.3 nhưng xe vẫn chạy; phím 1-4 hoặc chuột): vặn ga bỏ chạy / đưa hàng / hô cướp / đạp xe chúng.
///    Bỏ chạy -> rượt đuổi -> lựa chọn 2 (5 s): rẽ vào ngõ hẹp / chạy về chốt CSGT / phanh gấp cho chúng vượt.
///    Hết giờ mà chưa chọn = đứng hình, bị cướp.
///  - Kết thúc: thoát (thưởng + XP, giao tiếp đơn), CSGT bắt được (thưởng thêm), mất gói hàng (đơn huỷ), mất cả hàng lẫn tiền mặt.
///    Sáng hôm sau có "bản tin" kể lại chuyện đêm qua.
/// Phím thử (chỉ bản Editor / Development): Shift + F4 gây sự kiện ngay khi đang chở hàng bằng xe máy.
/// </summary>
public partial class NightRobbery : MonoBehaviour
{
    public static NightRobbery Instance { get; private set; }
    public static bool Playing { get; private set; }

    [Range(0f, 1f)] public float chance = 0.6f;
    public float minDistance = 50f, maxDistance = 140f;

    private enum Ending { None, Escaped, Arrested, LostPackage, Robbed }

    private int decidedSerial = -1, lastDay = -1;
    private float nextCheck;
    private Ending lastNight = Ending.None;
    private string lastStreet = "";

    private static readonly Color Accent = new Color(0.95f, 0.25f, 0.2f), RobberCol = new Color(1f, 0.55f, 0.45f), YouCol = new Color(0.55f, 0.85f, 1f);

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
        if (Instance == null && SupermarketScene.IsActive) new GameObject("NightRobbery").AddComponent<NightRobbery>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Playing = false;

    void Awake()
    {
        Instance = this;
        GameManager.OnNewDay += OnNewDay;
        GameManager.HoldDayEnd = HoldsDayEnd;   // sự kiện camera ca đêm giữ báo cáo cuối ngày lại
    }

    void OnDestroy()
    {
        GameManager.OnNewDay -= OnNewDay;
        if (Instance == this) { Instance = null; GameManager.HoldDayEnd = null; }
        if (Playing) Time.timeScale = 1f;
        Playing = false;
    }

    // ================= Kích hoạt =================
    void Update()
    {
        if (Playing) return;
        if (!mournersPrepped && IsNight()) StartCoroutine(PrepMourners());   // chuẩn bị đồ tang từ chập tối (chạy nền)
        if (HorrorUpdate()) return;                                          // sự kiện kinh dị ban đêm (NightRobbery.Horror*.cs)
        bool force = GameDebug.CheatsEnabled && Input.GetKeyDown(KeyCode.F4) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        if (!force && Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        if (!DeliveryManager.IsDelivering(out Vector3 dest, out int serial))
        {
            if (force) ProgressHUD.Toast("Thử sự kiện cướp: hãy lấy gói hàng giao trước đã.");
            return;
        }
        PlayerCarController ride = RidingBike();
        if (ride == null)
        {
            // Đang lái ô tô: vụ "va chạm giả để ăn vạ" (NightRobbery.CarScam.cs)
            if (!TryCarScam(dest, serial, force) && force) ProgressHUD.Toast("Thử sự kiện: hãy lên xe máy hoặc ô tô trước đã.");
            return;
        }
        if (!force)
        {
            if (serial == decidedSerial || !IsNight()) return;
            Vector3 d = dest - ride.transform.position; d.y = 0f;
            if (d.magnitude > maxDistance || d.magnitude < minDistance) return;
            decidedSerial = serial; // mỗi đơn chỉ gieo xúc xắc một lần
            int day = GameManager.Instance != null ? GameManager.Instance.currentDay : 0;
            if (day == lastDay || Random.value > chance) return;
            lastDay = day;
        }
        if (Busy()) return;
        StartCoroutine(Run(ride));
    }

    private static bool Busy() =>
        StoreEvents.CutsceneRunning || IntroCutscene.Playing || Time.timeScale <= 0f || UIState.IsBlockingUIOpen;

    // ================= Bối cảnh: buổi trong ngày + thời tiết (lời thoại đổi theo) =================
    private enum Part { Morning, Noon, Afternoon, Dusk, Night }
    private Part part = Part.Night;
    private WeatherSystem.Weather wx = WeatherSystem.Weather.Sunny;
    private string lastWhen = "Đêm qua";
    private bool Wet => wx == WeatherSystem.Weather.Rain || wx == WeatherSystem.Weather.Storm;
    private bool Dark => part == Part.Night || part == Part.Dusk;

    private void ReadContext()
    {
        float h = DayNightCycle.DebugHourOverride >= 0f ? DayNightCycle.DebugHourOverride
                : GameManager.Instance != null ? GameManager.Instance.ClockHours : 21f;
        float sunset = DayNightCycle.Instance != null ? DayNightCycle.Instance.sunsetHour : 18.5f;
        part = h < 5f || h >= sunset + 0.4f ? Part.Night : h >= sunset - 0.8f ? Part.Dusk : h < 11f ? Part.Morning : h < 14f ? Part.Noon : Part.Afternoon;
        wx = WeatherSystem.CurrentWeather;
    }

    private string Title() => part switch
    {
        Part.Morning => "ĐƠN HÀNG BUỔI SÁNG", Part.Noon => "ĐƠN HÀNG GIỮA TRƯA", Part.Afternoon => "ĐƠN HÀNG BUỔI CHIỀU",
        Part.Dusk => "ĐƠN HÀNG LÚC CHẠNG VẠNG", _ => "ĐƠN HÀNG ĐÊM",
    };

    private string Subtitle()
    {
        string t = part switch { Part.Morning => "buổi sáng", Part.Noon => "giữa trưa", Part.Afternoon => "buổi chiều", Part.Dusk => "lúc chạng vạng", _ => "sau khi trời tối" };
        string w = wx switch
        {
            WeatherSystem.Weather.Rain => " - trời mưa", WeatherSystem.Weather.Storm => " - mưa giông", WeatherSystem.Weather.Fog => " - sương mù",
            WeatherSystem.Weather.Cloudy => " - trời âm u", _ => Dark ? "" : " - trời nắng",
        };
        return "Quận Hai Bà Trưng, " + t + w;
    }

    private string OpeningLine()
    {
        if (wx == WeatherSystem.Weather.Storm) return "Giông to thế này, phố chẳng còn ai...";
        if (wx == WeatherSystem.Weather.Rain) return Dark ? "Mưa thế này, phố đêm vắng tanh..." : "Mưa thế này, đường vắng hẳn...";
        if (wx == WeatherSystem.Weather.Fog) return "Sương mù dày quá, chẳng nhìn rõ gì phía trước...";
        return part switch
        {
            Part.Morning => "Sắp tới rồi... sớm thế này đoạn này vẫn còn vắng.",
            Part.Noon => wx == WeatherSystem.Weather.Sunny ? "Trưa nắng chang chang, người ta đi nghỉ trưa hết rồi..." : "Giờ nghỉ trưa, đoạn này vắng hoe.",
            Part.Afternoon => "Sắp tới rồi... đoạn phố này lúc nào cũng vắng.",
            Part.Dusk => "Trời sắp tối rồi, giao nhanh còn về.",
            _ => "Sắp tới rồi... phố đêm nay vắng thật.",
        };
    }

    private string CashLine() => Dark ? "Đơn đêm toàn tiền mặt. Đi!"
        : Wet ? "Mưa thế này chẳng ai để ý đâu. Đi!" : "Đường vắng, chẳng ai nhìn đâu. Đi!";

    private string MirrorLine() => Dark || wx == WeatherSystem.Weather.Fog ? "Cái đèn pha kia... bám sau mình ba dãy phố rồi." : "Cái xe máy kia... bám sau mình ba dãy phố rồi.";

    private string KnifeLine()
    {
        string where = Wet ? "dưới làn mưa" : wx == WeatherSystem.Weather.Fog ? "trong màn sương" : part == Part.Night ? "dưới ánh đèn đường"
                     : part == Part.Dusk ? "trong ánh chiều nhập nhoạng" : wx == WeatherSystem.Weather.Sunny ? "dưới nắng" : "giữa ban ngày";
        return $"<i>Lưỡi dao loé lên {where}.</i>";
    }

    private string ChaseLine() => "<i>Hai chiếc xe lao vun vút trên phố vắng. Còi inh ỏi. "
        + (Wet ? "Nước bắn tung toé." : Dark ? "Đèn pha loang loáng." : "Bụi tung mù mịt.") + "</i>";

    private string Road() => Wet ? "mặt đường ướt" : "mặt đường";

    private string AwayLine() => Dark ? "<i>Chiếc xe rú ga, mất hút vào bóng tối.</i>"
        : wx == WeatherSystem.Weather.Fog ? "<i>Chiếc xe rú ga, mất hút trong sương.</i>" : "<i>Chiếc xe rú ga, mất hút cuối phố.</i>";

    private string When() => part switch
    {
        Part.Morning => "Sáng qua", Part.Noon => "Trưa qua", Part.Afternoon => "Chiều qua", Part.Dusk => "Chiều tối qua", _ => "Đêm qua",
    };

    private static bool IsNight()
    {
        float h = DayNightCycle.DebugHourOverride >= 0f ? DayNightCycle.DebugHourOverride
                : GameManager.Instance != null ? GameManager.Instance.ClockHours : 12f;
        float sunset = DayNightCycle.Instance != null ? DayNightCycle.Instance.sunsetHour : 18.5f;
        return h >= sunset - 0.5f || h < 5f;
    }

    /// <summary>Xe máy người chơi đang lái (ô tô thì không bị cướp kiểu này).</summary>
    private static PlayerCarController RidingBike()
    {
        foreach (PlayerCarController c in FindObjectsByType<PlayerCarController>())
            if (c != null && c.isDriving && c.GetComponentInChildren<MotorcycleInteraction>(true) != null) return c;
        foreach (MotorcycleInteraction m in FindObjectsByType<MotorcycleInteraction>())
        {
            PlayerCarController c = m.GetComponentInParent<PlayerCarController>();
            if (c != null && c.isDriving) return c;
        }
        return null;
    }

    private void OnNewDay(int day)
    {
        switch (lastNight)
        {
            case Ending.Arrested:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> {lastWhen} {lastStreet}, CSGT bắt giữ hai tên cướp đi xe máy nhờ một shipper dũng cảm.");
                break;
            case Ending.Robbed:
            case Ending.LostPackage:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> {lastWhen} {lastStreet}, một shipper bị cướp. Công an khuyên shipper đi đường đông người, cảnh giác với xe bám đuôi.");
                break;
            case Ending.Escaped:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> {lastWhen} {lastStreet}, một shipper đã thoát khỏi hai tên cướp.");
                break;
        }
        lastNight = Ending.None;
        ScamNews();
        HorrorNews();
    }

    // ================= Đường chạy (làn phải của mạng đường) =================
    /// <summary>Đường gấp khúc chạy theo làn: lấy mẫu vị trí / hướng theo quãng đường s (có ngoại suy trước / sau).</summary>
    private class Rail
    {
        public readonly List<Vector3> p = new List<Vector3>();
        public readonly List<float> s = new List<float>();
        public float Length => s.Count > 0 ? s[s.Count - 1] : 0f;

        public void Add(Vector3 q)
        {
            q.y = VietDistrict.RoadTop;
            if (p.Count > 0 && (q - p[p.Count - 1]).sqrMagnitude < 0.04f) return;
            s.Add(p.Count == 0 ? 0f : s[s.Count - 1] + Vector3.Distance(p[p.Count - 1], q));
            p.Add(q);
        }

        public void Sample(float d, float lateral, out Vector3 pos, out Vector3 dir)
        {
            int n = p.Count;
            if (n < 2) { pos = n > 0 ? p[0] : Vector3.zero; dir = Vector3.forward; return; }
            int i = 0;
            if (d >= s[n - 1]) i = n - 2;
            else if (d > 0f) { while (i < n - 2 && s[i + 1] < d) i++; }
            Vector3 a = p[i], b = p[i + 1];
            dir = (b - a).normalized;
            float t = (d - s[i]) / Mathf.Max(0.001f, s[i + 1] - s[i]);
            pos = a + (b - a) * t;   // ngoài khoảng thì ngoại suy thẳng
            // làm mềm hướng ở chỗ nối đoạn
            if (i + 2 < n && t > 0.5f) dir = Vector3.Slerp(dir, (p[i + 2] - b).normalized, (t - 0.5f)).normalized;
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            pos += right * lateral;
        }

        public float Project(Vector3 q)
        {
            float best = float.MaxValue, at = 0f;
            for (int i = 0; i + 1 < p.Count; i++)
            {
                Vector3 a = p[i], ab = p[i + 1] - a; ab.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(new Vector3(q.x - a.x, 0f, q.z - a.z), ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                Vector3 c = a + ab * t;
                float dd = (new Vector3(q.x, 0f, q.z) - new Vector3(c.x, 0f, c.z)).sqrMagnitude;
                if (dd < best) { best = dd; at = s[i] + ab.magnitude * t; }
            }
            return at;
        }
    }

    /// <summary>Dựng đường chạy ~450 m từ đoạn phố gần người chơi nhất cùng hướng xe, qua ngã tư ưu tiên đi thẳng.</summary>
    private static Rail BuildRail(VietDistrict city, Vector3 pos, Vector3 heading, out float lane, bool turnFirst = false)
    {
        lane = 2.5f;
        city.EnsureLayout();
        var nodes = city.Nodes;
        int ne = city.Edges.Count;
        if (nodes.Count < 2 || ne == 0) return null;
        var links = new List<int>[nodes.Count];
        for (int i = 0; i < links.Length; i++) links[i] = new List<int>();
        var half = new Dictionary<long, float>();
        for (int e = 0; e < ne; e++)
        {
            Vector2Int ed = city.Edges[e];
            links[ed.x].Add(ed.y); links[ed.y].Add(ed.x);
            half[Key(ed.x, ed.y)] = e < city.EdgeHalf.Count ? city.EdgeHalf[e] : 5f;
        }
        // Đoạn gần nhất, chiều đi theo hướng xe
        int from = -1, to = -1; float bestD = float.MaxValue;
        Vector3 p0 = new Vector3(pos.x, 0f, pos.z);
        foreach (Vector2Int ed in city.Edges)
        {
            Vector3 a = nodes[ed.x], b = nodes[ed.y]; a.y = b.y = 0f;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p0 - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            float d = (p0 - (a + ab * t)).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                bool along = Vector3.Dot(ab, heading) >= 0f;
                from = along ? ed.x : ed.y; to = along ? ed.y : ed.x;
            }
        }
        if (from < 0) return null;
        // Làn: giữ khoảng cách hiện tại tới tim đường (bên phải), kẹp trong mặt đường
        {
            Vector3 a = nodes[from], b = nodes[to]; a.y = b.y = 0f;
            Vector3 dir = (b - a).normalized, right = new Vector3(dir.z, 0f, -dir.x);
            float h = half.TryGetValue(Key(from, to), out float hh) ? hh : 5f;
            float lat = Vector3.Dot(p0 - a, right);
            lane = Mathf.Clamp(lat, 1.6f, Mathf.Max(1.8f, h - 1.6f));
            if (lat < 0.5f) lane = Mathf.Max(1.8f, h * 0.5f);
        }
        var rail = new Rail();
        Vector3 LaneRight(int x, int y) { Vector3 d = nodes[y] - nodes[x]; d.y = 0f; d.Normalize(); return new Vector3(d.z, 0f, -d.x); }
        Vector3 Dir(int x, int y) { Vector3 d = nodes[y] - nodes[x]; d.y = 0f; return d.normalized; }
        {
            Vector3 a = nodes[from]; a.y = 0f;
            Vector3 dir = Dir(from, to);
            float t = Vector3.Dot(p0 - a, dir);
            rail.Add(a + dir * (t - 1f) + LaneRight(from, to) * lane);
        }
        int prev = from, cur = to, guard = 0;
        bool wantTurn = turnFirst;
        while (rail.Length < 450f && guard++ < 20)
        {
            Vector3 node = nodes[cur]; node.y = 0f;
            Vector3 inDir = Dir(prev, cur), inRight = LaneRight(prev, cur);
            float r = cur < city.NodeRadius.Count ? Mathf.Max(4f, city.NodeRadius[cur]) : 6f;
            // ngã tư tiếp theo: ưu tiên đi thẳng, không quay đầu
            int next = -1; float bestDot = -2f;
            foreach (int k in links[cur])
            {
                if (k == prev) continue;
                float dot = Vector3.Dot(Dir(cur, k), inDir);
                if (dot > bestDot) { bestDot = dot; next = k; }
            }
            // Rẽ gấp (cắt đuôi): ngã tư đầu tiên cách xa hơn 12 m, ưu tiên rẽ phải, không có thì rẽ trái
            if (wantTurn && Vector3.Distance(new Vector3(p0.x, 0f, p0.z), node) > 12f)
            {
                int turn = -1; float best = -1f;
                foreach (int k in links[cur])
                {
                    if (k == prev) continue;
                    Vector3 dk = Dir(cur, k);
                    if (Mathf.Abs(Vector3.Dot(dk, inDir)) > 0.5f) continue;
                    float score = Vector3.Dot(dk, inRight) > 0f ? 2f : 1f;
                    if (score > best) { best = score; turn = k; }
                }
                if (turn >= 0) { next = turn; bestDot = Vector3.Dot(Dir(cur, turn), inDir); wantTurn = false; }
            }
            if (next < 0) { rail.Add(node - inDir * r + inRight * lane); break; } // đường cụt
            Vector3 outDir = Dir(cur, next), outRight = LaneRight(cur, next);
            Vector3 enter = node - inDir * r + inRight * lane, exit = node + outDir * r + outRight * lane;
            rail.Add(enter);
            if (bestDot < 0.9f)
            {
                // cua: Bezier bậc 2 qua giao điểm hai làn
                Vector3 ctrl = node + inRight * lane + outRight * lane;
                for (int k = 1; k < 6; k++)
                {
                    float u = k / 6f;
                    rail.Add(Vector3.Lerp(Vector3.Lerp(enter, ctrl, u), Vector3.Lerp(ctrl, exit, u), u));
                }
            }
            rail.Add(exit);
            prev = cur; cur = next;
        }
        return rail.p.Count >= 2 ? rail : null;
    }

    private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    // ================= Diễn viên + chuyển động =================
    private PlayerCarController car;
    private Rail rail;
    private float lane, sYou, vYou, vYouWant, youLat, youLatWant, sRob, robRel, robRelWant, robLat, robLatWant, robLean, slow = 1f, yOffset;
    private bool robFleeing, robDown;
    private float robRoll, robRollWant, robSpin, robSpinWant, shake, flash, youLean, vRob, vRobWant;
    private Rail robRail;
    private VietDistrict cityRef;
    private Transform bike, standIn;
    private Vector3 youPos, youDir, robPos, robDir;

    /// <summary>Gọi mỗi frame trong mọi cú máy: xe người chơi chạy theo làn, xe cướp bám theo vị trí tương đối mong muốn.</summary>
    private void Drive()
    {
        if (rail == null || car == null) { UpdateCctv(); UpdateGhouls(Time.deltaTime); return; }   // đoạn phim trong cửa hàng: không có xe
        float dt = Time.unscaledDeltaTime * slow;
        if (!float.IsNaN(stopAtS)) vYouWant = Mathf.Clamp((stopAtS - sYou) * 0.9f, 0f, 8f);   // phanh dừng đúng chỗ
        vYou = Mathf.MoveTowards(vYou, vYouWant, (vYouWant > vYou ? 5f : 9f) * dt);
        if (sYou + vYou * dt > rail.Length - 3f) { vYou = Mathf.Max(0f, (rail.Length - 3f - sYou) / Mathf.Max(dt, 0.001f)); vYouWant = 0f; }
        sYou += vYou * dt;
        youLat = Mathf.MoveTowards(youLat, youLatWant, 3f * dt);
        rail.Sample(sYou, youLat, out youPos, out youDir);
        Transform ct = car.transform;
        Vector3 fwdNow = ct.forward; fwdNow.y = 0f;
        Vector3 lookDir = Vector3.Slerp(fwdNow.sqrMagnitude > 0.001f ? fwdNow.normalized : youDir, youDir, 1f - Mathf.Exp(-6f * dt));
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.001f) lookDir = youDir;
        // nghiêng xe theo tốc độ quay đầu (như người lái nghiêng vào cua)
        float yawRate = dt > 0f ? Vector3.SignedAngle(fwdNow, lookDir, Vector3.up) / dt : 0f;
        youLean = Mathf.Lerp(youLean, Mathf.Clamp(-yawRate * (carMode ? 0.04f : 0.35f) * Mathf.Clamp01(vYou / 8f), -32f, 32f), 1f - Mathf.Exp(-6f * dt)); // ô tô chỉ nghiêng nhẹ
        ct.SetPositionAndRotation(youPos + Vector3.up * yOffset, Quaternion.LookRotation(lookDir) * Quaternion.Euler(0f, 0f, youLean));

        UpdateFlyer(dt);
        UpdateScamAnim(dt);
        UpdateCoffin(dt);
        UpdateDemons(Time.deltaTime);
        UpdateGhouls(Time.deltaTime);
        if (bike == null) return;
        float lastLat = robLat;
        robLat = Mathf.MoveTowards(robLat, robLatWant, 2.2f * dt);
        if (robRail != null)
        {
            // đã bị cắt đuôi: chạy tiếp trên đường cũ, tự phanh
            vRob = Mathf.MoveTowards(vRob, vRobWant, 9f * dt);
            sRob += vRob * dt;
            robRail.Sample(sRob, robLat, out robPos, out robDir);
        }
        else
        {
            robRel = Mathf.MoveTowards(robRel, robRelWant, (robFleeing ? 16f : 7f) * dt);
            sRob = sYou + robRel;
            rail.Sample(sRob, robLat, out robPos, out robDir);
        }
        robLean = Mathf.Lerp(robLean, Mathf.Clamp((robLat - lastLat) / Mathf.Max(dt, 0.001f) * -9f, -20f, 20f), 1f - Mathf.Exp(-5f * dt));
        robRoll = Mathf.MoveTowards(robRoll, robRollWant, 160f * dt);   // đổ xe: nghiêng hẳn xuống đường
        robSpin = Mathf.MoveTowards(robSpin, robSpinWant, 60f * dt);    // trượt xoay ngang
        bike.SetPositionAndRotation(robPos, Quaternion.LookRotation(robDir) * Quaternion.Euler(0f, robSpin, robDown ? robRoll : robLean));
    }

    // ================= Đoạn phim =================
    private IEnumerator Run(PlayerCarController ride)
    {
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (city == null || city.peopleModels == null || city.peopleModels.Length == 0) yield break;
        car = ride;
        Transform ct = car.transform;
        Vector3 heading = ct.forward; heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        rail = BuildRail(city, ct.position, heading.normalized, out lane);
        if (rail == null) yield break;
        Playing = true;
        ReadContext();
        lastWhen = When();
        lastStreet = DeliveryManager.TargetName(out string place) ? "gần " + place : "trong thành phố";

        // Xe người chơi do đoạn phim điều khiển
        Rigidbody rb = car.GetComponent<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
        car.enabled = false;
        yOffset = Mathf.Clamp(ct.position.y - Ground(ct.position, ct), 0f, 1.5f);
        sYou = rail.Project(ct.position);
        vYou = vYouWant = 8.5f;
        youLat = youLatWant = 0f;
        passPose.Clear(); youPose.Clear();
        robDown = false; robRoll = robRollWant = robSpin = robSpinWant = 0f; shake = flash = youLean = 0f; robRail = null; cityRef = city;
        standIn = SpawnStandIn(city);

        // Bọn cướp xuất phát 28 m phía sau, làn bên trái người chơi
        bike = SpawnRobbers(city);
        robRel = -28f; robRelWant = -0.6f; robLat = robLatWant = -1.7f; robFleeing = false; slow = 1f;
        Drive();

        BeginFilm();
        Ending ending = Ending.None;
        bool injured = false;

        // ===== HỒI 1: bị nhắm =====
        robRel = robRelWant = -45f; // bọn cướp còn khuất phía sau
        Drive();
        yield return Shot(3f, Title(), Subtitle(),
            () => (youPos + youDir * 6.5f + Right(youDir) * 1.8f + Vector3.up * 1.4f, youPos - youDir * 8f + Vector3.up * 0.9f),
            () => Say("Bạn", OpeningLine(), YouCol));
        robRel = robRelWant = -26f;
        yield return Shot(2.6f, null, null,
            () => (robPos - robDir * 3.2f + Right(robDir) * 0.7f + Vector3.up * 1.55f, youPos + Vector3.up * 1f),
            () => Say("Tên cướp", "Thấy con xe có túi giao hàng kia không? Nó đấy.", RobberCol));
        yield return Shot(2f, null, null,
            () => (robPos - robDir * 3.2f + Right(robDir) * 0.7f + Vector3.up * 1.55f, youPos + Vector3.up * 1f),
            () => { robRelWant = -14f; Say("Tên cướp 2", CashLine(), RobberCol); });
        yield return Shot(2.6f, null, null,
            () => (youPos + Vector3.up * 1.75f + youDir * 0.25f, youPos - youDir * 20f + Vector3.up * 1f),
            () => Say("Bạn", MirrorLine(), YouCol));

        int pick0 = -1;
        yield return Choose(6f, new[] { "Tăng tốc một chút", "Bình tĩnh, cứ chạy bình thường", "Giảm tốc cho chúng vượt" },
            () => (youPos - youDir * 3.5f + Right(youDir) * 1.2f + Vector3.up * 1.6f, youPos + youDir * 4f + Vector3.up * 1f), i => pick0 = i);
        if (pick0 == 0)
        {
            vYouWant = 12f; robRelWant = -0.6f;
            yield return Shot(2.4f, null, null,
                () => (youPos - youDir * 7f - Right(youDir) * 1.5f + Vector3.up * 1.2f, youPos + Vector3.up * 1f),
                () => Say("Tên cướp", "Nó thấy mình rồi! Bám chặt vào!", RobberCol));
        }
        else if (pick0 == 2)
        {
            vYouWant = 4f; robRelWant = -0.6f;
            yield return Shot(2.4f, null, null,
                () => (youPos - Right(youDir) * 4f + youDir * 1f + Vector3.up * 1.3f, robPos + Vector3.up * 1.2f),
                () => Say("Tên cướp", "Giảm tốc chờ bọn anh à? Lịch sự ghê.", RobberCol));
            vYouWant = 8f;
        }
        else
        {
            robRelWant = -0.6f;
            yield return Shot(2.4f, null, null,
                () => (youPos + youDir * 5f + Vector3.up * 0.6f, youPos - youDir * 6f + Vector3.up * 0.9f),
                () => Say("", "<i>Tiếng máy xe phía sau mỗi lúc một gần... một gần hơn.</i>", Color.white));
        }

        // ===== HỒI 2: áp sát, rút dao =====
        robRelWant = -0.6f; robLatWant = -1.7f;
        yield return Shot(2.8f, null, null,
            () => (youPos - Right(youDir) * 5f + youDir * 2.5f + Vector3.up * 1.5f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 1f),
            () => Say("", "<i>Một chiếc xe máy không biển số áp sát ngay bên cạnh.</i>", Color.white));
        yield return Shot(2.6f, null, null,
            () => (robPos + robDir * 2.2f + Right(robDir) * 0.9f + Vector3.up * 1.5f, robPos + Vector3.up * 1.5f - robDir * 0.45f),
            () => { robLatWant = -1.15f; Say("", KnifeLine(), Color.white); });
        yield return Shot(2.8f, null, null,
            () => (youPos + Vector3.up * 1.75f - youDir * 0.9f + Right(youDir) * 0.4f, robPos + Vector3.up * 1.45f + robDir * 0.2f),
            () => { robLatWant = -1.5f; Say("Tên cướp", "Dừng xe! Đưa gói hàng với tiền đây - NGAY!", RobberCol); });

        // --- Lựa chọn chính: vừa chạy vừa chọn ---
        int pick = -1;
        yield return Choose(8f, new[] { "Vặn ga bỏ chạy", "Đưa gói hàng", "Hô \"Cướp! Cướp! Cứu với!\"", "Đạp xe chúng ra" },
            () => (youPos + Vector3.up * 1.8f - youDir * 2f + Right(youDir) * 0.8f, robPos + Vector3.up * 1.2f + youDir * 2f), i => pick = i);

        // ===== HỒI 3 =====
        switch (pick)
        {
            case 0:
            {
                vYouWant = 15f;
                yield return Shot(3f, null, null,
                    () => (youPos - Right(youDir) * 3.4f - youDir * 2f + Vector3.up * 1.2f, youPos + youDir * 2f + Vector3.up * 0.9f),
                    () => { robRelWant = -0.4f; robLatWant = -1.4f; Say("Tên cướp", "Nó chạy kìa! Đuổi theo!", RobberCol); AudioManager.Play("alarm", 0.12f); });
                yield return Shot(2f, null, null,
                    () => (youPos - youDir * 10f + Right(youDir) * 2f + Vector3.up * 4.5f, youPos + youDir * 6f),
                    () => Say("", ChaseLine(), Color.white));
                yield return Shot(2.2f, null, null,
                    () => (youPos + youDir * 3f - Right(youDir) * 0.4f + Vector3.up * 1.2f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 1.2f),
                    () => { robLatWant = -0.95f; Say("Tên cướp", "Cắt quai túi nó đi!", RobberCol); });

                // Nhát dao vào quai túi
                int dodge = -1;
                yield return Choose(4f, new[] { "Lạng xe tránh", "Thúc cùi chỏ vào tay cầm dao", "Cúi rạp người, bám chặt tay lái" },
                    () => (youPos + youDir * 2.6f + Right(youDir) * 0.6f + Vector3.up * 1.4f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 1.3f), i => dodge = i);
                float d = Random.value;
                bool safe = (dodge == 0 && d < 0.7f) || (dodge == 1 && d < 0.5f) || (dodge == 2 && d < 0.6f);
                // Khoảnh khắc nhát chém: chậm hình, máy quay sát giữa hai xe
                Show(passPose, "slash");
                if (dodge == 0 && safe) youLatWant = 1.4f;
                if (dodge == 1) Show(youPose, "elbow");
                if (dodge == 2) Show(youPose, "duck");
                slow = 0.3f; Time.timeScale = 0.3f; shake = 0.2f;
                yield return Shot(1.5f, null, null,
                    () => (Vector3.Lerp(youPos, robPos, 0.5f) + youDir * 2.2f + Vector3.up * 1.25f, Vector3.Lerp(youPos, robPos, 0.55f) + Vector3.up * 1.15f),
                    () => Say("", "<i>Lưỡi dao vung xuống...</i>", Color.white));
                slow = 1f; Time.timeScale = 1f;
                if (!safe)
                {
                    ending = Ending.LostPackage;
                    Show(youPose, "ride"); flash = 0.6f; shake = 0.3f;
                    robLatWant = -2.2f;
                    yield return Shot(2.4f, null, null,
                        () => (youPos - youDir * 2.5f + Vector3.up * 1.5f, robPos + Vector3.up * 1.2f),
                        () => Say("", dodge < 0 ? "<i>Chậm quá. Lưỡi dao cắt đứt quai túi - gói hàng mất rồi.</i>" : "<i>Không kịp. Lưỡi dao cắt đứt quai túi - gói hàng mất rồi.</i>", Color.white));
                    Show(passPose, "threat");
                    yield return Say2("Tên cướp 2", "Được rồi! Chạy, chạy!", RobberCol);
                    break;
                }
                if (dodge == 1) { Show(passPose, "recoil"); shake = 0.3f; }
                robLatWant = -2.6f;
                yield return Shot(2.2f, null, null,
                    () => (youPos - Right(youDir) * 3f + youDir * 0.5f + Vector3.up * 1.1f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 1f),
                    () => Say("", dodge == 0 ? "<i>Bạn lạng xe gấp - lưỡi dao chém vào không khí.</i>"
                                : dodge == 1 ? "<i>Cùi chỏ bạn thúc trúng cổ tay hắn. Hắn ôm tay, suýt rơi cả dao!</i>"
                                : "<i>Bạn cúi rạp xuống - lưỡi dao rít qua ngay trên mũ bảo hiểm.</i>", Color.white));
                Show(youPose, "ride"); Show(passPose, "threat"); youLatWant = 0f;
                yield return Say2("Tên cướp", "Đứng lại, thằng ranh-!", RobberCol);

                // Chướng ngại ngẫu nhiên trên đường (xe thật đặt trên làn)
                int hz = Random.Range(0, 3);
                vYouWant = 9f; robRelWant = -4f; robLatWant = 0f; // bọn cướp tụt ra bám đuôi
                List<GameObject> hazard = SpawnHazard(hz, sYou + 42f);
                string[] hzLine =
                {
                    "<i>Một chiếc taxi từ ngõ lao ra - chắn ngang làn đường!</i>",
                    "<i>Chiếc ô tô phía trước phanh khựng giữa làn, nháy đèn khẩn cấp!</i>",
                    "<i>Hai chiếc ô tô đỗ song song chắn đường - chỉ còn một khe hẹp ở giữa!</i>",
                };
                string[][] hzOpt =
                {
                    new[] { "Lách qua khe hở", "Leo lên vỉa hè" },
                    new[] { "Lạng vòng qua", "Phanh gấp" },
                    new[] { "Luồn qua khe giữa hai xe", "Cắt qua vỉa hè" },
                };
                yield return Shot(2.2f, null, null,
                    () => (youPos - youDir * 3.5f + Right(youDir) * 0.6f + Vector3.up * 1.7f, youPos + youDir * 14f + Vector3.up * 0.8f),
                    () => Say("", hzLine[hz], Color.white));
                int hpick = -1;
                yield return Choose(3.5f, hzOpt[hz],
                    () => (youPos - youDir * 2.5f + Vector3.up * 1.9f, youPos + youDir * 10f + Vector3.up * 0.6f), i => hpick = i);
                bool through = hpick >= 0 && Random.value < (hz == 1 && hpick == 1 ? 0.3f : hpick == 0 ? 0.65f : 0.75f);
                if (through)
                {
                    if (hz == 1) youLatWant = -2.6f;                     // vòng qua xe đỗ giữa làn
                    else if (hpick == 1) youLatWant = 2.6f;              // leo lên vỉa hè
                    vYouWant = 12f; robRelWant = -16f; robFleeing = true; // bọn cướp phải phanh gấp
                    yield return Shot(2.6f, null, null,
                        () => (youPos + youDir * 4f - Right(youDir) * 2f + Vector3.up * 1.2f, youPos + Vector3.up * 0.9f),
                        () => Say("", "<i>Bạn lách qua được! Phía sau, bọn cướp phải phanh gấp.</i>", Color.white));
                    youLatWant = 0f; robFleeing = false;
                    yield return Say2("Tên cướp", "Á! Nhanh lên, nó chạy mất bây giờ!", RobberCol);
                    robRelWant = -6f;
                    foreach (GameObject g in hazard) if (g != null) Destroy(g);
                }
                else
                {
                    vYouWant = 0f;
                    yield return Shot(2.4f, null, null,
                        () => (youPos + Right(youDir) * 3f + youDir * 1.5f + Vector3.up * 1.2f, youPos + Vector3.up * 0.8f),
                        () => Say("", hpick < 0 ? "<i>Bạn cứng người - phanh trượt, dừng cách đầu xe chỉ một gang tay.</i>" : (Wet ? "<i>Bánh xe trượt trên mặt đường ướt. Xe bạn trượt dài rồi dừng hẳn.</i>" : "<i>Bánh xe trượt trên vệt cát. Xe bạn trượt dài rồi dừng hẳn.</i>"), Color.white));
                    foreach (GameObject g in hazard) if (g != null) Destroy(g);
                    yield return CaughtUp("Hết đường chạy rồi nhé, shipper.", (e, hurt) => { ending = e; injured = hurt; });
                    break;
                }

                int pick2 = -1;
                yield return Choose(5f, new[] { "Rẽ vào ngõ hẹp", "Phóng về chốt CSGT", "Phanh gấp cho chúng vượt qua" },
                    () => (youPos + youDir * 5.5f + Right(youDir) * 1.3f + Vector3.up * 1.3f, Vector3.Lerp(youPos, robPos, 0.4f) + Vector3.up * 1f), i => pick2 = i);
                float luck = Random.value - 0.1f; // vừa cắt đuôi được qua chướng ngại -> dễ thoát hơn
                if (pick2 == 0 && luck < 0.7f) ending = Ending.Escaped;
                else if (pick2 == 1 && luck < 0.85f) ending = Ending.Arrested;
                else if (pick2 == 2 && luck < 0.55f) ending = Ending.Escaped;
                else ending = Ending.Robbed;

                if (pick2 == 0 && ending == Ending.Escaped)
                    yield return AlleyEscape();
                else if (pick2 == 1 && ending == Ending.Arrested)
                    yield return Cut("Hai dãy phố sau...", "Tiếng còi. Đèn xanh đỏ nhấp nháy. CSGT bước ra vẫy gậy chặn bọn cướp lại.");
                else if (pick2 == 2 && ending == Ending.Escaped)
                {
                    vYouWant = 2f; robRelWant = 40f; robFleeing = true;
                    yield return Shot(2.6f, null, null,
                        () => (youPos + Right(youDir) * 4f + youDir * 1f + Vector3.up * 1.3f, youPos + youDir * 6f + Vector3.up * 0.8f),
                        () => Say("", "<i>Lốp rít lên. Chúng vụt thẳng qua mặt bạn.</i>", Color.white));
                    vYouWant = 7f;
                }
                else yield return CaughtUp(pick2 < 0 ? "<i>Bạn chần chừ chậm mất một giây.</i>" : null, (e, hurt) => { ending = e; injured = hurt; });
                break;
            }
            case 1:
                ending = Ending.LostPackage;
                vYouWant = 3f; robRelWant = 0f; robLatWant = -1.2f;
                yield return Shot(2.8f, null, null,
                    () => (youPos + Right(youDir) * 2.6f + youDir * 2f + Vector3.up * 1.5f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 1.1f),
                    () => Say("Bạn", "Được rồi! Được rồi... cầm lấy đi.", YouCol));
                yield return Shot(2.2f, null, null,
                    () => (Vector3.Lerp(youPos, robPos, 0.5f) + youDir * 1.6f + Vector3.up * 1.2f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 1f),
                    () => Say("", "<i>Một bàn tay đeo găng giật phăng chiếc túi khỏi vai bạn.</i>", Color.white));
                yield return Say2("Tên cướp", "Khôn đấy. Cứ thế mà đi, đừng ngoái lại.", RobberCol);
                break;
            case 2:
            {
                int people = 0;
                foreach (PedestrianWalker w in FindObjectsByType<PedestrianWalker>())
                    if (w != null && (w.transform.position - youPos).sqrMagnitude < 40f * 40f) people++;
                bool helped = people >= 2 || Random.value < 0.35f;
                vYouWant = 6f;
                yield return Shot(2.2f, null, null,
                    () => (youPos + youDir * 3.5f - Right(youDir) * 1.2f + Vector3.up * 1.5f, youPos + Vector3.up * 1.5f),
                    () => Say("Bạn", "CƯỚP! CƯỚP! Ai cứu với!", YouCol));
                if (helped)
                {
                    ending = Ending.Escaped;
                    yield return Say2("", $"<i>{(people >= 2 ? "Người trong các cửa hàng chạy ùa ra, la ó" : (Dark ? "Một ô cửa bật đèn, cánh cửa đập mạnh mở ra" : "Một ô cửa sổ bật mở, có người ló ra quát lớn"))}. Bọn cướp hoảng hốt.</i>", Color.white);
                }
                else yield return CaughtUp("Chẳng ai tới đâu, thằng ngốc. Đưa đây!", (e, hurt) => { ending = e; injured = hurt; });
                break;
            }
            case 3:
            {
                bool hit = Random.value < 0.55f;
                robLatWant = -1.05f; robRelWant = 0.2f;
                yield return Shot(1.4f, null, null,
                    () => (youPos - youDir * 3f + Vector3.up * 1.1f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 0.7f),
                    () => Say("Bạn", "Tránh xa tao ra!", YouCol));
                // Cú đạp: chậm hình, góc máy thấp giữa hai xe
                Show(youPose, "kick"); slow = 0.3f; Time.timeScale = 0.3f; shake = 0.25f;
                yield return Shot(1.6f, null, null,
                    () => (Vector3.Lerp(youPos, robPos, 0.5f) + youDir * 2.4f + Vector3.up * 0.55f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 0.6f - youDir * 0.3f),
                    () => Say("", "<i>Bạn dồn hết sức, đạp thẳng vào sườn xe chúng!</i>", Color.white));
                slow = 1f; Time.timeScale = 1f;
                Show(youPose, "ride");
                if (hit)
                {
                    ending = Ending.Escaped;
                    robDown = true; robRoll = robLean; robRollWant = 82f; robSpinWant = -35f;
                    robLatWant = -3.2f; robRelWant = -35f; robFleeing = true; vYouWant = 13f;
                    Show(passPose, "disarmed"); shake = 0.4f;
                    yield return Shot(2.6f, null, null,
                        () => (youPos + youDir * 1.5f - Right(youDir) * 5.5f + Vector3.up * 1.6f, robPos + Vector3.up * 0.4f),
                        () => Say("", $"<i>Xe chúng chao đảo, đổ nghiêng, trượt dài trên {Road()}!</i>", Color.white));
                    yield return Shot(2.2f, null, null,
                        () => (youPos + Vector3.up * 1.7f - youDir * 1.2f + Right(youDir) * 0.6f, robPos + Vector3.up * 0.5f),
                        () => Say("Tên cướp", "Á! Chân tao! Đuổi theo nó mau!", RobberCol));
                }
                else
                {
                    robLatWant = -2.7f; // chúng lạng tránh
                    yield return Shot(1.6f, null, null,
                        () => (youPos - youDir * 3.5f - Right(youDir) * 1.5f + Vector3.up * 1.4f, robPos + Vector3.up * 1f),
                        () => Say("", "<i>Hụt! Chúng lạng xe tránh được.</i>", Color.white));
                    robLatWant = -1.2f; Show(passPose, "slash"); shake = 0.2f;
                    yield return Shot(1.6f, null, null,
                        () => (youPos + youDir * 2.2f + Right(youDir) * 0.4f + Vector3.up * 1.4f, robPos + Vector3.up * 1.3f),
                        () => Say("Tên cướp 2", "Mày muốn chết à?!", RobberCol));
                    Show(passPose, "threat");
                    yield return CaughtUp("Ngu thế. Giờ thì nôn cả tiền ra!", (e, hurt) => { ending = e; injured = hurt; });
                }
                break;
            }
            default:
                yield return CaughtUp("<i>Bạn đứng hình. Chúng tạt ngang chặn đầu.</i>", (e, hurt) => { ending = e; injured = hurt; });
                break;
        }

        // Kết: bọn cướp phóng đi
        if (bike != null && robDown)
        {
            robRelWant = -70f;
            yield return Shot(2.2f, null, null,
                () => (youPos + Vector3.up * 1.6f - youDir * 2.5f + Right(youDir) * 1f, robPos + Vector3.up * 0.4f),
                () => Say("", "<i>Phía sau, bọn cướp lồm cồm dựng xe dậy - không đuổi nổi nữa.</i>", Color.white));
        }
        else if (bike != null && ending != Ending.Arrested)
        {
            bool escaped = ending == Ending.Escaped;
            robFleeing = true;
            robRelWant = robRelWant >= 0f ? 70f : -45f; // đang ở trước thì phóng tiếp lên trước, ở sau thì tụt lại
            robLatWant = robRelWant > 0f ? -1.5f : -2.6f;
            if (!escaped) vYouWant = 2f;
            yield return Shot(2.4f, null, null,
                () => (youPos + Vector3.up * 1.6f - youDir * 2.5f + Right(youDir) * 1f, bike != null ? robPos + Vector3.up : youPos + youDir * 10f),
                () => Say(escaped ? "Tên cướp" : "", escaped ? "Mẹ kiếp! Thôi bỏ đi, chuồn!" : AwayLine(),
                          escaped ? RobberCol : Color.white));
        }
        // Lời kết
        vYouWant = ending == Ending.Escaped || ending == Ending.Arrested ? 6f : 0f;
        yield return Shot(3f, null, null,
            () => (youPos + youDir * 3.2f + Right(youDir) * 0.9f + Vector3.up * 1.6f, youPos + Vector3.up * 1.5f),
            () => Say("Bạn",
                ending == Ending.Arrested ? "Bắt được rồi... bắt được thật rồi. Thở đi nào. Đi giao nốt đơn thôi."
                : ending == Ending.Escaped ? "Tay vẫn còn run... Thôi, đi giao nốt đơn."
                : injured ? "Á... cái tay. Còn sống là may rồi. Giờ biết nói sao với khách đây?"
                : "...Giờ biết nói sao với khách đây?", YouCol));

        EndFilm();
        if (bike != null) Destroy(bike.gameObject);
        if (standIn != null) Destroy(standIn.gameObject);
        if (flyer != null) Destroy(flyer.gameObject);
        flyer = null;
        bike = standIn = null;
        car.enabled = true;
        if (rb != null) { rb.isKinematic = wasKinematic; rb.linearVelocity = Vector3.zero; }
        Apply(ending, injured);
        lastNight = ending;
        rail = null;
        Playing = false;
    }

    /// <summary>
    /// Bị đuổi kịp: bọn cướp vượt lên chặn đầu, hai xe dừng, chúng đòi hàng + ví. Lựa chọn cuối: đưa hết / đánh trả / doạ có công an.
    /// </summary>
    private IEnumerator CaughtUp(string line, System.Action<Ending, bool> result)
    {
        // Chúng ép sát bên trái, nhỉnh lên trước một chút, buộc hai xe dừng lại
        Show(passPose, "threat"); Show(youPose, "ride");
        robRelWant = 1.3f; robLatWant = -1.35f; robFleeing = false; vYouWant = 0f;
        yield return Shot(2.6f, null, null,
            () => (youPos - Right(youDir) * 4.5f + youDir * 2.5f + Vector3.up * 1.4f, Vector3.Lerp(youPos, robPos, 0.5f) + Vector3.up * 0.9f),
            () =>
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("<i>")) Say("", string.IsNullOrEmpty(line) ? "<i>Chúng ép sát, buộc bạn phải dừng xe.</i>" : line, Color.white);
                else Say("Tên cướp", line, RobberCol);
            });
        yield return Shot(2.6f, null, null,
            () => (youPos + Vector3.up * 1.7f - youDir * 0.5f + Right(youDir) * 0.3f, robPos + Vector3.up * 1.5f),
            () => Say("Tên cướp", "Gói hàng. Cả ví nữa. Từ từ thôi. Đừng có làm anh hùng.", RobberCol));
        int last = -1;
        yield return Choose(5f, new[] { "Đưa hết cho chúng", "Đánh trả", "Doạ: \"Công an đang ở ngay sau tôi!\"" },
            () => (youPos - Right(youDir) * 2.2f + youDir * 1.2f + Vector3.up * 1.5f, robPos + Vector3.up * 1.3f), i => last = i);
        float r = Random.value;
        if (last == 1)
        {
            // Cú đấm: chậm hình, cận cảnh
            Show(youPose, "punch"); slow = 0.3f; Time.timeScale = 0.3f; shake = 0.3f;
            yield return Shot(1.5f, null, null,
                () => (Vector3.Lerp(youPos, robPos, 0.5f) + youDir * 2f + Vector3.up * 1.35f, Vector3.Lerp(youPos, robPos, 0.6f) + Vector3.up * 1.3f),
                () => Say("Bạn", "Cút đi!", YouCol));
            slow = 1f; Time.timeScale = 1f;
            if (r < 0.35f)
            {
                Show(passPose, "disarmed"); shake = 0.35f;
                if (bike != null) Throw(bike.TransformPoint(knifeHandLocal), -Right(robDir) * 2.5f + robDir * 1.5f + Vector3.up * 4f);
                yield return Shot(2.2f, null, null,
                    () => (youPos - youDir * 3f + Right(youDir) * 1.5f + Vector3.up * 1.2f, flyer != null ? flyer.position : robPos + Vector3.up),
                    () => Say("", "<i>Cú đấm trúng mặt hắn. Con dao văng xoay tròn rồi rơi loảng xoảng xuống đường.</i>", Color.white));
                Show(youPose, "ride");
                yield return Say2("Tên cướp", "Thằng này điên rồi! Chuồn!", RobberCol);
                result(Ending.Escaped, false);
            }
            else
            {
                Show(passPose, "slash"); Show(youPose, "hurt"); flash = 1f; shake = 0.55f;
                yield return Shot(2.4f, null, null,
                    () => (youPos + Right(youDir) * 2.2f + youDir * 2.2f + Vector3.up * 1.3f, Vector3.Lerp(youPos, robPos, 0.4f) + Vector3.up * 1.1f),
                    () => Say("", "<i>Hắn né được, quật trả một cú trúng tay bạn. Đau điếng.</i>", Color.white));
                Show(passPose, "threat");
                yield return Say2("Tên cướp", "Đã bảo đừng làm anh hùng. Đưa đây!", RobberCol);
                result(Ending.Robbed, true);
            }
        }
        else if (last == 2 && r < 0.4f)
        {
            yield return Shot(2.6f, null, null,
                () => (robPos + robDir * 3f + Vector3.up * 1.4f, robPos + Vector3.up * 1.4f),
                () => Say("", "<i>Chúng ngoái lại nhìn cuối phố... rồi vọt mất.</i>", Color.white));
            result(Ending.Escaped, false);
        }
        else
        {
            yield return Say2("Tên cướp", last == 2 ? "Doạ ai đấy. Ví. Ngay." : "Ngoan đấy.", RobberCol);
            result(Ending.Robbed, false);
        }
    }

    // ================= Vật bay (dao bị đánh văng) =================
    private Transform flyer;
    private Vector3 flyVel;

    private void Throw(Vector3 from, Vector3 velocity)
    {
        if (flyer != null) Destroy(flyer.gameObject);
        var g = new GameObject("FlyingKnife");
        g.transform.SetParent(transform, false);
        g.transform.position = from;
        g.AddComponent<MeshFilter>().sharedMesh = Knife(false);
        g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        flyer = g.transform; flyVel = velocity;
    }

    private void UpdateFlyer(float dt)
    {
        if (flyer == null) return;
        flyVel.y -= 9.8f * dt;
        Vector3 p = flyer.position + flyVel * dt;
        float floor = VietDistrict.RoadTop + 0.02f;
        if (p.y < floor) { p.y = floor; flyVel = new Vector3(flyVel.x * 0.5f, Mathf.Abs(flyVel.y) * 0.3f, flyVel.z * 0.5f); }
        flyer.position = p;
        if (flyVel.sqrMagnitude > 0.2f) flyer.Rotate(new Vector3(720f, 90f, 0f) * dt, Space.Self);
    }

    /// <summary>
    /// Cắt đuôi: người chơi bẻ lái rẽ gấp ở ngã tư gần nhất (đường rẽ thật của bản đồ, xe nghiêng vào cua),
    /// bọn cướp đang lao nhanh trượt qua ngã tư, phanh gấp, xe xoay ngang; máy quay bám theo người chơi vào phố mới.
    /// </summary>
    private IEnumerator AlleyEscape()
    {
        Vector3 startDir = youDir;
        Rail turnRail = BuildRail(cityRef, youPos, youDir, out float newLane, true);
        if (turnRail == null)
        {
            yield return Cut("Vài khúc rẽ sau...", "Trái, phải, luồn dưới dây phơi quần áo... Bạn cắt đuôi chúng trong mê cung ngõ nhỏ.");
            yield break;
        }
        // Bọn cướp ở lại trên đường cũ, chạy thẳng với tốc độ hiện tại
        robRail = rail; sRob = sYou + robRel; vRob = vYouWant = Mathf.Max(vYou, 12f); vRobWant = vRob + 1f;
        rail = turnRail; lane = newLane;
        sYou = rail.Project(youPos); youLat = youLatWant = 0f;
        vYouWant = 11f;
        Vector3 corner = youPos; bool cornerSet = false;
        bool Turned() { return Vector3.Angle(startDir, youDir) > 55f; }

        // 1. Lao tới ngã tư, bẻ lái: máy quay thấp phía trước - bên trong khúc cua
        yield return Shot(6f, null, null,
            () => (youPos + youDir * 5f - Right(youDir) * 1.2f + Vector3.up * 0.7f, youPos + Vector3.up * 0.9f),
            () => Say("", "<i>Bạn đột ngột bẻ lái, ôm cua gắt vào con phố ngang...</i>", Color.white),
            () =>
            {
                if (!cornerSet && Vector3.Angle(startDir, youDir) > 20f) { corner = youPos; cornerSet = true; }
                return Turned();
            });
        if (!cornerSet) corner = youPos;
        // 2. Bọn cướp trượt qua ngã tư: phanh gấp, xe xoay ngang
        vRobWant = 0f; robSpinWant = 40f; shake = 0.2f;
        Vector3 camAt = corner - startDir * 2f + Right(startDir) * 3f + Vector3.up * 1.4f;
        yield return Shot(2.6f, null, null,
            () => (camAt, robPos + Vector3.up * 0.9f),
            () => Say("", "<i>Phía sau, bọn cướp lao vụt qua ngã tư - phanh rít lên, xe xoay ngang.</i>", Color.white));
        yield return Shot(1.8f, null, null,
            () => (camAt, robPos + Vector3.up * 1.2f),
            () => Say("Tên cướp", "Nó rẽ đâu rồi?! Mất dấu rồi!", RobberCol));
        // 3. Bám theo người chơi trên phố mới
        vYouWant = 8f;
        yield return Shot(2.6f, null, null,
            () => (youPos - youDir * 5f + Right(youDir) * 1.4f + Vector3.up * 1.6f, youPos + youDir * 6f + Vector3.up * 0.8f),
            () => Say("Bạn", "Hộc... hộc... cắt đuôi được rồi.", YouCol));
        if (bike != null) { Destroy(bike.gameObject); bike = null; }
        robRail = null;
    }

    /// <summary>Xe chắn đường cho cảnh rượt đuổi: 0 taxi lao ra chắn chéo làn, 1 xe dừng giữa làn, 2 hai xe song song.</summary>
    private List<GameObject> SpawnHazard(int kind, float at)
    {
        var list = new List<GameObject>();
        void Car(int variant, float sOff, float lat, float yaw, bool hazardLights)
        {
            CarModels.Info info = CarModels.Get(variant);
            rail.Sample(at + sOff, lat, out Vector3 p, out Vector3 d);
            var go = new GameObject("Hazard_" + info.kind);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(d) * Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = info.lod0;
            go.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
            if (hazardLights)
                foreach (float sx in new[] { -1f, 1f })
                {
                    var l = new GameObject("Blinker").AddComponent<Light>();
                    l.transform.SetParent(go.transform, false);
                    l.transform.localPosition = new Vector3(sx * info.size.x * 0.4f, info.size.y * 0.55f, -info.size.z * 0.5f - 0.2f);
                    l.type = LightType.Point; l.range = 4f; l.intensity = 3f; l.color = new Color(1f, 0.6f, 0.1f);
                }
            list.Add(go);
        }
        switch (kind)
        {
            case 0: Car(2, 0f, -2.4f, -55f, false); break;                    // taxi lao ra chắn chéo nửa trái làn
            case 1: Car(Random.Range(0, 2), 0f, 0.2f, 0f, true); break;       // xe dừng giữa làn, bật đèn khẩn cấp
            default: Car(1, 0f, -2.7f, 0f, false); Car(3, 1.5f, 2.7f, 0f, false); break; // hai xe song song, khe hẹp ở giữa
        }
        return list;
    }

    private static Vector3 Right(Vector3 d) => new Vector3(d.z, 0f, -d.x);

    /// <summary>Kết quả vào tiền / đơn hàng / XP.</summary>
    private static void Apply(Ending e, bool injured)
    {
        EconomyManager eco = EconomyManager.Instance;
        switch (e)
        {
            case Ending.Escaped:
                if (eco != null) eco.AddIncome(25f, MoneyCategory.Other, "Brave rider bonus");
                StoreProgress.AddXP(6);
                ProgressHUD.Toast("<color=#6BE37B>Thoát rồi! Gói hàng vẫn an toàn - đi giao nốt nhé. Thưởng shipper dũng cảm +$25.</color>");
                break;
            case Ending.Arrested:
                if (eco != null) eco.AddIncome(80f, MoneyCategory.Other, "Police reward");
                StoreProgress.AddXP(12);
                ProgressHUD.Toast("<color=#6BE37B>Bọn cướp đã bị bắt! CSGT thưởng +$80. Đi giao nốt đơn hàng nhé.</color>");
                break;
            case Ending.LostPackage:
                if (DeliveryManager.Instance != null) DeliveryManager.Instance.LoseToRobbery();
                ProgressHUD.Toast("<color=#FF8A7A>Bạn đã đưa gói hàng. Người không sao, nhưng mất đơn.</color>");
                break;
            case Ending.Robbed:
                float cash = eco != null ? Mathf.Round(Mathf.Clamp(eco.Money * 0.1f, 20f, 80f)) + (injured ? 40f : 0f) : 0f;
                if (eco != null && cash > 0f) eco.Charge(cash, MoneyCategory.Other, "Robbed during a night delivery");
                if (DeliveryManager.Instance != null) DeliveryManager.Instance.LoseToRobbery();
                AudioManager.Play("alarm", 0.5f);
                ProgressHUD.Toast(injured
                    ? $"<color=#FF8A7A>Bị cướp và bị thương! Mất gói hàng và {EconomyManager.Format(cash)} (gồm $40 tiền khám).</color>"
                    : $"<color=#FF8A7A>Bị cướp! Mất gói hàng và {EconomyManager.Format(cash)} tiền mặt.</color>");
                break;
        }
    }

    // ================= Model =================
    private static readonly Color GearDark = new Color(0.07f, 0.07f, 0.08f), MaskBlue = new Color(0.35f, 0.55f, 0.75f),
        Steel = new Color(0.78f, 0.8f, 0.82f), Wood = new Color(0.42f, 0.28f, 0.16f), Grip = new Color(0.12f, 0.1f, 0.09f);

    /// <summary>Các tư thế dựng sẵn (mỗi tư thế 1 object, chỉ bật 1 cái) - đổi tư thế = cắt cảnh hành động.</summary>
    private readonly Dictionary<string, GameObject> passPose = new Dictionary<string, GameObject>(), youPose = new Dictionary<string, GameObject>();
    private Vector3 knifeHandLocal; // để cho dao bay ra khỏi tay khi bị đánh

    private static void Show(Dictionary<string, GameObject> set, string key)
    {
        if (!set.ContainsKey(key)) return;
        foreach (var kv in set) if (kv.Value != null) kv.Value.SetActive(kv.Key == key);
    }

    /// <summary>Đặt model đã tạo tư thế lên xe: khớp hông trùng hipsAt (toạ độ của parent).</summary>
    private static GameObject PoseObj(RealModels.Model m, Transform parent, Vector3 hipsAt, string name)
    {
        GameObject g = RealModels.Spawn(m, parent, Vector3.zero, Quaternion.identity, name, true, 0.04f, 0.004f);
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = Vector3.one;
        g.transform.localPosition = hipsAt - m.hips;
        return g;
    }

    /// <summary>
    /// Gắn đồ cầm tay vào lòng bàn tay: chuôi đặt ở giữa lòng bàn tay (cổ tay + 7 cm theo hướng cẳng tay),
    /// thân dao / gậy vuông góc với cẳng tay, ngả theo hướng gợi ý axisHint (toạ độ model).
    /// </summary>
    private static void Hold(GameObject poseObj, RealModels.Model m, bool right, Mesh lod0, Mesh lod1, Vector3 axisHint)
    {
        Vector3 hand = right ? m.handR : m.handL, elbow = right ? m.elbowR : m.elbowL;
        Vector3 f = hand - elbow;
        f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        Vector3 axis = Vector3.ProjectOnPlane(axisHint.normalized, f);
        axis = axis.sqrMagnitude > 0.05f ? axis.normalized : axisHint.normalized;
        Vector3 grip = hand + f * 0.07f;
        RealModels.AddTo(poseObj, Single(lod0, lod1), Matrix4x4.TRS(grip, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one), true);
    }

    /// <summary>
    /// Hai tên cướp trên một xe máy (không biển số): KHÔNG mũ bảo hiểm, mũ len đen, khẩu trang, quần áo tối.
    /// Tên ngồi sau có các tư thế: threat (dao giơ ngang vai, gậy thả cạnh đùi), slash (vung dao chém sang phải),
    /// recoil (giật lùi, dao vẫn trong tay), disarmed (bị đánh văng dao, hai tay che mặt).
    /// </summary>
    private Transform SpawnRobbers(VietDistrict city)
    {
        int v = Random.Range(0, BikeModels.FirstElectric);
        BikeModels.Info info = BikeModels.Get(v);
        GameObject go = RealModels.Spawn(Single(info.lod0, info.lod1), transform, Vector3.zero, Quaternion.identity, "Robbers", true, 0.04f, 0.004f);
        GameObject[] people = city.peopleModels, mats = city.peopleMaterials;
        RealModels.Model gear = Single(Gear(false), Gear(true));
        string kind = info.scooter ? "scooter" : "wave";
        int a = Random.Range(0, people.Length), b = (a + 1 + Random.Range(0, Mathf.Max(1, people.Length - 1))) % people.Length;
        GameObject matA = mats != null && a < mats.Length ? mats[a] : null, matB = mats != null && b < mats.Length ? mats[b] : null;

        // Tên lái
        RealModels.Model rider = RealModels.Person(people[a], matA, RealModels.Pose.Ride, MotorbikeTraffic.RiderIK(info), kind);
        if (rider != null && rider.ok)
        {
            GameObject r = PoseObj(rider, go.transform, info.seat, "Driver");
            RealModels.AddTo(r, gear, Matrix4x4.TRS(rider.head, Quaternion.identity, Vector3.one), true);
            Darken(r.transform, 0);
        }
        // Tên ngồi sau: nhiều tư thế
        Vector3 footL = info.pegL - info.rearSeat, footR = info.pegR - info.rearSeat;
        int passTint = Random.Range(1, RobberTints.Length);
        Mesh k0 = Knife(false), k1 = Knife(true), s0 = Stick(false), s1 = Stick(true);
        void Variant(string key, Vector3 hl, Vector3 hr, bool knife, bool stick, Vector3 knifeAxis, Vector3 stickAxis)
        {
            var ik = new RealModels.IK { on = true, handL = hl, handR = hr, footL = footL, footR = footR };
            RealModels.Model m = RealModels.Person(people[b], matB, RealModels.Pose.RideBack, ik, $"robber_{kind}_{key}");
            if (m == null || !m.ok) return;
            GameObject p = PoseObj(m, go.transform, info.rearSeat, "Passenger_" + key);
            RealModels.AddTo(p, gear, Matrix4x4.TRS(m.head, Quaternion.identity, Vector3.one), true);
            if (knife) Hold(p, m, true, k0, k1, knifeAxis);
            if (stick) Hold(p, m, false, s0, s1, stickAxis);
            Darken(p.transform, passTint);
            passPose[key] = p;
            if (key == "threat") knifeHandLocal = info.rearSeat + (m.handR - m.hips);
        }
        Variant("threat", new Vector3(-0.28f, 0.02f, 0.14f), new Vector3(0.44f, 0.42f, 0.12f), true, true, new Vector3(0.15f, 0.9f, 0.45f), new Vector3(0f, -0.4f, -1f));
        Variant("slash", new Vector3(-0.17f, 0.02f, 0.3f), new Vector3(0.72f, 0.22f, 0.32f), true, false, new Vector3(1f, 0.15f, 0.45f), Vector3.up);
        Variant("recoil", new Vector3(-0.2f, 0.45f, 0.1f), new Vector3(0.22f, 0.6f, -0.02f), true, false, new Vector3(0.2f, 1f, -0.2f), Vector3.up);
        Variant("disarmed", new Vector3(-0.12f, 0.58f, 0.16f), new Vector3(0.12f, 0.58f, 0.16f), false, false, Vector3.up, Vector3.up);
        Show(passPose, "threat");
        // Đèn pha để thấy xe áp sát trong bóng tối
        var lamp = new GameObject("Headlight").AddComponent<Light>();
        lamp.transform.SetParent(go.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 1f, 0.7f);
        lamp.type = LightType.Spot;
        lamp.spotAngle = 60f;
        lamp.range = 22f;
        lamp.intensity = 6f;
        lamp.color = new Color(1f, 0.95f, 0.8f);
        lamp.enabled = Dark || wx != WeatherSystem.Weather.Sunny;
        return go.transform;
    }

    private static readonly Dictionary<(Material, int), Material> darkMats = new Dictionary<(Material, int), Material>();
    private static readonly Color[] RobberTints =
    {
        new Color(0.95f, 0.95f, 1f),   // xám than
        new Color(0.85f, 1f, 0.8f),    // xanh rêu
        new Color(0.8f, 0.9f, 1.15f),  // xanh đen
    };

    /// <summary>
    /// Quần áo bọn cướp: bớt rực, tối đi ~một nửa, ngả theo tông riêng của từng tên (vẫn giữ nếp vải / hoa văn của texture).
    /// Da, tóc, mắt, răng gần như giữ nguyên. Bỏ qua xe, mũ len, khẩu trang, dao gậy (vật liệu bảng màu).
    /// </summary>
    private static void Darken(Transform root, int tint)
    {
        Material palette = VPalette.Material;
        Color t = RobberTints[((tint % RobberTints.Length) + RobberTints.Length) % RobberTints.Length];
        foreach (Renderer rd in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] ms = rd.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < ms.Length; i++)
            {
                Material m = ms[i];
                if (m == null || m == palette) continue;
                if (!darkMats.TryGetValue((m, tint), out Material dm) || dm == null)
                {
                    string n = (m.name + " " + rd.name).ToLowerInvariant();
                    bool skin = n.Contains("skin") || n.Contains("face") || n.Contains("head") || n.Contains("hair") || n.Contains("eye")
                                || n.Contains("teeth") || n.Contains("tongue") || n.Contains("lash") || n.Contains("brow");
                    dm = new Material(m) { name = m.name + "_Robber" + tint, hideFlags = HideFlags.DontSave };
                    Color Tone(Color c)
                    {
                        if (skin) return new Color(c.r * 0.92f, c.g * 0.9f, c.b * 0.9f, c.a);
                        float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        Color d = Color.Lerp(new Color(g, g, g, c.a), c, 0.35f) * 0.55f;   // bớt rực + tối đi
                        return new Color(d.r * t.r, d.g * t.g, d.b * t.b, c.a);
                    }
                    if (dm.HasProperty("_BaseColor")) dm.SetColor("_BaseColor", Tone(dm.GetColor("_BaseColor")));
                    if (dm.HasProperty("_Color")) dm.SetColor("_Color", Tone(dm.GetColor("_Color")));
                    darkMats[(m, tint)] = dm;
                }
                ms[i] = dm; changed = true;
            }
            if (changed) rd.sharedMaterials = ms;
        }
    }

    /// <summary>Tìm chi tiết theo tên trong xe người chơi và lấy khung bao của nó (toạ độ xe).</summary>
    private bool PartBounds(string name, out Bounds b)
    {
        b = default;
        foreach (Transform t in car.GetComponentsInChildren<Transform>(true))
            if (t.name == name && ModelFit.MeshBounds(t, car.transform, out b, true)) return true;
        return false;
    }

    /// <summary>
    /// Nhân vật người chơi (model Remy đang ẩn khi ngồi xe) ngồi trên xe trong đoạn phim, nhiều tư thế:
    /// ride (2 tay nắm đúng 2 tay nắm xe), kick (đạp chân trái sang), elbow (thúc cùi chỏ trái), punch (đấm tay trái),
    /// duck (cúi rạp), hurt (ôm tay). Tay nắm / gác chân đo từ chi tiết PIPE_COMP_STRG_HANDLE / RUBBER_STEP của xe Cub.
    /// </summary>
    private Transform SpawnStandIn(VietDistrict city)
    {
        MotorcycleInteraction mi = car.GetComponentInChildren<MotorcycleInteraction>(true);
        if (mi == null) mi = car.GetComponentInParent<MotorcycleInteraction>();
        if (mi == null || mi.driverSeat == null) return null;
        Transform ct = car.transform;
        Vector3 hipsW = mi.driverSeat.position + Vector3.up * 0.06f;
        if ((ct.InverseTransformPoint(hipsW)).magnitude > 3f) return null;
        Quaternion inv = Quaternion.Inverse(ct.rotation);
        Vector3 Rel(Vector3 world) => inv * (world - hipsW);   // đích IK: từ hông, theo hướng xe

        // Tay nắm: hai đầu ống tay lái; gác chân: hai đầu cao su gác chân
        Vector3 gripL = new Vector3(-0.3f, 0.3f, 0.5f), gripR = new Vector3(0.3f, 0.3f, 0.5f);
        Vector3 footL = new Vector3(-0.17f, -0.58f, 0.2f), footR = new Vector3(0.17f, -0.58f, 0.2f);
        if (PartBounds("PIPE_COMP_STRG_HANDLE", out Bounds hb) && hb.size.x > 0.3f)
        {
            float y = hb.center.y + hb.extents.y * 0.3f;
            gripL = Rel(ct.TransformPoint(new Vector3(hb.min.x + 0.06f, y, hb.center.z - hb.extents.z * 0.3f)));
            gripR = Rel(ct.TransformPoint(new Vector3(hb.max.x - 0.06f, y, hb.center.z - hb.extents.z * 0.3f)));
        }
        if (PartBounds("RUBBER_STEP", out Bounds fb) && fb.size.x > 0.2f)
        {
            footL = Rel(ct.TransformPoint(new Vector3(fb.min.x + 0.07f, fb.max.y + 0.04f, fb.center.z)));
            footR = Rel(ct.TransformPoint(new Vector3(fb.max.x - 0.07f, fb.max.y + 0.04f, fb.center.z)));
        }

        GameObject[] people = city.peopleModels, mats = city.peopleMaterials;
        Animator body = mi.player != null ? mi.player.GetComponentInChildren<Animator>(true) : null;
        GameObject src = body != null && body.isHuman ? body.gameObject : people[1 % people.Length];
        GameObject srcMat = src == (body != null ? body.gameObject : null) ? null : (mats != null && mats.Length > 1 ? mats[1] : null);

        var root = new GameObject("RiderStandIn").transform;
        root.SetParent(ct, false);
        root.localRotation = Quaternion.identity;
        Vector3 ls = ct.lossyScale;
        root.localScale = new Vector3(1f / Mathf.Max(0.001f, ls.x), 1f / Mathf.Max(0.001f, ls.y), 1f / Mathf.Max(0.001f, ls.z));
        root.position = hipsW;                                   // gốc = hông -> PoseObj(hipsAt = 0)
        var helmet = Single(BikeModels.Helmet(new Color(0.1f, 0.5f, 0.35f), false), BikeModels.Helmet(new Color(0.1f, 0.5f, 0.35f), true));
        string id = car.name;
        void Variant(string key, RealModels.Pose pose, Vector3 hl, Vector3 hr, Vector3 fl, Vector3 fr)
        {
            var ik = new RealModels.IK { on = true, handL = hl, handR = hr, footL = fl, footR = fr };
            RealModels.Model m = RealModels.Person(src, srcMat, pose, ik, $"player_{key}_{id}");
            if (m == null || !m.ok) return;
            GameObject g = PoseObj(m, root, Vector3.zero, "You_" + key);
            RealModels.AddTo(g, helmet, Matrix4x4.TRS(m.head + new Vector3(0f, 0.07f, 0.01f), Quaternion.identity, Vector3.one), true);
            youPose[key] = g;
        }
        Variant("ride", RealModels.Pose.RideUpright, gripL, gripR, footL, footR);
        Variant("kick", RealModels.Pose.RideUpright, gripL, gripR, new Vector3(-0.78f, -0.32f, 0.3f), footR);
        Variant("elbow", RealModels.Pose.RideUpright, new Vector3(-0.1f, 0.32f, 0.2f), gripR, footL, footR);
        Variant("punch", RealModels.Pose.RideUpright, new Vector3(-0.82f, 0.4f, 0.3f), gripR, footL, footR);
        Variant("duck", RealModels.Pose.Ride, gripL + new Vector3(0f, -0.05f, 0f), gripR + new Vector3(0f, -0.05f, 0f), footL, footR);
        Variant("hurt", RealModels.Pose.RideUpright, new Vector3(0.12f, 0.2f, 0.22f), new Vector3(0.24f, 0.12f, 0.3f), footL, footR);
        if (youPose.Count == 0) { Destroy(root.gameObject); return null; }
        Show(youPose, "ride");
        return root;
    }

    private static RealModels.Model Single(Mesh lod0, Mesh lod1)
    {
        var m = new RealModels.Model { ok = true };
        m.lod0.Add(new RealModels.Part { mesh = lod0, mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        m.lod1.Add(new RealModels.Part { mesh = lod1, mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        return m;
    }

    /// <summary>Mũ len đen trùm đầu + khẩu trang (gốc tại khớp đầu, mặt hướng +Z).</summary>
    private static Mesh Gear(bool low)
    {
        var mb = new VMeshBuilder();
        mb.Add(VShape.Dome(low ? 10 : 20), Matrix4x4.TRS(new Vector3(0f, 0.06f, -0.005f), Quaternion.identity, new Vector3(0.2f, 0.17f, 0.22f)), GearDark);
        mb.Add(VShape.Torus(0.35f, low ? 10 : 20), Matrix4x4.TRS(new Vector3(0f, 0.07f, -0.005f), Quaternion.identity, new Vector3(0.205f, 0.05f, 0.225f)), GearDark);
        mb.Add(VShape.RoundBox(0.45f, low ? 8 : 14), Matrix4x4.TRS(new Vector3(0f, -0.035f, 0.085f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.15f, 0.085f, 0.05f)), GearDark);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(-0.078f, 0f, 0.035f), Quaternion.Euler(0f, 0f, 15f), new Vector3(0.006f, 0.012f, 0.09f)), GearDark);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0.078f, 0f, 0.035f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0.006f, 0.012f, 0.09f)), GearDark);
        Mesh mesh = mb.ToMesh(RealModels.MeshPrefix + "_RobberGear");
        mesh.hideFlags = HideFlags.DontSave;
        return mesh;
    }

    /// <summary>Dao rựa: cán gỗ + lưỡi thép dài (gốc tại nắm tay, lưỡi hướng +Y).</summary>
    private static Mesh Knife(bool low)
    {
        var mb = new VMeshBuilder();
        mb.Add(VShape.Cylinder(low ? 6 : 10), Matrix4x4.TRS(new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.035f, 0.13f, 0.035f)), Grip);
        mb.Add(VShape.RoundBox(0.2f, low ? 6 : 10), Matrix4x4.TRS(new Vector3(0f, 0.25f, 0.015f), Quaternion.identity, new Vector3(0.008f, 0.34f, 0.06f)), Steel);
        mb.Add(VShape.RoundBox(0.3f, low ? 6 : 10), Matrix4x4.TRS(new Vector3(0f, 0.43f, 0.02f), Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.008f, 0.06f, 0.07f)), Steel);
        Mesh mesh = mb.ToMesh(RealModels.MeshPrefix + "_Knife");
        mesh.hideFlags = HideFlags.DontSave;
        return mesh;
    }

    /// <summary>Gậy gỗ / tuýp (gốc tại nắm tay, hướng +Y).</summary>
    private static Mesh Stick(bool low)
    {
        var mb = new VMeshBuilder();
        mb.Add(VShape.Cylinder(low ? 6 : 10), Matrix4x4.TRS(new Vector3(0f, 0.28f, 0f), Quaternion.identity, new Vector3(0.04f, 0.75f, 0.04f)), Wood);
        mb.Add(VShape.Cylinder(low ? 6 : 10), Matrix4x4.TRS(new Vector3(0f, -0.03f, 0f), Quaternion.identity, new Vector3(0.046f, 0.14f, 0.046f)), Grip);
        Mesh mesh = mb.ToMesh(RealModels.MeshPrefix + "_Stick");
        mesh.hideFlags = HideFlags.DontSave;
        return mesh;
    }

    /// <summary>Mặt đất dưới p, bỏ qua collider của chính xe người chơi.</summary>
    private static float Ground(Vector3 p, Transform ignore)
    {
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? best : VietDistrict.RoadTop;
    }

    // ================= Máy quay, phụ đề, lựa chọn =================
    private Camera cam;
    private Canvas canvas;
    private TMP_Text subLine, titleText, hintText;
    private Image fade;
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private Camera prevBubbleCam;
    private CursorLockMode prevLock;
    private bool prevCursor;

    private readonly List<GameObject> hiddenTraffic = new List<GameObject>();

    /// <summary>Tạm ẩn dòng ô tô / xe máy giao thông trong lúc chiếu phim (để không xe nào chạy xuyên qua cảnh), xong thì hiện lại.</summary>
    private void HideTraffic(bool hide)
    {
        if (hide)
        {
            hiddenTraffic.Clear();
            foreach (MonoBehaviour sys in new MonoBehaviour[] { TrafficSystem.Instance, MotorbikeTraffic.Instance })
                if (sys != null && sys.gameObject.activeSelf) { sys.gameObject.SetActive(false); hiddenTraffic.Add(sys.gameObject); }
        }
        else
        {
            foreach (GameObject g in hiddenTraffic) if (g != null) g.SetActive(true);
            hiddenTraffic.Clear();
        }
    }

    private void BeginFilm()
    {
        HideTraffic(true);
        Camera like = Camera.main;
        var camGo = new GameObject("RobberyCamera");
        cam = camGo.AddComponent<Camera>();
        if (like != null) cam.CopyFrom(like);
        cam.targetTexture = null;
        cam.depth = 96f;
        cam.fieldOfView = 52f;
        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.06f);   // máy quay trong cabin không cắt xuyên thành xe
        prevBubbleCam = SpeechBubble.ViewCamera;
        SpeechBubble.ViewCamera = cam;
        hiddenCanvases.Clear();
        foreach (Canvas c in FindObjectsByType<Canvas>())
        {
            if (c == null || !c.enabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }
        canvas = UIKit.CreateCanvas(camGo.transform, 885);
        Bar(true); Bar(false);
        titleText = UIKit.Text(canvas.transform, "", 44, new Vector2(1700, 60), new Vector2(0, 380), TextAlignmentOptions.Center, true, Accent * 1.2f + new Color(0.2f, 0.2f, 0.2f));
        hintText = UIKit.Text(canvas.transform, "", 24, new Vector2(1700, 40), new Vector2(0, 335), TextAlignmentOptions.Center, false, new Color(1f, 1f, 1f, 0.75f));
        subLine = UIKit.Text(canvas.transform, "", 32, new Vector2(1500, 90), new Vector2(0, -300), TextAlignmentOptions.Center, false);
        fade = UIKit.FullScreen("Fade", canvas.transform, Color.black).GetComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
        prevLock = Cursor.lockState; prevCursor = Cursor.visible;
    }

    private void EndFilm()
    {
        HideTraffic(false);
        Time.timeScale = 1f;
        slow = 1f;
        foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
        SpeechBubble.ViewCamera = prevBubbleCam;
        Cursor.lockState = prevLock; Cursor.visible = prevCursor;
        if (cam != null) Destroy(cam.gameObject);
        cam = null; canvas = null; subLine = titleText = hintText = null; fade = null;
    }

    private void Bar(bool top)
    {
        GameObject g = UIKit.Rect(top ? "BarTop" : "BarBottom", canvas.transform);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 90f);
        Image img = g.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    private void Say(string speaker, string text, Color color)
    {
        if (subLine == null) return;
        subLine.text = string.IsNullOrEmpty(speaker) ? text : $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{speaker}:</b></color> {text}";
    }

    /// <summary>Lời thoại 2.2 s, máy quay giữ góc hiện tại nhưng vẫn bám theo xe.</summary>
    private IEnumerator Say2(string speaker, string text, Color color)
    {
        Say(speaker, text, color);
        Vector3 rel = cam.transform.position - youPos, look = youPos + Vector3.up * 1f;
        Vector3 lookRel = look - youPos;
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 2.2f)
        {
            Drive();
            cam.transform.position = youPos + rel;
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(youPos + lookRel - cam.transform.position), Time.unscaledDeltaTime * 4f);
            yield return null;
        }
    }

    /// <summary>Một cú máy: frame() trả về vị trí + điểm nhìn mỗi frame (tính sau khi xe đã chạy); xe vẫn chạy suốt cú máy.</summary>
    private IEnumerator Shot(float seconds, string title, string hint, System.Func<(Vector3 pos, Vector3 look)> frame, System.Action onStart, System.Func<bool> until = null)
    {
        if (title != null && titleText != null) { titleText.text = title; hintText.text = hint ?? ""; }
        else if (titleText != null) { titleText.text = ""; hintText.text = ""; }
        onStart?.Invoke();
        Drive();
        var f = frame();
        Vector3 pos = Unblocked(f.look, f.pos);
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(f.look - pos));
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < seconds)
        {
            Drive();
            if (until != null && until()) break;
            var s = frame();
            Vector3 want = Unblocked(s.look, s.pos);
            pos = Vector3.Lerp(pos, want, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.2f);
            if (flash > 0f && fade != null) { flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.6f); fade.color = new Color(0.75f, 0f, 0f, flash * 0.5f); }
            float amp = 0.07f + shake, fq = 0.9f + shake * 25f;
            Vector3 shake3 = new Vector3(Mathf.PerlinNoise(Time.unscaledTime * fq, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.unscaledTime * fq) - 0.5f, 0f) * amp;
            cam.transform.SetPositionAndRotation(pos + shake3, Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(s.look - pos), 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)));
            yield return null;
        }
    }

    /// <summary>Chuyển cảnh: tối màn hình, dòng chữ, sáng lại (bọn cướp biến khỏi cảnh, xe người chơi chạy tiếp).</summary>
    private IEnumerator Cut(string caption, string line)
    {
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime * 2.5f) { Drive(); fade.color = new Color(0f, 0f, 0f, a); yield return null; }
        fade.color = Color.black;
        titleText.text = caption; hintText.text = "";
        Say("", $"<i>{line}</i>", Color.white);
        if (bike != null) { Destroy(bike.gameObject); bike = null; }
        vYouWant = 7f;
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 2.6f) { Drive(); yield return null; }
        titleText.text = "";
        Vector3 back = -youDir * 5f + Right(youDir) * 1.2f + Vector3.up * 1.6f;
        for (float a = 1f; a > 0f; a -= Time.unscaledDeltaTime * 2f)
        {
            Drive();
            cam.transform.SetPositionAndRotation(Unblocked(youPos + Vector3.up, youPos + back), Quaternion.LookRotation(youDir * 6f - back + Vector3.up));
            fade.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// Bảng lựa chọn "BẠN SẼ LÀM GÌ?": nút "[1] ..." + thanh đếm ngược; phím 1-4 hoặc chuột.
    /// Hình chậm x0.3 (cả thế giới lẫn hai xe) nhưng xe VẪN CHẠY; máy quay bám theo và đẩy dần vào. Hết giờ -> result(-1).
    /// </summary>
    private IEnumerator Choose(float seconds, string[] options, System.Func<(Vector3 pos, Vector3 look)> frame, System.Action<int> result)
    {
        int chosen = -1;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        GameObject panel = UIKit.Rect("Choices", canvas.transform, new Vector2(900, 80 * options.Length + 70), new Vector2(0, -60));
        UIKit.Text(panel.transform, "BẠN SẼ LÀM GÌ?", 30, new Vector2(900, 44), new Vector2(0, 40 * options.Length + 12), TextAlignmentOptions.Center, true, Accent * 1.3f);
        for (int i = 0; i < options.Length; i++)
        {
            int k = i;
            UIKit.Btn(panel.transform, $"[{i + 1}]  {options[i]}", new Vector2(860, 64), new Vector2(0, 40 * options.Length - 30 - i * 78),
                () => chosen = k, new Color(0.08f, 0.09f, 0.12f, 0.88f), 27);
        }
        Image timer = UIKit.HBar(panel.transform, new Vector2(-430, -40 * options.Length - 18), 860, 10, Accent);
        timer.fillAmount = 1f;

        slow = 0.3f;
        Time.timeScale = 0.3f;
        Say("", "", Color.white);
        Drive();
        var f = frame();
        Vector3 pos = Unblocked(f.look, f.pos);
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(f.look - pos));
        float t0 = Time.unscaledTime;
        while (chosen < 0 && Time.unscaledTime - t0 < seconds)
        {
            for (int i = 0; i < options.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) chosen = i;
            float left = 1f - (Time.unscaledTime - t0) / seconds;
            timer.fillAmount = left;
            timer.color = left < 0.35f ? Color.Lerp(Accent, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f)) : Accent;
            Drive();
            var s = frame();
            Vector3 want = Unblocked(s.look, s.pos);
            pos = Vector3.Lerp(pos, want, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            cam.transform.SetPositionAndRotation(pos, Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(s.look - pos), 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)));
            cam.fieldOfView = Mathf.Lerp(52f, 44f, 1f - left);
            yield return null;
        }
        slow = 1f;
        Time.timeScale = 1f;
        cam.fieldOfView = 52f;
        Destroy(panel);
        Cursor.lockState = prevLock; Cursor.visible = prevCursor;
        if (chosen >= 0) Say("Bạn", $"<i>{options[chosen]}</i>", YouCol);
        result(chosen);
    }

    private Vector3 Unblocked(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return to;
        float best = len;
        foreach (RaycastHit h in Physics.SphereCastAll(from, 0.2f, d / len, len, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance <= 0f) continue;
            if (car != null && h.collider.transform.IsChildOf(car.transform)) continue;
            if (bike != null && h.collider.transform.IsChildOf(bike)) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null || h.collider.GetComponentInParent<PedestrianWalker>() != null) continue;
            if (TrafficSystem.IsTrafficCar(h.collider.transform) || h.collider.GetComponentInParent<TrafficBike>() != null) continue;
            best = Mathf.Min(best, h.distance - 0.15f);
        }
        return from + d / len * Mathf.Max(0.6f, best);
    }
}
