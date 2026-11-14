using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thẻ gợi ý khi nhìn vào đồ vật (thay cho dòng chữ trắng to giữa màn hình):
/// nằm lệch phải-dưới tâm ngắm, nền tối có vạch đỏ, dòng đầu là tên (kèm ô "40/40" + thanh nhỏ nếu có số lượng),
/// các dòng sau là chi tiết chữ nhỏ, cuối cùng là hàng phím bấm (ô phím màu kem + việc làm).
/// Tự tách "[LMB] Set price", "[F] Move"... từ câu gợi ý cũ, nên không phải sửa chỗ gọi SetPrompt.
/// </summary>
public class InteractCard : MonoBehaviour
{
    private static InteractCard instance;
    private static readonly Regex HintRx = new Regex(@"\[([^\]\n]{1,12})\]\s*([^\[\n|]*)");
    private static readonly Regex StockRx = new Regex(@"^(.*?)[\s(]+(\d+)\s*/\s*(\d+)\)?\s*(boxes)?$");

    private RectTransform card;
    private CanvasGroup group;
    private string current;
    private float alpha, target;

    private const float MaxW = 560f, MinW = 300f, Pad = 22f;

    public static void Show(string message)
    {
        if (instance == null) instance = new GameObject("InteractCard").AddComponent<InteractCard>();
        instance.Set(message);
    }

    public static void Hide()
    {
        if (instance != null) instance.target = 0f;
    }

