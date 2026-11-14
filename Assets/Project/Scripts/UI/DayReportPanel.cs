using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Báo cáo cuối ngày kiểu "sổ sách tiệm tạp hoá": trên mặt quầy gỗ là tờ lịch bloc của ngày vừa xong và tờ HOÁ ĐƠN BÁN LẺ
/// in đỏ, số liệu viết tay mực xanh (doanh thu, chi phí, lãi, số khách, đánh giá, hàng hỏng, số dư).
/// Từng dòng hiện ra lần lượt, rồi con dấu đập xuống. Bấm nút / Enter: tờ lịch bị xé, sang ngày mới.
/// (Enter lúc các dòng đang hiện = hiện hết ngay.) Chạy theo thời gian thật, không phụ thuộc Time.timeScale.
/// </summary>
public class DayReportPanel : MonoBehaviour
{
    private static DayReportPanel instance;
    private GameObject root;
    private TMP_Text dayNumber, nextNumber, invoiceNo, profitText, balanceText, stampText, nextLabel;
    private RectTransform page, invoice, rowsRoot, stamp;
    private readonly List<GameObject> rows = new List<GameObject>();
    private Action onNext;
    private Coroutine reveal;
    private bool revealing, tearing;
    private Vector2 pagePos, invoicePos;
    private Quaternion pageRot;

