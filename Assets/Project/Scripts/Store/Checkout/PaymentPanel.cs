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
    private TMP_Text itemNames, itemPrices, dateText, quoteText, queueText, dueBig, handSub, cashLabel, changeLabel;
    private GameObject cashFooter, cardFooter;
    private Button confirmButton;
    private readonly List<int> given = new List<int>();
    private string cardEntry = "";

    private static readonly Color Green = ShopPC.Mint;
    private static readonly Color Red = ShopPC.Hex(0xFF8A7A);
    private static readonly Color Yellow = ShopPC.Gold;
    private static readonly Color Cream = ShopPC.Hex(0xD9CDB4);

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
        else
        {
            int[] notes = { 100, 500, 1000, 2000, 5000 };
            KeyCode[] coinKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T };
            int[] coins = { 1, 5, 10, 25, 50 };
            for (int i = 0; i < 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Give(notes[i]);
                if (Input.GetKeyDown(coinKeys[i])) Give(coins[i]);
            }
            if (Input.GetKeyDown(KeyCode.Backspace)) Undo();
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
        titleText.text = cash ? "GIVE CHANGE" : "CARD PAYMENT";
        cashGroup.SetActive(cash);
        cardGroup.SetActive(!cash);
        cashFooter.SetActive(cash);
        cardFooter.SetActive(!cash);
        root.SetActive(true);
        UIState.Open(this);
        Refresh();
    }

    private void Refresh()
    {
        int due = counter.DueCents;
        totalText.text = Money(due);
        FillReceipt();
        int waiting = Mathf.Max(0, counter.QueueLength - 1);
        queueText.text = waiting > 0 ? $"{waiting} WAITING IN LINE" : "NOBODY ELSE IN LINE";
        GameManager gm = GameManager.Instance;
        dateText.text = gm != null ? $"24 Hai Bà Trưng · Day {gm.currentDay} · {gm.ClockString}" : "24 Hai Bà Trưng";
        if (counter.Method == PaymentMethod.Cash)
        {
            int change = counter.CashGivenCents - due;
            int giving = Giving;
            quoteText.text = $"\"Here is {Money(counter.CashGivenCents)}, dear.\"";
            cashLabel.text = "CASH";
            receivedText.text = Money(counter.CashGivenCents);
            changeLabel.gameObject.SetActive(true);
            changeText.gameObject.SetActive(true);
            changeText.text = Money(change);
            dueBig.text = Money(change);
            givingText.text = Money(giving);
            if (giving == change) { givingText.color = Green; handSub.text = "exactly right"; hintText.text = "Correct change! Press Enter or HAND OVER CHANGE."; hintText.color = Green; }
            else if (giving < change) { givingText.color = Yellow; handSub.text = $"{Money(change - giving)} more"; hintText.text = ""; }
            else { givingText.color = Red; handSub.text = $"too much: you lose {Money(giving - change)}"; hintText.text = "Too much change is your loss."; hintText.color = Red; }
            confirmButton.interactable = giving >= change;
        }
        else
        {
            quoteText.text = "\"Card, please.\"";
            cashLabel.text = "CARD";
            receivedText.text = Money(due);
            changeLabel.gameObject.SetActive(false);
            changeText.gameObject.SetActive(false);
            cardDisplay.text = cardEntry.Length > 0 ? "$" + cardEntry : "$0.00";
            hintText.text = "Type the total from the receipt on the card machine, then press Enter.";
            hintText.color = Cream;
            confirmButton.interactable = cardEntry.Length > 0;
        }
        ShopPC.SetLabel(confirmButton, counter.Method == PaymentMethod.Cash ? "HAND OVER CHANGE" : "CHARGE THE CARD");
    }

    /// <summary>Các món đã quét, gộp theo tên: "Fresh Milk ×2 ..... $3.80".</summary>
    private void FillReceipt()
    {
        var order = new List<string>();
        var count = new Dictionary<string, int>();
        var sum = new Dictionary<string, float>();
        foreach (var (name, price) in counter.Scanned)
        {
            if (!count.ContainsKey(name)) { order.Add(name); count[name] = 0; sum[name] = 0f; }
            count[name]++; sum[name] += price;
        }
        var left = new System.Text.StringBuilder();
        var right = new System.Text.StringBuilder();
        int shown = 0;
        foreach (string n in order)
        {
            if (shown == 9 && order.Count > 10) { left.AppendLine($"... and {order.Count - 9} more"); right.AppendLine(); break; }
            left.AppendLine($"{n} ×{count[n]}");
            right.AppendLine(EconomyManager.Format(sum[n]));
            shown++;
        }
        if (order.Count == 0) left.AppendLine("(items on the counter)");
        itemNames.text = left.ToString();
        itemPrices.text = right.ToString();
    }

    private int Giving
    {
        get { int s = 0; foreach (int v in given) s += v; return s; }
    }

    private void Give(int cents) { given.Add(cents); AudioManager.Play("coin", 0.6f); Refresh(); }
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
            int giving = Giving;
            if (!CashMath.CanConfirm(counter.CashGivenCents, counter.DueCents, giving)) return;
            counter.FinishPayment(CashMath.LossFromChange(counter.CashGivenCents, counter.DueCents, giving));
        }
        else
        {
            if (!CashMath.CardEntryMatches(cardEntry, counter.DueCents))
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

    // ================= Dựng giao diện (bản thiết kế 12: mặt quầy, hoá đơn, ngăn kéo tiền) =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 500).transform;
        RectTransform bg = ShopUI.Full("Root", canvas, ShopPC.A(ShopPC.Hex(0x3A2515), 0.97f), true);
        root = bg.gameObject;
        for (int i = 0; i < 44; i++) ShopUI.Box("Grain", bg, new Vector2(3f, 3000f), new Vector2(-2100f + i * 96f + (i % 3) * 13f, 0f), new Color(0f, 0f, 0f, 0.12f), 2f);
        ShopUI.Full("Vignette", bg, new Color(0f, 0f, 0f, 0.8f), false).GetComponent<Image>().sprite = ShopUI.Vignette;
        RectTransform c = ShopUI.Group("Stage", bg, new Vector2(1920f, 1080f), Vector2.zero);

        // ---- Hoá đơn ----
        RectTransform rc = ShopPC.Sheet("Receipt", c, 96f, 60f, 480f, 760f, ShopPC.Hex(0xF4F1E8), -1.5f, 30f);
        rc.GetComponent<Shadow>().effectDistance = new Vector2(0f, -24f);
        Color ink = ShopPC.Hex(0x1B1A17), dash = ShopPC.Hex(0x8C877C);
        ShopPC.Text(rc, "TẠP HOÁ BÀ LAN", ShopUI.Print, 36, 0f, 27f, 480f, 46f, ShopPC.Red, TextAlignmentOptions.Center, 4.5f);
        dateText = ShopPC.Text(rc, "", ShopUI.Body, 16, 0f, 74f, 480f, 24f, ink, TextAlignmentOptions.Center);
        DashLine(rc, 108f, dash);
        itemNames = ShopPC.Text(rc, "", ShopUI.Body, 19, 30f, 120f, 300f, 400f, ink, TextAlignmentOptions.TopLeft);
        itemPrices = ShopPC.Text(rc, "", ShopUI.Body, 19, 330f, 120f, 120f, 400f, ink, TextAlignmentOptions.TopRight);
        itemNames.lineSpacing = itemPrices.lineSpacing = 12f;
        itemNames.overflowMode = itemPrices.overflowMode = TextOverflowModes.Truncate;
        DashLine(rc, 536f, dash);
        ShopPC.Text(rc, "TOTAL", ShopUI.Body, 20, 30f, 556f, 200f, 60f, ink, TextAlignmentOptions.Left, 0f, true);
        totalText = ShopPC.Text(rc, "", ShopUI.Print, 51, 200f, 548f, 250f, 66f, ink, TextAlignmentOptions.Right);
        cashLabel = ShopPC.Text(rc, "CASH", ShopUI.Body, 19, 30f, 628f, 200f, 30f, ink, TextAlignmentOptions.Left);
        receivedText = ShopPC.Text(rc, "", ShopUI.Body, 19, 200f, 628f, 250f, 30f, ink, TextAlignmentOptions.Right);
        changeLabel = ShopPC.Text(rc, "CHANGE", ShopUI.Body, 19, 30f, 664f, 200f, 30f, ShopPC.Red, TextAlignmentOptions.Left, 0f, true);
        changeText = ShopPC.Text(rc, "", ShopUI.Body, 19, 200f, 664f, 250f, 30f, ShopPC.Red, TextAlignmentOptions.Right, 0f, true);
        for (float zx = 0f; zx < 480f; zx += 24f) ShopPC.Dot(rc, new Vector2(18f, 18f), ShopPC.At(rc.sizeDelta, zx + 3f, 751f, 18f, 18f), ShopPC.Hex(0x3A2515));

        // ---- Bên phải ----
        const float rx = 660f, rw = 1164f;
        titleText = ShopPC.Text(c, "GIVE CHANGE", ShopUI.Print, 45, rx, 54f, 420f, 60f, ShopPC.Light, TextAlignmentOptions.Left, 4.5f);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        quoteText = ShopPC.Text(c, "", ShopUI.Hand, 39, rx + 400f, 56f, 520f, 56f, ShopPC.Hex(0xFFE08A), TextAlignmentOptions.Left);
        queueText = ShopPC.Text(c, "", ShopUI.Body, 18, rx + rw - 340f, 66f, 340f, 30f, Cream, TextAlignmentOptions.Right, 3f, true);

        cashGroup = ShopPC.Group("Cash", c, rx, 138f, rw, 560f).gameObject;
        Transform cg = cashGroup.transform;
        float bw = (rw - 21f) / 2f;
        RectTransform dueBox = ShopPC.Box("Due", cg, 0f, 0f, bw, 162f, ShopPC.A(ShopPC.Hex(0x08090C), 0.6f));
        ShopPC.Box("Strip", dueBox, 0f, 0f, 6f, 162f, ShopPC.Gold);
        ShopPC.Text(dueBox, "CHANGE DUE", ShopUI.Body, 16, 27f, 21f, bw - 40f, 24f, Cream, TextAlignmentOptions.Left, 3f, true);
        dueBig = ShopPC.Text(dueBox, "", ShopUI.Print, 90, 27f, 45f, bw - 40f, 100f, ShopPC.Gold, TextAlignmentOptions.Left);
        RectTransform handBox = ShopPC.Box("Hand", cg, bw + 21f, 0f, bw, 162f, ShopPC.A(ShopPC.Hex(0x08090C), 0.6f));
        ShopPC.Box("Strip", handBox, 0f, 0f, 6f, 162f, ShopPC.Mint);
        ShopPC.Text(handBox, "IN YOUR HAND", ShopUI.Body, 16, 27f, 21f, bw - 40f, 24f, Cream, TextAlignmentOptions.Left, 3f, true);
        givingText = ShopPC.Text(handBox, "", ShopUI.Print, 90, 27f, 40f, bw - 40f, 96f, ShopPC.Mint, TextAlignmentOptions.Left);
        givingText.gameObject.AddComponent<AlphaPulse>().min = 0.7f;
        handSub = ShopPC.Text(handBox, "", ShopUI.Body, 18, 27f, 128f, bw - 40f, 28f, Cream, TextAlignmentOptions.Left);

        RectTransform drawer = ShopPC.Box("Drawer", cg, 0f, 186f, rw, 282f, ShopPC.Ink);
        ShopUI.Frame(drawer, drawer.sizeDelta, ShopPC.Hex(0x5A3E22), 4.5f);
        ShopPC.Text(drawer, "NGĂN KÉO TIỀN · CASH DRAWER", ShopUI.Body, 16, 27f, 24f, 700f, 24f, Cream, TextAlignmentOptions.Left, 3f, true);
        (int cents, string label, int color)[] notes = { (100, "$1", 0xBFD8B0), (500, "$5", 0xB9C9E6), (1000, "$10", 0xE6CFA8), (2000, "$20", 0xD9B8D6), (5000, "$50", 0xE6B0A8) };
        for (int i = 0; i < notes.Length; i++)
        {
            int cents = notes[i].cents;
            Button b = ShopPC.Btn(drawer, notes[i].label, 27f + i * 198f, 66f, 180f, 84f, () => Give(cents), ShopPC.Kind.Red, 39f);
            b.GetComponent<Image>().color = ShopPC.Hex(notes[i].color);
            foreach (Image img in b.GetComponentsInChildren<Image>()) if (img.gameObject != b.gameObject) img.color = ShopPC.A(Color.white, 0.55f);
            b.GetComponentInChildren<TMP_Text>().color = ShopPC.Hex(0x14100A);
            b.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -7f);
        }
        (int cents, string label)[] coins = { (1, "1¢"), (5, "5¢"), (10, "10¢"), (25, "25¢"), (50, "50¢") };
        for (int i = 0; i < coins.Length; i++)
        {
            int cents = coins[i].cents;
            RectTransform cn = ShopPC.Box("Coin_" + coins[i].label, drawer, 27f + i * 111f, 174f, 87f, 87f, new Color(0f, 0f, 0f, 0.001f), 0f, true);
            ShopPC.Dot(cn, new Vector2(87f, 87f), Vector2.zero, ShopPC.Hex(0xB08A34));
            ShopPC.Dot(cn, new Vector2(78f, 78f), Vector2.zero, ShopPC.Hex(0xD9B45A));
            ShopUI.Text(cn, coins[i].label, ShopUI.Print, 30, cn.sizeDelta, Vector2.zero, ShopPC.Hex(0x14100A)).textWrappingMode = TextWrappingModes.NoWrap;
            Button b = cn.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Give(cents));
            cn.gameObject.AddComponent<UIMotion>().hoverScale = 1.08f;
        }
        ShopPC.Btn(cg, "TAKE IT BACK", rw - 360f - 15f - 210f - 15f - 270f, 492f, 270f, 69f, Undo, ShopPC.Kind.Ghost, 26f);
        ShopPC.Btn(cg, "CLEAR", rw - 360f - 15f - 210f, 492f, 210f, 69f, ClearGiven, ShopPC.Kind.Ghost, 26f);

        // ---- Máy quẹt thẻ ----
        cardGroup = ShopPC.Group("Card", c, rx, 138f, rw, 560f).gameObject;
        Transform kg = cardGroup.transform;
        RectTransform pos = ShopPC.Box("Terminal", kg, 0f, 0f, 600f, 468f, ShopPC.Hex(0x1E2228));
        ShopUI.Frame(pos, pos.sizeDelta, ShopPC.Hex(0x3A4048), 4.5f);
        RectTransform screen = ShopPC.Box("PosScreen", pos, 30f, 27f, 540f, 96f, ShopPC.Hex(0x0C140C));
        cardDisplay = ShopPC.Text(screen, "$0.00", ShopUI.Print, 60, 18f, 0f, 504f, 96f, Green, TextAlignmentOptions.Right);
        string[] keys = { "7", "8", "9", "4", "5", "6", "1", "2", "3", ".", "0", "<" };
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i];
            int row = i / 3, col = i % 3;
            Button kb = ShopPC.Btn(pos, k == "<" ? "DEL" : k, 30f + col * 138f, 147f + row * 78f, 126f, 66f, () => CardKey(k), ShopPC.Kind.Ghost, 33f);
            kb.GetComponent<Image>().color = ShopPC.Hex(0x2C3138);
        }
        Button ck = ShopPC.Btn(pos, "C", 30f + 3f * 138f, 147f, 126f, 300f, () => CardKey("C"), ShopPC.Kind.Red, 39f);
        ck.GetComponent<Image>().color = ShopPC.Hex(0xB8462A);
        ShopPC.StickyNote(kg, "Check the total on the receipt twice. A wrong number makes the customer wait. - bà Lan", 660f, 30f, 450f, 170f, 2f, 31f);

        hintText = ShopPC.Text(c, "", ShopUI.Hand, 30, rx, 712f, rw - 400f, 69f, Cream, TextAlignmentOptions.Left);
        confirmButton = ShopPC.Btn(c, "HAND OVER CHANGE", rx + rw - 360f, 630f, 360f, 69f, Confirm, ShopPC.Kind.Red, 26f);

        // ---- Phím tắt ----
        cashFooter = Footer(bg, new[] { ("1-5", "Notes"), ("Q-T", "Coins"), ("Backspace", "Take back"), ("Enter", "Hand over"), ("Esc", "Later") },
            "Too much change is your loss · too little hurts your rating");
        cardFooter = Footer(bg, new[] { ("0-9", "Type amount"), ("Backspace", "Delete"), ("Enter", "Charge"), ("Esc", "Later") }, "Type exactly the total on the receipt");
        root.SetActive(false);
    }

    private static GameObject Footer(Transform parent, (string, string)[] pairs, string right)
    {
        RectTransform foot = ShopUI.Box("Footer", parent, Vector2.zero, Vector2.zero, ShopPC.Hex(0x14100A));
        foot.anchorMin = Vector2.zero; foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(0.5f, 0f);
        foot.sizeDelta = new Vector2(0f, 78f); foot.anchoredPosition = Vector2.zero;
        ShopPC.KeyHints(foot, pairs, right);
        return foot.gameObject;
    }

    private static void DashLine(RectTransform parent, float y, Color c)
    {
        for (float x = 30f; x < parent.sizeDelta.x - 30f; x += 12f) ShopPC.Box("Dash", parent, x, y, 6f, 1.5f, c);
    }
}
