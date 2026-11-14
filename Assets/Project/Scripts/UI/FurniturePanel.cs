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

    private RectTransform content;

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
        headerInfo.text = "Buy here, then pick up the box outside and press <b><color=#F1F3F6>LMB</color></b> to place it. <b><color=#F1F3F6>R</color></b> rotates.";
        foreach (var r in rows)
        {
            string reason = null;
            if (StoreProgress.Level < r.data.requiredLevel) reason = $"Needs store Lv {r.data.requiredLevel}";
            else if (money < r.data.price) reason = "Not enough money";
            r.status.text = reason == null ? $"Lv {r.data.requiredLevel} · {TypeName(r.data)}" : $"<color=#FF9F8A>{reason}</color>";
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

    // ================= Dựng giao diện (bản thiết kế 08: thẻ nội thất) =================
    private void BuildFrame()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        content = ShopPC.Computer(canvas, "FURNITURE", Close, out root);
        headerInfo = ShopPC.Title(content, "FURNITURE", "", out _);
        statusText = ShopPC.Text(content, "", ShopUI.Hand, 30, 0f, ShopPC.ContentSize.y - 50f, ShopPC.ContentSize.x, 50f, ShopPC.Gold, TextAlignmentOptions.Left);
        root.SetActive(false);
    }

    private static string TypeName(FurnitureData d) =>
        d.displayType == "Fresh" ? "fresh / cold products" : d.displayType == "Storage" ? "box storage" : "dry products";

    private void BuildRows(List<FurnitureData> catalog)
    {
        built = catalog;
        rows.Clear();
        if (window != null) Destroy(window);
        window = ShopPC.Group("Cards", content, 0f, 0f, ShopPC.ContentSize.x, ShopPC.ContentSize.y).gameObject;
        Transform t = window.transform;

        int n = catalog != null ? catalog.Count : 0;
        const int cols = 4;
        const float gap = 21f, top = 84f;
        int rowsN = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
        float w = (ShopPC.ContentSize.x - gap * (cols - 1)) / cols;
        float h = Mathf.Min(330f, (ShopPC.ContentSize.y - top - 60f - gap * (rowsN - 1)) / rowsN);
        int i = 0;
        if (catalog != null)
            foreach (FurnitureData d in catalog)
            {
                if (d == null) continue;
                float x = (i % cols) * (w + gap), y = top + (i / cols) * (h + gap);
                RectTransform card = ShopPC.Box("Card_" + d.displayName, t, x, y, w, h, ShopPC.Card);
                card.gameObject.AddComponent<UIMotion>().dropIn = 24f;
                ShopUI.Frame(card, card.sizeDelta, ShopPC.A(Color.white, 0.08f), 1.5f);
                float artH = Mathf.Min(144f, h * 0.45f);
                RectTransform art = ShopPC.Box("Art", card, 0f, 0f, w, artH, ShopPC.CardArt);
                if (d.icon != null)
                {
                    RectTransform ic = ShopPC.Box("Icon", art, w * 0.5f - artH * 0.45f, artH * 0.05f, artH * 0.9f, artH * 0.9f, Color.white);
                    Image img = ic.GetComponent<Image>(); img.sprite = d.icon; img.preserveAspect = true;
                }
                else Shape(art, d, artH);
                ShopPC.PriceTag(art, EconomyManager.Format(d.price), ShopPC.At(art.sizeDelta, w - 12f - 110f, artH - 22f, 110f, 40f), 28f, -3f);
                ShopPC.Text(card, d.displayName, ShopUI.Body, 22, 18f, artH + 16f, w - 36f, 32f, ShopPC.Light, TextAlignmentOptions.Left, 0f, true);
                TMP_Text status = ShopPC.Text(card, "", ShopUI.Body, 17, 18f, artH + 50f, w - 36f, 26f, ShopPC.Dim, TextAlignmentOptions.Left);
                FurnitureData captured = d;
                Button buy = ShopPC.Btn(card, "BUY", 18f, h - 18f - 51f, w - 36f, 51f, () => Buy(captured), ShopPC.Kind.Gold, 22f);
                rows.Add((d, status, buy));
                i++;
            }
    }

    /// <summary>Hình khối đơn giản theo loại nội thất (khi chưa có icon).</summary>
    private static void Shape(RectTransform art, FurnitureData d, float artH)
    {
        string n = (d.displayName ?? "").ToLowerInvariant();
        Vector2 size; Color c;
        if (n.Contains("freez")) { size = new Vector2(144f, 66f); c = ShopPC.Hex(0xDDE6EE); }
        else if (n.Contains("fridge") || d.displayType == "Fresh") { size = new Vector2(90f, 120f); c = ShopPC.Hex(0xBFD6E8); }
        else if (n.Contains("checkout") || n.Contains("counter")) { size = new Vector2(165f, 60f); c = ShopPC.Hex(0x6B5A4A); }
        else if (n.Contains("produce") || n.Contains("fruit") || n.Contains("veg")) { size = new Vector2(150f, 75f); c = ShopPC.Hex(0x4FA36A); }
        else if (n.Contains("bin") || n.Contains("trash")) { size = new Vector2(51f, 66f); c = ShopPC.Hex(0x3E7BC0); }
        else if (d.displayType == "Storage" || n.Contains("rack")) { size = new Vector2(126f, 114f); c = ShopPC.Hex(0x7A808A); }
        else { size = new Vector2(135f, 105f); c = ShopPC.Hex(0x8A6A3A); }
        size *= Mathf.Min(1f, (artH - 30f) / 120f);
        RectTransform b = ShopUI.Box("Shape", art, size, new Vector2(0f, -artH * 0.5f + 15f + size.y * 0.5f), c);
        ShopUI.Frame(b, size - new Vector2(4f, 4f), ShopPC.A(Color.white, 0.18f), 6f);
        ShopUI.Box("Shade", b, new Vector2(size.x, 12f), new Vector2(0f, -size.y * 0.5f + 6f), ShopPC.A(Color.black, 0.25f));
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
        t.font = ShopUI.Body;
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