    void Awake()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 45).transform;
        card = ShopUI.Group("Card", canvas, new Vector2(MinW, 100f), Vector2.zero);
        card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(70f, -40f);       // lệch phải-dưới tâm màn hình, không che món đồ
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;
        group.alpha = 0f;
    }

    void Update()
    {
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 8f);
        group.alpha = alpha;
        card.anchoredPosition = new Vector2(70f - (1f - alpha) * 12f, -40f);
        if (alpha <= 0f && target <= 0f) current = null;
    }

    private void Set(string message)
    {
        target = 1f;
        if (message == current) return;
        current = message;
        foreach (Transform c in card) Destroy(c.gameObject);

        // --- Tách nội dung ---
        var hints = new List<(string key, string what)>();
        string body = message;
        bool sentence = message.Contains("Press [");                    // câu cảnh báo có phím ở giữa câu: giữ nguyên
        if (!sentence)
        {
            foreach (Match m in HintRx.Matches(message))
                hints.Add((m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim().TrimEnd('.', ',')));
            body = HintRx.Replace(message, "");
        }
        var lines = new List<string>();
        foreach (string raw in body.Replace("|", "\n").Split('\n'))
        {
            string l = Regex.Replace(raw, @"\s{2,}", "  ").Trim();
            if (StripTags(l).Trim().Length > 0) lines.Add(l);
        }
        if (lines.Count == 0 && hints.Count == 0) { target = 0f; return; }

        string title = lines.Count > 0 ? lines[0] : "";
        string stock = null;
        float fill = -1f;
        Match sm = StockRx.Match(StripTags(title));
        if (lines.Count > 0 && sm.Success && int.TryParse(sm.Groups[2].Value, out int have) && int.TryParse(sm.Groups[3].Value, out int cap) && cap > 0)
        {
            title = sm.Groups[1].Value.Trim().TrimEnd(':');
            stock = $"{have}/{cap}{(sm.Groups[4].Success ? " boxes" : "")}";
            fill = Mathf.Clamp01(have / (float)cap);
        }

        // --- Đo bề rộng ---
        TMP_Text titleT = ShopUI.Text(card, title, ShopUI.Body, 24, new Vector2(MaxW, 40f), Vector2.zero, Color.white, TextAlignmentOptions.TopLeft);
        titleT.fontStyle = FontStyles.Bold;
        float stockW = stock != null ? 150f : 0f;
        float w = Mathf.Max(MinW, titleT.GetPreferredValues(title).x + stockW + Pad * 2f + 10f);
        var detailTexts = new List<TMP_Text>();
        for (int i = 1; i < lines.Count; i++)
        {
            TMP_Text d = ShopUI.Text(card, lines[i], ShopUI.Body, 19, new Vector2(MaxW, 30f), Vector2.zero, ShopPC.Hex(0xC9CFD8), TextAlignmentOptions.TopLeft);
            d.richText = true;
            w = Mathf.Max(w, d.GetPreferredValues(lines[i]).x + Pad * 2f + 10f);
            detailTexts.Add(d);
        }
        float hintsW = Pad * 2f;
        foreach (var (k, what) in hints) hintsW += Mathf.Max(34f, k.Length * 11f + 18f) + 8f + what.Length * 9.5f + 22f;
        w = Mathf.Clamp(Mathf.Max(w, hintsW), MinW, MaxW);
        float inner = w - Pad * 2f - 6f;

        // --- Xếp từ trên xuống ---
        float y = 16f;
        titleT.textWrappingMode = TextWrappingModes.Normal;
        titleT.overflowMode = TextOverflowModes.Ellipsis;
        float th = Mathf.Min(64f, titleT.GetPreferredValues(title, inner - stockW, 0f).y);
        Place(titleT.rectTransform, Pad + 6f, y, inner - stockW, th, w);
        if (stock != null)
        {
            TMP_Text st = ShopUI.Text(card, stock, ShopUI.Body, 17, new Vector2(140f, 24f), Vector2.zero, Color.white, TextAlignmentOptions.Right);
            st.fontStyle = FontStyles.Bold;
            Place(st.rectTransform, w - Pad - 140f, y + 2f, 140f, 24f, w);
            RectTransform track = ShopUI.Box("StockBar", card, new Vector2(110f, 6f), Vector2.zero, ShopPC.A(Color.white, 0.15f));
            Place(track, w - Pad - 110f, y + 28f, 110f, 6f, w);
            Color fc = fill < 0.25f ? ShopPC.Hex(0xFF6B5B) : fill < 0.6f ? ShopPC.Gold : ShopPC.Mint;
            ShopUI.Box("Fill", track, new Vector2(110f * fill, 6f), new Vector2(-55f + 55f * fill, 0f), fc);
        }
        y += Mathf.Max(th, stock != null ? 36f : 0f) + 6f;
        foreach (TMP_Text d in detailTexts)
        {
            d.textWrappingMode = TextWrappingModes.Normal;
            float dh = d.GetPreferredValues(d.text, inner, 0f).y;
            Place(d.rectTransform, Pad + 6f, y, inner, dh, w);
            y += dh + 2f;
        }
        if (hints.Count > 0)
        {
            y += 8f;
            RectTransform rule = ShopUI.Box("Rule", card, new Vector2(inner, 1.5f), Vector2.zero, ShopPC.A(Color.white, 0.1f));
            Place(rule, Pad + 6f, y, inner, 1.5f, w);
            y += 12f;
            float x = Pad + 6f;
            foreach (var (k, what) in hints)
            {
                TMP_Text kt = ShopUI.Text(card, k, ShopUI.Body, 15, new Vector2(120f, 30f), Vector2.zero, ShopPC.Hex(0x14100A), TextAlignmentOptions.Center);
                kt.fontStyle = FontStyles.Bold;
                kt.textWrappingMode = TextWrappingModes.NoWrap;
                float kw = Mathf.Max(34f, kt.GetPreferredValues(k).x + 16f);
                TMP_Text wt = ShopUI.Text(card, what, ShopUI.Body, 18, new Vector2(300f, 30f), Vector2.zero, ShopPC.Hex(0xF4ECD8), TextAlignmentOptions.Left);
                wt.textWrappingMode = TextWrappingModes.NoWrap;
                float ww = wt.GetPreferredValues(what).x + 4f;
                if (x + kw + 8f + ww > w - Pad && x > Pad + 6f) { x = Pad + 6f; y += 38f; }
                RectTransform keyCap = ShopUI.Box("Key", card, new Vector2(kw, 30f), Vector2.zero, ShopPC.Hex(0xF4ECD8));
                Place(keyCap, x, y, kw, 30f, w);
                kt.transform.SetParent(keyCap, false);
                kt.rectTransform.anchoredPosition = Vector2.zero; kt.rectTransform.sizeDelta = keyCap.sizeDelta;
                Place(wt.rectTransform, x + kw + 8f, y, ww, 30f, w);
                x += kw + 8f + ww + 22f;
            }
            y += 30f;
        }
        y += 16f;

        // --- Nền (đặt xuống dưới cùng) ---
        card.sizeDelta = new Vector2(w, y);
        RectTransform bg = ShopUI.Box("Bg", card, new Vector2(w, y), new Vector2(0f, 0f), ShopPC.A(ShopPC.Bg, 0.88f));
        bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
        bg.SetAsFirstSibling();
        Shadow sh = bg.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.45f); sh.effectDistance = new Vector2(0f, -8f);
        RectTransform strip = ShopUI.Box("Strip", card, new Vector2(6f, y), new Vector2(-w * 0.5f + 3f, 0f), ShopPC.Red);
        strip.SetSiblingIndex(1);
    }

    /// <summary>Đặt ô (x, y, w, h) tính từ góc trên trái của thẻ rộng cardW (thẻ có pivot góc trên trái).</summary>
    private void Place(RectTransform rt, float x, float y, float w, float h, float cardW)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private static string StripTags(string s) => Regex.Replace(s ?? "", "<[^>]+>", "");
}
