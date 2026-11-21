using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Chụp ảnh model 3D thật trong game để làm hình trên giao diện: xe trong tab XE, ảnh thẻ nhân viên trong tab NHÂN VIÊN.
/// Cách làm: nhân bản model (xe trong scene hoặc prefab nhân vật) dưới một gốc đang tắt, gỡ hết script / va chạm / âm thanh /
/// camera / đèn trước khi bật lên (để bản sao không chạy gì), đặt ở xa phía dưới bản đồ trên một layer riêng, chụp bằng
/// camera tạm chỉ thấy layer đó, rồi xoá bản sao. Mỗi ảnh chỉ chụp một lần rồi giữ lại (RenderTexture).
/// </summary>
public static class ModelSnapshot
{
    public enum Framing { Vehicle, Portrait }

    private const int Layer = 31;                                     // layer trống (ProjectSettings/TagManager)
    private static readonly Vector3 Stage = new Vector3(0f, -600f, 0f);
    private static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();

    /// <summary>Ảnh của model (chụp lần đầu, sau đó lấy lại từ bộ nhớ). null nếu không chụp được.</summary>
    public static Texture Get(string key, GameObject source, Framing framing, int width, int height, Color background)
    {
        if (source == null || width <= 0 || height <= 0) return null;
        if (cache.TryGetValue(key, out RenderTexture rt) && rt != null && rt.IsCreated()) return rt;
        rt = Capture(source, framing, width, height, background);
        if (rt != null) cache[key] = rt;
        return rt;
    }

    /// <summary>Đặt ảnh phủ kín một khung giao diện.</summary>
    public static RawImage Show(RectTransform parent, Texture texture)
    {
        var go = new GameObject("Snapshot", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RawImage img = go.AddComponent<RawImage>();
        img.texture = texture;
        img.raycastTarget = false;
        return img;
    }

    private static RenderTexture Capture(GameObject source, Framing framing, int width, int height, Color background)
    {
        var holder = new GameObject("_SnapshotHolder");
        holder.SetActive(false);
        GameObject camGo = null, keyGo = null, fillGo = null;
        RenderTexture rt = null;
        try
        {
            GameObject clone = Object.Instantiate(source, holder.transform);
            clone.SetActive(true);                                    // gốc giữ đang tắt nên chưa có gì chạy
            Strip(clone);
            clone.transform.localScale = source.transform.lossyScale;
            clone.transform.SetPositionAndRotation(Stage, Quaternion.identity);
            SetLayer(clone.transform);
            holder.SetActive(true);

            foreach (Animator a in clone.GetComponentsInChildren<Animator>())
            {
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.Rebind();
                a.Update(0f);                                         // tư thế đứng đầu tiên thay vì tư thế chữ T
            }

            if (!TryBounds(clone, out Bounds b)) return null;
            if (framing == Framing.Portrait)
            {
                // Ảnh thẻ: đầu và vai (phần trên ~45% chiều cao), không lấy tay dang ngang
                float hgt = b.size.y;
                b = new Bounds(new Vector3(b.center.x, b.max.y - hgt * 0.2f, b.center.z),
                               new Vector3(Mathf.Min(b.size.x, hgt * 0.3f), hgt * 0.42f, Mathf.Min(b.size.z, hgt * 0.25f)));
            }

            Vector3 toCam = framing == Framing.Vehicle ? new Vector3(-0.85f, 0.42f, 1f).normalized : new Vector3(0.22f, 0.06f, 1f).normalized;
            camGo = new GameObject("_SnapshotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = framing == Framing.Vehicle ? 24f : 20f;
            cam.aspect = width / (float)height;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            cam.allowHDR = false;
            cam.transform.rotation = Quaternion.LookRotation(-toCam);
            float dist = FitDistance(cam, b, framing == Framing.Vehicle ? 0.86f : 0.95f);
            cam.transform.position = b.center + toCam * dist;

            // Đèn tạm: đèn chính chéo từ trên-trái phía trước, đèn phụ từ bên phải (không đổ bóng)
            keyGo = MakeLight("_SnapshotKey", -(toCam + Vector3.up * 1.1f - cam.transform.right * 0.7f), 1.5f, new Color(1f, 0.97f, 0.92f));
            fillGo = MakeLight("_SnapshotFill", -(toCam * 0.2f + Vector3.up * 0.3f + cam.transform.right * 1f), 0.55f, new Color(0.85f, 0.9f, 1f));

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Snapshot_" + source.name, antiAliasing = 4 };
            rt.Create();
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, request)) { request.destination = rt; RenderPipeline.SubmitRenderRequest(cam, request); }
            else cam.Render();
            cam.targetTexture = null;
            return rt;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Snapshot] Could not capture {source.name}: {ex.Message}");
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
            return null;
        }
        finally
        {
            Object.DestroyImmediate(holder);
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (keyGo != null) Object.DestroyImmediate(keyGo);
            if (fillGo != null) Object.DestroyImmediate(fillGo);
        }
    }

    /// <summary>Gỡ mọi thứ không phải hình: script, va chạm, vật lý, âm thanh, camera, đèn, hạt... (gỡ từ cuối lên để thành phần phụ thuộc đi trước).</summary>
    private static void Strip(GameObject root)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            Component[] all = root.GetComponentsInChildren<Component>(true);
            bool removed = false;
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Component c = all[i];
                if (c == null || Keep(c)) continue;
                if (pass < 2 && !(c is MonoBehaviour) && HasMonoBehaviour(c.gameObject)) continue;   // script trước, phần còn lại sau
                Object.DestroyImmediate(c);
                removed = true;
            }
            if (!removed && pass > 0) break;
        }
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
        }
    }

    private static bool Keep(Component c) =>
        c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is Animator || c is LODGroup;

    private static bool HasMonoBehaviour(GameObject g) => g.GetComponent<MonoBehaviour>() != null;

    private static void SetLayer(Transform t)
    {
        t.gameObject.layer = Layer;
        foreach (Transform c in t) SetLayer(c);
    }

    private static bool TryBounds(GameObject root, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r.bounds.size.sqrMagnitude < 1e-6f) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    /// <summary>Khoảng cách camera sao cho cả 8 góc của khối nằm trong khung hình (margin: phần khung được dùng).</summary>
    private static float FitDistance(Camera cam, Bounds b, float margin)
    {
        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * margin;
        float tanH = tanV * cam.aspect;
        Vector3 f = cam.transform.forward, r = cam.transform.right, u = cam.transform.up;
        float d = 0.5f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, (i & 2) == 0 ? -b.extents.y : b.extents.y, (i & 4) == 0 ? -b.extents.z : b.extents.z);
            float depthOffset = Vector3.Dot(p, f);
            d = Mathf.Max(d, Mathf.Abs(Vector3.Dot(p, r)) / tanH - depthOffset, Mathf.Abs(Vector3.Dot(p, u)) / tanV - depthOffset);
        }
        return d;
    }

    private static GameObject MakeLight(string name, Vector3 direction, float intensity, Color color)
    {
        var go = new GameObject(name);
        go.transform.rotation = Quaternion.LookRotation(direction.normalized);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = intensity;
        l.color = color;
        l.shadows = LightShadows.None;
        return go;
    }
}
