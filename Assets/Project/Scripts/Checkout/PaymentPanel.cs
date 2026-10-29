using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Màn hình thanh toán ở quầy (giống Supermarket Simulator):
///  - Tiền mặt: khách đưa tiền, người chơi bấm từng tờ / đồng xu trong ngăn kéo để thối đúng tiền thừa.
///    Thối thiếu thì không xác nhận được; thối dư thì mất phần dư.
///  - Thẻ: người chơi gõ đúng số tiền trên máy POS, sai thì báo lỗi và gõ lại.
/// Tự dựng giao diện bằng code. Esc = tạm đóng (bấm lại vào tiền/thẻ trên quầy để mở).
/// </summary>
public class PaymentPanel : MonoBehaviour
{
    private static PaymentPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private static readonly int[] Denominations = { 5000, 2000, 1000, 500, 100, 50, 25, 10, 5, 1 }; // cent

    private CheckoutCounter counter;
    private NPCShopper forCustomer;
    private GameObject root, cashGroup, cardGroup;
    private TMP_Text titleText, totalText, receivedText, changeText, givingText, hintText, cardDisplay;
    private Button confirmButton;
    private readonly List<int> given = new List<int>();
    private string cardEntry = "";

    private static readonly Color WindowColor = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    private static readonly Color HeaderColor = new Color(0.0f, 0.6f, 0.95f, 1f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.3f, 0.38f, 1f);
    private static readonly Color BillColor = new Color(0.35f, 0.6f, 0.35f, 1f);
    private static readonly Color CoinColor = new Color(0.75f, 0.62f, 0.3f, 1f);
    private static readonly Color Green = new Color(0.35f, 0.85f, 0.4f);
    private static readonly Color Red = new Color(1f, 0.4f, 0.35f);
    private static readonly Color Yellow = new Color(1f, 0.8f, 0.3f);

    public static void Open(CheckoutCounter counter)
    {
        if (counter == null || !counter.AwaitingPayment) return;
        if (instance == null) instance = new GameObject("PaymentPanel").AddComponent<PaymentPanel>();
        instance.Show(counter);
    }

    /// <summary>Đóng nếu đang mở cho quầy này (khách bỏ đi...).</summary>
    public static void CloseFor(CheckoutCounter counter)
    {
        if (instance != null && instance.counter == counter && IsOpen) instance.Close();
    }

    void Awake() => Build();

