using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD "Tiệm của bà Lan" (tự tạo khi vào game, không cần đặt vào scene):
///  - Góc trên trái: tờ lịch bloc nhỏ (ngày), đồng hồ, biển treo OPEN / CLOSING / CLOSED, nhãn giá vàng (tiền quỹ; nảy lên và
///    hiện "+ $x" khi tiền đổi).
///  - Giữa trên: biển cấp cửa hàng + thanh XP; lên cấp thì băng rôn đỏ "NHIỆT LIỆT CHÀO MỪNG" thả xuống vài giây.
///  - Thông báo nổi (Toast) kiểu "loa phường": tờ giấy trượt xuống giữa màn hình vài giây.
/// </summary>
public class ProgressHUD : MonoBehaviour
{
    private static ProgressHUD instance;

    private TMP_Text levelText, xpText, toastText, moneyText, dayText, clockText, statusText, gainText, bannerText;
    private Image statusBg;
    private RectTransform barFill, moneyTag, banner, toastBox;
    private CanvasGroup toastGroup;
    private readonly Queue<string> toasts = new Queue<string>();
    private float toastUntil, gainUntil = -10f, bannerUntil = -10f;
    private float shownMoney = float.NaN;
    private int shownDay = -1, shownStatus = -1;
    private string shownClock;

    private static readonly Color OpenGreen = new Color(0.12f, 0.48f, 0.27f);
    private static readonly Color ClosingOrange = new Color(0.85f, 0.5f, 0.1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        CreateIfMissing();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => CreateIfMissing();

    private static void CreateIfMissing()
    {
        if (instance == null && SupermarketScene.IsActive) new GameObject("ProgressHUD").AddComponent<ProgressHUD>();
    }

    /// <summary>Hiện một dòng thông báo giữa màn hình vài giây.</summary>
    public static void Toast(string message)
    {
        if (instance == null) CreateIfMissing();
        if (instance == null) return;
        instance.toasts.Enqueue(message);
    }

    void Awake()
    {
        instance = this;
        Build();
        StoreProgress.OnChanged += Refresh;
        StoreProgress.OnLevelUp += OnLevelUp;
        LicenseManager.OnLicensesChanged += OnLicense;
        Refresh();
    }

    void OnDestroy()
    {
        StoreProgress.OnChanged -= Refresh;
        StoreProgress.OnLevelUp -= OnLevelUp;
        LicenseManager.OnLicensesChanged -= OnLicense;
        if (instance == this) instance = null;
    }

    /// <summary>Góc trên trái: ngày, giờ + trạng thái cửa hàng, số tiền (chỉ dựng lại chữ khi giá trị đổi).</summary>
    private void RefreshInfo()
    {
        GameManager gm = GameManager.Instance;
        EconomyManager eco = EconomyManager.Instance;
        if (eco != null && !Mathf.Approximately(eco.Money, shownMoney))
        {
            if (!float.IsNaN(shownMoney))
            {
                float delta = eco.Money - shownMoney;
                gainText.text = (delta > 0f ? "+ " : "- ") + EconomyManager.Format(Mathf.Abs(delta));
                gainText.color = delta > 0f ? new Color(0.62f, 0.96f, 0.72f) : new Color(1f, 0.62f, 0.56f);
                gainUntil = Time.unscaledTime + 1.8f;
            }
            shownMoney = eco.Money;
            moneyText.text = EconomyManager.Format(eco.Money);
            moneyText.color = eco.Money >= 0f ? ShopUI.Ink : ShopUI.SignRed;
        }
        if (gm == null) return;
        if (gm.currentDay != shownDay) { shownDay = gm.currentDay; dayText.text = shownDay.ToString(); }
        string clock = gm.ClockString;
        if (clock != shownClock) { shownClock = clock; clockText.text = clock; }
        int status = gm.IsStoreOpen ? 0 : (gm.IsClosingTime ? 1 : 2);
        if (status != shownStatus)
        {
            shownStatus = status;
            statusText.text = status == 0 ? "OPEN" : status == 1 ? "CLOSING" : "CLOSED";
            statusBg.color = status == 0 ? OpenGreen : status == 1 ? ClosingOrange : ShopUI.SignRed;
        }
    }

    private void OnLevelUp(int level)
    {
        AudioManager.Play("level_up", 0.9f);
        bannerText.text = $"STORE LEVEL {level}";
        bannerUntil = Time.unscaledTime + 4f;
        toastUntil = Mathf.Max(toastUntil, bannerUntil);          // loa phường chờ băng rôn rút lên
        foreach (ProductLicense l in LicenseManager.All)
            if (l.requiredLevel == level) Toast($"New license available: {l.name}");
    }

    private void OnLicense() => Toast("License purchased! New products are in the market.");

    private void Refresh()
    {
        levelText.text = $"STORE LV {StoreProgress.Level}";
        xpText.text = $"{StoreProgress.XP} / {StoreProgress.XPToNext} XP";
        barFill.anchorMax = new Vector2(StoreProgress.Progress01, 1f);
    }

    void Update()
    {
        RefreshInfo();
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

        // Loa phường
        if (now > toastUntil && toasts.Count > 0)
        {
            toastText.text = toasts.Dequeue();
            toastUntil = now + 3f;
        }
        float target = now < toastUntil && now > bannerUntil ? 1f : 0f;
        toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, dt * 4f);
        toastBox.anchoredPosition = new Vector2(0f, -150f + (1f - toastGroup.alpha) * 36f);

        // Băng rôn lên cấp: thả xuống rồi rút lên
        float by = now < bannerUntil ? -8f : 230f;
        banner.anchoredPosition = new Vector2(0f, Mathf.Lerp(banner.anchoredPosition.y, by, 1f - Mathf.Exp(-dt * 7f)));

        // Tiền vừa đổi: nhãn giá nảy lên, dòng "+ $x" bay lên rồi mờ đi
        float left = gainUntil - now;
        gainText.alpha = Mathf.Clamp01(left / 0.6f);
        gainText.rectTransform.anchoredPosition = new Vector2(250f, -26f + Mathf.Clamp01(1f - left / 1.8f) * 22f);
        moneyTag.localScale = Vector3.one * (1f + 0.14f * Mathf.Clamp01((left - 1.5f) / 0.3f));
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
    }

