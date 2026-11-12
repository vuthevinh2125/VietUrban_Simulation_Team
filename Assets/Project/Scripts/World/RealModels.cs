using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dùng model thật (nhân vật Mixamo của game, xe Honda Super Cub C125) cho người và xe trong thành phố mà vẫn nhẹ:
///  - Người: tạo bản sao model, đặt tư thế (đứng / ngồi ghế nhựa / ngồi xổm / lái xe máy) bằng cách xoay xương tay chân
///    theo hướng mong muốn, rồi "nướng" thành mesh tĩnh (BakeMesh) -> không cần Animator, không tốn skinning mỗi khung hình.
///  - Xe: gộp hàng trăm mảnh của model xe thành 1 mesh (mỗi vật liệu một submesh).
///  - Mỗi model có bản rút gọn (gộp đỉnh theo lưới, LOD1) cho tầm xa; LODGroup tự đổi bản và ẩn khi quá xa.
/// Kết quả được dùng chung cho mọi bản sao (cache theo model + tư thế). Cần mesh gốc bật Read/Write (menu 20 tự bật).
/// </summary>
public static class RealModels
{
    public enum Pose { Stand, Sit, Squat, Ride, RideBack }

    /// <summary>Đích tay / chân (tính từ khớp hông, hệ toạ độ nhân vật: x phải, y lên, z trước) cho tư thế lái xe.</summary>
    public struct IK
    {
        public bool on;
        public Vector3 handL, handR, footL, footR;
    }

    public class Part
    {
        public Mesh mesh;
        public Material[] mats;
        public Matrix4x4 local;
    }

    public class Model
    {
        public readonly List<Part> lod0 = new List<Part>(), lod1 = new List<Part>();
        public Vector3 hips, head;
        public Bounds bounds;
        public bool ok;
    }

    private static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();
    public const string MeshPrefix = "VBaked";

