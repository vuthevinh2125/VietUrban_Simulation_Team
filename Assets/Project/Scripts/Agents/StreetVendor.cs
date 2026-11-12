using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hàng rong biết đi: gắn đồ lên người đi bộ (NPC thật của PedestrianSystem).
///  - Gánh hàng: nón lá trên đầu, đòn gánh tre trên vai phải (dọc theo hướng đi), hai thúng hoa quả trước / sau, dây quang.
///  - Xe đạp bán hoa: người dắt xe đạp chở đầy cúc hoạ mi / hồng / cúc vàng bên phải, đội nón lá.
/// Đồ gắn vào xương (đầu, ngực) nên nhún theo bước đi. Mesh dựng một lần, dùng chung vật liệu bảng màu.
/// </summary>
public static class StreetVendor
{
    private static Mesh nonLa;
    private static Mesh[] ganh, bikes;

    private static readonly Color NonLaCol = new Color(0.92f, 0.86f, 0.62f), Bamboo = new Color(0.78f, 0.66f, 0.42f),
        Wood = new Color(0.45f, 0.28f, 0.16f), Rail = new Color(0.14f, 0.14f, 0.15f), Steel = new Color(0.8f, 0.82f, 0.85f),
        Dark = new Color(0.1f, 0.09f, 0.08f), Leaf = new Color(0.25f, 0.5f, 0.2f);
    private static readonly Color[] Fruits =
    {
        new Color(0.95f, 0.85f, 0.15f), new Color(0.9f, 0.25f, 0.15f), new Color(0.35f, 0.7f, 0.2f), new Color(0.98f, 0.6f, 0.1f), new Color(0.6f, 0.15f, 0.35f),
    };
    private static readonly Color[] Flowers =
    {
        new Color(0.98f, 0.98f, 0.95f), new Color(0.98f, 0.85f, 0.2f), new Color(0.95f, 0.45f, 0.6f), new Color(0.85f, 0.12f, 0.15f), new Color(0.65f, 0.4f, 0.85f),
    };

