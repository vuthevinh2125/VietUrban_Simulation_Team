using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bộ dựng "màn hình máy tính của tiệm bà Lan" (bản thiết kế 02-09): nền tối, thanh trên có biển BÀ LAN MART,
/// các tab chữ in (tab đang mở màu vàng có gạch chân), nhãn tiền vàng, nút X; phần nội dung là giấy, bìa, con dấu, giấy nhớ.
/// Toạ độ trong vùng nội dung tính từ góc trên trái (giống bản thiết kế, đã nhân 1.5 lần cho màn 1920x1080).
/// </summary>
public static class ShopPC
{
    // ---------- Màu theo bản thiết kế ----------
    public static readonly Color Bg = Hex(0x0B0D12);
    public static readonly Color Bar = Hex(0x07080B);
    public static readonly Color Card = Hex(0x12161E);
    public static readonly Color CardArt = Hex(0x1B212C);
    public static readonly Color Dim = Hex(0x8E97A6);
    public static readonly Color Soft = Hex(0xAEB6C2);
    public static readonly Color Light = Hex(0xF1F3F6);
    public static readonly Color Yellow = Hex(0xFFD400);
    public static readonly Color Gold = Hex(0xFFC23D);
    public static readonly Color Red = Hex(0xC8161D);
    public static readonly Color Paper = Hex(0xFBF7EC);
    public static readonly Color PaperLocked = Hex(0xCFC8B4);
    public static readonly Color Ink = Hex(0x1B1410);
    public static readonly Color Pen = Hex(0x1F3FA8);
    public static readonly Color Muted = Hex(0x5F5A50);
    public static readonly Color Body = Hex(0x3A342A);
    public static readonly Color Mint = Hex(0x6EE7A0);
    public static readonly Color Note = Hex(0xFFF8B8);
    public static readonly Color Cream = Hex(0xFFF3D6);
    public static readonly Color GreenInk = Hex(0x2F7A4A);

    public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>Kích thước vùng nội dung dưới thanh trên.</summary>
    public static readonly Vector2 ContentSize = new Vector2(1848f, 930f);
    public static readonly string[] Tabs = { "PRODUCTS", "FURNITURE", "STAFF", "EXPAND", "LICENSES", "VEHICLES", "STATS", "ADVISOR" };

    // ---------- Khung màn hình ----------
    /// <summary>
    /// Dựng nền + thanh trên. close: đóng bảng hiện tại (bấm tab khác sẽ đóng bảng này rồi mở bảng kia; X đóng luôn cả máy tính).
    /// Trả về vùng nội dung (1848x930, gốc ở giữa).
    /// </summary>
    public static RectTransform Computer(Transform canvas, string active, Action close, out GameObject root)
    {
        RectTransform r = ShopUI.Full("Root", canvas, Bg, true);
        root = r.gameObject;

        RectTransform bar = ShopUI.Box("TopBar", r, Vector2.zero, Vector2.zero, Bar);
        bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, 96f); bar.anchoredPosition = Vector2.zero;
        RectTransform line = ShopUI.Box("RedLine", bar, Vector2.zero, Vector2.zero, Red);
        line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(0.5f, 0f);
        line.sizeDelta = new Vector2(0f, 3f); line.anchoredPosition = Vector2.zero;

        // Biển BÀ LAN MART
        float x = 36f;
        TMP_Text tagText = MakeText(bar, "BÀ LAN MART", ShopUI.Print, 30, Gold, TextAlignmentOptions.Center, 3f);
        float tw = tagText.GetPreferredValues("BÀ LAN MART").x + 36f;
        RectTransform tag = LeftBox("Brand", bar, x, tw, 48f, Red);
        tagText.transform.SetParent(tag, false);
        Fit(tagText.rectTransform, tag.sizeDelta);
        x += tw + 39f;

        // Các tab
        foreach (string t in Tabs)
        {
            bool on = t == active;
            TMP_Text label = MakeText(bar, t, ShopUI.Print, 28, on ? Gold : Dim, TextAlignmentOptions.Center, 3f);
            float w = label.GetPreferredValues(t).x + 4f;
            RectTransform tab = LeftBox("Tab_" + t, bar, x, w, 60f, new Color(0f, 0f, 0f, 0.001f), true);
            label.transform.SetParent(tab, false);
            Fit(label.rectTransform, new Vector2(w, 48f));
            label.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            if (on) ShopUI.Box("Underline", tab, new Vector2(w, 4f), new Vector2(0f, -27f), Gold);
            if (!on)
            {
                Button b = tab.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                string target = t;
                b.onClick.AddListener(() => GoTab(target, close));
                tab.gameObject.AddComponent<UIMotion>().hoverScale = 1.06f;
                tab.gameObject.AddComponent<TabHover>().label = label;
            }
            x += w + 33f;
        }

