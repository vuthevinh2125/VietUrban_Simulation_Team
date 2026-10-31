using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Khách đánh giá cửa hàng (1–5 sao) khi ra về, dựa trên 4 tiêu chí:
///  - Hàng hoá: có đủ món muốn mua không (kệ trống = mất điểm)
///  - Giá: đắt so với giá thị trường thì mất điểm, rẻ thì cộng điểm
///  - Thanh toán: chờ ở quầy lâu / bỏ về vì hàng quá dài
///  - Vệ sinh: số vết bẩn trong cửa hàng
/// Điểm cửa hàng = trung bình 50 đánh giá gần nhất. Điểm cao thì khách tới đông hơn (x0.7 → x1.3).
/// HUD hiện số sao dưới bảng thời tiết; xem chi tiết ở máy tính → Stats → Reviews.
/// </summary>
public class ReviewManager : MonoBehaviour
{
    [Serializable]
    public class Review
    {
        public int day;
        public string name;
        public string type; // kiểu khách (Student, Office worker...), trống với đánh giá cũ
        public int stars;
        public float stock, price, checkout, clean; // điểm từng tiêu chí 1..5, 0 = không áp dụng
        public string comment;
    }

    /// <summary>Những gì khách gặp trong một lần ghé (NPCShopper ghi lại).</summary>
    public class Visit
    {
        public int wanted, got, tooExpensive, dirtySkips, pricey, greatPrice, maxDirt;
        public bool noStock, checkedOut, gaveUpQueue;
        public float queueStart = -1f, queueSeconds;
        public string customerType = "";
        public float queueTolerance = 35f; // xếp hàng lâu hơn mức này mới bắt đầu bực (dân văn phòng / shipper ít kiên nhẫn hơn)
    }

    public const int WindowSize = 50;     // số đánh giá gần nhất tính điểm cửa hàng
    private const int KeepMax = 300;      // giữ tối đa trong file save
    public const int MinForEffect = 5;    // đủ số đánh giá mới bắt đầu ảnh hưởng lượng khách

    public static ReviewManager Instance { get; private set; }
    public static event Action OnChanged;

    private readonly List<Review> reviews = new List<Review>();
    private Image[] hudStars;
    private TMP_Text hudText;
    private static Sprite starSprite;

    public static IReadOnlyList<Review> All => Instance != null ? Instance.reviews : (IReadOnlyList<Review>)Array.Empty<Review>();
    public static IEnumerable<Review> Recent => All.Skip(Mathf.Max(0, All.Count - WindowSize));
    public static int RecentCount => Mathf.Min(All.Count, WindowSize);
    public static float Average => RecentCount > 0 ? (float)Recent.Average(r => r.stars) : 0f;

    /// <summary>Hệ số lượng khách theo điểm: 1★ = x0.7, 3★ = x1.0, 5★ = x1.3.</summary>
    public static float CustomerMultiplier =>
        RecentCount < MinForEffect ? 1f : Mathf.Lerp(0.7f, 1.3f, Mathf.InverseLerp(1f, 5f, Average));

    /// <summary>Trung bình một tiêu chí trong các đánh giá gần nhất (bỏ qua chỗ không áp dụng).</summary>
    public static float FactorAverage(Func<Review, float> f)
    {
        var vals = Recent.Select(f).Where(v => v > 0f).ToList();
        return vals.Count > 0 ? vals.Average() : 0f;
    }

    /// <summary>Ba kiểu khách đông nhất trong ngày (theo đánh giá), vd. "Student 4 · Homemaker 3 · Retiree 1".</summary>
    public static string VisitorMix(int day)
    {
        var top = All.Where(r => r.day == day && !string.IsNullOrEmpty(r.type))
                     .GroupBy(r => r.type).OrderByDescending(g => g.Count()).Take(3)
                     .Select(g => $"{g.Key} {g.Count()}").ToList();
        return top.Count > 0 ? string.Join(" · ", top) : "";
    }

