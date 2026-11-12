using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bảng màu dùng chung cho các vật dựng bằng code (khu phố Việt, xe máy): một texture 32 x 32, mỗi ô một màu.
/// Mỗi khối chỉ cần UV trỏ vào ô màu của nó nên cả một khu phố vài nghìn khối nhiều màu chỉ dùng MỘT vật liệu
/// -> mỗi khu chỉ 1 - 2 lệnh vẽ thay vì vài chục.
/// Có các ô "đặt tên" (slot) đổi màu được lúc chạy: tán lá cây đổi theo mùa mà không phải dựng lại mesh.
/// </summary>
public static class VPalette
{
    private const int Size = 32;
    private static Texture2D tex;
    private static Material mat;
    private static Color32[] pixels;
    private static readonly Dictionary<Color32, int> index = new Dictionary<Color32, int>();
    private static readonly Dictionary<string, int> slots = new Dictionary<string, int>();
    private static int used;
    private static bool dirty;

    private static void Ensure()
    {
        if (tex != null && mat != null) return;
        tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "VietPalette", hideFlags = HideFlags.DontSave
        };
        pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        index.Clear();
        slots.Clear();
        used = 0;
        mat = new Material(RuntimeMaterials.Lit(Color.white)) { name = "VietPalette", hideFlags = HideFlags.DontSave };
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
        dirty = true;
    }

    /// <summary>Vật liệu dùng chung (đã cập nhật texture).</summary>
    public static Material Material
    {
        get { Ensure(); Apply(); return mat; }
    }

    /// <summary>UV của ô chứa màu c (màu được làm tròn 32 mức mỗi kênh để bảng không đầy).</summary>
    public static Vector2 UV(Color c)
    {
        Ensure();
        Color32 q = Quantize(c);
        if (!index.TryGetValue(q, out int i))
        {
            if (used >= Size * Size) i = Nearest(q);
            else
            {
                i = used++;
                index[q] = i;
                pixels[i] = q;
                dirty = true;
            }
        }
        return Cell(i);
    }

    /// <summary>Ô riêng có tên (vd "leaf3"): màu đổi được về sau bằng SetSlot.</summary>
    public static Vector2 Slot(string key, Color initial)
    {
        Ensure();
        if (!slots.TryGetValue(key, out int i))
        {
            if (used >= Size * Size) return UV(initial);
            i = used++;
            slots[key] = i;
            pixels[i] = initial;
            dirty = true;
        }
        return Cell(i);
    }

    public static void SetSlot(string key, Color c)
    {
        Ensure();
        if (!slots.TryGetValue(key, out int i)) return;
        Color32 q = c;
        q.a = 255;
        if (pixels[i].Equals(q)) return;
        pixels[i] = q;
        dirty = true;
    }

    public static void Apply()
    {
        if (tex == null || !dirty) return;
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        dirty = false;
    }

    private static Vector2 Cell(int i) => new Vector2((i % Size + 0.5f) / Size, (i / Size + 0.5f) / Size);

    private static Color32 Quantize(Color c)
    {
        Color32 k = c;
        k.r = (byte)((k.r & 0xF8) | 0x04);
        k.g = (byte)((k.g & 0xF8) | 0x04);
        k.b = (byte)((k.b & 0xF8) | 0x04);
        k.a = 255;
        return k;
    }

    private static int Nearest(Color32 q)
    {
        int best = 0, bestD = int.MaxValue;
        foreach (var kv in index)
        {
            int dr = kv.Key.r - q.r, dg = kv.Key.g - q.g, db = kv.Key.b - q.b;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = kv.Value; }
        }
        return best;
    }
}

/// <summary>Hình khối gốc (đỉnh + pháp tuyến + tam giác), tâm ở gốc toạ độ, kích thước 1.</summary>
public class VShape
{
    public Vector3[] v, n;
    public int[] t;

    private static VShape cube, prism, blob, cone;
    private static Mesh cubeMesh;

    public static VShape Cube => cube ?? (cube = MakeCube());
    /// <summary>Lăng trụ 8 cạnh (thay hình trụ), trục y, cao 1, đường kính 1.</summary>
    public static VShape Prism => prism ?? (prism = MakePrism(8));
    /// <summary>Khối 20 mặt (tán cây, đầu người, hoa...), đường kính 1.</summary>
    public static VShape Blob => blob ?? (blob = MakeBlob());
    /// <summary>Nón (nón lá, mái tháp), đáy ở y = -0.5, đỉnh ở y = 0.5.</summary>
    public static VShape Cone => cone ?? (cone = MakeCone(10));

    // ================= Khối mịn (pháp tuyến nội suy) cho xe máy, mũ bảo hiểm =================
    private static readonly Dictionary<string, VShape> smooth = new Dictionary<string, VShape>();

    /// <summary>Cầu mịn đường kính 1.</summary>
    public static VShape Sphere(int res = 16) => Cached("sph" + res, () => Param(res, res / 2 + 2, (u, w) =>
    {
        float th = w * Mathf.PI, ph = u * Mathf.PI * 2f;
        Vector3 p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f;
        return (p, p);
    }));

