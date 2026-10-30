using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sắp xếp lại thanh trên cùng của màn hình máy tính (MARKET) cho gọn và đều:
///  - Hàng tab: các tab cùng kích thước, cách đều nhau, icon bên trái + chữ căn giữa,
///    thứ tự: Products · Furniture · Staff · Expand · Licenses · Vehicles · Stats; nút Buy (xanh) tách riêng bên phải.
///  - Hàng lọc: ô tìm kiếm thẳng mép trái với tab đầu, bộ lọc Category thẳng mép phải với nút Buy, Display đứng ngay trước Category.
///  - Nút đóng (X) thẳng mép phải.
/// Tự gắn vào object có ShopCatalog (ComputerUI) khi vào game; chạy lại mỗi lần mở máy tính.
/// </summary>
public class ComputerLayout : MonoBehaviour
{
    private const float Margin = 12f;   // lề trái / phải trong hàng
    private const float Gap = 8f;       // khoảng cách giữa các tab
    private const float TabH = 30f;
    private const float BuyW = 132f, BuyH = 34f;

    private static readonly string[] Order = { "product", "furniture", "staff", "expand", "license", "vehicle", "stats" };
    private static Sprite chartIcon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        AttachAll();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => AttachAll();

    private static void AttachAll()
    {
        foreach (ShopCatalog c in Object.FindObjectsByType<ShopCatalog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.GetComponent<ComputerLayout>() == null) c.gameObject.AddComponent<ComputerLayout>();
    }

    void OnEnable() => StartCoroutine(ApplyLater());

    private IEnumerator ApplyLater()
    {
        // Đợi ShopCatalog thêm tab Expand / Stats và layout của Unity tính xong kích thước
        yield return null;
        yield return null;
        try { Apply(); }
        catch (System.Exception e) { Debug.LogException(e, this); }
    }

    public void Apply()
    {
        Canvas.ForceUpdateCanvases();
        RectTransform middle = FindRect("Middle_Row");
        RectTransform bottom = FindRect("Bottom_Row");
        RectTransform top = FindRect("Top_Row");
        if (middle != null) LayoutTabs(middle);
        if (bottom != null) LayoutFilters(bottom);
        if (top != null) LayoutTop(top);
    }

    // ================= Hàng tab =================
    private void LayoutTabs(RectTransform row)
    {
        float w = row.rect.width;
        Button buy = null;
        var tabs = new List<Button>();
        foreach (Transform t in row)
        {
            if (!t.gameObject.activeSelf) continue;
            Button b = t.GetComponent<Button>();
            if (b == null) continue;
            if (t.name.Contains("Checkout") || Label(b).ToLowerInvariant().Contains("buy")) buy = b;
            else tabs.Add(b);
        }
        tabs.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

        float right = w / 2f - Margin;
        if (buy != null)
        {
            Place(buy, right - BuyW / 2f, BuyW, BuyH);
            StyleContent(buy, 26f, 20f);
            right -= BuyW + Gap * 3f; // tách nút Buy ra xa các tab
        }
        float left = -w / 2f + Margin;
        int n = tabs.Count;
        if (n == 0) return;
        float tabW = (right - left - Gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            Button b = tabs[i];
            Place(b, left + tabW / 2f + i * (tabW + Gap), tabW, TabH);
            if (Label(b).ToLowerInvariant().Contains("furniture")) SetLabel(b, "Furniture");
            if (b.name == "Button_Stats") RestoreStatsIcon(b);
            StyleContent(b, 22f, 15f);
        }
    }

    private static int Rank(Button b)
    {
        string l = Label(b).ToLowerInvariant();
        for (int i = 0; i < Order.Length; i++) if (l.Contains(Order[i])) return i;
        return Order.Length;
    }

