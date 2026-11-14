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

    private static readonly Color ChartBg = ShopPC.Card;

    private GameObject root;
    private GameObject chartBg, sidePanel, reviewsRoot, kpiRow;
    private TMP_Text legend, rangeText, chartTitle;
    private readonly TMP_Text[] kpiValue = new TMP_Text[4], kpiSub = new TMP_Text[4];
    private readonly List<(TMP_Text num, TMP_Text name, TMP_Text qty)> best = new List<(TMP_Text, TMP_Text, TMP_Text)>();
    private readonly List<(GameObject go, Image[] stars, TMP_Text text, TMP_Text who)> says = new List<(GameObject, Image[], TMP_Text, TMP_Text)>();
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
    private TMP_Text empty, axisTop, axisMid;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<GameObject> chartItems = new List<GameObject>();
    private Mode mode = Mode.Days;
    private List<BusinessStats.Period> periods = new List<BusinessStats.Period>();
    private int selected = -1;

    private const float ChartW = 1260f, ChartH = 440f;

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

    // ================= Vẽ biểu đồ (bản thiết kế 01: sổ cái) =================
    private void Rebuild()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool on = i == (int)mode;
            tabs[i].GetComponent<Image>().color = on ? ShopPC.Gold : new Color(0f, 0f, 0f, 0.001f);
            tabs[i].transform.Find("Border").gameObject.SetActive(!on);
            tabs[i].GetComponentInChildren<TMP_Text>().color = on ? ShopPC.Hex(0x120C02) : ShopPC.Hex(0xC3CAD4);
        }
        foreach (GameObject g in chartItems) Destroy(g);
        chartItems.Clear();

        bool rev = mode == Mode.Reviews;
        chartBg.SetActive(!rev);
        sidePanel.SetActive(!rev);
        kpiRow.SetActive(!rev);
        reviewsRoot.SetActive(rev);
        if (rev) { rangeText.text = "What customers wrote when they left"; RefreshReviews(); return; }

        periods = mode == Mode.Days ? BusinessStats.ByDay(14) : mode == Mode.Weeks ? BusinessStats.ByWeek(10) : BusinessStats.ByMonth(12);
        chartTitle.text = mode == Mode.Days ? "SALES PER DAY" : mode == Mode.Weeks ? "SALES PER WEEK" : "SALES PER MONTH";
        empty.gameObject.SetActive(periods.Count == 0);
        if (periods.Count == 0)
        {
            rangeText.text = "No days recorded yet";
            legend.text = "";
            axisTop.text = axisMid.text = "";
            ShowDetail(null, null);
            return;
        }
        if (selected < 0 || selected >= periods.Count) selected = periods.Count - 1;
        rangeText.text = $"{periods[0].label.Replace('\n', ' ')}  -  {periods[periods.Count - 1].label.Replace('\n', ' ')}";

        float max = 1f;
        foreach (var p in periods) max = Mathf.Max(max, p.Income, p.Expenses);
        max = NiceCeil(max);
        axisTop.text = EconomyManager.Format(max);
        axisMid.text = EconomyManager.Format(max / 2f);

        float slot = ChartW / Mathf.Max(periods.Count, 7);
        float barW = Mathf.Min(70f, slot * 0.5f);
        for (int i = 0; i < periods.Count; i++)
        {
            var p = periods[i];
            float cx = -ChartW / 2f + slot * (i + 0.5f);
            int index = i;
            bool sel = i == selected;
            Color ink = sel ? ShopPC.Red : ShopPC.Pen;

            // Vùng bấm chọn
            GameObject hit = UIKit.Rect("Slot", chart, new Vector2(slot - 4f, ChartH + 80f), new Vector2(cx, 0f));
            Image hi = hit.AddComponent<Image>();
            hi.color = sel ? ShopPC.A(ShopPC.Red, 0.06f) : new Color(1f, 1f, 1f, 0.001f);
            Button b = hit.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { selected = index; Rebuild(); });
            chartItems.Add(hit);

            float hIn = p.Income / max * ChartH, hOut = p.Expenses / max * ChartH;
            Bar(cx - barW * 0.12f, hIn, barW * 0.76f, ShopPC.A(ink, 0.45f), ink, i);
            Bar(cx + barW * 0.44f, hOut, barW * 0.2f, ShopPC.A(ShopPC.Red, 0.55f), ShopPC.A(ShopPC.Red, 0.8f), i);

            TMP_Text val = UIKit.Text(chart, ShortMoney(p.Income), 27, new Vector2(slot, 34), new Vector2(cx, -ChartH / 2f + hIn + 22f), TextAlignmentOptions.Center, false, ink);
            val.font = ShopUI.Hand;
            chartItems.Add(val.gameObject);
            TMP_Text label = UIKit.Text(chart, p.label.Replace('\n', ' '), 15, new Vector2(slot, 26), new Vector2(cx, -ChartH / 2f - 20f), TextAlignmentOptions.Center, true, sel ? ShopPC.Red : ShopPC.Muted);
            label.characterSpacing = 1.5f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 15;
            chartItems.Add(label.gameObject);
        }
        var bestDay = periods[selected].BestDay;
        legend.text = bestDay != null && periods[selected].days.Count > 1
            ? $"Best day: Day {bestDay.day} ({EconomyManager.Format(bestDay.Profit)}), {bestDay.weather}"
            : "Blue = sales · thin red = costs · click a column";
        ShowDetail(periods[selected], selected > 0 ? periods[selected - 1] : null);
    }

    private void Bar(float x, float h, float w, Color fill, Color edge, int index)
    {
        GameObject bar = UIKit.Rect("Bar", chart, new Vector2(w, Mathf.Max(2f, h)), Vector2.zero);
        var rt = (RectTransform)bar.transform;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, -ChartH / 2f);
        Image img = bar.AddComponent<Image>();
        img.color = fill;
        img.raycastTarget = false;
        if (h > 6f) ShopUI.Frame(rt, rt.sizeDelta, edge, 3f);
        // Cột mọc lên từ đáy
        bar.AddComponent<GrowUp>().delay = index * 0.05f;
        chartItems.Add(bar);
    }

    private void ShowDetail(BusinessStats.Period p, BusinessStats.Period prev)
    {
        string F(float v) => EconomyManager.Format(v);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (p == null)
        {
            for (int i = 0; i < 4; i++) { kpiValue[i].text = "-"; kpiSub[i].text = ""; }
            for (int i = 0; i < best.Count; i++) { best[i].num.text = ""; best[i].name.text = i == 0 ? "No sales yet" : ""; best[i].qty.text = ""; }
            RefreshSays();
            return;
        }
        float sales = p.Income;
        kpiValue[0].text = F(sales);
        kpiSub[0].text = prev != null && prev.Income > 0f ? $"{(sales - prev.Income) / prev.Income * 100f:+0;-0}% vs before" : p.title.Split('·')[0].Trim();
        kpiValue[1].text = F(p.Profit);
        kpiValue[1].color = p.Profit >= 0f ? ShopPC.Ink : ShopPC.Red;
        kpiSub[1].text = sales > 0f ? $"{p.Profit / sales * 100f:0}% margin" : "no sales";
        kpiValue[2].text = p.Customers.ToString(inv);
        kpiSub[2].text = p.days.Count > 1 ? $"{p.Customers / (float)p.days.Count:0} a day" : $"{p.Items} items sold";
        kpiValue[3].text = p.Reviews > 0 ? p.Rating.ToString("0.0", inv) + " / 5" : "-";
        kpiSub[3].text = p.Reviews > 0 ? $"{p.Reviews} reviews" : "no reviews yet";

        var top = p.TopProducts(best.Count);
        for (int i = 0; i < best.Count; i++)
        {
            bool has = i < top.Count;
            best[i].num.text = has ? (i + 1).ToString(inv) : "";
            best[i].name.text = has ? top[i].item : (i == 0 ? "No sales yet" : "");
            best[i].qty.text = has ? top[i].count.ToString(inv) : "";
        }
        RefreshSays();
    }

    /// <summary>3 đánh giá mới nhất trên giấy nhớ.</summary>
    private void RefreshSays()
    {
        var all = ReviewManager.All;
        for (int i = 0; i < says.Count; i++)
        {
            int idx = all.Count - 1 - i;
            bool has = idx >= 0;
            says[i].go.SetActive(has || i == 0);
            if (!has)
            {
                ReviewManager.SetStars(says[i].stars, 0f);
                says[i].text.text = i == 0 ? "No reviews yet. Customers write one when they leave." : "";
                says[i].who.text = "";
                continue;
            }
            var r = all[idx];
            ReviewManager.SetStars(says[i].stars, r.stars);
            says[i].text.text = r.comment;
            says[i].who.text = "- " + (string.IsNullOrEmpty(r.type) ? r.name : $"{r.name}, {r.type.ToLowerInvariant()}");
        }
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        RectTransform c = ShopPC.Computer(canvas, "STATS", Close, out root);
        const float leftW = 1323f, rx = leftW + 30f;
        float rw = ShopPC.ContentSize.x - leftW - 30f;

        // Dòng tiêu đề + chip chọn kỳ
        TMP_Text title = ShopPC.Text(c, "SỔ CÁI · LEDGER", ShopUI.Print, 39, 0f, 0f, 520f, 54f, ShopPC.Light, TextAlignmentOptions.BottomLeft, 4.5f);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        float tw = title.GetPreferredValues(title.text).x + 15f;
        rangeText = ShopPC.Text(c, "", ShopUI.Body, 18, tw, 18f, 420f, 30f, ShopPC.Dim, TextAlignmentOptions.BottomLeft);
        string[] names = { "DAY", "WEEK", "MONTH", "REVIEWS" };
        float cxp = leftW;
        for (int i = names.Length - 1; i >= 0; i--)
        {
            float w = names[i].Length * 13f + 42f;
            cxp -= w;
            Mode m = (Mode)i;
            RectTransform chip = ShopPC.Box("Chip_" + names[i], c, cxp, 6f, w, 48f, ShopPC.Gold, 0f, true);
            RectTransform border = ShopUI.Group("Border", chip, chip.sizeDelta, Vector2.zero);
            ShopUI.Frame(border, chip.sizeDelta, ShopPC.A(Color.white, 0.18f), 1.5f);
            TMP_Text t = ShopUI.Text(chip, names[i], ShopUI.Body, 18, chip.sizeDelta, Vector2.zero, ShopPC.Hex(0x120C02), TextAlignmentOptions.Center, 0f, 3f);
            t.fontStyle = FontStyles.Bold;
            Button b = chip.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SetMode(m));
            chip.gameObject.AddComponent<UIMotion>().hoverScale = 1.05f;
            tabs.Insert(0, b);
            cxp -= 12f;
        }

        // 4 thẻ số liệu
        kpiRow = ShopPC.Group("Kpis", c, 0f, 75f, leftW, 135f).gameObject;
        string[] kl = { "SALES", "PROFIT", "CUSTOMERS", "RATING" };
        float[] kr = { -1f, 0.6f, -0.5f, 1f };
        float kw = (leftW - 18f * 3f) / 4f;
        for (int i = 0; i < 4; i++)
        {
            RectTransform k = ShopPC.Sheet("Kpi_" + kl[i], kpiRow.transform, i * (kw + 18f), 0f, kw, 135f, ShopPC.Paper, kr[i], 24f);
            ShopPC.Text(k, kl[i], ShopUI.Body, 15, 18f, 15f, kw - 36f, 22f, ShopPC.Red, TextAlignmentOptions.Left, 3f, true);
            kpiValue[i] = ShopPC.Text(k, "-", ShopUI.Print, 51, 18f, 36f, kw - 36f, 60f, ShopPC.Ink, TextAlignmentOptions.Left);
            kpiValue[i].enableAutoSizing = true; kpiValue[i].fontSizeMin = 30; kpiValue[i].fontSizeMax = 51;
            kpiSub[i] = ShopPC.Text(k, "", ShopUI.Hand, 27, 18f, 96f, kw - 36f, 32f, ShopPC.Pen, TextAlignmentOptions.Left);
        }

        // Giấy kẻ ô có biểu đồ
        float py = 234f, ph = ShopPC.ContentSize.y - py;
        RectTransform paper = ShopPC.Sheet("Chart", c, 0f, py, leftW, ph, ShopPC.Paper);
        chartBg = paper.gameObject;
        Color gl = ShopPC.A(ShopPC.Pen, 0.12f);
        for (float gx = 33f; gx < leftW; gx += 33f) ShopPC.Box("GridV", paper, gx, 0f, 1.5f, ph, gl);
        for (float gy = 33f; gy < ph; gy += 33f) ShopPC.Box("GridH", paper, 0f, gy, leftW, 1.5f, gl);
        chartTitle = ShopPC.Text(paper, "SALES PER DAY", ShopUI.Print, 27, 27f, 18f, 500f, 36f, ShopPC.Red, TextAlignmentOptions.Left, 3f);
        legend = ShopPC.Text(paper, "", ShopUI.Hand, 30, 450f, 14f, leftW - 477f, 40f, ShopPC.Pen, TextAlignmentOptions.Right);
        chart = ShopPC.Group("Plot", paper, (leftW - ChartW) / 2f, 92f, ChartW, ChartH);
        ShopUI.Box("Base", chart, new Vector2(ChartW, 3f), new Vector2(0f, -ChartH / 2f - 1.5f), ShopPC.Ink);
        ShopUI.Box("Mid", chart, new Vector2(ChartW, 1.5f), Vector2.zero, ShopPC.A(ShopPC.Ink, 0.25f));
        axisTop = ShopPC.Text(paper, "", ShopUI.Body, 14, 27f, 70f, 160f, 20f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
        axisMid = ShopPC.Text(paper, "", ShopUI.Body, 14, 27f, 92f + ChartH / 2f - 22f, 160f, 20f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
        empty = ShopPC.Text(paper, "No data yet.\nFinish a day (end-of-day report) to start the ledger.", ShopUI.Hand, 36, 0f, 0f, leftW, ph, ShopPC.Pen, TextAlignmentOptions.Center);

        // Cột phải: bán chạy + khách nói gì
        sidePanel = ShopPC.Group("Right", c, rx, 0f, rw, ShopPC.ContentSize.y).gameObject;
        Transform side = sidePanel.transform;
        RectTransform list = ShopPC.Sheet("BestSellers", side, 0f, 0f, rw, 330f, ShopPC.Paper, 0f, 24f);
        for (float ly = 84f; ly < 330f; ly += 42f) ShopPC.Box("Rule", list, 0f, ly, rw, 1.5f, ShopPC.A(ShopPC.Pen, 0.16f));
        ShopUI.Tape(list, 0f, -4f);
        ShopPC.Text(list, "BEST SELLERS · ITEMS SOLD", ShopUI.Print, 27, 24f, 24f, rw - 48f, 42f, ShopPC.Red, TextAlignmentOptions.Left, 3f);
        for (int i = 0; i < 5; i++)
        {
            float y = 84f + i * 42f;
            TMP_Text num = ShopPC.Text(list, "", ShopUI.Body, 18, 24f, y, 30f, 42f, ShopPC.Red, TextAlignmentOptions.Left, 0f, true);
            TMP_Text nm = ShopPC.Text(list, "", ShopUI.Hand, 31, 60f, y - 2f, rw - 160f, 44f, ShopPC.Pen, TextAlignmentOptions.Left);
            TMP_Text q = ShopPC.Text(list, "", ShopUI.Hand, 31, rw - 100f, y - 2f, 76f, 44f, ShopPC.Pen, TextAlignmentOptions.Right);
            best.Add((num, nm, q));
        }
        ShopPC.Text(side, "CUSTOMERS SAY", ShopUI.Print, 27, 0f, 354f, rw, 40f, ShopPC.Light, TextAlignmentOptions.Left, 3f);
        float[] nr = { -1f, 1f, -0.6f };
        for (int i = 0; i < 3; i++)
        {
            float y = 408f + i * 174f;
            RectTransform note = ShopPC.Sheet("Says", side, 0f, y, rw, 156f, ShopPC.Note, nr[i], 24f);
            RectTransform stars = ShopPC.Group("Stars", note, 15f, 12f, 130f, 24f);
            Image[] st = ReviewManager.StarRow(stars, Vector2.zero, 21f);
            foreach (Image img in stars.GetComponentsInChildren<Image>(true))
                img.color = img.type == Image.Type.Filled ? ShopPC.Red : ShopPC.A(ShopPC.Red, 0.18f);
            TMP_Text t = ShopPC.Text(note, "", ShopUI.Hand, 28, 15f, 40f, rw - 30f, 84f, ShopPC.Pen, TextAlignmentOptions.TopLeft);
            t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = 28; t.lineSpacing = -14f;
            TMP_Text who = ShopPC.Text(note, "", ShopUI.Body, 15, 15f, 124f, rw - 30f, 24f, ShopPC.Hex(0x7A6E58), TextAlignmentOptions.Right, 0f, true);
            says.Add((note.gameObject, st, t, who));
        }

        BuildReviews(c);
        root.SetActive(false);
    }

    // ================= Tab đánh giá =================
    private static readonly string[] FactorNames = { "Stock", "Prices", "Checkout", "Cleanliness" };

    private void BuildReviews(Transform window)
    {
        reviewsRoot = UIKit.Rect("Reviews", window, new Vector2(1560, 800), new Vector2(0f, -10f));
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
            factorBars[i] = Bar(l, new Vector2(-90, y), 250f, ShopUI.SignGold);
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
    private static Image Bar(Transform parent, Vector2 leftCenter, float width, Color color) =>
        UIKit.HBar(parent, leftCenter, width, 14f, color);

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
            rows[i].text.text = $"<b>{r.name}</b>{(string.IsNullOrEmpty(r.type) ? "" : $" <size=85%><color=#C9D6E3>({r.type})</color></size>")}  <size=85%><color=#9FB0C4>Day {r.day}</color></size>\n<color=#DDE6F0>\"{r.comment}\"</color>";
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
