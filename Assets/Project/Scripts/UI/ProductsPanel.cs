using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tab PRODUCTS của máy tính theo bản thiết kế 02: thanh trên chung (ShopPC), giấy nhớ "Sổ tay bà Lan" gợi ý nhập hàng,
/// ô tìm kiếm + chip lọc, lưới thẻ sản phẩm (ảnh, nhãn giá nhập vàng, số thùng, ADD) và tờ "PHIẾU NHẬP HÀNG" bên phải
/// (giỏ hàng, tổng tiền, nút đặt hàng). Phủ lên giao diện máy tính dựng trong scene; dữ liệu và giỏ hàng vẫn là của
/// ShopCatalog / MarketManager như cũ. ComputerInteract mở / đóng bảng này cùng lúc với máy tính.
/// </summary>
public class ProductsPanel : MonoBehaviour
{
    private static ProductsPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private const float AsideW = 408f, Gap = 27f;
    private const int Cols = 4;

    private class Card
    {
        public ItemData item;
        public string category;
        public GameObject go;
        public TMP_Text amount, tag;
        public GameObject tagBox;
        public int boxes = 1;
    }

    private GameObject root;
    private RectTransform content, grid;
    private ScrollRect scroll;
    private TMP_InputField search;
    private TMP_Text noteText, emptyText, cartNames, cartTotals, totalText, afterText, statusText, dateText, searchHint;
    private Button noteButton, orderButton, clearButton;
    private RestockAdvisor.Suggestion noteSuggestion;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<(Button b, string key)> chips = new List<(Button, string)>();
    private string chip = "ALL";
    private ShopCatalog catalog;
    private float statusUntil;

    public static void Show(GameObject computerUI)
    {
        if (instance == null) instance = new GameObject("ProductsPanel").AddComponent<ProductsPanel>();
        instance.Open(computerUI);
    }

    public static void Hide()
    {
        if (instance != null && instance.root != null) instance.root.SetActive(false);
    }

