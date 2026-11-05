using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tab ADVISOR trên máy tính: bảng gợi ý nhập hàng (RestockAdvisor) - mặt hàng, tồn kho, dự kiến bán tới hết ngày mai,
/// mức nên có, số thùng nên đặt và lý do; nút thêm vào giỏ (rồi bấm Buy như bình thường). Esc / X để đóng.
/// </summary>
public class AdvisorPanel : MonoBehaviour
{
    private static AdvisorPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private const int MaxRows = 12;
    private const float W = 1640f, H = 860f, RowH = 44f;
    private static readonly Color RowA = new Color(0.09f, 0.11f, 0.15f, 1f), RowB = new Color(0.11f, 0.135f, 0.18f, 1f);
    private static readonly Color Dim = new Color(0.68f, 0.74f, 0.82f), Gold = new Color(1f, 0.84f, 0.3f);
    private static readonly Color Disabled = new Color(0.3f, 0.33f, 0.38f, 1f);
    // Cột: tên, x (tâm), rộng
    private static readonly (string title, float x, float w)[] Cols =
    {
        ("Product", -640f, 300f), ("Group", -430f, 120f), ("On hand", -320f, 100f), ("Expected sales", -195f, 150f),
        ("Target", -75f, 90f), ("Order", 30f, 120f), ("Why", 320f, 460f), ("", 700f, 190f),
    };

    private class Row
    {
        public GameObject go;
        public TMP_Text[] cells;
        public Button button;
        public Image buttonImage;
        public TMP_Text buttonLabel;
        public RestockAdvisor.Suggestion s;
        public bool added;
    }