    /// <summary>Nửa cầu (vòm) đường kính 1, đáy ở y = 0, hở đáy.</summary>
    public static VShape Dome(int res = 18) => Cached("dome" + res, () => Param(res, res / 3 + 2, (u, w) =>
    {
        float th = w * Mathf.PI * 0.5f, ph = u * Mathf.PI * 2f;
        Vector3 p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f;
        return (p, p);
    }));

    /// <summary>Hộp bo tròn (siêu elip) kích thước 1; e nhỏ = vuông hơn (0.2 - 0.5 đẹp cho thân xe).</summary>
    public static VShape RoundBox(float e = 0.3f, int res = 14) => Cached($"rb{e:0.00}_{res}", () => Param(res, res / 2 + 2, (u, w) =>
    {
        float v = -Mathf.PI * 0.5f + w * Mathf.PI, ph = -Mathf.PI + u * Mathf.PI * 2f;
        float cv = SPow(Mathf.Cos(v), e), sv = SPow(Mathf.Sin(v), e), cu = SPow(Mathf.Cos(ph), e), su = SPow(Mathf.Sin(ph), e);
        Vector3 p = new Vector3(cv * cu, sv, cv * su) * 0.5f;
        return (p, p);
    }));

    /// <summary>Hình xuyến (lốp xe) trục y, đường kính ngoài 1; minor = tỉ lệ bán kính ống / bán kính ngoài.</summary>
    public static VShape Torus(float minor = 0.18f, int res = 20) => Cached($"tor{minor:0.00}_{res}", () =>
    {
        float rr = 0.5f * minor, R = 0.5f - rr;
        return Param(res, Mathf.Max(6, res / 2), (u, w) =>
        {
            float a = u * Mathf.PI * 2f, b = w * Mathf.PI * 2f;
            Vector3 ring = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 p = ring * (R + rr * Mathf.Cos(b)) + Vector3.up * (rr * Mathf.Sin(b));
            return (p, p - ring * R);
        });
    });

    /// <summary>Trụ tròn mịn trục y, cao 1, đường kính 1 (có nắp).</summary>
    public static VShape Cylinder(int res = 14) => Cached("cyl" + res, () =>
    {
        VShape side = Param(res, 1, (u, w) =>
        {
            float a = u * Mathf.PI * 2f;
            Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f;
            return (r + Vector3.up * (w - 0.5f), r);
        });
        var b = new Builder();
        for (int s = 0; s < res; s++)
        {
            float a0 = s * Mathf.PI * 2f / res, a1 = (s + 1) * Mathf.PI * 2f / res;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
            b.Tri(Vector3.up * 0.5f, p0 + Vector3.up * 0.5f, p1 + Vector3.up * 0.5f, Vector3.up);
            b.Tri(Vector3.down * 0.5f, p0 + Vector3.down * 0.5f, p1 + Vector3.down * 0.5f, Vector3.down);
        }
        VShape caps = b.Done();
        return Merge(side, caps);
    });

