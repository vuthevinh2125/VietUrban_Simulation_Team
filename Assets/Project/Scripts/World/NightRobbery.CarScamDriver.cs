using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Người lái trong ô tô (vụ ăn vạ) + cửa sổ lái để mở:
///  - Nhân vật người chơi (model đang ẩn khi ngồi xe) ngồi đúng ghế lái, nhiều tư thế dựng sẵn: drive (hai tay cầm vô lăng),
///    shock (giơ tay che khi bị đập kính), phone (áp điện thoại lên tai gọi 113), give (thò tay qua cửa sổ đưa xấp tiền).
///  - Cửa sổ lái: khi chọn đưa tiền / hạ kính nói chuyện, tấm kính phía cửa lái được bỏ đi ngay giữa hai cú máy
///    (coi như đã hạ kính, không diễn cảnh kính trượt); xong đoạn phim trả lại mesh gốc.
/// </summary>
public partial class NightRobbery
{
    private Transform driverRoot;
    private Vector3 drvHips, seatLocal;            // hông người lái (thế giới / theo trục xe, đơn vị mét)
    private float doorSide = -1f;                // -1: ghế lái bên trái (xe Việt Nam), +1: bên phải
    private GameObject cashProp;
    private bool grabbing;
    private float carCenterZ, carHalfL = 2.2f, carBottom, carHeight = 1.45f;
    private bool carMeasured;

    /// <summary>Hướng từ ghế lái ra ngoài cửa lái (mặt phẳng ngang).</summary>
    private Vector3 DoorOut => Right(youDir) * doorSide;
    /// <summary>Đầu người lái (để khung hình nhìn vào qua cửa sổ).</summary>
    private Vector3 DriverHead => (car != null ? car.transform.position : youPos) + Rot() * (seatLocal + new Vector3(0f, drvHeadUp, 0.05f));
    private float drvHeadUp = 0.62f;

    private Quaternion Rot() => car != null ? car.transform.rotation : Quaternion.LookRotation(youDir);

    /// <summary>Tìm ghế lái: CarInteraction.driverSeat, không có thì ước lượng theo khung xe (40% nửa bề ngang bên trái, cao 0.48 m).</summary>
    private void FindSeat()
    {
        Transform ct = car.transform;
        CarInteraction ci = car.GetComponent<CarInteraction>();
        if (ci == null) ci = car.GetComponentInParent<CarInteraction>();
        if (ci == null) ci = car.GetComponentInChildren<CarInteraction>(true);
        Quaternion inv = Quaternion.Inverse(ct.rotation);
        // Độ cao hông lấy theo khung xe (gốc xe có thể ở giữa thân chứ không ở mặt đường): ngồi thấp để đầu không chọc nóc xe
        float hipY = carBottom + Mathf.Min(carHeight * 0.36f, carHeight - 0.95f);
        seatLocal = new Vector3(-carHalfW * 0.42f, hipY, carCenterZ - 0.15f);
        if (ci != null && ci.driverSeat != null)
        {
            Vector3 l = inv * (ci.driverSeat.position - ct.position);
            if (l.magnitude < 3f && Mathf.Abs(l.x) > 0.15f) seatLocal = new Vector3(l.x, hipY, l.z);   // chỉ lấy vị trí ngang của DriverSeat
        }
        doorSide = seatLocal.x < 0f ? -1f : 1f;
        drvHips = ct.position + ct.rotation * seatLocal;
    }