    void OnDestroy()
    {
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (counter == null || !counter.AwaitingPayment) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

        if (counter.Method == PaymentMethod.Card)
        {
            for (int d = 0; d <= 9; d++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) CardKey(d.ToString());
            if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.KeypadPeriod) || Input.GetKeyDown(KeyCode.Comma)) CardKey(".");
            if (Input.GetKeyDown(KeyCode.Backspace)) CardKey("<");
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
    }

    // ================= Hành vi =================
    private void Show(CheckoutCounter c)
    {
        // Mở lại cho cùng khách thì giữ số tiền đang thối dở
        if (counter != c || forCustomer != c.Current) { given.Clear(); cardEntry = ""; }
        counter = c;
        forCustomer = c.Current;

        bool cash = c.Method == PaymentMethod.Cash;
        titleText.text = cash ? "CASH PAYMENT" : "CARD PAYMENT";
        cashGroup.SetActive(cash);
        cardGroup.SetActive(!cash);
        root.SetActive(true);
        UIState.Open(this);
        Refresh();
    }

    private void Refresh()
    {
        int due = counter.DueCents;
        totalText.text = $"Total:  {Money(due)}";
        if (counter.Method == PaymentMethod.Cash)
        {
            int change = counter.CashGivenCents - due;
            int giving = Giving;
            receivedText.text = $"Received:  {Money(counter.CashGivenCents)}";
            changeText.text = $"Change:  {Money(change)}";
            givingText.text = $"Giving:  {Money(giving)}";
            if (giving == change) { givingText.color = Green; hintText.text = "Correct change! Press Confirm."; hintText.color = Green; }
            else if (giving < change) { givingText.color = Yellow; hintText.text = $"Still need {Money(change - giving)}"; hintText.color = Yellow; }
            else { givingText.color = Red; hintText.text = $"Too much! You lose {Money(giving - change)}"; hintText.color = Red; }
            confirmButton.interactable = giving >= change;
        }
        else
        {
            cardDisplay.text = cardEntry.Length > 0 ? "$" + cardEntry : "$0.00";
            hintText.text = "Type the total on the POS, then Confirm";
            hintText.color = Color.white;
            confirmButton.interactable = cardEntry.Length > 0;
        }
    }

    private int Giving
    {
        get { int s = 0; foreach (int v in given) s += v; return s; }
    }

    private void Give(int cents) { given.Add(cents); Refresh(); }
    private void Undo() { if (given.Count > 0) given.RemoveAt(given.Count - 1); Refresh(); }
    private void ClearGiven() { given.Clear(); Refresh(); }

    private void CardKey(string key)
    {
        if (key == "<") { if (cardEntry.Length > 0) cardEntry = cardEntry.Substring(0, cardEntry.Length - 1); }
        else if (key == "C") cardEntry = "";
        else if (key == ".") { if (!cardEntry.Contains(".")) cardEntry += cardEntry.Length == 0 ? "0." : "."; }
        else
        {
            int dot = cardEntry.IndexOf('.');
            if (dot >= 0 && cardEntry.Length - dot > 2) return; // tối đa 2 số lẻ
            if (cardEntry.Length < 8) cardEntry += key;
        }
        Refresh();
    }

    private void Confirm()
    {
        if (counter == null || !counter.AwaitingPayment) return;

        if (counter.Method == PaymentMethod.Cash)
        {
            int change = counter.CashGivenCents - counter.DueCents;
            int giving = Giving;
            if (giving < change) return;
            counter.FinishPayment(giving - change);
        }
        else
        {
            if (!float.TryParse(cardEntry, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ||
                Mathf.RoundToInt(v * 100f) != counter.DueCents)
            {
                cardEntry = "";
                Refresh();
                hintText.text = "Wrong amount! Try again.";
                hintText.color = Red;
                return;
            }
            counter.FinishPayment(0);
        }
        given.Clear();
        cardEntry = "";
        Close();
    }

    private void Close()
    {
        root.SetActive(false);
        UIState.Close(this);
    }

    private static string Money(int cents) => EconomyManager.Format(cents / 100f);

    // ================= Dựng giao diện =================
    private void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = NewRect("Root", canvasGo.transform);
        Stretch(root);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        GameObject window = NewRect("Window", root.transform, new Vector2(760, 600));
        window.AddComponent<Image>().color = WindowColor;

        GameObject header = NewRect("Header", window.transform, new Vector2(760, 70), new Vector2(0, 265));
        header.AddComponent<Image>().color = HeaderColor;
        titleText = Text(header.transform, "PAYMENT", 34, new Vector2(700, 60), Vector2.zero, TextAlignmentOptions.Center, Color.white, true);
        Btn(header.transform, "X", new Vector2(50, 50), new Vector2(350, 0), Close, new Color(0.9f, 0.2f, 0.2f));

        totalText = Text(window.transform, "Total", 32, new Vector2(700, 44), new Vector2(0, 195), TextAlignmentOptions.Center, Color.white, true);

        // ---- Tiền mặt ----
        cashGroup = NewRect("Cash", window.transform, new Vector2(760, 440), new Vector2(0, -40));
        receivedText = Text(cashGroup.transform, "Received", 26, new Vector2(340, 40), new Vector2(-180, 190), TextAlignmentOptions.Center, Color.white);
        changeText = Text(cashGroup.transform, "Change", 26, new Vector2(340, 40), new Vector2(180, 190), TextAlignmentOptions.Center, Yellow, true);
        givingText = Text(cashGroup.transform, "Giving", 30, new Vector2(700, 44), new Vector2(0, 145), TextAlignmentOptions.Center, Yellow, true);
        for (int i = 0; i < Denominations.Length; i++)
        {
            int cents = Denominations[i];
            int row = i / 5, col = i % 5;
            string label = cents >= 100 ? $"${cents / 100}" : $"{cents / 100f:0.00}";
            Btn(cashGroup.transform, label, new Vector2(130, 80), new Vector2(-280 + col * 140, 70 - row * 95),
                () => Give(cents), cents >= 100 ? BillColor : CoinColor);
        }
        Btn(cashGroup.transform, "Undo", new Vector2(160, 54), new Vector2(-90, -115), Undo);
        Btn(cashGroup.transform, "Clear", new Vector2(160, 54), new Vector2(90, -115), ClearGiven);

        // ---- Thẻ ----
        cardGroup = NewRect("Card", window.transform, new Vector2(760, 440), new Vector2(0, -40));
        GameObject screen = NewRect("PosScreen", cardGroup.transform, new Vector2(360, 70), new Vector2(0, 175));
        screen.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.05f);
        cardDisplay = Text(screen.transform, "$0.00", 40, new Vector2(340, 64), Vector2.zero, TextAlignmentOptions.Right, Green, true);
        string[] keys = { "7", "8", "9", "4", "5", "6", "1", "2", "3", ".", "0", "<" };
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i];
            int row = i / 3, col = i % 3;
            Btn(cardGroup.transform, k == "<" ? "Del" : k, new Vector2(110, 62), new Vector2(-120 + col * 120, 100 - row * 70), () => CardKey(k));
        }
        Btn(cardGroup.transform, "C", new Vector2(110, 62), new Vector2(250, 100), () => CardKey("C"), new Color(0.7f, 0.35f, 0.2f));

        hintText = Text(window.transform, "", 24, new Vector2(700, 40), new Vector2(0, -205), TextAlignmentOptions.Center, Color.white);
        confirmButton = Btn(window.transform, "Confirm", new Vector2(240, 60), new Vector2(0, -260), Confirm, new Color(0.2f, 0.7f, 0.3f));

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

    private static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(Transform parent, string text, float size, Vector2 box, Vector2 pos,
                                 TextAlignmentOptions align, Color color, bool bold = false)
    {
        GameObject go = NewRect("Text", parent, box, pos);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    private static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color? color = null)
    {
        GameObject go = NewRect("Button_" + label, parent, size, pos);
        go.AddComponent<Image>().color = color ?? ButtonColor;
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        Text(go.transform, label, 28, size, Vector2.zero, TextAlignmentOptions.Center, Color.white, true);
        return b;
    }
}
