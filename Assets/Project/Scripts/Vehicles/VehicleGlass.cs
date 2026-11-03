using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tạo kính trong suốt cho model xe làm cả thân bằng MỘT vật liệu đục (vd. xe thể thao Car4: kính là ô màu đen trên texture).
/// Tách các tam giác của kính ra một submesh riêng rồi gán vật liệu kính trong suốt (Resources/Vehicles/Glass).
/// Tam giác được coi là kính khi: tâm UV nằm trong ô màu kính của texture (uvRect) VÀ nằm trong vùng khoang lái
/// (theo tỉ lệ khung mesh: z từ đuôi 0 -> đầu 1 trong zRange, cao hơn minHeight). Ô đen trên texture còn dùng cho viền,
/// lưới tản nhiệt, gầm xe... nên phải lọc thêm theo vị trí.
/// Cần mesh bật Read/Write (Car4.fbx: isReadable = 1).
/// </summary>
public static class VehicleGlass
{
    public static int Apply(Transform skin, Vector4 uvRect, Vector2 zRange, float minHeight)
    {
        Material glass = Resources.Load<Material>("Vehicles/Glass");
        if (glass == null) return 0;

        // Thân xe = mesh lớn nhất (bánh xe là các mesh nhỏ riêng)
        MeshFilter body = null;
        foreach (MeshFilter mf in skin.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && (body == null || mf.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = mf;
        if (body == null || !body.sharedMesh.isReadable || body.sharedMesh.subMeshCount != 1) return 0;

        Mesh src = body.sharedMesh;
        Vector3[] v = src.vertices;
        Vector2[] uv = src.uv;
        int[] tris = src.GetTriangles(0);
        if (uv == null || uv.Length != v.Length) return 0;
        Bounds b = src.bounds;

        var keep = new List<int>(tris.Length);
        var glassTris = new List<int>();
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            int a = tris[i], c = tris[i + 1], d = tris[i + 2];
            Vector2 u = (uv[a] + uv[c] + uv[d]) / 3f;
            Vector3 p = (v[a] + v[c] + v[d]) / 3f;
            float z = Mathf.InverseLerp(b.min.z, b.max.z, p.z);
            float h = Mathf.InverseLerp(b.min.y, b.max.y, p.y);
            bool isGlass = u.x >= uvRect.x && u.x <= uvRect.z && u.y >= uvRect.y && u.y <= uvRect.w
                           && z >= zRange.x && z <= zRange.y && h >= minHeight;
            (isGlass ? glassTris : keep).Add(a);
            (isGlass ? glassTris : keep).Add(c);
            (isGlass ? glassTris : keep).Add(d);
        }
        if (glassTris.Count == 0) return 0;

        Mesh m = Object.Instantiate(src);
        m.name = src.name + "_Glass";
        m.subMeshCount = 2;
        m.SetTriangles(keep, 0);
        m.SetTriangles(glassTris, 1);
        body.sharedMesh = m;

        Renderer r = body.GetComponent<Renderer>();
        Material[] mats = r.sharedMaterials;
        r.sharedMaterials = new[] { mats.Length > 0 ? mats[0] : null, glass };
        return glassTris.Count / 3;
    }
}
