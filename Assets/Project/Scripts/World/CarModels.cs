using UnityEngine;

/// <summary>
/// Ô tô điện phố Việt dựng bằng code (thiết kế riêng của nhóm, logo tự chế "VU" - không dùng xe của người chơi):
///  - Mini: crossover điện cỡ nhỏ (~4 m), Compact: crossover cỡ C (~4,3 m), Taxi: bản Compact sơn xanh ngọc + hộp đèn TAXI,
///    SUV: SUV 3 hàng ghế (~4,9 m) có giá nóc.
/// Thân bo tròn, khoang kính vát (2 khối nghiêng), trụ A / B / C, hốc bánh + ốp vòm nhựa đen, mâm đúc 5 chấu, đèn pha + dải LED ngang,
/// đèn hậu chạy ngang, gương, tay nắm cửa, khe cửa, cánh gió, vây cá mập, biển số trắng.
/// Chung 1 vật liệu bảng màu (1 lệnh vẽ / xe), có bản LOD1. Toạ độ: mặt đất y = 0, đầu xe hướng +Z, tâm xe ở z = 0.
/// </summary>
public static class CarModels
{
    public enum Kind { Mini, Compact, Taxi, SUV }
    public const int Variants = 12;

    public class Info
    {
        public Mesh lod0, lod1;
        public Kind kind;
        /// <summary>Kích thước bao (rộng, cao, dài) để đặt collider.</summary>
        public Vector3 size;
    }

    private static Info[] cache;

    private static readonly Kind[] Kinds =
    {
        Kind.Mini, Kind.Compact, Kind.Taxi, Kind.SUV, Kind.Mini, Kind.Taxi,
        Kind.Compact, Kind.SUV, Kind.Mini, Kind.Taxi, Kind.Compact, Kind.Mini,
    };

    private static readonly Color[] Paints =
    {
        new Color(0.07f, 0.07f, 0.08f), new Color(0.66f, 0.68f, 0.7f), new Color(0.93f, 0.93f, 0.92f), new Color(0.72f, 0.06f, 0.07f),
        new Color(0.88f, 0.4f, 0.1f), new Color(0.1f, 0.25f, 0.62f), new Color(0.36f, 0.37f, 0.4f), new Color(0.55f, 0.8f, 0.72f),
        new Color(0.93f, 0.93f, 0.92f), new Color(0.07f, 0.07f, 0.08f), new Color(0.66f, 0.68f, 0.7f),
    };

    private static readonly Color Black = new Color(0.06f, 0.06f, 0.07f), Rubber = new Color(0.09f, 0.09f, 0.09f),
        Chrome = new Color(0.82f, 0.84f, 0.86f), Alloy = new Color(0.62f, 0.64f, 0.66f), Glass = new Color(0.1f, 0.13f, 0.16f),
        Lamp = new Color(1f, 0.97f, 0.88f), Red = new Color(0.8f, 0.06f, 0.05f), Amber = new Color(1f, 0.6f, 0.1f),
        Plate = new Color(0.95f, 0.95f, 0.93f), Clad = new Color(0.1f, 0.1f, 0.11f), Well = new Color(0.03f, 0.03f, 0.03f),
        Teal = new Color(0f, 0.62f, 0.6f), White = new Color(0.94f, 0.94f, 0.93f);

    public static Info Get(int v)
    {
        if (cache == null) cache = new Info[Variants];
        v = ((v % Variants) + Variants) % Variants;
        if (cache[v] == null || cache[v].lod0 == null) cache[v] = Build(v);
        return cache[v];
    }

    private static Info Build(int v)
    {
        Kind kind = Kinds[v];
        Color paint = kind == Kind.Taxi ? Teal : Paints[(v * 5 + 3) % Paints.Length];
        // Một số xe nóc đen tương phản (trừ xe đen / taxi)
        Color roof = kind != Kind.Taxi && v % 3 == 1 && paint.r + paint.g + paint.b > 0.6f ? Black : paint;
        var info = new Info { kind = kind };
        info.lod0 = Make(kind, paint, roof, false, $"{RealModels.MeshPrefix}_Car{v}", out info.size);
        info.lod1 = Make(kind, paint, roof, true, $"{RealModels.MeshPrefix}_Car{v}_LOD1", out _);
        return info;
    }

