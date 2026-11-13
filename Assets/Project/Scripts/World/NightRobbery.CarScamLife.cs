using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "Sống động" cho vụ ăn vạ ô tô:
///  - Người có hoạt ảnh thật (prefab khách hàng + Animator "Speed"): hai "người nhà" CHẠY tới chắn đầu xe,
///    4-5 người hiếu kỳ ĐI tới đứng vòng quanh xem; đứng thì về hoạt ảnh thở, luôn quay mặt về chỗ va chạm.
///  - Bong bóng thoại chen nhau trên đầu từng người (đồng bọn, nạn nhân, người nhà, người xem).
///  - Kẹt xe phía sau: 2 ô tô dừng sau xe người chơi, thỉnh thoảng bóp còi.
///  - Dấu vết va chạm: mảnh nhựa vỡ, gương xe máy văng trên đường, hai vệt phanh sau bánh trước ô tô.
///  - Tiếng ồn đám đông to dần theo số người kéo tới (AudioManager.CrowdBoost).
/// </summary>
public partial class NightRobbery
{
    private class Mover
    {
        public Transform t;
        public Animator anim;
        public bool hasSpeed;
        public Vector3 target, face;
        public float speed;
        public bool arrived;
        public string role;
        public ClipPlayer cp;          // hoạt ảnh Mixamo (Resources/Animations); null = dùng tham số Speed của Animator gốc
        public float nextIdle, fallAt;
        public readonly List<Vector3> path = new List<Vector3>();   // các điểm vòng qua xe (không đi xuyên thân xe)
        public Vector3 lastSet = Vector3.positiveInfinity;        // đích đã tính đường; đổi target từ ngoài -> tính lại
    }

    // Hoạt ảnh đứng theo vai (chọn ngẫu nhiên, đổi sau 3-6 s)
    private static readonly string[] AccompliceClips = { "Yelling", "Angry_Gesture", "Pointing", "Yelling", "Standing_Arguing" };
    private static readonly string[] RelativeClips = { "Standing_Arguing", "Standing_Arguing_1", "Pointing", "Angry_Gesture" };
    private static readonly string[] OnlookerClips = { "Texting_While_Standing", "Texting", "Texting_While_Standing", "Pointing", "Standing_Arguing_1" };

    /// <summary>Có đủ hoạt ảnh Mixamo để dùng người có hoạt ảnh thay cho tư thế tĩnh không.</summary>
    private static bool AnimatedCrowd => ClipPlayer.Has("Yelling") && ClipPlayer.Has("Walking_To_Dying") && ClipPlayer.Has("Walking");
    private readonly List<Mover> movers = new List<Mover>();
    private bool chatter;
    private float nextChat, nextHorn;
    private readonly List<GameObject> jamCars = new List<GameObject>();

    private static readonly string[] AccompliceLines = { "Đền tiền!", "Xuống xe ngay!", "Mở cửa ra!", "Đâm người rồi định chạy à?!", "5 triệu, không nói nhiều!", "Mày có biết tao là ai không?!" };
    private static readonly string[] VictimLines = { "Á á... cái chân!", "Gãy rồi... gãy thật rồi!", "Ối giời ơi!", "Đau quá...!" };
    private static readonly string[] RelativeLines = { "Không đền thì đừng hòng đi!", "Quay lại biển số nó!", "Anh tôi gãy chân rồi!", "Gọi người nhà ra đây!", "Đền ngay!" };
    private static readonly string[] OnlookerLines = { "Ăn vạ đấy, tôi thấy hết!", "Gọi công an đi!", "Lại cái trò này...", "Quay video lại đi!", "Thôi đưa ít tiền cho xong.", "Xe máy tự phanh mà!", "Ai đúng ai sai đây?" };

    /// <summary>Người có hoạt ảnh (prefab khách hàng): gỡ script mua sắm, NavMeshAgent, va chạm; di chuyển bằng tay.</summary>
    private Mover SpawnMover(Vector3 at, Vector3 target, Vector3 face, float speed, string role, GameObject prefab = null)
    {
        if (prefab == null)
        {
            CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
            prefab = spawner != null ? spawner.RandomCustomerPrefab() : null;
        }
        if (prefab == null) return null;
        var holder = new GameObject("_ScamHolder");
        holder.SetActive(false);
        Vector3 dir = target - at; dir.y = 0f;
        GameObject go = Instantiate(prefab, at, Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward), holder.transform);
        for (int pass = 0; pass < 3; pass++)
        {
            MonoBehaviour[] all = go.GetComponentsInChildren<MonoBehaviour>(true);
            if (all.Length == 0) break;
            for (int i = all.Length - 1; i >= 0; i--) if (all[i] != null) DestroyImmediate(all[i]);
        }
        foreach (NavMeshAgent a in go.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(a);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        go.transform.SetParent(transform, true);
        Destroy(holder);
        go.name = "Scam_" + role;
        go.SetActive(true);
        scamPeople.Add(go);
        var m = new Mover { t = go.transform, anim = go.GetComponentInChildren<Animator>(), target = target, face = face, speed = speed, role = role };
        if (m.anim != null) foreach (AnimatorControllerParameter p in m.anim.parameters) if (p.name == "Speed") m.hasSpeed = true;
        if (m.anim != null && m.anim.isHuman && ClipPlayer.Has("Walking")) m.cp = ClipPlayer.On(m.anim);
        m.nextIdle = 0f;
        movers.Add(m);
        return m;
    }

