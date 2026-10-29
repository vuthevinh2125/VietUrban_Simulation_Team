using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh cấp độ cửa hàng (góc trên giữa màn hình) + thông báo nổi (Toast) như "Store level 3!".
/// Tự tạo khi vào game, không cần đặt vào scene.
/// </summary>
public class ProgressHUD : MonoBehaviour
{
    private static ProgressHUD instance;
    private TMP_Text levelText, xpText, toastText, moneyText, dayText, clockText;
    private float shownMoney = float.NaN;
    private RectTransform barFill;
    private CanvasGroup toastGroup;
    private readonly Queue<string> toasts = new Queue<string>();
    private float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance == null) new GameObject("ProgressHUD").AddComponent<ProgressHUD>();
    }

    /// <summary>Hiện một dòng thông báo giữa màn hình vài giây.</summary>
    public static void Toast(string message)
    {
        if (instance == null) AutoCreate();
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

    /// <summary>Góc trên trái: ngày, giờ + trạng thái cửa hàng, số tiền (cập nhật liên tục).</summary>
    private void RefreshInfo()
    {
        GameManager gm = GameManager.Instance;
        EconomyManager eco = EconomyManager.Instance;
        if (eco != null && !Mathf.Approximately(eco.Money, shownMoney))
        {
            shownMoney = eco.Money;
            moneyText.text = EconomyManager.Format(eco.Money);
            moneyText.color = eco.Money >= 0f ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f);
        }
        if (gm != null)
        {
            dayText.text = $"DAY {gm.currentDay}";
            string status = gm.IsStoreOpen ? "<color=#5BE36B>OPEN</color>" : (gm.IsClosingTime ? "<color=#FFB347>CLOSING</color>" : "<color=#FF6B6B>CLOSED</color>");
            clockText.text = $"{gm.ClockString}   {status}";
        }
    }

    void OnDestroy()
    {
        StoreProgress.OnChanged -= Refresh;
        StoreProgress.OnLevelUp -= OnLevelUp;
        LicenseManager.OnLicensesChanged -= OnLicense;
        if (instance == this) instance = null;
    }

    private void OnLevelUp(int level)
    {
        string unlock = "";
        foreach (ProductLicense l in LicenseManager.All)
            if (l.requiredLevel == level) unlock += $"\n<size=70%>New license available: {l.name}</size>";
        Toast($"STORE LEVEL {level}!{unlock}");
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
        if (Time.unscaledTime > toastUntil)
        {
            if (toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                toastUntil = Time.unscaledTime + 3f;
            }
        }
        float target = Time.unscaledTime < toastUntil ? 1f : 0f;
        toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, Time.unscaledDeltaTime * 4f);
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

        // Khung cấp độ: giữa trên
        GameObject box = Rect("Level", canvasGo.transform, new Vector2(360, 64), new Vector2(0.5f, 1f), new Vector2(0, -12));
        box.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        levelText = Label(box.transform, 24, new Vector2(340, 30), new Vector2(0, 14), TextAlignmentOptions.Center, true);

        GameObject bar = Rect("Bar", box.transform, new Vector2(320, 14), new Vector2(0.5f, 0.5f), new Vector2(0, -14));
        bar.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(bar.transform, false);
        barFill = (RectTransform)fill.transform;
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = new Color(0.3f, 0.8f, 1f);
        xpText = Label(bar.transform, 12, new Vector2(320, 14), Vector2.zero, TextAlignmentOptions.Center, true);

        // Ngày / giờ / tiền: góc trên trái
        GameObject info = Rect("Info", canvasGo.transform, new Vector2(300, 110), new Vector2(0f, 1f), new Vector2(160, -12));
        info.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dayText = Label(info.transform, 26, new Vector2(280, 32), new Vector2(0, 34), TextAlignmentOptions.Left, true);
        clockText = Label(info.transform, 20, new Vector2(280, 28), new Vector2(0, 4), TextAlignmentOptions.Left, false);
        moneyText = Label(info.transform, 32, new Vector2(280, 40), new Vector2(0, -32), TextAlignmentOptions.Left, true);

        // Thông báo nổi
        GameObject toast = Rect("Toast", canvasGo.transform, new Vector2(900, 110), new Vector2(0.5f, 1f), new Vector2(0, -150));
        toast.AddComponent<Image>().color = new Color(0.05f, 0.35f, 0.6f, 0.85f);
        toastGroup = toast.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastText = Label(toast.transform, 34, new Vector2(880, 100), Vector2.zero, TextAlignmentOptions.Center, true);
    }

    private static GameObject Rect(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return go;
    }

    private static TMP_Text Label(Transform parent, float size, Vector2 box, Vector2 pos, TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }
}
