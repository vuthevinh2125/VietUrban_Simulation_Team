using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Các kiểu nhà và công trình của khu phố Việt (nhà ống, biệt thự Pháp, ki-ốt, khu tập thể, Hồ Gươm, Nhà hát Lớn, chợ, trường, chùa, công viên).</summary>
public partial class VietDistrict
{
    // ================= Nhà ống =================
    private void TubeHouse(Vector3 o, Vector3 u, Vector3 vin, float w, float d, string address, Line street, Cell type, bool tall, bool alley)
    {
        Quaternion rot = Quaternion.LookRotation(vin, Vector3.up);
        Vector3 P(float a, float y, float i) => o + u * a + Vector3.up * y + vin * i;
        int floors = tall ? rng.Next(4, 9) : alley ? rng.Next(2, 5) : type == Cell.Residential ? rng.Next(3, 7) : rng.Next(2, 6);
        float h = floors * Floor;
        Color facade = Pick(Facades);
        Color trim = Color.Lerp(facade, White, 0.45f);
        double shopP = alley ? 0.3 : tall ? 0.95 : type == Cell.Residential ? 0.5 : type == Cell.Outer ? 0.6 : 0.9;
        bool shop = Chance(shopP);
        float gd = Mathf.Min(4.2f, d - 1f); // chiều sâu gian hàng tầng trệt

        // Thân nhà: các tầng trên + phần sau tầng trệt + 2 tường bên gian hàng
        Box(P(w * 0.5f, Floor + (h - Floor) * 0.5f, d * 0.5f), rot, new Vector3(w - 0.06f, h - Floor, d), facade, true);
        Box(P(w * 0.5f, Floor * 0.5f, gd + (d - gd) * 0.5f), rot, new Vector3(w - 0.06f, Floor, d - gd), facade, true);
        Box(P(0.12f, Floor * 0.5f, gd * 0.5f), rot, new Vector3(0.24f, Floor, gd), facade, true);
        Box(P(w - 0.12f, Floor * 0.5f, gd * 0.5f), rot, new Vector3(0.24f, Floor, gd), facade, true);
        HouseCollider(P(w * 0.5f, h * 0.5f, d * 0.5f), rot, new Vector3(w, h, d), address, P(w * 0.5f, 0f, alley ? -0.9f : -1.3f));
        // Trụ hai bên + gờ chỉ tầng
        Box(P(0.1f, h * 0.5f, -0.05f), rot, new Vector3(0.2f, h, 0.1f), trim, false);
        Box(P(w - 0.1f, h * 0.5f, -0.05f), rot, new Vector3(0.2f, h, 0.1f), trim, false);
        for (int f = 1; f < floors; f++) Box(P(w * 0.5f, f * Floor, -0.06f), rot, new Vector3(w, 0.18f, 0.12f), trim, false);
        // Vệt ố dưới mái (nhà cũ)
        if (!tall && Chance(0.5)) Box(P(w * 0.5f, h - 0.4f, -0.015f), rot, new Vector3(w - 0.3f, 0.8f, 0.02f), Color.Lerp(facade, Dark, 0.18f), false);

        string trade = Trade(street.id, type, tall);
        bool food = IsFood(trade);
        if (shop) ShopInterior(P, rot, w, gd, trade, food);
        else
        {
            // Cửa cuốn / cửa xếp sắt
            Box(P(w * 0.5f, 1.4f, 0.05f), rot, new Vector3(w - 0.5f, 2.7f, 0.04f), Metal, false);
            for (float y = 0.3f; y < 2.8f; y += 0.22f) Box(P(w * 0.5f, y, 0.02f), rot, new Vector3(w - 0.5f, 0.025f, 0.02f), Rail, false);
            Box(P(w * 0.5f, 2.85f, 0.0f), rot, new Vector3(w - 0.4f, 0.3f, 0.3f), Metal, false);
        }

        // Biển hiệu + mái bạt
        string num = address.Split(' ')[0];
        if (shop)
        {
            Color sc = Pick(SignColors);
            float sw = alley ? Mathf.Min(w - 0.6f, 2.4f) : w - 0.2f;
            Box(P(w * 0.5f, 2.95f, -0.18f), rot, new Vector3(sw, alley ? 0.6f : 0.85f, 0.12f), sc, true);
            string sub = alley ? "" : $"\n<size=48%>{num} {street.vn}  -  ĐT: 09{rng.Next(10, 99)}.{rng.Next(100, 999)}.{rng.Next(100, 999)}</size>";
            Sign(P(w * 0.5f, 2.95f, -0.25f), vin, new Vector2(sw - 0.2f, alley ? 0.5f : 0.75f), trade + sub, sc.grayscale > 0.6f ? new Color(0.75f, 0.05f, 0.05f) : new Color(1f, 0.96f, 0.7f));
            if (!alley && Chance(0.6))
            {
                Color aw = Pick(Awnings);
                Box(P(w * 0.5f, 2.38f, -0.85f), rot * Quaternion.Euler(-13f, 0f, 0f), new Vector3(w - 0.15f, 0.04f, 1.7f), aw, true);
                Box(P(w * 0.5f, 2.08f, -1.68f), rot, new Vector3(w - 0.15f, 0.22f, 0.03f), Color.Lerp(aw, White, 0.35f), false);
            }
        }

        // Các tầng trên
        bool glassy = tall && Chance(0.4);
        for (int f = 1; f < floors; f++)
        {
            float y = f * Floor;
            if (glassy)
            {
                Box(P(w * 0.5f, y + 1.6f, -0.02f), rot, new Vector3(w - 0.5f, 2.8f, 0.04f), GlassBlue, false);
                for (float a = 1.2f; a < w - 0.8f; a += 1.4f) Box(P(a, y + 1.6f, -0.05f), rot, new Vector3(0.06f, 2.8f, 0.04f), Metal, false);
                continue;
            }
            double kind = rng.NextDouble();
            if (kind < 0.55) Balcony(P, rot, w, y);
            else if (kind < 0.85)
            {
                // Cửa sổ có hoa sắt
                float ww = Mathf.Min(w * 0.5f, 2.2f);
                Box(P(w * 0.5f, y + 1.6f, -0.02f), rot, new Vector3(ww, 1.5f, 0.04f), Glass, false);
                Box(P(w * 0.5f, y + 1.6f, -0.01f), rot, new Vector3(ww + 0.16f, 1.66f, 0.03f), Frame, false);
                for (float a = -ww * 0.5f + 0.25f; a < ww * 0.5f; a += 0.3f) Box(P(w * 0.5f + a, y + 1.6f, -0.12f), rot, new Vector3(0.03f, 1.5f, 0.03f), Rail, false);
                Box(P(w * 0.5f, y + 2.0f, -0.12f), rot, new Vector3(ww, 0.03f, 0.03f), Rail, false);
                Box(P(w * 0.5f, y + 1.2f, -0.12f), rot, new Vector3(ww, 0.03f, 0.03f), Rail, false);
                Box(P(w * 0.5f, y + 2.42f, -0.25f), rot * Quaternion.Euler(-20f, 0f, 0f), new Vector3(ww + 0.4f, 0.04f, 0.55f), Pick(Roofs), false);
            }
            else
            {
                // Cửa chớp gỗ
                Color sh = Chance(0.5) ? Shutter : WoodBrown;
                for (float a = w * 0.3f; a < w - 0.5f; a += w * 0.4f)
                {
                    Box(P(a, y + 1.5f, -0.02f), rot, new Vector3(0.9f, 1.9f, 0.04f), Glass, false);
                    Box(P(a - 0.62f, y + 1.5f, -0.05f), rot * Quaternion.Euler(0f, -25f, 0f), new Vector3(0.45f, 1.9f, 0.04f), sh, false);
                    Box(P(a + 0.62f, y + 1.5f, -0.05f), rot * Quaternion.Euler(0f, 25f, 0f), new Vector3(0.45f, 1.9f, 0.04f), sh, false);
                }
            }
            if (Chance(0.45)) Box(P(Chance(0.5) ? 0.6f : w - 0.6f, y + 2.55f, -0.2f), rot, new Vector3(0.8f, 0.55f, 0.32f), White, false);
            if (f == 1 && Chance(0.22)) Flag(P(0.35f, y + 1.0f, 0f), u, vin);
            if (f == 2 && !alley && Chance(tall ? 0.35 : 0.12)) SideSign(P(w - 0.35f, y + 0.2f, -0.55f), u, vin);
        }

        // Mái: tường chắn mái, mái tôn, tum thang, bồn nước inox nằm, bình năng lượng mặt trời, ăng-ten, cây cảnh, quần áo phơi
        Box(P(w * 0.5f, h + 0.45f, 0.08f), rot, new Vector3(w - 0.06f, 0.9f, 0.16f), facade, true);
        bool tin = Chance(0.28);
        if (tin) Box(P(w * 0.5f, h + 0.95f, d * 0.2f), rot * Quaternion.Euler(-12f, 0f, 0f), new Vector3(w, 0.06f, d * 0.36f), Pick(Roofs), true);
        if (d >= 8f && Chance(0.6))
        {
            Box(P(w * 0.5f, h + 1.25f, d * 0.78f), rot, new Vector3(Mathf.Min(w - 0.4f, 2.8f), 2.5f, 3.0f), facade, true);
            Box(P(w * 0.5f, h + 2.55f, d * 0.78f), rot, new Vector3(Mathf.Min(w - 0.2f, 3.1f), 0.08f, 3.3f), Pick(Roofs), false);
        }
        if (Chance(0.7))
        {
            Vector3 tank = P(w * 0.5f, h + 0.95f, d * 0.5f);
            Box(tank + Vector3.down * 0.55f, rot, new Vector3(1.6f, 0.08f, 0.9f), Rail, false);
            Box(tank + Vector3.down * 0.3f + u * 0.6f, rot, new Vector3(0.06f, 0.5f, 0.06f), Rail, false);
            Box(tank + Vector3.down * 0.3f - u * 0.6f, rot, new Vector3(0.06f, 0.5f, 0.06f), Rail, false);
            Prism(tank, rot * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.95f, 1.7f, 0.95f), Steel, true);
        }
        if (Chance(0.18))
        {
            Vector3 sp = P(w * 0.3f, h + 1.0f, d * 0.62f);
            Box(sp, rot * Quaternion.Euler(-35f, 0f, 0f), new Vector3(1.3f, 0.05f, 1.6f), PanelBlue, false);
            Prism(sp + Vector3.up * 0.7f + vin * 0.5f, rot * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.4f, 1.3f, 0.4f), Steel, false);
        }
        if (Chance(0.3))
        {
            Vector3 ap = P(w * 0.8f, h + 0.9f, d * 0.35f);
            Box(ap + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.04f, 3f, 0.04f), Rail, false);
            for (int k = 0; k < 3; k++) Box(ap + Vector3.up * (2.2f + k * 0.35f), rot, new Vector3(1.0f - k * 0.25f, 0.03f, 0.03f), Rail, false);
        }
        if (Chance(0.3)) for (int k = rng.Next(2, 6); k > 0; k--) PlantPot(P(Range(0.5f, w - 0.5f), h, Range(0.5f, 2.5f)));
        if (Chance(0.25)) Laundry(P(0.4f, h + 1.4f, d * 0.4f), P(w - 0.4f, h + 1.4f, d * 0.4f));
        if (tall && Chance(0.16)) Billboard(P(w * 0.5f, h + 0.9f, d * 0.45f), vin, Mathf.Max(w, 6f));

        // Vỉa hè trước nhà
        if (alley)
        {
            if (Chance(0.4)) Motorbike(o + u * Range(0.8f, w - 0.8f) - vin * 0.7f, u, Pick(Bikes));
            if (Chance(0.3)) PlantPot(o + u * 0.4f - vin * 0.3f);
            return;
        }
        if (shop && Chance(0.62))
        {
            int n = rng.Next(1, Mathf.Max(2, (int)(w / 0.85f)));
            for (int b = 0; b < n; b++)
                Motorbike(o + u * (0.5f + b * 0.85f) - vin * 1.15f, Quaternion.AngleAxis(Range(-30f, -15f), Vector3.up) * -vin, Pick(Bikes));
        }
        if (food) FoodSet(o + u * (w * 0.5f) - vin * 1.6f, u, vin, Chance(0.75));
        else if (shop && Chance(0.45)) GoodsDisplay(P, rot, w, trade);
        if (!shop && Chance(0.3)) PlantPot(o + u * (w - 0.4f) - vin * 0.3f);
    }

    private void Balcony(System.Func<float, float, float, Vector3> P, Quaternion rot, float w, float y)
    {
        Box(P(w * 0.5f, y + 0.08f, -0.55f), rot, new Vector3(w - 0.2f, 0.16f, 1.1f), Slab, true);
        bool cage = Chance(0.18);
        if (cage)
        {
            // "Chuồng cọp": lồng sắt quây kín ban công
            for (float a = 0.2f; a < w - 0.1f; a += 0.35f) Box(P(a, y + 1.4f, -1.08f), rot, new Vector3(0.03f, 2.6f, 0.03f), Rail, false);
            Box(P(w * 0.5f, y + 2.7f, -0.55f), rot, new Vector3(w - 0.2f, 0.05f, 1.1f), Rail, false);
        }
        else
        {
            Box(P(w * 0.5f, y + 0.62f, -1.08f), rot, new Vector3(w - 0.2f, 0.9f, 0.03f), Chance(0.5) ? Rail : White, false);
            Box(P(w * 0.5f, y + 1.1f, -1.08f), rot, new Vector3(w - 0.15f, 0.06f, 0.07f), Rail, false);
        }
        Box(P(w * 0.5f, y + 1.35f, -0.02f), rot, new Vector3(w * 0.56f, 2.3f, 0.04f), Glass, false);
        Box(P(w * 0.5f, y + 1.35f, -0.01f), rot, new Vector3(w * 0.56f + 0.16f, 2.46f, 0.03f), Frame, false);
        if (Chance(0.55)) for (int k = rng.Next(1, 4); k > 0; k--) PlantPot(P(Range(0.4f, w - 0.4f), y + 0.16f, -0.8f));
        if (Chance(0.35))
            for (float a = 0.4f; a < w - 0.4f; a += Range(0.35f, 0.55f))
                Box(P(a, y + 1.9f, -0.85f), rot, new Vector3(0.3f, Range(0.4f, 0.7f), 0.02f), Pick(Clothes), false);
    }

    private void ShopInterior(System.Func<float, float, float, Vector3> P, Quaternion rot, float w, float gd, string trade, bool food)
    {
        Box(P(w * 0.5f, 0.012f, gd * 0.5f), rot, new Vector3(w - 0.5f, 0.02f, gd), TileFloor, false);
        Box(P(w * 0.5f, 1.6f, gd - 0.01f), rot, new Vector3(w - 0.5f, 3.2f, 0.02f), Interior, false);
        if (food)
        {
            // Nồi nước dùng to trên xe đẩy đầu nhà + bàn ghế nhựa bên trong
            Box(P(0.75f, 0.42f, 0.55f), rot, new Vector3(1.0f, 0.84f, 0.75f), Steel, false);
            Prism(P(0.75f, 1.08f, 0.55f), rot, new Vector3(0.62f, 0.5f, 0.62f), Steel, false);
            Box(P(0.75f, 1.3f, 0.55f), rot, new Vector3(0.3f, 0.02f, 0.3f), Steel, false);
            for (float i = 1.6f; i < gd - 0.6f; i += 1.2f) LowTable(P(w * 0.6f, 0f, i), rot, Chance(0.5) ? Stool1 : Stool2);
            return;
        }
        if (trade.Contains("VÀNG") || trade.Contains("BẠC") || trade.Contains("TRANG SỨC"))
        {
            Box(P(w * 0.5f, 1.6f, gd - 0.03f), rot, new Vector3(w - 0.5f, 3.2f, 0.02f), new Color(0.65f, 0.08f, 0.06f), false);
            for (float i = 0.8f; i < gd - 0.5f; i += 1.4f)
            {
                Box(P(w * 0.5f, 0.5f, i), rot, new Vector3(w - 1.2f, 1.0f, 0.5f), Frame, false);
                Box(P(w * 0.5f, 1.05f, i), rot, new Vector3(w - 1.2f, 0.12f, 0.5f), Gold, false);
            }
            return;
        }
        if (trade.Contains("SỬA XE"))
        {
            for (int k = 0; k < 4; k++) Prism(P(0.6f, 0.2f + k * 0.13f, gd - 0.8f), rot * Quaternion.Euler(0f, 0f, 0f), new Vector3(0.6f, 0.12f, 0.6f), Dark, false);
            Motorbike(P(w * 0.5f, 0f, 1.6f), rot * Vector3.right, Pick(Bikes));
            return;
        }
        if (trade.Contains("THỜI TRANG") || trade.Contains("LỤA") || trade.Contains("VẢI") || trade.Contains("GIÀY"))
        {
            for (float i = 0.6f; i < gd - 0.3f; i += 0.35f)
            {
                Box(P(0.45f, 1.5f, i), rot, new Vector3(0.05f, 0.9f, 0.3f), Pick(Clothes), false);
                Box(P(w - 0.45f, 1.5f, i), rot, new Vector3(0.05f, 0.9f, 0.3f), Pick(Clothes), false);
            }
            return;
        }
        // Tạp hoá / thuốc / điện thoại...: kệ hai bên chất đầy hàng + quầy
        foreach (float a in new[] { 0.5f, w - 0.5f })
        {
            Box(P(a, 1.1f, gd * 0.5f), rot, new Vector3(0.5f, 2.2f, gd - 0.6f), Frame, false);
            for (int lvl = 0; lvl < 4; lvl++)
                for (float i = 0.6f; i < gd - 0.5f; i += 0.45f)
                    Box(P(a + (a < w * 0.5f ? 0.08f : -0.08f), 0.35f + lvl * 0.5f, i), rot, new Vector3(0.38f, 0.32f, 0.36f), Pick(Goods), false);
        }
        Box(P(w * 0.5f, 0.5f, gd - 1.0f), rot, new Vector3(Mathf.Min(1.6f, w - 1.6f), 1.0f, 0.5f), Glass, false);
    }

    private void GoodsDisplay(System.Func<float, float, float, Vector3> P, Quaternion rot, float w, string trade)
    {
        for (float a = 0.5f; a < w - 0.4f; a += 0.7f)
        {
            if (Chance(0.35)) continue;
            Box(P(a, 0.2f, -0.45f), rot, new Vector3(0.55f, 0.4f, 0.45f), Chance(0.5) ? Crate : Pick(Goods), false);
            if (Chance(0.5)) for (int k = 0; k < 4; k++) Blob(P(a + Range(-0.18f, 0.18f), 0.48f, -0.45f + Range(-0.12f, 0.12f)), rot, Vector3.one * 0.16f, Pick(Fruits), false);
        }
    }

    private void SideSign(Vector3 p, Vector3 u, Vector3 vin)
    {
        // Biển dọc vuông góc mặt tiền ("NHÀ NGHỈ", "KHÁCH SẠN"...), đọc được khi đi dọc phố
        string[] texts = { "NHÀ\nNGHỈ", "KHÁCH\nSẠN", "SPA\nNAIL", "NHA\nKHOA", "KARA\nOKE", "TIẾNG\nANH", "CẦM\nĐỒ" };
        Color c = Pick(SignColors);
        Quaternion r = Quaternion.LookRotation(u);
        Box(p + Vector3.up * 1.3f, r, new Vector3(0.8f, 2.6f, 0.12f), c, false);
        Box(p + Vector3.up * 1.3f + vin * 0.42f, Quaternion.LookRotation(vin), new Vector3(0.05f, 0.05f, 0.5f), Rail, false);
        string t = Pick(texts);
        Color tc = c.grayscale > 0.6f ? new Color(0.7f, 0.05f, 0.05f) : new Color(1f, 0.95f, 0.6f);
        Sign(p + Vector3.up * 1.3f - u * 0.07f, u, new Vector2(0.7f, 2.4f), t, tc);
        Sign(p + Vector3.up * 1.3f + u * 0.07f, -u, new Vector2(0.7f, 2.4f), t, tc);
    }

    private void Billboard(Vector3 p, Vector3 vin, float w)
    {
        string[] ads =
        {
            "TRÀ XANH SEN VÀNG", "MÌ TÔM GIA ĐÌNH", "DẦU ĂN BÔNG LÚA", "XE MÁY THĂNG LONG", "SƠN NHÀ BỐN MÙA",
            "SỮA ĐẬU NÀNH HÀ THÀNH", "NƯỚC SUỐI LAM SƠN", "BÁNH CỐM HÀNG THAN", "ĐIỆN MÁY PHỐ CỔ",
        };
        Quaternion r = Quaternion.LookRotation(vin);
        float bw = Mathf.Min(w, 9f);
        Box(p + Vector3.up * 0.9f + Vector3.Cross(Vector3.up, vin) * (bw * 0.4f), r, new Vector3(0.12f, 1.8f, 0.12f), Rail, false);
        Box(p + Vector3.up * 0.9f - Vector3.Cross(Vector3.up, vin) * (bw * 0.4f), r, new Vector3(0.12f, 1.8f, 0.12f), Rail, false);
        Color c = Pick(SignColors);
        Box(p + Vector3.up * 2.9f, r, new Vector3(bw, 2.4f, 0.15f), c, true);
        Sign(p + Vector3.up * 2.9f - vin * 0.09f, vin, new Vector2(bw - 0.4f, 2.0f), Pick(ads), c.grayscale > 0.6f ? new Color(0.7f, 0.05f, 0.05f) : White);
    }

    // ================= Biệt thự Pháp =================
    private void Villa(Vector3 o, Vector3 u, Vector3 vin, float w, float d, string address)
    {
        Quaternion rot = Quaternion.LookRotation(vin, Vector3.up);
        Vector3 P(float a, float y, float i) => o + u * a + Vector3.up * y + vin * i;
        int floors = 2 + rng.Next(0, 2);
        float set = 3.2f, dd = Mathf.Max(7f, d - set - 0.5f), h = floors * Floor + 0.5f;
        Color wall = Chance(0.75) ? FrenchYellow : Cream;
        Box(P(w * 0.5f, h * 0.5f, set + dd * 0.5f), rot, new Vector3(w, h, dd), wall, true);
        Box(P(w * 0.5f, 0.3f, set - 0.03f), rot, new Vector3(w + 0.1f, 0.6f, 0.1f), Stone, false);
        HouseCollider(P(w * 0.5f, h * 0.5f, set + dd * 0.5f), rot, new Vector3(w, h, dd), address, P(w * 0.5f, 0f, -1.3f));
        for (int f = 0; f < floors; f++)
        {
            float y = f * Floor + 0.5f;
            Box(P(w * 0.5f, y + Floor - 0.05f, set - 0.06f), rot, new Vector3(w, 0.16f, 0.14f), White, false);
            for (float a = 1.5f; a < w - 1.1f; a += 2.6f)
            {
                Box(P(a, y + 1.45f, set - 0.02f), rot, new Vector3(1.0f, 2.0f, 0.04f), Glass, false);
                Box(P(a, y + 2.55f, set - 0.04f), rot, new Vector3(1.2f, 0.18f, 0.08f), White, false);
                Box(P(a - 0.75f, y + 1.45f, set - 0.05f), rot * Quaternion.Euler(0f, -30f, 0f), new Vector3(0.5f, 2.0f, 0.04f), Shutter, false);
                Box(P(a + 0.75f, y + 1.45f, set - 0.05f), rot * Quaternion.Euler(0f, 30f, 0f), new Vector3(0.5f, 2.0f, 0.04f), Shutter, false);
            }
        }
        // Ban công giữa tầng 2 có lan can con tiện
        Box(P(w * 0.5f, Floor + 0.55f, set - 0.6f), rot, new Vector3(3.2f, 0.15f, 1.2f), White, true);
        for (float a = -1.45f; a <= 1.45f; a += 0.29f) Prism(P(w * 0.5f + a, Floor + 0.95f, set - 1.15f), rot, new Vector3(0.1f, 0.7f, 0.1f), White, false);
        Box(P(w * 0.5f, Floor + 1.33f, set - 1.15f), rot, new Vector3(3.2f, 0.08f, 0.14f), White, false);
        // Mái ngói đỏ dốc + ống khói
        float half = dd * 0.5f;
        Box(P(w * 0.5f, h + 1.0f, set + half * 0.5f), rot * Quaternion.Euler(-32f, 0f, 0f), new Vector3(w + 0.5f, 0.2f, half * 1.22f), Tile, true);
        Box(P(w * 0.5f, h + 1.0f, set + half * 1.5f), rot * Quaternion.Euler(32f, 0f, 0f), new Vector3(w + 0.5f, 0.2f, half * 1.22f), Tile, true);
        Box(P(w * 0.25f, h + 1.9f, set + half * 1.3f), rot, new Vector3(0.6f, 1.6f, 0.6f), Brick, true);
        // Hàng rào thấp, cổng sắt, sân trước có cây
        Box(P(w * 0.2f, 0.55f, 0.1f), rot, new Vector3(w * 0.36f, 1.1f, 0.18f), White, true);
        Box(P(w * 0.8f, 0.55f, 0.1f), rot, new Vector3(w * 0.36f, 1.1f, 0.18f), White, true);
        Box(P(w * 0.38f, 0.85f, 0.1f), rot, new Vector3(0.35f, 1.7f, 0.35f), White, true);
        Box(P(w * 0.62f, 0.85f, 0.1f), rot, new Vector3(0.35f, 1.7f, 0.35f), White, true);
        for (float a = w * 0.4f; a < w * 0.6f; a += 0.16f) Box(P(a, 0.75f, 0.1f), rot, new Vector3(0.03f, 1.4f, 0.03f), Rail, false);
        if (Chance(0.6)) PlantTree(P(w * 0.15f, 0f, set * 0.5f), Range(0.75f, 0.95f), false, true);
        if (Chance(0.3))
        {
            // Biệt thự thành quán cà phê sân vườn
            Sign(P(w * 0.5f, 2.6f, 0.0f), vin, new Vector2(3.4f, 0.6f), Pick(new[] { "CÀ PHÊ PHỐ CŨ", "CAFÉ BIỆT THỰ", "TRÀ CHIỀU 1930" }), new Color(0.35f, 0.18f, 0.08f));
            Box(P(w * 0.5f, 2.6f, 0.06f), rot, new Vector3(3.6f, 0.7f, 0.06f), Cream, false);
            for (int k = 0; k < 2; k++) Umbrella(P(w * (0.3f + 0.4f * k), 0f, set * 0.55f));
        }
        if (Chance(0.4)) Motorbike(o + u * (w * 0.5f) - vin * 1.6f, -vin, Pick(Bikes));
    }

    // ================= Ki-ốt (dãy hàng 1 - 2 tầng ở chân khu tập thể / trường / chợ) =================
    private void Kiosk(Vector3 o, Vector3 u, Vector3 vin, float w, float d, string address, Line street)
    {
        Quaternion rot = Quaternion.LookRotation(vin, Vector3.up);
        Vector3 P(float a, float y, float i) => o + u * a + Vector3.up * y + vin * i;
        float h = Chance(0.3) ? 5.8f : 3.1f;
        Color facade = Pick(Facades);
        Box(P(w * 0.5f, h * 0.5f, d * 0.5f + 0.5f), rot, new Vector3(w - 0.06f, h, d - 1f), facade, true);
        HouseCollider(P(w * 0.5f, h * 0.5f, d * 0.5f), rot, new Vector3(w, h, d), address, P(w * 0.5f, 0f, -1.2f));
        string trade = Trade(street.id, Cell.Apartments, false);
        Box(P(w * 0.5f, 1.3f, 0.48f), rot, new Vector3(w - 0.4f, 2.4f, 0.02f), Interior, false);
        for (int k = 0; k < 6; k++) Box(P(Range(0.4f, w - 0.4f), Range(0.3f, 1.8f), 0.7f), rot, new Vector3(0.4f, 0.35f, 0.3f), Pick(Goods), false);
        Color sc = Pick(SignColors);
        Box(P(w * 0.5f, 2.75f, 0.35f), rot, new Vector3(w - 0.2f, 0.6f, 0.1f), sc, true);
        Sign(P(w * 0.5f, 2.75f, 0.28f), vin, new Vector2(w - 0.4f, 0.5f), trade, sc.grayscale > 0.6f ? new Color(0.75f, 0.05f, 0.05f) : new Color(1f, 0.96f, 0.7f));
        Box(P(w * 0.5f, 2.35f, -0.4f), rot * Quaternion.Euler(-12f, 0f, 0f), new Vector3(w - 0.1f, 0.04f, 1.6f), Pick(Awnings), true);
        if (IsFood(trade)) FoodSet(o + u * (w * 0.5f) - vin * 1.6f, u, vin, Chance(0.7));
        else if (Chance(0.5)) Motorbike(o + u * (w * 0.5f) - vin * 1.1f, Quaternion.AngleAxis(-25f, Vector3.up) * -vin, Pick(Bikes));
    }

    // ================= Khu tập thể =================
    private void BuildApartments(Rect r)
    {
        float x0 = r.xMin + Walk + 7f, x1 = r.xMax - Walk - 7f, z0 = r.yMin + Walk + 7f, z1 = r.yMax - Walk - 7f;
        float len = Mathf.Min(x1 - x0 - 4f, 54f);
        if (len < 20f) return;
        float depth = 10f, yard = 15f;
        int count = Mathf.Clamp((int)((z1 - z0 + yard) / (depth + yard)), 1, 2);
        float total = count * depth + (count - 1) * yard;
        float zStart = (z0 + z1) * 0.5f - total * 0.5f + depth * 0.5f;
        int block = 1 + rng.Next(0, 8);
        for (int b = 0; b < count; b++)
        {
            Vector3 c = new Vector3((x0 + x1) * 0.5f, LotTop, zStart + b * (depth + yard));
            reserved.Add(new Bounds(c + Vector3.up * 8f, new Vector3(len + 2f, 18f, depth + 4f)));
            Apartment(c, len, depth, block++);
        }
        // Sân chung: cây, chỗ để xe có mái tôn, bàn cờ tướng, người
        for (int n = 0; n < 10; n++)
        {
            Vector3 p = new Vector3(Range(x0, x1), LotTop, Range(z0, z1));
            if (Overlaps(reserved, new Bounds(p + Vector3.up * 2f, new Vector3(3f, 4f, 3f)))) continue;
            switch (n % 4)
            {
                case 0: PlantTree(p, Range(0.9f, 1.2f), false); break;
                case 1: BikeShed(p); break;
                case 2: ChessGame(p); break;
                default: Person(p, Rot(rng.Next(360)) * Vector3.forward, 0, 0); break;
            }
        }
    }

    private void Apartment(Vector3 c, float len, float depth, int n)
    {
        int floors = 5;
        float h = floors * Floor;
        Color wall = Pick(KttColors);
        Box(c + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(len, h, depth), wall, true);
        AddCollider(c + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(len, h, depth), $"Nha A{n}");
        Box(c + Vector3.up * (h - 0.6f), Quaternion.identity, new Vector3(len + 0.02f, 1.2f, depth + 0.02f), Color.Lerp(wall, Dark, 0.15f), false);
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 face = new Vector3(0f, 0f, side);
            Quaternion r = Quaternion.LookRotation(-face);
            for (int f = 0; f < floors; f++)
            {
                float y = f * Floor;
                for (float a = -len * 0.5f + 1.8f; a < len * 0.5f - 1.2f; a += 3.6f)
                {
                    Vector3 p = c + new Vector3(a, y, side * depth * 0.5f);
                    Box(p + Vector3.up * 1.6f + face * 0.02f, r, new Vector3(1.4f, 1.4f, 0.04f), Glass, false);
                    if (f > 0 && Chance(0.55))
                    {
                        // Chuồng cọp cơi nới
                        Box(p + Vector3.up * 0.08f + face * 0.5f, r, new Vector3(2.8f, 0.14f, 1.0f), Slab, true);
                        for (float b = -1.35f; b <= 1.35f; b += 0.3f) Box(p + new Vector3(b, 1.35f, 0f) + face * 1.0f, r, new Vector3(0.03f, 2.5f, 0.03f), Rail, false);
                        Box(p + Vector3.up * 2.65f + face * 0.5f, r, new Vector3(3.0f, 0.06f, 1.1f), Pick(Roofs), false);
                        if (Chance(0.5)) for (float b = -1.1f; b < 1.2f; b += 0.4f) Box(p + new Vector3(b, 1.9f, 0f) + face * 0.7f, r, new Vector3(0.3f, 0.6f, 0.02f), Pick(Clothes), false);
                        if (Chance(0.4)) PlantPot(p + Vector3.up * 0.15f + face * 0.8f);
                    }
                    if (Chance(0.3)) Box(p + new Vector3(1.0f, 2.4f, 0f) + face * 0.2f, r, new Vector3(0.75f, 0.5f, 0.3f), White, false);
                }
            }
        }
        // Lồng cầu thang + cửa vào, địa chỉ giao hàng mỗi đơn nguyên
        int unit = 1;
        for (float a = -len * 0.5f + 9f; a < len * 0.5f - 5f; a += 18f)
        {
            Vector3 core = c + new Vector3(a, 0f, depth * 0.5f + 0.6f);
            Box(core + Vector3.up * (h + 1.2f) * 0.5f, Quaternion.identity, new Vector3(3.2f, h + 1.2f, 1.2f), Color.Lerp(wall, White, 0.2f), true);
            for (int f = 0; f < floors; f++) Box(core + new Vector3(0f, f * Floor + 2.0f, 0.62f), Quaternion.identity, new Vector3(0.6f, 1.2f, 0.04f), Glass, false);
            Box(core + new Vector3(0f, 1.1f, 0.62f), Quaternion.identity, new Vector3(1.4f, 2.2f, 0.04f), Dark, false);
            Address($"Nha A{n} - Don nguyen {unit} - Tap the Kim Lien", core + new Vector3(0f, 0f, 1.8f), core + Vector3.up * 2f);
            unit++;
        }
        Sign(c + new Vector3(len * 0.5f + 0.03f, h - 2.2f, 0f), Vector3.left, new Vector2(4f, 2.2f), $"A{n}", new Color(0.55f, 0.12f, 0.08f));
        for (int k = 0; k < 4; k++)
        {
            Vector3 tp = c + new Vector3(Range(-len * 0.4f, len * 0.4f), h + 0.6f, Range(-2f, 2f));
            Prism(tp, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.9f, 1.6f, 0.9f), Steel, true);
        }
    }

    private void BikeShed(Vector3 p)
    {
        Box(p + Vector3.up * 2.3f, Quaternion.identity, new Vector3(6f, 0.06f, 2.6f), Pick(Roofs), true);
        for (int k = 0; k < 4; k++) Box(p + new Vector3(k < 2 ? -2.9f : 2.9f, 1.15f, k % 2 == 0 ? -1.2f : 1.2f), Quaternion.identity, new Vector3(0.08f, 2.3f, 0.08f), Rail, false);
        for (float a = -2.4f; a <= 2.4f; a += 0.8f) Motorbike(p + new Vector3(a, 0f, 0f), Vector3.forward, Pick(Bikes));
    }

    // ================= Sân trong / vườn =================
    private void Courtyard(Rect r, float depth)
    {
        for (int n = 0; n < 8; n++)
        {
            Vector3 p = new Vector3(Range(r.xMin + Walk + depth + 3f, r.xMax - Walk - depth - 3f), LotTop, Range(r.yMin + Walk + depth + 3f, r.yMax - Walk - depth - 3f));
            if (n < 4) PlantTree(p, Range(0.9f, 1.2f), false);
            else Box(p + Vector3.up * 2.5f, Quaternion.Euler(0f, rng.Next(0, 4) * 90f, 0f), new Vector3(Range(5f, 10f), 5f, Range(5f, 9f)), Pick(Facades), true);
        }
    }

    private void Garden(Rect r, float margin)
    {
        for (int n = 0; n < 12; n++)
        {
            Vector3 p = new Vector3(Range(r.xMin + margin, r.xMax - margin), LotTop, Range(r.yMin + margin, r.yMax - margin));
            if (Overlaps(reserved, new Bounds(p + Vector3.up * 3f, new Vector3(3f, 6f, 3f)))) continue;
            PlantTree(p, Range(0.9f, 1.3f), false, Chance(0.4));
        }
    }

    // ================= Nhà hát Lớn =================
    private void BuildOperaHouse(Rect r, Edge front)
    {
        Vector3 c = new Vector3(r.center.x, LotTop, r.yMax - Walk - 14f);
        reserved.Add(new Bounds(c + Vector3.up * 10f, new Vector3(46f, 24f, 28f)));
        Color wall = new Color(0.96f, 0.89f, 0.7f);
        Box(c + Vector3.up * 7f, Quaternion.identity, new Vector3(38f, 14f, 18f), wall, true);
        Box(c + Vector3.up * 16f, Quaternion.identity, new Vector3(16f, 4f, 12f), wall, true);
        Box(c + Vector3.up * 18.6f, Quaternion.identity, new Vector3(12f, 1.4f, 8f), new Color(0.35f, 0.47f, 0.52f), true);
        Blob(c + Vector3.up * 20.2f, Quaternion.identity, new Vector3(5f, 3.4f, 5f), new Color(0.35f, 0.47f, 0.52f), true);
        Box(c + new Vector3(-15f, 15.5f, 5f), Quaternion.identity, new Vector3(5f, 3f, 5f), wall, true);
        Box(c + new Vector3(15f, 15.5f, 5f), Quaternion.identity, new Vector3(5f, 3f, 5f), wall, true);
        for (int i = 0; i < 8; i++) Prism(c + new Vector3(-10.5f + i * 3f, 5.3f, 9.8f), Quaternion.identity, new Vector3(0.9f, 7f, 0.9f), White, true);
        Box(c + new Vector3(0f, 9.5f, 10f), Quaternion.identity, new Vector3(25f, 1.4f, 1.4f), White, true);
        Box(c + new Vector3(0f, 11.3f, 10f), Quaternion.Euler(0f, 0f, 0f), new Vector3(16f, 2.2f, 0.8f), White, true);
        for (int s = 0; s < 4; s++) Box(c + new Vector3(0f, 0.12f + s * 0.24f, 12.6f - s * 0.6f), Quaternion.identity, new Vector3(26f - s, 0.24f, 1.2f), Stone, false);
        for (int f = 0; f < 3; f++)
            for (int i = 0; i < 10; i++) Box(c + new Vector3(-17f + i * 3.8f, 3f + f * 4f, -9.03f), Quaternion.identity, new Vector3(1.4f, 2.2f, 0.04f), Glass, false);
        Sign(c + new Vector3(0f, 11.3f, 10.42f), Vector3.back, new Vector2(15f, 1.6f), "NHÀ HÁT LỚN HÀ NỘI", new Color(0.4f, 0.25f, 0.1f));
        HouseCollider(c + Vector3.up * 7f, Quaternion.identity, new Vector3(38f, 14f, 18f), "Nha Hat Lon, " + front.line.id, c + new Vector3(0f, 0f, 14f));
        // Quảng trường trước nhà hát + đài phun nước
        Box(c + new Vector3(0f, 0.012f, 16f), Quaternion.identity, new Vector3(36f, 0.02f, 8f), Stone, false);
        for (int k = 0; k < 6; k++) Person(c + new Vector3(Range(-14f, 14f), 0f, Range(13f, 18f)), Rot(rng.Next(360)) * Vector3.forward, 0, 0);
    }

    // ================= Chợ Đồng Xuân =================
    private void BuildMarket(Rect r, Edge front)
    {
        float w = Mathf.Min(r.width - Walk * 2f - 14f, 60f), d = Mathf.Min(r.height - Walk * 2f - 20f, 38f);
        Vector3 c = new Vector3(r.center.x, LotTop, r.center.y + 3f);
        reserved.Add(new Bounds(c + Vector3.up * 10f, new Vector3(w + 2f, 22f, d + 14f)));
        Color wall = new Color(0.92f, 0.87f, 0.72f);
        Box(c + Vector3.up * 6.5f, Quaternion.identity, new Vector3(w, 13f, d), wall, true);
        Box(c + Vector3.up * 13.2f, Quaternion.identity, new Vector3(w + 0.4f, 0.4f, d + 0.4f), new Color(0.42f, 0.42f, 0.44f), true);
        Vector3 f = c - Vector3.forward * (d * 0.5f);
        Box(f + new Vector3(0f, 9f, -0.8f), Quaternion.identity, new Vector3(14f, 18f, 1.6f), wall, true);
        Box(f + new Vector3(0f, 18.4f, -0.8f), Quaternion.identity, new Vector3(9f, 0.8f, 1.8f), new Color(0.42f, 0.42f, 0.44f), true);
        for (int i = 0; i < 5; i++)
        {
            float x = -w * 0.36f + i * w * 0.18f;
            Box(f + new Vector3(x, 2.5f, -0.02f - (i == 2 ? 1.6f : 0f)), Quaternion.identity, new Vector3(4.4f, 5f, 0.06f), Dark, false);
            Blob(f + new Vector3(x, 5f, -0.03f - (i == 2 ? 1.6f : 0f)), Quaternion.identity, new Vector3(4.4f, 2.2f, 0.05f), Dark, false);
        }
        for (int i = 0; i < 11; i++) Box(f + new Vector3(-w * 0.45f + i * w * 0.09f, 9.5f, -0.02f), Quaternion.identity, new Vector3(2.2f, 2.6f, 0.05f), Glass, false);
        Sign(f + new Vector3(0f, 15.6f, -1.65f), Vector3.forward, new Vector2(12.5f, 2f), "CHỢ ĐỒNG XUÂN", new Color(0.72f, 0.08f, 0.06f));
        Sign(f + new Vector3(0f, 13.6f, -1.65f), Vector3.forward, new Vector2(4f, 1f), "1889", new Color(0.72f, 0.08f, 0.06f));
        HouseCollider(c + Vector3.up * 6.5f, Quaternion.identity, new Vector3(w, 13f, d), "Cho Dong Xuan, " + front.line.id, f + new Vector3(0f, 0f, -4.5f));
        // Sạp hàng trước chợ: ô dù, thúng mủng, người bán người mua, xe máy dựng kín
        for (float x = -w * 0.45f; x < w * 0.45f; x += 3.2f)
        {
            Vector3 p = f + new Vector3(x, 0f, -4.4f);
            if (Mathf.Abs(x) < 3.5f) continue;
            Umbrella(p);
            Box(p + Vector3.up * 0.35f, Quaternion.identity, new Vector3(1.6f, 0.7f, 1.0f), Crate, false);
            for (int k = 0; k < 6; k++) Blob(p + new Vector3(Range(-0.65f, 0.65f), 0.8f, Range(-0.4f, 0.4f)), Quaternion.identity, Vector3.one * 0.22f, Pick(Fruits), false);
            if (Chance(0.7)) Person(p + new Vector3(0f, 0f, 1.0f), Vector3.back, 2, Chance(0.6) ? 1 : 0);
            if (Chance(0.5)) Person(p + new Vector3(Range(-0.6f, 0.6f), 0f, -1.4f), Vector3.forward, 0, Chance(0.3) ? 1 : 0);
        }
        for (float x = -w * 0.45f; x < w * 0.45f; x += 0.85f) if (Chance(0.6)) Motorbike(f + new Vector3(x, 0f, -6.6f), Vector3.back, Pick(Bikes));
    }

    // ================= Trường học =================
    private void BuildSchool(Rect r, Edge front)
    {
        float len = Mathf.Min(r.width - Walk * 2f - 16f, 52f);
        Vector3 c = new Vector3(r.center.x, LotTop, r.yMax - Walk - 30f);
        reserved.Add(new Bounds(new Vector3(c.x, 8f, c.z + 10f), new Vector3(len + 4f, 20f, 36f)));
        Color wall = new Color(0.98f, 0.84f, 0.42f);
        float h = 3 * 3.6f;
        Box(c + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(len, h, 9f), wall, true);
        Box(c + new Vector3(-len * 0.5f + 4.5f, h * 0.5f, 9f), Quaternion.identity, new Vector3(9f, h, 9f), wall, true);
        Box(c + new Vector3(len * 0.5f - 4.5f, h * 0.5f, 9f), Quaternion.identity, new Vector3(9f, h, 9f), wall, true);
        AddCollider(c + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(len, h, 9f), "School");
        AddCollider(c + new Vector3(-len * 0.5f + 4.5f, h * 0.5f, 9f), Quaternion.identity, new Vector3(9f, h, 9f), "SchoolW");
        AddCollider(c + new Vector3(len * 0.5f - 4.5f, h * 0.5f, 9f), Quaternion.identity, new Vector3(9f, h, 9f), "SchoolE");
        Box(c + Vector3.up * (h + 1.4f), Quaternion.Euler(-25f, 0f, 0f), new Vector3(len + 1f, 0.2f, 5.6f), Tile, true);
        for (int f = 0; f < 3; f++)
        {
            float y = f * 3.6f;
            Box(c + new Vector3(0f, y + 0.1f, 5.2f), Quaternion.identity, new Vector3(len - 18f, 0.2f, 1.6f), Slab, true);
            Box(c + new Vector3(0f, y + 1.1f, 6f), Quaternion.identity, new Vector3(len - 18f, 0.9f, 0.08f), White, false);
            for (float a = -len * 0.5f + 11f; a < len * 0.5f - 10f; a += 3.4f)
                Box(c + new Vector3(a, y + 1.8f, 4.52f), Quaternion.identity, new Vector3(1.6f, 1.6f, 0.04f), Glass, false);
        }
        Sign(c + new Vector3(0f, h - 1.2f, 6.1f), Vector3.back, new Vector2(18f, 1.4f), "TRƯỜNG THCS TRƯNG VƯƠNG", new Color(0.7f, 0.06f, 0.05f));
        // Sân trường, cột cờ, cây, hàng rào + cổng
        Vector3 pole = c + new Vector3(0f, 0f, 16f);
        Box(pole + Vector3.up * 0.15f, Quaternion.identity, new Vector3(2.4f, 0.3f, 2.4f), Stone, false);
        Prism(pole + Vector3.up * 5f, Quaternion.identity, new Vector3(0.14f, 10f, 0.14f), Steel, true);
        BigFlag(pole + Vector3.up * 9.2f, Vector3.right);
        for (int k = 0; k < 6; k++) PlantTree(c + new Vector3(-len * 0.4f + k * len * 0.16f, 0f, 21f), Range(0.9f, 1.15f), false);
        float fz = r.yMax - Walk - 0.3f;
        Box(new Vector3(r.center.x - len * 0.3f - 2f, LotTop + 0.8f, fz), Quaternion.identity, new Vector3(len * 0.6f, 1.6f, 0.12f), Rail, false);
        Box(new Vector3(r.center.x + len * 0.3f + 2f, LotTop + 0.8f, fz), Quaternion.identity, new Vector3(len * 0.6f, 1.6f, 0.12f), Rail, false);
        Box(new Vector3(r.center.x, LotTop + 3.3f, fz), Quaternion.identity, new Vector3(7f, 0.8f, 0.2f), new Color(0.7f, 0.06f, 0.05f), false);
        Sign(new Vector3(r.center.x, LotTop + 3.3f, fz + 0.12f), Vector3.back, new Vector2(6.6f, 0.65f), "TRƯỜNG THCS TRƯNG VƯƠNG", new Color(1f, 0.88f, 0.2f));
        Address("Truong THCS Trung Vuong, " + front.line.id, new Vector3(r.center.x, LotTop, fz + 1.5f), c);
        for (int k = 0; k < 10; k++) Person(c + new Vector3(Range(-len * 0.4f, len * 0.4f), 0f, Range(12f, 22f)), Rot(rng.Next(360)) * Vector3.forward, 0, 0, true);
    }

    // ================= Chùa Trấn Quốc =================
    private void BuildPagoda(Rect r, Edge front)
    {
        Vector3 c = new Vector3(r.center.x, LotTop, r.center.y + 4f);
        float pw = Mathf.Min(r.width - 34f, 34f), pd = Mathf.Min(r.height - 34f, 26f);
        reserved.Add(new Bounds(c + Vector3.up * 8f, new Vector3(pw + 10f, 20f, r.height - Walk * 2f - 20f)));
        // Ao sen + đảo tháp
        Water(c + Vector3.up * 0.02f, pw * 0.5f, pd * 0.5f);
        for (int k = 0; k < 18; k++) Prism(c + new Vector3(Range(-pw * 0.45f, pw * 0.45f), 0.05f, Range(-pd * 0.45f, pd * 0.45f)), Quaternion.identity, new Vector3(0.7f, 0.03f, 0.7f), Plant, false);
        Vector3 t = c + new Vector3(0f, 0f, 2f);
        Box(t + Vector3.up * 0.25f, Quaternion.identity, new Vector3(9f, 0.5f, 9f), Stone, true);
        Color brick = new Color(0.62f, 0.24f, 0.16f);
        float y = 0.5f;
        for (int tier = 0; tier < 11; tier++)
        {
            float s = 3.6f - tier * 0.2f, th = 1.25f;
            Box(t + Vector3.up * (y + th * 0.5f), Quaternion.identity, new Vector3(s, th, s), brick, true);
            Box(t + Vector3.up * (y + th - 0.08f), Quaternion.identity, new Vector3(s + 0.5f, 0.16f, s + 0.5f), Color.Lerp(brick, Dark, 0.3f), false);
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
                Box(t + Vector3.up * (y + th * 0.5f) + q * new Vector3(0f, 0f, s * 0.5f + 0.01f), q, new Vector3(0.45f, 0.65f, 0.02f), Dark, false);
            }
            y += th;
        }
        Cone(t + Vector3.up * (y + 0.9f), Quaternion.identity, new Vector3(1.4f, 1.8f, 1.4f), Gold, true);
        Blob(t + Vector3.up * (y + 2f), Quaternion.identity, Vector3.one * 0.6f, Gold, false);
        AddCollider(t + Vector3.up * 3f, Quaternion.identity, new Vector3(4f, 6f, 4f), "Thap Tran Quoc");
        // Chính điện mái cong
        Vector3 hall = c + new Vector3(0f, 0f, pd * 0.5f + 5f);
        TempleHall(hall, 14f, 7f, new Color(0.62f, 0.12f, 0.08f));
        // Tam quan ở mặt phố
        Vector3 gate = new Vector3(c.x, LotTop, r.yMax - Walk - 1.5f);
        foreach (float gx in new[] { -4.5f, 0f, 4.5f })
        {
            float gh = gx == 0f ? 5f : 3.8f;
            Box(gate + new Vector3(gx - 1.3f, gh * 0.5f, 0f), Quaternion.identity, new Vector3(0.6f, gh, 0.6f), new Color(0.62f, 0.12f, 0.08f), true);
            Box(gate + new Vector3(gx + 1.3f, gh * 0.5f, 0f), Quaternion.identity, new Vector3(0.6f, gh, 0.6f), new Color(0.62f, 0.12f, 0.08f), true);
            CurvedRoof(gate + new Vector3(gx, gh, 0f), 3.6f, 1.8f);
        }
        Sign(gate + new Vector3(0f, 4.1f, -0.33f), Vector3.forward, new Vector2(2.4f, 0.8f), "CHÙA\nTRẤN QUỐC", new Color(1f, 0.85f, 0.2f));
        Address("Chua Tran Quoc, " + front.line.id, gate + new Vector3(0f, 0f, 2.5f), hall);
        // Lư hương, cây bồ đề, người đi lễ
        Box(c + new Vector3(0f, 0.6f, pd * 0.5f + 10.2f), Quaternion.identity, new Vector3(1f, 1.2f, 1f), Gold, false);
        for (int k = 0; k < 6; k++) PlantTree(c + new Vector3(k < 3 ? -pw * 0.5f - 4f : pw * 0.5f + 4f, 0f, -pd * 0.4f + (k % 3) * pd * 0.4f), Range(1.1f, 1.4f), false, k % 2 == 0);
        for (int k = 0; k < 5; k++) Person(c + new Vector3(Range(-5f, 5f), 0f, pd * 0.5f + Range(10.8f, 12f)), Vector3.back, 0, 0);
    }

    private void TempleHall(Vector3 p, float w, float d, Color wall)
    {
        Box(p + Vector3.up * 0.3f, Quaternion.identity, new Vector3(w + 1.5f, 0.6f, d + 1.5f), Stone, false);
        Box(p + Vector3.up * 2.4f, Quaternion.identity, new Vector3(w, 3.6f, d), wall, true);
        foreach (float side in new[] { -1f, 1f })
        {
            for (float a = -w * 0.5f + 1f; a <= w * 0.5f - 0.9f; a += 2.4f) Prism(p + new Vector3(a, 2.4f, side * (d * 0.5f + 0.5f)), Quaternion.identity, new Vector3(0.35f, 3.6f, 0.35f), wall, true);
            Box(p + new Vector3(0f, 2.1f, side * (d * 0.5f + 0.01f)), Quaternion.identity, new Vector3(2.2f, 2.8f, 0.04f), Gold, false);
        }
        CurvedRoof(p + Vector3.up * 4.2f, w + 2f, d + 2f);
        CurvedRoof(p + Vector3.up * 5.6f, w * 0.7f, d * 0.75f);
        AddCollider(p + Vector3.up * 2.4f, Quaternion.identity, new Vector3(w, 3.6f, d), "Temple");
    }

    /// <summary>Mái ngói hai dốc kiểu đình chùa, bốn góc đao cong vút.</summary>
    private void CurvedRoof(Vector3 p, float w, float d)
    {
        Box(p + new Vector3(0f, 0.55f, -d * 0.25f), Quaternion.Euler(28f, 0f, 0f), new Vector3(w, 0.18f, d * 0.6f), TempleRoof, true);
        Box(p + new Vector3(0f, 0.55f, d * 0.25f), Quaternion.Euler(-28f, 0f, 0f), new Vector3(w, 0.18f, d * 0.6f), TempleRoof, true);
        Box(p + Vector3.up * 0.95f, Quaternion.identity, new Vector3(w * 0.9f, 0.3f, 0.3f), TempleRoof, false);
        for (int s = 0; s < 4; s++)
        {
            float sx = s < 2 ? -1f : 1f, sz = s % 2 == 0 ? -1f : 1f;
            Box(p + new Vector3(sx * w * 0.5f, 0.35f, sz * d * 0.45f), Quaternion.Euler(0f, 0f, -sx * 35f), new Vector3(0.9f, 0.14f, 0.2f), TempleRoof, false);
        }
        Blob(p + new Vector3(-w * 0.45f, 1.15f, 0f), Quaternion.identity, new Vector3(0.4f, 0.5f, 0.2f), Gold, false);
        Blob(p + new Vector3(w * 0.45f, 1.15f, 0f), Quaternion.identity, new Vector3(0.4f, 0.5f, 0.2f), Gold, false);
    }

    // ================= Công viên =================
    private void BuildPark(Rect r)
    {
        Vector3 c = new Vector3(r.center.x, LotTop, r.center.y);
        float iw = r.width - Walk * 2f, ih = r.height - Walk * 2f;
        // Lối đi chữ thập + vòng quanh hồ nhỏ (người đi bộ đi được)
        Box(c + Vector3.up * 0.012f, Quaternion.identity, new Vector3(iw, 0.02f, 3f), Path, false);
        Box(c + Vector3.up * 0.012f, Quaternion.identity, new Vector3(3f, 0.02f, ih), Path, false);
        MarkVolume(c, new Vector3(iw + 1f, 1.5f, 3f));
        MarkVolume(c, new Vector3(3f, 1.5f, ih + 1f));
        for (float t = -0.4f; t <= 0.41f; t += 0.2f) { WalkPoints.Add(c + new Vector3(iw * t, 0f, 0f)); WalkPoints.Add(c + new Vector3(0f, 0f, ih * t)); }
        Vector3 pond = c + new Vector3(iw * 0.22f, 0f, ih * 0.22f);
        Water(pond + Vector3.up * 0.02f, 9f, 7f);
        // Thuỷ tạ
        Vector3 pav = pond + new Vector3(-4f, 0f, 0f);
        for (int k = 0; k < 4; k++) Box(pav + new Vector3(k < 2 ? -1.6f : 1.6f, 1.4f, k % 2 == 0 ? -1.6f : 1.6f), Quaternion.identity, new Vector3(0.25f, 2.8f, 0.25f), new Color(0.62f, 0.12f, 0.08f), true);
        Box(pav + Vector3.up * 0.3f, Quaternion.identity, new Vector3(4f, 0.2f, 4f), WoodBrown, false);
        CurvedRoof(pav + Vector3.up * 2.8f, 4.6f, 4.6f);
        // Rừng cây lá vàng (bỏ chỗ lối đi và hồ)
        for (float x = r.xMin + Walk + 4f; x < r.xMax - Walk - 3f; x += 8.5f)
        {
            var row = new List<Vector3>();
            for (float z = r.yMin + Walk + 4f; z < r.yMax - Walk - 3f; z += 8.5f)
            {
                Vector3 p = new Vector3(x + Range(-1.5f, 1.5f), LotTop, z + Range(-1.5f, 1.5f));
                if (Mathf.Abs(p.x - c.x) < 3.5f || Mathf.Abs(p.z - c.z) < 3.5f) continue;
                if (Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(pond.x, 0f, pond.z)) < 11f) continue;
                PlantTree(p, Range(1.0f, 1.35f), false);
                row.Add(p);
            }
            if (row.Count >= 2) Leaves(row[0], row[row.Count - 1], 6.5f);
        }
        if (benchPrefab != null)
            for (float a = -iw * 0.4f; a < iw * 0.4f; a += 9f)
            {
                if (Mathf.Abs(a) < 4f) continue;
                Prop(benchPrefab, c + new Vector3(a, 0f, 2.2f), Quaternion.LookRotation(Vector3.back));
                Prop(benchPrefab, c + new Vector3(a, 0f, -2.2f), Quaternion.LookRotation(Vector3.forward));
            }
        // Người tập dưỡng sinh, ngồi ghế, đánh cờ, xe đạp bán hoa ở lối vào
        for (int k = 0; k < 8; k++) Person(c + new Vector3(-iw * 0.25f + (k % 4) * 1.6f, 0f, -ih * 0.25f + (k / 4) * 1.6f), Vector3.forward, 0, 0);
        ChessGame(c + new Vector3(iw * 0.25f, 0f, -ih * 0.2f));
        ChessGame(c + new Vector3(-iw * 0.3f, 0f, ih * 0.25f));
        FlowerBike(new Vector3(c.x + 3f, LotTop, r.yMax - Walk - 1.5f), Vector3.right);
        Box(new Vector3(c.x, LotTop + 3.4f, r.yMax - Walk - 0.4f), Quaternion.identity, new Vector3(8f, 0.9f, 0.2f), new Color(0.08f, 0.4f, 0.2f), false);
        Sign(new Vector3(c.x, LotTop + 3.4f, r.yMax - Walk - 0.28f), Vector3.back, new Vector2(7.6f, 0.75f), "CÔNG VIÊN THỐNG NHẤT", new Color(1f, 0.95f, 0.6f));
        Box(new Vector3(c.x - 4.2f, LotTop + 1.8f, r.yMax - Walk - 0.4f), Quaternion.identity, new Vector3(0.3f, 3.6f, 0.3f), Rail, false);
        Box(new Vector3(c.x + 4.2f, LotTop + 1.8f, r.yMax - Walk - 0.4f), Quaternion.identity, new Vector3(0.3f, 3.6f, 0.3f), Rail, false);
        Address("Cong vien Thong Nhat (cong chinh)", new Vector3(c.x, LotTop, r.yMax - Walk - 2f), c);
    }

    // ================= Hồ Hoàn Kiếm =================
    private void BuildLake(Rect r)
    {
        string name = "HoGuom";
        reserved.Clear();
        LotAndSidewalk(r, name, Grass);
        var edges = new[]
        {
            new Edge { a = new Vector3(r.xMin, 0f, r.yMin), b = new Vector3(r.xMax, 0f, r.yMin), n = Vector3.back, street = true, line = Z[4] },
            new Edge { a = new Vector3(r.xMax, 0f, r.yMax), b = new Vector3(r.xMin, 0f, r.yMax), n = Vector3.forward, street = true, line = Z[6] },
            new Edge { a = new Vector3(r.xMin, 0f, r.yMax), b = new Vector3(r.xMin, 0f, r.yMin), n = Vector3.left, street = true, line = X[4], trim = 1f },
            new Edge { a = new Vector3(r.xMax, 0f, r.yMin), b = new Vector3(r.xMax, 0f, r.yMax), n = Vector3.right, street = true, line = X[6], trim = 1f },
        };
        foreach (Edge e in edges) EdgeFurniture(e, Cell.Lake);

        Vector3 center = new Vector3(r.center.x, LotTop, r.center.y);
        float rx = r.width * 0.5f - 15f, rz = r.height * 0.5f - 14f;
        Water(center + Vector3.up * 0.02f, rx, rz);

        // Kè đá, đường dạo, cây (lá vàng + liễu rủ), ghế đá
        int segs = 72;
        var ke = new List<(Vector3 p, Quaternion q, float len)>();
        var ring = new List<Vector3>();
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs, am = (a0 + a1) * 0.5f;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * rx, 0f, Mathf.Sin(a0) * rz), p1 = center + new Vector3(Mathf.Cos(a1) * rx, 0f, Mathf.Sin(a1) * rz);
            Vector3 mid = (p0 + p1) * 0.5f, dir = p1 - p0;
            Quaternion q = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 outward = new Vector3(Mathf.Cos(am) / rx, 0f, Mathf.Sin(am) / rz).normalized;
            ke.Add((mid, q, dir.magnitude + 0.1f));
            AddCollider(mid + Vector3.up * 0.6f, q, new Vector3(0.5f, 1.2f, dir.magnitude + 0.1f), "Lake_Edge");
            Box(mid + outward * 2.4f + Vector3.up * 0.012f, q, new Vector3(3.6f, 0.02f, dir.magnitude + 0.7f), Path, false);
            if (i % 3 == 0) { if (Chance(0.4)) Willow(mid + outward * 0.9f); else PlantTree(mid + outward * 5.4f, Range(1.1f, 1.4f), false); ring.Add(mid + outward * 5f); }
            else if (i % 3 == 1 && benchPrefab != null) Prop(benchPrefab, mid + outward * 4.4f, Quaternion.LookRotation(-outward));
            if (i % 9 == 4 && lampPrefab != null) Prop(lampPrefab, mid + outward * 4.6f, Quaternion.LookRotation(outward), i % 18 == 4);
        }
        // 4 lối vào từ vỉa hè tới đường dạo quanh hồ (người đi bộ chỉ đi vùng Sidewalks)
        foreach (Vector3 dir in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
        {
            bool alongZ = dir.x == 0f;
            float shore = alongZ ? rz : rx;
            float edge = alongZ ? r.height * 0.5f - Walk : r.width * 0.5f - Walk;
            Vector3 lateral = alongZ ? Vector3.right * (dir.z < 0f ? 6f : 2f) : Vector3.forward * (dir.x > 0f ? -rz * 0.3f : 0f);
            Vector3 a = center + dir * (edge + 0.3f) + lateral, b = center + dir * (shore + 2.4f) + lateral;
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            Box(mid + Vector3.up * 0.013f, Quaternion.LookRotation(dir), new Vector3(3f, 0.02f, len), Path, false);
            MarkVolume(mid, alongZ ? new Vector3(3f, 1.5f, len + 1f) : new Vector3(len + 1f, 1.5f, 3f));
            WalkPoints.Add(b);
        }
        GameObject pathGroup = Flush(name + "_Path");
        MarkSidewalk(pathGroup);
        // Kè đá riêng một nhóm, không cho người đi bộ đi trên thành kè
        foreach (var k in ke) Box(k.p + Vector3.up * 0.2f, k.q, new Vector3(0.5f, 0.4f, k.len), Stone, true);
        MarkArea(Flush(name + "_Ke"), "Not Walkable");
        for (int i = 0; i < 24; i++)
        {
            float ang = i * Mathf.PI * 2f / 24f;
            Vector3 outw = new Vector3(Mathf.Cos(ang) / rx, 0f, Mathf.Sin(ang) / rz).normalized;
            WalkPoints.Add(center + new Vector3(Mathf.Cos(ang) * rx, 0f, Mathf.Sin(ang) * rz) + outw * 2.4f);
        }
        for (int s = 0; s + 6 < ring.Count; s += 6) Leaves(ring[s], ring[s + 6], 7f);

        // Tháp Rùa
        Vector3 turtle = center + new Vector3(0f, 0f, -rz * 0.22f);
        Color moss = new Color(0.74f, 0.7f, 0.56f);
        Blob(turtle, Quaternion.identity, new Vector3(11f, 1.0f, 7f), Plant, true);
        Box(turtle + Vector3.up * 1.9f, Quaternion.identity, new Vector3(4.2f, 3.4f, 3.2f), moss, true);
        Box(turtle + Vector3.up * 4.3f, Quaternion.identity, new Vector3(3.1f, 1.6f, 2.4f), moss, true);
        Box(turtle + Vector3.up * 5.6f, Quaternion.identity, new Vector3(2.2f, 1.0f, 1.7f), moss, true);
        Box(turtle + Vector3.up * 6.4f, Quaternion.identity, new Vector3(1.5f, 0.6f, 1.2f), moss, true);
        Cone(turtle + Vector3.up * 7.1f, Quaternion.identity, new Vector3(0.9f, 0.9f, 0.9f), TempleRoof, true);
        for (int s = 0; s < 4; s++)
        {
            Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
            float half = s % 2 == 0 ? 1.61f : 2.11f;
            for (int k = -1; k <= 1; k++) Box(turtle + q * new Vector3(k * 1.1f, 1.6f, half), q, new Vector3(0.6f, 1.6f, 0.04f), Dark, false);
        }
        // Đền Ngọc Sơn + cầu Thê Húc + Tháp Bút
        Vector3 island = center + new Vector3(rx * 0.55f, 0f, rz * 0.55f);
        Blob(island, Quaternion.identity, new Vector3(18f, 1.0f, 14f), Plant, true);
        TempleHall(island + new Vector3(0f, 0.3f, 1f), 9f, 5f, new Color(0.6f, 0.16f, 0.1f));
        PlantTree(island + new Vector3(-6f, 0.3f, 3f), 1.2f, false, true);
        PlantTree(island + new Vector3(6f, 0.3f, -3f), 1.1f, false, true);
        float shoreX = center.x + rx * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((island.z - center.z) / rz, 2f)));
        Vector3 from = new Vector3(shoreX + 1.2f, LotTop, island.z), to = island + new Vector3(8f, 0.2f, 0f);
        int bseg = 16;
        for (int i = 0; i < bseg; i++)
        {
            float t0 = (float)i / bseg, t1 = (float)(i + 1) / bseg;
            Vector3 a = Vector3.Lerp(from, to, t0) + Vector3.up * (0.3f + Mathf.Sin(t0 * Mathf.PI) * 1.6f);
            Vector3 b = Vector3.Lerp(from, to, t1) + Vector3.up * (0.3f + Mathf.Sin(t1 * Mathf.PI) * 1.6f);
            Vector3 dir = b - a;
            Quaternion q = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
            Box((a + b) * 0.5f, q, new Vector3(2.6f, 0.2f, dir.magnitude + 0.05f), Bridge, true);
            Box((a + b) * 0.5f + side * 1.25f + Vector3.up * 0.5f, q, new Vector3(0.1f, 0.9f, dir.magnitude + 0.05f), Bridge, false);
            Box((a + b) * 0.5f - side * 1.25f + Vector3.up * 0.5f, q, new Vector3(0.1f, 0.9f, dir.magnitude + 0.05f), Bridge, false);
            if (i % 2 == 0) Person((a + b) * 0.5f + side * Range(-0.8f, 0.8f), dir.normalized * (Chance(0.5) ? 1f : -1f), 0, 0);
        }
        Vector3 g = from + new Vector3(2.4f, 0f, 0f);
        Box(g + new Vector3(0f, 1.8f, -1.7f), Quaternion.identity, new Vector3(0.5f, 3.6f, 0.5f), Bridge, true);
        Box(g + new Vector3(0f, 1.8f, 1.7f), Quaternion.identity, new Vector3(0.5f, 3.6f, 0.5f), Bridge, true);
        CurvedRoof(g + Vector3.up * 3.6f, 1.2f, 4.6f);
        Sign(g + new Vector3(0.28f, 3.1f, 0f), Vector3.left, new Vector2(2.8f, 0.55f), "THÊ HÚC", new Color(1f, 0.85f, 0.2f));
        Vector3 pen = g + new Vector3(4.5f, 0f, -5f);
        Box(pen + Vector3.up * 0.6f, Quaternion.identity, new Vector3(3f, 1.2f, 3f), Stone, true);
        for (int t = 0; t < 5; t++) Box(pen + Vector3.up * (1.2f + t * 1.5f + 0.75f), Quaternion.identity, new Vector3(1.4f - t * 0.18f, 1.5f, 1.4f - t * 0.18f), Stone, true);
        Cone(pen + Vector3.up * 9.6f, Quaternion.identity, new Vector3(0.6f, 1.4f, 0.6f), Stone, true);
        AddCollider(pen + Vector3.up * 4.5f, Quaternion.identity, new Vector3(3f, 9f, 3f), "Thap But");
        Address("Den Ngoc Son, " + X[6].id, g + new Vector3(4f, 0f, 2.5f), g);
        // Bia đá, ki-ốt kem Tràng Tiền, người tập thể dục, xe đạp bán hoa
        Vector3 stone = new Vector3(center.x - 12f, LotTop, r.yMin + Walk + 3f);
        Box(stone + Vector3.up * 0.9f, Quaternion.identity, new Vector3(4f, 1.8f, 0.6f), Stone, true);
        Sign(stone + new Vector3(0f, 1.0f, -0.31f), Vector3.forward, new Vector2(3.6f, 1.2f), "HỒ HOÀN KIẾM\n<size=50%>Hồ Gươm - Hà Nội</size>", new Color(0.25f, 0.2f, 0.15f));
        Vector3 kem = new Vector3(center.x + 14f, LotTop, r.yMin + Walk + 3.5f);
        Box(kem + Vector3.up * 1.4f, Quaternion.identity, new Vector3(4f, 2.8f, 2.6f), White, true);
        Box(kem + Vector3.up * 2.95f, Quaternion.identity, new Vector3(4.6f, 0.3f, 3.2f), new Color(0.12f, 0.42f, 0.75f), true);
        Box(kem + new Vector3(0f, 2.4f, -1.32f), Quaternion.identity, new Vector3(3.8f, 0.6f, 0.04f), new Color(0.12f, 0.42f, 0.75f), false);
        Sign(kem + new Vector3(0f, 2.4f, -1.36f), Vector3.forward, new Vector2(3.6f, 0.5f), "KEM TRÀNG TIỀN", White);
        Address("Kem Trang Tien, " + Z[4].id, kem + new Vector3(0f, 0f, -2.5f), kem);
        for (int k = 0; k < 4; k++) Person(kem + new Vector3(Range(-1.5f, 1.5f), 0f, -2.2f - k * 0.7f), Vector3.forward, 0, 0);
        Vector3 gym = center + new Vector3(-rx * 0.3f, 0f, rz + 4.5f);
        for (int k = 0; k < 10; k++) Person(gym + new Vector3((k % 5) * 1.5f, 0f, (k / 5) * 1.5f), Vector3.back, 0, Chance(0.2) ? 1 : 0);
        for (int k = 0; k < 4; k++)
        {
            float a = Range(0f, Mathf.PI * 2f);
            Vector3 outward = new Vector3(Mathf.Cos(a) / rx, 0f, Mathf.Sin(a) / rz).normalized;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz) + outward * 2.6f;
            FlowerBike(p, Vector3.Cross(Vector3.up, outward));
        }
        Flush(name);
    }

    private Material waterMat;

    private void Water(Vector3 c, float rx, float rz)
    {
        var go = new GameObject("Water");
        go.transform.SetParent(geo, false);
        go.AddComponent<MeshFilter>().sharedMesh = Ellipse(c, rx, rz, 64);
        var mr = go.AddComponent<MeshRenderer>();
        if (waterMat == null)
        {
            waterMat = new Material(RuntimeMaterials.Lit(WaterColor)) { name = "VietWater", hideFlags = HideFlags.DontSave };
            if (waterMat.HasProperty("_Smoothness")) waterMat.SetFloat("_Smoothness", 0.92f);
        }
        mr.sharedMaterial = waterMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        MarkArea(go, "Not Walkable"); // không ai đi trên mặt nước
    }

    private static Mesh Ellipse(Vector3 c, float rx, float rz, int n)
    {
        var v = new Vector3[n + 1];
        var nn = new Vector3[n + 1];
        var tri = new int[n * 3];
        v[0] = c;
        nn[0] = Vector3.up;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            v[i + 1] = c + new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
            nn[i + 1] = Vector3.up;
            tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % n + 1; tri[i * 3 + 2] = i + 1;
        }
        var m = new Mesh { name = "Water" };
        m.vertices = v;
        m.normals = nn;
        m.triangles = tri;
        m.RecalculateBounds();
        return m;
    }
}