    private static float GroundAt(Vector3 p)
    {
        float best = float.MinValue;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<PlayerCarController>() != null) continue;
            if (h.point.y > best && h.point.y < p.y + 0.6f) best = h.point.y;
        }
        return best > float.MinValue ? best : VietDistrict.RoadTop;
    }

    /// <summary>
    /// Chỗ đứng không đụng tường / cột / xe: nếu bị vướng thì dịch dần về phía xe người chơi (mặt đường).
    /// Cách những người đã đặt ít nhất 0.9 m.
    /// </summary>
    private Vector3 FreeSpot(Vector3 p)
    {
        Vector3 toward = youPos - p; toward.y = 0f;
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : -youDir;
        for (int i = 0; i < 14; i++)
        {
            float gy = GroundAt(p);
            Vector3 a = new Vector3(p.x, gy + 0.55f, p.z), b = new Vector3(p.x, gy + 1.6f, p.z);
            bool blocked = Physics.CheckCapsule(a, b, 0.32f, ~0, QueryTriggerInteraction.Ignore);
            if (!blocked)
                foreach (Mover m in movers)
                    if (m.t != null) { Vector3 q = m.target - p; q.y = 0f; if (q.sqrMagnitude < 0.81f) { blocked = true; break; } }
            if (!blocked) return new Vector3(p.x, gy, p.z);
            p += toward * 0.5f + Vector3.Cross(Vector3.up, toward) * ((i % 2 == 0) ? 0.35f : -0.35f);
        }
        p.y = GroundAt(p);
        return p;
    }

    /// <summary>Hai "người nhà" chạy từ vỉa hè phía trước tới chắn đầu xe; 4-5 người hiếu kỳ đi tới đứng vòng quanh hai bên.</summary>
    private void SpawnCrowdMovers()
    {
        Vector3 center = youPos + youDir * (carFront + 1f);
        float side = lane + 2.6f;   // mép vỉa hè (tính từ tim làn người chơi)
        for (int k = 0; k < 2; k++)
        {
            rail.Sample(sYou + carFront + 13f + k * 3f, k == 0 ? -side - 1f : side, out Vector3 from, out _);
            Vector3 to = FreeSpot(youPos + youDir * (carFront + 1.3f + k * 0.3f) + Right(youDir) * (k == 0 ? -1.2f : 1.3f));
            Mover rel = SpawnMover(FreeSpot(from), to, youPos, 3.8f, "relative");
            if (rel != null) rel.t.position = new Vector3(rel.t.position.x, GroundAt(rel.t.position), rel.t.position.z);
        }
        int n = Random.Range(4, 6);
        for (int k = 0; k < n; k++)
        {
            float sgn = k % 2 == 0 ? 1f : -1f;
            rail.Sample(sYou + Random.Range(-18f, 26f), sgn * (side + Random.Range(0.5f, 2.5f)), out Vector3 from, out _);
            Vector3 to = FreeSpot(center + Right(youDir) * sgn * Random.Range(4.6f, 6.8f) + youDir * Random.Range(-4.5f, 4f));
            from = FreeSpot(from);
            Mover m = SpawnMover(from, to, center, Random.Range(1.2f, 1.6f), "onlooker");
            if (m != null) m.t.position = from;
        }
        chatter = true;
        nextChat = Time.unscaledTime + 0.6f;
        nextHorn = Time.unscaledTime + 2f;
    }

    /// <summary>Người hiếu kỳ / người nhà tản ra (khi ô tô lùi rồi vòng qua): đi nhanh về vỉa hè.</summary>
    private void ScatterMovers()
    {
        foreach (Mover m in movers)
        {
            if (m.t == null || m.role == "victim") continue;
            Vector3 p = m.t.position;
            float s = Vector3.Dot(p - youPos, Right(youDir)) >= 0f ? 1f : -1f;
            m.target = p + Right(youDir) * s * 3.5f;
            m.face = youPos;
            m.speed = 3.2f; m.arrived = false;
        }
    }

    /// <summary>Mỗi frame: người đi / chạy tới chỗ, tới nơi thì đứng (hoạt ảnh thở) quay về chỗ va chạm; bong bóng thoại; còi xe kẹt.</summary>
    private void UpdateCrowd(float dt)
    {
        UpdatePolice(dt);
        int arrived = 0;
        foreach (Mover m in movers)
        {
            if (m.t == null) continue;
            Vector3 p = m.t.position;
            Vector3 d = m.target - p; d.y = 0f;
            float dist = d.magnitude;
            float v = 0f;
            if (!m.arrived && m.target != m.lastSet)
            {
                // Đích mới: tìm đường vòng qua ô tô người chơi / xe CSGT thay vì đi thẳng xuyên qua thân xe
                List<Vector3> pts = Route(p, m.target);
                m.path.Clear();
                m.target = pts[0];
                for (int i = 1; i < pts.Count; i++) m.path.Add(pts[i]);
                m.lastSet = m.target;
                d = m.target - p; d.y = 0f; dist = d.magnitude;
            }
            if (!m.arrived)
            {
                if (dist < 0.15f)
                {
                    if (m.path.Count > 0) { m.target = m.lastSet = m.path[0]; m.path.RemoveAt(0); }
                    else m.arrived = true;
                }
                else
                {
                    float step = Mathf.Min(dist, m.speed * dt);
                    p += d / dist * step;
                    v = m.speed;
                    m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(d / dist), 1f - Mathf.Exp(-10f * dt));
                }
            }
            if (m.arrived)
            {
                arrived++;
                Vector3 f = m.face - p; f.y = 0f;
                if (f.sqrMagnitude > 0.01f) m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(f), 1f - Mathf.Exp(-5f * dt));
            }
            p.y = Mathf.Lerp(p.y, GroundAt(p), 1f - Mathf.Exp(-12f * dt));
            m.t.position = p;
            if (m.cp != null) AnimateMover(m, v);
            else if (m.hasSpeed) m.anim.SetFloat("Speed", v);
        }
        Separate(dt);
        AudioManager.CrowdBoost = chatter ? Mathf.Min(0.35f, 0.08f + 0.05f * arrived) : 0f;

        if (chatter && Time.unscaledTime >= nextChat)
        {
            nextChat = Time.unscaledTime + Random.Range(0.7f, 1.4f);
            var who = new List<(Transform t, string[] lines, float h, string speaker)>();
            if (banging || accomplice != null)
                foreach (ScamActor a in actors)
                    if (a.frames.Contains(accomplice) && a.idx < a.frames.Count && a.frames[a.idx] != null) who.Add((a.frames[a.idx].transform, AccompliceLines, 2.1f, "Đồng bọn"));
            if (victimDown != null && victimDown.activeInHierarchy) who.Add((victimDown.transform, VictimLines, 1.3f, "Nạn nhân"));
            foreach (Mover m in movers)
                if (m.t != null && m.arrived)
                    who.Add((m.t, m.role == "relative" ? RelativeLines : m.role == "accomplice" ? AccompliceLines : m.role == "victim" ? VictimLines
                                  : m.role == "police" ? PoliceLines : OnlookerLines,
                             m.role == "victim" ? 1.1f : 2.1f,
                             m.role == "relative" ? "Người nhà" : m.role == "accomplice" ? "Đồng bọn" : m.role == "victim" ? "Nạn nhân" : m.role == "police" ? "CSGT" : "Người xem"));
            if (who.Count > 0)
            {
                var w = who[Random.Range(0, who.Count)];
                Color c = w.lines == OnlookerLines ? new Color(0.85f, 0.9f, 1f) : ScamCol;
                string line = w.lines[Random.Range(0, w.lines.Length)];
                SpeechBubble.Show(w.t, line, c, w.h, 1.6f);
                nextChat = Time.unscaledTime + Random.Range(1.1f, 2f);
            }
        }
        if (jamCars.Count > 0 && Time.unscaledTime >= nextHorn)
        {
            nextHorn = Time.unscaledTime + Random.Range(1.8f, 4f);
            AudioManager.Play(Random.value < 0.5f ? "horn_1" : "horn_2", 0.3f);
        }
    }

    /// <summary>
    /// Không cho người đi xuyên qua nhau: hai người gần hơn 0.62 m thì đẩy ra hai phía (người đang đứng yên bị đẩy ít hơn);
    /// người đang đi mà bị chắn ngay trước mặt thì bước tạt ngang để vòng qua. "Nạn nhân" nằm dưới đất không tính.
    /// </summary>
    private void Separate(float dt)
    {
        const float R = 0.62f;
        for (int i = 0; i < movers.Count; i++)
        {
            Mover a = movers[i];
            if (a.t == null || a.role == "victim") continue;
            for (int j = i + 1; j < movers.Count; j++)
            {
                Mover b = movers[j];
                if (b.t == null || b.role == "victim") continue;
                Vector3 d = a.t.position - b.t.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist >= R) continue;
                Vector3 n = dist > 1e-3f ? d / dist : Vector3.Cross(Vector3.up, youDir) * (i % 2 == 0 ? 1f : -1f);
                float push = Mathf.Min(R - dist, 2.5f * dt + (R - dist) * 0.5f);
                float wa = a.arrived && !b.arrived ? 0.25f : b.arrived && !a.arrived ? 0.75f : 0.5f;
                a.t.position += n * push * wa;
                b.t.position -= n * push * (1f - wa);
                // người đang đi: lệch hướng sang ngang để đi vòng qua người kia
                if (!a.arrived) a.t.position += Vector3.Cross(Vector3.up, n) * (0.6f * dt);
                if (!b.arrived) b.t.position -= Vector3.Cross(Vector3.up, n) * (0.6f * dt);
            }
        }
    }

    /// <summary>Hai ô tô kẹt lại phía sau xe người chơi (dựng bằng CarModels).</summary>
    private void SpawnJam()
    {
        float[] back = { carFront + 5.8f, carFront + 12.5f };
        for (int k = 0; k < back.Length; k++)
        {
            CarModels.Info info = CarModels.Get(Random.Range(0, CarModels.Variants));
            rail.Sample(sYou - back[k], Random.Range(-0.3f, 0.3f), out Vector3 p, out Vector3 d);
            var go = new GameObject("JamCar");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(d) * Quaternion.Euler(0f, Random.Range(-6f, 6f), 0f));
            go.AddComponent<MeshFilter>().sharedMesh = info.lod0;
            go.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
            jamCars.Add(go);
            scamPeople.Add(go);
        }
    }

    /// <summary>Mảnh nhựa vỡ quanh xe máy đổ, chiếc gương văng ra, hai vệt phanh đen sau bánh trước ô tô.</summary>
    private void SpawnDebris()
    {
        var mb = new VMeshBuilder();
        Color[] cols = { new Color(0.08f, 0.08f, 0.09f), new Color(0.5f, 0.52f, 0.55f), new Color(0.85f, 0.15f, 0.1f), new Color(0.95f, 0.6f, 0.1f) };
        Vector3 c0 = robPos;
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = c0 + Random.insideUnitSphere * 1.6f; p.y = VietDistrict.RoadTop + 0.015f;
            mb.Add(VShape.Cube, Matrix4x4.TRS(p, Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f)),
                new Vector3(Random.Range(0.05f, 0.16f), 0.015f, Random.Range(0.04f, 0.12f))), cols[Random.Range(0, cols.Length)]);
        }
        // gương xe máy văng ra
        Vector3 mp = c0 + Right(youDir) * Random.Range(1.4f, 2.2f) + youDir * Random.Range(0.5f, 1.5f); mp.y = VietDistrict.RoadTop + 0.04f;
        Quaternion mr = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        mb.Add(VShape.Sphere(10), Matrix4x4.TRS(mp, mr, new Vector3(0.11f, 0.08f, 0.03f)), cols[0]);
        mb.Add(VShape.Cylinder(8), Matrix4x4.TRS(mp + mr * new Vector3(0f, -0.14f, 0f), mr, new Vector3(0.012f, 0.22f, 0.012f)), cols[0]);
        // vệt phanh: từ bánh trước ô tô kéo lùi về sau 3 m
        foreach (float sx in new[] { -1f, 1f })
        {
            Vector3 a = youPos + Right(youDir) * sx * (carHalfW - 0.28f) + youDir * (carFront * 0.62f);
            Vector3 b = a - youDir * 3.2f;
            Vector3 mid = (a + b) * 0.5f; mid.y = VietDistrict.RoadTop + 0.006f;
            mb.Add(VShape.Cube, Matrix4x4.TRS(mid, Quaternion.LookRotation(youDir), new Vector3(0.2f, 0.004f, 3.2f)), new Color(0.07f, 0.07f, 0.075f));
        }
        Mesh mesh = mb.ToMesh(RealModels.MeshPrefix + "_ScamDebris");
        mesh.hideFlags = HideFlags.DontSave;
        var go = new GameObject("ScamDebris");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        scamPeople.Add(go);
    }

    /// <summary>Chọn hoạt ảnh cho người: đi / chạy khi di chuyển; đứng thì theo vai (la hét, cãi, chỉ trỏ, quay điện thoại...).</summary>
    private void AnimateMover(Mover m, float v)
    {
        if (m.role == "victim")
        {
            // ôm người đau -> loạng choạng ngã vật ra đường (giữ khung cuối); bị bóc mẽ thì StandUpVictim() đổi hoạt ảnh
            if (m.fallAt > 0f && Time.time >= m.fallAt)
            {
                m.fallAt = 0f;
                m.cp.Play("Walking_To_Dying", false, 0.3f);
                m.anim.applyRootMotion = true;   // bước loạng choạng đi thật vài bước rồi mới ngã (không dậm chân tại chỗ)
            }
            return;
        }
        if (m.role == "mourner")
        {
            // đi chậm rãi; dừng lại thì đứng im phăng phắc (đóng băng tư thế)
            if (v > 0.01f) { if (m.cp.Current != "Walking") m.cp.Play("Walking", true, 0.3f, 0.6f, Random.value); else m.cp.SetSpeed(Mathf.Clamp(v / 1.3f, 0.45f, 1f)); }
            else m.cp.SetSpeed(0f);
            return;
        }
        if (v > 0.01f)
        {
            ClipPlayer.RemovePhone(m.anim);
            m.cp.Play(v > 2.4f ? "Running" : "Walking", true, 0.2f, Mathf.Clamp(v / (v > 2.4f ? 3.8f : 1.4f), 0.7f, 1.4f));
            m.nextIdle = 0f;
            return;
        }
        if (m.role == "accomplice" && banging) { m.cp.Play(ClipPlayer.Has("Knocking") ? "Knocking" : "Angry_Gesture", true, 0.25f); return; }   // gõ / đập cửa kính
        if (m.role == "accomplice" && grabbing) return;   // đang thò tay vào xe (giữ hoạt ảnh do đoạn phim đặt)
        if (Time.time < m.nextIdle) return;
        string[] set = m.role == "accomplice" ? AccompliceClips : m.role == "relative" ? RelativeClips
                     : m.role == "police" ? new[] { "Pointing", "Standing_Arguing_1" } : OnlookerClips;
        string clip = set[Random.Range(0, set.Length)];
        if (!ClipPlayer.Has(clip)) clip = "Standing_Arguing";
        m.cp.Play(clip, true, 0.4f, Random.Range(0.9f, 1.1f), Random.value);
        if (clip.StartsWith("Texting")) ClipPlayer.AttachPhone(m.anim); else ClipPlayer.RemovePhone(m.anim);
        m.nextIdle = Time.time + Random.Range(3f, 6f);
    }

    /// <summary>Đồng bọn xuống xe đi tới cửa lái; "nạn nhân" đứng cạnh xe ôm người rồi ngã vật ra (hoạt ảnh thật).</summary>
    private void SpawnAnimatedScammers(Vector3 victimPos)
    {
        Mover v = SpawnMover(victimPos, victimPos, youPos, 0f, "victim");
        if (v != null)
        {
            v.arrived = true;
            v.t.position = new Vector3(victimPos.x, GroundAt(victimPos), victimPos.z);
            Vector3 f = Right(youDir) + youDir * 0.35f; f.y = 0f;   // loạng choạng ra phía lề, chéo về trước
            v.t.rotation = Quaternion.LookRotation(f.normalized);
            if (v.cp != null) { v.cp.Play(ClipPlayer.Has("Injured_Hurting_Idle") ? "Injured_Hurting_Idle" : "Standing_Arguing", true, 0f); v.fallAt = Time.time + 1.6f; }
            victimMover = v;
        }
        Vector3 from = robPos - Right(youDir) * 1.2f;
        // tới đứng sát cửa lái, quay thẳng mặt vào cửa kính (để gõ kính / nói chuyện qua cửa sổ)
        Mover a = SpawnMover(from, winPos, winPos - DoorOut * 2f, 1.7f, "accomplice");
        if (a != null) a.t.position = new Vector3(from.x, GroundAt(from), from.z);
        if (a != null) accompliceMover = a;
    }

    private Mover victimMover, accompliceMover;

    /// <summary>"Nạn nhân" bị camera hành trình bóc mẽ: đứng bật dậy (chuyển hoạt ảnh từ nằm sang đứng cãi).</summary>
    private void StandUpVictim()
    {
        if (victimMover != null && victimMover.cp != null)
        {
            victimMover.fallAt = 0f;
            victimMover.anim.applyRootMotion = false;
            victimMover.cp.Play("Standing_Arguing", true, 0.5f);
        }
    }

    // ================= Cảnh sát tới (nhánh gọi 113) =================
    private Transform policeCar;
    private float polS, polV, polStopS, nextFlash;
    private bool polStopped, flashRed;
    private Light polRed, polBlue;
    private AudioSource polSiren;
    private float polHalfW = 0.95f, polHalfL = 2.3f, polCenterZ;
    private static readonly string[] PoliceLines = { "Tất cả đứng yên!", "Xuất trình giấy tờ!", "Ai là người bị nạn?", "Về đồn làm việc!" };

    /// <summary>
    /// Xe tuần tra CSGT có sẵn (nhân bản từ hệ thống giao thông) chạy tới từ phía trước trên làn người chơi, còi hú thật (âm 3D, Doppler)
    /// + đèn đỏ xanh nhấp nháy, dừng trước hiện trường; hai CSGT bước ra từ HAI CỬA TRƯỚC, đi vòng qua đầu xe (không xuyên thân xe)
    /// tới chỗ bọn ăn vạ; "nạn nhân" khỏi đau chân ngay; cả nhóm bị dẫn tới đứng xếp hàng cạnh hông xe CSGT rồi chuyển cảnh.
    /// </summary>
    private IEnumerator PoliceArrive()
    {
        banging = false;
        Show(youPose, "drive");
        SpawnPoliceCar();
        if (policeCar == null) { yield return ScamCut("Mười phút sau...", "Còi hú. Công an phường và CSGT tới nơi - cả nhóm bị đưa về đồn."); yield break; }
        // 1. Xe lao tới
        yield return Shot(4.5f, null, null,
            () => (youPos + youDir * (carFront + 3f) - Right(youDir) * 3.2f + Vector3.up * 2f, policeCar != null ? policeCar.position + Vector3.up * 0.8f : youPos),
            () => Say("", "<i>Còi hú vang. Xe CSGT lao tới, đèn xanh đỏ nhấp nháy.</i>", Color.white),
            () => polStopped);
        // 2. Hai CSGT mở cửa trước bước ra, đi vòng đầu xe tới chỗ bọn ăn vạ
        Vector3 pf = policeCar.forward; pf.y = 0f; pf.Normalize();
        Vector3 pr = Right(pf);
        Vector3 pmid = policeCar.position + pf * polCenterZ;
        var cops = new List<Mover>();
        Vector3[] goals =
        {
            accompliceMover != null && accompliceMover.t != null ? accompliceMover.t.position + DoorOut * 1.1f + youDir * 0.4f : winPos + DoorOut * 1.1f,
            youPos + youDir * (carFront + 2.6f) - Right(youDir) * 0.4f,
        };
        for (int k = 0; k < 2; k++)
        {
            float side = k == 0 ? -1f : 1f;   // cửa lái / cửa phụ
            Vector3 door = pmid + pr * side * (polHalfW + 0.45f) + pf * 0.35f;
            door.y = GroundAt(door);
            Mover cop = SpawnMover(door, FreeSpot(goals[k]), youPos + youDir * carFront, 1.5f, "police", k < copPrefabs.Count ? copPrefabs[k] : null);
            if (cop == null) continue;
            cop.t.position = door;
            cop.t.rotation = Quaternion.LookRotation(pr * side);
            DressPolice(cop.t.gameObject, cop.anim);
            cops.Add(cop);
        }
        yield return Shot(3.2f, null, null,
            () => (pmid - pf * (polHalfL + 3f) + pr * 2.5f + Vector3.up * 1.8f, youPos + youDir * carFront + Vector3.up * 1.1f),
            () => Say("CSGT", "Tất cả đứng yên! Xuất trình giấy tờ!", new Color(0.6f, 0.85f, 1f)));
        // 3. "Nạn nhân" khỏi đau chân ngay lập tức
        StandUpVictim();
        yield return Shot(2.6f, null, null,
            () => VictimCam(3.4f),
            () => Say("", "<i>\"Nạn nhân\" bỗng khỏi đau chân, đứng bật dậy định chuồn.</i>", Color.white));
        // 4. Cả nhóm bị dẫn tới đứng xếp hàng cạnh hông xe CSGT (phía có hiện trường), đi vòng chứ không xuyên xe
        float rs = Vector3.Dot(youPos - pmid, pr) >= 0f ? 1f : -1f;
        int slot = 0;
        foreach (Mover m in movers)
        {
            if (m.t == null || m.role == "onlooker" || m.role == "police") continue;
            float along = polHalfL - 0.7f - (slot % 4) * 0.85f;
            float outward = polHalfW + 0.75f + (slot / 4) * 0.8f;
            Vector3 spot = pmid + pr * rs * outward + pf * along;
            spot.y = GroundAt(spot);
            m.target = spot;
            m.face = pmid; m.speed = 1.3f; m.arrived = false;
            if (m.anim != null) m.anim.applyRootMotion = false;
            slot++;
        }
        // CSGT đi kèm hai đầu hàng
        for (int k = 0; k < cops.Count; k++)
        {
            Vector3 spot = pmid + pr * rs * (polHalfW + 1.9f) + pf * (k == 0 ? polHalfL - 0.3f : polHalfL - 3.2f);
            spot.y = GroundAt(spot);
            cops[k].target = spot; cops[k].face = pmid + pf * (polHalfL - 1.6f); cops[k].arrived = false;
        }
        yield return Shot(3.4f, null, null,
            () => (youPos + youDir * (carFront + 8f) + Right(youDir) * 3f + Vector3.up * 3f, Vector3.Lerp(youPos + youDir * carFront, pmid, 0.6f) + Vector3.up * 0.8f),
            () => Say("CSGT", "Dàn cảnh va chạm để cưỡng đoạt tiền à? Cả nhóm về đồn làm việc!", new Color(0.6f, 0.85f, 1f)));
        yield return ScamCut("Một lúc sau...", "Cả nhóm bị đưa về đồn. Camera hành trình của bạn là bằng chứng.");
    }

    private float polYOff;
    private readonly List<GameObject> copPrefabs = new List<GameObject>();

    /// <summary>
    /// Chuẩn bị trước (chạy nền từ đầu đoạn phim): chọn sẵn 2 người làm CSGT, vẽ đồng phục dần qua nhiều frame,
    /// tạo sẵn tiếng còi - để lúc xe CSGT tới không bị khựng game.
    /// </summary>
    private IEnumerator PrepPolice()
    {
        copPrefabs.Clear();
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        for (int k = 0; k < 2 && spawner != null; k++)
        {
            GameObject p = spawner.RandomCustomerPrefab();
            if (p != null) copPrefabs.Add(p);
        }
        yield return null;
        SynthAudio.Siren();
        foreach (GameObject p in copPrefabs.ToArray())
        {
            yield return null;
            if (p != null) yield return PaintOutfitCo(p, true, PoliceColors, "csgt");
        }
    }

    /// <summary>Nhân bản xe tuần tra CSGT có sẵn của hệ thống giao thông (thân trắng sọc xanh, biển xanh), bỏ script lái, thêm đèn chớp + còi.</summary>
    private void SpawnPoliceCar()
    {
        GameObject tpl = TrafficSystem.PoliceTemplate;
        if (tpl == null) return;
        var holder = new GameObject("_PoliceHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(tpl, holder.transform);
        foreach (Camera c in go.GetComponentsInChildren<Camera>(true)) DestroyImmediate(c.gameObject);
        for (int pass = 0; pass < 3; pass++)
        {
            MonoBehaviour[] all = go.GetComponentsInChildren<MonoBehaviour>(true);
            if (all.Length == 0) break;
            for (int i = all.Length - 1; i >= 0; i--) if (all[i] != null) DestroyImmediate(all[i]);
        }
        foreach (AudioListener l in go.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(l);
        foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(a);
        foreach (WheelCollider w in go.GetComponentsInChildren<WheelCollider>(true)) DestroyImmediate(w);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        go.transform.SetParent(transform, true);
        Destroy(holder);
        go.name = "PoliceCar";
        go.SetActive(true);
        // Đặt bánh chạm mặt đường + kích thước thật của xe (để người đi vòng qua, CSGT bước ra đúng cửa)
        float top = 1.5f;
        polYOff = 0f; polHalfW = 0.95f; polHalfL = 2.3f; polCenterZ = 0f;
        if (ModelFit.MeshBounds(go.transform, go.transform, out Bounds b))
        {
            Vector3 ls = go.transform.lossyScale;
            polYOff = -b.min.y * ls.y;
            top = b.max.y * ls.y;
            float hw = b.extents.x * ls.x, hl = b.extents.z * ls.z;
            if (hw > 0.6f && hw < 1.5f) polHalfW = hw;
            if (hl > 1.5f && hl < 3.5f) { polHalfL = hl; polCenterZ = b.center.z * ls.z; }
        }
        Light L(Color c, float x)
        {
            var l = new GameObject("Beacon").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            Vector3 ls = go.transform.lossyScale;
            l.transform.localPosition = new Vector3(x / Mathf.Max(1e-4f, ls.x), (top + 0.35f) / Mathf.Max(1e-4f, ls.y), 0f);
            l.type = LightType.Point; l.range = 14f; l.intensity = 0f; l.color = c;
            return l;
        }
        polRed = L(new Color(1f, 0.15f, 0.1f), -0.35f);
        polBlue = L(new Color(0.2f, 0.4f, 1f), 0.35f);
        // Còi hú: âm 3D gắn trên xe, có hiệu ứng Doppler khi xe lao tới
        polSiren = go.AddComponent<AudioSource>();
        polSiren.clip = SynthAudio.Siren();
        polSiren.loop = true;
        polSiren.spatialBlend = 1f;
        polSiren.dopplerLevel = 1.2f;
        polSiren.rolloffMode = AudioRolloffMode.Logarithmic;
        polSiren.minDistance = 12f; polSiren.maxDistance = 220f;
        polSiren.volume = 0.85f;
        polSiren.time = Random.Range(0f, 3.5f);
        polSiren.Play();
        policeCar = go.transform;
        scamPeople.Add(go);
        polS = sYou + 85f; polV = 16f; polStopS = sYou + carFront + 10f + polHalfL; polStopped = false;   // chạy tới từ phía trước, cùng làn
        UpdatePolice(0f);
    }

    /// <summary>Xe CSGT giảm tốc êm tới điểm dừng; đèn đỏ / xanh luân phiên; dừng rồi thì còi nhỏ dần (vẫn hú như xe đỗ làm nhiệm vụ).</summary>
    private void UpdatePolice(float dt)
    {
        if (policeCar == null) return;
        // Đi ngược chiều trên làn của người chơi (đường phía trước đang trống) -> không lấn dải phân cách giữa đường
        float rem = polS - polStopS;
        float want = Mathf.Clamp(rem * 1.1f, 0f, 16f);
        polV = Mathf.MoveTowards(polV, want, 9f * dt);
        polS -= polV * dt;
        polStopped = rem < 0.3f && polV < 0.5f;
        rail.Sample(polS, 0.3f, out Vector3 p, out Vector3 d);
        policeCar.SetPositionAndRotation(p + Vector3.up * polYOff, Quaternion.LookRotation(-d));
        if (Time.unscaledTime >= nextFlash)
        {
            nextFlash = Time.unscaledTime + 0.22f;
            flashRed = !flashRed;
            if (polRed != null) polRed.intensity = flashRed ? 6f : 0f;
            if (polBlue != null) polBlue.intensity = flashRed ? 0f : 6f;
        }
        if (polSiren != null) polSiren.volume = Mathf.MoveTowards(polSiren.volume, polStopped ? 0.35f : 0.85f, dt * 0.25f);
    }

    // ================= Đi vòng qua xe =================
    private struct Box { public Vector3 c, f; public float hw, hl; }

    /// <summary>Thân ô tô người chơi + xe CSGT (hình chữ nhật trên mặt đường, nới thêm 0.5 m).</summary>
    private List<Box> Obstacles()
    {
        var list = new List<Box>();
        if (car != null) list.Add(new Box { c = youPos + youDir * carCenterZ, f = youDir, hw = carHalfW + 0.5f, hl = carHalfL + 0.5f });
        if (policeCar != null)
        {
            Vector3 pf = policeCar.forward; pf.y = 0f; pf.Normalize();
            list.Add(new Box { c = policeCar.position + pf * polCenterZ, f = pf, hw = polHalfW + 0.5f, hl = polHalfL + 0.5f });
        }
        return list;
    }

    private static Vector2 Local(Box b, Vector3 p)
    {
        Vector3 d = p - b.c; Vector3 r = Vector3.Cross(Vector3.up, b.f);
        return new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, b.f));
    }

    private static Vector3 World(Box b, Vector2 l) => b.c + Vector3.Cross(Vector3.up, b.f) * l.x + b.f * l.y;

    /// <summary>Đoạn a-b có cắt hình chữ nhật (thu nhỏ một chút để đi men theo mép không bị tính là cắt) không.</summary>
    private static bool Cuts(Box b, Vector3 a3, Vector3 b3, float shrink = 0.4f)
    {
        Vector2 a = Local(b, a3), e = Local(b, b3), d = e - a;
        float hw = b.hw - shrink, hl = b.hl - shrink, t0 = 0f, t1 = 1f;
        bool Clip(float p, float q) { if (Mathf.Abs(p) < 1e-6f) return q >= 0f; float r = q / p; if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; } else { if (r < t0) return false; if (r < t1) t1 = r; } return true; }
        return Clip(-d.x, a.x + hw) && Clip(d.x, hw - a.x) && Clip(-d.y, a.y + hl) && Clip(d.y, hl - a.y) && t0 < t1;
    }

    /// <summary>Đường đi từ a tới b vòng qua các thân xe: đi theo các góc (đã nới rộng) của xe chắn đường, chọn phía ngắn hơn.</summary>
    private List<Vector3> Route(Vector3 a, Vector3 b)
    {
        var res = new List<Vector3>();
        List<Box> obs = Obstacles();
        Vector3 cur = a;
        var used = new HashSet<int>();
        for (int iter = 0; iter < 3; iter++)
        {
            int hit = -1;
            for (int i = 0; i < obs.Count; i++) if (!used.Contains(i) && Cuts(obs[i], cur, b)) { hit = i; break; }
            if (hit < 0) break;
            used.Add(hit);
            Box o = obs[hit];
            Vector3[] cn = { World(o, new Vector2(o.hw, o.hl)), World(o, new Vector2(-o.hw, o.hl)), World(o, new Vector2(-o.hw, -o.hl)), World(o, new Vector2(o.hw, -o.hl)) };
            float best = float.MaxValue; List<Vector3> bestPath = null;
            for (int i = 0; i < 4; i++)
            {
                if (Cuts(o, cur, cn[i])) continue;
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    var path = new List<Vector3> { cn[i] };
                    float len = Vector3.Distance(cur, cn[i]);
                    int j = i;
                    for (int step = 0; step < 4 && Cuts(o, cn[j], b); step++)
                    {
                        int n = (j + dir + 4) % 4;
                        len += Vector3.Distance(cn[j], cn[n]);
                        path.Add(cn[n]); j = n;
                    }
                    if (Cuts(o, cn[j], b)) continue;
                    len += Vector3.Distance(cn[j], b);
                    if (len < best) { best = len; bestPath = path; }
                }
            }
            if (bestPath == null) break;
            foreach (Vector3 q in bestPath) res.Add(new Vector3(q.x, GroundAt(q), q.z));
            cur = res[res.Count - 1];
        }
        res.Add(b);
        return res;
    }

    private static Mesh capMesh, batonMesh, shoulderMesh, beltMesh;
    private static readonly Dictionary<string, Material> outfitCache = new Dictionary<string, Material>();   // theo mesh + bộ màu
    /// <summary>Màu theo vùng: 0 giữ nguyên (da), 1 áo, 2 quần, 3 giày. CSGT: áo vàng, quần xanh lá đậm, giày đen.</summary>
    private static readonly Color[] PoliceColors = { Color.white, new Color(0.93f, 0.8f, 0.3f), new Color(0.2f, 0.29f, 0.17f), new Color(0.05f, 0.05f, 0.05f) };

    /// <summary>
    /// Vẽ đồng phục CSGT lên texture riêng: nhân vật Mixamo chỉ có 1 vật liệu "body" (da + quần áo chung 1 texture),
    /// nên không tô màu cả vật liệu được. Dựa vào xương mỗi đỉnh bám theo nhiều nhất:
    ///   thân, vai, bắp tay -> áo vàng; đùi, cẳng chân -> quần xanh lá đậm; bàn chân -> giày đen;
    ///   cẳng tay, bàn tay, cổ, đầu -> giữ nguyên (da, áo cộc tay).
    /// Màu mới nhân với độ sáng texture cũ nên vẫn giữ nếp vải. Kết quả được nhớ theo mesh (tạo 1 lần).
    /// </summary>
    private static void PaintUniform(GameObject go, Animator anim)
    {
        IEnumerator e = PaintOutfitCo(go, false, PoliceColors, "csgt");
        while (e.MoveNext()) { }
    }

    /// <summary>Loại vùng theo tên xương Mixamo (dùng được cả trên prefab chưa tạo ra): 1 áo, 2 quần, 3 giày, 0 giữ nguyên (da).</summary>
    private static int BoneClass(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("toe") || n.Contains("foot")) return 3;
            if (n.Contains("leg")) return 2;
            if (n.Contains("forearm") || n.Contains("hand") || n.Contains("neck") || n.Contains("head") || n.Contains("finger")
                || n.Contains("thumb") || n.Contains("index") || n.Contains("middle") || n.Contains("ring") || n.Contains("pinky")) return 0;
            if (n.Contains("arm") || n.Contains("shoulder") || n.Contains("spine") || n.Contains("chest") || n.Contains("hips")) return 1;
        }
        return 0;
    }

    /// <summary>
    /// Vẽ đồng phục (xem PaintUniform). sliced = chia nhỏ ra nhiều frame (~4 ms / frame) để chuẩn bị trước, không làm khựng game.
    /// </summary>
    private static IEnumerator PaintOutfitCo(GameObject go, bool sliced, Color[] col, string tag, float gore = 0f)
    {
        // gore > 0: da tái nhợt xám xanh + loang máu (nhiều ở đầu / tay, áo vừa, quần ít)
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) continue;
            string n = (smr.name + " " + (smr.sharedMaterial != null ? smr.sharedMaterial.name : "")).ToLowerInvariant();
            if (n.Contains("hair") || n.Contains("eye") || n.Contains("lash") || n.Contains("teeth")) continue;
            string key = mesh.GetHashCode() + "_" + mesh.name + "_" + tag;
            if (outfitCache.TryGetValue(key, out Material cached) && cached != null) { if (!sliced) smr.sharedMaterial = cached; continue; }
            Material src = smr.sharedMaterial;
            Texture srcTex = src != null ? (src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture) : null;
            if (!mesh.isReadable || srcTex == null)
            {
                Debug.LogWarning($"[CSGT] Không vẽ được đồng phục cho '{smr.name}': mesh chưa bật Read/Write hoặc vật liệu không có texture.");
                continue;
            }

            // Chép texture gốc ra bản đọc được (kể cả khi texture không bật Read/Write)
            int w = Mathf.Min(srcTex.width, 1024), h = Mathf.Min(srcTex.height, 1024);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(srcTex, rt);
            var prevRt = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = srcTex.name + "_" + tag, hideFlags = HideFlags.DontSave };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prevRt;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = tex.GetPixels32();

            Transform[] bones = smr.bones;
            BoneWeight[] bw = mesh.boneWeights;
            Vector2[] uv = mesh.uv;
            int[] tris = mesh.triangles;
            if (bw == null || bw.Length != uv.Length || uv.Length == 0) continue;
            var boneCls = new int[bones.Length];
            for (int bi = 0; bi < bones.Length; bi++) boneCls[bi] = BoneClass(bones[bi]);
            var vc = new int[uv.Length];
            for (int v = 0; v < uv.Length; v++) { int bi = bw[v].boneIndex0; vc[v] = bi >= 0 && bi < boneCls.Length ? boneCls[bi] : 0; }
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                if (sliced && clock.ElapsedMilliseconds > 4) { yield return null; clock.Restart(); if (tex == null) yield break; }
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int k = vc[a] == vc[b] || vc[a] == vc[c] ? vc[a] : vc[b] == vc[c] ? vc[b] : 0;   // đa số 3 đỉnh
                if ((k == 0 || col == null) && gore <= 0f) continue;
                Vector2 A = Wrap(uv[a]) * new Vector2(w, h), B = Wrap(uv[b]) * new Vector2(w, h), C = Wrap(uv[c]) * new Vector2(w, h);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))) + 1);
                float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
                if (Mathf.Abs(area) < 1e-6f || (x1 - x0) * (y1 - y0) > 400000) continue;
                Color target = k == 0 ? (tag == "tang" || tag == "xac" ? new Color(0.9f, 0.92f, 0.95f) : new Color(0.74f, 0.77f, 0.8f)) : col != null ? col[k] : Color.white;   // đoàn đưa tang: trắng bệch
                bool keep = (col == null && k != 0) || (k == 0 && tag == "fiend");   // giữ màu gốc, chỉ thêm máu
                float bloodiness = gore * (k == 0 ? 0.95f : k == 1 ? 0.7f : k == 2 ? 0.4f : 0.15f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 P = new Vector2(x + 0.5f, y + 0.5f);
                        float w0 = ((B.x - P.x) * (C.y - P.y) - (C.x - P.x) * (B.y - P.y)) / area;
                        float w1 = ((C.x - P.x) * (A.y - P.y) - (A.x - P.x) * (C.y - P.y)) / area;
                        float w2 = 1f - w0 - w1;
                        const float e = -0.04f;   // nới rộng chút để không hở đường viền giữa các mảng UV
                        if (w0 < e || w1 < e || w2 < e) continue;
                        int idx = y * w + x;
                        Color32 o = px[idx];
                        float lum = (o.r * 0.3f + o.g * 0.59f + o.b * 0.11f) / 255f;
                        float shade = Mathf.Lerp(0.62f, 1.12f, lum);   // giữ nếp vải / bóng đổ của texture cũ
                        Color outc = keep ? (Color)o : new Color(Mathf.Clamp01(target.r * shade), Mathf.Clamp01(target.g * shade), Mathf.Clamp01(target.b * shade), 1f);
                        if (bloodiness > 0f)
                        {
                            float bn = Mathf.PerlinNoise(x * 0.035f + 13.1f, y * 0.035f) * 0.75f + Mathf.PerlinNoise(x * 0.16f, y * 0.16f + 5.3f) * 0.35f;
                            if (bn > 1.02f - bloodiness * 0.55f)
                            {
                                float wet = Mathf.Clamp01((bn - (1.02f - bloodiness * 0.55f)) * 6f);
                                outc = Color.Lerp(outc, new Color(0.38f + 0.12f * lum, 0.015f, 0.015f, 1f), 0.55f + 0.4f * wet);
                            }
                        }
                        px[idx] = outc;
                    }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            var mat = new Material(src) { name = src.name + "_" + tag, hideFlags = HideFlags.DontSave };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            outfitCache[key] = mat;
            if (!sliced) smr.sharedMaterial = mat;   // chuẩn bị trước trên prefab: chỉ nhớ lại, không sửa prefab
            if (sliced) { yield return null; clock.Restart(); }
        }
    }

    private static Vector2 Wrap(Vector2 uv) => new Vector2(uv.x - Mathf.Floor(uv.x), uv.y - Mathf.Floor(uv.y));

    /// <summary>
    /// CSGT Việt Nam: áo vàng, quần xanh lá đậm, giày đen (giữ nếp vải của texture; da / tóc giữ nguyên);
    /// mũ kê-pi vàng kem viền đỏ có quân hiệu gắn vào xương đầu; gậy chỉ huy sọc trắng - đỏ ở tay phải.
    /// Phân loại áo / quần / giày theo tên vật liệu + tên mesh (Mixamo: ..._Shirt, ..._Pants, ..._Shoes...).
    /// </summary>
    private static void DressPolice(GameObject go, Animator anim)
    {
        if (anim != null && anim.isHuman) PaintUniform(go, anim);
        if (anim == null || !anim.isHuman) return;
        if (capMesh == null)
        {
            var mb = new VMeshBuilder();
            Color top = new Color(0.86f, 0.78f, 0.45f), band = new Color(0.72f, 0.06f, 0.06f), visor = new Color(0.06f, 0.06f, 0.07f), gold = new Color(0.98f, 0.82f, 0.2f);
            mb.Add(VShape.Cylinder(16), Matrix4x4.TRS(new Vector3(0f, 0.035f, 0f), Quaternion.identity, new Vector3(0.205f, 0.07f, 0.225f)), band);      // vành đỏ
            mb.Add(VShape.RoundBox(0.4f, 14), Matrix4x4.TRS(new Vector3(0f, 0.09f, 0.012f), Quaternion.Euler(-6f, 0f, 0f), new Vector3(0.27f, 0.07f, 0.3f)), top); // chóp mũ
            mb.Add(VShape.RoundBox(0.4f, 10), Matrix4x4.TRS(new Vector3(0f, 0.008f, 0.13f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.19f, 0.012f, 0.09f)), visor); // lưỡi trai
            mb.Add(VShape.Sphere(10), Matrix4x4.TRS(new Vector3(0f, 0.065f, 0.118f), Quaternion.identity, new Vector3(0.045f, 0.045f, 0.012f)), gold);     // quân hiệu
            mb.Add(VShape.Sphere(8), Matrix4x4.TRS(new Vector3(0f, 0.065f, 0.123f), Quaternion.identity, new Vector3(0.022f, 0.022f, 0.006f)), band);
            capMesh = mb.ToMesh(RealModels.MeshPrefix + "_CSGTCap");
            capMesh.hideFlags = HideFlags.DontSave;
            var bb = new VMeshBuilder();
            for (int k = 0; k < 6; k++)
                bb.Add(VShape.Cylinder(10), Matrix4x4.TRS(new Vector3(0f, 0.04f + k * 0.07f, 0f), Quaternion.identity, new Vector3(0.032f, 0.07f, 0.032f)), k % 2 == 0 ? Color.white : band);
            bb.Add(VShape.Cylinder(10), Matrix4x4.TRS(new Vector3(0f, -0.03f, 0f), Quaternion.identity, new Vector3(0.036f, 0.08f, 0.036f)), visor);
            batonMesh = bb.ToMesh(RealModels.MeshPrefix + "_CSGTBaton");
            batonMesh.hideFlags = HideFlags.DontSave;
        }
        GameObject Attach(Transform bone, Mesh mesh, string name, Vector3 offset, Quaternion rot)
        {
            var g = new GameObject(name);
            g.transform.SetParent(bone, false);
            Vector3 ls = bone.lossyScale;
            g.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
            g.transform.position = bone.position + anim.transform.rotation * offset;
            g.transform.rotation = anim.transform.rotation * rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
            return g;
        }
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) Attach(head, capMesh, "CSGTCap", new Vector3(0f, 0.15f, 0.01f), Quaternion.identity);
        // Cầu vai (cấp hiệu) xanh có vạch vàng trên hai vai; thắt lưng nâu khoá vàng
        if (shoulderMesh == null)
        {
            var sb = new VMeshBuilder();
            sb.Add(VShape.RoundBox(0.25f, 8), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.06f, 0.012f, 0.13f)), new Color(0.2f, 0.32f, 0.2f));
            sb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0.007f, -0.02f), Quaternion.identity, new Vector3(0.05f, 0.002f, 0.012f)), new Color(0.95f, 0.8f, 0.2f));
            sb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0.007f, 0.02f), Quaternion.identity, new Vector3(0.05f, 0.002f, 0.012f)), new Color(0.95f, 0.8f, 0.2f));
            shoulderMesh = sb.ToMesh(RealModels.MeshPrefix + "_CSGTShoulder");
            shoulderMesh.hideFlags = HideFlags.DontSave;
            var bb2 = new VMeshBuilder();
            bb2.Add(VShape.Torus(0.12f, 24), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.36f, 0.25f, 0.27f)), new Color(0.3f, 0.18f, 0.1f));
            bb2.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0f, 0.135f), Quaternion.identity, new Vector3(0.06f, 0.045f, 0.012f)), new Color(0.95f, 0.8f, 0.2f));
            beltMesh = bb2.ToMesh(RealModels.MeshPrefix + "_CSGTBelt");
            beltMesh.hideFlags = HideFlags.DontSave;
        }
        foreach (HumanBodyBones sh in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
        {
            Transform up = anim.GetBoneTransform(sh);
            if (up == null) continue;
            float side = sh == HumanBodyBones.LeftUpperArm ? -1f : 1f;
            Attach(up, shoulderMesh, "CSGTShoulder", new Vector3(-side * 0.06f, 0.075f, 0f), Quaternion.identity).transform.SetParent(up, true);
        }
        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        if (hips != null) Attach(hips, beltMesh, "CSGTBelt", new Vector3(0f, 0.08f, 0.01f), Quaternion.identity);
        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand != null)
        {
            var g = new GameObject("CSGTBaton");
            g.transform.SetParent(hand, false);
            Vector3 ls = hand.lossyScale;
            g.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
            g.transform.position = hand.position + hand.up * 0.07f;
            g.transform.rotation = hand.rotation * Quaternion.Euler(90f, 0f, 0f);   // gậy vuông góc với bàn tay, chĩa ra trước
            g.AddComponent<MeshFilter>().sharedMesh = batonMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        }
    }

    private void ClearCrowd()
    {
        policeCar = null; polRed = polBlue = null; polSiren = null;
        movers.Clear();
        victimMover = accompliceMover = null;
        jamCars.Clear();
        chatter = false;
        AudioManager.CrowdBoost = 0f;
    }
}

static class VecExt
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
