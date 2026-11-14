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

    private static readonly string[] Paint = { "3E7BC0", "C8161D", "E9E3D6", "4FA36A", "E8B13A", "7A5BB5" };

    private class Card
    {
        public VehicleManager.Entry entry;
        public TMP_Text badge, level, price, action;
        public Image speedBar, boostBar;
        public TMP_Text speedText, boostText;
        public Button button;
        public Image buttonImage;
        public GameObject stamp, plate, tag;
        public TMP_Text tagText, lockText;
        public CanvasGroup group;
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
        headerInfo.text = $"Faster vehicles finish deliveries sooner. Owned vehicles park outside - walk up and press <b><color=#F1F3F6>F</color></b> to ride.   Owned {ownedCount}/{VehicleManager.Catalog.Length}";

        foreach (Card c in cards)
        {
            var e = c.entry;
            bool own = VehicleManager.Owns(e.id);
            bool driving = VehicleManager.IsBeingDriven(e.id);
            var result = VehicleManager.CanBuy(e);
            bool lvOk = StoreProgress.Level >= e.level;

            c.stamp.SetActive(own);
            c.plate.SetActive(own);
            c.tag.SetActive(!own && lvOk);
            c.tagText.text = e.price <= 0f ? "FREE" : EconomyManager.Format(e.price);
            c.lockText.gameObject.SetActive(!own && !lvOk);
            c.lockText.text = $"needs store Lv {e.level} · {(e.price <= 0f ? "free" : EconomyManager.Format(e.price))}";
            c.group.alpha = !own && !lvOk ? 0.72f : 1f;

            if (own)
            {
                c.action.text = driving ? "IN USE" : "CALL";
                c.button.interactable = !driving;
                c.button.gameObject.SetActive(true);
            }
            else
            {
                c.action.text = result == VehicleManager.BuyResult.NotEnoughMoney ? "NO MONEY" : "BUY";
                c.button.interactable = result == VehicleManager.BuyResult.Ok;
                c.button.gameObject.SetActive(lvOk);
            }
            c.buttonImage.color = own ? ShopPC.Ink : ShopPC.Red;
            c.action.color = own ? ShopPC.Gold : ShopPC.Cream;
        }
    }

    private void OnAction(Card c)
    {
        if (VehicleManager.Owns(c.entry.id)) VehicleManager.BringToStore(c.entry);
        else VehicleManager.Buy(c.entry);
        Refresh();
    }

    // ================= Dựng giao diện (bản thiết kế 05: giấy đăng ký xe) =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        RectTransform c = ShopPC.Computer(canvas, "VEHICLES", Close, out root);
        headerInfo = ShopPC.Title(c, "VEHICLES", "", out _);

        int n = Mathf.Max(1, VehicleManager.Catalog.Length);
        const float gap = 21f, top = 84f, cardH = 480f;
        float cardW = Mathf.Min(420f, (ShopPC.ContentSize.x - gap * (n - 1)) / n);
        float maxBoost = 1f;
        foreach (var e in VehicleManager.Catalog) maxBoost = Mathf.Max(maxBoost, e.boost);
        for (int i = 0; i < VehicleManager.Catalog.Length; i++)
            cards.Add(BuildCard(c, VehicleManager.Catalog[i], i, i * (cardW + gap), top, cardW, cardH, maxBoost));

        ShopPC.StickyNote(c, "Drive slowly after dark. Not everyone on the road at night is a customer. - bà Lan", 0f, top + cardH + 60f, 780f, 96f, -1f, 31f);
        root.SetActive(false);
    }

    private Card BuildCard(Transform parent, VehicleManager.Entry e, int index, float x, float y, float w, float h, float maxBoost)
    {
        var c = new Card { entry = e };
        RectTransform card = ShopPC.Sheet("Card_" + e.name, parent, x, y, w, h, ShopPC.Paper, 0f, 24f);
        c.group = card.gameObject.AddComponent<CanvasGroup>();
        bool car = e.type == "Car";
        Color paint = ShopPC.Hex(System.Convert.ToInt32(Paint[index % Paint.Length], 16));

        // Ảnh: trời + mặt đường + xe
        float aw = w - 36f;
        RectTransform art = ShopPC.Box("Art", card, 18f, 18f, aw, 144f, ShopPC.Hex(0xDDE6EE));
        ShopPC.Box("Road", art, 0f, 98f, aw, 46f, ShopPC.Hex(0x9AA3AE));
        if (car) DrawCar(art, paint, e.speed >= 14f);
        else DrawBike(art, paint);
        RectTransform plate = ShopPC.Box("Plate", art, 9f, 9f, 150f, 30f, Color.white);
        ShopUI.Frame(plate, plate.sizeDelta, ShopPC.Hex(0x14100A), 3f);
        int hsh = Mathf.Abs(e.id.GetHashCode());
        string plateNo = car ? $"51G-{hsh % 900 + 100}.{hsh / 7 % 90 + 10}" : $"29-{(char)('A' + hsh % 26)}1 {hsh % 900 + 100}.{hsh / 7 % 90 + 10}";
        ShopUI.Text(plate, plateNo, ShopUI.Body, 15, plate.sizeDelta, Vector2.zero, ShopPC.Hex(0x14100A)).fontStyle = FontStyles.Bold;
        c.plate = plate.gameObject;

        ShopPC.Text(card, e.name.ToUpperInvariant(), ShopUI.Print, 33, 21f, 174f, w - 42f, 42f, ShopPC.Ink, TextAlignmentOptions.Left, 1.5f).enableAutoSizing = true;
        ShopPC.Text(card, $"{e.type}{(e.starter ? " · starter" : "")} · store Lv {e.level}", ShopUI.Body, 16, 21f, 216f, w - 42f, 24f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
        TMP_Text desc = ShopPC.Text(card, e.description, ShopUI.Body, 15, 21f, 244f, w - 42f, 66f, ShopPC.Body, TextAlignmentOptions.TopLeft);
        desc.enableAutoSizing = true; desc.fontSizeMin = 12; desc.fontSizeMax = 15;

        c.speedBar = StatBar(card, "Speed", 322f, w, e.speed / maxBoost, out c.speedText);
        c.boostBar = StatBar(card, "Boost", 352f, w, e.boost / maxBoost, out c.boostText);
        c.speedText.text = Kmh(e.speed);
        c.boostText.text = Kmh(e.boost);

        float fy = h - 18f - 54f;
        c.stamp = ShopPC.Stamp(card, "CHÍNH CHỦ · OWNED", ShopPC.At(card.sizeDelta, 21f, fy + 3f, 210f, 48f), 21f, -5f).gameObject;
        c.tagText = ShopPC.PriceTag(card, "", ShopPC.At(card.sizeDelta, 21f, fy + 5f, 120f, 44f), 33f, -3f);
        c.tag = c.tagText.transform.parent.gameObject;
        c.lockText = ShopPC.Text(card, "", ShopUI.Body, 16, 21f, fy, w - 42f, 54f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
        c.button = ShopPC.Btn(card, "BUY", w - 21f - 130f, fy, 130f, 54f, null, ShopPC.Kind.Red, 22f);
        c.button.onClick.AddListener(() => OnAction(c));
        c.buttonImage = c.button.GetComponent<Image>();
        c.action = c.button.GetComponentInChildren<TMP_Text>();
        return c;
    }

    private static Image StatBar(Transform card, string label, float y, float w, float fill, out TMP_Text value)
    {
        ShopPC.Text(card, label, ShopUI.Body, 16, 21f, y - 12f, 75f, 24f, ShopPC.Muted, TextAlignmentOptions.Left, 0f, true);
        Image bar = ShopPC.HBar(card, ShopPC.At(((RectTransform)card).sizeDelta, 105f, y - 6f, 0f, 12f), w - 105f - 120f, 12f, ShopPC.A(ShopPC.Ink, 0.12f), ShopPC.Red);
        bar.fillAmount = Mathf.Clamp01(fill);
        value = ShopPC.Text(card, "", ShopUI.Body, 16, w - 21f - 100f, y - 12f, 100f, 24f, ShopPC.Ink, TextAlignmentOptions.Right, 0f, true);
        return bar;
    }

    private static void DrawBike(RectTransform art, Color paint)
    {
        Vector2 s = art.sizeDelta;
        float cx = s.x * 0.5f;
        Color tyre = ShopPC.Hex(0x14100A), road = ShopPC.Hex(0x9AA3AE);
        foreach (float wx in new[] { cx - 66f, cx + 66f })
        {
            ShopPC.Dot(art, new Vector2(60f, 60f), ShopPC.At(s, wx - 30f, 144f - 66f, 60f, 60f), tyre);
            ShopPC.Dot(art, new Vector2(39f, 39f), ShopPC.At(s, wx - 19.5f, 144f - 55.5f, 39f, 39f), road);
        }
        ShopPC.Box("Frame", art, cx - 66f, 144f - 54f, 132f, 21f, paint, 0f);
        ShopPC.Box("Body", art, cx - 45f, 144f - 84f, 90f, 33f, paint);
        ShopPC.Box("Seat", art, cx - 54f, 144f - 93f, 60f, 12f, ShopPC.Hex(0x2A2018));
        ShopPC.Box("Bar", art, cx + 39f, 144f - 108f, 9f, 42f, ShopPC.Hex(0x2A2018), -18f);
        ShopPC.Box("Handle", art, cx + 33f, 144f - 111f, 36f, 7.5f, ShopPC.Hex(0x2A2018));
    }

    private static void DrawCar(RectTransform art, Color paint, bool low)
    {
        Vector2 s = art.sizeDelta;
        float cx = s.x * 0.5f, bw = 255f, bh = low ? 27f : 39f;
        float left = cx - bw * 0.5f;
        ShopPC.Box("Body", art, left, 144f - 21f - bh, bw, bh, paint);
        ShopPC.Box("Cabin", art, left + 57f, 144f - 21f - bh - (low ? 24f : 33f), bw - 108f, low ? 24f : 33f, paint);
        ShopPC.Box("Window", art, left + 66f, 144f - 21f - bh - (low ? 18f : 27f), bw - 126f, low ? 15f : 22f, ShopPC.A(Color.white, 0.6f));
        foreach (float wx in new[] { left + 33f, left + bw - 33f - 45f })
        {
            ShopPC.Dot(art, new Vector2(45f, 45f), ShopPC.At(s, wx, 144f - 45f, 45f, 45f), ShopPC.Hex(0x14100A));
            ShopPC.Dot(art, new Vector2(21f, 21f), ShopPC.At(s, wx + 12f, 144f - 33f, 21f, 21f), ShopPC.Hex(0x9AA3AE));
        }
    }

    private static string Kmh(float speed) => Mathf.RoundToInt(speed * 3.6f).ToString(CultureInfo.InvariantCulture) + " km/h";

}