        // Nút X (đóng máy tính)
        RectTransform xb = RightBox("Close", bar, 36f, 66f, 66f, new Color(0f, 0f, 0f, 0.001f), true);
        ShopUI.Frame(xb, new Vector2(66f, 66f), A(Color.white, 0.15f), 2f);
        MakeCross(xb, 24f, Hex(0xDDE2EA));
        Button xbtn = xb.gameObject.AddComponent<Button>();
        xbtn.transition = Selectable.Transition.None;
        xbtn.onClick.AddListener(() => { close?.Invoke(); CloseComputer(); });
        xb.gameObject.AddComponent<UIMotion>().hoverScale = 1.08f;

        // Nhãn tiền vàng
        RectTransform money = RightBox("Money", bar, 36f + 66f + 30f, 200f, 52f, Yellow);
        money.localRotation = Quaternion.Euler(0f, 0f, -2f);
        Shadow sh = money.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.5f); sh.effectDistance = new Vector2(0f, -5f);
        TMP_Text mt = MakeText(money, "", ShopUI.Print, 33, Hex(0x14100A), TextAlignmentOptions.Center, 1f);
        Fit(mt.rectTransform, new Vector2(200f, 52f));
        ComputerMoney cm = money.gameObject.AddComponent<ComputerMoney>();
        cm.text = mt; cm.box = money;

        RectTransform content = ShopUI.Group("Content", r, ContentSize, new Vector2(0f, -48f));
        return content;
    }

    private static void GoTab(string tab, Action close)
    {
        close?.Invoke();
        switch (tab)
        {
            case "FURNITURE":
                FurnitureCatalog cat = UnityEngine.Object.FindAnyObjectByType<FurnitureCatalog>(FindObjectsInactive.Include);
                FurniturePanel.Open(cat != null ? cat.furniture : null);
                break;
            case "STAFF": StaffPanel.Open(); break;
            case "EXPAND": ExpansionPanel.Open(); break;
            case "LICENSES": LicensePanel.Open(); break;
            case "VEHICLES": VehiclePanel.Open(); break;
            case "STATS": StatsPanel.Open(); break;
            case "ADVISOR": AdvisorPanel.Open(); break;
            default: break;                               // PRODUCTS: quay về màn hình đặt hàng phía sau
        }
    }

    private static void CloseComputer()
    {
        foreach (ComputerInteract c in UnityEngine.Object.FindObjectsByType<ComputerInteract>(FindObjectsSortMode.None))
            if (c.IsOpen) c.CloseComputer();
    }

    // ---------- Bố cục theo toạ độ góc trên trái ----------
    /// <summary>Tâm của ô (x, y, w, h) tính từ góc trên trái của khung cha kích thước parent.</summary>
    public static Vector2 At(Vector2 parent, float x, float y, float w, float h) =>
        new Vector2(-parent.x * 0.5f + x + w * 0.5f, parent.y * 0.5f - y - h * 0.5f);

    public static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color, float rot = 0f, bool raycast = false)
    {
        Vector2 ps = ((RectTransform)parent).sizeDelta;
        return ShopUI.Box(name, parent, new Vector2(w, h), At(ps, x, y, w, h), color, rot, raycast);
    }

    public static RectTransform Group(string name, Transform parent, float x, float y, float w, float h, float rot = 0f)
    {
        Vector2 ps = ((RectTransform)parent).sizeDelta;
        return ShopUI.Group(name, parent, new Vector2(w, h), At(ps, x, y, w, h), rot);
    }

    /// <summary>Tờ giấy / thẻ có bóng đổ đậm như bản thiết kế, rơi nhẹ xuống khi hiện (delay giây).</summary>
    public static RectTransform Sheet(string name, Transform parent, float x, float y, float w, float h, Color color, float rot = 0f, float drop = 0f)
    {
        RectTransform rt = Box(name, parent, x, y, w, h, color, rot);
        Shadow s = rt.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.45f);
        s.effectDistance = new Vector2(0f, -12f);
        if (drop > 0f) rt.gameObject.AddComponent<UIMotion>().dropIn = drop;
        return rt;
    }

    public static TMP_Text Text(Transform parent, string text, TMP_FontAsset font, float size, float x, float y, float w, float h, Color color,
                                TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f, bool bold = false)
    {
        Vector2 ps = ((RectTransform)parent).sizeDelta;
        TMP_Text t = ShopUI.Text(parent, text, font, size, new Vector2(w, h), At(ps, x, y, w, h), color, align, 0f, spacing);
        if (bold) t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    /// <summary>Dòng tiêu đề trang: chữ in to + dòng phụ xám (+ chữ bên phải nếu cần). Trả về dòng phụ để cập nhật.</summary>
    public static TMP_Text Title(Transform content, string title, string sub, out TMP_Text right)
    {
        TMP_Text t = Text(content, title, ShopUI.Print, 39, 0f, 0f, 700f, 54f, Light, TextAlignmentOptions.BottomLeft, 4.5f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        float w = t.GetPreferredValues(title).x + 21f;
        TMP_Text s = Text(content, sub, ShopUI.Body, 20, w, 14f, 1848f - w - 520f, 34f, Soft, TextAlignmentOptions.BottomLeft);
        right = Text(content, "", ShopUI.Body, 20, 1848f - 520f, 8f, 520f, 40f, Soft, TextAlignmentOptions.BottomRight);
        return s;
    }

    // ---------- Nút ----------
    public enum Kind { Red, Dark, Ghost, Ink, Gold }

    public static Button Btn(Transform parent, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick, Kind kind = Kind.Red, float fontSize = 0f)
    {
        Vector2 ps = ((RectTransform)parent).sizeDelta;
        return Btn(parent, label, new Vector2(w, h), At(ps, x, y, w, h), onClick, kind, fontSize);
    }

    public static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, Kind kind = Kind.Red, float fontSize = 0f)
    {
        Color bg, fg;
        switch (kind)
        {
            case Kind.Dark: bg = new Color(0f, 0f, 0f, 0.001f); fg = Ink; break;
            case Kind.Ghost: bg = new Color(0f, 0f, 0f, 0.001f); fg = Hex(0xF4ECD8); break;
            case Kind.Ink: bg = Ink; fg = Gold; break;
            case Kind.Gold: bg = A(Gold, 0.14f); fg = Gold; break;
            default: bg = Red; fg = Cream; break;
        }
        RectTransform rt = ShopUI.Box("Button_" + label, parent, size, pos, bg, 0f, true);
        if (kind == Kind.Red) ShopUI.Frame(rt, size - new Vector2(4f, 4f), Gold, 3f);
        if (kind == Kind.Dark) ShopUI.Frame(rt, size, Ink, 3f);
        if (kind == Kind.Ghost) ShopUI.Frame(rt, size, A(Hex(0xF4ECD8), 0.35f), 2f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;   // khoá thì cả nút mờ đi (FadeWhenDisabled)
        if (onClick != null) b.onClick.AddListener(onClick);
        TMP_Text t = ShopUI.Text(rt, label, ShopUI.Print, fontSize > 0f ? fontSize : Mathf.Min(28f, size.y * 0.42f), size, Vector2.zero, fg, TextAlignmentOptions.Center, 0f, 3f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        rt.gameObject.AddComponent<UIMotion>().hoverScale = 1.05f;
        rt.gameObject.AddComponent<FadeWhenDisabled>();
        return b;
    }

    public static void SetLabel(Button b, string text)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = text;
    }

    // ---------- Đồ vật ----------
    /// <summary>Con dấu đỏ, đập xuống mỗi lần hiện.</summary>
    public static RectTransform Stamp(Transform parent, string text, Vector2 pos, float fontSize, float rot)
    {
        TMP_Text probe = ShopUI.Text(parent, text, ShopUI.Print, fontSize, new Vector2(1000f, 100f), Vector2.zero, Color.clear, TextAlignmentOptions.Center, 0f, 3f);
        float w = probe.GetPreferredValues(text).x + 30f;
        UnityEngine.Object.Destroy(probe.gameObject);
        Vector2 size = new Vector2(w, fontSize * 1.45f);
        RectTransform g = ShopUI.Group("Stamp", parent, size, pos, rot);
        Color c = Hex(0xD0202A);
        ShopUI.Frame(g, size, c, 4.5f);
        ShopUI.Text(g, text, ShopUI.Print, fontSize, size, Vector2.zero, c, TextAlignmentOptions.Center, 0f, 3f).textWrappingMode = TextWrappingModes.NoWrap;
        g.gameObject.AddComponent<StampSlam>();
        return g;
    }

    /// <summary>Nhãn giá vàng nghiêng (kiểu nhãn kệ).</summary>
    public static TMP_Text PriceTag(Transform parent, string text, Vector2 pos, float fontSize, float rot = -3f, string small = null)
    {
        float h = fontSize * (small != null ? 1.55f : 1.3f);
        RectTransform tag = ShopUI.Box("PriceTag", parent, new Vector2(140f, h), pos, Yellow, rot);
        Shadow s = tag.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.5f); s.effectDistance = new Vector2(0f, -5f);
        if (small != null)
            ShopUI.Text(tag, small, ShopUI.Body, 10.5f, new Vector2(130f, 14f), new Vector2(0f, h * 0.5f - 10f), Hex(0x14100A), TextAlignmentOptions.Right, 0f, 1.5f).fontStyle = FontStyles.Bold;
        TMP_Text t = ShopUI.Text(tag, text, ShopUI.Print, fontSize, new Vector2(140f, fontSize * 1.2f), new Vector2(0f, small != null ? -h * 0.12f : 0f), Hex(0x14100A), TextAlignmentOptions.Center);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        tag.gameObject.AddComponent<FitTag>().text = t;
        return t;
    }

    /// <summary>Giấy nhớ vàng chữ viết tay, có băng dính và kẻ dòng, đung đưa nhẹ.</summary>
    public static RectTransform StickyNote(Transform parent, string text, float x, float y, float w, float h, float rot, float fontSize = 31f)
    {
        RectTransform n = Sheet("StickyNote", parent, x, y, w, h, Note, rot);
        for (float ly = 42f; ly < h - 6f; ly += 42f)
            ShopUI.Box("Line", n, new Vector2(w, 1.5f), new Vector2(0f, h * 0.5f - ly), A(Pen, 0.16f));
        ShopUI.Tape(n, -w * 0.5f + 90f, -4f);
        TMP_Text t = ShopUI.Text(n, text, ShopUI.Hand, fontSize, new Vector2(w - 42f, h - 24f), Vector2.zero, Pen, TextAlignmentOptions.Left);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = fontSize;
        t.lineSpacing = -14f;
        UIMotion m = n.gameObject.AddComponent<UIMotion>();
        m.swayDegrees = 0.6f; m.swaySpeed = 0.8f;
        return n;
    }

    /// <summary>Thanh ngang (nền mờ + phần màu), trả về phần màu để chỉnh fillAmount.</summary>
    public static Image HBar(Transform parent, Vector2 leftCenter, float width, float height, Color back, Color fill)
    {
        RectTransform bg = ShopUI.Box("BarBg", parent, new Vector2(width, height), leftCenter + new Vector2(width * 0.5f, 0f), back);
        RectTransform fg = ShopUI.Box("Bar", bg, new Vector2(width, height), Vector2.zero, fill);
        Image f = fg.GetComponent<Image>();
        f.sprite = UIKit.WhiteSprite;
        f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = (int)Image.OriginHorizontal.Left;
        f.fillAmount = 0f;
        return f;
    }

    /// <summary>Khung nét đứt (phòng có thể xây trên bản vẽ mặt bằng).</summary>
    public static void DashedFrame(Transform parent, Vector2 size, Color color, float thick, float dash = 15f, float gap = 9f)
    {
        void Run(Vector2 from, Vector2 dir, float length, bool horizontal)
        {
            for (float d = 0f; d < length; d += dash + gap)
            {
                float l = Mathf.Min(dash, length - d);
                Vector2 c = from + dir * (d + l * 0.5f);
                ShopUI.Box("Dash", parent, horizontal ? new Vector2(l, thick) : new Vector2(thick, l), c, color);
            }
        }
        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        Run(new Vector2(-hx, hy - thick * 0.5f), Vector2.right, size.x, true);
        Run(new Vector2(-hx, -hy + thick * 0.5f), Vector2.right, size.x, true);
        Run(new Vector2(-hx + thick * 0.5f, hy), Vector2.down, size.y, false);
        Run(new Vector2(hx - thick * 0.5f, hy), Vector2.down, size.y, false);
    }

    private static Sprite circle;
    /// <summary>Hình tròn trắng sắc cạnh (bánh xe, đầu người, đồng xu).</summary>
    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    float a = Mathf.Clamp01(n * 0.5f - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            circle = Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return circle;
        }
    }

    public static Image Dot(Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        RectTransform rt = ShopUI.Box("Dot", parent, size, pos, color);
        Image img = rt.GetComponent<Image>();
        img.sprite = Circle;
        return img;
    }

    /// <summary>Khung thẻ: giấy có viền đỏ đôi (giấy phép).</summary>
    public static void DoubleBorder(Transform parent, Vector2 size, Color color)
    {
        ShopUI.Frame(parent, size - new Vector2(14f, 14f), color, 3f);
        ShopUI.Frame(parent, size - new Vector2(30f, 30f), color, 1.5f);
    }

    /// <summary>Thanh phím tắt dưới đáy: phím (ô kem) + việc làm, chữ phụ bên phải.</summary>
    public static void KeyHints(RectTransform bar, (string key, string what)[] pairs, string right)
    {
        float x = 96f;
        foreach (var (key, what) in pairs)
        {
            TMP_Text k = MakeText(bar, key, ShopUI.Body, 18, Hex(0x14100A), TextAlignmentOptions.Center, 0f);
            k.fontStyle = FontStyles.Bold;
            float kw = Mathf.Max(39f, k.GetPreferredValues(key).x + 18f);
            RectTransform cap = LeftBox("Key_" + key, bar, x, kw, 39f, Hex(0xF4ECD8));
            k.transform.SetParent(cap, false);
            Fit(k.rectTransform, cap.sizeDelta);
            x += kw + 12f;
            TMP_Text w = MakeText(bar, what, ShopUI.Body, 19, Hex(0xD9CDB4), TextAlignmentOptions.Left, 0f);
            float ww = w.GetPreferredValues(what).x + 4f;
            RectTransform wr = w.rectTransform;
            wr.anchorMin = wr.anchorMax = new Vector2(0f, 0.5f); wr.pivot = new Vector2(0f, 0.5f);
            wr.sizeDelta = new Vector2(ww, 39f); wr.anchoredPosition = new Vector2(x, 1.5f);
            x += ww + 33f;
        }
        if (!string.IsNullOrEmpty(right))
        {
            TMP_Text r = MakeText(bar, right, ShopUI.Body, 19, Hex(0x9A8E76), TextAlignmentOptions.Right, 0f);
            RectTransform rr = r.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(1f, 0.5f); rr.pivot = new Vector2(1f, 0.5f);
            rr.sizeDelta = new Vector2(800f, 39f); rr.anchoredPosition = new Vector2(-72f, 1.5f);
        }
    }

    // ---------- Nội bộ ----------
    private static TMP_Text MakeText(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, float spacing)
    {
        TMP_Text t = ShopUI.Text(parent, text, font, size, new Vector2(600f, 60f), Vector2.zero, color, align, 0f, spacing);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static void Fit(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
    }

    private static RectTransform LeftBox(string name, Transform bar, float x, float w, float h, Color c, bool raycast = false)
    {
        RectTransform rt = ShopUI.Box(name, bar, new Vector2(w, h), Vector2.zero, c, 0f, raycast);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 1.5f);
        return rt;
    }

    private static RectTransform RightBox(string name, Transform bar, float right, float w, float h, Color c, bool raycast = false)
    {
        RectTransform rt = ShopUI.Box(name, bar, new Vector2(w, h), Vector2.zero, c, 0f, raycast);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-right, 1.5f);
        return rt;
    }

    private static void MakeCross(Transform parent, float size, Color c)
    {
        ShopUI.Box("X1", parent, new Vector2(size * 1.25f, 3.6f), Vector2.zero, c, 45f);
        ShopUI.Box("X2", parent, new Vector2(size * 1.25f, 3.6f), Vector2.zero, c, -45f);
    }
}

