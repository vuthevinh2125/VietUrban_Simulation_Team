using UnityEngine;

/// <summary>
/// Xe máy phố Việt dựng bằng code (không dùng xe của người chơi): xe số kiểu Wave và xe tay ga kiểu Vision.
/// Lốp hình xuyến, vành đúc 5 chấu, đĩa phanh, phuộc, giảm xóc sau, ống xả, ốp chân, đèn pha / xi-nhan / đèn hậu,
/// gương tròn, biển số trắng, yên da, giá chở đồ... toàn khối bo tròn mịn, chung 1 vật liệu bảng màu (1 lệnh vẽ / xe).
/// Mỗi mẫu có bản LOD1 ít đa giác cho tầm xa. Toạ độ: mặt đất y = 0, đầu xe hướng +Z.
/// </summary>
public static class BikeModels
{
    public const int Variants = 12;

    public class Info
    {
        public Mesh lod0, lod1;
        public bool scooter;
        /// <summary>Vị trí khớp hông người lái / người ngồi sau.</summary>
        public Vector3 seat, rearSeat;
        public Vector3 gripL, gripR, footL, footR, pegL, pegR;
    }

    private static Info[] cache;

    private static readonly Color[] Paints =
    {
        new Color(0.75f, 0.08f, 0.08f), new Color(0.08f, 0.08f, 0.09f), new Color(0.92f, 0.92f, 0.9f), new Color(0.1f, 0.22f, 0.55f),
        new Color(0.55f, 0.57f, 0.6f), new Color(0.82f, 0.62f, 0.72f), new Color(0.42f, 0.3f, 0.22f), new Color(0.15f, 0.42f, 0.3f),
        new Color(0.88f, 0.84f, 0.74f), new Color(0.3f, 0.3f, 0.33f),
    };
    private static readonly Color Black = new Color(0.06f, 0.06f, 0.07f), Rubber = new Color(0.09f, 0.09f, 0.09f),
        Chrome = new Color(0.82f, 0.84f, 0.86f), Alloy = new Color(0.62f, 0.64f, 0.66f), Engine = new Color(0.45f, 0.46f, 0.48f),
        Lamp = new Color(1f, 0.97f, 0.85f), Amber = new Color(1f, 0.6f, 0.1f), Red = new Color(0.85f, 0.08f, 0.06f),
        Plate = new Color(0.95f, 0.95f, 0.93f), Seat = new Color(0.1f, 0.09f, 0.09f), SeatBrown = new Color(0.35f, 0.22f, 0.15f);

    public static Info Get(int v)
    {
        if (cache == null) cache = new Info[Variants];
        v = ((v % Variants) + Variants) % Variants;
        if (cache[v] == null || cache[v].lod0 == null) cache[v] = Build(v);
        return cache[v];
    }

    private static Info Build(int v)
    {
        var rnd = new System.Random(7000 + v);
        bool scooter = v % 3 == 2;
        Color paint = Paints[rnd.Next(Paints.Length)];
        Color accent = rnd.NextDouble() < 0.5 ? Black : Color.Lerp(paint, Color.white, 0.6f);
        Color seat = scooter && rnd.NextDouble() < 0.4 ? SeatBrown : Seat;
        var info = new Info { scooter = scooter };
        info.lod0 = Make(scooter, paint, accent, seat, false, $"{RealModels.MeshPrefix}_Bike{v}");
        info.lod1 = Make(scooter, paint, accent, seat, true, $"{RealModels.MeshPrefix}_Bike{v}_LOD1");
        if (scooter)
        {
            info.seat = new Vector3(0f, 0.99f, -0.2f);
            info.rearSeat = new Vector3(0f, 1.0f, -0.6f);
            info.gripL = new Vector3(-0.32f, 1.05f, 0.42f); info.gripR = new Vector3(0.32f, 1.05f, 0.42f);
            info.footL = new Vector3(-0.13f, 0.44f, 0.16f); info.footR = new Vector3(0.13f, 0.44f, 0.16f);
            info.pegL = new Vector3(-0.21f, 0.45f, -0.34f); info.pegR = new Vector3(0.21f, 0.45f, -0.34f);
        }
        else
        {
            info.seat = new Vector3(0f, 0.99f, -0.2f);
            info.rearSeat = new Vector3(0f, 1.0f, -0.6f);
            info.gripL = new Vector3(-0.32f, 1.04f, 0.42f); info.gripR = new Vector3(0.32f, 1.04f, 0.42f);
            info.footL = new Vector3(-0.19f, 0.4f, 0.02f); info.footR = new Vector3(0.19f, 0.4f, 0.02f);
            info.pegL = new Vector3(-0.21f, 0.45f, -0.42f); info.pegR = new Vector3(0.21f, 0.45f, -0.42f);
        }
        return info;
    }

