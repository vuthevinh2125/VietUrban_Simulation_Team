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
    private TMP_Text titleText, costText, marketText, profitText, reactionText;
    private Image iconImage;
    private TMP_InputField input;
    private bool ignoreInput;

    private static readonly Color WindowColor = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    private static readonly Color HeaderColor = new Color(0.0f, 0.6f, 0.95f, 1f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.3f, 0.38f, 1f);
    private static readonly Color Green = new Color(0.35f, 0.85f, 0.4f);
    private static readonly Color Red = new Color(1f, 0.4f, 0.35f);
    private static readonly Color Yellow = new Color(1f, 0.8f, 0.3f);

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
    }

    // ================= Hành vi =================
    private void Show(ItemData data)
    {
        item = data;
        titleText.text = item.itemName;
        iconImage.sprite = item.icon;
        iconImage.enabled = item.icon != null;
        costText.text = $"Cost:  {EconomyManager.Format(item.unitPrice)}";
        int trend = MarketPrices.Trend(item);
        string arrow = trend > 0 ? "  <color=#6BE37B>(up today)</color>" : trend < 0 ? "  <color=#FF8A7A>(down today)</color>" : "";
        marketText.text = $"Market price:  {EconomyManager.Format(MarketPrices.Get(item))}{arrow}";
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
        profitText.text = $"Profit per item:  {EconomyManager.Format(profit)}  ({margin:+0;-0}%)";
        profitText.color = profit >= 0f ? Green : Red;

        float ratio = PriceManager.PriceRatio(item, price);
        float chance = PriceManager.BuyChance(item, price);
        if (ratio <= PriceManager.CheapRatio) { reactionText.text = "Customers: Great deal!"; reactionText.color = Green; }
        else if (ratio <= 1.0001f) { reactionText.text = "Customers: Fair price"; reactionText.color = Green; }
        else if (chance > 0.5f) { reactionText.text = "Customers: A bit expensive"; reactionText.color = Yellow; }
        else if (chance > 0f) { reactionText.text = "Customers: Expensive, few will buy"; reactionText.color = Red; }
        else { reactionText.text = "Customers: Too expensive! Nobody buys"; reactionText.color = Red; }
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

        // Nền mờ toàn màn hình (bấm ra ngoài không làm gì)
        root = NewRect("Root", canvasGo.transform);
        Stretch(root);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        GameObject window = NewRect("Window", root.transform, new Vector2(620, 470));
        window.AddComponent<Image>().color = WindowColor;

        GameObject header = NewRect("Header", window.transform, new Vector2(620, 70), new Vector2(0, 200));
        header.AddComponent<Image>().color = HeaderColor;
        titleText = Text(header.transform, "Item", 34, new Vector2(560, 60), Vector2.zero, TextAlignmentOptions.Center, Color.white, true);
        Button close = Btn(header.transform, "X", new Vector2(50, 50), new Vector2(275, 0), Close);
        close.GetComponent<Image>().color = new Color(0.9f, 0.2f, 0.2f);

        GameObject iconGo = NewRect("Icon", window.transform, new Vector2(130, 130), new Vector2(-220, 90));
        iconImage = iconGo.AddComponent<Image>();
        iconImage.preserveAspect = true;

        costText = Text(window.transform, "Cost", 26, new Vector2(400, 40), new Vector2(70, 125), TextAlignmentOptions.Left, Color.white);
        marketText = Text(window.transform, "Market", 26, new Vector2(400, 40), new Vector2(70, 80), TextAlignmentOptions.Left, Color.white);
        Text(window.transform, "Your price:", 26, new Vector2(200, 50), new Vector2(-30, 30), TextAlignmentOptions.Left, Color.white);

        // Ô nhập giá
        GameObject inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        inputGo.name = "PriceInput";
        inputGo.transform.SetParent(window.transform, false);
        RectTransform irt = inputGo.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(180, 50);
        irt.anchoredPosition = new Vector2(170, 30);
        input = inputGo.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.pointSize = 30;
        input.onValueChanged.AddListener(OnInputChanged);
        foreach (TMP_Text t in inputGo.GetComponentsInChildren<TMP_Text>(true))
        {
            t.fontSize = 30;
            t.alignment = TextAlignmentOptions.Center;
        }

        // Nút tăng giảm nhanh
        float[] steps = { -1f, -0.1f, 0.1f, 1f };
        string[] labels = { "-1.00", "-0.10", "+0.10", "+1.00" };
        for (int i = 0; i < steps.Length; i++)
        {
            float d = steps[i];
            Btn(window.transform, labels[i], new Vector2(120, 48), new Vector2(-195 + i * 130, -35), () => Step(d));
        }

        profitText = Text(window.transform, "Profit", 26, new Vector2(560, 40), new Vector2(0, -95), TextAlignmentOptions.Center, Green);
        reactionText = Text(window.transform, "Reaction", 24, new Vector2(560, 40), new Vector2(0, -135), TextAlignmentOptions.Center, Green);

        Btn(window.transform, "Market price", new Vector2(200, 56), new Vector2(-200, -195), () => SetPrice(MarketPrices.Get(item), true));
        Btn(window.transform, "Cancel", new Vector2(160, 56), new Vector2(15, -195), Close);
        Button ok = Btn(window.transform, "Confirm", new Vector2(180, 56), new Vector2(205, -195), Confirm);
        ok.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f);

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

    private static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = NewRect("Button_" + label, parent, size, pos);
        go.AddComponent<Image>().color = ButtonColor;
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        Text(go.transform, label, 24, size, Vector2.zero, TextAlignmentOptions.Center, Color.white, true);
        return b;
    }
}
