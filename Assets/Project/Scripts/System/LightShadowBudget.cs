using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Giới hạn số đèn phụ (point / spot) được đổ bóng cùng lúc.
/// Cảnh có rất nhiều đèn đổ bóng (đèn trong cửa hàng, đèn pha của xe và xe giao thông...) làm URP báo liên tục
/// "Reduced additional punctual light shadows resolution ... to make N shadow maps fit in the shadow atlas" và tốn hiệu năng.
/// Script này chỉ giữ bóng cho vài đèn gần camera nhất (vốn có bật bóng), các đèn còn lại vẫn sáng nhưng không đổ bóng.
/// Đèn mặt trời / mặt trăng (directional) không bị ảnh hưởng.
/// </summary>
public class LightShadowBudget : MonoBehaviour
{
    [Tooltip("Số đèn point / spot tối đa được đổ bóng cùng lúc (point light tốn 6 ô bóng, spot 1 ô)")]
    public int maxShadowLights = 4;
    [Tooltip("Đèn xa camera hơn khoảng này thì không đổ bóng")]
    public float maxShadowDistance = 25f;

    private static LightShadowBudget instance;
    private readonly Dictionary<Light, LightShadows> original = new Dictionary<Light, LightShadows>();
    private readonly List<Light> candidates = new List<Light>();
    private float nextScan, nextUpdate;

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
        if (instance == null) instance = new GameObject("LightShadowBudget").AddComponent<LightShadowBudget>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            // Quét toàn scene khá tốn (tạo mảng mới, có thể gây khựng); đèn mới hiếm khi xuất hiện nên 10 giây quét một lần là đủ
            nextScan = Time.unscaledTime + 10f;
            Scan();
        }
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.4f;
        Assign();
    }

    /// <summary>Ghi nhận các đèn phụ có bật bóng (giữ lại kiểu bóng gốc để bật lại khi tới gần).</summary>
    private void Scan()
    {
        candidates.RemoveAll(l => l == null);
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            if (original.ContainsKey(l)) continue;
            if (l.shadows == LightShadows.None) { original[l] = LightShadows.None; continue; }
            original[l] = l.shadows;
            candidates.Add(l);
            l.shadows = LightShadows.None; // tắt trước, Assign() sẽ bật lại cho đèn gần
        }
    }

    private void Assign()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        float maxSqr = maxShadowDistance * maxShadowDistance;

        candidates.RemoveAll(l => l == null);
        candidates.Sort((a, b) => (a.transform.position - c).sqrMagnitude.CompareTo((b.transform.position - c).sqrMagnitude));
        int given = 0;
        foreach (Light l in candidates)
        {
            bool on = given < maxShadowLights && l.isActiveAndEnabled && (l.transform.position - c).sqrMagnitude <= maxSqr;
            LightShadows want = on ? original[l] : LightShadows.None;
            if (l.shadows != want) l.shadows = want;
            if (on) given++;
        }
    }
}
