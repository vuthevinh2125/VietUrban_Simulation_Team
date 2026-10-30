using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tab THỐNG KÊ trên máy tính: biểu đồ cột doanh thu / chi phí / lãi theo Ngày, Tuần, Tháng.
/// Bấm vào một cột để xem chi tiết kỳ đó: các khoản chi, số khách, món đã bán, giao hàng, ngày tốt nhất, top sản phẩm.
/// Esc / X để đóng.
/// </summary>
public class StatsPanel : MonoBehaviour
{
    private enum Mode { Days, Weeks, Months, Reviews }

    private static StatsPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    private static readonly Color IncomeColor = new Color(0.3f, 0.82f, 0.4f);
    private static readonly Color ExpenseColor = new Color(0.92f, 0.36f, 0.3f);
    private static readonly Color ChartBg = new Color(0.09f, 0.11f, 0.15f, 1f);
    private static readonly Color TabOn = new Color(0.0f, 0.6f, 0.95f, 1f);

    private GameObject root;
    private GameObject chartBg, sidePanel, reviewsRoot;
    private TMP_Text legend;
    // Tab đánh giá
    private TMP_Text revBig, revCount, revEffect;
    private Image[] revStars;
    private readonly Image[] distBars = new Image[5];
    private readonly TMP_Text[] distText = new TMP_Text[5];
    private readonly Image[] factorBars = new Image[4];
    private readonly TMP_Text[] factorText = new TMP_Text[4];
    private readonly List<(Image[] stars, TMP_Text text)> rows = new List<(Image[], TMP_Text)>();
    private const int ReviewRows = 10;
    private RectTransform chart;
    private TMP_Text detail, empty, axisTop, axisMid;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<GameObject> chartItems = new List<GameObject>();
    private Mode mode = Mode.Days;
    private List<BusinessStats.Period> periods = new List<BusinessStats.Period>();
    private int selected = -1;

