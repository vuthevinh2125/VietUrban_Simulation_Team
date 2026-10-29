using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Hàm dựng giao diện bằng code dùng chung (canvas phủ màn hình, khung, chữ, nút).</summary>
public static class UIKit
{
    public static readonly Color Window = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    public static readonly Color Header = new Color(0.0f, 0.6f, 0.95f, 1f);
    public static readonly Color Button = new Color(0.25f, 0.3f, 0.38f, 1f);
    public static readonly Color Green = new Color(0.2f, 0.7f, 0.3f, 1f);
    public static readonly Color Red = new Color(0.8f, 0.25f, 0.2f, 1f);

    public static Canvas CreateCanvas(Transform parent, int sortingOrder)
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static GameObject Rect(string name, Transform parent, Vector2 size = default, Vector2 pos = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return go;
    }

    public static GameObject FullScreen(string name, Transform parent, Color color)
    {
        GameObject go = Rect(name, parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        return go;
    }

    public static GameObject Panel(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = Rect(name, parent, size, pos);
        go.AddComponent<Image>().color = color;
        return go;
    }

    public static TMP_Text Text(Transform parent, string text, float size, Vector2 box, Vector2 pos,
                                TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, Color? color = null)
    {
        GameObject go = Rect("Text", parent, box, pos);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color ?? Color.white;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    public static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color? color = null, float fontSize = 28)
    {
        GameObject go = Rect("Button_" + label, parent, size, pos);
        go.AddComponent<Image>().color = color ?? Button;
        Button b = go.AddComponent<Button>();
        if (onClick != null) b.onClick.AddListener(onClick);
        Text(go.transform, label, fontSize, size, Vector2.zero, TextAlignmentOptions.Center, true);
        return b;
    }

    public static void SetLabel(Button b, string label)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = label;
    }
}
