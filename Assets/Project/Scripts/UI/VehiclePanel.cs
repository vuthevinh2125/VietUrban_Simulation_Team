using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tab PHƯƠNG TIỆN trên máy tính: mua xe máy / ô tô, xem tốc độ, gọi xe đã mua về trước cửa hàng.
/// Esc / X để đóng.
/// </summary>
public class VehiclePanel : MonoBehaviour
{
    private static VehiclePanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    private static readonly Color CardColor = new Color(0.09f, 0.11f, 0.15f, 1f);
    private static readonly Color BikeColor = new Color(0.1f, 0.62f, 0.62f, 1f);
    private static readonly Color CarColor = new Color(0.45f, 0.3f, 0.8f, 1f);
    private static readonly Color Disabled = new Color(0.3f, 0.33f, 0.38f, 1f);
    private static readonly Color BlueBtn = new Color(0.0f, 0.55f, 0.9f, 1f);

    private class Card
    {
        public VehicleManager.Entry entry;
        public TMP_Text badge, level, price, action;
        public Image speedBar, boostBar;
        public TMP_Text speedText, boostText;
        public Button button;
        public Image buttonImage;
    }

    private GameObject root;
    private TMP_Text headerInfo;
    private readonly List<Card> cards = new List<Card>();

    public static void Open()
    {
        if (instance == null) instance = new GameObject("VehiclePanel").AddComponent<VehiclePanel>();
        instance.root.SetActive(true);
        UIState.Open(instance);
        instance.Refresh();
    }

    void Awake()
    {
        Build();
        VehicleManager.OnChanged += Refresh;
        StoreProgress.OnChanged += Refresh;
        EconomyManager.OnMoneyChanged += OnMoney;
    }