    private void Build()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform c = canvasGo.transform;
        const TextAlignmentOptions Left = TextAlignmentOptions.Left, Center = TextAlignmentOptions.Center;

        // Biển cấp cửa hàng: giữa trên
        RectTransform box = ShopUI.SignBoard(c, new Vector2(360, 64), Vector2.zero);
        Anchor(box, new Vector2(0.5f, 1f), new Vector2(0, -12));
        levelText = ShopUI.Text(box, "", ShopUI.Print, 24, new Vector2(340, 30), new Vector2(0, 13), ShopUI.SignGold, Center, 0f, 6f);
        RectTransform bar = ShopUI.Box("Bar", box, new Vector2(320, 14), new Vector2(0, -15), new Color(0f, 0f, 0f, 0.5f));
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(bar, false);
        barFill = (RectTransform)fill.transform;
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>(); fillImg.color = ShopUI.SignGold; fillImg.raycastTarget = false;
        xpText = ShopUI.Text(bar, "", ShopUI.Body, 11, new Vector2(320, 14), Vector2.zero, Color.white);
        xpText.fontStyle = FontStyles.Bold; xpText.outlineWidth = 0.25f; xpText.outlineColor = new Color32(0, 0, 0, 255);

        // Ngày / giờ / tiền: góc trên trái (giữ nguyên vùng 300 x 110 để các dòng thời tiết, đánh giá bên dưới không lệch)
        RectTransform info = ShopUI.Group("Info", c, new Vector2(300, 110), Vector2.zero);
        Anchor(info, new Vector2(0f, 1f), new Vector2(160, -12));
        RectTransform cal = ShopUI.Group("Calendar", info, new Vector2(80, 104), new Vector2(-108, 0), -3f);
        RectTransform pg = ShopUI.Sheet("Page", cal, new Vector2(80, 92), new Vector2(0, -6), ShopUI.Paper);
        ShopUI.Box("Binding", cal, new Vector2(80, 10), new Vector2(0, 45), new Color(0.5f, 0.52f, 0.56f));
        ShopUI.Box("Head", pg, new Vector2(80, 18), new Vector2(0, 37), ShopUI.SignRed);
        ShopUI.Text(pg, "DAY", ShopUI.Body, 10, new Vector2(80, 18), new Vector2(0, 37), new Color(1f, 0.95f, 0.84f), Center, 0f, 12f).fontStyle = FontStyles.Bold;
        dayText = ShopUI.Text(pg, "", ShopUI.Print, 52, new Vector2(76, 66), new Vector2(0, -9), ShopUI.SignRed);
        dayText.enableAutoSizing = true; dayText.fontSizeMin = 22; dayText.fontSizeMax = 52;
        clockText = ShopUI.Text(info, "", ShopUI.Sign, 36, new Vector2(150, 46), new Vector2(15, 30), Color.white, Left);
        clockText.outlineWidth = 0.22f; clockText.outlineColor = new Color32(0, 0, 0, 255);
        clockText.textWrappingMode = TextWrappingModes.NoWrap;            // "08:53 AM" không xuống dòng
        clockText.enableAutoSizing = true; clockText.fontSizeMin = 20; clockText.fontSizeMax = 36;
        RectTransform st = ShopUI.Box("Status", info, new Vector2(104, 28), new Vector2(98, 31), OpenGreen, 2f);
        statusBg = st.GetComponent<Image>();
        ShopUI.Frame(st, new Vector2(98, 22), Color.white, 2f);
        statusText = ShopUI.Text(st, "", ShopUI.Print, 15, new Vector2(104, 28), Vector2.zero, Color.white, Center, 0f, 6f);
        moneyTag = ShopUI.Sheet("Money", info, new Vector2(210, 52), new Vector2(45, -26), ShopUI.ShelfYellow, -1.5f);
        ShopUI.Text(moneyTag, "TIỀN QUỸ · CASH", ShopUI.Body, 9, new Vector2(194, 12), new Vector2(0, 18), ShopUI.Ink, Left, 0f, 8f).fontStyle = FontStyles.Bold;
        moneyText = ShopUI.Text(moneyTag, "", ShopUI.Print, 30, new Vector2(194, 36), new Vector2(0, -7), ShopUI.Ink, Left);
        moneyText.enableAutoSizing = true; moneyText.fontSizeMin = 18; moneyText.fontSizeMax = 30;
        gainText = ShopUI.Text(info, "", ShopUI.Hand, 28, new Vector2(200, 34), new Vector2(250, -26), Color.white, Left);
        gainText.outlineWidth = 0.2f; gainText.outlineColor = new Color32(0, 0, 0, 255);
        gainText.alpha = 0f;

