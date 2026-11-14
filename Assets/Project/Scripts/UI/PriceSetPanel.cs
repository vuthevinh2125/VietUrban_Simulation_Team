using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bảng đặt giá bán (giống Supermarket Simulator): nhìn vào kệ có hàng, bấm chuột trái để mở.
/// Hiện giá nhập, giá thị trường, ô nhập giá của bạn, lãi mỗi món và phản ứng dự kiến của khách.
/// Tự dựng giao diện bằng code, không cần prefab. Enter = xác nhận, Esc = huỷ.
/// </summary>
public class PriceSetPanel : MonoBehaviour
{
    private static PriceSetPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private ItemData item;
    private float price;
    private GameObject root;
    private TMP_Text titleText, costText, marketText, profitText, reactionText, bigPrice, oldPrice, brandText, trendText, stockText;
    private RectTransform meterMark;
    private GameObject trendChip;
    private Image iconImage;
    private TMP_InputField input;
    private bool ignoreInput;

    private const float MeterW = 750f;

    public static void Open(ItemData item)
    {
        if (item == null) return;
        if (instance == null) instance = new GameObject("PriceSetPanel").AddComponent<PriceSetPanel>();
        instance.Show(item);
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
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
        else if (Input.GetKeyDown(KeyCode.A)) Step(Input.GetKey(KeyCode.LeftShift) ? -1f : -0.1f);
        else if (Input.GetKeyDown(KeyCode.D)) Step(Input.GetKey(KeyCode.LeftShift) ? 1f : 0.1f);
        else if (Input.GetKeyDown(KeyCode.M)) SetPrice(MarketPrices.Get(item), true);
    }

    // ================= Hành vi =================
    private void Show(ItemData data)
    {
        item = data;
        titleText.text = item.itemName;
        iconImage.sprite = item.icon;
        iconImage.enabled = item.icon != null;
        brandText.text = string.IsNullOrEmpty(item.brand) ? "per item" : $"{item.brand} · per item";
        oldPrice.text = $"<s>{EconomyManager.Format(PriceManager.GetSellPrice(item))}</s>";
        costText.text = EconomyManager.Format(item.unitPrice);
        int trend = MarketPrices.Trend(item);
        trendChip.SetActive(trend != 0);
        trendText.text = trend > 0 ? "MARKET UP TODAY" : "MARKET DOWN TODAY";
        marketText.text = EconomyManager.Format(MarketPrices.Get(item));
        stockText.text = item.itemsPerBox > 0 ? $"{item.itemsPerBox} per box · box costs {EconomyManager.Format(item.BoxPrice)}" : "";
        SetPrice(PriceManager.GetSellPrice(item), true);

        root.SetActive(true);
        UIState.Open(this);
        input.Select();
        input.ActivateInputField();
    }

    private void SetPrice(float value, bool updateInput)
    {
        price = Mathf.Max(0.01f, Mathf.Round(value * 100f) / 100f);
        if (updateInput)
        {
            ignoreInput = true;
            input.text = price.ToString("0.00", CultureInfo.InvariantCulture);
            ignoreInput = false;
        }

        float profit = price - item.unitPrice;
        float margin = item.unitPrice > 0f ? profit / item.unitPrice * 100f : 0f;
        bigPrice.text = EconomyManager.Format(price);
        profitText.text = $"{EconomyManager.Format(profit)} · {margin:0}%";
        profitText.color = profit >= 0f ? ShopPC.GreenInk : ShopPC.Red;

        float ratio = PriceManager.PriceRatio(item, price);
        float chance = PriceManager.BuyChance(item, price);
        if (ratio <= PriceManager.CheapRatio) reactionText.text = "A bargain. People will fill their baskets and come back tomorrow.";
        else if (ratio <= 1.0001f) reactionText.text = "A fair price. Nobody will complain.";
        else if (chance > 0.5f) reactionText.text = "A little high. Most people still buy, a few put it back.";
        else if (chance > 0f) reactionText.text = "Expensive. Only a few will buy it at this price.";
        else reactionText.text = "Too expensive! Nobody will buy it at this price.";
        float k = Mathf.InverseLerp(0.6f, PriceManager.MaxRatio, ratio);
        meterMark.anchoredPosition = new Vector2(-MeterW * 0.5f + MeterW * k, meterMark.anchoredPosition.y);
    }

