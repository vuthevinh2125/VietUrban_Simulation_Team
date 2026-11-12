using UnityEngine;

/// <summary>
/// Đồ vỉa hè chi tiết (thay các khối hộp đơn giản): ghế nhựa đỏ / xanh kiểu quán Việt có chân xoè và lỗ quai,
/// bàn inox / bàn nhựa thấp, bát phở, ống đũa, lọ tương ớt, gánh hàng rong (đòn gánh cong, thúng tre), xe đạp chở hoa,
/// xe bánh mì tủ kính, ô dù quán, chậu cây, thùng rác, bàn cờ tướng. Khối bo tròn mịn, chung vật liệu bảng màu.
/// </summary>
public partial class VietDistrict
{
    // ================= Khối mịn =================
    private void RBox(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow, float e = 0.3f) =>
        (shadow ? solid : detail).Add(VShape.RoundBox(e, 8), Matrix4x4.TRS(c, r, s), col);

    private void Sph(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) =>
        (shadow ? solid : detail).Add(VShape.Sphere(10), Matrix4x4.TRS(c, r, s), col);

    private void Cyl(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) =>
        (shadow ? solid : detail).Add(VShape.Cylinder(10), Matrix4x4.TRS(c, r, s), col);

    private void Ring(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow, float minor = 0.12f) =>
        (shadow ? solid : detail).Add(VShape.Torus(minor, 16), Matrix4x4.TRS(c, r, s), col);

    private void Bowl(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) =>
        (shadow ? solid : detail).Add(VShape.Dome(12), Matrix4x4.TRS(c, r * Quaternion.Euler(180f, 0f, 0f), s), col);

