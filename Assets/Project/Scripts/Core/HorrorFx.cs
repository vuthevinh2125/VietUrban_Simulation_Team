using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng máu me cho các sự kiện kinh dị (tạo hết bằng code):
///  - Texture vết máu bắn (loang + giọt chảy), dấu bàn tay máu, vết kéo lê.
///  - Decal: tấm phẳng dán vết máu lên tường / kính / mặt đường (Sprites/Default: trong suốt, không cần ánh sáng).
///  - Chữ viết bằng máu (TextMeshPro 3D đỏ thẫm + vệt chảy dưới chữ).
///  - Máu bắn lên màn hình (lớp UI mờ dần), mưa giọt máu (hạt rơi).
/// </summary>
public static class HorrorFx
{
    private static Texture2D splat, hand, drag, screen;
    private static Mesh sphere;
    public static readonly Color Blood = new Color(0.42f, 0.02f, 0.02f), BloodWet = new Color(0.58f, 0.03f, 0.03f);

    private static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 2147483647; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }

    /// <summary>Vết máu bắn: lõi loang + nhiều giọt văng + vài vệt chảy dài xuống dưới.</summary>
    public static Texture2D Splat()
    {
        if (splat != null) return splat;
        const int N = 256;
        var a = new float[N * N];
        var rnd = new System.Random(7);
        void Blob(float cx, float cy, float r, float k)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 2)), x1 = Mathf.Min(N - 1, (int)(cx + r + 2)), y0 = Mathf.Max(0, (int)(cy - r - 2)), y1 = Mathf.Min(N - 1, (int)(cy + r + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d < 1f) a[y * N + x] = Mathf.Max(a[y * N + x], k * Mathf.Clamp01((1f - d) * 4f));
                }
        }
        for (int i = 0; i < 26; i++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = (float)rnd.NextDouble() * 40f;
            Blob(128 + Mathf.Cos(ang) * dist, 140 + Mathf.Sin(ang) * dist, 18f + (float)rnd.NextDouble() * 22f, 1f);
        }
        for (int i = 0; i < 60; i++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = 55f + (float)rnd.NextDouble() * 65f;
            Blob(128 + Mathf.Cos(ang) * dist, 140 + Mathf.Sin(ang) * dist, 1.5f + (float)rnd.NextDouble() * 6f, 1f);
        }
        for (int i = 0; i < 7; i++)   // vệt chảy xuống
        {
            float x = 80 + (float)rnd.NextDouble() * 96f, len = 40f + (float)rnd.NextDouble() * 80f, w = 3f + (float)rnd.NextDouble() * 4f;
            for (float y = 120; y > 120 - len && y > 2; y -= 1f) Blob(x + Mathf.Sin(y * 0.08f) * 1.5f, y, w * (0.6f + 0.4f * (y - (120 - len)) / len), 1f);
            Blob(x, Mathf.Max(4f, 120 - len), w * 1.3f, 1f);
        }
        splat = Bake(a, N, "BloodSplat");
        return splat;
    }

    /// <summary>Dấu bàn tay máu (lòng bàn tay + 5 ngón, mép nhoè, có vệt chảy).</summary>
    public static Texture2D Hand()
    {
        if (hand != null) return hand;
        const int N = 128;
        var a = new float[N * N];
        void Ell(float cx, float cy, float rx, float ry, float rot)
        {
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float u = (dx * c + dy * s) / rx, v = (-dx * s + dy * c) / ry;
                    float d = u * u + v * v;
                    if (d < 1f) a[y * N + x] = Mathf.Max(a[y * N + x], Mathf.Clamp01((1f - d) * 3f) * (0.75f + 0.25f * Hash(x, y, 3)));
                }
        }
        Ell(64, 48, 26, 30, 0f);                                   // lòng bàn tay
        Ell(34, 58, 7, 18, 0.9f);                                   // ngón cái
        float[] fx = { 46, 58, 70, 82 }, fl = { 24, 29, 28, 21 };
        for (int i = 0; i < 4; i++) Ell(fx[i] + (i - 1.5f) * 2f, 78 + fl[i] * 0.5f + 4, 6.5f, fl[i] * 0.55f, (i - 1.5f) * -0.08f);
        for (int i = 0; i < 4; i++)                                 // vệt chảy
        {
            float x = 48 + i * 10;
            for (int y = 20; y > 2; y--) a[y * N + (int)x] = Mathf.Max(a[y * N + (int)x], 0.8f);
        }
        hand = Bake(a, N, "BloodHand");
        return hand;
    }

    /// <summary>Vệt kéo lê dài (dùng trên mặt đường).</summary>
    public static Texture2D Drag()
    {
        if (drag != null) return drag;
        const int N = 128;
        var a = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cx = 64 + Mathf.Sin(y * 0.07f) * 6f;
                float w = 22f * (0.55f + 0.45f * Mathf.PerlinNoise(y * 0.05f, 3.1f));
                float d = Mathf.Abs(x - cx) / w;
                float streak = 0.6f + 0.4f * Mathf.PerlinNoise(x * 0.4f, y * 0.02f);
                if (d < 1f) a[y * N + x] = Mathf.Clamp01((1f - d) * 2.5f) * streak * (y > N - 10 ? (N - y) / 10f : 1f);
            }
        drag = Bake(a, N, "BloodDrag");
        return drag;
    }

    private static Texture2D Bake(float[] a, int n, string name)
    {
        var px = new Color32[n * n];
        for (int i = 0; i < px.Length; i++)
        {
            float v = a[i];
            int x = i % n, y = i / n;
            float dark = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
            px[i] = new Color(Blood.r * dark + 0.12f * v * v, Blood.g * dark, Blood.b * dark, Mathf.Clamp01(v * 1.15f));
        }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        t.SetPixels32(px); t.Apply(true);
        return t;
    }

    private static Material DecalMaterial(Texture tex)
    {
        Shader sh = Shader.Find("Sprites/Default");
        var m = new Material(sh != null ? sh : Shader.Find("Unlit/Transparent")) { name = "BloodDecal_" + tex.name, hideFlags = HideFlags.DontSave };
        m.mainTexture = tex;
        m.color = Color.white;
        m.renderQueue = 3001;
        return m;
    }

    private static readonly Dictionary<Texture, Material> mats = new Dictionary<Texture, Material>();

    /// <summary>Dán một vết máu phẳng (quad) tại điểm, mặt quay theo pháp tuyến; up = hướng "trên" của vết (vệt chảy hướng xuống).</summary>
    public static GameObject Decal(Texture2D tex, Vector3 pos, Vector3 normal, Vector3 up, Vector2 size, Transform parent, float alpha = 1f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "Blood_" + tex.name;
        Object.Destroy(g.GetComponent<Collider>());
        if (parent != null) g.transform.SetParent(parent, true);
        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        if (Mathf.Abs(Vector3.Dot(up.normalized, normal)) > 0.95f) up = Vector3.Cross(normal, Vector3.right);
        g.transform.position = pos + normal * 0.012f;
        g.transform.rotation = Quaternion.LookRotation(-normal, up);   // quad nhìn về -Z
        g.transform.localScale = new Vector3(size.x, size.y, 1f);
        if (!mats.TryGetValue(tex, out Material m) || m == null) { m = DecalMaterial(tex); mats[tex] = m; }
        Renderer r = g.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (alpha < 0.999f) { var inst = new Material(m); inst.color = new Color(1f, 1f, 1f, alpha); r.sharedMaterial = inst; }
        else r.sharedMaterial = m;
        return g;
    }

    /// <summary>Vũng máu trên mặt đất.</summary>
    public static GameObject Pool(Vector3 at, float size, Transform parent) =>
        Decal(Splat(), at, Vector3.up, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward, new Vector2(size, size), parent);

    /// <summary>Chữ viết bằng máu (3D) + các vệt chảy dưới chữ.</summary>
    public static GameObject Writing(string text, Vector3 pos, Vector3 normal, float height, Transform parent, Vector3? textUp = null)
    {
        var g = new GameObject("BloodWriting");
        if (parent != null) g.transform.SetParent(parent, true);
        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.forward;
        Vector3 upV = textUp ?? Vector3.up;                              // viết dưới sàn: "trên" của chữ = hướng ra xa người đọc
        if (Mathf.Abs(Vector3.Dot(upV.normalized, normal)) > 0.95f) upV = Vector3.Cross(normal, Vector3.right);
        upV = Vector3.ProjectOnPlane(upV, normal).normalized;
        g.transform.SetPositionAndRotation(pos + normal * 0.015f, Quaternion.LookRotation(-normal, upV));
        var t = g.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = height * 10f;
        t.color = BloodWet;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold | FontStyles.Italic;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(height * text.Length * 0.8f, height * 1.4f);
        // vệt chảy
        Vector3 right = Vector3.Cross(upV, normal).normalized;
        int drips = Mathf.Clamp(text.Length / 2, 3, 10);
        for (int i = 0; i < drips; i++)
        {
            float x = (i / (float)(drips - 1) - 0.5f) * height * text.Length * 0.55f + Random.Range(-0.05f, 0.05f);
            Decal(Splat(), pos + right * x - upV * (height * 0.7f), normal, upV, new Vector2(height * 0.5f, height * 1.1f), g.transform, 0.9f);
        }
        return g;
    }

    // ================= Máu bắn lên màn hình =================
    public static Texture2D ScreenSplat()
    {
        if (screen != null) return screen;
        const int W = 384, H = 216;
        var px = new Color32[W * H];
        var rnd = new System.Random(11);
        var a = new float[W * H];
        void Blob(float cx, float cy, float r)
        {
            for (int y = Mathf.Max(0, (int)(cy - r)); y < Mathf.Min(H, (int)(cy + r)); y++)
                for (int x = Mathf.Max(0, (int)(cx - r)); x < Mathf.Min(W, (int)(cx + r)); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d < 1f) a[y * W + x] = Mathf.Max(a[y * W + x], Mathf.Clamp01((1f - d) * 3f));
                }
        }
        for (int c = 0; c < 4; c++)
        {
            float cx = c % 2 == 0 ? 40 + (float)rnd.NextDouble() * 60 : W - 40 - (float)rnd.NextDouble() * 60, cy = 30 + (float)rnd.NextDouble() * (H - 60);
            for (int i = 0; i < 18; i++) Blob(cx + ((float)rnd.NextDouble() - 0.5f) * 70, cy + ((float)rnd.NextDouble() - 0.5f) * 50, 6 + (float)rnd.NextDouble() * 18);
            for (int i = 0; i < 4; i++) { float x = cx + ((float)rnd.NextDouble() - 0.5f) * 50; for (float y = cy; y > 0 && y > cy - 90; y -= 1) Blob(x, y, 2.5f); }
        }
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.55f, 0.02f, 0.02f, Mathf.Clamp01(a[i]) * 0.85f);
        screen = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "ScreenBlood", hideFlags = HideFlags.DontSave };
        screen.SetPixels32(px); screen.Apply();
        return screen;
    }

    /// <summary>Lớp máu phủ màn hình (trên canvas đoạn phim); trả về RawImage để làm mờ dần.</summary>
    public static RawImage ScreenBlood(Transform canvas)
    {
        var g = UIKit.Rect("ScreenBlood", canvas);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = g.AddComponent<RawImage>();
        img.texture = ScreenSplat();
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Máu nhỏ giọt (hạt đỏ rơi thẳng) tại một điểm.</summary>
    public static ParticleSystem Drips(Vector3 at, float rate, Transform parent)
    {
        var g = new GameObject("BloodDrips");
        if (parent != null) g.transform.SetParent(parent, true);
        g.transform.position = at;
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 2f; main.loop = true;
        main.startLifetime = 1.2f; main.startSpeed = 0f; main.gravityModifier = 1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // gắn vào xương model có tỉ lệ lạ vẫn đúng cỡ thật
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        if (sphere == null) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp); }
        r.mesh = sphere;
        r.sharedMaterial = RuntimeMaterials.Lit(BloodWet);
        ps.Play();
        return ps;
    }

    private static Texture2D softDot;

    /// <summary>Chấm tròn mềm (tâm đặc, viền mờ) cho hạt khói.</summary>
    public static Texture2D SoftDot()
    {
        if (softDot != null) return softDot;
        const int N = 64;
        softDot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot", hideFlags = HideFlags.DontSave };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        softDot.SetPixels32(px);
        softDot.Apply();
        return softDot;
    }

    /// <summary>Khói (hương / nhang, sương khói): hạt mềm bay lên, nở to, mờ dần. Gắn theo parent (có thể là xương tay).</summary>
    public static ParticleSystem Smoke(Vector3 at, Transform parent, float rate, float size, Color col, float rise = 0.35f, float life = 3.2f)
    {
        var g = new GameObject("Smoke");
        if (parent != null) g.transform.SetParent(parent, true);
        g.transform.SetPositionAndRotation(at, Quaternion.identity);
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 4f; main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = col;
        main.gravityModifier = -0.01f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = size * 0.15f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(rise * 0.7f, rise); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.6f; nz.scrollSpeed = 0.3f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.6f));
        var ro = ps.rotationOverLifetime; ro.enabled = true; ro.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var cl = ps.colorOverLifetime; cl.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
        cl.color = grad;
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = SmokeMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    private static Material smokeMat;

    private static Material SmokeMaterial()
    {
        if (smokeMat != null) return smokeMat;
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh != null)
        {
            smokeMat = new Material(sh) { name = "HorrorSmoke", hideFlags = HideFlags.DontSave };
            smokeMat.SetFloat("_Surface", 1f);                 // trong suốt
            smokeMat.SetFloat("_Blend", 0f);                   // alpha
            smokeMat.SetOverrideTag("RenderType", "Transparent");
            smokeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            smokeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            smokeMat.SetInt("_ZWrite", 0);
            smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            smokeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else smokeMat = RuntimeMaterials.Ghost(Color.white);
        if (smokeMat.HasProperty("_BaseMap")) smokeMat.SetTexture("_BaseMap", SoftDot());
        smokeMat.mainTexture = SoftDot();
        RuntimeMaterials.SetColor(smokeMat, Color.white);
        return smokeMat;
    }

    // ================= Tiền âm phủ (vàng mã) =================
    private static Texture2D hellTex;
    private static Mesh hellMesh;
    private static Material hellMat;

    /// <summary>Tờ tiền âm phủ: nền vàng úa, viền đỏ son hoa văn, ô chân dung Diêm Vương tối màu, chữ số lớn, con dấu đỏ.</summary>
    public static Texture2D HellNoteTexture()
    {
        if (hellTex != null) return hellTex;
        const int W = 160, H = 72;
        hellTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "HellNote", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * H];
        Color32 paper = new Color32(222, 196, 120, 255), red = new Color32(170, 28, 22, 255), green = new Color32(60, 110, 70, 255), dark = new Color32(60, 40, 30, 255), gold = new Color32(235, 190, 60, 255);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color32 c = paper;
                float n = Hash(x, y, 7) * 18f;
                c = new Color32((byte)Mathf.Clamp(c.r - n, 0, 255), (byte)Mathf.Clamp(c.g - n, 0, 255), (byte)Mathf.Clamp(c.b - n, 0, 255), 255);
                int bx = Mathf.Min(x, W - 1 - x), by = Mathf.Min(y, H - 1 - y), b = Mathf.Min(bx, by);
                if (b < 4) c = red;                                                  // viền đỏ
                else if (b < 7) c = ((x + y) / 3) % 2 == 0 ? gold : red;              // hoa văn
                else if (b < 9) c = green;
                float dx = (x - W * 0.5f) / 20f, dy = (y - H * 0.5f) / 24f;
                if (dx * dx + dy * dy < 1f) c = dx * dx + dy * dy < 0.75f ? dark : gold; // ô chân dung
                if (x > 16 && x < 44 && y > 14 && y < 58 && ((x / 5 + y / 7) % 3 == 0)) c = red;   // chữ số / chữ Hán (giả)
                if (x > 116 && x < 144 && y > 14 && y < 58 && ((x / 5 + y / 7) % 3 == 0)) c = red;
                float sx = (x - 128f) / 10f, sy = (y - 20f) / 10f;
                if (sx * sx + sy * sy < 1f && sx * sx + sy * sy > 0.55f) c = red;    // con dấu
                px[y * W + x] = c;
            }
        hellTex.SetPixels32(px);
        hellTex.Apply(true);
        return hellTex;
    }

    /// <summary>Tờ tiền mỏng hai mặt (16 x 7 cm), có UV in hình tiền âm phủ.</summary>
    public static Mesh HellNoteMesh()
    {
        if (hellMesh != null) return hellMesh;
        float w = 0.08f, h = 0.035f;
        hellMesh = new Mesh { name = "HellNote", hideFlags = HideFlags.DontSave };
        hellMesh.vertices = new[] { new Vector3(-w, 0f, -h), new Vector3(w, 0f, -h), new Vector3(w, 0f, h), new Vector3(-w, 0f, h),
                                    new Vector3(-w, 0f, -h), new Vector3(w, 0f, -h), new Vector3(w, 0f, h), new Vector3(-w, 0f, h) };
        hellMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        hellMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.down, Vector3.down, Vector3.down, Vector3.down };
        hellMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 };
        hellMesh.RecalculateBounds();
        return hellMesh;
    }

    public static Material HellNoteMaterial()
    {
        if (hellMat != null) return hellMat;
        hellMat = new Material(RuntimeMaterials.Lit(Color.white)) { name = "HellNote", hideFlags = HideFlags.DontSave };
        if (hellMat.HasProperty("_BaseMap")) hellMat.SetTexture("_BaseMap", HellNoteTexture());
        hellMat.mainTexture = HellNoteTexture();
        RuntimeMaterials.SetColor(hellMat, Color.white);
        return hellMat;
    }

    /// <summary>Rải tiền âm phủ nằm trên mặt đường / sàn (một khối gộp, xoay ngẫu nhiên).</summary>
    public static GameObject ScatterHellMoney(Vector3 center, float radius, int count, Transform parent)
    {
        var combine = new CombineInstance[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            combine[i].mesh = HellNoteMesh();
            combine[i].transform = Matrix4x4.TRS(new Vector3(r.x, 0.015f + i * 0.0004f, r.y), Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f)), Vector3.one * Random.Range(1.4f, 1.9f));
        }
        var m = new Mesh { name = "HellMoneyOnGround", hideFlags = HideFlags.DontSave };
        m.CombineMeshes(combine, true, true);
        var g = new GameObject("HellMoney");
        if (parent != null) g.transform.SetParent(parent, true);
        g.transform.position = center;
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().sharedMaterial = HellNoteMaterial();
        return g;
    }
}