    public static void ForDay(int day, out float average, out int count)
    {
        var list = All.Where(r => r.day == day).ToList();
        count = list.Count;
        average = count > 0 ? (float)list.Average(r => r.stars) : 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        CreateIfMissing();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => CreateIfMissing();

    private static void CreateIfMissing()
    {
        if (Instance == null) new GameObject("ReviewManager").AddComponent<ReviewManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    void Awake()
    {
        Instance = this;
        BuildHud();
        RefreshHud();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ================= Chấm điểm =================
    public static Review Submit(Visit v)
    {
        if (Instance == null) CreateIfMissing();
        var r = new Review
        {
            day = GameManager.Instance != null ? GameManager.Instance.currentDay : 1,
            name = RandomName(),
            type = v.customerType,
        };

        // Hàng hoá
        int shortfall = Mathf.Max(0, v.wanted - v.got - v.tooExpensive - v.dirtySkips);
        r.stock = v.got == 0 && v.noStock ? 1f : Mathf.Clamp(5f - shortfall * 1.2f, 1f, 5f);

        // Giá (không xem được món nào thì không chấm)
        bool sawItems = v.got + v.tooExpensive + v.pricey + v.greatPrice > 0;
        r.price = sawItems ? Mathf.Clamp(4.5f - v.tooExpensive * 1.3f - v.pricey * 0.6f + v.greatPrice * 0.5f, 1f, 5f) : 0f;

        // Thanh toán
        if (v.gaveUpQueue) r.checkout = 1f;
        else if (v.checkedOut) r.checkout = Mathf.Clamp(5f - Mathf.Max(0f, v.queueSeconds - v.queueTolerance) / 25f, 1f, 5f);
        else r.checkout = 0f;

        // Vệ sinh
        r.clean = Mathf.Clamp(5f - v.maxDirt * 0.4f - v.dirtySkips * 0.8f, 1f, 5f);

        float sum = 0f, weight = 0f;
        void Add(float score, float w) { if (score > 0f) { sum += score * w; weight += w; } }
        Add(r.stock, 0.3f);
        Add(r.price, 0.3f);
        Add(r.checkout, 0.25f);
        Add(r.clean, 0.15f);
        float overall = weight > 0f ? sum / weight : 3f;
        overall += UnityEngine.Random.Range(-0.35f, 0.35f); // mỗi người một tính
        r.stars = Mathf.Clamp(Mathf.RoundToInt(overall), 1, 5);
        r.comment = Comment(r, v);

        Instance.reviews.Add(r);
        if (Instance.reviews.Count > KeepMax) Instance.reviews.RemoveRange(0, Instance.reviews.Count - KeepMax);
        Instance.RefreshHud();
        OnChanged?.Invoke();
        return r;
    }

    private static readonly string[] Names =
    {
        "Minh", "Lan", "Tuan", "Hoa", "Nam", "Linh", "Huy", "Mai", "Phong", "Thao", "Duc", "Ngoc", "Khoa", "Trang",
        "Bao", "Vy", "Long", "Hanh", "Quang", "Yen", "Son", "Thu", "Dung", "Nhi", "Khanh", "Tam", "Hai", "Uyen",
    };
    private static readonly string Initials = "NTLPHVDBGKQS";

    private static string RandomName() =>
        $"{Names[UnityEngine.Random.Range(0, Names.Length)]} {Initials[UnityEngine.Random.Range(0, Initials.Length)]}.";

    private static string Pick(params string[] options) => options[UnityEngine.Random.Range(0, options.Length)];

    private static string Comment(Review r, Visit v)
    {
        // Tiêu chí tệ nhất (nếu dưới 3.2) -> chê; đánh giá cao -> khen tiêu chí tốt nhất
        var factors = new List<(string key, float score)>
        {
            ("stock", r.stock), ("price", r.price), ("checkout", r.checkout), ("clean", r.clean),
        }.Where(f => f.score > 0f).ToList();
        if (factors.Count == 0) return "It was okay.";

        var worst = factors.OrderBy(f => f.score).First();
        if (worst.score < 3.2f && r.stars <= 3)
        {
            switch (worst.key)
            {
                case "stock": return v.got == 0 ? Pick("Nothing on the shelves!", "Couldn't find anything to buy.") : Pick("Half the shelves were empty.", "Couldn't find what I came for.", "Please restock more often.");
                case "price": return Pick("Prices are way too high.", "Too expensive for me.", "Cheaper at the market down the street.");
                case "checkout": return v.gaveUpQueue ? "Gave up waiting in line." : Pick("Waited forever at the checkout.", "The line was so slow!", "You need more cashiers.");
                default: return Pick("The floor was really dirty.", "Please clean the store!", "Messy and dirty.");
            }
        }
        if (r.stars >= 4)
        {
            var best = factors.OrderByDescending(f => f.score).First();
            switch (best.key)
            {
                case "stock": return Pick("Everything I needed was in stock!", "Shelves fully stocked, love it.");
                case "price": return Pick("Great prices!", "Very good value.", "Cheapest store around!");
                case "checkout": return Pick("Fast checkout!", "No waiting at all.", "Quick and easy.");
                default: return Pick("Clean and tidy store.", "Spotless!", "Very clean, nice place.");
            }
        }
        return Pick("It was okay.", "Not bad.", "Average store.", "Could be better.");
    }

    // ================= Lưu / nạp =================
    public static List<Review> Export() => Instance != null ? new List<Review>(Instance.reviews) : new List<Review>();

    public static void Import(List<Review> list)
    {
        if (Instance == null) CreateIfMissing();
        Instance.reviews.Clear();
        if (list != null) Instance.reviews.AddRange(list.Where(r => r != null));
        Instance.RefreshHud();
        OnChanged?.Invoke();
    }

    // ================= HUD =================
    private void BuildHud()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, 50);
        GameObject panel = UIKit.Panel("RatingInfo", canvas.transform, new Vector2(300, 30), Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -158f); // dưới dòng thời tiết, cùng cột với khung tiền
        hudStars = StarRow(panel.transform, new Vector2(-89f, 0f), 18f);
        hudText = UIKit.Text(panel.transform, "", 16, new Vector2(180, 28), new Vector2(52f, 0f), TextAlignmentOptions.Left);
        hudText.enableAutoSizing = true; hudText.fontSizeMin = 11; hudText.fontSizeMax = 16;
        hudText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RefreshHud()
    {
        if (hudText == null) return;
        int n = RecentCount;
        float avg = Average;
        SetStars(hudStars, avg);
        hudText.text = n == 0 ? "No reviews yet" : $"{avg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5  ({All.Count} reviews)";
    }

    // ================= Vẽ ngôi sao (dùng chung) =================
    public static readonly Color StarOn = new Color(1f, 0.8f, 0.2f, 1f);
    public static readonly Color StarOff = new Color(1f, 1f, 1f, 0.18f);

    /// <summary>Tạo hàng 5 ngôi sao (mỗi sao có nền mờ + lớp vàng tô theo tỉ lệ). Trả về 5 lớp vàng.</summary>
    public static Image[] StarRow(Transform parent, Vector2 center, float size)
    {
        var fills = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            Vector2 pos = center + new Vector2((i - 2) * (size + 3f), 0f);
            GameObject bg = UIKit.Rect("Star", parent, new Vector2(size, size), pos);
            Image b = bg.AddComponent<Image>();
            b.sprite = StarSprite; b.color = StarOff; b.raycastTarget = false;
            GameObject fg = UIKit.Rect("Fill", bg.transform, new Vector2(size, size), Vector2.zero);
            Image f = fg.AddComponent<Image>();
            f.sprite = StarSprite; f.color = StarOn; f.raycastTarget = false;
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = (int)Image.OriginHorizontal.Left;
            fills[i] = f;
        }
        return fills;
    }

    public static void SetStars(Image[] fills, float value)
    {
        if (fills == null) return;
        for (int i = 0; i < fills.Length; i++) fills[i].fillAmount = Mathf.Clamp01(value - i);
    }

    public static Sprite StarSprite
    {
        get
        {
            if (starSprite != null) return starSprite;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = i % 2 == 0 ? 31f : 12.5f;
                poly[i] = new Vector2(S / 2f + Mathf.Cos(ang) * rad, S / 2f - 1.5f + Mathf.Sin(ang) * rad);
            }
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hits = 0; // khử răng cưa 4x4
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (Inside(poly, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hits++;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            tex.SetPixels32(px);
            tex.Apply();
            starSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return starSprite;
        }
    }

    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }
}