    void Awake()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 540).transform;
        content = ShopPC.Computer(canvas, "PRODUCTS", null, out root);
        BuildStatic();
        root.SetActive(false);
        MarketManager.OnCartChanged += RefreshCart;
        MarketManager.OnStatus += OnStatus;
        EconomyManager.OnMoneyChanged += OnMoney;
        LicenseManager.OnLicensesChanged += ApplyFilter;
        DemandModel.OnChanged += RefreshTags;
    }

    void OnDestroy()
    {
        MarketManager.OnCartChanged -= RefreshCart;
        MarketManager.OnStatus -= OnStatus;
        EconomyManager.OnMoneyChanged -= OnMoney;
        LicenseManager.OnLicensesChanged -= ApplyFilter;
        DemandModel.OnChanged -= RefreshTags;
        if (instance == this) instance = null;
    }

    private void OnMoney(float _) => RefreshCart();

    void Update()
    {
        if (!IsOpen) return;
        if (statusUntil > 0f && Time.unscaledTime > statusUntil) { statusUntil = 0f; statusText.text = ""; }
        GameManager gm = GameManager.Instance;
        if (gm != null) dateText.text = $"Tạp hoá bà Lan · Day {gm.currentDay} · {gm.ClockString}";
    }

    private void Open(GameObject computerUI)
    {
        ShopCatalog cat = computerUI != null ? computerUI.GetComponentInChildren<ShopCatalog>(true) : null;
        if (cat == null) cat = FindAnyObjectByType<ShopCatalog>(FindObjectsInactive.Include);
        if (cat != catalog || cards.Count == 0) { catalog = cat; BuildCards(); }
        root.SetActive(true);
        RefreshNote();
        RefreshTags();
        RefreshCart();
        ApplyFilter();
        statusText.text = "";
    }

    // ================= Dữ liệu =================
    private void RefreshNote()
    {
        noteSuggestion = null;
        var items = new List<ItemData>();
        foreach (Card c in cards) if (c.item != null && LicenseManager.IsUnlocked(c.item)) items.Add(c.item);
        if (items.Count > 0)
        {
            RestockAdvisor.Report report = RestockAdvisor.Build(items);
            foreach (var s in report.items) if (s.boxes > 0) { noteSuggestion = s; break; }
        }
        if (noteSuggestion != null)
        {
            var s = noteSuggestion;
            string why = string.IsNullOrEmpty(s.reason) ? $"{s.onHand} left, about {s.forecast:0} will sell by tomorrow night" : s.reason;
            noteText.text = $"{s.item.itemName}: {why}. Order {s.boxes} more box{(s.boxes > 1 ? "es" : "")}.";
            ShopPC.SetLabel(noteButton, $"ADD {s.boxes} BOX{(s.boxes > 1 ? "ES" : "")}");
            noteButton.gameObject.SetActive(true);
        }
        else
        {
            noteText.text = "The shelves will last until tomorrow. Order only what you will sell. Boxes that sit around still cost money.";
            noteButton.gameObject.SetActive(false);
        }
    }

    private void RefreshTags()
    {
        foreach (Card c in cards)
        {
            if (c.item == null) continue;
            string t = DemandModel.CardTag(DemandModel.Current(c.item));
            c.tag.text = t;
            c.tagBox.SetActive(t.Length > 0);
        }
    }

    private void ApplyFilter()
    {
        if (cards.Count == 0) return;
        string q = search != null ? search.text.Trim().ToLowerInvariant() : "";
        int shown = 0;
        foreach (Card c in cards)
        {
            bool ok = c.item != null && LicenseManager.IsUnlocked(c.item);
            if (ok && q.Length > 0) ok = c.item.itemName.ToLowerInvariant().Contains(q) || (c.item.brand ?? "").ToLowerInvariant().Contains(q);
            if (ok && chip != "ALL")
            {
                if (chip == "FOOD" || chip == "HOUSEHOLD") ok = string.Equals(c.category, chip, System.StringComparison.OrdinalIgnoreCase);
                else ok = string.Equals(c.item.displayType ?? "", chip, System.StringComparison.OrdinalIgnoreCase);
            }
            c.go.SetActive(ok);
            if (ok) shown++;
        }
        emptyText.gameObject.SetActive(shown == 0);
        if (searchHint != null) searchHint.text = $"Search {shown} products";
        foreach (var (b, key) in chips)
        {
            bool on = key == chip;
            b.GetComponent<Image>().color = on ? ShopPC.Gold : ShopPC.A(Color.white, 0.05f);
            b.GetComponentInChildren<TMP_Text>().color = on ? ShopPC.Hex(0x120C02) : ShopPC.Hex(0xC3CAD4);
        }
    }

    private void RefreshCart()
    {
        if (root == null) return;
        MarketManager m = MarketManager.Instance;
        var names = new System.Text.StringBuilder();
        var totals = new System.Text.StringBuilder();
        int lines = 0;
        if (m != null)
            foreach (var kv in m.Cart)
            {
                if (lines == 9) { names.AppendLine($"<size=85%>... and {m.Cart.Count - 9} more</size>"); break; }
                names.AppendLine($"<b>{kv.Key.itemName}</b>\n<size=85%><color=#7A756A>{kv.Value} box{(kv.Value > 1 ? "es" : "")} × {EconomyManager.Format(kv.Key.BoxPrice)}</color></size>");
                totals.AppendLine($"<b>{EconomyManager.Format(kv.Key.BoxPrice * kv.Value)}</b>\n ");
                lines++;
            }
        cartNames.text = lines == 0 ? "<color=#7A756A>Nothing yet. Press ADD on a product.</color>" : names.ToString();
        cartTotals.text = totals.ToString();
        float total = m != null ? m.CartTotal : 0f;
        float money = EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0f;
        totalText.text = EconomyManager.Format(total);
        afterText.text = total > 0f ? $"You have {EconomyManager.Format(money)} · after: {EconomyManager.Format(money - total)}" : $"You have {EconomyManager.Format(money)}";
        afterText.color = money - total < 0f ? ShopPC.Red : ShopPC.Muted;
        int boxes = m != null ? m.CartBoxCount : 0;
        ShopPC.SetLabel(orderButton, boxes > 0 ? $"ORDER {boxes} BOX{(boxes > 1 ? "ES" : "")}" : "ORDER");
        orderButton.interactable = boxes > 0 && money >= total;
        clearButton.gameObject.SetActive(boxes > 0);
    }

    private void OnStatus(string message)
    {
        if (!IsOpen || statusText == null) return;
        statusText.text = message;
        statusUntil = Time.unscaledTime + 5f;
    }

    // ================= Dựng giao diện =================
    private void BuildStatic()
    {
        float mainW = ShopPC.ContentSize.x - AsideW - Gap;
        float h = ShopPC.ContentSize.y;

        // Giấy nhớ "Sổ tay bà Lan"
        RectTransform note = ShopPC.Sheet("AdvisorNote", content, 0f, 0f, mainW, 84f, ShopPC.Note);
        ShopPC.Box("Rule", note, 0f, 42f, mainW, 1.5f, ShopPC.A(ShopPC.Pen, 0.16f));
        ShopUI.Tape(note, -mainW * 0.5f + 105f, -4f);
        UIMotion nm = note.gameObject.AddComponent<UIMotion>();
        nm.swayDegrees = 0.25f; nm.swaySpeed = 1.2f;
        ShopPC.Text(note, "SỔ TAY BÀ LAN", ShopUI.Print, 27, 27f, 0f, 230f, 84f, ShopPC.Red, TextAlignmentOptions.Left, 3f);
        noteText = ShopPC.Text(note, "", ShopUI.Hand, 33, 255f, 0f, mainW - 255f - 250f, 84f, ShopPC.Pen, TextAlignmentOptions.Left);
        noteText.enableAutoSizing = true; noteText.fontSizeMin = 20; noteText.fontSizeMax = 33; noteText.lineSpacing = -12f;
        noteButton = ShopPC.Btn(note, "ADD", mainW - 21f - 220f, 18f, 220f, 48f, AddSuggestion, ShopPC.Kind.Red, 20f);

        // Ô tìm kiếm + chip
        float fy = 105f;
        string[] keys = { "ALL", "SHELF", "FRESH", "FRIDGE", "FOOD", "HOUSEHOLD" };
        float chipsW = 0f;
        foreach (string k in keys) chipsW += k.Length * 12f + 42f + 15f;
        float sw = mainW - chipsW;
        RectTransform sbox = ShopPC.Box("Search", content, 0f, fy, sw, 63f, ShopPC.A(Color.white, 0.05f));
        ShopUI.Frame(sbox, sbox.sizeDelta, ShopPC.A(Color.white, 0.1f), 1.5f);
        ShopPC.Dot(sbox, new Vector2(21f, 21f), ShopPC.At(sbox.sizeDelta, 21f, 18f, 21f, 21f), ShopPC.Dim);
        ShopPC.Dot(sbox, new Vector2(13f, 13f), ShopPC.At(sbox.sizeDelta, 25f, 22f, 13f, 13f), ShopPC.Hex(0x15181E));
        ShopPC.Box("Handle", sbox, 38f, 39f, 12f, 4f, ShopPC.Dim, -45f);
        GameObject inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        inputGo.name = "SearchInput";
        inputGo.transform.SetParent(sbox, false);
        RectTransform irt = (RectTransform)inputGo.transform;
        irt.sizeDelta = new Vector2(sw - 75f, 54f);
        irt.anchoredPosition = new Vector2(30f, 0f);
        inputGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        search = inputGo.GetComponent<TMP_InputField>();
        search.pointSize = 21;
        search.caretColor = ShopPC.Light;
        foreach (TMP_Text t in inputGo.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font = ShopUI.Body; t.fontSize = 21; t.color = ShopPC.Light;
            t.alignment = TextAlignmentOptions.Left;
        }
        searchHint = search.placeholder as TMP_Text;
        if (searchHint != null) { searchHint.color = ShopPC.Dim; searchHint.fontStyle = FontStyles.Normal; }
        search.onValueChanged.AddListener(_ => ApplyFilter());
        float cx = sw + 15f;
        foreach (string k in keys)
        {
            float w = k.Length * 12f + 42f;
            RectTransform cr = ShopPC.Box("Chip_" + k, content, cx, fy, w, 63f, ShopPC.A(Color.white, 0.05f), 0f, true);
            ShopUI.Frame(cr, cr.sizeDelta, ShopPC.A(Color.white, 0.1f), 1.5f);
            TMP_Text t = ShopUI.Text(cr, k, ShopUI.Body, 18, cr.sizeDelta, Vector2.zero, ShopPC.Hex(0xC3CAD4), TextAlignmentOptions.Center, 0f, 1.5f);
            t.fontStyle = FontStyles.Bold;
            Button b = cr.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            string key = k;
            b.onClick.AddListener(() => { chip = key; ApplyFilter(); scroll.verticalNormalizedPosition = 1f; });
            cr.gameObject.AddComponent<UIMotion>().hoverScale = 1.05f;
            chips.Add((b, k));
            cx += w + 15f;
        }

        // Lưới sản phẩm cuộn được
        float gy = fy + 63f + 21f;
        RectTransform view = ShopPC.Box("Viewport", content, 0f, gy, mainW, h - gy, new Color(0f, 0f, 0f, 0.001f), 0f, true);
        view.gameObject.AddComponent<RectMask2D>();
        grid = ShopUI.Group("Grid", view, new Vector2(mainW, 0f), Vector2.zero);
        grid.anchorMin = new Vector2(0f, 1f); grid.anchorMax = new Vector2(1f, 1f); grid.pivot = new Vector2(0.5f, 1f);
        grid.sizeDelta = Vector2.zero; grid.anchoredPosition = Vector2.zero;
        GridLayoutGroup gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        float cw = (mainW - 18f * (Cols - 1) - 12f) / Cols;
        gl.cellSize = new Vector2(cw, 285f);
        gl.spacing = new Vector2(18f, 18f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = Cols;
        gl.padding = new RectOffset(0, 12, 0, 24);
        ContentSizeFitter fit = grid.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view; scroll.content = grid;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // Thanh cuộn mảnh
        RectTransform track = ShopPC.Box("ScrollTrack", content, mainW - 6f, gy, 6f, h - gy, ShopPC.A(Color.white, 0.05f), 0f, true);
        RectTransform handle = ShopUI.Box("Handle", track, new Vector2(6f, 60f), Vector2.zero, ShopPC.A(ShopPC.Gold, 0.5f), 0f, true);
        Scrollbar sb = track.gameObject.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.handleRect = handle; sb.targetGraphic = handle.GetComponent<Image>();
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        emptyText = ShopPC.Text(content, "No products match. Clear the search, pick ALL, or buy a license for more products.", ShopUI.Hand, 33,
            0f, gy + 120f, mainW, 80f, ShopPC.Soft, TextAlignmentOptions.Center);

        // Phiếu nhập hàng
        float ax = mainW + Gap;
        RectTransform slip = ShopPC.Sheet("OrderSlip", content, ax, 0f, AsideW, h - 12f, ShopPC.Hex(0xECE7DC));
        Color ink = ShopPC.Hex(0x1B1A17), dash = ShopPC.Hex(0x8C877C);
        float sh = h - 12f;
        ShopPC.Text(slip, "PHIẾU NHẬP HÀNG", ShopUI.Print, 33, 0f, 27f, AsideW, 44f, ShopPC.Red, TextAlignmentOptions.Center, 4.5f);
        dateText = ShopPC.Text(slip, "Tạp hoá bà Lan", ShopUI.Body, 16, 0f, 72f, AsideW, 24f, ink, TextAlignmentOptions.Center);
        Dashes(slip, 108f, dash);
        cartNames = ShopPC.Text(slip, "", ShopUI.Body, 18, 27f, 123f, AsideW - 150f, sh - 123f - 300f, ink, TextAlignmentOptions.TopLeft);
        cartTotals = ShopPC.Text(slip, "", ShopUI.Body, 18, AsideW - 150f, 123f, 123f, sh - 123f - 300f, ink, TextAlignmentOptions.TopRight);
        cartNames.overflowMode = cartTotals.overflowMode = TextOverflowModes.Truncate;
        cartNames.lineSpacing = cartTotals.lineSpacing = 4f;
        float by = sh - 290f;
        Dashes(slip, by, dash);
        ShopPC.Text(slip, "TOTAL", ShopUI.Body, 20, 27f, by + 18f, 150f, 60f, ink, TextAlignmentOptions.Left, 0f, true);
        totalText = ShopPC.Text(slip, "", ShopUI.Print, 48, 150f, by + 12f, AsideW - 177f, 66f, ink, TextAlignmentOptions.Right);
        afterText = ShopPC.Text(slip, "", ShopUI.Body, 15, 27f, by + 78f, AsideW - 54f, 24f, ShopPC.Muted, TextAlignmentOptions.Right);
        orderButton = ShopPC.Btn(slip, "ORDER", 27f, by + 114f, AsideW - 54f, 66f, Order, ShopPC.Kind.Red, 26f);
        clearButton = ShopPC.Btn(slip, "EMPTY THE CART", 27f, by + 192f, AsideW - 54f, 45f, () => { if (MarketManager.Instance != null) MarketManager.Instance.ClearCart(); }, ShopPC.Kind.Dark, 18f);
        statusText = ShopPC.Text(slip, "", ShopUI.Hand, 24, 27f, by + 240f, AsideW - 54f, 44f, ShopPC.Pen, TextAlignmentOptions.TopLeft);
        statusText.enableAutoSizing = true; statusText.fontSizeMin = 15; statusText.fontSizeMax = 24; statusText.lineSpacing = -10f;
        for (float zx = 0f; zx < AsideW; zx += 24f) ShopPC.Dot(slip, new Vector2(18f, 18f), ShopPC.At(slip.sizeDelta, zx + 3f, sh - 9f, 18f, 18f), ShopPC.Bg);
    }

    private static void Dashes(RectTransform parent, float y, Color c)
    {
        for (float x = 27f; x < parent.sizeDelta.x - 27f; x += 12f) ShopPC.Box("Dash", parent, x, y, 6f, 1.5f, c);
    }

    private static readonly int[] ArtColors = { 0x3E7BC0, 0xC64A3A, 0x4FA36A, 0xE8B13A, 0x7A5BB5, 0x2F8F8A, 0xB86A3A, 0x5A6B7A };

    private void BuildCards()
    {
        foreach (Card c in cards) if (c.go != null) Destroy(c.go);
        cards.Clear();
        if (catalog == null) return;

        // Lấy từ các thẻ ShopCatalog đã dựng (giữ ảnh cũ và nhóm Food / Household), thiếu thì lấy danh sách items
        var seen = new HashSet<ItemData>();
        var list = new List<(ItemData item, string cat, Sprite icon)>();
        foreach (ProductCardUI pc in catalog.GetComponentsInChildren<ProductCardUI>(true))
            if (pc.itemData != null && seen.Add(pc.itemData))
                list.Add((pc.itemData, pc.category, pc.iconImage != null && pc.iconImage.enabled ? pc.iconImage.sprite : pc.itemData.icon));
        foreach (ItemData it in catalog.items)
            if (it != null && seen.Add(it)) list.Add((it, "Food", it.icon));

        float cw = ((GridLayoutGroup)grid.GetComponent<GridLayoutGroup>()).cellSize.x, ch = 285f;
        foreach (var (item, cat, icon) in list)
        {
            var c = new Card { item = item, category = cat };
            RectTransform card = ShopUI.Box("Card_" + item.itemName, grid, new Vector2(cw, ch), Vector2.zero, ShopPC.Card);
            c.go = card.gameObject;
            ShopUI.Frame(card, card.sizeDelta, ShopPC.A(Color.white, 0.06f), 1.5f);

            // Ảnh + nhãn
            Color art = ShopPC.Hex(ArtColors[Mathf.Abs(DemandModel.GroupOf(item).GetHashCode()) % ArtColors.Length]);
            RectTransform a = ShopPC.Box("Art", card, 0f, 0f, cw, 138f, art);
            if (icon != null)
            {
                ShopPC.Box("Shade", a, 0f, 0f, cw, 138f, ShopPC.A(Color.white, 0.18f));
                RectTransform ic = ShopPC.Box("Icon", a, cw * 0.5f - 57f, 12f, 114f, 114f, Color.white);
                Image img = ic.GetComponent<Image>(); img.sprite = icon; img.preserveAspect = true;
            }
            else
                ShopPC.Text(a, Initials(item.itemName), ShopUI.Print, 60, 0f, 0f, cw, 138f, ShopPC.A(Color.white, 0.9f), TextAlignmentOptions.Center, 1.5f);
            RectTransform tb = ShopPC.Box("Tag", a, 0f, 0f, 90f, 30f, ShopPC.A(ShopPC.Hex(0x08090C), 0.75f));
            c.tagBox = tb.gameObject;
            c.tag = ShopUI.Text(tb, "", ShopUI.Body, 16, tb.sizeDelta, Vector2.zero, Color.white, TextAlignmentOptions.Center, 0f, 1.5f);
            c.tag.fontStyle = FontStyles.Bold;
            ShopPC.PriceTag(a, EconomyManager.Format(item.BoxPrice), ShopPC.At(a.sizeDelta, cw - 9f - 120f, 138f - 36f, 120f, 50f), 30f, -3f, "GIÁ NHẬP · 1 BOX");

            // Tên + mô tả
            ShopPC.Text(card, item.itemName, ShopUI.Body, 21, 16f, 150f, cw - 32f, 30f, ShopPC.Light, TextAlignmentOptions.Left, 0f, true);
            string brand = string.IsNullOrEmpty(item.brand) ? "" : item.brand + " · ";
            ShopPC.Text(card, $"{brand}{Mathf.Max(1, item.itemsPerBox)} per box · {item.displayType}", ShopUI.Body, 16, 16f, 180f, cw - 32f, 24f, ShopPC.Dim, TextAlignmentOptions.Left);

            // Số thùng + ADD
            float ry = ch - 16f - 51f;
            RectTransform step = ShopPC.Box("Stepper", card, 16f, ry, 120f, 51f, new Color(0f, 0f, 0f, 0.001f));
            ShopUI.Frame(step, step.sizeDelta, ShopPC.A(Color.white, 0.14f), 1.5f);
            Card captured = c;
            StepButton(step, "-", 0f, () => { captured.boxes = Mathf.Max(1, captured.boxes - 1); captured.amount.text = captured.boxes.ToString(CultureInfo.InvariantCulture); });
            StepButton(step, "+", 75f, () => { captured.boxes = Mathf.Min(99, captured.boxes + 1); captured.amount.text = captured.boxes.ToString(CultureInfo.InvariantCulture); });
            c.amount = ShopPC.Text(step, "1", ShopUI.Body, 19, 45f, 0f, 30f, 51f, ShopPC.Light, TextAlignmentOptions.Center, 0f, true);
            ShopPC.Btn(card, "ADD", 145f, ry, cw - 161f, 51f, () => AddCard(captured), ShopPC.Kind.Gold, 21f);
            cards.Add(c);
        }
    }

    private static void StepButton(RectTransform parent, string label, float x, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform b = ShopPC.Box("Step" + label, parent, x, 0f, 45f, 51f, new Color(0f, 0f, 0f, 0.001f), 0f, true);
        ShopUI.Text(b, label, ShopUI.Body, 26, b.sizeDelta, Vector2.zero, ShopPC.Hex(0xDDE2EA), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        Button btn = b.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
        b.gameObject.AddComponent<UIMotion>().hoverScale = 1.12f;
    }

    private static string Initials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        string[] parts = name.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        string s = parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name.Substring(0, Mathf.Min(2, name.Length));
        return s.ToUpperInvariant();
    }

    // ================= Hành động =================
    private void AddCard(Card c)
    {
        MarketManager m = MarketManager.Instance;
        if (m == null || c.item == null) return;
        m.AddToCart(c.item, c.boxes);
        AudioManager.Play("ui_click", 0.5f);
        c.boxes = 1;
        c.amount.text = "1";
    }

    private void AddSuggestion()
    {
        MarketManager m = MarketManager.Instance;
        if (m == null || noteSuggestion == null) return;
        m.AddToCart(noteSuggestion.item, noteSuggestion.boxes);
        ShopPC.SetLabel(noteButton, "IN THE CART");
        noteButton.interactable = false;
    }

    private void Order()
    {
        MarketManager m = MarketManager.Instance;
        if (m == null) return;
        m.Checkout();
        AudioManager.Play("coin", 0.6f);
        RefreshNote();
        noteButton.interactable = true;
    }
}