    private const float ChartW = 880f, ChartH = 470f;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("StatsPanel").AddComponent<StatsPanel>();
        instance.root.SetActive(true);
        UIState.Open(instance);
        instance.selected = -1;
        instance.Rebuild();
    }

    void Awake()
    {
        Build();
        BusinessStats.OnChanged += OnStatsChanged;
        ReviewManager.OnChanged += OnStatsChanged;
    }

    void OnDestroy()
    {
        BusinessStats.OnChanged -= OnStatsChanged;
        ReviewManager.OnChanged -= OnStatsChanged;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    private void OnStatsChanged() { if (IsOpen) Rebuild(); }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Close()
    {
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        UIState.Close(this);
    }

    private void SetMode(Mode m)
    {
        mode = m;
        selected = -1;
        Rebuild();
    }

    // ================= Vẽ biểu đồ =================
    private void Rebuild()
    {
        for (int i = 0; i < tabs.Count; i++) tabs[i].GetComponent<Image>().color = i == (int)mode ? TabOn : UIKit.Button;
        foreach (GameObject g in chartItems) Destroy(g);
        chartItems.Clear();

        bool rev = mode == Mode.Reviews;
        chartBg.SetActive(!rev);
        sidePanel.SetActive(!rev);
        legend.gameObject.SetActive(!rev);
        reviewsRoot.SetActive(rev);
        if (rev) { RefreshReviews(); return; }

        periods = mode == Mode.Days ? BusinessStats.ByDay(14) : mode == Mode.Weeks ? BusinessStats.ByWeek(10) : BusinessStats.ByMonth(12);
        empty.gameObject.SetActive(periods.Count == 0);
        if (periods.Count == 0)
        {
            detail.text = "";
            axisTop.text = axisMid.text = "";
            return;
        }
        if (selected < 0 || selected >= periods.Count) selected = periods.Count - 1;

        float max = 1f;
        foreach (var p in periods) max = Mathf.Max(max, p.Income, p.Expenses);
        max = NiceCeil(max);
        axisTop.text = EconomyManager.Format(max);
        axisMid.text = EconomyManager.Format(max / 2f);

        float slot = ChartW / Mathf.Max(periods.Count, 4);
        float barW = Mathf.Min(34f, slot * 0.32f);
        for (int i = 0; i < periods.Count; i++)
        {
            var p = periods[i];
            float cx = -ChartW / 2f + slot * (i + 0.5f);
            int index = i;

            // Vùng bấm chọn
            GameObject hit = UIKit.Rect("Slot", chart, new Vector2(slot - 4f, ChartH + 50f), new Vector2(cx, -25f));
            Image hi = hit.AddComponent<Image>();
            hi.color = i == selected ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0f);
            Button b = hit.AddComponent<Button>();
            b.onClick.AddListener(() => { selected = index; Rebuild(); });
            chartItems.Add(hit);

            Bar(cx - barW * 0.55f, p.Income / max * ChartH, barW, IncomeColor);
            Bar(cx + barW * 0.55f, p.Expenses / max * ChartH, barW, ExpenseColor);

            // Lãi trên đầu cột
            float top = Mathf.Max(p.Income, p.Expenses) / max * ChartH;
            TMP_Text profit = UIKit.Text(chart, ShortMoney(p.Profit), 16, new Vector2(slot, 22), new Vector2(cx, -ChartH / 2f + top + 14f), TextAlignmentOptions.Center, true,
                p.Profit >= 0f ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f));
            chartItems.Add(profit.gameObject);
            // Nhãn dưới cột
            TMP_Text label = UIKit.Text(chart, p.label, 15, new Vector2(slot, 40), new Vector2(cx, -ChartH / 2f - 24f), TextAlignmentOptions.Center, false, new Color(0.8f, 0.85f, 0.9f));
            chartItems.Add(label.gameObject);
        }
        ShowDetail(periods[selected]);
    }

    private void Bar(float x, float h, float w, Color c)
    {
        GameObject bar = UIKit.Rect("Bar", chart, new Vector2(w, Mathf.Max(2f, h)), Vector2.zero);
        var rt = (RectTransform)bar.transform;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, -ChartH / 2f);
        Image img = bar.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        chartItems.Add(bar);
    }

    private void ShowDetail(BusinessStats.Period p)
    {
        string F(float v) => EconomyManager.Format(v);
        var sb = new StringBuilder();
        sb.AppendLine($"<b><size=115%>{p.title}</size></b>");
        sb.AppendLine($"<size=85%><color=#9FB0C4>{p.days.Count} day(s) recorded</color></size>\n");
        sb.AppendLine($"<color=#6BE07A>Revenue  {F(p.Income)}</color>");
        sb.AppendLine($"   Sales  {F(p.Category(d => d.sales))}   <size=85%>(deliveries {p.Deliveries}: {F(p.DeliveryRevenue)})</size>");
        sb.AppendLine($"<color=#FF7A6E>Expenses  {F(p.Expenses)}</color>");
        sb.AppendLine($"   Products {F(p.Category(d => d.supplies))} · Rent {F(p.Category(d => d.rent))}");
        sb.AppendLine($"   Wages {F(p.Category(d => d.wages))} · Upgrades {F(p.Category(d => d.upgrades))} · Other {F(p.Category(d => d.other))}");
        sb.AppendLine($"<b>Profit  <color={(p.Profit >= 0f ? "#6BE07A" : "#FF7A6E")}>{F(p.Profit)}</color></b>\n");
        int customers = p.Customers;
        sb.AppendLine($"Customers  {customers}   ·   Items sold  {p.Items}");
        sb.AppendLine($"Avg per customer  {(customers > 0 ? F(p.Category(d => d.sales) / customers) : "-")}");
        if (p.Reviews > 0) sb.AppendLine($"Rating  <color=#FFD54A>{p.Rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5</color>  <size=85%>({p.Reviews} reviews)</size>");
        if (p.days.Count > 1)
        {
            var best = p.BestDay;
            if (best != null) sb.AppendLine($"Best day  Day {best.day} ({F(best.Profit)}, {best.weather})");
        }
        var top = p.TopProducts(5);
        if (top.Count > 0)
        {
            sb.AppendLine("\n<b>Top products</b>");
            for (int i = 0; i < top.Count; i++) sb.AppendLine($"{i + 1}. {top[i].item}  x{top[i].count}  <color=#9FB0C4>{F(top[i].revenue)}</color>");
        }
        detail.text = sb.ToString();
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        root = UIKit.FullScreen("Root", canvas, new Color(0f, 0f, 0f, 0.5f));
        GameObject window = UIKit.Panel("Window", root.transform, new Vector2(1560, 860), Vector2.zero, UIKit.Window);
        GameObject header = UIKit.Panel("Header", window.transform, new Vector2(1560, 64), new Vector2(0, 398), UIKit.Header);
        UIKit.Text(header.transform, "BUSINESS STATS", 32, new Vector2(1400, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        UIKit.Btn(header.transform, "X", new Vector2(48, 48), new Vector2(750, 0), Close, UIKit.Red);

        string[] names = { "Days", "Weeks", "Months", "Reviews" };
        for (int i = 0; i < names.Length; i++)
        {
            Mode m = (Mode)i;
            tabs.Add(UIKit.Btn(window.transform, names[i], new Vector2(130, 46), new Vector2(-700 + i * 138, 330), () => SetMode(m), null, 22));
        }
        legend = UIKit.Text(window.transform, "<color=#4DD15F><b>Green</b> = Revenue</color>   <color=#EB5C4D><b>Red</b> = Expenses</color>   Top number = profit   ·   Click a column for details",
            17, new Vector2(520, 30), new Vector2(50, 330), TextAlignmentOptions.Left, false, new Color(0.8f, 0.85f, 0.9f));
        legend.enableAutoSizing = true; legend.fontSizeMin = 11; legend.fontSizeMax = 17;

        chartBg = UIKit.Panel("Chart", window.transform, new Vector2(ChartW + 60f, ChartH + 110f), new Vector2(-300, -30), ChartBg);
        chart = (RectTransform)UIKit.Rect("Plot", chartBg.transform, new Vector2(ChartW, ChartH), new Vector2(20, 20)).transform;
        // Lưới ngang
        for (int i = 0; i <= 2; i++)
        {
            GameObject line = UIKit.Rect("Grid", chart, new Vector2(ChartW, 2f), new Vector2(0, -ChartH / 2f + ChartH * i / 2f));
            Image li = line.AddComponent<Image>();
            li.color = new Color(1f, 1f, 1f, i == 0 ? 0.35f : 0.1f);
            li.raycastTarget = false;
        }
        axisTop = UIKit.Text(chartBg.transform, "", 15, new Vector2(120, 22), new Vector2(-(ChartW + 60f) / 2f + 64f, ChartH / 2f + 34f), TextAlignmentOptions.Left, false, new Color(0.7f, 0.75f, 0.8f));
        axisMid = UIKit.Text(chartBg.transform, "", 15, new Vector2(120, 22), new Vector2(-(ChartW + 60f) / 2f + 64f, 34f), TextAlignmentOptions.Left, false, new Color(0.7f, 0.75f, 0.8f));
        empty = UIKit.Text(chartBg.transform, "No data yet.\nFinish a day (end-of-day report) to start collecting stats.", 26, new Vector2(800, 120), Vector2.zero);

        sidePanel = UIKit.Panel("Detail", window.transform, new Vector2(560, 700), new Vector2(480, -30), ChartBg);
        detail = UIKit.Text(sidePanel.transform, "", 22, new Vector2(520, 670), Vector2.zero, TextAlignmentOptions.TopLeft);
        BuildReviews(window.transform);
        root.SetActive(false);
    }

    // ================= Tab đánh giá =================
    private static readonly string[] FactorNames = { "Stock", "Prices", "Checkout", "Cleanliness" };

    private void BuildReviews(Transform window)
    {
        reviewsRoot = UIKit.Rect("Reviews", window, new Vector2(1560, 800), Vector2.zero);
        Color dim = new Color(0.7f, 0.76f, 0.84f);

        // Bên trái: điểm tổng, phân bố sao, 4 tiêu chí
        GameObject left = UIKit.Panel("Summary", reviewsRoot.transform, new Vector2(560, 700), new Vector2(-480, -30), ChartBg);
        Transform l = left.transform;
        UIKit.Text(l, "STORE RATING", 22, new Vector2(500, 30), new Vector2(0, 318), TextAlignmentOptions.Center, true, dim);
        revBig = UIKit.Text(l, "", 64, new Vector2(500, 72), new Vector2(0, 265), TextAlignmentOptions.Center, true, ReviewManager.StarOn);
        revStars = ReviewManager.StarRow(l, new Vector2(0, 205), 40f);
        revCount = UIKit.Text(l, "", 18, new Vector2(500, 26), new Vector2(0, 165), TextAlignmentOptions.Center, false, dim);

        for (int i = 0; i < 5; i++)
        {
            int star = 5 - i;
            float y = 115 - i * 32;
            UIKit.Text(l, $"{star}", 20, new Vector2(30, 28), new Vector2(-235, y), TextAlignmentOptions.Right, true);
            GameObject icon = UIKit.Rect("Star", l, new Vector2(20, 20), new Vector2(-205, y));
            Image ic = icon.AddComponent<Image>(); ic.sprite = ReviewManager.StarSprite; ic.color = ReviewManager.StarOn; ic.raycastTarget = false;
            distBars[i] = Bar(l, new Vector2(-185, y), 330f, ReviewManager.StarOn);
            distText[i] = UIKit.Text(l, "", 18, new Vector2(70, 28), new Vector2(220, y), TextAlignmentOptions.Left, false, dim);
        }

        UIKit.Text(l, "WHAT CUSTOMERS SAY ABOUT", 18, new Vector2(500, 26), new Vector2(0, -60), TextAlignmentOptions.Center, true, dim);
        for (int i = 0; i < 4; i++)
        {
            float y = -98 - i * 38;
            UIKit.Text(l, FactorNames[i], 20, new Vector2(150, 28), new Vector2(-170, y), TextAlignmentOptions.Left);
            factorBars[i] = Bar(l, new Vector2(-90, y), 250f, new Color(0.3f, 0.75f, 1f));
            factorText[i] = UIKit.Text(l, "", 18, new Vector2(80, 28), new Vector2(215, y), TextAlignmentOptions.Left, false, dim);
        }
        revEffect = UIKit.Text(l, "", 17, new Vector2(520, 70), new Vector2(0, -300), TextAlignmentOptions.Center, false, dim);

        // Bên phải: 10 đánh giá gần nhất
        GameObject right = UIKit.Panel("Recent", reviewsRoot.transform, new Vector2(940, 700), new Vector2(-170 + 470, -30), ChartBg);
        UIKit.Text(right.transform, "LATEST REVIEWS", 22, new Vector2(880, 30), new Vector2(0, 318), TextAlignmentOptions.Center, true, dim);
        for (int i = 0; i < ReviewRows; i++)
        {
            float y = 268 - i * 62;
            GameObject row = UIKit.Panel("Row", right.transform, new Vector2(900, 56), new Vector2(0, y), new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0.015f));
            Image[] st = ReviewManager.StarRow(row.transform, new Vector2(-370, 0), 18f);
            TMP_Text t = UIKit.Text(row.transform, "", 18, new Vector2(760, 52), new Vector2(65, 0), TextAlignmentOptions.Left);
            rows.Add((st, t));
        }
        reviewsRoot.SetActive(false);
    }

    /// <summary>Thanh ngang có nền mờ; trả về lớp màu để chỉnh độ dài (fillAmount).</summary>
    private static Image Bar(Transform parent, Vector2 leftCenter, float width, Color color)
    {
        GameObject bg = UIKit.Rect("BarBg", parent, new Vector2(width, 14f), leftCenter + new Vector2(width / 2f, 0f));
        Image b = bg.AddComponent<Image>(); b.color = new Color(1f, 1f, 1f, 0.08f); b.raycastTarget = false;
        GameObject fg = UIKit.Rect("Bar", bg.transform, new Vector2(width, 14f), Vector2.zero);
        Image f = fg.AddComponent<Image>();
        f.sprite = WhiteSprite; f.color = color; f.raycastTarget = false;
        f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = (int)Image.OriginHorizontal.Left;
        f.fillAmount = 0f;
        return f;
    }

    private static Sprite white;
    private static Sprite WhiteSprite
    {
        get
        {
            if (white == null)
                white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
            return white;
        }
    }

    private void RefreshReviews()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int recent = ReviewManager.RecentCount;
        float avg = ReviewManager.Average;
        revBig.text = recent > 0 ? avg.ToString("0.0", inv) : "-";
        ReviewManager.SetStars(revStars, avg);
        revCount.text = recent > 0
            ? $"Average of the last {recent} reviews  ·  {ReviewManager.All.Count} total"
            : "No reviews yet - customers rate the store when they leave.";

        var recentList = new List<ReviewManager.Review>(ReviewManager.Recent);
        for (int i = 0; i < 5; i++)
        {
            int star = 5 - i;
            int n = recentList.FindAll(r => r.stars == star).Count;
            distBars[i].fillAmount = recent > 0 ? (float)n / recent : 0f;
            distText[i].text = n.ToString();
        }

        float[] factors =
        {
            ReviewManager.FactorAverage(r => r.stock), ReviewManager.FactorAverage(r => r.price),
            ReviewManager.FactorAverage(r => r.checkout), ReviewManager.FactorAverage(r => r.clean),
        };
        for (int i = 0; i < 4; i++)
        {
            float v = factors[i];
            factorBars[i].fillAmount = v / 5f;
            factorBars[i].color = v >= 4f ? new Color(0.35f, 0.85f, 0.45f) : v >= 3f ? new Color(1f, 0.8f, 0.3f) : new Color(0.95f, 0.4f, 0.35f);
            factorText[i].text = v > 0f ? v.ToString("0.0", inv) : "-";
        }

        if (recent < ReviewManager.MinForEffect)
            revEffect.text = $"After {ReviewManager.MinForEffect} reviews the rating starts to affect how many customers visit.";
        else
        {
            int pct = Mathf.RoundToInt((ReviewManager.CustomerMultiplier - 1f) * 100f);
            string sign = pct >= 0 ? "+" : "";
            revEffect.text = $"Customer traffic from rating: <b><color={(pct >= 0 ? "#6BE07A" : "#FF7A6E")}>{sign}{pct}%</color></b>\n<size=90%>Keep shelves full, prices fair, lines short and floors clean.</size>";
        }

        var all = ReviewManager.All;
        for (int i = 0; i < rows.Count; i++)
        {
            int idx = all.Count - 1 - i;
            bool has = idx >= 0;
            rows[i].text.transform.parent.gameObject.SetActive(has || i == 0);
            if (!has)
            {
                ReviewManager.SetStars(rows[i].stars, 0f);
                rows[i].text.text = i == 0 ? "<color=#9FB0C4>No reviews yet.</color>" : "";
                continue;
            }
            var r = all[idx];
            ReviewManager.SetStars(rows[i].stars, r.stars);
            rows[i].text.text = $"<b>{r.name}</b>  <size=85%><color=#9FB0C4>Day {r.day}</color></size>\n<color=#DDE6F0>\"{r.comment}\"</color>";
        }
    }

    private static float NiceCeil(float v)
    {
        float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(v)));
        float n = v / pow;
        float nice = n <= 1f ? 1f : n <= 2f ? 2f : n <= 5f ? 5f : 10f;
        return nice * pow;
    }

    private static string ShortMoney(float v)
    {
        float a = Mathf.Abs(v);
        string s = a >= 1000f ? $"{a / 1000f:0.#}k" : $"{a:0}";
        return (v < 0f ? "-$" : "$") + s;
    }
}