    // ================= Xe / đồ vật tĩnh =================
    /// <summary>Gộp mọi MeshFilter đang hiện của prefab thành 1 mesh nhiều submesh (LOD0) + bản rút gọn 1 vật liệu bảng màu (LOD1).</summary>
    public static Model Prop(GameObject prefab, float lodCell = 0.05f)
    {
        if (prefab == null) return null;
        string key = "prop:" + prefab.GetHashCode();
        if (cache.TryGetValue(key, out Model cached)) return cached;
        var model = new Model();
        cache[key] = model;

        var holder = new GameObject("_bake");
        holder.SetActive(false);
        GameObject g = Object.Instantiate(prefab, holder.transform, false);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        Matrix4x4 rootInv = Matrix4x4.TRS(g.transform.position, g.transform.rotation, Vector3.one).inverse;

        var byMat = new Dictionary<Material, List<CombineInstance>>();
        foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            var mr = mf.GetComponent<MeshRenderer>();
            if (m == null || mr == null || !mr.enabled || !ActiveInPrefab(mf.transform, g.transform)) continue;
            if (!m.isReadable) { Debug.LogWarning($"[RealModels] Mesh '{m.name}' chưa bật Read/Write - bỏ qua"); continue; }
            Material[] mats = mr.sharedMaterials;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                Material mat = s < mats.Length ? mats[s] : mats.Length > 0 ? mats[mats.Length - 1] : null;
                if (mat == null) continue;
                if (!byMat.TryGetValue(mat, out List<CombineInstance> list)) byMat[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = m, subMeshIndex = s, transform = rootInv * mf.transform.localToWorldMatrix });
            }
        }
        Object.DestroyImmediate(holder);
        if (byMat.Count == 0) return model;

        // LOD0: một submesh mỗi vật liệu
        var perMat = new List<CombineInstance>();
        var matList = new List<Material>();
        foreach (var kv in byMat)
        {
            var sub = new Mesh { indexFormat = IndexFormat.UInt32 };
            sub.CombineMeshes(kv.Value.ToArray(), true, true);
            perMat.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
            matList.Add(kv.Key);
        }
        var full = new Mesh { name = MeshPrefix + "_" + prefab.name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
        full.CombineMeshes(perMat.ToArray(), false, false);
        full.RecalculateBounds();
        foreach (CombineInstance ci in perMat) Object.DestroyImmediate(ci.mesh);
        model.lod0.Add(new Part { mesh = full, mats = matList.ToArray(), local = Matrix4x4.identity });

        // LOD1: gộp đỉnh theo lưới, tô màu theo màu vật liệu (1 vật liệu bảng màu chung)
        var colors = new Color[matList.Count];
        for (int i = 0; i < matList.Count; i++) colors[i] = MatColor(matList[i]);
        Mesh low = DecimateColored(full, colors, lodCell);
        low.name = MeshPrefix + "_" + prefab.name + "_LOD1";
        model.lod1.Add(new Part { mesh = low, mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        model.bounds = full.bounds;
        model.ok = true;
        return model;
    }

    private static bool ActiveInPrefab(Transform t, Transform root)
    {
        for (Transform c = t; c != null; c = c.parent)
        {
            if (!c.gameObject.activeSelf) return false;
            if (c == root) break;
        }
        return true;
    }

    private static Color MatColor(Material m)
    {
        Color c = Color.gray;
        if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
        else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
        bool textured = (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
        if (textured && c.r > 0.95f && c.g > 0.95f && c.b > 0.95f) c = new Color(0.55f, 0.55f, 0.56f);
        c.a = 1f;
        return c;
    }

    // ================= Người =================
    /// <summary>Nướng model người (FBX humanoid) theo một tư thế; vật liệu lấy từ prefab NPC tương ứng (nếu có).</summary>
    public static Model Person(GameObject model, GameObject materialSource, Pose pose, float lodCell = 0.06f) =>
        Person(model, materialSource, pose, default, "", lodCell);

    /// <summary>Như trên, kèm đích IK cho tay / chân (vd. tay nắm đúng tay lái, chân đặt đúng chỗ để chân của từng mẫu xe).</summary>
    public static Model Person(GameObject model, GameObject materialSource, Pose pose, IK ik, string tag, float lodCell = 0.06f)
    {
        if (model == null) return null;
        string key = $"person:{model.GetHashCode()}:{pose}:{tag}";
        if (cache.TryGetValue(key, out Model cached)) return cached;
        var result = new Model();
        cache[key] = result;

        GameObject g = Object.Instantiate(model, new Vector3(0f, -2000f, 0f), Quaternion.identity);
        g.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            Animator a = g.GetComponentInChildren<Animator>();
            if (a == null || !a.isHuman) { Debug.LogWarning($"[RealModels] {model.name} không phải humanoid"); return result; }
            ApplyPose(a, g.transform, pose);
            if (ik.on) ApplyIK(a, g.transform, ik);
            Matrix4x4 rootInv = Matrix4x4.TRS(g.transform.position, g.transform.rotation, Vector3.one).inverse;
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), head = a.GetBoneTransform(HumanBodyBones.Head);
            result.hips = hips != null ? rootInv.MultiplyPoint3x4(hips.position) : new Vector3(0f, 0.95f, 0f);
            result.head = head != null ? rootInv.MultiplyPoint3x4(head.position) : new Vector3(0f, 1.6f, 0f);

            var srcMats = new Dictionary<string, Material[]>();
            if (materialSource != null)
                foreach (SkinnedMeshRenderer s in materialSource.GetComponentsInChildren<SkinnedMeshRenderer>(true)) srcMats[s.name] = s.sharedMaterials;
            bool any = false;
            foreach (SkinnedMeshRenderer smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null || !smr.gameObject.activeSelf) continue;
                if (!smr.sharedMesh.isReadable) { Debug.LogWarning($"[RealModels] Mesh '{smr.sharedMesh.name}' chưa bật Read/Write - bỏ qua"); continue; }
                var baked = new Mesh { name = $"{MeshPrefix}_{model.name}_{pose}_{smr.name}", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                smr.BakeMesh(baked, true);
                baked.RecalculateBounds();
                Material[] mats = srcMats.TryGetValue(smr.name, out Material[] m) ? m : smr.sharedMaterials;
                Matrix4x4 local = rootInv * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                result.lod0.Add(new Part { mesh = baked, mats = mats, local = local });
                Mesh low = Decimate(baked, lodCell);
                low.name = baked.name + "_LOD1";
                result.lod1.Add(new Part { mesh = low, mats = mats, local = local });
                Bounds wb = TransformBounds(baked.bounds, local);
                if (!any) { result.bounds = wb; any = true; } else result.bounds.Encapsulate(wb);
            }
            result.ok = any;
        }
        finally
        {
            Object.DestroyImmediate(g);
        }
        return result;
    }

    /// <summary>Đặt tư thế bằng cách xoay từng đoạn xương về hướng mong muốn (trước = +Z, phải = +X, lên = +Y của nhân vật).</summary>
    private static void ApplyPose(Animator a, Transform root, Pose pose)
    {
        Vector3 F = root.forward, U = root.up, R = root.right;
        Vector3 D(float x, float y, float z) => (R * x + U * y + F * z).normalized;
        void Arms(Vector3 upper, Vector3 fore)
        {
            Aim(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, Mirror(upper, R));
            Aim(a, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Mirror(fore, R));
            Aim(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, upper);
            Aim(a, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, fore);
        }
        void Legs(Vector3 thigh, Vector3 shin)
        {
            Aim(a, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, Mirror(thigh, R));
            Aim(a, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, Mirror(shin, R));
            Aim(a, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, thigh);
            Aim(a, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, shin);
        }
        void Lean(float forward)
        {
            HumanBodyBones top = a.GetBoneTransform(HumanBodyBones.Chest) != null ? HumanBodyBones.Chest : HumanBodyBones.Neck;
            Aim(a, HumanBodyBones.Spine, top, D(0f, 1f, forward));
        }
        switch (pose)
        {
            case Pose.Sit:
                Legs(D(0.12f, -0.05f, 1f), D(0.04f, -1f, 0.12f));
                Lean(0.15f);
                Arms(D(0.12f, -0.8f, 0.45f), D(0.04f, -0.25f, 1f));
                break;
            case Pose.Squat:
                Legs(D(0.28f, 0.35f, 1f), D(0.05f, -1f, -0.35f));
                Lean(0.35f);
                Arms(D(0.12f, -0.6f, 0.7f), D(0f, -0.2f, 1f));
                break;
            case Pose.Ride:
                Legs(D(0.18f, -0.15f, 1f), D(0.06f, -1f, 0.38f));
                Lean(0.25f);
                Arms(D(0.22f, -0.45f, 1f), D(0.02f, -0.15f, 1f));
                break;
            case Pose.RideBack:
                Legs(D(0.2f, -0.12f, 1f), D(0.08f, -1f, 0.25f));
                Lean(0.1f);
                Arms(D(0.2f, -0.9f, 0.25f), D(0.05f, -0.5f, 0.8f));
                break;
            default:
                Arms(D(0.16f, -1f, 0.03f), D(0.04f, -1f, 0.14f));
                break;
        }
    }

    /// <summary>IK hai đoạn xương cho tay (vai - khuỷu - cổ tay) và chân (hông - gối - cổ chân), khuỷu chĩa ra ngoài / gối chĩa ra trước.</summary>
    private static void ApplyIK(Animator a, Transform root, IK ik)
    {
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null) return;
        Vector3 F = root.forward, U = root.up, R = root.right, h = hips.position;
        Vector3 W(Vector3 local) => h + R * local.x + U * local.y + F * local.z;
        TwoBone(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, W(ik.handL), -R * 0.7f - U * 0.7f - F * 0.1f);
        TwoBone(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, W(ik.handR), R * 0.7f - U * 0.7f - F * 0.1f);
        TwoBone(a, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, W(ik.footL), F + U * 0.4f - R * 0.25f);
        TwoBone(a, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, W(ik.footR), F + U * 0.4f + R * 0.25f);
    }

    private static void TwoBone(Animator a, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Vector3 target, Vector3 pole)
    {
        Transform t0 = a.GetBoneTransform(upper), t1 = a.GetBoneTransform(lower), t2 = a.GetBoneTransform(end);
        if (t0 == null || t1 == null || t2 == null) return;
        float la = Vector3.Distance(t0.position, t1.position), lb = Vector3.Distance(t1.position, t2.position);
        Vector3 toT = target - t0.position;
        float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
        Vector3 dir = toT.normalized;
        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
        Vector3 n = pole - dir * Vector3.Dot(pole, dir);
        if (n.sqrMagnitude < 1e-6f) n = Vector3.Cross(dir, Vector3.right);
        n.Normalize();
        Vector3 elbow = t0.position + dir * (la * cosA) + n * (la * sinA);
        t0.rotation = Quaternion.FromToRotation(t1.position - t0.position, elbow - t0.position) * t0.rotation;
        Vector3 hand = t0.position + dir * d;
        t1.rotation = Quaternion.FromToRotation(t2.position - t1.position, hand - t1.position) * t1.rotation;
    }

    /// <summary>Hướng cho bên trái = đối xứng hướng bên phải qua mặt phẳng giữa người.</summary>
    private static Vector3 Mirror(Vector3 v, Vector3 right) => v - 2f * Vector3.Dot(v, right) * right;

    private static void Aim(Animator a, HumanBodyBones bone, HumanBodyBones child, Vector3 dir)
    {
        Transform t = a.GetBoneTransform(bone), c = a.GetBoneTransform(child);
        if (t == null || c == null) return;
        Vector3 cur = c.position - t.position;
        if (cur.sqrMagnitude < 1e-8f) return;
        t.rotation = Quaternion.FromToRotation(cur, dir) * t.rotation;
    }

    private static Bounds TransformBounds(Bounds b, Matrix4x4 m)
    {
        Bounds r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            r.Encapsulate(m.MultiplyPoint3x4(c));
        }
        return r;
    }

    // ================= Rút gọn mesh (gộp đỉnh theo lưới) =================
    /// <summary>Giữ submesh + UV (dùng lại vật liệu gốc); các đỉnh trong cùng ô lưới gộp làm một.</summary>
    public static Mesh Decimate(Mesh src, float cell)
    {
        Vector3[] v = src.vertices;
        Vector3[] n = src.normals;
        Vector2[] uv = src.uv;
        bool hasN = n != null && n.Length == v.Length, hasUV = uv != null && uv.Length == v.Length;
        var map = new int[v.Length];
        var key = new Dictionary<Vector3Int, int>();
        var nv = new List<Vector3>();
        var nn = new List<Vector3>();
        var nuv = new List<Vector2>();
        var count = new List<int>();
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.FloorToInt(v[i].x / cell), Mathf.FloorToInt(v[i].y / cell), Mathf.FloorToInt(v[i].z / cell));
            if (!key.TryGetValue(k, out int id))
            {
                id = nv.Count;
                key[k] = id;
                nv.Add(Vector3.zero);
                nn.Add(Vector3.zero);
                nuv.Add(hasUV ? uv[i] : Vector2.zero);
                count.Add(0);
            }
            nv[id] += v[i];
            if (hasN) nn[id] += n[i];
            count[id]++;
            map[i] = id;
        }
        for (int i = 0; i < nv.Count; i++)
        {
            nv[i] /= count[i];
            nn[i] = nn[i].sqrMagnitude > 1e-8f ? nn[i].normalized : Vector3.up;
        }
        var m = new Mesh { indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, hideFlags = HideFlags.DontSave };
        m.SetVertices(nv);
        m.SetNormals(nn);
        m.SetUVs(0, nuv);
        m.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] t = src.GetTriangles(s);
            var outT = new List<int>(t.Length / 2);
            var seen = new HashSet<long>();
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int a = map[t[i]], b = map[t[i + 1]], c = map[t[i + 2]];
                if (a == b || b == c || a == c) continue;
                long h = TriKey(a, b, c);
                if (!seen.Add(h)) continue;
                outT.Add(a); outT.Add(b); outT.Add(c);
            }
            m.SetTriangles(outT, s);
        }
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Rút gọn + tô màu theo submesh (mỗi submesh một màu bảng) -> 1 submesh, mặt phẳng (flat shading).</summary>
    public static Mesh DecimateColored(Mesh src, Color[] subColors, float cell)
    {
        Vector3[] v = src.vertices;
        var map = new int[v.Length];
        var key = new Dictionary<Vector3Int, int>();
        var pos = new List<Vector3>();
        var cnt = new List<int>();
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.FloorToInt(v[i].x / cell), Mathf.FloorToInt(v[i].y / cell), Mathf.FloorToInt(v[i].z / cell));
            if (!key.TryGetValue(k, out int id)) { id = pos.Count; key[k] = id; pos.Add(Vector3.zero); cnt.Add(0); }
            pos[id] += v[i];
            cnt[id]++;
            map[i] = id;
        }
        for (int i = 0; i < pos.Count; i++) pos[i] /= cnt[i];
        var ov = new List<Vector3>();
        var on = new List<Vector3>();
        var ouv = new List<Vector2>();
        var ot = new List<int>();
        var seen = new HashSet<long>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            Vector2 u = VPalette.UV(s < subColors.Length ? subColors[s] : Color.gray);
            int[] t = src.GetTriangles(s);
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int a = map[t[i]], b = map[t[i + 1]], c = map[t[i + 2]];
                if (a == b || b == c || a == c) continue;
                if (!seen.Add(TriKey(a, b, c))) continue;
                Vector3 pa = pos[a], pb = pos[b], pc = pos[c];
                Vector3 nrm = Vector3.Cross(pb - pa, pc - pa);
                if (nrm.sqrMagnitude < 1e-12f) continue;
                nrm.Normalize();
                int baseIndex = ov.Count;
                ov.Add(pa); ov.Add(pb); ov.Add(pc);
                on.Add(nrm); on.Add(nrm); on.Add(nrm);
                ouv.Add(u); ouv.Add(u); ouv.Add(u);
                ot.Add(baseIndex); ot.Add(baseIndex + 1); ot.Add(baseIndex + 2);
            }
        }
        var m = new Mesh { indexFormat = ov.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, hideFlags = HideFlags.DontSave };
        m.SetVertices(ov);
        m.SetNormals(on);
        m.SetUVs(0, ouv);
        m.SetTriangles(ot, 0);
        m.RecalculateBounds();
        return m;
    }

    private static long TriKey(int a, int b, int c)
    {
        // Không phụ thuộc thứ tự quay vòng: xoay để chỉ số nhỏ nhất đứng đầu
        if (b < a && b < c) { int t = a; a = b; b = c; c = t; }
        else if (c < a && c < b) { int t = a; a = c; c = b; b = t; }
        return ((long)a * 73856093L) ^ ((long)b * 19349663L << 1) ^ ((long)c * 83492791L << 2);
    }

    // ================= Tạo bản sao trong thành phố =================
    /// <summary>
    /// Đặt model vào scene: object gốc + các mảnh LOD0 / LOD1, LODGroup (LOD0 khi chiếm > lod0Height chiều cao màn hình,
    /// ẩn khi nhỏ hơn cullHeight). scale: co giãn cả model (trẻ em, xe nhỏ...).
    /// </summary>
    public static GameObject Spawn(Model m, Transform parent, Vector3 pos, Quaternion rot, string name, bool shadows, float lod0Height = 0.18f, float cullHeight = 0.018f, float scale = 1f)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, rot);
        root.transform.localScale = Vector3.one * scale;
        if (m == null || !m.ok) return root;
        var r0 = new List<Renderer>();
        var r1 = new List<Renderer>();
        foreach (Part p in m.lod0) r0.Add(MakePart(root.transform, p, shadows, "LOD0"));
        foreach (Part p in m.lod1) r1.Add(MakePart(root.transform, p, false, "LOD1"));
        var lg = root.AddComponent<LODGroup>();
        if (r1.Count > 0) lg.SetLODs(new[] { new LOD(lod0Height, r0.ToArray()), new LOD(cullHeight, r1.ToArray()) });
        else lg.SetLODs(new[] { new LOD(cullHeight, r0.ToArray()) });
        lg.RecalculateBounds();
        return root;
    }

    /// <summary>Thêm các mảnh của model con (vd. người lái) vào LODGroup có sẵn của root.</summary>
    public static void AddTo(GameObject root, Model m, Matrix4x4 offset, bool shadows)
    {
        if (root == null || m == null || !m.ok) return;
        var lg = root.GetComponent<LODGroup>();
        if (lg == null) return;
        LOD[] lods = lg.GetLODs();
        var holder = new GameObject("Rider").transform;
        holder.SetParent(root.transform, false);
        holder.localPosition = offset.GetColumn(3);
        holder.localRotation = offset.rotation;
        var r0 = new List<Renderer>(lods.Length > 0 ? lods[0].renderers : new Renderer[0]);
        var r1 = new List<Renderer>(lods.Length > 1 ? lods[1].renderers : new Renderer[0]);
        foreach (Part p in m.lod0) r0.Add(MakePart(holder, p, shadows, "LOD0"));
        foreach (Part p in m.lod1) r1.Add(MakePart(holder, p, false, "LOD1"));
        if (lods.Length > 0) lods[0].renderers = r0.ToArray();
        if (lods.Length > 1) lods[1].renderers = r1.ToArray();
        lg.SetLODs(lods);
        lg.RecalculateBounds();
    }

    private static Renderer MakePart(Transform parent, Part p, bool shadows, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = p.local.GetColumn(3);
        go.transform.localRotation = p.local.rotation;
        go.transform.localScale = p.local.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = p.mats;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return mr;
    }
}
