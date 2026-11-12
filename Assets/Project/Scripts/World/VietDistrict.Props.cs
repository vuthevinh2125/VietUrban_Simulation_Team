using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Chi tiết đường phố: cây lá vàng + lá rơi theo mùa, cột điện dây điện, đèn, người, hàng rong, xe máy dựng, quán vỉa hè...</summary>
public partial class VietDistrict
{
    // ================= Màu =================
    private static readonly Color[] Facades =
    {
        new Color(0.93f, 0.79f, 0.42f), new Color(0.96f, 0.89f, 0.70f), new Color(0.78f, 0.90f, 0.84f), new Color(0.95f, 0.76f, 0.76f),
        new Color(0.70f, 0.82f, 0.93f), new Color(0.97f, 0.97f, 0.94f), new Color(0.96f, 0.67f, 0.44f), new Color(0.86f, 0.86f, 0.82f),
        new Color(0.80f, 0.91f, 0.66f), new Color(0.98f, 0.86f, 0.55f), new Color(0.88f, 0.72f, 0.86f), new Color(0.72f, 0.86f, 0.86f),
    };
    private static readonly Color[] KttColors = { new Color(0.86f, 0.78f, 0.55f), new Color(0.82f, 0.72f, 0.68f), new Color(0.72f, 0.77f, 0.78f), new Color(0.88f, 0.84f, 0.74f) };
    private static readonly Color[] SignColors =
    {
        new Color(0.78f, 0.08f, 0.08f), new Color(0.08f, 0.28f, 0.68f), new Color(0.98f, 0.80f, 0.08f), new Color(0.06f, 0.46f, 0.22f),
        new Color(0.95f, 0.45f, 0.05f), new Color(0.55f, 0.08f, 0.45f), new Color(0.95f, 0.95f, 0.92f), new Color(0.1f, 0.1f, 0.12f),
    };
    private static readonly Color[] Awnings =
    {
        new Color(0.12f, 0.36f, 0.72f), new Color(0.14f, 0.55f, 0.25f), new Color(0.85f, 0.18f, 0.12f), new Color(0.95f, 0.6f, 0.1f), new Color(0.9f, 0.9f, 0.88f),
    };
    private static readonly Color[] Roofs = { new Color(0.2f, 0.36f, 0.62f), new Color(0.65f, 0.18f, 0.12f), new Color(0.55f, 0.57f, 0.6f), new Color(0.2f, 0.45f, 0.3f) };
    private static readonly Color[] Bikes =
    {
        new Color(0.08f, 0.08f, 0.09f), new Color(0.75f, 0.1f, 0.1f), new Color(0.12f, 0.25f, 0.6f), new Color(0.92f, 0.92f, 0.9f),
        new Color(0.6f, 0.62f, 0.65f), new Color(0.85f, 0.6f, 0.75f), new Color(0.35f, 0.25f, 0.2f),
    };
    private static readonly Color[] Clothes =
    {
        new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.85f), new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.8f, 0.2f),
        new Color(0.3f, 0.7f, 0.4f), new Color(0.9f, 0.5f, 0.7f), new Color(0.2f, 0.2f, 0.25f), new Color(0.6f, 0.4f, 0.8f),
    };
    private static readonly Color[] Goods =
    {
        new Color(0.85f, 0.2f, 0.15f), new Color(0.2f, 0.5f, 0.85f), new Color(0.95f, 0.85f, 0.3f), new Color(0.3f, 0.7f, 0.35f),
        new Color(0.95f, 0.95f, 0.95f), new Color(0.75f, 0.55f, 0.35f), new Color(0.95f, 0.55f, 0.15f), new Color(0.55f, 0.3f, 0.7f),
    };
    private static readonly Color[] Fruits =
    {
        new Color(0.95f, 0.85f, 0.15f), new Color(0.9f, 0.25f, 0.15f), new Color(0.35f, 0.7f, 0.2f), new Color(0.98f, 0.6f, 0.1f), new Color(0.6f, 0.15f, 0.35f),
    };
    private static readonly Color[] Flowers =
    {
        new Color(0.98f, 0.98f, 0.95f), new Color(0.98f, 0.85f, 0.2f), new Color(0.95f, 0.45f, 0.6f), new Color(0.85f, 0.12f, 0.15f), new Color(0.65f, 0.4f, 0.85f),
    };
    private static readonly Color[] Skins = { new Color(0.93f, 0.78f, 0.64f), new Color(0.86f, 0.68f, 0.52f), new Color(0.76f, 0.58f, 0.44f) };
    private static readonly Color[] Shirts =
    {
        new Color(0.95f, 0.95f, 0.95f), new Color(0.2f, 0.35f, 0.7f), new Color(0.75f, 0.15f, 0.15f), new Color(0.3f, 0.55f, 0.35f),
        new Color(0.95f, 0.8f, 0.3f), new Color(0.15f, 0.15f, 0.18f), new Color(0.9f, 0.55f, 0.65f), new Color(0.55f, 0.7f, 0.9f),
    };
    private static readonly Color[] Pants = { new Color(0.15f, 0.15f, 0.2f), new Color(0.2f, 0.28f, 0.45f), new Color(0.45f, 0.4f, 0.35f), new Color(0.1f, 0.1f, 0.1f) };
    private static readonly Color[] Helmets = { new Color(0.9f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.1f, 0.3f, 0.7f), new Color(0.95f, 0.8f, 0.1f), new Color(0.1f, 0.1f, 0.1f), new Color(0.9f, 0.5f, 0.7f) };
    private static readonly Color[] UmbrellaColors = { new Color(0.85f, 0.15f, 0.12f), new Color(0.15f, 0.4f, 0.8f), new Color(0.95f, 0.8f, 0.1f), new Color(0.2f, 0.6f, 0.3f), new Color(0.95f, 0.95f, 0.92f) };
    private static readonly Color[] Bushes = { new Color(0.22f, 0.45f, 0.18f), new Color(0.3f, 0.52f, 0.2f), new Color(0.6f, 0.62f, 0.2f) };
    private static readonly Color[] Greens = { new Color(0.2f, 0.42f, 0.16f), new Color(0.27f, 0.5f, 0.2f), new Color(0.33f, 0.55f, 0.22f) };
    private static readonly Color[] LeafLitter = { new Color(0.95f, 0.78f, 0.2f), new Color(0.9f, 0.6f, 0.15f), new Color(0.82f, 0.42f, 0.1f), new Color(0.75f, 0.6f, 0.3f) };

    // Tán lá theo mùa (6 ô màu đổi được): mùa thu vàng rực như phố Hà Nội tháng 10 - 11
    private static readonly Color[] AutumnLeaves = { new Color(0.98f, 0.82f, 0.18f), new Color(0.95f, 0.68f, 0.12f), new Color(0.9f, 0.5f, 0.1f), new Color(0.99f, 0.9f, 0.35f), new Color(0.82f, 0.36f, 0.08f), new Color(0.78f, 0.74f, 0.22f) };
    private static readonly Color[] WinterLeaves = { new Color(0.78f, 0.66f, 0.3f), new Color(0.6f, 0.5f, 0.28f), new Color(0.42f, 0.48f, 0.26f), new Color(0.85f, 0.72f, 0.3f), new Color(0.5f, 0.4f, 0.25f), new Color(0.38f, 0.45f, 0.28f) };
    private static readonly Color[] SpringLeaves = { new Color(0.55f, 0.78f, 0.3f), new Color(0.65f, 0.82f, 0.35f), new Color(0.95f, 0.85f, 0.3f), new Color(0.45f, 0.7f, 0.25f), new Color(0.92f, 0.72f, 0.2f), new Color(0.98f, 0.75f, 0.8f) };
    private static readonly Color[] SummerLeaves = { new Color(0.2f, 0.45f, 0.15f), new Color(0.28f, 0.52f, 0.18f), new Color(0.85f, 0.15f, 0.1f), new Color(0.24f, 0.48f, 0.2f), new Color(0.92f, 0.75f, 0.2f), new Color(0.3f, 0.55f, 0.22f) };

    private static readonly Color Asphalt = new Color(0.2f, 0.2f, 0.21f), Paint = new Color(0.92f, 0.92f, 0.88f),
        Sidewalk = new Color(0.66f, 0.36f, 0.28f), SidewalkLine = new Color(0.52f, 0.28f, 0.22f), Curb = new Color(0.66f, 0.66f, 0.64f),
        Lot = new Color(0.45f, 0.42f, 0.38f), Yard = new Color(0.72f, 0.7f, 0.66f), AlleyFloor = new Color(0.58f, 0.57f, 0.55f),
        Dark = new Color(0.1f, 0.09f, 0.08f), Glass = new Color(0.16f, 0.21f, 0.26f), GlassBlue = new Color(0.25f, 0.42f, 0.55f),
        Metal = new Color(0.55f, 0.57f, 0.6f), Slab = new Color(0.86f, 0.86f, 0.84f), Rail = new Color(0.14f, 0.14f, 0.15f),
        Frame = new Color(0.8f, 0.8f, 0.78f), Plant = new Color(0.22f, 0.5f, 0.2f), White = new Color(0.93f, 0.93f, 0.92f),
        Steel = new Color(0.8f, 0.82f, 0.85f), PolePaint = new Color(0.62f, 0.62f, 0.6f), Wire = new Color(0.05f, 0.05f, 0.05f),
        Stool1 = new Color(0.86f, 0.1f, 0.1f), Stool2 = new Color(0.16f, 0.36f, 0.86f), Grass = new Color(0.33f, 0.5f, 0.24f),
        Path = new Color(0.74f, 0.72f, 0.67f), WaterColor = new Color(0.2f, 0.38f, 0.33f), Tile = new Color(0.68f, 0.3f, 0.2f),
        FrenchYellow = new Color(0.96f, 0.78f, 0.36f), Cream = new Color(0.95f, 0.92f, 0.82f), Shutter = new Color(0.17f, 0.42f, 0.3f),
        WoodBrown = new Color(0.45f, 0.28f, 0.16f), TempleRoof = new Color(0.27f, 0.23f, 0.2f), Bridge = new Color(0.8f, 0.1f, 0.08f),
        Stone = new Color(0.8f, 0.76f, 0.66f), Ground = new Color(0.4f, 0.4f, 0.36f), Brick = new Color(0.62f, 0.5f, 0.4f),
        Bark = new Color(0.3f, 0.23f, 0.17f), Soil = new Color(0.32f, 0.24f, 0.18f), StreetBlue = new Color(0.1f, 0.3f, 0.65f),
        TileFloor = new Color(0.9f, 0.9f, 0.87f), Interior = new Color(0.7f, 0.66f, 0.6f), Gold = new Color(0.92f, 0.72f, 0.2f),
        Crate = new Color(0.68f, 0.52f, 0.32f), PanelBlue = new Color(0.12f, 0.2f, 0.42f), NonLa = new Color(0.92f, 0.86f, 0.62f),
        Bamboo = new Color(0.78f, 0.66f, 0.42f), HairBlack = new Color(0.08f, 0.07f, 0.07f), Terracotta = new Color(0.72f, 0.38f, 0.24f);

    // ================= Tên cửa hàng =================
    private static readonly string[] Shops =
    {
        "TẠP HÓA", "PHỞ BÒ GIA TRUYỀN", "BÁNH MÌ", "CÀ PHÊ", "BÚN CHẢ", "CƠM BÌNH DÂN", "SỬA XE MÁY", "TIỆM VÀNG", "NHÀ THUỐC",
        "TRÀ SỮA", "ĐIỆN THOẠI", "THỜI TRANG", "CẮT TÓC GỘI ĐẦU", "BÚN BÒ HUẾ", "CHÁO SƯỜN", "BIA HƠI", "KHÓA - CHÌA KHÓA",
        "VĂN PHÒNG PHẨM", "MỸ PHẨM", "GIÀY DÉP", "BÁNH CUỐN", "XÔI XÉO", "PHOTO - IN ẤN", "NEM CHUA RÁN", "TRÀ ĐÁ", "CHÈ BỐN MÙA",
        "BÚN ỐC", "LẨU NƯỚNG", "PHỞ CUỐN", "BÚN RIÊU CUA", "SỬA ĐIỆN THOẠI", "ĐIỆN NƯỚC", "GẠO - NƯỚC MẮM", "HOA TƯƠI",
    };
    private static readonly string[] TallShops =
    {
        "KHÁCH SẠN PHỐ CỔ", "NGÂN HÀNG HÀ THÀNH", "ĐIỆN THOẠI - LAPTOP", "SIÊU THỊ MINI", "NHÀ HÀNG HẢI SẢN", "THỜI TRANG NỮ",
        "MỸ PHẨM CHÍNH HÃNG", "TRUNG TÂM TIẾNG ANH", "PHÒNG KHÁM ĐA KHOA", "CÀ PHÊ SÂN THƯỢNG", "ĐỒNG HỒ - KÍNH MẮT", "VÀNG BẠC ĐÁ QUÝ",
    };
    private static readonly string[] Food = { "PHỞ", "BÚN", "CƠM", "CÀ PHÊ", "BIA", "BÁNH CUỐN", "BÁNH MÌ", "CHÁO", "XÔI", "NEM", "TRÀ", "CHÈ", "LẨU", "NHÀ HÀNG", "GÀ TẦN" };
    private static readonly Dictionary<string, string[]> StreetTrades = new Dictionary<string, string[]>
    {
        { "Hang Bac", new[] { "TIỆM VÀNG BẠC", "BẠC MỸ NGHỆ", "TRANG SỨC", "KHẮC DẤU" } },
        { "Hang Dao", new[] { "LỤA HÀ ĐÔNG", "VẢI - LỤA", "THỜI TRANG", "ÁO DÀI MAY ĐO" } },
        { "Hang Chieu", new[] { "CHIẾU CÓI", "ĐỒ MÂY TRE", "TẠP HÓA" } },
        { "Hang Ga", new[] { "GÀ TẦN", "CƠM GÀ", "MIẾN GÀ" } },
        { "Trang Tien", new[] { "NHÀ SÁCH", "TRANH SƠN DẦU", "KEM", "BĂNG ĐĨA" } },
        { "Duong Thanh", new[] { "ĐỒ GỖ", "BÁT ĐĨA SỨ", "ĐỒ NHÔM INOX" } },
        { "Phung Hung", new[] { "CÀ PHÊ ĐƯỜNG TÀU", "BÚN CHẢ", "TRANH BÍCH HỌA" } },
        { "Dinh Tien Hoang", new[] { "CÀ PHÊ VIEW HỒ", "ĐỒ LƯU NIỆM", "TOUR DU LỊCH" } },
        { "Hai Ba Trung", new[] { "ĐIỆN THOẠI", "THỜI TRANG", "MÁY ẢNH" } },
        { "Tran Hung Dao", new[] { "NHÀ HÀNG", "XE MÁY CŨ", "PHỤ TÙNG XE MÁY" } },
        { "Nguyen Du", new[] { "BÁNH CUỐN", "CHÈ", "HOA SỮA" } },
        { "Le Thach", new[] { "HOMESTAY", "CÀ PHÊ TRỨNG" } },
    };

    private string Trade(string street, Cell type, bool tall)
    {
        if (tall && Chance(0.6)) return Pick(TallShops);
        if (street != null && StreetTrades.TryGetValue(street, out string[] t) && Chance(0.5)) return Pick(t);
        return Pick(Shops);
    }

    private static bool IsFood(string trade)
    {
        foreach (string f in Food) if (trade.StartsWith(f) || trade.Contains(" " + f)) return true;
        return false;
    }

    private static Quaternion Rot(float yaw) => Quaternion.Euler(0f, yaw, 0f);

    // ================= Đồ đường phố dọc một cạnh ô =================
    private void EdgeFurniture(Edge e, Cell type)
    {
        if (!e.street) return;
        float len = e.Len;
        Vector3 u = e.U, vin = e.In;
        bool main = e.line.main;
        float treeChance = type switch
        {
            Cell.French or Cell.Opera or Cell.Park or Cell.Lake or Cell.School or Cell.Pagoda or Cell.Apartments => 1f,
            Cell.Commercial => 0.8f,
            Cell.Residential => 0.55f,
            Cell.Outer => 0.7f,
            Cell.Market => 0.3f,
            _ => 0.3f,
        };
        if (main) treeChance = 1f;
        bool poles = type == Cell.OldQuarter || type == Cell.Residential || type == Cell.Commercial || type == Cell.Outer || type == Cell.Apartments || type == Cell.Market;
        bool lamps = main || type == Cell.French || type == Cell.Opera || type == Cell.School || type == Cell.Park || type == Cell.Lake;

        var trees = new List<Vector3>();
        for (float s = Walk + 5f; s < len - Walk - 5f; s += 13f)
        {
            if (!Chance(treeChance)) continue;
            Vector3 p = e.a + vin * 0.95f + u * (s + Range(-1f, 1f)) + Vector3.up * LotTop;
            PlantTree(p, Range(0.9f, 1.2f), true);
            trees.Add(p);
        }
        if (trees.Count >= 2) Leaves(trees[0], trees[trees.Count - 1], 6f);
        else if (trees.Count == 1) Leaves(trees[0] - u * 3f, trees[0] + u * 3f, 6f);

        if (poles) Poles(e, len);
        if (lamps && lampPrefab != null)
        {
            int n = 0;
            for (float s = Walk + 9f; s < len - Walk - 4f; s += 27f, n++)
                Prop(lampPrefab, e.a + vin * 0.45f + u * s + Vector3.up * LotTop, Quaternion.LookRotation(-e.n), n % 2 == 0);
        }
        for (float s = Walk + 20f; s < len - Walk - 6f; s += 44f) Bin(e.a + vin * 0.55f + u * s + Vector3.up * LotTop);

        if (type == Cell.Store || type == Cell.Lake || type == Cell.Park) return;
        // Xe ôm đầu phố, gánh hàng rong, xe đạp bán hoa, xe bánh mì
        if (Chance(0.3)) XeOm(e.a + vin * 1.6f + u * (Walk + 2.5f) + Vector3.up * LotTop, u);
        if (Chance(0.25)) Vendor(e.a + vin * 1.8f + u * Range(10f, len - 10f) + Vector3.up * LotTop, u);
        if ((type == Cell.OldQuarter || type == Cell.French) && Chance(0.2)) FlowerBike(e.a + vin * 1.5f + u * Range(10f, len - 10f) + Vector3.up * LotTop, u);
        if (Chance(0.2)) BreadCart(e.a + vin * 1.6f + u * Range(10f, len - 10f) + Vector3.up * LotTop, u);
    }

    private void Poles(Edge e, float len)
    {
        Vector3 u = e.U, vin = e.In;
        Vector3? prev = null;
        int k = 0;
        for (float s = Walk + 6f; s < len - Walk - 3f; s += 24f, k++)
        {
            Vector3 p = e.a + vin * 0.45f + u * s + Vector3.up * LotTop;
            Box(p + Vector3.up * 4.25f, Quaternion.identity, new Vector3(0.28f, 8.5f, 0.28f), PolePaint, true);
            Box(p + Vector3.up * 7.6f, Quaternion.LookRotation(u), new Vector3(1.5f, 0.12f, 0.12f), PolePaint, false);
            // Búi dây điện quấn quanh cột
            for (int b = 0; b < 3; b++)
                Blob(p + Vector3.up * (6.3f + b * 0.28f) + vin * Range(-0.2f, 0.2f), Rot(rng.Next(180)), new Vector3(Range(0.9f, 1.4f), 0.22f, Range(0.5f, 0.8f)), Wire, false);
            if (Chance(0.25)) Box(p + Vector3.up * 5.3f - vin * 0.45f, Quaternion.LookRotation(u), new Vector3(0.6f, 0.9f, 0.5f), Metal, false);
            if (Chance(0.3)) Box(p + Vector3.up * 2.4f - vin * 0.16f, Quaternion.LookRotation(vin), new Vector3(0.5f, 0.7f, 0.02f), Pick(new[] { White, new Color(0.95f, 0.9f, 0.3f), new Color(0.6f, 0.85f, 0.95f) }), false); // tờ rơi dán cột
            if (prev.HasValue)
                for (int w = 0; w < 4; w++)
                    Cable(prev.Value + Vector3.up * (7.5f - w * 0.3f), p + Vector3.up * (7.5f - w * 0.3f), 0.45f + w * 0.12f);
            if (Chance(0.6)) Cable(p + Vector3.up * 6.6f, e.a + vin * Walk + u * (s + Range(-4f, 4f)) + Vector3.up * (LotTop + Range(4.2f, 6f)), 0.3f);
            prev = p;
        }
    }

    private void Cable(Vector3 a, Vector3 b, float sag)
    {
        const int n = 6;
        for (int i = 0; i < n; i++)
        {
            float t0 = (float)i / n, t1 = (float)(i + 1) / n;
            Vector3 p0 = Vector3.Lerp(a, b, t0) - Vector3.up * sag * 4f * t0 * (1f - t0);
            Vector3 p1 = Vector3.Lerp(a, b, t1) - Vector3.up * sag * 4f * t1 * (1f - t1);
            Vector3 d = p1 - p0;
            if (d.sqrMagnitude < 1e-4f) continue;
            Box((p0 + p1) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up), new Vector3(0.03f, 0.03f, d.magnitude + 0.02f), Wire, false);
        }
    }

    private void Bin(Vector3 p)
    {
        Box(p + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.5f, 0.9f, 0.5f), new Color(0.15f, 0.5f, 0.25f), false);
        Box(p + Vector3.up * 0.93f, Quaternion.identity, new Vector3(0.55f, 0.06f, 0.55f), new Color(0.12f, 0.4f, 0.2f), false);
    }

    // ================= Cây =================
    private void InitFoliage()
    {
        for (int i = 0; i < 6; i++) VPalette.Slot("leaf" + i, AutumnLeaves[i]);
    }

    private void PlantTree(Vector3 p, float scale, bool pit, bool green = false)
    {
        float h = Range(3.8f, 5.2f) * scale;
        Quaternion lean = Quaternion.Euler(Range(-4f, 4f), Range(0f, 360f), Range(-4f, 4f));
        Prism(p + lean * Vector3.up * (h * 0.5f), lean, new Vector3(0.32f * scale, h, 0.32f * scale), Bark, true);
        for (int b = 0; b < 3; b++)
        {
            Quaternion bq = lean * Quaternion.Euler(Range(30f, 50f), b * 120f + Range(-20f, 20f), 0f);
            Prism(p + lean * Vector3.up * (h * 0.72f) + bq * Vector3.up * (0.85f * scale), bq, new Vector3(0.15f * scale, 1.8f * scale, 0.15f * scale), Bark, true);
        }
        Vector3 top = p + lean * Vector3.up * h;
        int n = rng.Next(4, 7);
        for (int k = 0; k < n; k++)
        {
            Vector3 off = new Vector3(Range(-1.4f, 1.4f), Range(-0.4f, 1.2f), Range(-1.4f, 1.4f)) * scale;
            float s = Range(2.0f, 3.2f) * scale;
            Matrix4x4 m = Matrix4x4.TRS(top + off, Rot(Range(0f, 360f)), new Vector3(s, s * 0.82f, s));
            if (green) solid.Add(VShape.Blob, m, Pick(Greens));
            else solid.AddUV(VShape.Blob, m, VPalette.Slot("leaf" + rng.Next(6), AutumnLeaves[0]));
        }
        if (pit)
        {
            Box(p + Vector3.up * 0.03f, Quaternion.identity, new Vector3(1.1f, 0.06f, 1.1f), Curb, false);
            Box(p + Vector3.up * 0.032f, Quaternion.identity, new Vector3(0.9f, 0.062f, 0.9f), Soil, false);
        }
        if (!green)
            for (int k = rng.Next(6, 11); k > 0; k--)
                Box(p + new Vector3(Range(-0.9f, 0.9f), 0.03f, Range(-0.9f, 0.9f)), Rot(Range(0f, 360f)), new Vector3(Range(0.12f, 0.4f), 0.01f, Range(0.1f, 0.3f)), Pick(LeafLitter), false);
        AddCollider(p + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.4f * scale, 2.4f, 0.4f * scale), "Tree");
    }

    /// <summary>Liễu rủ bên hồ.</summary>
    private void Willow(Vector3 p)
    {
        float h = Range(4f, 5.5f);
        Quaternion lean = Quaternion.Euler(Range(6f, 12f), Range(0f, 360f), 0f);
        Prism(p + lean * Vector3.up * (h * 0.5f), lean, new Vector3(0.35f, h, 0.35f), Bark, true);
        Vector3 top = p + lean * Vector3.up * h;
        for (int k = 0; k < 9; k++)
        {
            Vector3 off = new Vector3(Range(-1.8f, 1.8f), Range(-1.6f, 0.2f), Range(-1.8f, 1.8f));
            Blob(top + off, Rot(Range(0f, 360f)), new Vector3(Range(0.8f, 1.2f), Range(2.2f, 3.2f), Range(0.8f, 1.2f)), Color.Lerp(Pick(Greens), AutumnLeaves[5], Range(0f, 0.5f)), true);
        }
        AddCollider(p + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.4f, 2.4f, 0.4f), "Willow");
    }

    /// <summary>Lá vàng rơi lả tả dọc một hàng cây (hạt hình lá, gió lay, rơi xuống nằm trên vỉa hè một lúc).</summary>
    private void Leaves(Vector3 a, Vector3 b, float height)
    {
        Vector3 d = b - a;
        d.y = 0f;
        float len = Mathf.Max(6f, d.magnitude + 6f);
        var go = new GameObject("Leaves");
        go.transform.SetParent(fxRoot, false);
        go.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * height, Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.008f, 0.012f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Clamp((int)(len * 2.5f), 30, 260);
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = len * 0.14f;
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(3.5f, 2.5f, len);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.7f, -0.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.4f;
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        rol.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rol.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        // Mặt đất để lá đáp xuống (không xuyên vỉa hè)
        var plane = new GameObject("LeafGround").transform;
        plane.SetParent(go.transform, false);
        plane.position = new Vector3(go.transform.position.x, LotTop + 0.03f, go.transform.position.z);
        plane.rotation = Quaternion.identity;
        var col = ps.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.Planes;
        col.SetPlane(0, plane);
        col.dampen = 1f;
        col.bounce = 0f;
        col.lifetimeLoss = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = VShape.CubeMesh;
        r.sharedMaterial = RuntimeMaterials.Lit(Pick(LeafLitter));
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        leafSystems.Add(ps);
        if (Application.isPlaying) ps.Play();
    }

    private string lastSeason;
    private float nextSeasonCheck;

    /// <summary>Đổi màu tán lá + lượng lá rơi theo mùa trong game (Editor: luôn mùa thu).</summary>
    private void ApplySeason(bool force)
    {
        string s = Application.isPlaying && WeatherSystem.Instance != null ? WeatherSystem.CurrentSeason.ToString() : "Autumn";
        if (!force && s == lastSeason) return;
        lastSeason = s;
        Color[] set = s == "Autumn" ? AutumnLeaves : s == "Winter" ? WinterLeaves : s == "Spring" ? SpringLeaves : SummerLeaves;
        for (int i = 0; i < 6; i++) VPalette.SetSlot("leaf" + i, set[i]);
        VPalette.Apply();
        float mult = s == "Autumn" ? 1f : s == "Winter" ? 0.55f : s == "Spring" ? 0.35f : 0.15f;
        foreach (ParticleSystem ps in leafSystems)
        {
            if (ps == null) continue;
            var em = ps.emission;
            float len = ps.shape.scale.z;
            em.rateOverTime = len * 0.14f * mult;
        }
    }

    // ================= Prefab (đèn đường, ghế đá) =================
    private void Prop(GameObject prefab, Vector3 p, Quaternion r, bool light = false)
    {
        if (prefab == null) return;
        GameObject g = Instantiate(prefab, p, r * prefab.transform.rotation, propsRoot);
        foreach (Light l in g.GetComponentsInChildren<Light>(true))
        {
            if (!light)
            {
                if (Application.isPlaying) Destroy(l); else DestroyImmediate(l);
                continue;
            }
            l.shadows = LightShadows.None;
            l.range = Mathf.Min(l.range, 14f);
            l.enabled = false;
            if (l.GetComponent<NightLight>() == null) l.gameObject.AddComponent<NightLight>();
        }
    }

    // ================= Người (tĩnh) =================
    /// <summary>pose: 0 đứng, 1 ngồi ghế nhựa thấp, 2 ngồi xổm. hat: 0 không, 1 nón lá, 2 mũ bảo hiểm.</summary>
    private void Person(Vector3 feet, Vector3 fwd, int pose, int hat, bool kid = false)
    {
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Quaternion r = Quaternion.LookRotation(fwd.normalized);
        float k = kid ? 0.72f : Range(0.92f, 1.05f);
        Color skin = Pick(Skins), shirt = kid ? White : Pick(Shirts), pants = kid ? StreetBlue : Pick(Pants);
        Vector3 L(float x, float y, float z) => feet + r * (new Vector3(x, y, z) * k);
        Vector3 head;
        switch (pose)
        {
            case 1:
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(L(s * 0.1f, 0.36f, 0.2f), r, new Vector3(0.15f, 0.14f, 0.42f) * k, pants, false);
                    Box(L(s * 0.1f, 0.18f, 0.4f), r, new Vector3(0.13f, 0.36f, 0.14f) * k, pants, false);
                    Box(L(s * 0.25f, 0.75f, 0.16f), r * Quaternion.Euler(-50f, 0f, 0f), new Vector3(0.11f, 0.5f, 0.13f) * k, shirt, false);
                }
                Box(L(0f, 0.72f, 0.02f), r, new Vector3(0.4f, 0.58f, 0.24f) * k, shirt, true);
                head = L(0f, 1.15f, 0.04f);
                break;
            case 2:
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(L(s * 0.12f, 0.32f, 0.12f), r * Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.15f, 0.14f, 0.4f) * k, pants, false);
                    Box(L(s * 0.12f, 0.2f, 0.3f), r, new Vector3(0.13f, 0.38f, 0.14f) * k, pants, false);
                    Box(L(s * 0.24f, 0.55f, 0.2f), r * Quaternion.Euler(-60f, 0f, 0f), new Vector3(0.11f, 0.45f, 0.13f) * k, shirt, false);
                }
                Box(L(0f, 0.62f, 0f), r * Quaternion.Euler(15f, 0f, 0f), new Vector3(0.4f, 0.55f, 0.24f) * k, shirt, true);
                head = L(0f, 1.0f, 0.1f);
                break;
            default:
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(L(s * 0.1f, 0.42f, 0f), r, new Vector3(0.14f, 0.84f, 0.16f) * k, pants, false);
                    Box(L(s * 0.26f, 1.1f, 0f), r, new Vector3(0.11f, 0.58f, 0.13f) * k, shirt, false);
                    Box(L(s * 0.26f, 0.76f, 0f), r, new Vector3(0.09f, 0.1f, 0.1f) * k, skin, false);
                }
                Box(L(0f, 1.12f, 0f), r, new Vector3(0.4f, 0.6f, 0.24f) * k, shirt, true);
                head = L(0f, 1.56f, 0f);
                break;
        }
        Blob(head, r, new Vector3(0.24f, 0.28f, 0.25f) * k, skin, false);
        Blob(head + Vector3.up * 0.06f * k - r * Vector3.forward * 0.03f * k, r, new Vector3(0.26f, 0.2f, 0.26f) * k, HairBlack, false);
        if (hat == 1) Cone(head + Vector3.up * 0.2f * k, Quaternion.identity, new Vector3(0.58f, 0.22f, 0.58f) * k, NonLa, true);
        else if (hat == 2) Blob(head + Vector3.up * 0.05f * k, r, new Vector3(0.32f, 0.27f, 0.34f) * k, Pick(Helmets), false);
    }

    /// <summary>Gánh hàng rong: nón lá, đòn gánh, hai thúng hoa quả.</summary>
    private void Vendor(Vector3 p, Vector3 u)
    {
        Vector3 fwd = Vector3.Cross(u, Vector3.up);
        Person(p, fwd, Chance(0.3) ? 2 : 0, 1);
        Quaternion r = Quaternion.LookRotation(u);
        bool squat = false;
        Vector3 pole = p + Vector3.up * (squat ? 0.9f : 1.42f);
        Box(pole, r, new Vector3(0.05f, 0.05f, 1.8f), Bamboo, false);
        foreach (float s in new[] { -0.82f, 0.82f })
        {
            Vector3 basket = p + u * s + Vector3.up * 0.32f;
            Prism(basket, Quaternion.identity, new Vector3(0.6f, 0.3f, 0.6f), Bamboo, false);
            for (int k = 0; k < 5; k++) Blob(basket + new Vector3(Range(-0.18f, 0.18f), 0.18f, Range(-0.18f, 0.18f)), Rot(Range(0f, 360f)), Vector3.one * 0.18f, Pick(Fruits), false);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                Vector3 rim = basket + new Vector3(Mathf.Cos(a) * 0.27f, 0.15f, Mathf.Sin(a) * 0.27f);
                Vector3 top = pole + u * s * 0.98f;
                Vector3 d = top - rim;
                Box((rim + top) * 0.5f, Quaternion.LookRotation(d.normalized), new Vector3(0.015f, 0.015f, d.magnitude), Bamboo, false);
            }
        }
    }

    /// <summary>Xe đạp chở hoa (cúc hoạ mi, hồng...) - đặc sản phố Hà Nội mùa thu.</summary>
    private void FlowerBike(Vector3 p, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Quaternion r = Quaternion.LookRotation(forward.normalized);
        Vector3 L(float x, float y, float z) => p + r * new Vector3(x, y, z);
        Quaternion wheel = r * Quaternion.Euler(0f, 0f, 90f);
        Prism(L(0f, 0.34f, 0.55f), wheel, new Vector3(0.68f, 0.04f, 0.68f), Rail, false);
        Prism(L(0f, 0.34f, -0.55f), wheel, new Vector3(0.68f, 0.04f, 0.68f), Rail, false);
        Box(L(0f, 0.62f, 0f), r * Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.04f, 0.04f, 1.0f), new Color(0.15f, 0.35f, 0.25f), false);
        Box(L(0f, 0.65f, 0.38f), r * Quaternion.Euler(18f, 0f, 0f), new Vector3(0.04f, 0.6f, 0.04f), new Color(0.15f, 0.35f, 0.25f), false);
        Box(L(0f, 0.98f, 0.45f), r, new Vector3(0.55f, 0.04f, 0.04f), Rail, false);
        Box(L(0f, 0.9f, -0.22f), r, new Vector3(0.18f, 0.06f, 0.26f), Rail, false);
        Box(L(0f, 0.85f, -0.62f), r, new Vector3(0.75f, 0.3f, 0.75f), Bamboo, false);
        for (int k = 0; k < 16; k++)
            Blob(L(Range(-0.4f, 0.4f), Range(1.0f, 1.45f), -0.62f + Range(-0.4f, 0.4f)), Rot(Range(0f, 360f)), Vector3.one * Range(0.14f, 0.22f), Pick(Flowers), false);
        for (int k = 0; k < 6; k++)
            Box(L(Range(-0.3f, 0.3f), 1.05f, -0.62f + Range(-0.3f, 0.3f)), r, new Vector3(0.02f, 0.4f, 0.02f), Plant, false);
        Box(L(0f, 0.9f, 0.62f), r, new Vector3(0.35f, 0.22f, 0.3f), Bamboo, false);
        for (int k = 0; k < 5; k++) Blob(L(Range(-0.12f, 0.12f), 1.08f, 0.62f + Range(-0.1f, 0.1f)), Quaternion.identity, Vector3.one * 0.15f, Pick(Flowers), false);
        if (Chance(0.65)) Person(p + r * new Vector3(0.6f, 0f, 0f), -(r * Vector3.right), 0, 1);
    }

    /// <summary>Xe bánh mì: tủ kính trên xe đẩy, biển chữ đỏ.</summary>
    private void BreadCart(Vector3 p, Vector3 u)
    {
        Quaternion r = Quaternion.LookRotation(u);
        Box(p + Vector3.up * 0.55f, r, new Vector3(0.7f, 0.8f, 1.2f), White, false);
        Box(p + Vector3.up * 1.2f, r, new Vector3(0.65f, 0.5f, 1.1f), Glass, false);
        for (int k = 0; k < 6; k++) Box(p + Vector3.up * 1.05f + r * new Vector3(Range(-0.2f, 0.2f), 0f, -0.4f + k * 0.16f), r, new Vector3(0.12f, 0.08f, 0.32f), new Color(0.88f, 0.66f, 0.3f), false);
        Box(p + Vector3.up * 1.6f, r, new Vector3(0.75f, 0.3f, 1.25f), new Color(0.8f, 0.1f, 0.08f), false);
        Vector3 side = Vector3.Cross(Vector3.up, u);
        Sign(p + Vector3.up * 1.6f - side * 0.39f, side, new Vector2(1.2f, 0.26f), "BÁNH MÌ", new Color(1f, 0.92f, 0.3f));
        Prism(p + Vector3.up * 0.15f + u * 0.5f, Rot(0f) * Quaternion.LookRotation(u) * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.3f, 0.6f, 0.3f), Rail, false);
        Person(p - side * 0.9f, side, 0, Chance(0.5) ? 1 : 0);
    }

    /// <summary>Xe ôm đầu phố: bác tài ngồi vắt chân chờ khách.</summary>
    private void XeOm(Vector3 p, Vector3 u)
    {
        Motorbike(p, u, Pick(Bikes));
        Person(p + Vector3.up * 0.5f - u * 0.3f, u, 1, 2);
    }

    private void Motorbike(Vector3 p, Vector3 forward, Color body)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Quaternion r = Quaternion.LookRotation(forward.normalized, Vector3.up);
        Vector3 L(float x, float y, float z) => p + r * new Vector3(x, y, z);
        Quaternion wheel = r * Quaternion.Euler(0f, 0f, 90f);
        Prism(L(0f, 0.28f, 0.58f), wheel, new Vector3(0.54f, 0.08f, 0.54f), Dark, false);
        Prism(L(0f, 0.28f, -0.56f), wheel, new Vector3(0.54f, 0.08f, 0.54f), Dark, false);
        Box(L(0f, 0.55f, -0.12f), r, new Vector3(0.32f, 0.36f, 0.95f), body, true);
        Box(L(0f, 0.8f, 0.48f), r * Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.4f, 0.62f, 0.1f), body, false);
        Box(L(0f, 0.36f, 0.18f), r, new Vector3(0.34f, 0.05f, 0.42f), Dark, false);
        Box(L(0f, 0.84f, -0.28f), r, new Vector3(0.3f, 0.1f, 0.6f), Dark, false);
        Box(L(0f, 1.05f, 0.55f), r, new Vector3(0.64f, 0.05f, 0.05f), Rail, false);
        Box(L(0f, 1.0f, 0.62f), r, new Vector3(0.16f, 0.1f, 0.06f), new Color(1f, 0.95f, 0.75f), false);
        Box(L(0f, 0.62f, -0.72f), r, new Vector3(0.18f, 0.11f, 0.02f), White, false);
    }

    private void FoodSet(Vector3 p, Vector3 u, Vector3 vin, bool people)
    {
        Quaternion r = Quaternion.LookRotation(vin);
        Color sc = Chance(0.6) ? Stool1 : Stool2;
        Box(p + Vector3.up * 0.42f, r, new Vector3(0.8f, 0.04f, 0.5f), Chance(0.5) ? White : Steel, false);
        Box(p + Vector3.up * 0.2f, r, new Vector3(0.7f, 0.4f, 0.04f), Rail, false);
        for (int k = 0; k < 3; k++) Prism(p + Vector3.up * 0.47f + u * Range(-0.3f, 0.3f) + vin * Range(-0.15f, 0.15f), Quaternion.identity, new Vector3(0.16f, 0.07f, 0.16f), White, false);
        Vector3[] seats = { u * 0.65f, -u * 0.65f, vin * 0.5f, -vin * 0.5f };
        int seated = people ? rng.Next(1, 4) : 0;
        for (int k = 0; k < 4; k++)
        {
            Vector3 s = p + seats[k];
            Box(s + Vector3.up * 0.15f, r, new Vector3(0.3f, 0.3f, 0.3f), sc, false);
            if (k < seated) Person(s + Vector3.up * 0.0f - (p - s).normalized * 0.1f, p - s, 1, Chance(0.15) ? 2 : 0);
        }
    }

    private void LowTable(Vector3 p, Quaternion r, Color stool)
    {
        Box(p + Vector3.up * 0.42f, r, new Vector3(0.75f, 0.04f, 0.5f), White, false);
        Box(p + Vector3.up * 0.2f, r, new Vector3(0.65f, 0.4f, 0.04f), Rail, false);
        Box(p + r * new Vector3(0.6f, 0.15f, 0f), r, new Vector3(0.3f, 0.3f, 0.3f), stool, false);
        Box(p + r * new Vector3(-0.6f, 0.15f, 0f), r, new Vector3(0.3f, 0.3f, 0.3f), stool, false);
        if (Chance(0.5)) Person(p + r * new Vector3(0.62f, 0f, 0f), r * Vector3.left, 1, 0);
    }

    private void ChessGame(Vector3 p)
    {
        Box(p + Vector3.up * 0.22f, Quaternion.identity, new Vector3(0.5f, 0.44f, 0.5f), WoodBrown, false);
        Box(p + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.45f, 0.02f, 0.45f), new Color(0.9f, 0.8f, 0.55f), false);
        for (int k = 0; k < 10; k++) Prism(p + new Vector3(Range(-0.18f, 0.18f), 0.47f, Range(-0.18f, 0.18f)), Quaternion.identity, new Vector3(0.04f, 0.02f, 0.04f), k % 2 == 0 ? new Color(0.8f, 0.1f, 0.1f) : Dark, false);
        Box(p + new Vector3(0.6f, 0.15f, 0f), Quaternion.identity, new Vector3(0.3f, 0.3f, 0.3f), Stool1, false);
        Box(p + new Vector3(-0.6f, 0.15f, 0f), Quaternion.identity, new Vector3(0.3f, 0.3f, 0.3f), Stool2, false);
        Person(p + new Vector3(0.62f, 0f, 0f), Vector3.left, 1, 0);
        Person(p + new Vector3(-0.62f, 0f, 0f), Vector3.right, 1, 0);
        Person(p + new Vector3(0f, 0f, 0.8f), Vector3.back, 0, 0);
    }

    private void Umbrella(Vector3 p)
    {
        Box(p + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.05f, 2.4f, 0.05f), Rail, false);
        Cone(p + Vector3.up * 2.4f, Quaternion.identity, new Vector3(2.6f, 0.5f, 2.6f), Pick(UmbrellaColors), true);
    }

    private void PlantPot(Vector3 p)
    {
        Box(p + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.32f, 0.3f, 0.32f), Chance(0.5) ? Terracotta : new Color(0.2f, 0.35f, 0.6f), false);
        Color leaf = Chance(0.3) ? new Color(0.9f, 0.3f, 0.55f) : Pick(Greens); // hoa giấy hồng hoặc cây xanh
        Blob(p + Vector3.up * 0.5f, Rot(Range(0f, 360f)), new Vector3(0.5f, 0.45f, 0.5f), leaf, false);
    }

    private void Laundry(Vector3 a, Vector3 b)
    {
        Cable(a, b, 0.12f);
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.5f) return;
        Quaternion r = Quaternion.LookRotation(Vector3.Cross(Vector3.up, d.normalized));
        for (float t = 0.3f; t < len - 0.2f; t += Range(0.35f, 0.6f))
        {
            float f = t / len;
            Vector3 p = Vector3.Lerp(a, b, f) - Vector3.up * (0.12f * 4f * f * (1f - f));
            float h = Range(0.35f, 0.7f);
            Box(p - Vector3.up * h * 0.5f, r, new Vector3(Range(0.25f, 0.4f), h, 0.02f), Pick(Clothes), false);
        }
    }

    /// <summary>Dây đèn lồng đỏ vàng vắt ngang phố cổ.</summary>
    private void LanternString(Vector3 a, Vector3 b)
    {
        a.y = b.y = LotTop;
        Box(a + Vector3.up * 3.2f, Quaternion.identity, new Vector3(0.08f, 6.4f, 0.08f), Rail, false);
        Box(b + Vector3.up * 3.2f, Quaternion.identity, new Vector3(0.08f, 6.4f, 0.08f), Rail, false);
        Vector3 ta = a + Vector3.up * 6.3f, tb = b + Vector3.up * 6.3f;
        Cable(ta, tb, 0.6f);
        float len = Vector3.Distance(ta, tb);
        for (float t = 0.9f; t < len - 0.6f; t += 1.1f)
        {
            float f = t / len;
            Vector3 p = Vector3.Lerp(ta, tb, f) - Vector3.up * (0.6f * 4f * f * (1f - f) + 0.35f);
            Color c = rng.Next(3) == 0 ? new Color(1f, 0.82f, 0.15f) : new Color(0.9f, 0.12f, 0.1f);
            Blob(p, Quaternion.identity, new Vector3(0.36f, 0.44f, 0.36f), c, false);
            Box(p - Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.03f, 0.18f, 0.03f), new Color(1f, 0.8f, 0.2f), false);
        }
    }

    /// <summary>Băng rôn đỏ chữ vàng vắt ngang đường (kỷ niệm Ngày Giải phóng Thủ đô 10/10).</summary>
    private void Banner(Vector3 a, Vector3 b)
    {
        a.y = b.y = LotTop;
        Box(a + Vector3.up * 3.1f, Quaternion.identity, new Vector3(0.1f, 6.2f, 0.1f), Rail, false);
        Box(b + Vector3.up * 3.1f, Quaternion.identity, new Vector3(0.1f, 6.2f, 0.1f), Rail, false);
        Vector3 span = b - a;
        float len = span.magnitude;
        Vector3 mid = (a + b) * 0.5f + Vector3.up * 5.4f;
        Quaternion r = Quaternion.LookRotation(span.normalized);
        Box(mid, r, new Vector3(0.03f, 0.95f, len - 0.4f), new Color(0.82f, 0.08f, 0.08f), false);
        Vector3 view = Vector3.Cross(Vector3.up, span.normalized);
        const string text = "CHÀO MỪNG 72 NĂM NGÀY GIẢI PHÓNG THỦ ĐÔ (10/10/1954 - 10/10/2026)";
        Sign(mid - view * 0.03f, view, new Vector2(len - 1f, 0.8f), text, new Color(1f, 0.88f, 0.15f));
        Sign(mid + view * 0.03f, -view, new Vector2(len - 1f, 0.8f), text, new Color(1f, 0.88f, 0.15f));
    }

    private void BusStop(Vector3 p, Vector3 towardRoad)
    {
        Quaternion r = Quaternion.LookRotation(towardRoad);
        Vector3 L(float x, float y, float z) => p + r * new Vector3(x, y, z);
        Box(L(0f, 2.5f, 0f), r, new Vector3(3.6f, 0.1f, 1.5f), new Color(0.15f, 0.4f, 0.75f), true);
        Box(L(0f, 1.3f, -0.7f), r, new Vector3(3.6f, 1.8f, 0.05f), new Color(0.8f, 0.88f, 0.95f), false);
        Box(L(-1.75f, 1.25f, -0.7f), r, new Vector3(0.08f, 2.5f, 0.08f), Rail, false);
        Box(L(1.75f, 1.25f, -0.7f), r, new Vector3(0.08f, 2.5f, 0.08f), Rail, false);
        Box(L(0f, 0.45f, -0.45f), r, new Vector3(2.6f, 0.06f, 0.4f), Steel, false);
        Box(L(2.3f, 1.3f, 0.3f), r, new Vector3(0.07f, 2.6f, 0.07f), Rail, false);
        Box(L(2.3f, 2.4f, 0.3f), r, new Vector3(0.8f, 0.6f, 0.04f), new Color(0.15f, 0.4f, 0.75f), false);
        Sign(L(2.3f, 2.4f, 0.33f), towardRoad * -1f, new Vector2(0.75f, 0.55f), "ĐIỂM DỪNG\nXE BUÝT\n<size=70%>09 - 14 - 36</size>", White);
        int n = rng.Next(1, 4);
        for (int k = 0; k < n; k++) Person(L(Range(-1.2f, 1.2f), 0f, 0.2f), towardRoad, Chance(0.4) ? 1 : 0, 0);
    }

    // ================= Cờ đỏ sao vàng =================
    private Material flagMat;

    private void Flag(Vector3 root, Vector3 u, Vector3 vin)
    {
        Vector3 dir = (-vin * 0.75f + Vector3.up * 0.65f).normalized;
        Box(root + dir * 0.8f, Quaternion.LookRotation(dir), new Vector3(0.04f, 0.04f, 1.6f), Rail, false);
        Vector3 tip = root + dir * 1.5f;
        FlagQuad(tip - Vector3.up * 0.35f + u * 0.45f, Quaternion.LookRotation(vin), new Vector3(0.9f, 0.6f, 0.01f));
    }

    private void BigFlag(Vector3 top, Vector3 along)
    {
        FlagQuad(top + along * 0.95f, Quaternion.LookRotation(Vector3.Cross(along, Vector3.up)), new Vector3(1.8f, 1.2f, 0.02f));
    }

    private void FlagQuad(Vector3 pos, Quaternion rot, Vector3 size)
    {
        if (flagMat == null) flagMat = MakeFlagMaterial();
        var go = new GameObject("Flag");
        go.transform.SetParent(propsRoot, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = FlagMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = flagMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Mesh flagMesh;

    /// <summary>Tấm phẳng hai mặt có UV (cho texture cờ).</summary>
    private static Mesh FlagMesh
    {
        get
        {
            if (flagMesh != null) return flagMesh;
            flagMesh = new Mesh { name = "VietFlagQuad", hideFlags = HideFlags.DontSave };
            var a = new Vector3(-0.5f, -0.5f, 0f);
            var b = new Vector3(-0.5f, 0.5f, 0f);
            var c = new Vector3(0.5f, 0.5f, 0f);
            var d = new Vector3(0.5f, -0.5f, 0f);
            flagMesh.vertices = new[] { a, b, c, d, a, b, c, d };
            flagMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            flagMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
            flagMesh.RecalculateNormals();
            flagMesh.RecalculateBounds();
            return flagMesh;
        }
    }

    private static Material MakeFlagMaterial()
    {
        const int W = 96, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VietnamFlag", hideFlags = HideFlags.DontSave };
        var red = new Color(0.85f, 0.1f, 0.12f);
        var yellow = new Color(1f, 0.87f, 0f);
        var star = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI / 2f + i * Mathf.PI / 5f, rad = i % 2 == 0 ? 20f : 8f;
            star[i] = new Vector2(W * 0.5f + Mathf.Cos(ang) * rad, H * 0.5f + Mathf.Sin(ang) * rad);
        }
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                px[y * W + x] = InsidePoly(star, new Vector2(x + 0.5f, y + 0.5f)) ? yellow : red;
        tex.SetPixels(px);
        tex.Apply();
        var m = new Material(RuntimeMaterials.Lit(Color.white)) { name = "VietnamFlag", hideFlags = HideFlags.DontSave };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        return m;
    }

    private static bool InsidePoly(Vector2[] poly, Vector2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }
}