    /// <summary>Đặt nút vào giữa hàng theo toạ độ x (gốc giữa hàng), tắt các layout tự động đang giành quyền xếp.</summary>
    private static void Place(Button b, float x, float width, float height)
    {
        DisableAutoLayout(b.gameObject);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, 0f);
    }

    /// <summary>Icon sát trái, chữ căn giữa phần còn lại, cùng cỡ chữ cho mọi tab.</summary>
    private static void StyleContent(Button b, float iconSize, float maxFont)
    {
        var rt = (RectTransform)b.transform;
        Image icon = null;
        foreach (Transform c in b.transform)
        {
            Image img = c.GetComponent<Image>();
            if (img != null && c.GetComponentInChildren<TMP_Text>(true) == null) { icon = img; break; }
        }
        float textLeft = 8f;
        if (icon != null && icon.gameObject.activeSelf)
        {
            DisableAutoLayout(icon.gameObject);
            var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(iconSize, iconSize);
            ir.anchoredPosition = new Vector2(8f + iconSize / 2f, 0f);
            icon.preserveAspect = true;
            textLeft = 12f + iconSize;
        }
        TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            DisableAutoLayout(label.gameObject);
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(textLeft, 2f);
            lr.offsetMax = new Vector2(-8f, -2f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = maxFont;
            label.fontStyle |= FontStyles.Bold;
        }
    }

    private static void RestoreStatsIcon(Button b)
    {
        foreach (Transform c in b.transform)
        {
            Image img = c.GetComponent<Image>();
            if (img == null || c.GetComponentInChildren<TMP_Text>(true) != null) continue;
            c.gameObject.SetActive(true);
            img.sprite = ChartIcon;
            img.color = Color.white;
            return;
        }
    }

    // ================= Hàng lọc =================
    private void LayoutFilters(RectTransform row)
    {
        float w = row.rect.width;
        RectTransform search = Child(row, "Search_input");
        RectTransform display = Child(row, "Filter_Display");
        RectTransform category = Child(row, "Filter_Category");

        if (search != null)
        {
            DisableAutoLayout(search.gameObject);
            search.anchorMin = search.anchorMax = search.pivot = new Vector2(0.5f, 0.5f);
            search.sizeDelta = new Vector2(300f, search.sizeDelta.y);
            search.anchoredPosition = new Vector2(-w / 2f + Margin + 150f, 0f);
        }
        // Category sát mép phải, Display đứng ngay trước nó (giữ nguyên nhãn + dropdown bên trong mỗi nhóm)
        float edge = w / 2f - Margin;
        if (category != null)
        {
            Rect r = GroupRect(row, category);
            Shift(category, edge - r.xMax, -r.center.y);
            edge = r.xMin + (edge - r.xMax) - 36f;
        }
        if (display != null)
        {
            Rect r = GroupRect(row, display);
            Shift(display, edge - r.xMax, -r.center.y);
        }
    }

    // ================= Hàng tiêu đề =================
    private void LayoutTop(RectTransform row)
    {
        foreach (Transform t in row)
        {
            Button b = t.GetComponent<Button>();
            if (b == null || !t.gameObject.activeSelf) continue; // nút đóng (X)
            Rect r = GroupRect(row, (RectTransform)t);
            Shift((RectTransform)t, (row.rect.width / 2f - Margin) - r.xMax, 0f);
        }
    }

    // ================= Tiện ích =================
    private static void Shift(RectTransform rt, float dx, float dy)
    {
        DisableAutoLayout(rt.gameObject);
        rt.anchoredPosition += new Vector2(dx, dy);
    }

    /// <summary>Khung bao (trong toạ độ của hàng, gốc ở giữa) của một nhóm và mọi con đang hiện.</summary>
    private static Rect GroupRect(RectTransform row, RectTransform group)
    {
        var corners = new Vector3[4];
        float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
        foreach (RectTransform r in group.GetComponentsInChildren<RectTransform>(false))
        {
            if (r.rect.width <= 0.5f || r.rect.height <= 0.5f) continue;
            r.GetWorldCorners(corners);
            foreach (Vector3 c in corners)
            {
                Vector3 p = row.InverseTransformPoint(c);
                p.x -= row.rect.center.x; p.y -= row.rect.center.y;
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
            }
        }
        if (xMin > xMax) return new Rect(0, 0, 0, 0);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static void DisableAutoLayout(GameObject go)
    {
        foreach (ContentSizeFitter f in go.GetComponents<ContentSizeFitter>()) f.enabled = false;
        foreach (LayoutGroup g in go.GetComponents<LayoutGroup>()) g.enabled = false;
        Transform parent = go.transform.parent;
        if (parent != null)
            foreach (LayoutGroup g in parent.GetComponents<LayoutGroup>()) g.enabled = false;
    }

    private RectTransform FindRect(string name)
    {
        foreach (RectTransform r in GetComponentsInChildren<RectTransform>(true))
            if (r.name == name) return r;
        return null;
    }

    private static RectTransform Child(RectTransform row, string name)
    {
        Transform t = row.Find(name);
        return t != null && t.gameObject.activeSelf ? (RectTransform)t : null;
    }

    private static string Label(Button b)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) return t.text ?? "";
        Text legacy = b.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text ?? "" : "";
    }

    private static void SetLabel(Button b, string text)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = text;
    }

    /// <summary>Icon biểu đồ cột (vẽ bằng code) cho tab Stats.</summary>
    private static Sprite ChartIcon
    {
        get
        {
            if (chartIcon != null) return chartIcon;
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            Color32 bar1 = new Color32(110, 220, 120, 255), bar2 = new Color32(255, 205, 70, 255), bar3 = new Color32(90, 190, 255, 255);
            void Fill(int x0, int x1, int h, Color32 c) { for (int y = 3; y < 3 + h; y++) for (int x = x0; x < x1; x++) px[y * S + x] = c; }
            Fill(4, 11, 11, bar1);
            Fill(13, 20, 19, bar2);
            Fill(22, 29, 26, bar3);
            for (int x = 2; x < 31; x++) { px[1 * S + x] = new Color32(255, 255, 255, 255); px[2 * S + x] = new Color32(255, 255, 255, 255); }
            tex.SetPixels32(px);
            tex.Apply();
            chartIcon = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return chartIcon;
        }
    }
}
