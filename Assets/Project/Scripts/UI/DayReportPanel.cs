using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Báo cáo cuối ngày (giống Supermarket Simulator): doanh thu, chi phí, lãi, số khách, số dư. Bấm Next Day để sang ngày mới.</summary>
public class DayReportPanel : MonoBehaviour
{
    private static DayReportPanel instance;
    private GameObject root;
    private TMP_Text titleText, leftText, rightText, profitText, balanceText;
    private Action onNext;

    private static readonly Color WindowColor = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    private static readonly Color HeaderColor = new Color(0.0f, 0.6f, 0.95f, 1f);
    private static readonly Color Green = new Color(0.35f, 0.85f, 0.4f);
    private static readonly Color Red = new Color(1f, 0.4f, 0.35f);

    public static void Show(DailySummary s, Action onNext)
    {
        if (instance == null) instance = new GameObject("DayReportPanel").AddComponent<DayReportPanel>();
        instance.Fill(s, onNext);
    }

    void Awake() => Build();

    void OnDestroy()
    {
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (root.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Next();
    }

    private void Fill(DailySummary s, Action next)
    {
        onNext = next;
        titleText.text = $"DAY {s.day} REPORT";

        string F(float v) => EconomyManager.Format(v);
        leftText.text =
            "<b>Income</b>\n" +
            $"Sales:  {F(s.Get(MoneyCategory.Sales))}\n\n" +
            $"Customers served:  {s.customersServed}\n" +
            $"Items sold:  {s.itemsSold}";
        ReviewManager.ForDay(s.day, out float rating, out int reviewCount);
        if (reviewCount > 0)
            leftText.text += $"\nRating:  {rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5  ({reviewCount})";
        string mix = ReviewManager.VisitorMix(s.day);
        if (mix.Length > 0) leftText.text += $"\n<size=80%>Visitors: {mix}</size>";
        rightText.text =
            "<b>Expenses</b>\n" +
            $"Products:  {F(-s.Get(MoneyCategory.Supplies))}\n" +
            $"Rent:  {F(-s.Get(MoneyCategory.Rent))}\n" +
            $"Wages:  {F(-s.Get(MoneyCategory.Wages))}\n" +
            $"Upgrades:  {F(-s.Get(MoneyCategory.Upgrades))}\n" +
            $"Other:  {F(-s.Get(MoneyCategory.Other))}";

        profitText.text = $"Profit:  {F(s.Profit)}";
        profitText.color = s.Profit >= 0f ? Green : Red;
        balanceText.text = $"Balance:  {F(s.openingBalance)}  ->  {F(s.closingBalance)}";
        balanceText.color = s.closingBalance >= 0f ? Color.white : Red;

        root.SetActive(true);
        UIState.Open(this);
    }

    private void Next()
    {
        root.SetActive(false);
        UIState.Close(this);
        Action a = onNext;
        onNext = null;
        a?.Invoke();
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = NewRect("Root", canvasGo.transform);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        GameObject window = NewRect("Window", root.transform, new Vector2(820, 560));
        window.AddComponent<Image>().color = WindowColor;
        GameObject header = NewRect("Header", window.transform, new Vector2(820, 80), new Vector2(0, 240));
        header.AddComponent<Image>().color = HeaderColor;
        titleText = Text(header.transform, "DAY REPORT", 40, new Vector2(780, 70), Vector2.zero, TextAlignmentOptions.Center, true);

        leftText = Text(window.transform, "", 28, new Vector2(360, 260), new Vector2(-195, 60), TextAlignmentOptions.TopLeft);
        rightText = Text(window.transform, "", 28, new Vector2(360, 260), new Vector2(195, 60), TextAlignmentOptions.TopLeft);
        profitText = Text(window.transform, "", 40, new Vector2(760, 56), new Vector2(0, -120), TextAlignmentOptions.Center, true);
        balanceText = Text(window.transform, "", 28, new Vector2(760, 44), new Vector2(0, -170), TextAlignmentOptions.Center);

        GameObject btn = NewRect("Button_Next", window.transform, new Vector2(260, 64), new Vector2(0, -235));
        btn.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f);
        btn.AddComponent<Button>().onClick.AddListener(Next);
        Text(btn.transform, "Next Day", 32, new Vector2(260, 64), Vector2.zero, TextAlignmentOptions.Center, true);

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

    private static TMP_Text Text(Transform parent, string text, float size, Vector2 box, Vector2 pos, TextAlignmentOptions align, bool bold = false)
    {
        GameObject go = NewRect("Text", parent, box, pos);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }
}