    private static Mesh Make(Kind kind, Color paint, Color roofc, bool low, string name, out Vector3 size)
    {
        var mb = new VMeshBuilder();
        int res = low ? 8 : 16;
        VShape R = VShape.RoundBox(0.22f, res), Rs = VShape.RoundBox(0.4f, res), Rr = VShape.RoundBox(0.3f, res), Rg = VShape.RoundBox(0.16f, res),
               cyl = VShape.Cylinder(low ? 8 : 16), tor = VShape.Torus(0.34f, low ? 12 : 24), ring = VShape.Torus(0.3f, low ? 8 : 16), sph = VShape.Sphere(8);

        void S(VShape s, Vector3 p, Vector3 e, Vector3 sz, Color c) => mb.Add(s, Matrix4x4.TRS(p, Quaternion.Euler(e), sz), c);
        void Plank(VShape s, Vector3 a, Vector3 b, float w, float th, Color c, Vector3 up)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            mb.Add(s, Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d, up), new Vector3(w, th, d.magnitude)), c);
        }
        void Tube(Vector3 a, Vector3 b, float dd, Color c)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            mb.Add(cyl, Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d), new Vector3(dd, d.magnitude, dd)), c);
        }
        // Khối nghiêng: mặt trên dịch theo trục z một đoạn k * chiều cao (kính lái / kính hậu vát)
        void Shear(VShape s, Vector3 center, Vector3 sz, float k, Color c)
        {
            Matrix4x4 sh = Matrix4x4.identity;
            sh.m21 = k;
            mb.Add(s, Matrix4x4.Translate(center) * sh * Matrix4x4.Scale(sz), c);
        }

        float L, W, H, d, wb, cl;
        switch (kind)
        {
            case Kind.Mini: L = 3.97f; W = 1.72f; H = 1.58f; d = 0.64f; wb = 2.51f; cl = 0.19f; break;
            case Kind.SUV: L = 4.9f; W = 1.93f; H = 1.76f; d = 0.76f; wb = 2.95f; cl = 0.22f; break;
            default: L = 4.3f; W = 1.79f; H = 1.6f; d = 0.68f; wb = 2.61f; cl = 0.2f; break;
        }
        bool suv = kind == Kind.SUV;
        size = new Vector3(W, H, L);
        float belt = H * 0.6f, bodyH = belt - cl;
        float fz = wb * 0.5f + 0.04f, rz = -wb * 0.5f + 0.04f;
        float hood = L * (suv ? 0.22f : 0.25f);
        float zA = L * 0.5f - hood;                         // chân kính lái
        float zR = -L * 0.5f + (suv ? 0.14f : 0.16f);       // chân kính hậu
        float gh = H - belt - 0.06f;
        float ws = gh * 1.45f, rr = gh * (suv ? 0.3f : 0.45f);
        float zMid = (zA + zR) * 0.5f;
        float rf = zA - ws, rbk = zR + rr;                  // mép trước / sau của nóc

        // ===== Thân =====
        S(R, new Vector3(0f, cl + bodyH * 0.5f, 0f), Vector3.zero, new Vector3(W, bodyH, L), paint);
        S(Rr, new Vector3(0f, belt - 0.03f, L * 0.5f - hood * 0.5f - 0.02f), new Vector3(5f, 0f, 0f), new Vector3(W * 0.93f, 0.12f, hood), paint); // capo dốc
        // Khoang kính: nửa trước vát theo kính lái, nửa sau vát theo kính hậu
        Shear(Rg, new Vector3(0f, belt + gh * 0.5f, (zMid + zA) * 0.5f - ws * 0.5f), new Vector3(W * 0.86f, gh, zA - zMid), -ws / gh, Glass);
        Shear(Rg, new Vector3(0f, belt + gh * 0.5f, (zMid + zR) * 0.5f + rr * 0.5f), new Vector3(W * 0.86f, gh, zMid - zR), rr / gh, Glass);
        S(Rr, new Vector3(0f, H - 0.04f, (rf + rbk) * 0.5f), Vector3.zero, new Vector3(W * 0.84f, 0.08f, rf - rbk + 0.12f), roofc); // nóc
        foreach (float sx in new[] { -1f, 1f })
        {
            Plank(R, new Vector3(sx * W * 0.415f, belt, zA - 0.03f), new Vector3(sx * W * 0.405f, H - 0.06f, rf + 0.03f), 0.08f, 0.06f, roofc, Vector3.up); // trụ A
            Plank(R, new Vector3(sx * W * 0.43f, belt + 0.02f, zR + 0.15f), new Vector3(sx * W * 0.41f, H - 0.07f, rbk + 0.2f), 0.05f, 0.36f, roofc, new Vector3(sx, 0f, 0f)); // trụ C
            S(R, new Vector3(sx * W * 0.432f, belt + gh * 0.5f, zMid - 0.15f), Vector3.zero, new Vector3(0.02f, gh * 0.95f, 0.09f), Black); // trụ B
        }
        S(R, new Vector3(0f, belt + 0.01f, (zA + zR) * 0.5f), Vector3.zero, new Vector3(W * 0.875f, 0.035f, zA - zR - 0.05f), Black); // gioăng chân kính

        // ===== Bánh, hốc bánh, ốp vòm =====
        foreach (float az in new[] { fz, rz })
            foreach (float sx in new[] { -1f, 1f })
            {
                S(cyl, new Vector3(sx * (W * 0.5f + 0.002f), d * 0.5f, az), new Vector3(0f, 0f, 90f), new Vector3(d * 1.12f, 0.012f, d * 1.12f), Well);
                int n = low ? 4 : 7;
                float rad = d * 0.6f;
                for (int k = 0; k < n; k++)
                {
                    float a0 = (-12f + 204f * k / n) * Mathf.Deg2Rad, a1 = (-12f + 204f * (k + 1) / n) * Mathf.Deg2Rad, am = (a0 + a1) * 0.5f;
                    Vector3 p0 = new Vector3(sx * (W * 0.5f + 0.01f), d * 0.5f + rad * Mathf.Sin(a0), az + rad * Mathf.Cos(a0));
                    Vector3 p1 = new Vector3(sx * (W * 0.5f + 0.01f), d * 0.5f + rad * Mathf.Sin(a1), az + rad * Mathf.Cos(a1));
                    Plank(R, p0, p1, 0.05f, 0.1f, Clad, new Vector3(0f, Mathf.Sin(am), Mathf.Cos(am)));
                }
                Vector3 axle = new Vector3(sx * (W * 0.5f - 0.13f), d * 0.5f, az);
                S(tor, axle, new Vector3(0f, 0f, 90f), new Vector3(d, 0.22f / 0.34f, d), Rubber);
                S(cyl, axle, new Vector3(0f, 0f, 90f), new Vector3(d * 0.7f, 0.2f, d * 0.7f), Black);
                S(cyl, axle + Vector3.right * (sx * 0.09f), new Vector3(0f, 0f, 90f), new Vector3(d * 0.64f, 0.03f, d * 0.64f), Alloy);
                if (!low)
                    for (int k = 0; k < 5; k++)
                    {
                        float a = (k * 72f + 36f) * Mathf.Deg2Rad;
                        S(R, new Vector3(axle.x + sx * 0.105f, d * 0.5f + d * 0.2f * Mathf.Sin(a), az + d * 0.2f * Mathf.Cos(a)),
                          new Vector3(-a * Mathf.Rad2Deg, 0f, 0f), new Vector3(0.02f, d * 0.08f, d * 0.2f), Black);
                    }
                S(cyl, axle + Vector3.right * (sx * 0.11f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, 0.02f, 0.1f), Chrome);
            }

        // ===== Sườn: ốp dưới, khe cửa, tay nắm, gương =====
        foreach (float sx in new[] { -1f, 1f })
        {
            S(R, new Vector3(sx * (W * 0.5f + 0.005f), cl + 0.07f, (fz + rz) * 0.5f), Vector3.zero, new Vector3(0.04f, 0.14f, wb - d * 1.2f), Clad);
            if (!low)
            {
                foreach (float z in new[] { zA + 0.05f, zMid - 0.15f, rz + d * 0.62f })
                    S(VShape.Cube, new Vector3(sx * (W * 0.5f + 0.004f), cl + bodyH * 0.55f, z), Vector3.zero, new Vector3(0.004f, bodyH * 0.75f, 0.012f), Black);
                foreach (float z in new[] { zMid - 0.27f, rz + d * 0.5f })
                    S(R, new Vector3(sx * (W * 0.5f + 0.012f), belt - 0.11f, z), Vector3.zero, new Vector3(0.025f, 0.03f, 0.14f), Chrome);
            }
            Tube(new Vector3(sx * W * 0.44f, belt + 0.03f, zA - 0.12f), new Vector3(sx * (W * 0.5f + 0.06f), belt + 0.08f, zA - 0.14f), 0.04f, Black);
            S(Rs, new Vector3(sx * (W * 0.5f + 0.1f), belt + 0.1f, zA - 0.15f), new Vector3(0f, sx * 10f, 0f), new Vector3(0.2f, 0.12f, 0.09f), roofc);
            S(sph, new Vector3(sx * (W * 0.5f + 0.12f), belt + 0.08f, zA - 0.11f), Vector3.zero, new Vector3(0.1f, 0.02f, 0.02f), Amber);
            if (kind == Kind.Taxi) // dải trắng chạy dọc thân taxi
                S(R, new Vector3(sx * (W * 0.5f + 0.003f), cl + bodyH * 0.42f, (zA + rz) * 0.5f), Vector3.zero, new Vector3(0.01f, 0.07f, zA - rz + 0.2f), White);
        }
        S(R, new Vector3(0f, cl + 0.1f, L * 0.5f - 0.05f), Vector3.zero, new Vector3(W * 0.98f, 0.2f, 0.14f), Clad);   // cản trước
        S(R, new Vector3(0f, cl + 0.1f, -L * 0.5f + 0.05f), Vector3.zero, new Vector3(W * 0.98f, 0.2f, 0.14f), Clad);  // cản sau

        // ===== Mặt trước: đèn pha, dải LED ngang, logo "VU" tròn, hốc gió, biển số =====
        float yl = belt - 0.1f;
        foreach (float sx in new[] { -1f, 1f })
        {
            S(Rs, new Vector3(sx * W * 0.34f, yl, L * 0.5f - 0.05f), new Vector3(0f, sx * 14f, 0f), new Vector3(W * 0.22f, 0.075f, 0.1f), Lamp);
            S(Rs, new Vector3(sx * W * 0.34f, yl - 0.06f, L * 0.5f - 0.06f), new Vector3(0f, sx * 14f, 0f), new Vector3(W * 0.2f, 0.025f, 0.08f), Black);
            S(sph, new Vector3(sx * W * 0.36f, cl + 0.2f, L * 0.5f + 0.01f), Vector3.zero, new Vector3(0.12f, 0.04f, 0.03f), Lamp);
        }
        S(R, new Vector3(0f, yl + 0.025f, L * 0.5f + 0.002f), Vector3.zero, new Vector3(W * 0.5f, 0.018f, 0.02f), Lamp);
        S(ring, new Vector3(0f, yl - 0.02f, L * 0.5f + 0.01f), new Vector3(90f, 0f, 0f), new Vector3(0.13f, 0.02f, 0.13f), Chrome);
        S(cyl, new Vector3(0f, yl - 0.02f, L * 0.5f + 0.008f), new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.015f, 0.09f), Teal);
        S(Rs, new Vector3(0f, cl + 0.27f, L * 0.5f + 0.005f), Vector3.zero, new Vector3(W * 0.58f, 0.14f, 0.05f), Black);
        S(VShape.Cube, new Vector3(0f, cl + 0.3f, L * 0.5f + 0.035f), Vector3.zero, new Vector3(0.52f, 0.11f, 0.01f), Plate);

        // ===== Đuôi: đèn hậu ngang, biển số, logo, cánh gió, vây cá mập =====
        S(R, new Vector3(0f, belt - 0.06f, -L * 0.5f + 0.005f), Vector3.zero, new Vector3(W * 0.9f, 0.055f, 0.04f), Red);
        foreach (float sx in new[] { -1f, 1f })
        {
            S(Rs, new Vector3(sx * W * 0.4f, belt - 0.1f, -L * 0.5f + 0.03f), new Vector3(0f, -sx * 12f, 0f), new Vector3(W * 0.16f, 0.1f, 0.08f), Red);
            S(sph, new Vector3(sx * W * 0.38f, cl + 0.15f, -L * 0.5f), Vector3.zero, new Vector3(0.1f, 0.03f, 0.03f), Red);
        }
        S(VShape.Cube, new Vector3(0f, cl + 0.38f, -L * 0.5f - 0.005f), Vector3.zero, new Vector3(0.52f, 0.11f, 0.01f), Plate);
        S(ring, new Vector3(0f, belt - 0.18f, -L * 0.5f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.02f, 0.12f), Chrome);
        S(Rr, new Vector3(0f, H - 0.05f, rbk - 0.05f), new Vector3(-8f, 0f, 0f), new Vector3(W * 0.8f, 0.04f, 0.22f), roofc);
        S(Rs, new Vector3(0f, H + 0.02f, rbk + 0.15f), Vector3.zero, new Vector3(0.06f, 0.07f, 0.18f), Black);
        if (suv)
            foreach (float sx in new[] { -1f, 1f })
                Tube(new Vector3(sx * W * 0.37f, H + 0.02f, rf - 0.1f), new Vector3(sx * W * 0.37f, H + 0.02f, rbk + 0.15f), 0.04f, Black); // giá nóc
        if (kind == Kind.Taxi) // hộp đèn TAXI trên nóc
        {
            S(Rs, new Vector3(0f, H + 0.09f, (rf + rbk) * 0.5f), Vector3.zero, new Vector3(0.55f, 0.14f, 0.22f), White);
            S(R, new Vector3(0f, H + 0.05f, (rf + rbk) * 0.5f), Vector3.zero, new Vector3(0.56f, 0.03f, 0.23f), Teal);
            S(R, new Vector3(0f, H + 0.015f, (rf + rbk) * 0.5f), Vector3.zero, new Vector3(0.3f, 0.03f, 0.12f), Black);
        }

        Mesh m = mb.ToMesh(name);
        m.hideFlags = HideFlags.DontSave;
        return m;
    }
}