    /// <summary>Gắn đồ hàng rong cho một người đi bộ; bike = dắt xe đạp hoa, ngược lại là gánh hàng.</summary>
    public static void Dress(GameObject walker, bool bike, int seed)
    {
        Animator a = walker.GetComponentInChildren<Animator>();
        Transform root = walker.transform;
        var rnd = new System.Random(seed);
        if (a != null && a.isHuman)
        {
            Transform head = a.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) Attach(NonLa(), head, head.position + root.up * 0.16f, root.rotation, "Non la");
        }
        if (bike)
        {
            if (bikes == null) bikes = new Mesh[3];
            int v = rnd.Next(bikes.Length);
            if (bikes[v] == null) bikes[v] = FlowerBike(new System.Random(300 + v));
            Attach(bikes[v], root, root.position + root.right * 0.55f + root.forward * 0.25f, root.rotation, "Xe dap hoa");
            return;
        }
        if (ganh == null) ganh = new Mesh[4];
        int g = rnd.Next(ganh.Length);
        if (ganh[g] == null) ganh[g] = Ganh(new System.Random(500 + g));
        Transform chest = null, shoulder = null;
        if (a != null && a.isHuman)
        {
            chest = a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest) ?? a.GetBoneTransform(HumanBodyBones.Spine);
            shoulder = a.GetBoneTransform(HumanBodyBones.RightUpperArm) ?? a.GetBoneTransform(HumanBodyBones.RightShoulder);
        }
        Vector3 at = shoulder != null ? shoulder.position + root.up * 0.09f - root.right * 0.04f : root.position + root.up * 1.42f + root.right * 0.18f;
        float shoulderHeight = Vector3.Dot(at - root.position, root.up);
        Transform host = chest != null ? chest : root;
        GameObject o = Attach(ganh[g], host, at, root.rotation, "Ganh hang");
        // Mesh gánh làm cho vai cao 1.42 m: người thấp / cao thì co giãn theo chiều đứng để thúng vẫn cách đất ~0.3 m
        o.transform.localScale = new Vector3(o.transform.localScale.x, o.transform.localScale.y * Mathf.Clamp(shoulderHeight / 1.42f, 0.8f, 1.2f), o.transform.localScale.z);
    }

    private static GameObject Attach(Mesh m, Transform parent, Vector3 pos, Quaternion rot, string name)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.SetParent(parent, true);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = VPalette.Material;
        mr.shadowCastingMode = ShadowCastingMode.On;
        return go;
    }

    // ================= Mesh =================
    private static Mesh NonLa()
    {
        if (nonLa != null) return nonLa;
        var mb = new VMeshBuilder();
        mb.Add(VShape.Cone, Matrix4x4.TRS(Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.6f, 0.22f, 0.6f)), NonLaCol);
        mb.Add(VShape.Torus(0.12f, 20), Matrix4x4.TRS(Vector3.down * 0.05f, Quaternion.identity, new Vector3(0.6f, 0.06f, 0.6f)), Color.Lerp(NonLaCol, Wood, 0.3f));
        nonLa = Done(mb, "NonLa");
        return nonLa;
    }

    /// <summary>Gốc ở điểm đặt trên vai, đòn gánh dọc trục Z (trước - sau), vai cao 1.42 m.</summary>
    private static Mesh Ganh(System.Random r)
    {
        var mb = new VMeshBuilder();
        void Pipe(Vector3 a, Vector3 b, float d, Color c)
        {
            Vector3 dir = b - a;
            mb.Add(VShape.Cylinder(8), Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(d, dir.magnitude, d)), c);
        }
        Vector3 f = new Vector3(0f, -0.05f, 0.95f), m = Vector3.zero, b = new Vector3(0f, -0.05f, -0.95f);
        Pipe(b, m, 0.045f, Bamboo);
        Pipe(m, f, 0.045f, Bamboo);
        Color fruit = Fruits[r.Next(Fruits.Length)];
        foreach (float z in new[] { 0.85f, -0.85f })
        {
            Vector3 top = new Vector3(0f, -0.86f, z);
            mb.Add(VShape.Dome(14), Matrix4x4.TRS(top, Quaternion.Euler(180f, 0f, 0f), new Vector3(0.56f, 0.48f, 0.56f)), Bamboo);
            mb.Add(VShape.Torus(0.12f, 18), Matrix4x4.TRS(top, Quaternion.identity, new Vector3(0.58f, 0.1f, 0.58f)), Color.Lerp(Bamboo, Wood, 0.4f));
            for (int k = 0; k < 10; k++)
            {
                Vector3 p = top + new Vector3((float)r.NextDouble() * 0.34f - 0.17f, (float)r.NextDouble() * 0.06f - 0.05f, (float)r.NextDouble() * 0.34f - 0.17f);
                mb.Add(VShape.Sphere(8), Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * (0.09f + (float)r.NextDouble() * 0.04f)), r.NextDouble() < 0.75 ? fruit : Fruits[r.Next(Fruits.Length)]);
            }
            Vector3 hook = new Vector3(0f, -0.05f, z * 1.08f);
            for (int k = 0; k < 4; k++)
            {
                float ang = (45f + k * 90f) * Mathf.Deg2Rad;
                Pipe(top + new Vector3(Mathf.Cos(ang) * 0.27f, 0f, Mathf.Sin(ang) * 0.27f), hook, 0.008f, Wood);
            }
        }
        return Done(mb, "Ganh");
    }

    /// <summary>Xe đạp chở hoa, gốc ở mặt đất, đầu xe +Z.</summary>
    private static Mesh FlowerBike(System.Random r)
    {
        var mb = new VMeshBuilder();
        void Pipe(Vector3 a, Vector3 b, float d, Color c)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) return;
            mb.Add(VShape.Cylinder(8), Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(d, dir.magnitude, d)), c);
        }
        Color frame = r.NextDouble() < 0.5 ? new Color(0.15f, 0.35f, 0.28f) : new Color(0.12f, 0.12f, 0.14f);
        Quaternion side = Quaternion.Euler(0f, 0f, 90f);
        foreach (float wz in new[] { 0.55f, -0.52f })
        {
            Vector3 hub = new Vector3(0f, 0.34f, wz);
            mb.Add(VShape.Torus(0.06f, 24), Matrix4x4.TRS(hub, side, new Vector3(0.68f, 0.18f, 0.68f)), Rail);
            mb.Add(VShape.Torus(0.03f, 24), Matrix4x4.TRS(hub, side, new Vector3(0.62f, 0.1f, 0.62f)), Steel);
            for (int k = 0; k < 12; k++) Pipe(hub, hub + Quaternion.Euler(k * 30f, 0f, 0f) * Vector3.forward * 0.3f, 0.005f, Steel);
        }
        Vector3 bb = new Vector3(0f, 0.32f, 0f), seat = new Vector3(0f, 0.86f, -0.18f), head = new Vector3(0f, 0.9f, 0.42f);
        Pipe(bb, seat, 0.035f, frame);
        Pipe(bb, head, 0.04f, frame);
        Pipe(seat + Vector3.down * 0.05f, head + Vector3.down * 0.06f, 0.032f, frame);
        Pipe(bb, new Vector3(0f, 0.34f, -0.52f), 0.025f, frame);
        Pipe(seat, new Vector3(0f, 0.34f, -0.52f), 0.022f, frame);
        Pipe(head, new Vector3(0f, 0.34f, 0.55f), 0.03f, frame);
        mb.Add(VShape.RoundBox(0.4f, 8), Matrix4x4.TRS(seat + Vector3.up * 0.04f, Quaternion.identity, new Vector3(0.14f, 0.06f, 0.24f)), Dark);
        Pipe(head + new Vector3(-0.28f, 0.08f, -0.05f), head + new Vector3(0.28f, 0.08f, -0.05f), 0.022f, Steel);
        Pipe(head, head + Vector3.up * 0.08f, 0.025f, Steel);
        mb.Add(VShape.Dome(12), Matrix4x4.TRS(new Vector3(0f, 1.02f, 0.6f), Quaternion.Euler(180f, 0f, 0f), new Vector3(0.36f, 0.3f, 0.3f)), Bamboo);
        mb.Add(VShape.RoundBox(0.3f, 8), Matrix4x4.TRS(new Vector3(0f, 0.78f, -0.6f), Quaternion.identity, new Vector3(0.6f, 0.06f, 0.42f)), Rail);
        mb.Add(VShape.Dome(14), Matrix4x4.TRS(new Vector3(0f, 1.05f, -0.6f), Quaternion.Euler(180f, 0f, 0f), new Vector3(0.8f, 0.55f, 0.6f)), Bamboo);
        Color main = Flowers[r.Next(Flowers.Length)];
        for (int k = 0; k < 40; k++)
        {
            Vector3 fl = new Vector3((float)r.NextDouble() * 0.72f - 0.36f, 1.08f + (float)r.NextDouble() * 0.42f, -0.6f + (float)r.NextDouble() * 0.52f - 0.26f);
            Pipe(fl + Vector3.down * 0.25f, fl, 0.008f, Leaf);
            mb.Add(VShape.Sphere(8), Matrix4x4.TRS(fl, Quaternion.identity, Vector3.one * (0.06f + (float)r.NextDouble() * 0.04f)), r.NextDouble() < 0.7 ? main : Flowers[r.Next(Flowers.Length)]);
        }
        for (int k = 0; k < 8; k++)
            mb.Add(VShape.Sphere(8), Matrix4x4.TRS(new Vector3((float)r.NextDouble() * 0.2f - 0.1f, 1.1f + (float)r.NextDouble() * 0.12f, 0.6f + (float)r.NextDouble() * 0.16f - 0.08f), Quaternion.identity, Vector3.one * 0.08f), Flowers[r.Next(Flowers.Length)]);
        return Done(mb, "XeDapHoa");
    }

    private static Mesh Done(VMeshBuilder mb, string name)
    {
        Mesh m = mb.ToMesh(RealModels.MeshPrefix + "_" + name);
        m.hideFlags = HideFlags.DontSave;
        return m;
    }
}
