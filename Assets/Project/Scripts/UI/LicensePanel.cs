using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Màn hình giấy phép trên máy tính (giống Supermarket Simulator): danh sách nhóm hàng, cấp yêu cầu, giá, nút mua.
/// Mở từ nút "Licenses" trên máy tính. Esc / X để đóng (về lại màn hình chợ).
/// </summary>
public class LicensePanel : MonoBehaviour
{
    private static LicensePanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    /// <summary>Frame vừa đóng bảng (để Esc không đóng luôn máy tính phía sau).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject root;
    private TMP_Text headerInfo;
    private readonly List<(ProductLicense lic, TMP_Text status, Button buy, Image bg)> rows = new List<(ProductLicense, TMP_Text, Button, Image)>();

    private static readonly Color WindowColor = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    private static readonly Color HeaderColor = new Color(0.0f, 0.6f, 0.95f, 1f);
    private static readonly Color RowColor = new Color(0.2f, 0.24f, 0.3f, 1f);
    private static readonly Color OwnedColor = new Color(0.16f, 0.32f, 0.2f, 1f);

    public static void Open()
    {
        if (instance == null) instance = new GameObject("LicensePanel").AddComponent<LicensePanel>();
        instance.root.SetActive(true);
        UIState.Open(instance);
        instance.Refresh();
    }

    void Awake()
    {
        Build();
        EconomyManager.OnMoneyChanged += OnMoney;
        StoreProgress.OnChanged += Refresh;
        LicenseManager.OnLicensesChanged += Refresh;
    }

    void OnDestroy()
    {
        EconomyManager.OnMoneyChanged -= OnMoney;
        StoreProgress.OnChanged -= Refresh;
        LicenseManager.OnLicensesChanged -= Refresh;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnMoney(float _) => Refresh();

    private void Close()
    {
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        UIState.Close(this);
    }

    private void Refresh()
    {
        if (root == null || !root.activeSelf) return;
        headerInfo.text = $"Store level {StoreProgress.Level}   |   Money {EconomyManager.Format(EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0f)}";
        foreach (var r in rows)
        {
            bool owned = LicenseManager.Owns(r.lic);
            bool can = LicenseManager.CanBuy(r.lic, out string reason);
            r.bg.color = owned ? OwnedColor : RowColor;
            r.status.text = owned ? "<color=#6BE37B>OWNED</color>" : (can ? $"<color=#FFFFFF>{EconomyManager.Format(r.lic.price)}</color>" : $"<color=#FF8A7A>{reason}</color>");
            r.buy.gameObject.SetActive(!owned);
            r.buy.interactable = can;
        }
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
        canvas.sortingOrder = 550; // nằm trên giao diện máy tính
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = NewRect("Root", canvasGo.transform);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        int n = LicenseManager.All.Count;
        float rowH = 62f, gap = 8f;
        float h = 170 + n * (rowH + gap);
        GameObject window = NewRect("Window", root.transform, new Vector2(960, h));
        window.AddComponent<Image>().color = WindowColor;

        GameObject header = NewRect("Header", window.transform, new Vector2(960, 70), new Vector2(0, h / 2 - 35));
        header.AddComponent<Image>().color = HeaderColor;
        Text(header.transform, "PRODUCT LICENSES", 34, new Vector2(900, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        Btn(header.transform, "X", new Vector2(50, 50), new Vector2(445, 0), Close, new Color(0.9f, 0.2f, 0.2f));
        headerInfo = Text(window.transform, "", 24, new Vector2(900, 36), new Vector2(0, h / 2 - 95), TextAlignmentOptions.Center, false);

        float y = h / 2 - 145;
        foreach (ProductLicense lic in LicenseManager.All)
        {
            GameObject row = NewRect("Row_" + lic.id, window.transform, new Vector2(900, rowH), new Vector2(0, y));
            Image bg = row.AddComponent<Image>();
            Text(row.transform, $"<b>{lic.name}</b>", 26, new Vector2(300, rowH), new Vector2(-290, 0), TextAlignmentOptions.Left, false);
            Text(row.transform, $"<size=70%>Lv {lic.requiredLevel}  ·  {lic.keys.Length} product types</size>", 26, new Vector2(240, rowH), new Vector2(-30, 0), TextAlignmentOptions.Left, false);
            TMP_Text status = Text(row.transform, "", 24, new Vector2(240, rowH), new Vector2(200, 0), TextAlignmentOptions.Center, true);
            ProductLicense captured = lic;
            Button buy = Btn(row.transform, "Buy", new Vector2(110, 46), new Vector2(380, 0), () => LicenseManager.TryBuy(captured), new Color(0.2f, 0.7f, 0.3f));
            rows.Add((lic, status, buy, bg));
            y -= rowH + gap;
        }
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

    private static TMP_Text Text(Transform parent, string text, float size, Vector2 box, Vector2 pos, TextAlignmentOptions align, bool bold)
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

    private static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject go = NewRect("Button_" + label, parent, size, pos);
        go.AddComponent<Image>().color = color;
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        Text(go.transform, label, 26, size, Vector2.zero, TextAlignmentOptions.Center, true);
        return b;
    }
}
