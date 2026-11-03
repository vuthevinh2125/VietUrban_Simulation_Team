using UnityEngine;

/// <summary>
/// Gắn một model 3D nhập về (Asset Store) vào một object có sẵn và co giãn cho vừa khung của object đó.
/// Dùng để "thay vỏ" xe: giữ nguyên phần lái / va chạm / camera của xe mẫu, chỉ đổi hình.
/// Quy ước: model quay mặt theo trục dài nằm ngang của nó; nếu trục dài là X thì xoay 90° để thành Z (hướng chạy).
/// </summary>
public static class ModelFit
{
    /// <summary>Khung bao các mesh dưới root, tính trong toạ độ của space (không cần mesh Read/Write).</summary>
    public static bool MeshBounds(Transform root, Transform space, out Bounds bounds, bool includeInactive = false)
    {
        bounds = default;
        bool any = false;
        Matrix4x4 toSpace = space.worldToLocalMatrix;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(includeInactive))
        {
            if (mf.sharedMesh == null) continue;
            Renderer r = mf.GetComponent<Renderer>();
            if (r == null || (!includeInactive && !r.enabled)) continue;
            Encapsulate(mf.sharedMesh.bounds, toSpace * mf.transform.localToWorldMatrix, ref bounds, ref any);
        }
        foreach (SkinnedMeshRenderer sr in root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive))
        {
            if (sr.sharedMesh == null || (!includeInactive && !sr.enabled)) continue;
            Encapsulate(sr.sharedMesh.bounds, toSpace * sr.transform.localToWorldMatrix, ref bounds, ref any);
        }
        return any;
    }

    private static void Encapsulate(Bounds b, Matrix4x4 m, ref Bounds result, ref bool any)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = m.MultiplyPoint3x4(c);
            if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
            else result.Encapsulate(p);
        }
    }

    /// <summary>Ẩn mọi MeshRenderer / SkinnedMeshRenderer dưới root (vỏ cũ); trả về số renderer đã ẩn.</summary>
    public static int HideMeshes(Transform root)
    {
        int n = 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) { r.enabled = false; n++; }
        return n;
    }

    /// <summary>
    /// Tạo model dưới parent và đặt nó vào khung target (toạ độ parent): chiều dài theo Z bằng target, đáy bằng đáy target,
    /// tâm ngang trùng tâm target. yawOffset: xoay thêm (vd. 180 nếu model quay ngược). Bỏ va chạm / vật lý của model.
    /// </summary>
    public static Transform Attach(Transform parent, GameObject prefab, Bounds target, float yawOffset = 0f, float lengthScale = 1f)
    {
        var holder = new GameObject("Skin_" + prefab.name).transform;
        holder.SetParent(parent, false);
        GameObject m = Object.Instantiate(prefab, holder, false);
        foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        foreach (Rigidbody rb in m.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb);
        foreach (MonoBehaviour mb in m.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb); // script demo của gói (nếu có)

        if (!MeshBounds(m.transform, holder, out Bounds b, includeInactive: true)) return holder; // parent có thể đang tắt (mẫu xe giao thông)
        float yaw = (b.size.x > b.size.z ? 90f : 0f) + yawOffset;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 rotatedSize = Abs(rot * b.size);
        float scale = target.size.z * lengthScale / Mathf.Max(0.01f, rotatedSize.z);

        holder.localRotation = rot;
        holder.localScale = Vector3.one * scale;
        // Sau khi xoay + co giãn: tâm ngang của model về tâm target, đáy về đáy target
        Vector3 c0 = rot * (b.center * scale);
        float bottom = c0.y - rotatedSize.y * scale * 0.5f;
        holder.localPosition = new Vector3(target.center.x - c0.x, target.min.y - bottom, target.center.z - c0.z);
        return holder;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