    private static float SPow(float x, float e) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);

    private static VShape Cached(string key, System.Func<VShape> make)
    {
        if (!smooth.TryGetValue(key, out VShape s)) smooth[key] = s = make();
        return s;
    }

    private static VShape Merge(VShape a, VShape b)
    {
        var v = new List<Vector3>(a.v); v.AddRange(b.v);
        var n = new List<Vector3>(a.n); n.AddRange(b.n);
        var t = new List<int>(a.t);
        foreach (int i in b.t) t.Add(i + a.v.Length);
        return new VShape { v = v.ToArray(), n = n.ToArray(), t = t.ToArray() };
    }

    /// <summary>Lưới tham số (u, w trong 0..1) đỉnh dùng chung, pháp tuyến = trung bình các mặt quanh đỉnh; f trả về (vị trí, hướng ra ngoài).</summary>
    private static VShape Param(int nu, int nw, System.Func<float, float, (Vector3 p, Vector3 o)> f)
    {
        var v = new Vector3[(nu + 1) * (nw + 1)];
        var o = new Vector3[v.Length];
        for (int j = 0; j <= nw; j++)
            for (int i = 0; i <= nu; i++)
            {
                var r = f((float)i / nu, (float)j / nw);
                v[j * (nu + 1) + i] = r.p;
                o[j * (nu + 1) + i] = r.o;
            }
        var t = new List<int>();
        var n = new Vector3[v.Length];
        void Tri(int a, int b, int c)
        {
            Vector3 nn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (nn.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(nn, o[a] + o[b] + o[c]) < 0f) { int tmp = b; b = c; c = tmp; nn = -nn; }
            t.Add(a); t.Add(b); t.Add(c);
            n[a] += nn; n[b] += nn; n[c] += nn;
        }
        for (int j = 0; j < nw; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                Tri(a, b, d);
                Tri(a, d, c);
            }
        // Gộp pháp tuyến các đỉnh trùng vị trí (mép nối u = 0 / 1, hai cực) để không lộ đường nối
        var weld = new Dictionary<Vector3Int, Vector3>();
        Vector3Int K(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
        for (int i = 0; i < v.Length; i++) { Vector3Int k = K(v[i]); weld[k] = (weld.TryGetValue(k, out Vector3 s) ? s : Vector3.zero) + n[i]; }
        for (int i = 0; i < n.Length; i++)
        {
            Vector3 s = weld[K(v[i])];
            n[i] = s.sqrMagnitude > 1e-12f ? s.normalized : (o[i].sqrMagnitude > 1e-12f ? o[i].normalized : Vector3.up);
        }
        return new VShape { v = v, n = n, t = t.ToArray() };
    }

    /// <summary>Mesh khối hộp (cho hạt lá rơi).</summary>
    public static Mesh CubeMesh
    {
        get
        {
            if (cubeMesh != null) return cubeMesh;
            cubeMesh = new Mesh { name = "VietCube", hideFlags = HideFlags.DontSave };
            cubeMesh.vertices = Cube.v;
            cubeMesh.normals = Cube.n;
            cubeMesh.triangles = Cube.t;
            cubeMesh.RecalculateBounds();
            return cubeMesh;
        }
    }

    private class Builder
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<int> t = new List<int>();

        /// <summary>Tam giác hướng ra phía want (Unity: mặt trước theo chiều kim đồng hồ).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nn, want) < 0f) { Vector3 tmp = b; b = c; c = tmp; nn = -nn; }
            nn.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nn); n.Add(nn); n.Add(nn);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>Tứ giác a b c d (theo vòng), 4 đỉnh dùng chung.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nn, want) < 0f) { Vector3 tmp = b; b = d; d = tmp; nn = -nn; }
            nn.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(nn);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public VShape Done() => new VShape { v = v.ToArray(), n = n.ToArray(), t = t.ToArray() };
    }

    private static VShape MakeCube()
    {
        var b = new Builder();
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 nrm in normals)
        {
            Vector3 up = Mathf.Abs(nrm.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(nrm, up);
            Vector3 c = nrm * 0.5f;
            b.Quad(c + (right - up) * 0.5f, c + (right + up) * 0.5f, c + (-right + up) * 0.5f, c + (-right - up) * 0.5f, nrm);
        }
        return b.Done();
    }

    private static VShape MakePrism(int sides)
    {
        var b = new Builder();
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides, am = (a0 + a1) * 0.5f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f), p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            Vector3 dn = Vector3.down * 0.5f, upv = Vector3.up * 0.5f;
            b.Quad(p0 + dn, p0 + upv, p1 + upv, p1 + dn, new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)));
            b.Tri(upv, p0 + upv, p1 + upv, Vector3.up);
            b.Tri(dn, p0 + dn, p1 + dn, Vector3.down);
        }
        return b.Done();
    }

    private static VShape MakeCone(int sides)
    {
        var b = new Builder();
        Vector3 apex = Vector3.up * 0.5f, baseC = Vector3.down * 0.5f;
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides, am = (a0 + a1) * 0.5f;
            Vector3 p0 = baseC + new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f), p1 = baseC + new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            b.Tri(apex, p0, p1, new Vector3(Mathf.Cos(am), 0.5f, Mathf.Sin(am)));
            b.Tri(baseC, p0, p1, Vector3.down);
        }
        return b.Done();
    }

    private static VShape MakeBlob()
    {
        float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] p =
        {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
        };
        for (int i = 0; i < p.Length; i++) p[i] = p[i].normalized * 0.5f;
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var b = new Builder();
        for (int i = 0; i < f.Length; i += 3)
        {
            Vector3 a = p[f[i]], c1 = p[f[i + 1]], c2 = p[f[i + 2]];
            b.Tri(a, c1, c2, (a + c1 + c2) / 3f);
        }
        return b.Done();
    }
}

/// <summary>Gộp nhiều hình khối (mỗi khối một ma trận + một màu bảng) thành một mesh.</summary>
public class VMeshBuilder
{
    private readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
    private readonly List<Vector2> uv = new List<Vector2>();
    private readonly List<int> t = new List<int>();

    public int Count => v.Count;

    public void Add(VShape s, Matrix4x4 m, Color c) => AddUV(s, m, VPalette.UV(c));

    public void AddUV(VShape s, Matrix4x4 m, Vector2 u)
    {
        Matrix4x4 nm = m.inverse.transpose;
        int b = v.Count;
        for (int i = 0; i < s.v.Length; i++)
        {
            v.Add(m.MultiplyPoint3x4(s.v[i]));
            n.Add(nm.MultiplyVector(s.n[i]).normalized);
            uv.Add(u);
        }
        for (int i = 0; i < s.t.Length; i++) t.Add(b + s.t[i]);
    }

    public Mesh ToMesh(string name)
    {
        var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    public void Clear()
    {
        v.Clear(); n.Clear(); uv.Clear(); t.Clear();
    }
}