    /// <summary>Người lái ngồi trong xe (model người chơi), các tư thế đổi bằng Show(youPose, key).</summary>
    private void SpawnCarDriver(VietDistrict city)
    {
        FindSeat();
        Transform ct = car.transform;
        CarInteraction ci = car.GetComponent<CarInteraction>();
        if (ci == null) ci = car.GetComponentInParent<CarInteraction>();
        if (ci == null) ci = car.GetComponentInChildren<CarInteraction>(true);
        Animator body = ci != null && ci.player != null ? ci.player.GetComponentInChildren<Animator>(true) : null;
        GameObject[] people = city.peopleModels, mats = city.peopleMaterials;
        GameObject src = body != null && body.isHuman ? body.gameObject : people[1 % people.Length];
        GameObject srcMat = body != null && src == body.gameObject ? null : (mats != null && mats.Length > 1 ? mats[1] : null);

        // Khoang lái theo trục xe (mét): sàn ~0.27 m trên đáy bánh xe, trần thấp hơn nóc 8 cm
        float floor = carBottom + 0.27f, roof = carBottom + carHeight - 0.08f;
        float s = doorSide;   // tay phía cửa = tay trái khi ngồi bên trái
        string id = car.name;
        Vector3 footL = new Vector3(-0.13f, -0.4f, 0.6f), footR = new Vector3(0.15f, -0.4f, 0.62f);
        Vector3 wheel = new Vector3(0f, 0.3f, 0.43f);

        // 1) Dựng thử tư thế ngồi, đo khung thật của model (hông -> đế giày, hông -> đỉnh đầu)
        float below = 0.5f, above = 0.9f;
        RealModels.Model probe = RealModels.Person(src, srcMat, RealModels.Pose.Sit,
            new RealModels.IK { on = true, handL = wheel + new Vector3(-0.17f, 0f, 0f), handR = wheel + new Vector3(0.17f, 0f, 0f), footL = footL, footR = footR }, $"driver_probe_{id}");
        if (probe != null && probe.ok)
        {
            below = Mathf.Max(0.2f, probe.hips.y - probe.bounds.min.y);
            above = Mathf.Max(0.5f, probe.bounds.max.y - probe.hips.y);
        }
        // 2) Model to hơn khoang xe thì thu nhỏ một chút; đặt đế giày lên sàn, đỉnh đầu dưới trần (ưu tiên đầu không chọc nóc)
        float k = Mathf.Clamp((roof - floor) / (below + above), 0.72f, 1f);
        float hipY = floor + below * k;
        if (hipY + above * k > roof) hipY = roof - above * k;
        seatLocal.y = hipY;
        drvHeadUp = Mathf.Max(0.4f, above - 0.12f) * k;
        drvHips = ct.position + ct.rotation * seatLocal;

        driverRoot = new GameObject("DriverStandIn").transform;
        driverRoot.SetParent(ct, false);
        driverRoot.localRotation = Quaternion.identity;
        Vector3 ls = ct.lossyScale;
        driverRoot.localScale = new Vector3(k / Mathf.Max(0.001f, ls.x), k / Mathf.Max(0.001f, ls.y), k / Mathf.Max(0.001f, ls.z));
        driverRoot.position = drvHips;

        // 3) Vô lăng: chi tiết tên có "steer" nếu có (đổi sang đơn vị model khi đã thu nhỏ), không thì trước ngực
        foreach (Transform t in ct.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (!n.Contains("steer")) continue;
            Vector3 w = Quaternion.Inverse(ct.rotation) * (t.position - drvHips) / k;
            if (w.magnitude < 0.9f && w.z > 0.15f) { wheel = w; break; }
        }
        Vector3 gripL = wheel + new Vector3(-0.17f, 0.02f, -0.04f), gripR = wheel + new Vector3(0.17f, 0.02f, -0.04f);
        RealModels.Model Variant(string key, Vector3 hl, Vector3 hr)
        {
            var ik = new RealModels.IK { on = true, handL = hl, handR = hr, footL = footL, footR = footR };
            RealModels.Model m = RealModels.Person(src, srcMat, RealModels.Pose.Sit, ik, $"driver_{key}_{id}");
            if (m == null || !m.ok) return null;
            youPose[key] = PoseObj(m, driverRoot, Vector3.zero, "Driver_" + key);
            return m;
        }
        Variant("drive", gripL, gripR);
        Variant("shock", new Vector3(-0.2f, 0.5f, 0.3f), new Vector3(0.14f, 0.56f, 0.28f));
        RealModels.Model ph = Variant("phone", s < 0f ? gripL : new Vector3(-0.13f, 0.6f, 0.04f), s < 0f ? new Vector3(0.13f, 0.6f, 0.04f) : gripR);
        // đưa tiền: tay phía cửa duỗi ra ngoài cửa sổ, ngang vai
        Vector3 outHand = new Vector3(s * 0.62f, 0.36f, 0.26f) / Mathf.Max(0.8f, k);
        RealModels.Model gv = Variant("give", s < 0f ? outHand : gripL, s < 0f ? gripR : outHand);
        if (ph != null && youPose.TryGetValue("phone", out GameObject pg))
            Prop(pg.transform, s < 0f ? ph.handR : ph.handL, new Vector3(0.075f, 0.15f, 0.012f), new Color(0.06f, 0.06f, 0.07f), "Phone");
        if (gv != null && youPose.TryGetValue("give", out GameObject gg))
            cashProp = Prop(gg.transform, s < 0f ? gv.handL : gv.handR, new Vector3(0.16f, 0.014f, 0.075f), new Color(0.3f, 0.62f, 0.72f), "Cash");
        Show(youPose, "drive");
    }