    private static readonly Color InvoicePaper = new Color(1f, 0.957f, 0.95f);
    private static readonly Color InvoiceRed = new Color(0.7f, 0.125f, 0.165f);
    private static readonly Color LossRed = new Color(0.75f, 0.22f, 0.17f);

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
        if (!root.activeSelf || tearing) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Next();
    }

    private void Fill(DailySummary s, Action next)
    {
        onNext = next;
        string F(float v) => EconomyManager.Format(v);

        dayNumber.text = s.day.ToString();
        nextNumber.text = (s.day + 1).ToString();
        invoiceNo.text = s.day.ToString("0000000");
        nextLabel.text = $"TEAR THE PAGE · DAY {s.day + 1}";

        foreach (GameObject r in rows) Destroy(r);
        rows.Clear();
        float y = 250f;
        void Money(string label, float value, bool income)
        {
            rows.Add(Row(label, F(value), income ? ShopUI.Pen : (Mathf.Approximately(value, 0f) ? ShopUI.Muted : LossRed), y, 34f));
            y -= 50f;
        }
        void NoteLine(string text, Color color)
        {
            rows.Add(Row(text, "", color, y, 28f));
            y -= 42f;
        }
        Money("Sales", s.Get(MoneyCategory.Sales), true);
        Money("Products bought", -s.Get(MoneyCategory.Supplies), false);
        Money("Rent", -s.Get(MoneyCategory.Rent), false);
        Money("Wages", -s.Get(MoneyCategory.Wages), false);
        Money("Upgrades", -s.Get(MoneyCategory.Upgrades), false);
        Money("Other", -s.Get(MoneyCategory.Other), false);
        y -= 6f;
        NoteLine($"{s.customersServed} customers served, {s.itemsSold} items sold", ShopUI.Pen);
        ReviewManager.ForDay(s.day, out float rating, out int reviewCount);
        if (reviewCount > 0)
            NoteLine($"Rating today: {rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5 ({reviewCount} reviews)", ShopUI.Pen);
        string mix = ReviewManager.VisitorMix(s.day);
        if (mix.Length > 0) NoteLine($"Visitors: {mix}", ShopUI.Pen);
        if (s.spoiledItems > 0) NoteLine($"Spoiled: {s.spoiledItems} item(s), {F(s.spoiledValue)} lost", LossRed);

        profitText.text = F(s.Profit);
        profitText.color = s.Profit >= 0f ? ShopUI.Pen : LossRed;
        balanceText.text = $"Balance:  {F(s.openingBalance)}  ->  {F(s.closingBalance)}";
        balanceText.color = s.closingBalance >= 0f ? ShopUI.Ink : LossRed;
        stampText.text = s.Profit >= 0f ? "ĐÃ THU TIỀN" : "THUA LỖ";

        page.anchoredPosition = pagePos; page.localRotation = pageRot;
        page.GetComponent<CanvasGroup>().alpha = 1f;
        tearing = false;
        root.SetActive(true);
        UIState.Open(this);
        if (reveal != null) StopCoroutine(reveal);
        reveal = StartCoroutine(Reveal());
    }

    /// <summary>Từng dòng hoá đơn hiện ra, rồi tiền lãi, rồi con dấu đập xuống (tờ hoá đơn rung nhẹ).</summary>
    private IEnumerator Reveal()
    {
        revealing = true;
        foreach (GameObject r in rows) r.SetActive(false);
        profitText.gameObject.SetActive(false);
        balanceText.gameObject.SetActive(false);
        stamp.gameObject.SetActive(false);
        yield return Wait(0.35f);
        foreach (GameObject r in rows)
        {
            if (!revealing) break;
            r.SetActive(true);
            AudioManager.Play("scan", 0.15f);
            yield return Wait(0.13f);
        }
        if (revealing) yield return Wait(0.2f);
        ShowAll(false);
        if (revealing) yield return Wait(0.35f);
        stamp.gameObject.SetActive(true);
        float t = 0f;
        while (t < 0.16f)
        {
            t += Time.unscaledDeltaTime;
            stamp.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, Mathf.Clamp01(t / 0.16f));
            yield return null;
        }
        stamp.localScale = Vector3.one;
        AudioManager.Play("box_drop", 0.5f);
        t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - t / 0.22f;
            invoice.anchoredPosition = invoicePos + new Vector2(Mathf.Sin(t * 90f), Mathf.Cos(t * 70f)) * (7f * k);
            yield return null;
        }
        invoice.anchoredPosition = invoicePos;
        revealing = false;
        reveal = null;
    }

    private void ShowAll(bool withStamp)
    {
        foreach (GameObject r in rows) r.SetActive(true);
        profitText.gameObject.SetActive(true);
        balanceText.gameObject.SetActive(true);
        if (withStamp) { stamp.gameObject.SetActive(true); stamp.localScale = Vector3.one; }
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    private void Next()
    {
        if (tearing) return;
        if (revealing)                                   // đang hiện dần: hiện hết ngay, chưa sang ngày
        {
            revealing = false;
            if (reveal != null) StopCoroutine(reveal);
            reveal = null;
            invoice.anchoredPosition = invoicePos;
            ShowAll(true);
            return;
        }
        StartCoroutine(Tear());
    }

    /// <summary>Xé tờ lịch: giật nhẹ rồi xoay, rơi khỏi màn hình; xong thì sang ngày mới.</summary>
    private IEnumerator Tear()
    {
        tearing = true;
        AudioManager.Play("sweep", 0.6f);
        CanvasGroup cg = page.GetComponent<CanvasGroup>();
        float t = 0f;
        const float dur = 0.55f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur), e = k * k;
            page.localRotation = pageRot * Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -46f, e) + Mathf.Sin(k * 20f) * 3f * (1f - k));
            page.anchoredPosition = pagePos + new Vector2(-260f * e, -760f * e);
            cg.alpha = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
            yield return null;
        }
        root.SetActive(false);
        UIState.Close(this);
        tearing = false;
        Action a = onNext;
        onNext = null;
        a?.Invoke();
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
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
        const TextAlignmentOptions Left = TextAlignmentOptions.Left, Right = TextAlignmentOptions.Right, Center = TextAlignmentOptions.Center;

        // Mặt quầy gỗ
        RectTransform bg = ShopUI.Full("Root", canvasGo.transform, new Color(0.23f, 0.145f, 0.08f));
        root = bg.gameObject;
        for (int i = 0; i < 40; i++) ShopUI.Box("Grain", bg, new Vector2(3, 3000), new Vector2(-1950 + i * 100 + (i % 3) * 17, 0), new Color(0f, 0f, 0f, 0.13f));
        ShopUI.GlowLight(bg, new Vector2(1900, 1300), new Vector2(0, 80), new Color(1f, 0.84f, 0.59f, 0.3f));
        ShopUI.Full("Vignette", bg, new Color(0f, 0f, 0f, 0.85f), false).GetComponent<Image>().sprite = ShopUI.Vignette;

        // Lịch bloc: tờ ngày mai nằm dưới, tờ hôm nay nằm trên (sẽ bị xé)
        RectTransform cal = ShopUI.Group("Calendar", bg, new Vector2(420, 560), new Vector2(-640, 90));
        RectTransform under = ShopUI.Sheet("NextPage", cal, new Vector2(400, 500), new Vector2(14, -24), new Color(0.95f, 0.93f, 0.87f), 3f);
        ShopUI.Text(under, "NGÀY · DAY", ShopUI.Body, 16, new Vector2(380, 26), new Vector2(0, 150), ShopUI.Muted, Center, 0f, 10f);
        nextNumber = ShopUI.Text(under, "", ShopUI.Print, 250, new Vector2(380, 270), new Vector2(0, 0), new Color(0.12f, 0.48f, 0.27f));
        nextNumber.enableAutoSizing = true; nextNumber.fontSizeMin = 90; nextNumber.fontSizeMax = 250;
        page = ShopUI.Sheet("Page", cal, new Vector2(400, 500), new Vector2(0, -10), ShopUI.Paper, -5f);
        page.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        pagePos = page.anchoredPosition; pageRot = page.localRotation;
        ShopUI.Box("Binding", page, new Vector2(400, 26), new Vector2(0, 263), new Color(0.5f, 0.52f, 0.56f));
        RectTransform head = ShopUI.Box("Head", page, new Vector2(400, 50), new Vector2(0, 225), ShopUI.SignRed);
        ShopUI.Text(head, "STORE CLOSED", ShopUI.Body, 16, new Vector2(390, 40), Vector2.zero, new Color(1f, 0.95f, 0.84f), Center, 0f, 10f).fontStyle = FontStyles.Bold;
        ShopUI.Text(page, "NGÀY · DAY", ShopUI.Body, 16, new Vector2(380, 26), new Vector2(0, 175), ShopUI.Muted, Center, 0f, 10f);
        dayNumber = ShopUI.Text(page, "", ShopUI.Print, 250, new Vector2(380, 270), new Vector2(0, 20), ShopUI.SignRed);
        dayNumber.enableAutoSizing = true; dayNumber.fontSizeMin = 90; dayNumber.fontSizeMax = 250;
        ShopUI.Text(page, "END OF DAY", ShopUI.Print, 30, new Vector2(380, 40), new Vector2(0, -150), ShopUI.Ink, Center, 0f, 10f);

        // Hoá đơn bán lẻ
        invoice = ShopUI.Sheet("Invoice", bg, new Vector2(900, 900), new Vector2(250, 30), InvoicePaper, 1f, true);
        invoicePos = invoice.anchoredPosition;
        ShopUI.Text(invoice, "HOÁ ĐƠN BÁN LẺ", ShopUI.Print, 58, new Vector2(600, 70), new Vector2(-120, 385), InvoiceRed, Left, 0f, 4f);
        ShopUI.Text(invoice, "DAILY TAKINGS · LIÊN 1: LƯU", ShopUI.Body, 15, new Vector2(600, 22), new Vector2(-120, 340), InvoiceRed, Left, 0f, 8f).fontStyle = FontStyles.Bold;
        ShopUI.Text(invoice, "No.", ShopUI.Body, 16, new Vector2(60, 30), new Vector2(230, 385), InvoiceRed, Right).fontStyle = FontStyles.Bold;
        invoiceNo = ShopUI.Text(invoice, "", ShopUI.Hand, 38, new Vector2(170, 44), new Vector2(350, 387), ShopUI.Pen, Left);
        RectTransform table = ShopUI.Group("Table", invoice, new Vector2(840, 620), new Vector2(0, -5));
        ShopUI.Frame(table, new Vector2(840, 620), InvoiceRed, 3f);
        ShopUI.Box("ColLine", table, new Vector2(2, 620), new Vector2(210, 0), ShopUI.A(InvoiceRed, 0.6f));
        ShopUI.Text(table, "NỘI DUNG · ITEM", ShopUI.Body, 13, new Vector2(600, 24), new Vector2(-100, 292), InvoiceRed, Left, 0f, 8f).fontStyle = FontStyles.Bold;
        ShopUI.Text(table, "THÀNH TIỀN", ShopUI.Body, 13, new Vector2(190, 24), new Vector2(315, 292), InvoiceRed, Right, 0f, 8f).fontStyle = FontStyles.Bold;
        ShopUI.Box("HeadLine", table, new Vector2(840, 2), new Vector2(0, 276), InvoiceRed);
        rowsRoot = ShopUI.Group("Rows", table, new Vector2(840, 620), Vector2.zero);
        ShopUI.Text(invoice, "CỘNG · PROFIT", ShopUI.Print, 34, new Vector2(320, 50), new Vector2(-250, -355), InvoiceRed, Left, 0f, 6f);
        profitText = ShopUI.Text(invoice, "", ShopUI.Hand, 78, new Vector2(420, 90), new Vector2(210, -352), ShopUI.Pen, Right);
        profitText.enableAutoSizing = true; profitText.fontSizeMin = 40; profitText.fontSizeMax = 78;
        balanceText = ShopUI.Text(invoice, "", ShopUI.Hand, 30, new Vector2(840, 40), new Vector2(0, -415), ShopUI.Ink, Left);
        stamp = ShopUI.Group("Stamp", invoice, new Vector2(330, 70), new Vector2(120, -260), -9f);
        Color sc = new Color(0.82f, 0.13f, 0.16f, 0.88f);
        ShopUI.Frame(stamp, new Vector2(330, 70), sc, 5f);
        stampText = ShopUI.Text(stamp, "", ShopUI.Print, 44, new Vector2(330, 70), Vector2.zero, sc, Center, 0f, 8f);

        // Nút sang ngày mới (dưới tờ lịch)
        Button nextBtn = ShopUI.PrintButton(bg, "TEAR THE PAGE", new Vector2(520, 78), new Vector2(-640, -330), Next);
        nextLabel = nextBtn.GetComponentInChildren<TMP_Text>();
        nextLabel.fontSize = 32;
        ShopUI.Text(bg, "Enter", ShopUI.Body, 18, new Vector2(520, 26), new Vector2(-640, -390), new Color(0.85f, 0.8f, 0.7f));

        root.SetActive(false);
    }

    /// <summary>Một dòng viết tay trên hoá đơn: nội dung bên trái, số tiền bên phải (để trống nếu là ghi chú).</summary>
    private GameObject Row(string label, string value, Color ink, float y, float size)
    {
        RectTransform r = ShopUI.Group("Row", rowsRoot, new Vector2(820, 44), new Vector2(0, y));
        ShopUI.Text(r, label, ShopUI.Hand, size, new Vector2(value.Length > 0 ? 590 : 800, 44), new Vector2(value.Length > 0 ? -105 : 0, 0), ink, TextAlignmentOptions.Left);
        if (value.Length > 0) ShopUI.Text(r, value, ShopUI.Hand, size, new Vector2(190, 44), new Vector2(305, 0), ink, TextAlignmentOptions.Right);
        ShopUI.Box("Dots", r, new Vector2(820, 1), new Vector2(0, -22), ShopUI.A(InvoiceRed, 0.3f));
        return r.gameObject;
    }
}