    void OnDestroy()
    {
        VehicleManager.OnChanged -= Refresh;
        StoreProgress.OnChanged -= Refresh;
        EconomyManager.OnMoneyChanged -= OnMoney;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    private void OnMoney(float _) => Refresh();

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Close()
    {
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        UIState.Close(this);
    }

    // ================= Cập nhật =================
    private void Refresh()
    {
        if (root == null || !root.activeSelf) return;
        int ownedCount = 0;
        foreach (var e in VehicleManager.Catalog) if (VehicleManager.Owns(e.id)) ownedCount++;
        headerInfo.text = $"Store Lv {StoreProgress.Level}   ·   Money {EconomyManager.Format(EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0f)}   ·   Owned {ownedCount}/{VehicleManager.Catalog.Length}";

        foreach (Card c in cards)
        {
            var e = c.entry;
            bool own = VehicleManager.Owns(e.id);
            bool driving = VehicleManager.IsBeingDriven(e.id);
            var result = VehicleManager.CanBuy(e);

            c.badge.text = own ? (driving ? "<color=#FFD54A>IN USE</color>" : "<color=#6BE07A>OWNED</color>") : "";
            bool lvOk = StoreProgress.Level >= e.level;
            c.level.text = lvOk
                ? $"<color=#6BE07A>Store Lv {e.level}</color> required"
                : $"<color=#FF8A7A>Store Lv {e.level}</color> required  (you are Lv {StoreProgress.Level})";
            c.price.text = own ? "" : e.price <= 0f ? "FREE" : EconomyManager.Format(e.price);

            if (own)
            {
                c.action.text = driving ? "IN USE" : "BRING TO STORE";
                c.button.interactable = !driving;
                c.buttonImage.color = driving ? Disabled : BlueBtn;
            }
            else
            {
                switch (result)
                {
                    case VehicleManager.BuyResult.Ok: c.action.text = "BUY"; break;
                    case VehicleManager.BuyResult.LevelTooLow: c.action.text = $"LOCKED  ·  LV {e.level}"; break;
                    case VehicleManager.BuyResult.NotEnoughMoney: c.action.text = "NOT ENOUGH MONEY"; break;
                    default: c.action.text = "UNAVAILABLE"; break;
                }
                bool ok = result == VehicleManager.BuyResult.Ok;
                c.button.interactable = ok;
                c.buttonImage.color = ok ? UIKit.Green : Disabled;
            }
        }
    }

    private void OnAction(Card c)
    {
        if (VehicleManager.Owns(c.entry.id)) VehicleManager.BringToStore(c.entry);
        else VehicleManager.Buy(c.entry);
        Refresh();
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        root = UIKit.FullScreen("Root", canvas, new Color(0f, 0f, 0f, 0.5f));
        GameObject window = UIKit.Panel("Window", root.transform, new Vector2(1300, 760), Vector2.zero, UIKit.Window);
        GameObject header = UIKit.Panel("Header", window.transform, new Vector2(1300, 64), new Vector2(0, 348), UIKit.Header);
        UIKit.Text(header.transform, "VEHICLES", 32, new Vector2(1100, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        UIKit.Btn(header.transform, "X", new Vector2(48, 48), new Vector2(620, 0), Close, UIKit.Red);

        headerInfo = UIKit.Text(window.transform, "", 20, new Vector2(1200, 30), new Vector2(0, 288), TextAlignmentOptions.Center, false, new Color(0.8f, 0.85f, 0.9f));
        UIKit.Text(window.transform, "Owned vehicles are parked outside the store - walk up and press <b>[F]</b> to ride. Faster vehicles make deliveries easier.",
            18, new Vector2(1200, 28), new Vector2(0, 258), TextAlignmentOptions.Center, false, new Color(0.65f, 0.72f, 0.8f));

        float maxBoost = 1f;
        foreach (var e in VehicleManager.Catalog) maxBoost = Mathf.Max(maxBoost, e.boost);

        int n = VehicleManager.Catalog.Length;
        const float cardW = 390f, cardH = 560f, gap = 24f;
        float total = n * cardW + (n - 1) * gap;
        for (int i = 0; i < n; i++)
        {
            var e = VehicleManager.Catalog[i];
            float x = -total / 2f + cardW / 2f + i * (cardW + gap);
            cards.Add(BuildCard(window.transform, e, new Vector2(x, -40f), cardW, cardH, maxBoost));
        }
        root.SetActive(false);
    }

    private Card BuildCard(Transform parent, VehicleManager.Entry e, Vector2 pos, float w, float h, float maxBoost)
    {
        var c = new Card { entry = e };
        GameObject card = UIKit.Panel("Card_" + e.name, parent, new Vector2(w, h), pos, CardColor);
        Transform t = card.transform;
        bool car = e.type == "Car";
        Color accent = car ? CarColor : BikeColor;
        float top = h / 2f;

        // Dải tiêu đề
        GameObject strip = UIKit.Panel("Title", t, new Vector2(w, 86), new Vector2(0, top - 43), accent);
        UIKit.Text(strip.transform, e.name, 30, new Vector2(w - 30, 40), new Vector2(0, 12), TextAlignmentOptions.Left, true);
        UIKit.Text(strip.transform, e.type.ToUpperInvariant() + (e.starter ? "  ·  STARTER" : ""), 16, new Vector2(w - 30, 24), new Vector2(0, -22),
            TextAlignmentOptions.Left, true, new Color(1f, 1f, 1f, 0.8f));
        c.badge = UIKit.Text(strip.transform, "", 18, new Vector2(w - 30, 24), new Vector2(0, -22), TextAlignmentOptions.Right, true);

        // Mô tả
        TMP_Text desc = UIKit.Text(t, e.description, 19, new Vector2(w - 40, 96), new Vector2(0, top - 150), TextAlignmentOptions.TopLeft, false,
            new Color(0.82f, 0.87f, 0.93f));
        desc.textWrappingMode = TextWrappingModes.Normal;

        // Tốc độ
        float y = top - 236;
        UIKit.Text(t, "Top speed", 18, new Vector2(150, 26), new Vector2(-w / 2f + 95, y), TextAlignmentOptions.Left, false, new Color(0.7f, 0.76f, 0.84f));
        c.speedBar = Bar(t, new Vector2(-w / 2f + 20, y - 24), w - 140, accent);
        c.speedText = UIKit.Text(t, "", 18, new Vector2(100, 26), new Vector2(w / 2f - 60, y - 24), TextAlignmentOptions.Right, true);
        y -= 70;
        UIKit.Text(t, "Boost [Shift]", 18, new Vector2(150, 26), new Vector2(-w / 2f + 95, y), TextAlignmentOptions.Left, false, new Color(0.7f, 0.76f, 0.84f));
        c.boostBar = Bar(t, new Vector2(-w / 2f + 20, y - 24), w - 140, accent);
        c.boostText = UIKit.Text(t, "", 18, new Vector2(100, 26), new Vector2(w / 2f - 60, y - 24), TextAlignmentOptions.Right, true);
        c.speedBar.fillAmount = e.speed / maxBoost;
        c.boostBar.fillAmount = e.boost / maxBoost;
        c.speedText.text = Kmh(e.speed);
        c.boostText.text = Kmh(e.boost);

        // Yêu cầu cấp + giá
        c.level = UIKit.Text(t, "", 18, new Vector2(w - 40, 26), new Vector2(0, -h / 2f + 150), TextAlignmentOptions.Center);
        c.price = UIKit.Text(t, "", 40, new Vector2(w - 40, 50), new Vector2(0, -h / 2f + 108), TextAlignmentOptions.Center, true, new Color(1f, 0.84f, 0.3f));

        // Nút hành động
        c.button = UIKit.Btn(t, "BUY", new Vector2(w - 50, 58), new Vector2(0, -h / 2f + 44), null, UIKit.Green, 24);
        c.button.onClick.AddListener(() => OnAction(c));
        c.buttonImage = c.button.GetComponent<Image>();
        c.action = c.button.GetComponentInChildren<TMP_Text>();
        return c;
    }

    private static string Kmh(float speed) => Mathf.RoundToInt(speed * 3.6f).ToString(CultureInfo.InvariantCulture) + " km/h";

    private static Sprite white;

    private static Image Bar(Transform parent, Vector2 leftCenter, float width, Color color)
    {
        GameObject bg = UIKit.Rect("BarBg", parent, new Vector2(width, 12f), leftCenter + new Vector2(width / 2f, 0f));
        Image b = bg.AddComponent<Image>(); b.color = new Color(1f, 1f, 1f, 0.08f); b.raycastTarget = false;
        GameObject fg = UIKit.Rect("Bar", bg.transform, new Vector2(width, 12f), Vector2.zero);
        Image f = fg.AddComponent<Image>();
        if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
        f.sprite = white; f.color = color; f.raycastTarget = false;
        f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = (int)Image.OriginHorizontal.Left;
        return f;
    }
}