/// <summary>Nhãn tiền vàng trên thanh máy tính: cập nhật số tiền và co giãn theo độ dài.</summary>
public class ComputerMoney : MonoBehaviour
{
    public TMP_Text text;
    public RectTransform box;
    private float last = float.NaN;

    void Update()
    {
        float m = EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0f;
        if (Mathf.Approximately(m, last)) return;
        last = m;
        text.text = EconomyManager.Format(m);
        float w = text.GetPreferredValues(text.text).x + 30f;
        box.sizeDelta = new Vector2(w, box.sizeDelta.y);
        text.rectTransform.sizeDelta = new Vector2(w, box.sizeDelta.y);
    }
}

/// <summary>Nhãn giá co theo độ dài chữ.</summary>
public class FitTag : MonoBehaviour
{
    public TMP_Text text;
    private string last;

    void LateUpdate()
    {
        if (text == null || text.text == last) return;
        last = text.text;
        var rt = (RectTransform)transform;
        float w = Mathf.Max(70f, text.GetPreferredValues(text.text).x + 24f);
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        text.rectTransform.sizeDelta = new Vector2(w, text.rectTransform.sizeDelta.y);
    }
}

/// <summary>Tab chưa chọn: chữ sáng lên khi rê chuột.</summary>
public class TabHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text label;
    public void OnPointerEnter(PointerEventData e) { if (label != null) label.color = ShopPC.Light; }
    public void OnPointerExit(PointerEventData e) { if (label != null) label.color = ShopPC.Dim; }
}

