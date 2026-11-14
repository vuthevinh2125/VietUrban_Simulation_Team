using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Màn hình giấy phép trên máy tính (giống Supermarket Simulator): danh sách nhóm hàng, cấp yêu cầu, giá, nút mua.
/// Mở từ nút "Licenses" trên máy tính. Esc / X để đóng (về lại màn hình chợ).
/// </summary>
public class LicensePanel : MonoBehaviour
{
    private static LicensePanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    /// <summary>Frame vừa đóng bảng (để Esc không đóng luôn máy tính phía sau).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject root;
    private TMP_Text headerInfo;
    private readonly List<(ProductLicense lic, LicenseCard card, Button buy)> rows = new List<(ProductLicense, LicenseCard, Button)>();


    public static void Open()
    {
        if (instance == null) instance = new GameObject("LicensePanel").AddComponent<LicensePanel>();
        instance.root.SetActive(true);
        UIState.Open(instance);
        instance.Refresh();
    }

    void Awake()
    {
        Build();
        EconomyManager.OnMoneyChanged += OnMoney;
        StoreProgress.OnChanged += Refresh;
        LicenseManager.OnLicensesChanged += Refresh;
    }

    void OnDestroy()
    {
        EconomyManager.OnMoneyChanged -= OnMoney;
        StoreProgress.OnChanged -= Refresh;
        LicenseManager.OnLicensesChanged -= Refresh;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnMoney(float _) => Refresh();

    private void Close()
    {
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        UIState.Close(this);
    }

    private void Refresh()
    {
        if (root == null || !root.activeSelf) return;
        int granted = 0;
        foreach (var r in rows) if (LicenseManager.Owns(r.lic)) granted++;
        headerInfo.text = $"A license adds a new group of products to the market. {granted} of {rows.Count} granted.";
        foreach (var r in rows)
        {
            bool owned = LicenseManager.Owns(r.lic);
            bool can = LicenseManager.CanBuy(r.lic, out string reason);
            bool locked = !owned && StoreProgress.Level < r.lic.requiredLevel;
            r.card.bg.color = locked ? ShopPC.PaperLocked : ShopPC.Paper;
            r.card.borderOn.SetActive(!locked);
            r.card.borderOff.SetActive(locked);
            r.card.number.color = locked ? ShopPC.Hex(0x8A7F68) : ShopPC.Red;
            r.card.stamp.SetActive(owned);
            r.card.price.gameObject.SetActive(!owned);
            r.card.price.text = EconomyManager.Format(r.lic.price);
            r.card.price.color = locked ? ShopPC.Muted : ShopPC.Pen;
            r.card.note.gameObject.SetActive(!owned && !can);
            r.card.note.text = locked ? $"needs store Lv {r.lic.requiredLevel}" : reason;
            r.buy.gameObject.SetActive(!owned && !locked);
            r.buy.interactable = can;
            r.card.group.alpha = locked ? 0.85f : 1f;
        }
    }

    // ================= Dựng giao diện (bản thiết kế 06: giấy phép kinh doanh) =================
    private class LicenseCard
    {
        public Image bg;
        public GameObject borderOn, borderOff, stamp;
        public TMP_Text number, price, note;
        public CanvasGroup group;
    }

    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform; // nằm trên giao diện máy tính
        RectTransform c = ShopPC.Computer(canvas, "LICENSES", Close, out root);
        headerInfo = ShopPC.Title(c, "LICENSES", "", out _);

        int n = LicenseManager.All.Count;
        const int cols = 4;
        const float gap = 24f, top = 84f;
        int rowsN = Mathf.Max(1, Mathf.CeilToInt((n + 1) / (float)cols));
        float w = (ShopPC.ContentSize.x - gap * (cols - 1)) / cols;
        float h = Mathf.Min(198f, (ShopPC.ContentSize.y - top - gap * (rowsN - 1)) / rowsN);
        for (int i = 0; i < n; i++)
        {
            ProductLicense lic = LicenseManager.All[i];
            float x = (i % cols) * (w + gap), y = top + (i / cols) * (h + gap);
            var lc = new LicenseCard();
            RectTransform card = ShopPC.Sheet("License_" + lic.id, c, x, y, w, h, ShopPC.Paper, 0f, 24f);
            lc.bg = card.GetComponent<Image>();
            lc.group = card.gameObject.AddComponent<CanvasGroup>();
            RectTransform on = ShopUI.Group("BorderOn", card, card.sizeDelta, Vector2.zero);
            ShopPC.DoubleBorder(on, card.sizeDelta, ShopPC.Red);
            RectTransform off = ShopUI.Group("BorderOff", card, card.sizeDelta, Vector2.zero);
            ShopPC.DoubleBorder(off, card.sizeDelta, ShopPC.Hex(0x8A7F68));
            lc.borderOn = on.gameObject; lc.borderOff = off.gameObject;
            lc.number = ShopPC.Text(card, $"GIẤY PHÉP KINH DOANH · No. {i + 1:000}", ShopUI.Body, 12, 0f, 24f, w, 18f, ShopPC.Red, TextAlignmentOptions.Center, 3f, true);
            TMP_Text licName = ShopPC.Text(card, lic.name.ToUpperInvariant(), ShopUI.Print, 28, 24f, 44f, w - 48f, h - 44f - 84f, ShopPC.Ink, TextAlignmentOptions.Center, 1.5f);
            licName.enableAutoSizing = true; licName.fontSizeMin = 18; licName.fontSizeMax = 28;
            float fy = h - 24f - 48f;
            lc.stamp = ShopPC.Stamp(card, "ĐÃ CẤP · GRANTED", ShopPC.At(card.sizeDelta, w - 30f - 230f, fy, 230f, 48f), 20f, -7f).gameObject;
            lc.price = ShopPC.Text(card, "", ShopUI.Hand, 33, 30f, fy - 4f, 160f, 52f, ShopPC.Pen, TextAlignmentOptions.Left);
            lc.note = ShopPC.Text(card, "", ShopUI.Body, 14, 30f, fy - 24f, w - 60f, 22f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
            ProductLicense captured = lic;
            Button buy = ShopPC.Btn(card, "BUY", w - 30f - 100f, fy, 100f, 48f, () => LicenseManager.TryBuy(captured), ShopPC.Kind.Red, 21f);
            rows.Add((lic, lc, buy));
        }
        // Giấy nhớ ở ô kế tiếp
        float nx = (n % cols) * (w + gap), ny = top + (n / cols) * (h + gap);
        ShopPC.StickyNote(c, "Drinks next. People buy them hot when it rains and cold when it is over 32°C. - bà Lan", nx, ny, w, h, 1.5f, 30f);
        root.SetActive(false);
    }

    private static GameObject NewRect(string name, Transform parent, Vector2 size = default, Vector2 pos = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return go;
    }

    private static TMP_Text Text(Transform parent, string text, float size, Vector2 box, Vector2 pos, TextAlignmentOptions align, bool bold)
    {
        GameObject go = NewRect("Text", parent, box, pos);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = ShopUI.Body;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    private static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject go = NewRect("Button_" + label, parent, size, pos);
        go.AddComponent<Image>().color = color;
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        Text(go.transform, label, 26, size, Vector2.zero, TextAlignmentOptions.Center, true);
        return b;
    }
}
