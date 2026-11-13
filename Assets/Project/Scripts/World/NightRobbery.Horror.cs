using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yếu tố kinh dị ban đêm - phần chung (kích hoạt, dựng "ma", người đưa tang, cửa hàng, màn hình điện thoại).
/// Ba sự kiện:
///  - CAMERA CA ĐÊM (NightRobbery.HorrorCamera.cs): lúc đóng cửa, cửa hàng vắng, camera an ninh thấy một bóng áo trắng
///    mà ngoài đời không có ai; nó tiến gần dần... (40% mỗi tối, cách nhau ít nhất 2 ngày, người chơi phải đang ở trong cửa hàng).
///  - ĐÁM MA NỬA ĐÊM (NightRobbery.HorrorFuneral.cs): đang giao hàng đêm, một đoàn đưa tang áo trắng băng qua đường.
///  - ĐƠN GIAO CHO CHÍNH MÌNH (NightRobbery.HorrorDouble.cs): đơn hàng lạ gửi tới chính cửa hàng của bạn, người ở quầy là... bạn.
/// Hai sự kiện ngoài đường chỉ xảy ra khi trời tối hẳn, đang giao hàng bằng xe máy hoặc ô tô, còn 50-140 m tới điểm giao:
/// mỗi đơn xét 1 lần, 35%, tối đa 1 lần / ngày, hai sự kiện luân phiên. Sáng hôm sau có "bản tin" rùng rợn.
/// Phím thử (Editor / Development): Shift + F1 camera ca đêm, Shift + F2 đám ma, Shift + F3 đơn giao cho chính mình,
/// Shift + F12 nhảy tới lúc trời tối hẳn (cửa hàng vẫn mở, không hết ngày).
/// </summary>
public partial class NightRobbery
{
    [Range(0f, 1f)] public float horrorChance = 0.35f;
    [Range(0f, 1f)] public float cameraChance = 0.4f;
    private int decidedHorror = -1, lastHorrorDay = -1, cameraRolledDay = -1, lastCameraDay = -10;
    private bool nextIsFuneral = true, cameraPending, cameraRunning;
    private string horrorNews;

    private static readonly Color MournCol = new Color(0.85f, 0.88f, 0.95f), DoubleCol = new Color(0.55f, 0.85f, 1f);

    /// <summary>Giữ lại báo cáo cuối ngày khi sự kiện camera ca đêm sắp / đang chiếu (GameManager.HoldDayEnd).</summary>
    private bool HoldsDayEnd() => cameraPending || cameraRunning;

