using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Thành phố kiểu Hà Nội (quanh Hồ Hoàn Kiếm - phố cổ) dựng bằng code, siêu thị của người chơi nằm ở TRUNG TÂM:
///  - Lưới 8 x 8 tuyến phố tên thật (Hai Bà Trưng trước cửa siêu thị, Tràng Tiền, Hàng Bạc, Hàng Đào, Lê Thái Tổ,
///    Đinh Tiên Hoàng, Trần Quang Khải, Trần Hưng Đạo...), rộng khoảng 640 x 620 m.
///  - Mỗi ô phố một kiểu: phố cổ nhà ống có ngõ, phố buôn bán nhà cao, khu phố Pháp (biệt thự, Nhà hát Lớn),
///    khu tập thể, Hồ Gươm (Tháp Rùa, cầu Thê Húc, đền Ngọc Sơn, Tháp Bút), chợ Đồng Xuân, chùa Trấn Quốc, trường học, công viên.
///  - Cây lá vàng mùa thu có lá rơi (đổi màu theo mùa trong game), xe máy dựng vỉa hè, quán ghế nhựa, gánh hàng rong,
///    xe đạp bán hoa, cột điện dây chằng chịt, đèn lồng, băng rôn, biển tên phố.
///  - Mỗi nhà là một địa chỉ giao hàng (DeliveryAddress); vỉa hè / vạch qua đường / ngõ là vùng NavMesh "Sidewalks".
///  - Tất cả khối cùng một vật liệu (bảng màu VPalette) nên mỗi ô phố chỉ còn 1 - 2 lệnh vẽ.
/// Dựng lại mỗi lần mở scene / vào game (cùng hạt giống nên giống hệt); scene chỉ lưu object gốc + script này.
/// Menu: Tools > Store Setup > 20. Build Viet City (sieu thi o trung tam).
/// </summary>
[ExecuteAlways]
public partial class VietDistrict : MonoBehaviour
{
    public const string RootName = "VietDistrict_HoanKiem";
    public int seed = 1010; // Thăng Long 1010
    [Tooltip("Mặt bằng siêu thị (x, z) - menu 20 tự đo từ các object của cửa hàng. Mép dưới = mép vỉa hè trước cửa.")]
    public Rect storeRect = Rect.MinMaxRect(-150f, 16f, -70f, 64f);
    public GameObject lampPrefab, benchPrefab;

    public const float RoadTop = 0.1f, LotTop = 0.18f, Walk = 3f, Floor = 3.2f;

    // ================= Bố cục =================
    public struct Line
    {
        public float c0, c1;
        public string id, vn;
        public bool main;
        public Line(float c0, float c1, string id, string vn, bool main = false) { this.c0 = c0; this.c1 = c1; this.id = id; this.vn = vn; this.main = main; }
        public float Center => (c0 + c1) * 0.5f;
        public float Half => (c1 - c0) * 0.5f;
    }

    /// <summary>8 tuyến dọc (x cố định, tây -> đông) và 8 tuyến ngang (z cố định, nam -> bắc).</summary>
    public Line[] X { get; private set; }
    public Line[] Z { get; private set; }
    public Rect City { get; private set; }
    public int AddressCount { get; private set; }

    // Đồ thị đường cho ô tô / xe máy: nút = ngã tư, cạnh = đoạn phố
    public readonly List<Vector3> Nodes = new List<Vector3>();
    public readonly List<float> NodeRadius = new List<float>();
    public readonly List<Vector2Int> Edges = new List<Vector2Int>();
    public readonly List<float> EdgeHalf = new List<float>();
    private int[,] nodeAt;

    private enum Cell { Outer, Store, Lake, OldQuarter, Residential, Commercial, French, Opera, Park, School, Pagoda, Apartments, Market }

    public void EnsureLayout()
    {
        if (X == null) Layout();
    }