    private void OnInputChanged(string s)
    {
        if (ignoreInput || item == null) return;
        if (float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            SetPrice(v, false);
    }

    private void Step(float delta) => SetPrice(price + delta, true);

    private void Confirm()
    {
        if (item == null) return;
        PriceManager.SetSellPrice(item, price);
        Debug.Log($"[Price] {item.itemName}: {EconomyManager.Format(price)} (market {EconomyManager.Format(MarketPrices.Get(item))})");
        Close();
    }

    private void Close()
    {
        root.SetActive(false);
        UIState.Close(this);
    }

    // ================= Dựng giao diện (bản thiết kế 11: biển giá bìa các-tông + bảng giá) =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 500).transform;
        // Nền: cảnh kệ hàng phía sau tối đi (giống bản thiết kế)
        root = ShopUI.Full("Root", canvas, ShopPC.A(ShopPC.Hex(0x08090C), 0.72f), true).gameObject;
        RectTransform c = ShopUI.Group("Stage", root.transform, new Vector2(1920f, 1080f), Vector2.zero);

        // Biển giá các-tông treo bằng dây
        RectTransform hang = ShopPC.Group("SignHang", c, 165f, 165f, 645f, 520f, -3f);
        ShopPC.Box("String", hang, 321f, -105f, 3f, 120f, ShopPC.Hex(0xD8D0BC));
        RectTransform sign = ShopPC.Sheet("Cardboard", hang, 0f, 0f, 645f, 520f, ShopUI.Cardboard);
        sign.GetComponent<Shadow>().effectDistance = new Vector2(0f, -27f);
        titleText = ShopPC.Text(sign, "Item", ShopUI.Hand, 60, 30f, 36f, 585f, 66f, ShopPC.Ink, TextAlignmentOptions.Center);
        brandText = ShopPC.Text(sign, "", ShopUI.Hand, 33, 30f, 102f, 585f, 40f, ShopPC.Hex(0x4A3520), TextAlignmentOptions.Center);
        bigPrice = ShopPC.Text(sign, "", ShopUI.Hand, 225, 0f, 140f, 645f, 240f, ShopPC.Red, TextAlignmentOptions.Center);
        bigPrice.enableAutoSizing = true; bigPrice.fontSizeMin = 110; bigPrice.fontSizeMax = 225;
        bigPrice.textWrappingMode = TextWrappingModes.NoWrap;
        UIMotion pop = bigPrice.gameObject.AddComponent<UIMotion>();
        pop.pulse = 0.02f; pop.pulseSpeed = 2.1f;
        oldPrice = ShopPC.Text(sign, "", ShopUI.Hand, 36, 0f, 390f, 645f, 46f, ShopPC.Hex(0x4A3520), TextAlignmentOptions.Center);
        RectTransform iconRt = ShopPC.Box("Icon", sign, 30f, 400f, 96f, 96f, Color.white);
        iconImage = iconRt.GetComponent<Image>();
        iconImage.preserveAspect = true;

        // Bảng giá giấy
        RectTransform p = ShopPC.Sheet("PricePaper", c, 960f, 105f, 810f, 790f, ShopPC.Paper);
        p.GetComponent<Shadow>().effectDistance = new Vector2(0f, -27f);
        ShopPC.Text(p, "BẢNG GIÁ · SET PRICE", ShopUI.Print, 45, 42f, 36f, 560f, 60f, ShopPC.Red, TextAlignmentOptions.Left, 4.5f);
        RectTransform chip = ShopPC.Box("Trend", p, 810f - 42f - 230f, 48f, 230f, 36f, ShopPC.Red);
        trendText = ShopUI.Text(chip, "", ShopUI.Body, 16, chip.sizeDelta, Vector2.zero, ShopPC.Hex(0xFFE08A), TextAlignmentOptions.Center, 0f, 1.5f);
        trendText.fontStyle = FontStyles.Bold;
        trendChip = chip.gameObject;

        // − [ô giá + thước] +
        ShopPC.Btn(p, "-", 42f, 117f, 78f, 78f, () => Step(-0.1f), ShopPC.Kind.Dark, 45f);
        ShopPC.Btn(p, "+", 810f - 42f - 78f, 117f, 78f, 78f, () => Step(0.1f), ShopPC.Kind.Dark, 45f);
        RectTransform ruler = ShopPC.Group("Ruler", p, 141f, 117f, 528f, 78f);
        for (float rx = 0f; rx <= 528f; rx += 30f) ShopPC.Box("Tick", ruler, rx, 48f, 3f, 27f, ShopPC.A(ShopPC.Ink, 0.5f));
        ShopPC.Box("RulerLine", ruler, 0f, 75f, 528f, 3f, ShopPC.Ink);

        // Ô nhập giá (gõ số trực tiếp)
        GameObject inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        inputGo.name = "PriceInput";
        inputGo.transform.SetParent(ruler, false);
        RectTransform irt = inputGo.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(220f, 54f);
        irt.anchoredPosition = new Vector2(0f, 12f);
        inputGo.GetComponent<Image>().color = ShopPC.Paper;
        ShopUI.Frame(irt, irt.sizeDelta, ShopPC.Pen, 2f);
        input = inputGo.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.pointSize = 40;
        input.caretColor = ShopPC.Pen;
        input.onValueChanged.AddListener(OnInputChanged);
        foreach (TMP_Text t in inputGo.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font = ShopUI.Print; t.fontSize = 40; t.color = ShopPC.Pen;
            t.alignment = TextAlignmentOptions.Center;
        }

        // 3 cột số
        string[] heads = { "YOU PAY", "MARKET PRICE", "YOU KEEP" };
        TMP_Text[] vals = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float x = 42f + i * 250f;
            ShopPC.Text(p, heads[i], ShopUI.Body, 15, x, 228f, 240f, 22f, ShopPC.Muted, TextAlignmentOptions.Left, 3f, true);
            vals[i] = ShopPC.Text(p, "", ShopUI.Hand, 51, x, 250f, 245f, 60f, ShopPC.Pen, TextAlignmentOptions.Left);
            vals[i].textWrappingMode = TextWrappingModes.NoWrap;
            vals[i].enableAutoSizing = true; vals[i].fontSizeMin = 28; vals[i].fontSizeMax = 51;
        }
        costText = vals[0]; marketText = vals[1]; profitText = vals[2];
        stockText = ShopPC.Text(p, "", ShopUI.Body, 15, 42f, 312f, 726f, 22f, ShopPC.Muted, TextAlignmentOptions.Left);