    private GameObject root;
    private TMP_Text info, empty, totalText;
    private Button addAll;
    private readonly List<Row> rows = new List<Row>();
    private RestockAdvisor.Report report;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("AdvisorPanel").AddComponent<AdvisorPanel>();
        instance.root.SetActive(true);
        UIState.Open(instance);
        instance.Refresh();
    }

    void Awake() => Build();

    void OnDestroy()
    {
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Close()
    {
        root.SetActive(false);
        UIState.Close(this);
    }

    // ================= Dữ liệu =================
    private void Refresh()
    {
        ShopCatalog cat = FindAnyObjectByType<ShopCatalog>(FindObjectsInactive.Include);
        report = RestockAdvisor.Build(cat != null ? cat.items : new List<ItemData>());

        GameManager gm = GameManager.Instance;
        int day = gm != null ? gm.currentDay : 1;
        string date = GameManager.DateOfDay(day + 1).ToString("ddd dd/MM", CultureInfo.InvariantCulture);
        string history = report.daysOfHistory == 0 ? "<color=#FFB86B>no sales history yet - default estimates</color>"
            : report.daysOfHistory < 7 ? $"<color=#FFB86B>learned from {report.daysOfHistory} day(s) of sales - still learning</color>"
            : $"learned from {report.daysOfHistory} days of sales";
        string today = report.hoursLeftToday > 0f ? $"{report.hoursLeftToday:0.#} h left today + " : "";
        info.text = $"Covers {today}tomorrow ({date}: forecast <b>{report.tomorrow}</b>, forecasts are right ~{WeatherSystem.ForecastAccuracy * 100f:0}% of days)   ·   {history}";

        float total = 0f;
        int shown = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            bool has = i < report.items.Count;
            r.go.SetActive(has);
            if (!has) continue;
            shown++;
            var s = report.items[i];
            r.s = s;
            r.added = false;
            string name = s.item.itemName + (s.perishable ? "  <color=#7CD992>(fresh)</color>" : "");
            r.cells[0].text = name;
            r.cells[1].text = DemandModel.GroupName(s.group);
            r.cells[2].text = s.onHand.ToString(CultureInfo.InvariantCulture);
            r.cells[3].text = s.forecast.ToString("0.#", CultureInfo.InvariantCulture);
            r.cells[4].text = Mathf.CeilToInt(s.target).ToString(CultureInfo.InvariantCulture);
            r.cells[5].text = s.boxes > 0
                ? $"<color=#FFD54A><b>{s.boxes} box{(s.boxes > 1 ? "es" : "")}</b></color>\n<size=14>{s.boxes * Mathf.Max(1, s.item.itemsPerBox)} items</size>"
                : "<color=#6BE07A>enough</color>";
            r.cells[6].text = string.IsNullOrEmpty(s.reason) ? "<color=#8A94A3>normal demand</color>" : s.reason;
            if (s.boxes > 0) total += s.boxes * s.item.BoxPrice;
            UpdateButton(r);
        }
        empty.gameObject.SetActive(shown == 0);
        totalText.text = total > 0f ? $"Suggested order total: <color=#FFD54A>{EconomyManager.Format(total)}</color>" : "Nothing to order right now.";
        addAll.interactable = total > 0f && MarketManager.Instance != null;
    }

    private void UpdateButton(Row r)
    {
        bool can = r.s != null && r.s.boxes > 0 && !r.added && MarketManager.Instance != null;
        r.button.interactable = can;
        r.buttonImage.color = can ? UIKit.Green : Disabled;
        r.buttonLabel.text = r.added ? "IN CART" : r.s != null && r.s.boxes > 0 ? $"ADD {r.s.boxes}" : "-";
    }

    private void Add(Row r)
    {
        if (r.s == null || r.s.boxes <= 0 || r.added || MarketManager.Instance == null) return;
        MarketManager.Instance.AddToCart(r.s.item, r.s.boxes);
        r.added = true;
        UpdateButton(r);
    }

    private void AddAll()
    {
        int n = 0;
        foreach (Row r in rows)
            if (r.go.activeSelf && r.s != null && r.s.boxes > 0 && !r.added) { Add(r); n++; }
        if (n > 0) ProgressHUD.Toast($"Added {n} suggested product(s) to the cart. Press <b>Buy</b> to order.");
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        root = UIKit.FullScreen("Root", canvas, new Color(0f, 0f, 0f, 0.5f));
        GameObject window = UIKit.Panel("Window", root.transform, new Vector2(W, H), Vector2.zero, UIKit.Window);
        Transform t = window.transform;
        GameObject header = UIKit.Panel("Header", t, new Vector2(W, 64), new Vector2(0, H / 2f - 32f), UIKit.Header);
        UIKit.Text(header.transform, "RESTOCK ADVISOR", 32, new Vector2(W - 200, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        UIKit.Btn(header.transform, "X", new Vector2(48, 48), new Vector2(W / 2f - 30f, 0), Close, UIKit.Red);

        info = UIKit.Text(t, "", 19, new Vector2(W - 60, 30), new Vector2(0, H / 2f - 88f), TextAlignmentOptions.Center, false, new Color(0.85f, 0.9f, 0.95f));
        TMP_Text how = UIKit.Text(t,
            "Expected sales come from your own sales history (weather, temperature and weekday effects it has learned) - not from a fixed rule. " +
            "Target keeps enough stock for 90% of days; fresh food uses the newsvendor rule to balance lost sales against spoilage.",
            16, new Vector2(W - 80, 44), new Vector2(0, H / 2f - 124f), TextAlignmentOptions.Center, false, Dim);
        how.textWrappingMode = TextWrappingModes.Normal;

        float y = H / 2f - 172f;
        GameObject head = UIKit.Panel("ColumnHeader", t, new Vector2(W - 40, 36), new Vector2(0, y), new Color(0.06f, 0.08f, 0.11f, 1f));
        foreach (var c in Cols)
            UIKit.Text(head.transform, c.title, 17, new Vector2(c.w, 34), new Vector2(c.x, 0), Align(c.title), true, Dim);

        y -= 40f;
        for (int i = 0; i < MaxRows; i++)
        {
            var r = new Row { cells = new TMP_Text[Cols.Length - 1] };
            r.go = UIKit.Panel("Row" + i, t, new Vector2(W - 40, RowH - 2), new Vector2(0, y - i * RowH), i % 2 == 0 ? RowA : RowB);
            for (int c = 0; c < Cols.Length - 1; c++)
            {
                r.cells[c] = UIKit.Text(r.go.transform, "", c == 6 ? 15 : 18, new Vector2(Cols[c].w, RowH - 4), new Vector2(Cols[c].x, 0), Align(Cols[c].title), c == 0);
                r.cells[c].textWrappingMode = c == 6 || c == 0 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                r.cells[c].overflowMode = TextOverflowModes.Ellipsis;
            }
            Row captured = r;
            r.button = UIKit.Btn(r.go.transform, "ADD", new Vector2(Cols[7].w - 20, RowH - 10), new Vector2(Cols[7].x, 0), () => Add(captured), UIKit.Green, 17);
            r.buttonImage = r.button.GetComponent<Image>();
            r.buttonLabel = r.button.GetComponentInChildren<TMP_Text>();
            rows.Add(r);
        }
        empty = UIKit.Text(t, "No products in the store yet - order a few products first, then come back after a day of sales.", 22,
            new Vector2(W - 200, 60), new Vector2(0, 0), TextAlignmentOptions.Center, false, Dim);

        float by = -H / 2f + 44f;
        totalText = UIKit.Text(t, "", 22, new Vector2(700, 44), new Vector2(-W / 2f + 380f, by), TextAlignmentOptions.Left, true);
        addAll = UIKit.Btn(t, "ADD ALL TO CART", new Vector2(320, 56), new Vector2(W / 2f - 190f, by), AddAll, UIKit.Green, 22);
        root.SetActive(false);
    }

    private static TextAlignmentOptions Align(string col) =>
        col == "Product" || col == "Why" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
}