    private void Layout()
    {
        Rect s = storeRect;
        var z = new Line[8];
        var x = new Line[8];
        z[3] = new Line(s.yMin - 16f, s.yMin, "Hai Ba Trung", "HAI BÀ TRƯNG", true);
        z[4] = new Line(s.yMax, s.yMax + 10f, "Trang Tien", "TRÀNG TIỀN");
        z[5] = new Line(z[4].c1 + 70f, z[4].c1 + 80f, "Le Thach", "LÊ THẠCH");
        z[6] = new Line(z[5].c1 + 70f, z[5].c1 + 80f, "Hang Bac", "HÀNG BẠC");
        z[7] = new Line(z[6].c1 + 66f, z[6].c1 + 76f, "Hang Chieu", "HÀNG CHIẾU");
        z[2] = new Line(z[3].c0 - 80f, z[3].c0 - 70f, "Ly Thuong Kiet", "LÝ THƯỜNG KIỆT");
        z[1] = new Line(z[2].c0 - 84f, z[2].c0 - 70f, "Tran Hung Dao", "TRẦN HƯNG ĐẠO", true);
        z[0] = new Line(z[1].c0 - 80f, z[1].c0 - 70f, "Nguyen Du", "NGUYỄN DU");

        x[3] = new Line(s.xMin - 10f, s.xMin, "Hang Dao", "HÀNG ĐÀO");
        x[4] = new Line(s.xMax, s.xMax + 10f, "Le Thai To", "LÊ THÁI TỔ");
        x[5] = new Line(x[4].c1 + 76f, x[4].c1 + 86f, "Hang Bai", "HÀNG BÀI");
        x[6] = new Line(x[5].c1 + 76f, x[5].c1 + 86f, "Dinh Tien Hoang", "ĐINH TIÊN HOÀNG");
        x[7] = new Line(x[6].c1 + 66f, x[6].c1 + 80f, "Tran Quang Khai", "TRẦN QUANG KHẢI", true);
        x[2] = new Line(x[3].c0 - 74f, x[3].c0 - 64f, "Hang Ga", "HÀNG GÀ");
        x[1] = new Line(x[2].c0 - 74f, x[2].c0 - 64f, "Duong Thanh", "ĐƯỜNG THÀNH");
        x[0] = new Line(x[1].c0 - 76f, x[1].c0 - 64f, "Phung Hung", "PHÙNG HƯNG", true);
        X = x;
        Z = z;
        City = Rect.MinMaxRect(x[0].c0 - 34f, z[0].c0 - 34f, x[7].c1 + 34f, z[7].c1 + 34f);

        Nodes.Clear(); NodeRadius.Clear(); Edges.Clear(); EdgeHalf.Clear();
        nodeAt = new int[8, 8];
        for (int i = 0; i < 8; i++)
            for (int k = 0; k < 8; k++)
            {
                nodeAt[i, k] = -1;
                if (!JunctionExists(i, k)) continue;
                nodeAt[i, k] = Nodes.Count;
                Nodes.Add(new Vector3(x[i].Center, RoadTop, z[k].Center));
                NodeRadius.Add(Mathf.Max(x[i].Half, z[k].Half) + 1f);
            }
        for (int k = 0; k < 8; k++)
            for (int i = 1; i <= 7; i++)
                if (HasH(k, i)) { Edges.Add(new Vector2Int(nodeAt[i - 1, k], nodeAt[i, k])); EdgeHalf.Add(z[k].Half); }
        for (int i = 0; i < 8; i++)
            for (int k = 1; k <= 7; k++)
                if (HasV(i, k)) { Edges.Add(new Vector2Int(nodeAt[i, k - 1], nodeAt[i, k])); EdgeHalf.Add(x[i].Half); }
    }

    /// <summary>Ô (i, k), i = 0..8 tây -> đông, k = 0..8 nam -> bắc; hàng / cột 0 và 8 là vành ngoài.</summary>
    private Cell Zone(int i, int k)
    {
        if (i <= 0 || i >= 8 || k <= 0 || k >= 8) return Cell.Outer;
        if (i == 4 && k == 4) return Cell.Store;
        if ((i == 5 || i == 6) && (k == 5 || k == 6)) return Cell.Lake;
        if (i == 4 && k == 7) return Cell.Market;
        if (i == 5 && k == 3) return Cell.Opera;
        if ((i == 6 || i == 7) && (k == 2 || k == 3)) return Cell.French;
        if (i == 5 && k == 2) return Cell.Park;
        if (i == 4 && k == 2) return Cell.School;
        if (i == 2 && k == 1) return Cell.Pagoda;
        if ((i == 1 && (k == 1 || k == 2)) || (i == 3 && k == 1) || (i == 2 && k == 2)) return Cell.Apartments;
        if (k >= 5) return Cell.OldQuarter;
        if (k == 4) return i < 4 ? Cell.OldQuarter : Cell.Commercial;
        if (k == 3) return i <= 4 ? Cell.Commercial : Cell.French;
        return Cell.Residential;
    }

    /// <summary>Đoạn phố ngang trên tuyến Z[k], giữa X[i-1] và X[i].</summary>
    private bool HasH(int k, int i)
    {
        if (i < 1 || i > 7 || k < 0 || k > 7) return false;
        return !(Zone(i, k) == Cell.Lake && Zone(i, k + 1) == Cell.Lake);
    }

    /// <summary>Đoạn phố dọc trên tuyến X[i], giữa Z[k-1] và Z[k].</summary>
    private bool HasV(int i, int k)
    {
        if (k < 1 || k > 7 || i < 0 || i > 7) return false;
        return !(Zone(i, k) == Cell.Lake && Zone(i + 1, k) == Cell.Lake);
    }

    private bool JunctionExists(int i, int k) => HasH(k, i) || HasH(k, i + 1) || HasV(i, k) || HasV(i, k + 1);

    private Rect CellRect(int i, int k)
    {
        float xa = i == 0 ? City.xMin : X[i - 1].c1, xb = i == 8 ? City.xMax : X[i].c0;
        float za = k == 0 ? City.yMin : Z[k - 1].c1, zb = k == 8 ? City.yMax : Z[k].c0;
        return Rect.MinMaxRect(xa, za, xb, zb);
    }

    private Rect LakeRect() => Rect.MinMaxRect(X[4].c1, Z[4].c1, X[6].c0, Z[6].c0);

    // ================= Trạng thái dựng =================
    private System.Random rng;
    private readonly VMeshBuilder solid = new VMeshBuilder(), detail = new VMeshBuilder();
    private Transform geo, cols, signsRoot, propsRoot, fxRoot;
    private TMP_FontAsset font;
    private readonly List<Bounds> reserved = new List<Bounds>();
    private readonly List<ParticleSystem> leafSystems = new List<ParticleSystem>();
    private GameObject lastFlush;

#if UNITY_EDITOR
    private static bool reloading;
    [UnityEditor.InitializeOnLoadMethod]
    private static void HookReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => reloading = true;
#endif