    /// <summary>Đồ cầm tay (điện thoại / xấp tiền): hộp nhỏ đặt ở bàn tay (toạ độ model).</summary>
    private static GameObject Prop(Transform parent, Vector3 at, Vector3 size, Color col, string name)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = at;
        g.transform.localScale = size;
        Renderer r = g.GetComponent<Renderer>();
        Material m = r.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        m.color = col;
        return g;
    }

    // ================= Kính cửa lái =================
    // Khi "hạ kính": bỏ hẳn tấm kính phía cửa lái (giữa hai cú máy) - để đưa tiền / nói chuyện qua cửa sổ.
    private class Pane { public MeshFilter mf; public Mesh orig; }
    private readonly List<Pane> panes = new List<Pane>();

    private static bool IsGlass(Material m)
    {
        if (m == null) return false;
        string n = m.name.ToLowerInvariant();
        if (n.Contains("light") || n.Contains("lamp") || n.Contains("den")) return false;
        return n.Contains("glass") || n.Contains("window") || n.Contains("kinh") || n.Contains("windshield") || m.renderQueue >= 2900;
    }

    /// <summary>Bỏ các tam giác kính phía cửa lái (ngang ghế lái) khỏi mesh xe; xong đoạn phim trả lại mesh gốc.</summary>
    private void OpenDriverWindow()
    {
        if (panes.Count > 0) return;
        Transform ct = car.transform;
        Quaternion inv = Quaternion.Inverse(ct.rotation);
        foreach (MeshRenderer r in ct.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (driverRoot != null && r.transform.IsChildOf(driverRoot)) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            Material[] mats = r.sharedMaterials;
            Mesh mesh = mf.sharedMesh, cut = null;
            Matrix4x4 toW = r.transform.localToWorldMatrix;
            Vector3[] v = mesh.vertices;
            for (int sub = 0; sub < mats.Length && sub < mesh.subMeshCount; sub++)
            {
                if (!IsGlass(mats[sub])) continue;
                int[] tris = mesh.GetTriangles(sub);
                var keep = new List<int>(tris.Length);
                bool removed = false;
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector3 a = toW.MultiplyPoint3x4(v[tris[i]]), b = toW.MultiplyPoint3x4(v[tris[i + 1]]), c = toW.MultiplyPoint3x4(v[tris[i + 2]]);
                    Vector3 cl = inv * ((a + b + c) / 3f - ct.position);
                    Vector3 nl = inv * Vector3.Cross(b - a, c - a).normalized;
                    bool door = cl.x * doorSide > Mathf.Abs(seatLocal.x) + 0.12f && Mathf.Abs(nl.x) > 0.55f
                                && cl.z > seatLocal.z - 0.6f && cl.z < seatLocal.z + 0.8f && cl.y > seatLocal.y + 0.1f;
                    if (door) { removed = true; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                if (!removed) continue;
                if (cut == null) { cut = Object.Instantiate(mesh); cut.name = mesh.name + "_OpenWindow"; }
                cut.SetTriangles(keep, sub);
            }
            if (cut == null) continue;
            panes.Add(new Pane { mf = mf, orig = mesh });
            mf.sharedMesh = cut;
        }
    }

    private void ClearCarDriver()
    {
        foreach (Pane p in panes)
        {
            if (p.mf == null) continue;
            Mesh cut = p.mf.sharedMesh;
            p.mf.sharedMesh = p.orig;
            if (cut != null && cut != p.orig) Destroy(cut);
        }
        panes.Clear();
        if (driverRoot != null) Destroy(driverRoot.gameObject);
        driverRoot = null; cashProp = null; grabbing = false;
        youPose.Clear();
    }

    // ================= Các cú máy quanh cửa lái =================
    /// <summary>Đứng ngoài cửa lái nhìn vào mặt người lái qua ô cửa sổ.</summary>
    private (Vector3, Vector3) DoorCam() => (DriverHead + DoorOut * 1.9f + youDir * 0.9f + Vector3.up * 0.05f, DriverHead - DoorOut * 0.1f - Vector3.up * 0.1f);

    /// <summary>Trong xe, sau lưng người lái nhìn ra cửa sổ (thấy tay đưa tiền và tên đồng bọn).</summary>
    private (Vector3, Vector3) OverDriverCam() => (DriverHead - DoorOut * 0.75f - youDir * 0.55f + Vector3.up * 0.12f, winPos + Vector3.up * 1.3f);

    /// <summary>Toàn thân "nạn nhân" (khung lấy từ khớp hông, đứng cách xa đủ để không cắt tay chân).</summary>
    private (Vector3, Vector3) VictimCam(float dist = 3f)
    {
        Vector3 c = robPos + Right(youDir) * 0.9f + Vector3.up * 0.4f;
        if (victimMover != null && victimMover.anim != null && victimMover.anim.isHuman)
        {
            Transform hips = victimMover.anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) c = hips.position;
        }
        // đứng phía trước, đối diện ô tô (không bị đồng bọn / người nhà đứng chắn giữa máy quay và nạn nhân)
        Vector3 dir = c - youPos; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : youDir;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        return (c + dir * dist + side * dist * 0.35f + Vector3.up * 1.3f, c + Vector3.up * 0.2f);
    }

    /// <summary>Tên đồng bọn thò tay qua cửa sổ: chuyển xấp tiền sang tay hắn.</summary>
    private void HandCashToAccomplice()
    {
        if (cashProp == null) return;
        Transform hand = accompliceMover != null && accompliceMover.anim != null && accompliceMover.anim.isHuman
            ? accompliceMover.anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null) { cashProp.SetActive(false); return; }
        cashProp.transform.SetParent(hand, false);
        cashProp.transform.localPosition = new Vector3(0f, 0.08f, 0.03f);
        cashProp.transform.localRotation = Quaternion.identity;
        Vector3 ls = hand.lossyScale;
        cashProp.transform.localScale = new Vector3(0.16f / Mathf.Max(1e-4f, ls.x), 0.014f / Mathf.Max(1e-4f, ls.y), 0.075f / Mathf.Max(1e-4f, ls.z));
        cashProp = null;
    }

    /// <summary>Đồng bọn chồm vào cửa sổ (chỉ tay / với vào trong).</summary>
    private void AccompliceReach()
    {
        if (accompliceMover == null || accompliceMover.cp == null) return;
        grabbing = true;
        accompliceMover.cp.Play(ClipPlayer.Has("Pointing") ? "Pointing" : "Angry_Gesture", false, 0.15f);
    }

    /// <summary>
    /// Hạ kính đưa tiền: kính trượt xuống, hai bên nói qua lại, bạn thò tay đưa xấp tiền, tên đồng bọn giật lấy.
    /// short = bị đơ không chọn (chúng tự quyết giá).
    /// </summary>
    private IEnumerator PayScene(bool shortVersion)
    {
        banging = false;
        Show(youPose, "drive");
        OpenDriverWindow();   // kính đã hạ (giữa hai cú máy)
        yield return Shot(2.2f, null, null, DoorCam, () => Say("Bạn", shortVersion ? "Được rồi, được rồi... đừng đập nữa." : "Bình tĩnh đã anh, có gì từ từ nói chuyện.", YouCol));
        if (!shortVersion)
        {
            yield return Shot(2.4f, null, null, OverDriverCam,
                () => Say("Đồng bọn", "Từ từ cái gì? Người ta nằm kia kìa! Năm triệu, không nói nhiều!", ScamCol));
            yield return Shot(2.4f, null, null, DoorCam,
                () => Say("Bạn", "Tôi làm gì có nhiều thế... Tôi chỉ còn từng này thôi.", YouCol));
        }
        Show(youPose, "give");
        yield return Shot(2.2f, null, null, OverDriverCam,
            () => Say("Bạn", "Đây, cầm lấy rồi đi cho.", YouCol));
        AccompliceReach();
        yield return Shot(1.4f, null, null, OverDriverCam, () => { });
        HandCashToAccomplice();
        Show(youPose, "drive");
        yield return Shot(2.4f, null, null, DoorCam,
            () => Say("Đồng bọn", "Ít thế này à? Thôi, coi như hôm nay mày gặp may.", ScamCol));
        grabbing = false;
    }

    /// <summary>Hạ kính nói chuyện -> bị thò tay giật điện thoại và ví.</summary>
    private IEnumerator SnatchScene()
    {
        banging = false;
        Show(youPose, "phone");
        OpenDriverWindow();
        yield return Shot(2.2f, null, null, DoorCam, () => Say("Bạn", "Anh bình tĩnh, công an đang tới rồi đấy...", YouCol));
        yield return Shot(2.2f, null, null, OverDriverCam,
            () => Say("Đồng bọn", "Công an à? Thế cái này tao cầm hộ nhé!", ScamCol));
        AccompliceReach();
        Show(youPose, "shock");
        flash = 0.6f; shake = 0.4f;
        if (youPose.TryGetValue("phone", out GameObject pg)) foreach (Transform t in pg.transform) if (t.name == "Phone") t.gameObject.SetActive(false);
        yield return Shot(2.4f, null, null, OverDriverCam,
            () => Say("", "<i>Một bàn tay thò vào giật phăng điện thoại và ví trên ghế!</i>", Color.white));
        yield return Shot(2f, null, null, DoorCam,
            () => Say("Bạn", "Này! Trả đây! Cướp! Cướp!", YouCol));
        grabbing = false;
    }
}