        // Thước "khách sẽ nghĩ gì"
        ShopPC.Text(p, "WHAT CUSTOMERS WILL THINK", ShopUI.Body, 15, 42f, 354f, 726f, 22f, ShopPC.Muted, TextAlignmentOptions.Left, 3f, true);
        RectTransform meter = ShopPC.Group("Meter", p, 30f, 388f, MeterW, 24f);
        ShopPC.Box("Bargain", meter, 0f, 0f, MeterW * 0.28f, 24f, ShopPC.GreenInk);
        ShopPC.Box("Fair", meter, MeterW * 0.28f, 0f, MeterW * 0.33f, 24f, ShopPC.Hex(0xE8B13A));
        ShopPC.Box("High", meter, MeterW * 0.61f, 0f, MeterW * 0.39f, 24f, ShopPC.Red);
        meterMark = ShopUI.Box("Mark", meter, new Vector2(6f, 39f), Vector2.zero, ShopPC.Ink);
        ShopPC.Text(p, "bargain", ShopUI.Body, 16, 42f, 420f, 200f, 24f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
        ShopPC.Text(p, "fair", ShopUI.Body, 16, 255f, 420f, 200f, 24f, ShopPC.Muted, TextAlignmentOptions.Center, 0f, true);
        ShopPC.Text(p, "too expensive: some walk out", ShopUI.Body, 16, 450f, 420f, 318f, 24f, ShopPC.Muted, TextAlignmentOptions.Right, 0f, true);
        reactionText = ShopPC.Text(p, "", ShopUI.Hand, 33, 42f, 470f, 726f, 120f, ShopPC.Pen, TextAlignmentOptions.TopLeft);
        reactionText.lineSpacing = -10f;

        ShopPC.Btn(p, "MARKET PRICE", 810f - 42f - 330f - 15f - 240f, 790f - 42f - 69f, 240f, 69f, () => SetPrice(MarketPrices.Get(item), true), ShopPC.Kind.Dark, 26f);
        ShopPC.Btn(p, "PUT UP THE SIGN", 810f - 42f - 330f, 790f - 42f - 69f, 330f, 69f, Confirm, ShopPC.Kind.Red, 26f);

        // Thanh phím tắt
        RectTransform foot = ShopUI.Box("Footer", root.transform, Vector2.zero, Vector2.zero, ShopPC.Hex(0x14100A));
        foot.anchorMin = Vector2.zero; foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(0.5f, 0f);
        foot.sizeDelta = new Vector2(0f, 78f); foot.anchoredPosition = Vector2.zero;
        ShopPC.KeyHints(foot, new[] { ("A D", "Change 10¢"), ("Shift", "Change $1"), ("M", "Market price"), ("Enter", "Confirm"), ("Esc", "Cancel") },
            "Type a price, or use − and +");
        root.SetActive(false);
    }
}
