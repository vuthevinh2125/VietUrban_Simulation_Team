using UnityEngine;

/// <summary>Đo kích thước model sản phẩm để xếp vừa ô kệ / lòng thùng.</summary>
public static class ProductFit
{
    /// <summary>
    /// Khung bao tất cả mesh bên dưới root, tính trong toạ độ local của root
    /// (không tính scale của chính root). Kích thước thật = Scale(size, localScale của root).
    /// </summary>
    public static Bounds LocalBounds(Transform root)
    {
        Bounds b = default;
        bool any = false;
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        void Add(Mesh mesh, Transform t)
        {
            if (mesh == null) return;
            Matrix4x4 m = toRoot * t.localToWorldMatrix;
            Bounds mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            if (mf.GetComponent<MeshRenderer>() != null) Add(mf.sharedMesh, mf.transform);
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Add(smr.sharedMesh, smr.transform);

        if (!any) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
        b.size = Vector3.Max(b.size, Vector3.one * 0.005f);
        return b;
    }

    /// <summary>Gỡ va chạm / vật lý khỏi một bản sao chỉ dùng để trưng (trong thùng, trên quầy...).</summary>
    public static void MakeVisualOnly(GameObject go)
    {
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }
}