    private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>Gọi đầu Update. Trả về true nếu đã bắt đầu một sự kiện kinh dị.</summary>
    private bool HorrorUpdate()
    {
        bool cheats = GameDebug.CheatsEnabled && Shift;
        GameManager gm = GameManager.Instance;

        // Phím thử: nhảy tới 1.2 giờ sau hoàng hôn (trời tối hẳn, cửa hàng vẫn mở, chưa hết ngày)
        if (cheats && Input.GetKeyDown(KeyCode.F12) && gm != null)
        {
            float sunset = DayNightCycle.Instance != null ? DayNightCycle.Instance.sunsetHour : 18.5f;
            gm.DebugSetClock(Mathf.Min(sunset + 1.2f, gm.closingHour - 0.4f));
            ProgressHUD.Toast("(Thử) Trời đã tối - cửa hàng vẫn mở.");
            return false;
        }

        // ===== Camera ca đêm =====
        if (cheats && Input.GetKeyDown(KeyCode.F1))
        {
            if (Busy()) return false;
            if (StorePlayer() == null) { ProgressHUD.Toast("Thử sự kiện: hãy đứng trong cửa hàng (không ngồi xe)."); return false; }
            StartCoroutine(RunCamera(false));
            return true;
        }
        if (gm != null)
        {
            int day = gm.currentDay;
            // Gieo xúc xắc từ 1 giờ trước giờ đóng cửa (để kịp giữ báo cáo cuối ngày lại)
            if (day != cameraRolledDay && gm.ClockHours >= gm.closingHour - 1f)
            {
                cameraRolledDay = day;
                cameraPending = day >= 2 && day - lastCameraDay >= 2 && Random.value < cameraChance;
            }
            if (cameraPending && gm.IsClosingTime && NPCShopper.Active.Count == 0)
            {
                if (StorePlayer() == null) cameraPending = false;            // người chơi không ở trong cửa hàng: bỏ, cho hết ngày
                else if (!Busy())
                {
                    cameraPending = false;
                    lastCameraDay = day;
                    StartCoroutine(RunCamera(true));
                    return true;
                }
            }
        }

        // ===== Ngoài đường: đám ma / đơn giao cho chính mình =====
        bool forceFuneral = cheats && Input.GetKeyDown(KeyCode.F2), forceDouble = cheats && Input.GetKeyDown(KeyCode.F3);
        if (!forceFuneral && !forceDouble && (Time.time < nextCheck || !NightAmbience.DeepNight)) return false;
        if (!DeliveryManager.IsDelivering(out Vector3 dest, out int serial))
        {
            if (forceFuneral || forceDouble) ProgressHUD.Toast("Thử sự kiện: hãy lấy gói hàng giao trước đã.");
            return false;
        }
        PlayerCarController ride = RidingBike();
        if (ride == null) ride = DrivingCar();
        if (ride == null)
        {
            if (forceFuneral || forceDouble) ProgressHUD.Toast("Thử sự kiện: hãy lên xe máy hoặc ô tô trước đã.");
            return false;
        }
        bool funeral;
        if (forceFuneral || forceDouble) funeral = forceFuneral;
        else
        {
            if (serial == decidedHorror) return false;
            Vector3 d = dest - ride.transform.position; d.y = 0f;
            if (d.magnitude > maxDistance || d.magnitude < minDistance) return false;
            decidedHorror = serial;
            int day = gm != null ? gm.currentDay : 0;
            if (day == lastHorrorDay || Random.value > horrorChance) return false;
            lastHorrorDay = day;
            decidedSerial = serial;   // đêm nay đơn này không bị cướp nữa
            funeral = nextIsFuneral;
            nextIsFuneral = !nextIsFuneral;
        }
        if (Busy()) return false;
        StartCoroutine(funeral ? RunFuneral(ride) : RunDouble(ride));
        return true;
    }

    private void HorrorNews()
    {
        if (string.IsNullOrEmpty(horrorNews)) return;
        ProgressHUD.Toast(horrorNews);
        horrorNews = null;
    }

    // ================= Lên xe / xuống xe trong đoạn phim =================
    private float stopAtS = float.NaN;   // dừng xe đúng tại quãng đường này (không lao vào đoàn người)
    private Rigidbody rideRb;
    private bool rideWasKinematic, rideIsBike;

    /// <summary>Chuẩn bị đoạn phim đi đường (dùng chung cho xe máy và ô tô): đường ray, người lái đứng thay, máy quay.</summary>
    private bool SetupRide(PlayerCarController ride, VietDistrict city)
    {
        car = ride;
        Transform ct = car.transform;
        Vector3 heading = ct.forward; heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        rail = BuildRail(city, ct.position, heading.normalized, out lane);
        if (rail == null) return false;
        Playing = true;
        ReadContext();
        lastWhen = When();
        lastStreet = DeliveryManager.TargetName(out string place) ? "gần " + place : "trong thành phố";
        rideIsBike = car.GetComponentInChildren<MotorcycleInteraction>(true) != null || car.GetComponentInParent<MotorcycleInteraction>() != null;

        rideRb = car.GetComponent<Rigidbody>();
        rideWasKinematic = rideRb != null && rideRb.isKinematic;
        if (rideRb != null) { rideRb.linearVelocity = Vector3.zero; rideRb.angularVelocity = Vector3.zero; rideRb.isKinematic = true; }
        car.enabled = false;
        yOffset = Mathf.Clamp(ct.position.y - Ground(ct.position, ct), 0f, 1.5f);
        sYou = rail.Project(ct.position);
        vYou = vYouWant = 8f;
        youLat = youLatWant = 0f;
        passPose.Clear(); youPose.Clear();
        robDown = false; robRoll = robRollWant = robSpin = robSpinWant = 0f; shake = flash = youLean = 0f; robRail = null; cityRef = city;
        bike = null; standIn = null; stopAtS = float.NaN;
        if (rideIsBike) standIn = SpawnStandIn(city);
        else
        {
            carMode = true;
            MeasureCar();
            if (carMeasured)
            {
                rail.Sample(sYou, youLat, out Vector3 railP, out _);
                yOffset = Mathf.Clamp(Ground(railP, ct) - railP.y - carBottom, -0.5f, 1.5f);
            }
            SpawnCarDriver(city);
        }
        Drive();
        BeginFilm();
        return true;
    }