    /// <summary>Ống tròn từ a tới b, đường kính d.</summary>
    private void Pipe(Vector3 a, Vector3 b, float d, Color col, bool shadow = false)
    {
        Vector3 dir = b - a;
        if (dir.sqrMagnitude < 1e-6f) return;
        (shadow ? solid : detail).Add(VShape.Cylinder(8), Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(d, dir.magnitude, d)), col);
    }

    // ================= Ghế nhựa, bàn quán =================
    /// <summary>Ghế đẩu nhựa thấp (cao 0.3 m): mặt ngồi bo tròn có lỗ giữa, 4 chân xoè, thanh giằng.</summary>
    private void PlasticStool(Vector3 p, Quaternion r, Color c)
    {
        Color dark = Color.Lerp(c, Color.black, 0.25f);
        RBox(p + Vector3.up * 0.29f, r, new Vector3(0.28f, 0.035f, 0.28f), c, false, 0.35f);
        Cyl(p + Vector3.up * 0.31f, r, new Vector3(0.07f, 0.004f, 0.07f), dark, false);
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = r * Quaternion.Euler(0f, 45f + k * 90f, 0f);
            Vector3 top = p + Vector3.up * 0.28f + q * Vector3.forward * 0.15f, foot = p + q * Vector3.forward * 0.2f;
            Vector3 leg = (top - foot).normalized, side = q * Vector3.right;
            RBox((top + foot) * 0.5f, Quaternion.LookRotation(Vector3.Cross(side, leg), leg), new Vector3(0.09f, 0.3f, 0.03f), c, false, 0.4f);
        }
        Ring(p + Vector3.up * 0.09f, r * Quaternion.Euler(0f, 45f, 0f), new Vector3(0.38f, 0.1f, 0.38f), c, false, 0.06f);
    }

    /// <summary>Bàn thấp quán vỉa hè (inox hoặc nhựa) cao 0.45 m, bày bát, ống đũa, lọ tương ớt, giấy ăn.</summary>
    private void StreetTable(Vector3 p, Quaternion r, bool inox, bool dishes)
    {
        Color top = inox ? Steel : (Chance(0.5) ? Stool2 : Stool1);
        RBox(p + Vector3.up * 0.44f, r, new Vector3(0.78f, 0.03f, 0.52f), top, false, 0.25f);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Pipe(p + r * new Vector3(sx * 0.34f, 0.43f, sz * 0.21f), p + r * new Vector3(sx * 0.36f, 0f, sz * 0.23f), 0.025f, inox ? Steel : top);
        if (!dishes) return;
        int n = rng.Next(1, 4);
        for (int k = 0; k < n; k++)
        {
            Vector3 b = p + Vector3.up * 0.46f + r * new Vector3(Range(-0.28f, 0.28f), 0f, Range(-0.14f, 0.14f));
            Bowl(b + Vector3.up * 0.07f, r, new Vector3(0.17f, 0.14f, 0.17f), White, false);
            Cyl(b + Vector3.up * 0.06f, r, new Vector3(0.15f, 0.005f, 0.15f), new Color(0.75f, 0.55f, 0.3f), false); // nước dùng
            Pipe(b + new Vector3(0.05f, 0.08f, 0f), b + new Vector3(0.12f, 0.14f, 0.03f), 0.008f, WoodBrown);
        }
        Vector3 c = p + Vector3.up * 0.46f + r * new Vector3(0.3f, 0f, 0.18f);
        Cyl(c + Vector3.up * 0.07f, r, new Vector3(0.07f, 0.14f, 0.07f), new Color(0.2f, 0.5f, 0.85f), false);                    // ống đũa
        for (int k = 0; k < 6; k++) Pipe(c + Vector3.up * 0.1f + new Vector3(Range(-0.02f, 0.02f), 0f, Range(-0.02f, 0.02f)), c + Vector3.up * 0.24f + new Vector3(Range(-0.03f, 0.03f), 0f, Range(-0.03f, 0.03f)), 0.006f, Bamboo);
        Cyl(c + r * new Vector3(-0.1f, 0.06f, 0f), r, new Vector3(0.05f, 0.12f, 0.05f), new Color(0.85f, 0.15f, 0.1f), false);    // tương ớt
        Cyl(c + r * new Vector3(-0.17f, 0.05f, 0f), r, new Vector3(0.05f, 0.1f, 0.05f), new Color(0.55f, 0.35f, 0.15f), false);   // nước mắm
        RBox(c + r * new Vector3(-0.3f, 0.04f, 0f), r, new Vector3(0.1f, 0.08f, 0.1f), White, false);                                 // hộp giấy ăn
    }

    private void FoodSet(Vector3 p, Vector3 u, Vector3 vin, bool people)
    {
        Quaternion r = Quaternion.LookRotation(vin);
        Color sc = Chance(0.6) ? Stool1 : Stool2;
        StreetTable(p, r, Chance(0.6), true);
        Vector3[] seats = { u * 0.62f, -u * 0.62f, vin * 0.48f, -vin * 0.48f };
        int seated = people ? rng.Next(1, 4) : 0;
        for (int k = 0; k < 4; k++)
        {
            Vector3 s = p + seats[k];
            PlasticStool(s, r, sc);
            if (k < seated) Person(s, p - s, 1, Chance(0.15) ? 2 : 0);
        }
    }

    private void LowTable(Vector3 p, Quaternion r, Color stool)
    {
        StreetTable(p, r, true, Chance(0.6));
        PlasticStool(p + r * new Vector3(0.6f, 0f, 0f), r, stool);
        PlasticStool(p + r * new Vector3(-0.6f, 0f, 0f), r, stool);
        if (Chance(0.5)) Person(p + r * new Vector3(0.6f, 0f, 0f), r * Vector3.left, 1, 0);
    }

    private void ChessGame(Vector3 p)
    {
        RBox(p + Vector3.up * 0.2f, Quaternion.identity, new Vector3(0.5f, 0.4f, 0.5f), WoodBrown, false, 0.25f);
        RBox(p + Vector3.up * 0.41f, Quaternion.identity, new Vector3(0.46f, 0.025f, 0.46f), new Color(0.9f, 0.8f, 0.55f), false, 0.2f);
        for (float g = -0.18f; g <= 0.181f; g += 0.045f)
        {
            Box(p + new Vector3(g, 0.424f, 0f), Quaternion.identity, new Vector3(0.004f, 0.002f, 0.4f), Dark, false);
            Box(p + new Vector3(0f, 0.424f, g), Quaternion.identity, new Vector3(0.4f, 0.002f, 0.004f), Dark, false);
        }
        for (int k = 0; k < 16; k++)
            Cyl(p + new Vector3(Range(-0.18f, 0.18f), 0.435f, Range(-0.18f, 0.18f)), Quaternion.identity, new Vector3(0.035f, 0.016f, 0.035f), k % 2 == 0 ? new Color(0.8f, 0.1f, 0.1f) : Dark, false);
        PlasticStool(p + new Vector3(0.6f, 0f, 0f), Quaternion.identity, Stool1);
        PlasticStool(p + new Vector3(-0.6f, 0f, 0f), Quaternion.identity, Stool2);
        Cyl(p + new Vector3(0.2f, 0.47f, 0.2f), Quaternion.identity, new Vector3(0.06f, 0.07f, 0.06f), new Color(0.95f, 0.85f, 0.4f), false); // chén trà
        Person(p + new Vector3(0.6f, 0f, 0f), Vector3.left, 1, 0);
        Person(p + new Vector3(-0.6f, 0f, 0f), Vector3.right, 1, 0);
        Person(p + new Vector3(0f, 0f, 0.8f), Vector3.back, 0, 0);
    }

    // ================= Ô, chậu cây, thùng rác =================
    private void Umbrella(Vector3 p)
    {
        Color c = Pick(UmbrellaColors);
        Cyl(p + Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.45f, 0.12f, 0.45f), Dark, false);
        Pipe(p, p + Vector3.up * 2.5f, 0.04f, White);
        (solid).Add(VShape.Dome(16), Matrix4x4.TRS(p + Vector3.up * 2.05f, Quaternion.identity, new Vector3(2.6f, 0.9f, 2.6f)), c);
        Ring(p + Vector3.up * 2.06f, Quaternion.identity, new Vector3(2.64f, 0.1f, 2.64f), Color.Lerp(c, Color.white, 0.5f), false, 0.03f);
        for (int k = 0; k < 8; k++)
        {
            Quaternion q = Quaternion.Euler(0f, k * 45f, 0f);
            Pipe(p + Vector3.up * 2.05f + q * Vector3.forward * 1.25f, p + Vector3.up * 2.25f, 0.015f, White);
        }
    }

    private void PlantPot(Vector3 p)
    {
        Color pot = Chance(0.5) ? Terracotta : new Color(0.2f, 0.35f, 0.6f);
        Cyl(p + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.32f, 0.3f, 0.32f), pot, false);
        Ring(p + Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.36f, 0.12f, 0.36f), pot, false, 0.1f);
        Cyl(p + Vector3.up * 0.295f, Quaternion.identity, new Vector3(0.28f, 0.01f, 0.28f), Soil, false);
        bool flower = Chance(0.3);
        Color leaf = Pick(Greens);
        for (int k = 0; k < 4; k++)
            Sph(p + new Vector3(Range(-0.1f, 0.1f), Range(0.45f, 0.6f), Range(-0.1f, 0.1f)), Rot(Range(0f, 360f)), new Vector3(0.28f, 0.24f, 0.28f), leaf, false);
        if (flower) for (int k = 0; k < 7; k++)
            Sph(p + new Vector3(Range(-0.17f, 0.17f), Range(0.5f, 0.72f), Range(-0.17f, 0.17f)), Quaternion.identity, Vector3.one * 0.07f, new Color(0.92f, 0.28f, 0.55f), false); // hoa giấy
    }

    private void Bin(Vector3 p)
    {
        Color g = new Color(0.15f, 0.5f, 0.25f);
        Cyl(p + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.5f, 0.9f, 0.5f), g, false);
        Sph(p + Vector3.up * 0.9f, Quaternion.identity, new Vector3(0.52f, 0.12f, 0.52f), Color.Lerp(g, Color.black, 0.2f), false);
        RBox(p + Vector3.up * 0.95f + Vector3.forward * 0.15f, Quaternion.identity, new Vector3(0.18f, 0.03f, 0.06f), Dark, false);
        foreach (int s in new[] { -1, 1 }) Cyl(p + new Vector3(s * 0.15f, 0.06f, -0.2f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.12f, 0.04f, 0.12f), Dark, false);
    }

    // ================= Gánh hàng rong =================
    /// <summary>Gánh hàng rong: nón lá, đòn gánh tre hơi võng, hai thúng tre có hoa quả, dây quang.</summary>
    private void Vendor(Vector3 p, Vector3 u)
    {
        Vector3 fwd = Vector3.Cross(u, Vector3.up);
        bool squat = Chance(0.3);
        Person(p, fwd, squat ? 2 : 0, 1);
        float poleY = squat ? 0.85f : 1.42f;
        Vector3 a = p - u * 0.95f + Vector3.up * (poleY - 0.04f), m = p + Vector3.up * poleY, b = p + u * 0.95f + Vector3.up * (poleY - 0.04f);
        Pipe(a, m, 0.04f, Bamboo);
        Pipe(m, b, 0.04f, Bamboo);
        foreach (float s in new[] { -0.82f, 0.82f })
        {
            Vector3 basket = p + u * s + Vector3.up * (squat ? 0.02f : 0.3f);
            Bowl(basket + Vector3.up * 0.26f, Quaternion.identity, new Vector3(0.58f, 0.5f, 0.58f), Bamboo, false);
            Ring(basket + Vector3.up * 0.26f, Quaternion.identity, new Vector3(0.6f, 0.12f, 0.6f), Color.Lerp(Bamboo, WoodBrown, 0.4f), false, 0.05f);
            Color fruit = Pick(Fruits);
            for (int k = 0; k < 9; k++)
                Sph(basket + new Vector3(Range(-0.17f, 0.17f), Range(0.2f, 0.3f), Range(-0.17f, 0.17f)), Rot(Range(0f, 360f)), Vector3.one * Range(0.09f, 0.13f), Chance(0.75) ? fruit : Pick(Fruits), false);
            Vector3 top = p + u * s * 1.1f + Vector3.up * (poleY - 0.03f);
            for (int k = 0; k < 4; k++)
            {
                float ang = (45f + k * 90f) * Mathf.Deg2Rad;
                Pipe(basket + new Vector3(Mathf.Cos(ang) * 0.28f, 0.27f, Mathf.Sin(ang) * 0.28f), top, 0.008f, WoodBrown);
            }
        }
    }

    // ================= Xe đạp chở hoa =================
    /// <summary>Xe đạp chở hoa mùa thu Hà Nội: bánh xe nan, khung ống, yên, ghi-đông, giỏ trước, sọt hoa sau đầy cúc hoạ mi / hồng.</summary>
    private void FlowerBike(Vector3 p, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Quaternion r = Quaternion.LookRotation(forward.normalized);
        Vector3 L(float x, float y, float z) => p + r * new Vector3(x, y, z);
        Color frame = Chance(0.5) ? new Color(0.15f, 0.35f, 0.28f) : new Color(0.12f, 0.12f, 0.14f);
        Quaternion side = r * Quaternion.Euler(0f, 0f, 90f);
        foreach (float wz in new[] { 0.55f, -0.52f })
        {
            Vector3 hub = L(0f, 0.34f, wz);
            Ring(hub, side, new Vector3(0.68f, 0.18f, 0.68f), Rail, false, 0.06f);
            Ring(hub, side, new Vector3(0.62f, 0.1f, 0.62f), Steel, false, 0.03f);
            for (int k = 0; k < 12; k++)
            {
                Quaternion q = r * Quaternion.Euler(k * 30f, 0f, 0f);
                Pipe(hub, hub + q * Vector3.forward * 0.3f, 0.005f, Steel);
            }
        }
        Vector3 bb = L(0f, 0.32f, 0f), seat = L(0f, 0.86f, -0.18f), head = L(0f, 0.9f, 0.42f);
        Pipe(bb, seat, 0.035f, frame);
        Pipe(bb, head, 0.04f, frame);
        Pipe(seat + r * new Vector3(0f, -0.05f, 0f), head + r * new Vector3(0f, -0.06f, 0f), 0.032f, frame);
        Pipe(bb, L(0f, 0.34f, -0.52f), 0.025f, frame);
        Pipe(seat, L(0f, 0.34f, -0.52f), 0.022f, frame);
        Pipe(head, L(0f, 0.34f, 0.55f), 0.03f, frame);
        RBox(seat + Vector3.up * 0.04f, r, new Vector3(0.14f, 0.06f, 0.24f), Dark, false, 0.4f);
        Pipe(head + Vector3.up * 0.08f + r * new Vector3(-0.28f, 0f, -0.05f), head + Vector3.up * 0.08f + r * new Vector3(0.28f, 0f, -0.05f), 0.022f, Steel);
        Pipe(head, head + Vector3.up * 0.08f, 0.025f, Steel);
        // Giỏ trước + sọt sau
        Bowl(L(0f, 1.02f, 0.6f), r, new Vector3(0.36f, 0.3f, 0.3f), Bamboo, false);
        RBox(L(0f, 0.78f, -0.6f), r, new Vector3(0.6f, 0.06f, 0.42f), Rail, false);
        Bowl(L(0f, 1.05f, -0.6f), r, new Vector3(0.8f, 0.55f, 0.6f), Bamboo, false);
        Color main = Pick(Flowers);
        for (int k = 0; k < 40; k++)
        {
            Vector3 f = L(Range(-0.36f, 0.36f), Range(1.08f, 1.5f), -0.6f + Range(-0.26f, 0.26f));
            Pipe(f - Vector3.up * 0.25f, f, 0.008f, Plant);
            Sph(f, Rot(Range(0f, 360f)), Vector3.one * Range(0.06f, 0.1f), Chance(0.7) ? main : Pick(Flowers), false);
        }
        for (int k = 0; k < 8; k++) Sph(L(Range(-0.1f, 0.1f), Range(1.08f, 1.22f), 0.6f + Range(-0.08f, 0.08f)), Quaternion.identity, Vector3.one * 0.08f, Pick(Flowers), false);
        if (Chance(0.65)) Person(p + r * new Vector3(0.6f, 0f, 0f), -(r * Vector3.right), 0, 1);
    }

    // ================= Xe bánh mì =================
    private void BreadCart(Vector3 p, Vector3 u)
    {
        Quaternion r = Quaternion.LookRotation(u);
        Vector3 side = Vector3.Cross(Vector3.up, u);
        Vector3 L(float x, float y, float z) => p + r * new Vector3(x, y, z);
        RBox(L(0f, 0.6f, 0f), r, new Vector3(0.66f, 0.62f, 1.2f), White, true, 0.15f);
        RBox(L(0f, 0.93f, 0f), r, new Vector3(0.7f, 0.04f, 1.24f), Steel, false, 0.2f);
        RBox(L(0f, 1.18f, 0f), r, new Vector3(0.62f, 0.46f, 1.12f), Glass, false, 0.12f);
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Pipe(L(sx * 0.3f, 0.95f, sz * 0.55f), L(sx * 0.3f, 1.42f, sz * 0.55f), 0.025f, Steel);
        for (int k = 0; k < 7; k++)
            RBox(L(Range(-0.18f, 0.18f), 1.02f, -0.42f + k * 0.14f), r * Quaternion.Euler(0f, Range(-20f, 20f), 0f), new Vector3(0.1f, 0.07f, 0.3f), new Color(0.9f, 0.66f, 0.3f), false, 0.5f);
        RBox(L(0f, 1.58f, 0f), r, new Vector3(0.74f, 0.24f, 1.28f), new Color(0.8f, 0.1f, 0.08f), false, 0.2f);
        Sign(L(-0.38f, 1.58f, 0f), side, new Vector2(1.2f, 0.22f), "BÁNH MÌ", new Color(1f, 0.92f, 0.3f));
        Sign(L(0.38f, 1.58f, 0f), -side, new Vector2(1.2f, 0.22f), "BÁNH MÌ", new Color(1f, 0.92f, 0.3f));
        Quaternion wheel = r * Quaternion.Euler(0f, 0f, 90f);
        foreach (int sx in new[] { -1, 1 })
        {
            Ring(L(sx * 0.36f, 0.22f, 0.3f), wheel, new Vector3(0.42f, 0.25f, 0.42f), Rail, false, 0.15f);
            Cyl(L(sx * 0.36f, 0.22f, 0.3f), wheel, new Vector3(0.28f, 0.03f, 0.28f), Steel, false);
        }
        Pipe(L(0f, 0.22f, -0.5f), L(0f, 0.0f, -0.5f), 0.04f, Steel);
        Pipe(L(-0.25f, 0.85f, -0.62f), L(-0.25f, 0.85f, -0.85f), 0.025f, Steel);
        Pipe(L(0.25f, 0.85f, -0.62f), L(0.25f, 0.85f, -0.85f), 0.025f, Steel);
        Pipe(L(-0.25f, 0.85f, -0.85f), L(0.25f, 0.85f, -0.85f), 0.03f, Rail);
        Person(p - side * 0.9f, side, 0, Chance(0.5) ? 1 : 0);
    }
}
