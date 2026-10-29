using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tab NỘI THẤT trên máy tính: mua kệ / tủ lạnh / tủ đông. Mua xong thùng nội thất được giao tới chỗ giao hàng,
/// người chơi nhặt thùng lên và chọn chỗ lắp đặt. Esc / X để đóng.
/// </summary>
public class FurniturePanel : MonoBehaviour
{
    private static FurniturePanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject root, window;
    private TMP_Text headerInfo, statusText;
    private readonly List<(FurnitureData data, TMP_Text status, Button buy)> rows = new List<(FurnitureData, TMP_Text, Button)>();
    private List<FurnitureData> built;
    private int delivered;

    private static readonly Color WindowColor = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    private static readonly Color HeaderColor = new Color(0.0f, 0.6f, 0.95f, 1f);
    private static readonly Color RowColor = new Color(0.2f, 0.24f, 0.3f, 1f);

    public static void Open(List<FurnitureData> catalog)
    {
        if (instance == null) instance = new GameObject("FurniturePanel").AddComponent<FurniturePanel>();
        instance.Show(catalog);
    }

    void Awake()
    {
        BuildFrame();
        EconomyManager.OnMoneyChanged += OnMoney;
        StoreProgress.OnChanged += Refresh;
    }

    void OnDestroy()
    {
        EconomyManager.OnMoneyChanged -= OnMoney;
        StoreProgress.OnChanged -= Refresh;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnMoney(float _) => Refresh();

    private void Show(List<FurnitureData> catalog)
    {
        if (built != catalog) BuildRows(catalog);
        statusText.text = catalog == null || catalog.Count == 0 ? "No furniture yet. Run Tools > Store Setup > 9. Build Furniture Catalog." : "";
        root.SetActive(true);
        UIState.Open(this);
        Refresh();
    }

    private void Close()
    {
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        UIState.Close(this);
    }

    private void Refresh()
    {
        if (root == null || !root.activeSelf) return;
        float money = EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0f;
        headerInfo.text = $"Store level {StoreProgress.Level}   |   Money {EconomyManager.Format(money)}";
        foreach (var r in rows)
        {
            string reason = null;
            if (StoreProgress.Level < r.data.requiredLevel) reason = $"Requires level {r.data.requiredLevel}";
            else if (money < r.data.price) reason = "Not enough money";
            r.status.text = reason == null ? EconomyManager.Format(r.data.price) : $"<color=#FF8A7A>{reason}</color>";
            r.buy.interactable = reason == null;
        }
    }

    private void Buy(FurnitureData data)
    {
        if (data == null || data.prefab == null || StoreProgress.Level < data.requiredLevel) return;
        if (!EconomyManager.Instance.TrySpend(data.price, MoneyCategory.Upgrades, $"Furniture: {data.displayName}")) return;

        // Giao thùng tới chỗ giao hàng (xếp hàng ngang cạnh nhau)
        Transform dp = MarketManager.Instance != null ? MarketManager.Instance.deliveryPoint : null;
        Vector3 pos = dp != null ? dp.position : Vector3.zero;
        Quaternion rot = dp != null ? dp.rotation : Quaternion.identity;
        pos += rot * new Vector3(-2.5f - (delivered % 4) * 1.1f, 0f, (delivered / 4 % 3) * 1.1f);
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;
        delivered++;
        FurnitureBox.Spawn(data, pos, rot);

        statusText.text = $"{data.displayName} delivered! Pick up the box outside to place it.";
        ProgressHUD.Toast($"{data.displayName} delivered!");
        Debug.Log($"[Furniture] Mua {data.displayName} {EconomyManager.Format(data.price)}, giao tại {pos:F1}.");
    }

    // ================= Dựng giao diện =================
    private void BuildFrame()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = NewRect("Root", canvasGo.transform);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        root.SetActive(false);
    }

    private void BuildRows(List<FurnitureData> catalog)
    {
        built = catalog;
        rows.Clear();
        if (window != null) Destroy(window);

        int n = catalog != null ? catalog.Count : 0;
        float rowH = 62f, gap = 8f;
        float h = 220 + Mathf.Max(1, n) * (rowH + gap);
        window = NewRect("Window", root.transform, new Vector2(900, h));
        window.AddComponent<Image>().color = WindowColor;

        GameObject header = NewRect("Header", window.transform, new Vector2(900, 70), new Vector2(0, h / 2 - 35));
        header.AddComponent<Image>().color = HeaderColor;
        Text(header.transform, "FURNITURE", 34, new Vector2(840, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        Btn(header.transform, "X", new Vector2(50, 50), new Vector2(415, 0), Close, new Color(0.9f, 0.2f, 0.2f));
        headerInfo = Text(window.transform, "", 24, new Vector2(840, 36), new Vector2(0, h / 2 - 95), TextAlignmentOptions.Center, false);

        float y = h / 2 - 145;
        if (catalog != null)
            foreach (FurnitureData d in catalog)
            {
                if (d == null) continue;
                GameObject row = NewRect("Row_" + d.displayName, window.transform, new Vector2(840, rowH), new Vector2(0, y));
                row.AddComponent<Image>().color = RowColor;
                string type = d.displayType == "Fresh" ? "Fresh / cold products" : d.displayType == "Storage" ? "Box storage" : "Dry products";
                Text(row.transform, $"<b>{d.displayName}</b>", 26, new Vector2(280, rowH), new Vector2(-270, 0), TextAlignmentOptions.Left, false);
                Text(row.transform, $"<size=70%>Lv {d.requiredLevel}  ·  {type}</size>", 26, new Vector2(260, rowH), new Vector2(-10, 0), TextAlignmentOptions.Left, false);
                TMP_Text status = Text(row.transform, "", 24, new Vector2(200, rowH), new Vector2(210, 0), TextAlignmentOptions.Center, true);
                FurnitureData captured = d;
                Button buy = Btn(row.transform, "Buy", new Vector2(100, 46), new Vector2(365, 0), () => Buy(captured), new Color(0.2f, 0.7f, 0.3f));
                rows.Add((d, status, buy));
                y -= rowH + gap;
            }
        statusText = Text(window.transform, "", 24, new Vector2(840, 40), new Vector2(0, -h / 2 + 40), TextAlignmentOptions.Center, false);
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