    private void TeardownRide()
    {
        EndFilm();
        ClearGhouls();
        ClearProps();
        ClearScamActors();
        stopAtS = float.NaN;
        if (carMode) ClearCarDriver();
        if (standIn != null) Destroy(standIn.gameObject);
        standIn = null; bike = null;
        if (car != null) car.enabled = true;
        if (rideRb != null) { rideRb.isKinematic = rideWasKinematic; rideRb.linearVelocity = Vector3.zero; }
        rail = null; robRail = null;
        carMode = false;
        Playing = false;
    }

    /// <summary>Đầu người lái (xe máy hoặc ô tô).</summary>
    private Vector3 RiderHead => rideIsBike ? youPos + Vector3.up * 1.6f : DriverHead;

    // ================= "Ma" / người bị vặn vẹo (người có hoạt ảnh, điều khiển tay) =================
    private static Mesh hairLo, hairHi;
    private readonly List<Mover> ghouls = new List<Mover>();
    private Transform horrorProps;    // mọi đạo cụ máu me của đoạn phim (dọn một lần)

    private Transform Props()
    {
        if (horrorProps == null)
        {
            horrorProps = new GameObject("HorrorProps").transform;
            horrorProps.SetParent(transform, false);
        }
        return horrorProps;
    }

    private void ClearProps()
    {
        if (horrorProps != null) Destroy(horrorProps.gameObject);
        horrorProps = null;
    }