/// <summary>Nút bị khoá thì mờ đi cả chữ (không chỉ nền).</summary>
public class FadeWhenDisabled : MonoBehaviour
{
    private Selectable sel;
    private CanvasGroup group;
    private bool last = true;

    void Awake()
    {
        sel = GetComponent<Selectable>();
        group = gameObject.AddComponent<CanvasGroup>();
    }

    void LateUpdate()
    {
        bool on = sel == null || sel.interactable;
        if (on == last) return;
        last = on;
        group.alpha = on ? 1f : 0.45f;
    }
}

/// <summary>Con dấu đập xuống: phóng to rồi thu về kích thước thật mỗi lần hiện.</summary>
public class StampSlam : MonoBehaviour
{
    private float shownAt;
    private bool done;
    void OnEnable() { shownAt = Time.unscaledTime; done = false; transform.localScale = Vector3.one * 2.2f; }

    void Update()
    {
        if (done) return;
        float k = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.16f);
        float s = k < 1f ? Mathf.Lerp(2.2f, 0.94f, k) : Mathf.MoveTowards(transform.localScale.x, 1f, Time.unscaledDeltaTime * 2f);
        transform.localScale = Vector3.one * s;
        if (k >= 1f && Mathf.Approximately(s, 1f)) done = true;
    }
}

