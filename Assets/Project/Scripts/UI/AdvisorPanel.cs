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

    private const int MaxRows = 11;
    private const float RowH = 60f, Left = 90f;
    // Cột: tên, vị trí trái, rộng (tính từ mép trái tờ giấy)
    private static readonly (string title, float x, float w)[] Cols =
    {
        ("PRODUCT", 90f, 285f), ("IN STOCK", 375f, 120f), ("WILL SELL", 495f, 135f), ("SHOULD HAVE", 630f, 150f),
        ("ORDER", 780f, 180f), ("WHY", 960f, 720f), ("", 1689f, 126f),
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
    private TMP_Text info, empty, totalText, learned;
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
        learned.text = report.daysOfHistory == 0 ? "what to order tonight - no sales history yet, so these are first guesses"
            : $"what to order tonight - learned from {report.daysOfHistory} day{(report.daysOfHistory > 1 ? "s" : "")} of sales";
        string today = report.hoursLeftToday > 0f ? $"TODAY: {report.hoursLeftToday:0.#} h left" : "TODAY: closed";
        info.text = $"{today}      TOMORROW {date.ToUpperInvariant()} (radio, right ~{WeatherSystem.ForecastAccuracy * 100f:0}% of days): {report.tomorrow}";

        float total = 0f;
        int shown = 0, toOrder = 0;
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
            r.cells[0].text = s.item.itemName + (s.perishable ? " <size=70%><color=#2F7A4A>(fresh)</color></size>" : "");
            r.cells[1].text = s.onHand.ToString(CultureInfo.InvariantCulture);
            r.cells[2].text = s.forecast.ToString("0.#", CultureInfo.InvariantCulture);
            r.cells[3].text = Mathf.CeilToInt(s.target).ToString(CultureInfo.InvariantCulture);
            r.cells[4].text = s.boxes > 0 ? $"+ {s.boxes} box{(s.boxes > 1 ? "es" : "")}" : "enough";
            r.cells[4].color = s.boxes > 0 ? ShopPC.Red : ShopPC.GreenInk;
            r.cells[5].text = string.IsNullOrEmpty(s.reason) ? $"{DemandModel.GroupName(s.group)} · normal demand" : s.reason;
            if (s.boxes > 0) { total += s.boxes * s.item.BoxPrice; toOrder++; }
            UpdateButton(r);
        }
        empty.gameObject.SetActive(shown == 0);
        totalText.text = total > 0f ? "Fresh food: I order a little less than I think I can sell. Spoiled food is money in the bin. - bà Lan"
                                    : "Nothing to order right now. The shelves will last until tomorrow. - bà Lan";
        ShopPC.SetLabel(addAll, total > 0f ? $"ADD ALL {toOrder} TO CART · {EconomyManager.Format(total)}" : "ADD ALL TO CART");
        addAll.interactable = total > 0f && MarketManager.Instance != null;
    }

    private void UpdateButton(Row r)
    {
        bool can = r.s != null && r.s.boxes > 0 && !r.added && MarketManager.Instance != null;
        r.button.interactable = can;
        r.button.gameObject.SetActive(r.s != null && r.s.boxes > 0);
        r.buttonLabel.text = r.added ? "IN CART" : "ADD";
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

    // ================= Dựng giao diện (bản thiết kế 09: sổ tay bà Lan) =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        RectTransform c = ShopPC.Computer(canvas, "ADVISOR", Close, out root);
        Vector2 sz = ShopPC.ContentSize;
        RectTransform book = ShopPC.Sheet("Notebook", c, 0f, 0f, sz.x, sz.y, ShopPC.Paper);
        Transform t = book;
        ShopPC.Box("Margin", book, 60f, 0f, 3f, sz.y, ShopPC.A(ShopPC.Red, 0.5f));
        for (float hy = 36f; hy < sz.y - 30f; hy += 69f) ShopPC.Dot(book, new Vector2(21f, 21f), ShopPC.At(sz, 21f, hy, 21f, 21f), ShopPC.Bg);

        TMP_Text title = ShopPC.Text(book, "SỔ TAY BÀ LAN", ShopUI.Print, 45, Left, 24f, 500f, 60f, ShopPC.Red, TextAlignmentOptions.BottomLeft, 4.5f);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        float tw = title.GetPreferredValues(title.text).x + 21f;
        learned = ShopPC.Text(book, "", ShopUI.Hand, 33, Left + tw, 30f, sz.x - Left - tw - 40f, 50f, ShopPC.Pen, TextAlignmentOptions.BottomLeft);
        info = ShopPC.Text(book, "", ShopUI.Body, 18, Left, 90f, sz.x - Left - 40f, 28f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);

        float y = 130f;
        foreach (var col in Cols)
            ShopPC.Text(book, col.title, ShopUI.Body, 15, col.x, y, col.w, 30f, ShopPC.Red, TextAlignmentOptions.BottomLeft, 1.5f, true);
        ShopPC.Box("HeadLine", book, Left, y + 33f, sz.x - Left - 33f, 3f, ShopPC.Red);

        y += 39f;
        for (int i = 0; i < MaxRows; i++)
        {
            var r = new Row { cells = new TMP_Text[6] };
            r.go = ShopPC.Group("Row" + i, book, 0f, y + i * RowH, sz.x, RowH).gameObject;
            Transform rt = r.go.transform;
            ShopPC.Box("Rule", rt, Left, RowH - 1.5f, sz.x - Left - 33f, 1.5f, ShopPC.A(ShopPC.Pen, 0.25f));
            for (int k = 0; k < 6; k++)
            {
                r.cells[k] = ShopPC.Text(rt, "", ShopUI.Hand, k == 5 ? 28 : 33, Cols[k].x, 0f, Cols[k].w - 12f, RowH, k == 5 ? ShopPC.Hex(0x5A4A34) : ShopPC.Pen, TextAlignmentOptions.Left);
                r.cells[k].textWrappingMode = TextWrappingModes.NoWrap;
                r.cells[k].overflowMode = TextOverflowModes.Ellipsis;
            }
            r.cells[0].fontStyle = FontStyles.Bold;
            Row captured = r;
            r.button = ShopPC.Btn(rt, "ADD", Cols[6].x, 7.5f, Cols[6].w, 45f, () => Add(captured), ShopPC.Kind.Red, 22f);
            r.buttonImage = r.button.GetComponent<Image>();
            r.buttonLabel = r.button.GetComponentInChildren<TMP_Text>();
            rows.Add(r);
        }
        empty = ShopPC.Text(book, "No products in the store yet - order a few products first, then come back after a day of sales.", ShopUI.Hand, 33,
            Left, 300f, sz.x - Left - 60f, 80f, ShopPC.Pen, TextAlignmentOptions.Center);

        float by = sz.y - 24f - 69f;
        totalText = ShopPC.Text(book, "", ShopUI.Hand, 31, Left, by, sz.x - Left - 560f, 69f, ShopPC.Pen, TextAlignmentOptions.Left);
        addAll = ShopPC.Btn(book, "ADD ALL TO CART", sz.x - 39f - 480f, by, 480f, 69f, AddAll, ShopPC.Kind.Red, 25f);
        root.SetActive(false);
    }

    private static TextAlignmentOptions Align(string col) =>
        col == "Product" || col == "Why" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
}
