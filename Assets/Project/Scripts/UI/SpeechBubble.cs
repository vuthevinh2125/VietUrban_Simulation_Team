using TMPro;
using UnityEngine;

/// <summary>Dòng chữ nổi trên đầu khách ("Too expensive!", "Great price!"), luôn quay về camera, tự biến mất.</summary>
public class SpeechBubble : MonoBehaviour
{
    private Transform target;
    private float height;
    private float endTime;
    private TextMeshPro tmp;
    private static readonly System.Collections.Generic.List<SpeechBubble> Active = new System.Collections.Generic.List<SpeechBubble>();

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public static void Show(Transform target, string text, Color color, float height = 2.1f, float duration = 2.5f)
    {
        if (target == null) return;
        for (int i = Active.Count - 1; i >= 0; i--)
            if (Active[i] == null || Active[i].target == target) { if (Active[i] != null) Destroy(Active[i].gameObject); Active.RemoveAt(i); }

        GameObject go = new GameObject("SpeechBubble"); // không làm con của khách để khỏi bị méo theo scale nhân vật
        SpeechBubble b = go.AddComponent<SpeechBubble>();
        b.target = target;
        b.height = height;
        b.endTime = Time.time + duration;
        b.tmp = go.AddComponent<TextMeshPro>();
        b.tmp.text = text;
        b.tmp.color = color;
        b.tmp.alignment = TextAlignmentOptions.Center;
        b.tmp.fontSize = 2.5f;
        b.tmp.fontStyle = FontStyles.Bold;
        b.tmp.outlineWidth = 0.25f;
        b.tmp.outlineColor = Color.black;
        b.tmp.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        b.LateUpdate();
    }

    void LateUpdate()
    {
        if (target == null || Time.time > endTime) { Destroy(gameObject); return; }
        transform.position = target.position + Vector3.up * (height + Mathf.Sin(Time.time * 3f) * 0.03f);
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