    private static Mesh Make(bool scooter, Color paint, Color accent, Color seatCol, bool low, string name)
    {
        var mb = new VMeshBuilder();
        VShape rb = VShape.RoundBox(0.3f, low ? 8 : 16), rbSoft = VShape.RoundBox(0.5f, low ? 8 : 16), cyl = VShape.Cylinder(low ? 6 : 14),
               tor = VShape.Torus(0.2f, low ? 12 : 28), sph = VShape.Sphere(low ? 8 : 14);
        void S(VShape s, Vector3 p, Vector3 e, Vector3 size, Color c) => mb.Add(s, Matrix4x4.TRS(p, Quaternion.Euler(e), size), c);
        void Tube(Vector3 a, Vector3 b, float d, Color c)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) return;
            mb.Add(cyl, Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(d, dir.magnitude, d)), c);
        }
        void Wheel(Vector3 axle, float d, bool disc)
        {
            Quaternion side = Quaternion.Euler(0f, 0f, 90f);
            mb.Add(tor, Matrix4x4.TRS(axle, side, new Vector3(d, 0.45f, d)), Rubber);
            mb.Add(cyl, Matrix4x4.TRS(axle, side, new Vector3(d * 0.7f, 0.04f, d * 0.7f)), Black);
            mb.Add(cyl, Matrix4x4.TRS(axle, side, new Vector3(d * 0.62f, 0.05f, d * 0.62f)), Alloy);
            mb.Add(cyl, Matrix4x4.TRS(axle, side, new Vector3(0.11f, 0.13f, 0.11f)), Chrome);
            if (!low)
                for (int k = 0; k < 5; k++)
                {
                    Quaternion q = Quaternion.Euler(k * 72f, 0f, 0f);
                    mb.Add(VShape.Cube, Matrix4x4.TRS(axle + q * (Vector3.forward * d * 0.17f), q, new Vector3(0.03f, 0.035f, d * 0.22f)), Black);
                }
            if (disc) mb.Add(cyl, Matrix4x4.TRS(axle + Vector3.right * 0.055f, side, new Vector3(d * 0.42f, 0.008f, d * 0.42f)), Chrome);
        }

        if (!scooter)
        {
            // ===== Xe số (kiểu Wave) =====
            float d = 0.58f;
            Vector3 fa = new Vector3(0f, d * 0.5f, 0.64f), ra = new Vector3(0f, d * 0.5f, -0.6f);
            Wheel(fa, d, true);
            Wheel(ra, d, false);
            Tube(fa + Vector3.right * 0.075f, new Vector3(0.06f, 0.93f, 0.46f), 0.045f, Chrome);
            Tube(fa - Vector3.right * 0.075f, new Vector3(-0.06f, 0.93f, 0.46f), 0.045f, Chrome);
            S(rb, new Vector3(0f, 0.63f, 0.63f), new Vector3(-12f, 0f, 0f), new Vector3(0.13f, 0.06f, 0.44f), paint);       // chắn bùn trước
            S(rb, new Vector3(0f, 0.68f, 0.37f), new Vector3(-20f, 0f, 0f), new Vector3(0.5f, 0.62f, 0.11f), paint);        // ốp chân
            S(rb, new Vector3(0f, 0.66f, 0.31f), new Vector3(-20f, 0f, 0f), new Vector3(0.44f, 0.55f, 0.06f), Black);
            S(rbSoft, new Vector3(0f, 1.0f, 0.47f), Vector3.zero, new Vector3(0.36f, 0.2f, 0.3f), paint);                 // đầu đèn
            S(sph, new Vector3(0f, 1.0f, 0.61f), Vector3.zero, new Vector3(0.2f, 0.11f, 0.06f), Lamp);
            S(rb, new Vector3(0f, 1.1f, 0.41f), new Vector3(-28f, 0f, 0f), new Vector3(0.2f, 0.04f, 0.13f), Black);       // đồng hồ
            Tube(new Vector3(0f, 0.92f, 0.44f), new Vector3(0f, 0.46f, 0.14f), 0.08f, paint);                                // khung xương
            S(rb, new Vector3(0f, 0.47f, 0.22f), new Vector3(-30f, 0f, 0f), new Vector3(0.18f, 0.16f, 0.3f), paint);
            S(rbSoft, new Vector3(0f, 0.33f, 0.04f), Vector3.zero, new Vector3(0.26f, 0.24f, 0.34f), Engine);            // máy
            S(rbSoft, new Vector3(0f, 0.38f, 0.24f), new Vector3(-60f, 0f, 0f), new Vector3(0.16f, 0.14f, 0.2f), Engine);
            Tube(new Vector3(-0.1f, 0.31f, 0.02f), new Vector3(-0.25f, 0.31f, 0.02f), 0.03f, Black);                       // gác chân
            Tube(new Vector3(0.1f, 0.31f, 0.02f), new Vector3(0.25f, 0.31f, 0.02f), 0.03f, Black);
            S(rb, new Vector3(0f, 0.66f, -0.36f), Vector3.zero, new Vector3(0.34f, 0.3f, 0.78f), paint);                 // thân sau
            S(rb, new Vector3(0f, 0.6f, -0.32f), Vector3.zero, new Vector3(0.345f, 0.06f, 0.62f), accent);               // sọc tem
            S(rbSoft, new Vector3(0f, 0.85f, -0.22f), Vector3.zero, new Vector3(0.28f, 0.12f, 0.66f), seatCol);          // yên
            S(rb, new Vector3(0f, 0.63f, -0.78f), new Vector3(10f, 0f, 0f), new Vector3(0.2f, 0.12f, 0.24f), paint);      // đuôi
            S(sph, new Vector3(0f, 0.7f, -0.86f), Vector3.zero, new Vector3(0.17f, 0.06f, 0.05f), Red);
            Tube(new Vector3(-0.1f, 0.87f, -0.55f), new Vector3(-0.1f, 0.87f, -0.86f), 0.02f, Black);                     // baga
            Tube(new Vector3(0.1f, 0.87f, -0.55f), new Vector3(0.1f, 0.87f, -0.86f), 0.02f, Black);
            Tube(new Vector3(-0.1f, 0.87f, -0.86f), new Vector3(0.1f, 0.87f, -0.86f), 0.02f, Black);
            Tube(new Vector3(0.13f, 0.27f, 0.12f), new Vector3(0.15f, 0.3f, -0.56f), 0.075f, Chrome);                      // ống xả
            S(rb, new Vector3(0.19f, 0.31f, -0.2f), Vector3.zero, new Vector3(0.04f, 0.1f, 0.32f), Black);
            Tube(new Vector3(0.08f, 0.3f, -0.02f), new Vector3(0.08f, 0.29f, -0.6f), 0.035f, Black);                       // gắp sau
            Tube(new Vector3(-0.08f, 0.3f, -0.02f), new Vector3(-0.08f, 0.29f, -0.6f), 0.035f, Black);
            S(rb, new Vector3(-0.11f, 0.3f, -0.3f), Vector3.zero, new Vector3(0.04f, 0.1f, 0.56f), Black);                // hộp xích
            foreach (float sx in new[] { -0.13f, 0.13f })
            {
                Tube(new Vector3(sx, 0.31f, -0.56f), new Vector3(sx, 0.68f, -0.45f), 0.04f, Chrome);                       // giảm xóc
                Tube(new Vector3(sx, 0.4f, -0.53f), new Vector3(sx, 0.6f, -0.48f), 0.06f, Black);
            }
        }
        else
        {
            // ===== Xe tay ga (kiểu Vision) =====
            float df = 0.5f, dr = 0.48f;
            Vector3 fa = new Vector3(0f, df * 0.5f, 0.64f), ra = new Vector3(0f, dr * 0.5f, -0.58f);
            Wheel(fa, df, true);
            Wheel(ra, dr, false);
            Tube(fa + Vector3.right * 0.07f, new Vector3(0.05f, 0.8f, 0.5f), 0.045f, Black);
            Tube(fa - Vector3.right * 0.07f, new Vector3(-0.05f, 0.8f, 0.5f), 0.045f, Black);
            S(rb, new Vector3(0f, 0.55f, 0.64f), new Vector3(-8f, 0f, 0f), new Vector3(0.14f, 0.06f, 0.38f), paint);
            S(rbSoft, new Vector3(0f, 0.7f, 0.43f), new Vector3(-22f, 0f, 0f), new Vector3(0.5f, 0.78f, 0.2f), paint);  // yếm
            S(rb, new Vector3(0f, 0.68f, 0.34f), new Vector3(-22f, 0f, 0f), new Vector3(0.42f, 0.6f, 0.06f), accent == Black ? Black : Engine);
            S(rbSoft, new Vector3(0f, 1.04f, 0.46f), Vector3.zero, new Vector3(0.52f, 0.16f, 0.26f), paint);            // ốp tay lái
            S(sph, new Vector3(0f, 1.05f, 0.59f), Vector3.zero, new Vector3(0.26f, 0.08f, 0.05f), Lamp);
            S(rb, new Vector3(0f, 1.12f, 0.42f), new Vector3(-25f, 0f, 0f), new Vector3(0.22f, 0.03f, 0.12f), Black);
            S(rb, new Vector3(0f, 0.3f, 0.08f), Vector3.zero, new Vector3(0.36f, 0.07f, 0.52f), Black);                  // sàn để chân
            S(rb, new Vector3(0f, 0.24f, 0.08f), Vector3.zero, new Vector3(0.4f, 0.14f, 0.5f), paint);
            S(rbSoft, new Vector3(0f, 0.57f, -0.36f), Vector3.zero, new Vector3(0.42f, 0.44f, 0.86f), paint);           // thân sau
            S(rb, new Vector3(0f, 0.47f, -0.36f), Vector3.zero, new Vector3(0.425f, 0.05f, 0.7f), accent);
            S(rbSoft, new Vector3(0f, 0.84f, -0.3f), Vector3.zero, new Vector3(0.32f, 0.12f, 0.72f), seatCol);
            S(rbSoft, new Vector3(-0.14f, 0.3f, -0.4f), Vector3.zero, new Vector3(0.12f, 0.2f, 0.52f), Engine);          // hộp số
            Tube(new Vector3(0.15f, 0.3f, -0.3f), new Vector3(0.16f, 0.34f, -0.74f), 0.09f, Black);                         // pô
            Tube(new Vector3(0.16f, 0.34f, -0.72f), new Vector3(0.16f, 0.345f, -0.79f), 0.06f, Chrome);
            Tube(new Vector3(-0.13f, 0.3f, -0.56f), new Vector3(-0.13f, 0.6f, -0.42f), 0.05f, Black);
            S(rb, new Vector3(0f, 0.63f, -0.8f), Vector3.zero, new Vector3(0.3f, 0.08f, 0.05f), Red);
            S(sph, new Vector3(-0.15f, 0.62f, -0.79f), Vector3.zero, new Vector3(0.06f, 0.05f, 0.04f), Amber);
            S(sph, new Vector3(0.15f, 0.62f, -0.79f), Vector3.zero, new Vector3(0.06f, 0.05f, 0.04f), Amber);
            Tube(new Vector3(-0.08f, 0.9f, -0.7f), new Vector3(0.08f, 0.9f, -0.7f), 0.025f, Chrome);                          // tay nắm sau
        }

        // Chung: tay lái, tay nắm, tay phanh, gương, xi-nhan trước, biển số
        float gy = scooter ? 1.05f : 1.04f;
        Tube(new Vector3(-0.3f, gy, 0.43f), new Vector3(0.3f, gy, 0.43f), 0.025f, scooter ? Black : Chrome);
        foreach (float sx in new[] { -1f, 1f })
        {
            Tube(new Vector3(sx * 0.24f, gy, 0.43f), new Vector3(sx * 0.37f, gy, 0.43f), 0.036f, Black);
            S(rb, new Vector3(sx * 0.29f, gy + 0.01f, 0.5f), new Vector3(0f, sx * 12f, 0f), new Vector3(0.14f, 0.015f, 0.02f), Black);
            Tube(new Vector3(sx * 0.21f, gy + 0.03f, 0.44f), new Vector3(sx * 0.27f, gy + 0.28f, 0.45f), 0.012f, Black);
            S(sph, new Vector3(sx * 0.28f, gy + 0.3f, 0.45f), Vector3.zero, new Vector3(0.1f, 0.075f, 0.025f), Black);
            S(sph, new Vector3(sx * 0.17f, scooter ? 0.98f : 0.98f, 0.6f), Vector3.zero, new Vector3(0.05f, 0.04f, 0.05f), Amber);
        }
        float pz = scooter ? -0.83f : -0.89f;
        S(VShape.Cube, new Vector3(0f, 0.52f, pz), new Vector3(10f, 0f, 0f), new Vector3(0.19f, 0.14f, 0.01f), Plate);
        S(VShape.Cube, new Vector3(0f, 0.545f, pz - 0.006f), new Vector3(10f, 0f, 0f), new Vector3(0.14f, 0.025f, 0.004f), Black);
        S(VShape.Cube, new Vector3(0f, 0.5f, pz - 0.009f), new Vector3(10f, 0f, 0f), new Vector3(0.15f, 0.025f, 0.004f), Black);

        Mesh m = mb.ToMesh(name);
        m.hideFlags = HideFlags.DontSave;
        return m;
    }

    /// <summary>Mũ bảo hiểm nửa đầu: vỏ tròn, viền, lưỡi trai ngắn, quai; gốc ở mép dưới vỏ, mặt trước +Z.</summary>
    public static Mesh Helmet(Color shell, bool low)
    {
        var mb = new VMeshBuilder();
        Color trim = Color.Lerp(shell, Color.black, 0.55f);
        mb.Add(VShape.Dome(low ? 10 : 22), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.29f, 0.3f, 0.33f)), shell);
        mb.Add(VShape.Torus(0.2f, low ? 10 : 24), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.3f, 0.12f, 0.34f)), trim);
        mb.Add(VShape.RoundBox(0.4f, low ? 6 : 12), Matrix4x4.TRS(new Vector3(0f, 0.015f, 0.17f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.21f, 0.022f, 0.08f)), trim);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(-0.13f, -0.08f, 0.02f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.012f, 0.16f, 0.02f)), Black);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0.13f, -0.08f, 0.02f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.012f, 0.16f, 0.02f)), Black);
        Mesh m = mb.ToMesh(RealModels.MeshPrefix + "_Helmet");
        m.hideFlags = HideFlags.DontSave;
        return m;
    }
}