    /// <summary>Tóc đen dài xoã che kín mặt (gốc tại khớp đầu, mặt hướng +Z).</summary>
    private static Mesh Hair(bool low)
    {
        if (low ? hairLo != null : hairHi != null) return low ? hairLo : hairHi;
        var mb = new VMeshBuilder();
        Color black = new Color(0.025f, 0.025f, 0.03f);
        int r = low ? 8 : 14;
        mb.Add(VShape.Dome(r), Matrix4x4.TRS(new Vector3(0f, 0.1f, -0.005f), Quaternion.identity, new Vector3(0.205f, 0.15f, 0.225f)), black);         // da đầu
        mb.Add(VShape.RoundBox(0.4f, r), Matrix4x4.TRS(new Vector3(0f, -0.03f, 0.108f), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.19f, 0.44f, 0.035f)), black); // tóc xoã che mặt
        mb.Add(VShape.RoundBox(0.4f, r), Matrix4x4.TRS(new Vector3(0f, -0.15f, -0.075f), Quaternion.Euler(-6f, 0f, 0f), new Vector3(0.23f, 0.62f, 0.06f)), black); // tóc sau lưng
        for (int s = -1; s <= 1; s += 2)
            mb.Add(VShape.RoundBox(0.4f, r), Matrix4x4.TRS(new Vector3(s * 0.1f, -0.06f, 0.02f), Quaternion.identity, new Vector3(0.045f, 0.42f, 0.17f)), black); // hai bên
        Mesh m = mb.ToMesh(RealModels.MeshPrefix + (low ? "_GhostHair_LOD1" : "_GhostHair"));
        m.hideFlags = HideFlags.DontSave;
        if (low) hairLo = m; else hairHi = m;
        return m;
    }

    /// <summary>Gắn một mesh (bảng màu) vào xương, giữ tỉ lệ thật.</summary>
    private static GameObject AttachToBone(Animator anim, HumanBodyBones bone, Mesh mesh, Vector3 offset, string name)
    {
        Transform b = anim != null && anim.isHuman ? anim.GetBoneTransform(bone) : null;
        if (b == null) return null;
        var g = new GameObject(name);
        g.transform.SetParent(b, false);
        Vector3 ls = b.lossyScale;
        g.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
        g.transform.position = b.position + anim.transform.rotation * offset;
        g.transform.rotation = anim.transform.rotation;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        return g;
    }

    /// <summary>Bộ màu "ma váy trắng": váy / áo trắng, quần trắng, chân trần xám.</summary>
    private static readonly Color[] GhostDress = { Color.white, new Color(0.9f, 0.9f, 0.88f), new Color(0.88f, 0.88f, 0.86f), new Color(0.6f, 0.62f, 0.62f) };

    /// <summary>
    /// Người có hoạt ảnh làm "ma": tô lại quần áo theo palette (null = giữ quần áo gốc), da tái + loang máu (gore),
    /// tóc xoã che mặt (hair), gắn Contort để vặn đầu / kéo cổ. Điều khiển tay (danh sách ghouls, UpdateGhouls).
    /// </summary>
    private Mover MakeGhoul(GameObject prefab, Vector3 at, Vector3 face, Color[] palette, float gore, string tag, bool hair, string role = "ghoul")
    {
        Mover m = SpawnMover(at, at, at + face, 0f, role, prefab);
        if (m == null) return null;
        movers.Remove(m);
        ghouls.Add(m);
        m.arrived = true; m.target = m.lastSet = at;
        m.t.SetPositionAndRotation(at, Quaternion.LookRotation(face.WithY(0f).sqrMagnitude > 0.01f ? face.WithY(0f).normalized : Vector3.forward));
        if (m.anim != null && m.anim.isHuman)
        {
            IEnumerator e = PaintOutfitCo(m.t.gameObject, false, palette, tag, gore);
            while (e.MoveNext()) { }
            if (hair) AttachToBone(m.anim, HumanBodyBones.Head, Hair(false), Vector3.zero, "GhostHair");
        }
        m.t.gameObject.AddComponent<Contort>();
        if (m.cp != null) m.cp.Play(ClipPlayer.Has("Injured_Hurting_Idle") ? "Injured_Hurting_Idle" : "Walking", true, 0f, 0.6f);
        return m;
    }

    /// <summary>Người làm "ma" chính là nhân vật người chơi (bản sao model, giữ nguyên quần áo, mặt tái, máu loang).</summary>
    private Mover MakeDouble(Vector3 at, Vector3 face)
    {
        GameObject src = PlayerBody();
        if (src == null) return null;
        return MakeGhoul(src, at, face, null, 0.85f, "double", false, "double");
    }

    private static GameObject PlayerBody()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        Animator body = pc != null ? pc.GetComponentInChildren<Animator>(true) : null;
        return body != null && body.isHuman ? body.gameObject : null;
    }

    /// <summary>Đi tới đích (đoạn phim trong cửa hàng / ngoài đường), hoạt ảnh đi / chạy; tới nơi thì quay mặt theo face.</summary>
    private void GhoulGo(Mover m, Vector3 to, float speed, Vector3? face = null)
    {
        if (m == null || m.t == null) return;
        m.target = new Vector3(to.x, m.t.position.y, to.z);
        m.speed = speed; m.arrived = false;
        if (face.HasValue) m.face = face.Value;
        if (m.cp != null) m.cp.Play(speed > 2.2f ? "Running" : "Walking", true, 0.15f, Mathf.Clamp(speed / (speed > 2.2f ? 3.6f : 1.3f), 0.5f, 1.6f));
    }

    private void GhoulIdle(Mover m, string clip = "Injured_Hurting_Idle")
    {
        if (m == null || m.cp == null) return;
        m.cp.Play(ClipPlayer.Has(clip) ? clip : "Walking", true, 0.25f, clip == "Walking" ? 0f : 0.7f);
    }

    /// <summary>Mỗi frame: di chuyển "ma", để lại dấu chân máu.</summary>
    private void UpdateGhouls(float dt)
    {
        for (int i = ghouls.Count - 1; i >= 0; i--)
        {
            Mover m = ghouls[i];
            if (m.t == null) { ghouls.RemoveAt(i); continue; }
            if (!m.arrived)
            {
                Vector3 d = m.target - m.t.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.08f) { m.arrived = true; GhoulIdle(m, m.role == "double" ? "Injured_Hurting_Idle" : "Injured_Hurting_Idle"); }
                else
                {
                    Vector3 step = d / dist * Mathf.Min(dist, m.speed * dt);
                    m.t.position += step;
                    m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(d / dist), 1f - Mathf.Exp(-10f * dt));
                    m.fallAt += step.magnitude;                          // (dùng lại làm bộ đếm quãng đường)
                    if (m.fallAt > 0.6f) { m.fallAt = 0f; Footprint(m); }
                }
            }
            else
            {
                Vector3 f = m.face - m.t.position; f.y = 0f;
                if (f.sqrMagnitude > 0.01f) m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(f), 1f - Mathf.Exp(-6f * dt));
            }
        }
    }

    private bool footLeft;
    private void Footprint(Mover m)
    {
        footLeft = !footLeft;
        Vector3 p = m.t.position + m.t.right * (footLeft ? -0.11f : 0.11f);
        HorrorFx.Decal(HorrorFx.Splat(), p + Vector3.up * 0.01f, Vector3.up, m.t.forward, new Vector2(0.16f, 0.28f), Props(), 0.85f);
    }

    private void ClearGhouls()
    {
        foreach (Mover m in ghouls) if (m.t != null) Destroy(m.t.gameObject);
        ghouls.Clear();
    }

    private static int FreeLayer()
    {
        for (int i = 31; i >= 8; i--) if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
        return -1;
    }

    private static void SetLayer(GameObject g, int layer)
    {
        if (g == null || layer < 0) return;
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // ================= Người đưa tang (người có hoạt ảnh, đồ tang trắng loang máu) =================
    private static readonly Color[] MourningColors = { Color.white, new Color(0.93f, 0.93f, 0.9f), new Color(0.9f, 0.9f, 0.88f), new Color(0.12f, 0.12f, 0.12f) };
    private const float MournGore = 0.85f;
    private readonly List<GameObject> mournerPrefabs = new List<GameObject>();
    private GameObject ghostPrefab;
    private bool mournersPrepped;

    /// <summary>
    /// Chuẩn bị trước (chạy nền từ chập tối): chọn 8 người đưa tang + 1 "ma váy trắng", vẽ đồ tang / da tái / máu dần qua nhiều frame;
    /// vẽ sẵn bản "máu me" của nhân vật người chơi. Lúc sự kiện xảy ra chỉ việc lấy ra, không khựng game.
    /// </summary>
    private IEnumerator PrepMourners()
    {
        mournersPrepped = true;
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null) yield break;
        for (int k = 0; k < 8; k++)
        {
            GameObject p = spawner.RandomCustomerPrefab();
            if (p != null) mournerPrefabs.Add(p);
        }
        ghostPrefab = spawner.RandomCustomerPrefab();
        if (ghostPrefab != null) yield return PaintOutfitCo(ghostPrefab, true, GhostDress, "ghost", 1f);
        GameObject body = PlayerBody();
        if (body != null) yield return PaintOutfitCo(body, true, null, "double", 0.85f);
    }

    private static Mesh headbandMesh, hoodMesh;

    /// <summary>Đồ tang: áo quần trắng loang máu, da tái (vẽ lên texture); khăn tang / mũ mấn trắng; gắn Contort để vặn đầu.</summary>
    private static void DressMourner(GameObject go, Animator anim, bool hood)
    {
        if (anim == null || !anim.isHuman) return;
        IEnumerator e = PaintOutfitCo(go, false, MourningColors, "tang", MournGore);
        while (e.MoveNext()) { }
        if (headbandMesh == null)
        {
            var mb = new VMeshBuilder();
            mb.Add(VShape.Torus(0.16f, 20), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.22f, 0.22f, 0.24f)), new Color(0.97f, 0.97f, 0.95f));
            mb.Add(VShape.RoundBox(0.3f, 8), Matrix4x4.TRS(new Vector3(0f, -0.12f, -0.12f), Quaternion.Euler(-10f, 0f, 0f), new Vector3(0.05f, 0.26f, 0.01f)), new Color(0.97f, 0.97f, 0.95f)); // đuôi khăn
            mb.Add(VShape.RoundBox(0.3f, 8), Matrix4x4.TRS(new Vector3(0.03f, -0.02f, 0.11f), Quaternion.identity, new Vector3(0.06f, 0.03f, 0.01f)), new Color(0.5f, 0.03f, 0.03f));   // vệt máu trên khăn
            headbandMesh = mb.ToMesh(RealModels.MeshPrefix + "_Headband");
            headbandMesh.hideFlags = HideFlags.DontSave;
            var hb = new VMeshBuilder();
            hb.Add(VShape.Cone, Matrix4x4.TRS(new Vector3(0f, 0.12f, -0.01f), Quaternion.identity, new Vector3(0.25f, 0.32f, 0.27f)), new Color(0.95f, 0.94f, 0.9f));
            hb.Add(VShape.RoundBox(0.3f, 8), Matrix4x4.TRS(new Vector3(0f, -0.1f, -0.1f), Quaternion.identity, new Vector3(0.24f, 0.36f, 0.04f)), new Color(0.95f, 0.94f, 0.9f));
            hb.Add(VShape.RoundBox(0.3f, 8), Matrix4x4.TRS(new Vector3(-0.04f, 0.02f, 0.13f), Quaternion.identity, new Vector3(0.08f, 0.14f, 0.01f)), new Color(0.5f, 0.03f, 0.03f));
            hoodMesh = hb.ToMesh(RealModels.MeshPrefix + "_MourningHood");
            hoodMesh.hideFlags = HideFlags.DontSave;
        }
        AttachToBone(anim, HumanBodyBones.Head, hood ? hoodMesh : headbandMesh, new Vector3(0f, 0.13f, 0.005f), hood ? "MourningHood" : "Headband");
        if (go.GetComponent<Contort>() == null) go.AddComponent<Contort>();
    }

    /// <summary>Làm cả đoàn người trắng bệch, xanh tái (khi họ cùng quay lại nhìn).</summary>
    private void PaleMovers(string role, float k)
    {
        foreach (Mover m in movers)
        {
            if (m.t == null || m.role != role) continue;
            foreach (SkinnedMeshRenderer r in m.t.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] ms = r.materials;   // bản riêng từng người
                foreach (Material mat in ms)
                {
                    Color c = Color.Lerp(Color.white, new Color(0.62f, 0.7f, 0.85f), k);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    mat.color = c;
                }
            }
        }
    }

    // ================= Cửa hàng =================
    private static Vector3 StoreDoor(out Vector3 outward, out Bounds storeBounds)
    {
        outward = Vector3.back;
        storeBounds = new Bounds(Vector3.zero, Vector3.one * 40f);
        EntranceDoor[] doors = FindObjectsByType<EntranceDoor>();
        GameObject store = GameObject.Find("Supermarket_full");
        Vector3 door = Vector3.zero;
        if (doors.Length > 0) { foreach (EntranceDoor d in doors) door += d.transform.position; door /= doors.Length; }
        if (store != null)
        {
            Renderer[] rs = store.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                storeBounds = b;
                if (doors.Length == 0) door = new Vector3(b.center.x, b.min.y, b.min.z);
                door.y = b.min.y;
                outward = door - b.center; outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f) outward = Vector3.back;
                outward.Normalize();
            }
        }
        return door;
    }

    /// <summary>Người chơi đang đi bộ trong cửa hàng (không ngồi xe) - trả về PlayerController.</summary>
    private static PlayerController StorePlayer()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null || !pc.isActiveAndEnabled) return null;
        Camera c = Camera.main;
        Vector3 p = c != null ? c.transform.position : pc.transform.position + Vector3.up * 1.6f;
        return WeatherSystem.IsSheltered(p) ? pc : null;
    }

    // Đèn trong cửa hàng: tối dần / chập chờn trong đoạn phim, xong trả lại
    private readonly List<(Light l, float i)> storeLights = new List<(Light, float)>();

    private void GrabStoreLights()
    {
        storeLights.Clear();
        StoreDoor(out _, out Bounds b);
        b.Expand(2f);
        foreach (Light l in FindObjectsByType<Light>())
            if (l != null && l.enabled && l.type != LightType.Directional && b.Contains(l.transform.position) && l.GetComponent<NightLight>() == null)
                storeLights.Add((l, l.intensity));
    }

    private void DimStoreLights(float k, float flicker)
    {
        foreach (var s in storeLights)
        {
            if (s.l == null) continue;
            float f = flicker > 0f && Random.value < flicker ? Random.Range(0f, 0.3f) : 1f;
            s.l.intensity = s.i * k * f;
        }
    }

    private void RestoreStoreLights()
    {
        foreach (var s in storeLights) if (s.l != null) s.l.intensity = s.i;
        storeLights.Clear();
    }

    // Người trong cửa hàng (khách, nhân viên) ẩn tạm khi cảnh cần cửa hàng vắng
    private readonly List<GameObject> hiddenPeople = new List<GameObject>();

    private void HideStorePeople(Bounds b)
    {
        hiddenPeople.Clear();
        foreach (Animator a in FindObjectsByType<Animator>())
        {
            if (a == null || !a.gameObject.activeInHierarchy || !a.isHuman) continue;
            if (!b.Contains(a.transform.position) || a.GetComponentInParent<PlayerController>() != null) continue;
            GameObject root = a.gameObject;
            if (root.transform.IsChildOf(transform)) continue;
            root.SetActive(false);
            hiddenPeople.Add(root);
        }
    }

    private void ShowStorePeople()
    {
        foreach (GameObject g in hiddenPeople) if (g != null) g.SetActive(true);
        hiddenPeople.Clear();
    }

    // ================= Thông báo điện thoại =================
    private GameObject phonePanel;

    private void PhoneNote(string title, string body)
    {
        if (phonePanel != null) Destroy(phonePanel);
        if (canvas == null) return;
        phonePanel = UIKit.Panel("PhoneNote", canvas.transform, new Vector2(620, 190), new Vector2(560, 250), new Color(0.06f, 0.07f, 0.09f, 0.94f));
        UIKit.Text(phonePanel.transform, "<color=#FF6B5B>●</color>  <b>VietUrban Giao Hàng</b>   <size=70%><color=#9AA3AE>bây giờ</color></size>", 24,
            new Vector2(580, 34), new Vector2(0, 66), TextAlignmentOptions.Left, false, Color.white);
        UIKit.Text(phonePanel.transform, $"<b>{title}</b>", 26, new Vector2(580, 34), new Vector2(0, 28), TextAlignmentOptions.Left, false, Color.white);
        UIKit.Text(phonePanel.transform, body, 22, new Vector2(580, 96), new Vector2(0, -36), TextAlignmentOptions.TopLeft, false, new Color(0.85f, 0.88f, 0.92f));
        HorrorAudio.Play2D(HorrorAudio.PhoneBuzz(), 0.7f);
    }

    private void ClosePhone()
    {
        if (phonePanel != null) Destroy(phonePanel);
        phonePanel = null;
    }

    /// <summary>Tối sầm màn hình (mạnh) + rung máy quay + tiếng rít: cú hù.</summary>
    private void Scare()
    {
        HorrorAudio.Play2D(HorrorAudio.Sting(), 1f);
        shake = 0.9f;
        if (fade != null) fade.color = new Color(1f, 1f, 1f, 0.35f);
    }

    /// <summary>Màn đen giữ một lúc (sau cú hù), có dòng chữ.</summary>
    private IEnumerator BlackHold(float seconds, string caption, string line)
    {
        if (fade != null) fade.color = Color.black;
        if (titleText != null) { titleText.text = caption ?? ""; hintText.text = ""; }
        Say("", line ?? "", Color.white);
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < seconds) { Drive(); yield return null; }
        if (titleText != null) titleText.text = "";
    }

    private IEnumerator FadeIn(float seconds)
    {
        for (float a = 1f; a > 0f; a -= Time.unscaledDeltaTime / seconds)
        {
            Drive();
            if (fade != null) fade.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        if (fade != null) fade.color = new Color(0f, 0f, 0f, 0f);
    }

    /// <summary>Khung hình cố định theo hướng nhìn, có thể quay nhanh (xoay đầu) từ hướng cũ sang hướng mới.</summary>
    private IEnumerator Turn(Vector3 pos, Vector3 fromLook, Vector3 toLook, float seconds)
    {
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < seconds)
        {
            Drive();
            float u = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - t0) / seconds);
            Vector3 look = Vector3.Slerp((fromLook - pos).normalized, (toLook - pos).normalized, u);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toLook - pos));
    }
}