/// <summary>Nhấp nháy độ trong suốt (phòng có thể xây, chữ "đang thiếu tiền"...).</summary>
public class AlphaPulse : MonoBehaviour
{
    public float speed = 3.9f, min = 0.45f;
    private Graphic g;
    private float baseA;

    void Awake() { g = GetComponent<Graphic>(); if (g != null) baseA = g.color.a; }

    void Update()
    {
        if (g == null) return;
        float k = Mathf.Lerp(min, 1f, 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * speed));
        Color c = g.color; c.a = baseA * k; g.color = c;
    }
}

/// <summary>Cột biểu đồ mọc lên từ đáy khi hiện.</summary>
public class GrowUp : MonoBehaviour
{
    public float delay, duration = 0.5f;
    private float shownAt;
    void OnEnable() { shownAt = Time.unscaledTime; transform.localScale = new Vector3(1f, 0f, 1f); }

    void Update()
    {
        float k = Mathf.Clamp01((Time.unscaledTime - shownAt - delay) / duration);
        float e = 1f - Mathf.Pow(1f - k, 3f);
        transform.localScale = new Vector3(1f, e, 1f);
    }
}

/// <summary>Chip chữ (nền màu) co theo độ dài chữ và ẩn nền khi chữ trống. Chip cần pivot trái.</summary>
public class ChipFit : MonoBehaviour
{
    public TMP_Text text;
    public float padding = 30f;
    private Image bg;
    private string last;

    void Awake() => bg = GetComponent<Image>();

    void LateUpdate()
    {
        if (text == null || text.text == last) return;
        last = text.text;
        bool has = !string.IsNullOrEmpty(last);
        if (bg != null) bg.enabled = has;
        var rt = (RectTransform)transform;
        float w = has ? text.GetPreferredValues(last).x + padding : 0f;
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        text.rectTransform.sizeDelta = new Vector2(w, rt.sizeDelta.y);
    }
}