    void OnEnable()
    {
        EnsureLayout();
        if (Application.isPlaying && FindAnyObjectByType<GameManager>() == null) return;
        Build();
    }

    void Start()
    {
        if (Application.isPlaying) Build(); // vào Play không nạp lại scene: OnEnable không chạy lại
    }

    void OnDisable()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (reloading) return; // biên dịch lại script: giữ khối đã dựng (không lưu vào scene nhưng còn trong bộ nhớ)
#endif
        Clear();
    }

    /// <summary>Đo lại bố cục, xoá rồi dựng lại toàn bộ (menu Editor gọi).</summary>
    public void Rebuild()
    {
        X = null;
        Clear();
        EnsureLayout();
        Build();
    }

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject c = transform.GetChild(i).gameObject;
            if (!Application.isPlaying)
                foreach (MeshFilter mf in c.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh != VShape.CubeMesh) DestroyImmediate(mf.sharedMesh);
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        leafSystems.Clear();
        AddressCount = 0;
    }

    private void Build()
    {
        if (transform.childCount > 0) return; // đã dựng
        EnsureLayout();
        rng = new System.Random(seed);
        font = TMP_Settings.defaultFontAsset;
        reserved.Clear();
        leafSystems.Clear();
        AddressCount = 0;
        solid.Clear(); detail.Clear();
        geo = Child("Geometry"); cols = Child("Colliders"); signsRoot = Child("Signs"); propsRoot = Child("Props"); fxRoot = Child("Leaves");
        InitFoliage();

        BuildGroundAndBackdrop();
        BuildRoads();
        for (int i = 0; i <= 8; i++)
            for (int k = 0; k <= 8; k++)
                BuildCell(i, k);
        VPalette.Apply();
        ApplySeason(true);
        SetHideFlags(transform);
    }

    void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextSeasonCheck) return;
        nextSeasonCheck = Time.unscaledTime + 2f;
        ApplySeason(false);
    }

    // ================= Nền, phông xa, tường biên =================
    private void BuildGroundAndBackdrop()
    {
        Rect c = City;
        Box(new Vector3(c.center.x, -0.05f, c.center.y), Quaternion.identity, new Vector3(c.width + 300f, 0.06f, c.height + 300f), Ground, false);
        Flush("Ground");
        AddCollider(new Vector3(c.center.x, -0.05f, c.center.y), Quaternion.identity, new Vector3(c.width + 300f, 0.06f, c.height + 300f), "Ground");

        // Tường vô hình quanh thành phố (không cho chạy ra ngoài bản đồ)
        AddCollider(new Vector3(c.center.x, 4f, c.yMin - 1f), Quaternion.identity, new Vector3(c.width + 4f, 8f, 1f), "Bound_S");
        AddCollider(new Vector3(c.center.x, 4f, c.yMax + 1f), Quaternion.identity, new Vector3(c.width + 4f, 8f, 1f), "Bound_N");
        AddCollider(new Vector3(c.xMin - 1f, 4f, c.center.y), Quaternion.identity, new Vector3(1f, 8f, c.height + 4f), "Bound_W");
        AddCollider(new Vector3(c.xMax + 1f, 4f, c.center.y), Quaternion.identity, new Vector3(1f, 8f, c.height + 4f), "Bound_E");

        // Toà cao xa xa (Hà Nội mới) che chân trời
        for (int side = 0; side < 4; side++)
        {
            bool horiz = side < 2;
            float from = horiz ? c.xMin - 60f : c.yMin - 60f, to = horiz ? c.xMax + 60f : c.yMax + 60f;
            for (float s = from; s < to; s += Range(26f, 40f))
            {
                float off = Range(35f, 110f), w = Range(16f, 30f), d = Range(16f, 30f), h = Range(25f, 85f);
                Vector3 p = side switch
                {
                    0 => new Vector3(s, 0f, c.yMin - off),
                    1 => new Vector3(s, 0f, c.yMax + off),
                    2 => new Vector3(c.xMin - off, 0f, s),
                    _ => new Vector3(c.xMax + off, 0f, s),
                };
                Color col = Color.Lerp(new Color(0.6f, 0.64f, 0.7f), Pick(Facades), 0.25f);
                Box(p + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(w, h, d), col, false);
                for (float y = 4f; y < h - 2f; y += 3.4f)
                    Box(p + Vector3.up * y, Quaternion.identity, new Vector3(w + 0.1f, 0.9f, d + 0.1f), Color.Lerp(col, Glass, 0.6f), false);
                if (rng.NextDouble() < 0.3) Box(p + Vector3.up * (h + 3f), Quaternion.identity, new Vector3(0.3f, 6f, 0.3f), Rail, false);
            }
        }
        Flush("Skyline");
    }

    // ================= Đường =================
    private void BuildRoads()
    {
        for (int k = 0; k < 8; k++)
        {
            for (int i = 1; i <= 7; i++)
                if (HasH(k, i))
                    Segment(true, Z[k], X[i - 1].c1, X[i].c0, Zone(i, k) == Cell.OldQuarter || Zone(i, k + 1) == Cell.OldQuarter, k == 3 && (i == 2 || i == 6) || k == 4 && i == 3);
            Flush("Road_" + Z[k].id);
        }
        for (int i = 0; i < 8; i++)
        {
            for (int k = 1; k <= 7; k++)
                if (HasV(i, k))
                    Segment(false, X[i], Z[k - 1].c1, Z[k].c0, Zone(i, k) == Cell.OldQuarter || Zone(i + 1, k) == Cell.OldQuarter, false);
            Flush("Road_" + X[i].id);
        }
        for (int i = 0; i < 8; i++)
            for (int k = 0; k < 8; k++)
                if (nodeAt[i, k] >= 0) Junction(i, k);
        Flush("Junctions");
    }

    private void Segment(bool alongX, Line L, float a0, float a1, bool lanterns, bool banner)
    {
        float len = a1 - a0, w = L.c1 - L.c0, mid = (a0 + a1) * 0.5f;
        Vector3 P(float along, float across, float y) => alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
        Vector3 S(float along, float y, float across) => alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
        Quaternion q = Quaternion.identity;

        Box(P(mid, L.Center, RoadTop - 0.05f), q, S(len, 0.1f, w), Asphalt, false);
        AddCollider(P(mid, L.Center, RoadTop - 0.05f), q, S(len, 0.1f, w), "Road " + L.id);
        // Vạch mép đường (làn xe máy sát vỉa hè)
        Box(P(mid, L.c0 + 0.6f, RoadTop + 0.004f), q, S(len - 6f, 0.01f, 0.12f), Paint, false);
        Box(P(mid, L.c1 - 0.6f, RoadTop + 0.004f), q, S(len - 6f, 0.01f, 0.12f), Paint, false);
        if (L.main)
        {
            // Dải phân cách giữa có bồn cây
            Box(P(mid, L.Center, RoadTop + 0.13f), q, S(len - 7f, 0.26f, 1.2f), Curb, false);
            Box(P(mid, L.Center, RoadTop + 0.265f), q, S(len - 7.2f, 0.02f, 1.0f), Grass, false);
            AddCollider(P(mid, L.Center, RoadTop + 0.2f), q, S(len - 7f, 0.4f, 1.2f), "Median");
            for (float s = a0 + 5f; s < a1 - 5f; s += 3.2f)
                Blob(P(s, L.Center, RoadTop + 0.55f), Quaternion.Euler(0f, Range(0f, 360f), 0f), new Vector3(Range(0.8f, 1.1f), Range(0.5f, 0.8f), 0.8f), Pick(Bushes), false);
        }
        else
        {
            for (float s = a0 + 5f; s < a1 - 6f; s += 6f)
                Box(P(s + 1.5f, L.Center, RoadTop + 0.005f), q, S(3f, 0.01f, 0.14f), Paint, false);
        }
        // Vạch qua đường hai đầu (người đi bộ chỉ qua đường ở đây)
        foreach (float e in new[] { a0 + 2f, a1 - 2f })
        {
            for (float c = L.c0 + 0.7f; c < L.c1 - 0.5f; c += 1.0f)
            {
                if (L.main && Mathf.Abs(c - L.Center) < 0.7f) continue;
                Box(P(e, c, RoadTop + 0.006f), q, S(3f, 0.012f, 0.55f), Paint, false);
            }
            MarkVolume(P(e, L.Center, RoadTop), S(3.2f, 1.2f, w + 1.6f));
        }
        // Nắp cống
        for (int m = 0; m < 2; m++)
            Prism(P(Range(a0 + 8f, a1 - 8f), L.Center + Range(-L.Half + 1.5f, L.Half - 1.5f), RoadTop + 0.005f), q, new Vector3(0.7f, 0.01f, 0.7f), Rail, false);

        if (lanterns) for (float s = a0 + 12f; s < a1 - 10f; s += 22f) LanternString(P(s, L.c0 - 1.0f, 0f), P(s, L.c1 + 1.0f, 0f));
        if (banner) Banner(P(mid, L.c0 - 0.8f, 0f), P(mid, L.c1 + 0.8f, 0f));
        if (L.main && len > 50f && rng.NextDouble() < 0.7) BusStop(P(mid + 10f, L.c0 - 1.6f, LotTop), alongX ? Vector3.forward : Vector3.right);
    }

    private void Junction(int i, int k)
    {
        Line a = X[i], b = Z[k];
        Box(new Vector3(a.Center, RoadTop - 0.05f, b.Center), Quaternion.identity, new Vector3(a.c1 - a.c0, 0.1f, b.c1 - b.c0), Asphalt, false);
        AddCollider(new Vector3(a.Center, RoadTop - 0.05f, b.Center), Quaternion.identity, new Vector3(a.c1 - a.c0, 0.1f, b.c1 - b.c0), "Junction");
        // Biển tên phố (xanh dương, chữ trắng) ở góc đông bắc
        Vector3 post = new Vector3(a.c1 + 1.1f, LotTop, b.c1 + 1.1f);
        if (Zone(i + 1, k + 1) != Cell.Store)
        {
            Box(post + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.08f, 3f, 0.08f), Rail, false);
            StreetPlate(post + new Vector3(0f, 2.85f, -0.06f), Vector3.forward, b);
            StreetPlate(post + new Vector3(-0.06f, 2.4f, 0f), Vector3.right, a);
        }
        if (a.main || b.main)
        {
            TrafficLight(new Vector3(a.c0 - 1.0f, LotTop, b.c0 - 1.0f));
            TrafficLight(new Vector3(a.c1 + 1.0f, LotTop, b.c1 + 1.0f) + new Vector3(0.6f, 0f, -0.6f));
        }
    }

    private void StreetPlate(Vector3 p, Vector3 viewDir, Line l)
    {
        Quaternion r = Quaternion.LookRotation(viewDir);
        Box(p, r, new Vector3(1.9f, 0.42f, 0.04f), StreetBlue, false);
        Box(p + viewDir * 0.012f, r, new Vector3(1.96f, 0.48f, 0.03f), White, false);
        Sign(p - viewDir * 0.03f, viewDir, new Vector2(1.8f, 0.38f), (l.main ? "ĐƯỜNG " : "PHỐ ") + l.vn, White);
    }

    private void TrafficLight(Vector3 p)
    {
        Prism(p + Vector3.up * 1.9f, Quaternion.identity, new Vector3(0.14f, 3.8f, 0.14f), Rail, true);
        int on = rng.Next(3);
        foreach (Vector3 dir in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
        {
            Quaternion r = Quaternion.LookRotation(dir);
            Vector3 head = p + Vector3.up * 3.4f - dir * 0.18f;
            Box(head, r, new Vector3(0.3f, 0.85f, 0.22f), Rail, false);
            Color[] lamp = { new Color(1f, 0.15f, 0.1f), new Color(1f, 0.75f, 0.1f), new Color(0.2f, 1f, 0.35f) };
            for (int l = 0; l < 3; l++)
            {
                Color c = l == on ? lamp[l] : Color.Lerp(lamp[l], Rail, 0.8f);
                Prism(head + Vector3.up * (0.26f - l * 0.26f) - dir * 0.115f, r * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.18f, 0.02f, 0.18f), c, false);
            }
            on = (on + 1) % 3;
        }
    }

    // ================= Ô phố =================
    private struct Edge
    {
        public Vector3 a, b, n;
        public bool street;
        public Line line;
        public float trim;
        public Vector3 U => (b - a).normalized;
        public Vector3 In => -n;
        public float Len => Vector3.Distance(a, b);
    }

    private Edge[] EdgesOf(int i, int k, Rect r)
    {
        var e = new Edge[4];
        e[0] = new Edge { a = new Vector3(r.xMin, 0f, r.yMin), b = new Vector3(r.xMax, 0f, r.yMin), n = Vector3.back, street = HasH(k - 1, i), line = k - 1 >= 0 ? Z[k - 1] : default };
        e[1] = new Edge { a = new Vector3(r.xMax, 0f, r.yMax), b = new Vector3(r.xMin, 0f, r.yMax), n = Vector3.forward, street = HasH(k, i), line = k <= 7 ? Z[k] : default };
        e[2] = new Edge { a = new Vector3(r.xMin, 0f, r.yMax), b = new Vector3(r.xMin, 0f, r.yMin), n = Vector3.left, street = HasV(i - 1, k), line = i - 1 >= 0 ? X[i - 1] : default, trim = 1f };
        e[3] = new Edge { a = new Vector3(r.xMax, 0f, r.yMin), b = new Vector3(r.xMax, 0f, r.yMax), n = Vector3.right, street = HasV(i, k), line = i <= 7 ? X[i] : default, trim = 1f };
        return e;
    }

    private void BuildCell(int i, int k)
    {
        Cell type = Zone(i, k);
        if (type == Cell.Lake)
        {
            if (i == 5 && k == 5) BuildLake(LakeRect());
            return;
        }
        Rect r = CellRect(i, k);
        string name = $"Cell_{i}_{k}_{type}";
        Edge[] edges = EdgesOf(i, k, r);
        reserved.Clear();
        LotAndSidewalk(r, name, type == Cell.Park ? Grass : type == Cell.School ? Yard : type == Cell.Pagoda ? Grass : Lot);
        if (type == Cell.Store) { Flush(name); return; }

        foreach (Edge e in edges) EdgeFurniture(e, type);

        var placed = new List<Bounds>();
        float tubeDepth = Mathf.Min(13f, Mathf.Min(r.width, r.height) * 0.5f - Walk - 0.5f);
        switch (type)
        {
            case Cell.OldQuarter:
            case Cell.Residential:
            case Cell.Outer:
                if (type != Cell.Outer) Alley(r, tubeDepth, edges[2], edges[3], type);
                foreach (Edge e in edges) Row(e, tubeDepth, Kind.Tube, type, placed);
                if (type == Cell.Outer) OuterFill(r, edges);
                break;
            case Cell.Commercial:
                foreach (Edge e in edges) Row(e, tubeDepth, Kind.Tall, type, placed);
                Courtyard(r, tubeDepth);
                break;
            case Cell.French:
                foreach (Edge e in edges) Row(e, Mathf.Min(16f, tubeDepth + 2f), Kind.Villa, type, placed);
                Garden(r, 18f);
                break;
            case Cell.Opera:
                BuildOperaHouse(r, edges[1]);
                foreach (Edge e in edges) Row(e, Mathf.Min(16f, tubeDepth + 2f), Kind.Villa, type, placed);
                break;
            case Cell.Apartments:
                foreach (Edge e in edges) Row(e, 5f, Kind.Kiosk, type, placed);
                BuildApartments(r);
                break;
            case Cell.Park: BuildPark(r); break;
            case Cell.School:
                BuildSchool(r, edges[1]);
                foreach (Edge e in edges) Row(e, 5f, Kind.Kiosk, type, placed);
                break;
            case Cell.Pagoda:
                BuildPagoda(r, edges[1]);
                foreach (Edge e in edges) Row(e, tubeDepth, Kind.Tube, Cell.Residential, placed);
                break;
            case Cell.Market:
                BuildMarket(r, edges[0]);
                foreach (Edge e in edges) Row(e, 6f, Kind.Kiosk, type, placed);
                break;
        }
        Flush(name);
    }

    private void LotAndSidewalk(Rect b, string name, Color lot)
    {
        Box(new Vector3(b.center.x, LotTop * 0.5f, b.center.y), Quaternion.identity, new Vector3(b.width - Walk * 2f + 0.02f, LotTop, b.height - Walk * 2f + 0.02f), lot, false);
        AddCollider(new Vector3(b.center.x, LotTop * 0.5f, b.center.y), Quaternion.identity, new Vector3(b.width, LotTop, b.height), name + "_Lot");
        Flush(name + "_Lot");

        var strips = new[]
        {
            Rect.MinMaxRect(b.xMin, b.yMin, b.xMax, b.yMin + Walk), Rect.MinMaxRect(b.xMin, b.yMax - Walk, b.xMax, b.yMax),
            Rect.MinMaxRect(b.xMin, b.yMin + Walk, b.xMin + Walk, b.yMax - Walk), Rect.MinMaxRect(b.xMax - Walk, b.yMin + Walk, b.xMax, b.yMax - Walk),
        };
        foreach (Rect s in strips)
        {
            Box(new Vector3(s.center.x, LotTop * 0.5f + 0.005f, s.center.y), Quaternion.identity, new Vector3(s.width, LotTop + 0.01f, s.height), Sidewalk, false);
            // Đường kẻ gạch lát (ô 0.6 m) cho đỡ trơn
            bool alongX = s.width > s.height;
            float len = alongX ? s.width : s.height;
            for (float t = 1.2f; t < len - 0.6f; t += 1.2f)
            {
                Vector3 p = alongX ? new Vector3(s.xMin + t, LotTop + 0.012f, s.center.y) : new Vector3(s.center.x, LotTop + 0.012f, s.yMin + t);
                Box(p, Quaternion.identity, alongX ? new Vector3(0.04f, 0.004f, s.height - 0.3f) : new Vector3(s.width - 0.3f, 0.004f, 0.04f), SidewalkLine, false);
            }
        }
        Box(new Vector3(b.center.x, LotTop * 0.5f + 0.012f, b.yMin + 0.12f), Quaternion.identity, new Vector3(b.width, LotTop + 0.025f, 0.24f), Curb, false);
        Box(new Vector3(b.center.x, LotTop * 0.5f + 0.012f, b.yMax - 0.12f), Quaternion.identity, new Vector3(b.width, LotTop + 0.025f, 0.24f), Curb, false);
        Box(new Vector3(b.xMin + 0.12f, LotTop * 0.5f + 0.012f, b.center.y), Quaternion.identity, new Vector3(0.24f, LotTop + 0.025f, b.height), Curb, false);
        Box(new Vector3(b.xMax - 0.12f, LotTop * 0.5f + 0.012f, b.center.y), Quaternion.identity, new Vector3(0.24f, LotTop + 0.025f, b.height), Curb, false);
        MarkSidewalk(Flush(name + "_Sidewalk"));
    }

    private enum Kind { Tube, Tall, Villa, Kiosk }

    /// <summary>Một dãy nhà dọc một cạnh ô phố (chỉ cạnh có đường).</summary>
    private void Row(Edge e, float depth, Kind kind, Cell type, List<Bounds> placed)
    {
        if (!e.street) return;
        Vector3 u = e.U, vin = e.In;
        float len = e.Len;
        Vector3 front = e.a + vin * Walk + Vector3.up * LotTop;
        float start = Walk + e.trim * (depth + 0.3f), end = len - Walk - e.trim * (depth + 0.3f);
        if (kind == Kind.Kiosk) { start = Walk + e.trim * 6f; end = len - Walk - e.trim * 6f; }
        int number = 1 + (e.n == Vector3.back || e.n == Vector3.left ? 1 : 0) + rng.Next(0, 3) * 2;
        float s = start;
        while (s < end - 3.2f)
        {
            float w = kind switch
            {
                Kind.Tall => Range(5f, 8f),
                Kind.Villa => Range(11f, 15f),
                Kind.Kiosk => Range(3f, 4.5f),
                _ => Range(3.6f, 6.2f),
            };
            if (s + w > end) w = end - s;
            if (w < 3f) break;
            Vector3 c = front + u * (s + w * 0.5f) + vin * (depth * 0.5f);
            var bb = new Bounds(c + Vector3.up * 5f, Abs(u * w) + Vector3.up * 10f + Abs(vin * depth));
            Bounds test = bb;
            test.Expand(new Vector3(-0.3f, 0f, -0.3f));
            if (Overlaps(placed, test) || Overlaps(reserved, test)) { s += 1.5f; continue; }
            placed.Add(bb);
            string address = $"{number} {e.line.id}";
            Vector3 o = front + u * s;
            switch (kind)
            {
                case Kind.Villa: Villa(o, u, vin, w, depth, address); break;
                case Kind.Kiosk: Kiosk(o, u, vin, w, depth, address, e.line); break;
                default: TubeHouse(o, u, vin, w, depth, address, e.line, type, kind == Kind.Tall, false); break;
            }
            number += 2;
            s += w + (kind == Kind.Villa ? Range(2.5f, 4f) : kind == Kind.Kiosk ? 0.1f : 0.05f);
        }
    }

    /// <summary>Ngõ xuyên giữa ô phố (theo trục x), hai bên là nhà ống nhỏ; cổng ngõ có biển "NGÕ ..." ở hai đầu.</summary>
    private void Alley(Rect r, float edgeDepth, Edge left, Edge right, Cell type)
    {
        if (r.width < 50f || r.height < 52f) return;
        const float half = 1.7f;
        float zc = r.center.y;
        reserved.Add(new Bounds(new Vector3(r.center.x, 5f, zc), new Vector3(r.width + 2f, 20f, half * 2f + 0.6f)));
        float x0 = r.xMin + Walk, x1 = r.xMax - Walk;
        Box(new Vector3((x0 + x1) * 0.5f, LotTop + 0.012f, zc), Quaternion.identity, new Vector3(x1 - x0, 0.02f, half * 2f), AlleyFloor, false);
        MarkVolume(new Vector3((x0 + x1) * 0.5f, LotTop, zc), new Vector3(x1 - x0 + 1.5f, 1.5f, half * 2f));
        Line street = left.street ? left.line : right.line;
        int ngo = 2 + 2 * rng.Next(3, 60);
        if (left.street) AlleyGate(new Vector3(x0 + 0.4f, LotTop, zc), Vector3.right, $"NGÕ {ngo} {left.line.vn}");
        if (right.street) AlleyGate(new Vector3(x1 - 0.4f, LotTop, zc), Vector3.left, $"NGÕ {ngo + 1} {right.line.vn}");

        float ix0 = r.xMin + Walk + edgeDepth + 0.6f, ix1 = r.xMax - Walk - edgeDepth - 0.6f;
        float innerDepth = Mathf.Min(10f, r.height * 0.5f - Walk - edgeDepth - half - 0.8f);
        if (innerDepth < 5f || ix1 - ix0 < 8f) return;
        int num = 1;
        foreach (int side in new[] { 1, -1 })
        {
            Vector3 vin = side > 0 ? Vector3.forward : Vector3.back;
            Vector3 u = side > 0 ? Vector3.right : Vector3.left;
            Vector3 o0 = new Vector3(side > 0 ? ix0 : ix1, LotTop, zc + side * half);
            float L = ix1 - ix0, s = 0f;
            while (s < L - 3.2f)
            {
                float w = Range(3.4f, 5.2f);
                if (s + w > L) w = L - s;
                if (w < 3.2f) break;
                TubeHouse(o0 + u * s, u, vin, w, innerDepth, $"{num} Ngo {ngo} {street.id}", street, type, false, true);
                num++;
                s += w + 0.05f;
            }
        }
        // Dây phơi + dây điện vắt ngang ngõ
        for (float x = ix0 + 3f; x < ix1 - 2f; x += 7f)
        {
            Cable(new Vector3(x, LotTop + 5.4f, zc - half), new Vector3(x + Range(-1.5f, 1.5f), LotTop + 5.6f, zc + half), 0.25f);
            if (rng.NextDouble() < 0.5) Laundry(new Vector3(x + 2f, LotTop + 3.3f, zc - half), new Vector3(x + 2f, LotTop + 3.3f, zc + half));
        }
    }

    private void AlleyGate(Vector3 p, Vector3 inward, string text)
    {
        Vector3 side = Vector3.Cross(Vector3.up, inward);
        Box(p + side * 1.75f + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.18f, 3.2f, 0.18f), Rail, false);
        Box(p - side * 1.75f + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.18f, 3.2f, 0.18f), Rail, false);
        Quaternion r = Quaternion.LookRotation(inward);
        Box(p + Vector3.up * 3.3f, r, new Vector3(3.8f, 0.6f, 0.08f), StreetBlue, false);
        Sign(p + Vector3.up * 3.3f - inward * 0.05f, inward, new Vector2(3.6f, 0.5f), text, White);
    }

    private void OuterFill(Rect r, Edge[] edges)
    {
        // Vành ngoài: phía sau dãy nhà là rặng cây vàng che mép bản đồ
        for (int n = 0; n < 14; n++)
        {
            Vector3 p = new Vector3(Range(r.xMin + 4f, r.xMax - 4f), LotTop, Range(r.yMin + 4f, r.yMax - 4f));
            var b = new Bounds(p + Vector3.up * 3f, new Vector3(3f, 6f, 3f));
            if (Overlaps(reserved, b)) continue;
            bool nearStreet = false;
            foreach (Edge e in edges)
                if (e.street && DistToEdge(p, e) < 18f) nearStreet = true;
            if (!nearStreet) PlantTree(p, Range(1f, 1.4f), false);
        }
    }

    private static float DistToEdge(Vector3 p, Edge e)
    {
        Vector3 d = e.b - e.a, w = p - e.a;
        float t = Mathf.Clamp01(Vector3.Dot(w, d) / d.sqrMagnitude);
        Vector3 c = e.a + d * t;
        c.y = p.y;
        return Vector3.Distance(p, c);
    }

    // ================= Gộp mesh =================
    /// <summary>Gộp các khối đã thêm thành tối đa 2 mesh (có / không đổ bóng) dưới object con name.</summary>
    private GameObject Flush(string name)
    {
        if (solid.Count == 0 && detail.Count == 0) { lastFlush = null; return null; }
        var parent = new GameObject(name).transform;
        parent.SetParent(geo, false);
        Emit(parent, solid, name + "_S", true);
        Emit(parent, detail, name + "_D", false);
        lastFlush = parent.gameObject;
        return lastFlush;
    }

    private static void Emit(Transform parent, VMeshBuilder mb, string name, bool shadow)
    {
        if (mb.Count == 0) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = VPalette.Material;
        r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        mb.Clear();
    }

    private void Box(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) => (shadow ? solid : detail).Add(VShape.Cube, Matrix4x4.TRS(c, r, s), col);
    private void Prism(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) => (shadow ? solid : detail).Add(VShape.Prism, Matrix4x4.TRS(c, r, s), col);
    private void Blob(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) => (shadow ? solid : detail).Add(VShape.Blob, Matrix4x4.TRS(c, r, s), col);
    private void Cone(Vector3 c, Quaternion r, Vector3 s, Color col, bool shadow) => (shadow ? solid : detail).Add(VShape.Cone, Matrix4x4.TRS(c, r, s), col);

    // ================= Va chạm / địa chỉ / NavMesh =================
    private GameObject AddCollider(Vector3 center, Quaternion rot, Vector3 size, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(cols, false);
        go.transform.SetPositionAndRotation(center, rot);
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    private void HouseCollider(Vector3 center, Quaternion rot, Vector3 size, string address, Vector3 door)
    {
        GameObject go = AddCollider(center, rot, size, address);
        var a = go.AddComponent<DeliveryAddress>();
        a.address = address;
        a.doorPoint = new Vector3(door.x, LotTop + 0.05f, door.z);
        AddressCount++;
    }

    private void Address(string address, Vector3 door, Vector3 home)
    {
        var go = new GameObject(address);
        go.transform.SetParent(cols, false);
        go.transform.position = home;
        var a = go.AddComponent<DeliveryAddress>();
        a.address = address;
        a.doorPoint = new Vector3(door.x, LotTop + 0.05f, door.z);
        AddressCount++;
    }

    /// <summary>Vùng NavMesh "Sidewalks" cho cả object (người đi bộ chỉ đi vỉa hè) - dùng NavMeshModifier nếu có gói AI Navigation.</summary>
    private static void MarkSidewalk(GameObject go)
    {
        if (go == null) return;
        System.Type t = System.Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
        int area = UnityEngine.AI.NavMesh.GetAreaFromName("Sidewalks");
        if (t == null || area < 0) return;
        Component mod = go.AddComponent(t);
        t.GetProperty("overrideArea")?.SetValue(mod, true);
        t.GetProperty("area")?.SetValue(mod, area);
    }

    private void MarkVolume(Vector3 center, Vector3 size)
    {
        System.Type t = System.Type.GetType("Unity.AI.Navigation.NavMeshModifierVolume, Unity.AI.Navigation");
        int area = UnityEngine.AI.NavMesh.GetAreaFromName("Sidewalks");
        if (t == null || area < 0) return;
        var go = new GameObject("WalkVolume");
        go.transform.SetParent(cols, false);
        go.transform.position = center;
        Component v = go.AddComponent(t);
        t.GetProperty("size")?.SetValue(v, size);
        t.GetProperty("center")?.SetValue(v, Vector3.zero);
        t.GetProperty("area")?.SetValue(v, area);
    }

    // ================= Biển chữ =================
    /// <summary>Chữ 3D (TextMeshPro). viewDir = hướng người xem nhìn vào biển.</summary>
    private void Sign(Vector3 pos, Vector3 viewDir, Vector2 size, string text, Color color)
    {
        var go = new GameObject("Sign");
        go.transform.SetParent(signsRoot, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(viewDir.sqrMagnitude > 0f ? viewDir : Vector3.forward, Vector3.up));
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.rectTransform.sizeDelta = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.1f;
        tmp.fontSizeMax = 8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.text = Supported(text);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) { mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; }
    }

    /// <summary>Font không có dấu tiếng Việt thì bỏ dấu (tránh ô vuông).</summary>
    private string Supported(string s)
    {
        if (font == null || font.HasCharacters(StripTags(s), out uint[] _, true, true)) return s;
        string d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('Đ', 'D').Replace('đ', 'd');
    }

    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "").Replace("\n", "");

    // ================= Tiện ích =================
    private Transform Child(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        return t;
    }

    private void SetHideFlags(Transform t)
    {
        if (Application.isPlaying) return;
        // Trong Editor: không lưu vào scene (dựng lại mỗi lần mở scene)
        foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
            if (c != t) c.gameObject.hideFlags = HideFlags.DontSave;
    }

    private float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    private bool Chance(double p) => rng.NextDouble() < p;
    private T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static bool Overlaps(List<Bounds> list, Bounds b)
    {
        foreach (Bounds x in list) if (x.Intersects(b)) return true;
        return false;
    }
}
