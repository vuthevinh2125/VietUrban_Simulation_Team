using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bộ giao diện "Tiệm của bà Lan": mọi thứ trên màn hình là đồ vật ở tiệm tạp hoá (biển hiệu, lịch bloc, bìa các-tông,
/// giấy nhớ, con dấu, nhãn giá). Dựng bằng code trên uGUI, dùng chung cho menu, HUD, báo cáo cuối ngày, máy tính.
///  - Phông: Resources/Fonts/*.ttf (Bungee: biển hiệu; Anton: chữ in; Patrick Hand: chữ viết tay; Be Vietnam Pro: chữ thường),
///    tạo TMP font asset lúc chạy (atlas động). Thiếu file thì dùng phông mặc định của TextMeshPro.
///  - Giấy không bao giờ thẳng tuyệt đối: xoay 0.5 - 3 độ. Chữ in màu đỏ, chữ viết tay màu xanh bút bi, màu vàng là tiền.
/// </summary>
public static class ShopUI
{
    // ---------- Màu ----------
    public static readonly Color Wall = Hex(0xC9, 0x99, 0x3A);
    public static readonly Color SignRed = Hex(0xC8, 0x16, 0x1D);
    public static readonly Color SignGold = Hex(0xFF, 0xC2, 0x3D);
    public static readonly Color ShelfYellow = Hex(0xFF, 0xD4, 0x00);
    public static readonly Color Paper = Hex(0xFB, 0xF7, 0xEC);
    public static readonly Color PaperOld = Hex(0xF4, 0xEC, 0xD8);
    public static readonly Color Pen = Hex(0x1F, 0x3F, 0xA8);
    public static readonly Color Ink = Hex(0x1B, 0x14, 0x10);
    public static readonly Color Cardboard = Hex(0xB9, 0x8C, 0x58);
    public static readonly Color CardboardDark = Hex(0x8F, 0x6A, 0x40);
    public static readonly Color Note = Hex(0xFF, 0xF8, 0xB8);
    public static readonly Color Night = Hex(0x14, 0x10, 0x0A);
    public static readonly Color Muted = Hex(0x5F, 0x5A, 0x50);
    public static readonly Color Enamel = Hex(0x1D, 0x4F, 0xA3);
    /// <summary>Gỗ quầy (nền cửa sổ quản lý), hàng trong bảng, nút thường.</summary>
    public static readonly Color Wood = new Color(0.17f, 0.115f, 0.075f, 0.98f);
    public static readonly Color WoodRow = new Color(0.27f, 0.19f, 0.125f, 1f);
    public static readonly Color WoodButton = new Color(0.4f, 0.29f, 0.18f, 1f);

    private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ---------- Phông ----------
    private static readonly Dictionary<string, TMP_FontAsset> fonts = new Dictionary<string, TMP_FontAsset>();
    public static TMP_FontAsset Sign => GetFont("Bungee-Regular");
    public static TMP_FontAsset Print => GetFont("Anton-Regular");
    public static TMP_FontAsset Hand => GetFont("PatrickHand-Regular");
    public static TMP_FontAsset Body => GetFont("BeVietnamPro-Regular");

    private static TMP_FontAsset GetFont(string file)
    {
        if (fonts.TryGetValue(file, out TMP_FontAsset fa) && fa != null) return fa;
        fa = null;
        UnityEngine.Font f = Resources.Load<UnityEngine.Font>("Fonts/" + file);
        if (f != null)
        {
            try { fa = TMP_FontAsset.CreateFontAsset(f); } catch { fa = null; }
            if (fa != null)
            {
                fa.name = file + " (runtime)";
                TMP_FontAsset def = TMP_Settings.defaultFontAsset;       // ký tự phông này thiếu thì lấy ở phông mặc định
                if (def != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { def };
            }
        }
        if (fa == null) fa = TMP_Settings.defaultFontAsset;
        fonts[file] = fa;
        return fa;
    }

    // ---------- Khối cơ bản ----------
    public static RectTransform Box(string name, Transform parent, Vector2 size, Vector2 pos, Color color, float rot = 0f, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        if (rot != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        Image img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = raycast;
        return rt;
    }

    /// <summary>Nhóm rỗng (không vẽ gì) để xoay / lắc cả cụm.</summary>
    public static RectTransform Group(string name, Transform parent, Vector2 size, Vector2 pos, float rot = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        if (rot != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        return rt;
    }

    public static RectTransform Full(string name, Transform parent, Color color, bool raycast = true)
    {
        RectTransform rt = Box(name, parent, Vector2.zero, Vector2.zero, color, 0f, raycast);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static TMP_Text Text(Transform parent, string text, TMP_FontAsset font, float size, Vector2 box, Vector2 pos,
                                Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, float rot = 0f, float spacing = 0f)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = box; rt.anchoredPosition = pos;
        if (rot != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Tờ giấy / tấm bìa có bóng đổ.</summary>
    public static RectTransform Sheet(string name, Transform parent, Vector2 size, Vector2 pos, Color color, float rot = 0f, bool raycast = false)
    {
        RectTransform rt = Box(name, parent, size, pos, color, rot, raycast);
        Shadow sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sh.effectDistance = new Vector2(6f, -10f);
        return rt;
    }

    /// <summary>Miếng băng dính giấy dán ở mép trên.</summary>
    public static void Tape(Transform sheet, float x = 0f, float rot = -4f)
    {
        var parent = (RectTransform)sheet;
        RectTransform t = Box("Tape", sheet, new Vector2(96f, 28f), new Vector2(x, parent.sizeDelta.y * 0.5f - 2f), new Color(0.98f, 0.94f, 0.8f, 0.85f), rot);
        t.SetAsLastSibling();
    }

    /// <summary>Khung viền (4 thanh) quanh một hình chữ nhật.</summary>
    public static void Frame(Transform parent, Vector2 size, Color color, float thick)
    {
        Box("FrameT", parent, new Vector2(size.x, thick), new Vector2(0f, size.y * 0.5f - thick * 0.5f), color);
        Box("FrameB", parent, new Vector2(size.x, thick), new Vector2(0f, -size.y * 0.5f + thick * 0.5f), color);
        Box("FrameL", parent, new Vector2(thick, size.y), new Vector2(-size.x * 0.5f + thick * 0.5f, 0f), color);
        Box("FrameR", parent, new Vector2(thick, size.y), new Vector2(size.x * 0.5f - thick * 0.5f, 0f), color);
    }

    /// <summary>Con dấu đỏ (khung + chữ), hơi nghiêng.</summary>
    public static RectTransform Stamp(Transform parent, string text, Vector2 pos, float fontSize = 30f, float rot = -8f)
    {
        var size = new Vector2(fontSize * 0.62f * text.Length + 36f, fontSize * 1.5f);
        RectTransform g = Group("Stamp", parent, size, pos, rot);
        Color c = new Color(0.82f, 0.13f, 0.16f, 0.9f);
        Frame(g, size, c, 4f);
        Text(g, text, Print, fontSize, size, Vector2.zero, c, TextAlignmentOptions.Center, 0f, 6f);
        return g;
    }

    /// <summary>Biển hiệu đỏ viền vàng.</summary>
    public static RectTransform SignBoard(Transform parent, Vector2 size, Vector2 pos, float rot = 0f)
    {
        RectTransform g = Sheet("Sign", parent, size, pos, SignGold, rot);
        Box("Red", g, size - new Vector2(12f, 12f), Vector2.zero, SignRed);
        return g;
    }

    /// <summary>Tấm bìa các-tông viết bút dạ, bấm được (mục menu).</summary>
    public static Button CardButton(Transform parent, string label, string note, Vector2 size, Vector2 pos, float rot,
                                    UnityEngine.Events.UnityAction onClick, Color? color = null, Color? ink = null)
    {
        RectTransform rt = Sheet("Card_" + label, parent, size, pos, color ?? Cardboard, rot, true);
        Button b = rt.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        if (onClick != null) b.onClick.AddListener(onClick);
        bool hasNote = !string.IsNullOrEmpty(note);
        Text(rt, label, Hand, size.y * 0.5f, new Vector2(size.x - 40f, size.y * 0.6f), new Vector2(0f, hasNote ? size.y * 0.13f : 0f), ink ?? Ink, TextAlignmentOptions.Left);
        if (hasNote) Text(rt, note, Hand, size.y * 0.23f, new Vector2(size.x - 40f, size.y * 0.3f), new Vector2(0f, -size.y * 0.27f), A(ink ?? Ink, 0.75f), TextAlignmentOptions.Left);
        Tape(rt, 0f, -3f);
        UIMotion m = rt.gameObject.AddComponent<UIMotion>();
        m.hoverScale = 1.06f; m.hoverRotate = rot > 0f ? -1.5f : 1.5f;
        return b;
    }

    /// <summary>Nút chữ in (đỏ chữ vàng / đen chữ vàng / viền đen).</summary>
    public static Button PrintButton(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, int style = 0)
    {
        Color bg = style == 0 ? SignRed : style == 1 ? Ink : Paper;
        Color fg = style == 2 ? Ink : style == 0 ? new Color(1f, 0.95f, 0.84f) : SignGold;
        RectTransform rt = Box("Button_" + label, parent, size, pos, bg, 0f, true);
        if (style == 0) Frame(rt, size - new Vector2(6f, 6f), SignGold, 3f);
        if (style == 2) Frame(rt, size, Ink, 3f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        if (onClick != null) b.onClick.AddListener(onClick);
        Text(rt, label, Print, size.y * 0.46f, size, Vector2.zero, fg, TextAlignmentOptions.Center, 0f, 6f);
        rt.gameObject.AddComponent<UIMotion>().hoverScale = 1.05f;
        return b;
    }

    /// <summary>
    /// Đổi tông một giao diện có sẵn (dựng trong scene) sang tông tiệm tạp hoá: xám xanh -> gỗ nâu, xanh dương -> đỏ biển hiệu,
    /// chữ dùng phông Be Vietnam Pro. Gọi lại nhiều lần không sao (màu đã đổi thì không khớp điều kiện nữa).
    /// </summary>
    public static void Skin(Transform root)
    {
        if (root == null) return;
        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text t)
            {
                if (t.font != Body && t.font != Print && t.font != Hand && t.font != Sign)
                    t.font = t.GetComponentInParent<Button>() != null ? Print : Body;
                Color tc = t.color;
                if (tc.b > tc.r + 0.4f && tc.b > 0.7f) t.color = new Color(SignGold.r, SignGold.g, SignGold.b, tc.a);
                continue;
            }
            Color c = g.color;
            if (c.a < 0.3f) continue;
            float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float spread = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
            if (c.b > c.r + 0.25f && c.b > 0.6f && spread > 0.4f) g.color = new Color(SignRed.r, SignRed.g, SignRed.b, c.a);
            else if (l < 0.5f && c.b >= c.r + 0.03f && spread < 0.3f) g.color = new Color(Mathf.Min(1f, l * 1.3f), l * 0.92f, l * 0.6f, c.a);
        }
    }

    // ---------- Cửa sổ quản lý (máy tính) ----------
    public static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f, 1f);
    /// <summary>Mặt quầy gỗ sẫm làm nền cửa sổ.</summary>
    public static readonly Color Counter = new Color(0.115f, 0.08f, 0.055f, 0.985f);

    /// <summary>
    /// Mặc "đồ tiệm tạp hoá" cho một cửa sổ đã dựng xong (Window có con "Header"): nền gỗ sẫm + viền vàng + bóng đổ,
    /// đầu cửa sổ là biển đỏ chữ vàng, hàng có vạch đỏ bên trái, nút kiểu in (đỏ viền vàng / viền trắng / mực),
    /// chữ trắng thành màu kem. Có giấy nhớ của bà Lan (note) dán cạnh cửa sổ nếu truyền vào. Chỉ đổi giao diện, không đổi hành vi.
    /// </summary>
    public static void DressWindow(Transform window, string note = null)
    {
        if (window == null) return;
        var rt = (RectTransform)window;
        Vector2 size = rt.sizeDelta;
        Image wi = window.GetComponent<Image>();
        if (wi != null) wi.color = Counter;
        if (window.GetComponent<Shadow>() == null)
        {
            Shadow sh = window.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(0f, -14f);
        }
        RectTransform line = Group("GoldLine", window, size, Vector2.zero);
        Frame(line, size - new Vector2(16f, 16f), A(SignGold, 0.35f), 2f);
        line.SetAsFirstSibling();

        Transform header = window.Find("Header");
        if (header != null)
        {
            var hrt = (RectTransform)header;
            Image hi = header.GetComponent<Image>();
            if (hi != null) hi.color = SignRed;
            foreach (TMP_Text t in header.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<Button>() != null) continue;
                t.font = Print; t.color = SignGold; t.fontStyle = FontStyles.UpperCase; t.characterSpacing = 6f;
            }
            Box("HeaderLine", header, new Vector2(hrt.sizeDelta.x, 4f), new Vector2(0f, -hrt.sizeDelta.y * 0.5f + 2f), SignGold);
            if (hrt.sizeDelta.x >= 900f)
            {
                TMP_Text tag = Text(header, "BÀ LAN MART", Body, 16, new Vector2(220, 30), new Vector2(-hrt.sizeDelta.x * 0.5f + 135f, 0f), A(Cream, 0.8f), TextAlignmentOptions.Left, 0f, 6f);
                tag.fontStyle = FontStyles.Bold;
            }
        }

        foreach (TMP_Text t in window.GetComponentsInChildren<TMP_Text>(true))
        {
            if (header != null && t.transform.IsChildOf(header)) continue;
            if (t.GetComponentInParent<Button>() != null) continue;
            if (t.font != Print && t.font != Hand && t.font != Sign) t.font = Body;
            Color c = t.color;
            if (c.r > 0.9f && c.g > 0.9f && c.b > 0.9f) t.color = new Color(Cream.r, Cream.g, Cream.b, c.a);
        }

        foreach (Image img in window.GetComponentsInChildren<Image>(true))
        {
            string n = img.gameObject.name;
            if (!(n.StartsWith("Row_") || n == "Row") || img.color.a < 0.5f) continue;
            var r = (RectTransform)img.transform;
            if (r.sizeDelta.y < 50f) continue;
            Box("Accent", img.transform, new Vector2(6f, r.sizeDelta.y), new Vector2(-r.sizeDelta.x * 0.5f + 3f, 0f), SignRed);
        }

        foreach (Button b in window.GetComponentsInChildren<Button>(true)) DressButton(b);

        if (!string.IsNullOrEmpty(note)) StickyNote(window, note, size);
    }

    /// <summary>Nút kiểu "in": xanh lá/đỏ = nút chính (đỏ viền vàng), đỏ cam = nút phụ (viền kem), xanh dương = nút mực chữ vàng, X = ô mực.</summary>
    public static void DressButton(Button b)
    {
        if (b == null || b.GetComponent<UIMotion>() != null) return;          // đã mặc rồi
        Image img = b.GetComponent<Image>();
        TMP_Text lbl = b.GetComponentInChildren<TMP_Text>(true);
        var brt = (RectTransform)b.transform;
        Vector2 s = brt.sizeDelta;
        string label = lbl != null ? lbl.text.Trim() : "";
        Color c = img != null ? img.color : Color.clear;
        bool green = c.g > c.r + 0.2f && c.g > c.b + 0.1f;
        bool red = Mathf.Abs(c.r - SignRed.r) < 0.06f && Mathf.Abs(c.g - SignRed.g) < 0.06f && Mathf.Abs(c.b - SignRed.b) < 0.06f;
        bool orange = !red && c.r > c.g + 0.25f && c.r > 0.5f;
        bool blue = c.b > c.r + 0.2f;
        Color fg = Cream;
        if (label == "X")
        {
            if (img != null) img.color = Ink;
            Frame(brt, s, A(Cream, 0.45f), 2f);
            if (lbl != null) { lbl.font = Body; lbl.fontStyle = FontStyles.Bold; lbl.color = Cream; }
            b.gameObject.AddComponent<UIMotion>().hoverScale = 1.1f;
            return;
        }
        if (green || red) { if (img != null) img.color = SignRed; Frame(brt, s - new Vector2(6f, 6f), SignGold, 2f); }
        else if (orange) { if (img != null) img.color = A(Ink, 0.6f); Frame(brt, s, A(Cream, 0.6f), 2f); }
        else if (blue) { if (img != null) img.color = Ink; Frame(brt, s, A(SignGold, 0.6f), 2f); fg = SignGold; }
        else Frame(brt, s, A(SignGold, 0.3f), 2f);
        if (lbl != null)
        {
            lbl.font = Print; lbl.fontStyle = FontStyles.UpperCase; lbl.characterSpacing = 3f; lbl.color = fg;
        }
        b.gameObject.AddComponent<UIMotion>().hoverScale = 1.05f;
    }

    /// <summary>Giấy nhớ vàng của bà Lan, dán băng dính ở cạnh phải cửa sổ (hoặc phía trên nếu cửa sổ quá rộng).</summary>
    public static RectTransform StickyNote(Transform window, string text, Vector2 windowSize)
    {
        Vector2 size = new Vector2(300f, 118f);
        Vector2 pos;
        float rot;
        if (windowSize.x * 0.5f + size.x - 30f <= 940f) { pos = new Vector2(windowSize.x * 0.5f + size.x * 0.5f - 24f, windowSize.y * 0.5f - 170f); rot = 3f; }
        else if (windowSize.y * 0.5f + size.y <= 530f) { pos = new Vector2(-windowSize.x * 0.5f + 230f, windowSize.y * 0.5f + size.y * 0.5f - 18f); rot = -2.5f; }
        else return null;
        RectTransform n = Sheet("StickyNote", window, size, pos, Note, rot);
        Tape(n, 0f, -5f);
        TMP_Text t = Text(n, text, Hand, 23, size - new Vector2(30f, 22f), new Vector2(0f, -4f), Pen, TextAlignmentOptions.Left);
        t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 23;
        t.lineSpacing = -12f;
        UIMotion m = n.gameObject.AddComponent<UIMotion>();
        m.swayDegrees = 0.8f; m.swaySpeed = 0.7f;
        return n;
    }

    // ---------- Hình tạo bằng code ----------
    private static Sprite glow, vignette;

    /// <summary>Đốm sáng tròn mờ dần ra mép (ánh đèn).</summary>
    public static Sprite Glow => glow != null ? glow : (glow = Radial(false));
    /// <summary>Tối dần ra bốn góc.</summary>
    public static Sprite Vignette => vignette != null ? vignette : (vignette = Radial(true));

    private static Sprite Radial(bool inverse)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = inverse ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d)) : Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    public static Image GlowLight(Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        RectTransform rt = Box("Light", parent, size, pos, color);
        Image img = rt.GetComponent<Image>();
        img.sprite = Glow;
        return img;
    }
}

/// <summary>
/// Chuyển động nhỏ cho đồ vật giao diện, chạy theo thời gian thật (menu mở lúc Time.timeScale = 0 vẫn động):
/// đung đưa (sway), phập phồng (pulse), chập chờn (flicker), nhấc lên khi rê chuột (hover), rơi vào lúc hiện (dropIn).
/// </summary>
public class UIMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float swayDegrees, swaySpeed = 1f;
    public float pulse, pulseSpeed = 4f;
    public float flicker;                 // 0..1: độ sụt sáng khi chập chờn
    public float hoverScale = 1f, hoverRotate;
    public float dropIn;                  // > 0: lúc hiện, trượt từ trên xuống quãng này (px) rồi nảy nhẹ

    private RectTransform rt;
    private Graphic graphic;
    private Quaternion baseRot;
    private Vector2 basePos;
    private Vector3 baseScale;
    private float baseAlpha, hover, hoverTarget, shownAt, seed;
    private bool ready;

    void Awake() => Init();

    private void Init()
    {
        if (ready) return;
        rt = (RectTransform)transform;
        graphic = GetComponent<Graphic>();
        baseRot = rt.localRotation; basePos = rt.anchoredPosition; baseScale = rt.localScale;
        baseAlpha = graphic != null ? graphic.color.a : 1f;
        seed = Random.value * 10f;
        ready = true;
    }

    void OnEnable() { Init(); shownAt = Time.unscaledTime; hover = hoverTarget = 0f; }

    public void OnPointerEnter(PointerEventData e) => hoverTarget = 1f;
    public void OnPointerExit(PointerEventData e) => hoverTarget = 0f;

    void Update()
    {
        float t = Time.unscaledTime;
        hover = Mathf.MoveTowards(hover, hoverTarget, Time.unscaledDeltaTime * 8f);

        float rot = hoverRotate * hover;
        if (swayDegrees != 0f) rot += Mathf.Sin((t + seed) * swaySpeed) * swayDegrees;
        rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, rot);

        float s = 1f + (hoverScale - 1f) * hover;
        if (pulse != 0f) s += Mathf.Sin(t * pulseSpeed) * pulse;
        rt.localScale = baseScale * s;

        if (dropIn > 0f)
        {
            float k = Mathf.Clamp01((t - shownAt) / 0.45f);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float bounce = Mathf.Sin(k * Mathf.PI) * 0.06f;
            rt.anchoredPosition = basePos + new Vector2(0f, dropIn * (1f - ease) - dropIn * bounce * (1f - k));
        }

        if (flicker > 0f && graphic != null)
        {
            float n = Mathf.PerlinNoise(t * 9f, seed);
            float dip = (Mathf.PerlinNoise(t * 0.35f, seed + 3f) > 0.72f && n > 0.5f) ? flicker : 0f;
            Color c = graphic.color; c.a = baseAlpha * (1f - dip); graphic.color = c;
        }
    }
}