        // Loa phường (thông báo nổi)
        toastBox = ShopUI.Sheet("Toast", c, new Vector2(900, 110), Vector2.zero, ShopUI.PaperOld);
        Anchor(toastBox, new Vector2(0.5f, 1f), new Vector2(0, -150));
        ShopUI.Box("Edge", toastBox, new Vector2(10, 110), new Vector2(-445, 0), ShopUI.SignRed);
        ShopUI.Text(toastBox, "LOA PHƯỜNG · NOTICE", ShopUI.Print, 16, new Vector2(850, 22), new Vector2(5, 40), ShopUI.SignRed, Left, 0f, 10f);
        toastText = ShopUI.Text(toastBox, "", ShopUI.Body, 28, new Vector2(850, 76), new Vector2(5, -12), ShopUI.Ink);
        toastText.fontStyle = FontStyles.Bold;
        toastText.enableAutoSizing = true; toastText.fontSizeMin = 16; toastText.fontSizeMax = 28;
        toastGroup = toastBox.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;

        // Băng rôn lên cấp (nằm ngoài màn hình cho tới khi lên cấp)
        banner = ShopUI.Group("Banner", c, new Vector2(720, 160), Vector2.zero);
        Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0, 230));
        ShopUI.Box("StringL", banner, new Vector2(3, 34), new Vector2(-310, 63), ShopUI.PaperOld);
        ShopUI.Box("StringR", banner, new Vector2(3, 34), new Vector2(310, 63), ShopUI.PaperOld);
        RectTransform cloth = ShopUI.Sheet("Cloth", banner, new Vector2(720, 126), new Vector2(0, -17), ShopUI.SignRed);
        ShopUI.Text(cloth, "NHIỆT LIỆT CHÀO MỪNG", ShopUI.Body, 16, new Vector2(700, 24), new Vector2(0, 42), new Color(1f, 0.88f, 0.54f), Center, 0f, 16f).fontStyle = FontStyles.Bold;
        bannerText = ShopUI.Text(cloth, "", ShopUI.Print, 56, new Vector2(700, 70), new Vector2(0, -10), ShopUI.ShelfYellow, Center, 0f, 8f);
        banner.gameObject.AddComponent<UIMotion>().swayDegrees = 0.8f;
    }
}
