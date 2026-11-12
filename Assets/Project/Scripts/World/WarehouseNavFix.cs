using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nhà kho (dựng bằng công cụ 15, ẩn tới khi mua) có thể đã không nằm trong NavMesh lúc bake (bake khi kho đang ẩn) ->
/// NavMesh chạy xuyên qua chỗ tường kho, NPC (restocker...) đi thẳng vào tường thay vì vào bằng cửa.
/// Khi vào game: gắn NavMeshObstacle (carve) cho từng mảng tường kho (trừ mảng có ô cửa) và từng kệ kho.
/// Obstacle chỉ cắt đường khi object đang hiện, nên kho chưa mua thì không ảnh hưởng gì.
/// </summary>
public static class WarehouseNavFix
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRun()
    {
        Apply();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Apply();

    private static void Apply()
    {
        if (!SupermarketScene.IsActive) return;
        int walls = 0, racks = 0;
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Warehouse") continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool wall = n.StartsWith("Wall") && !n.Contains("Door");
                bool rack = !wall && t.GetComponent<StorageRack>() != null;
                if (!wall && !rack) continue;
                if (t.GetComponent<NavMeshObstacle>() != null) continue;
                if (!LocalBounds(t, out Bounds b)) continue;
                var o = t.gameObject.AddComponent<NavMeshObstacle>();
                o.shape = NavMeshObstacleShape.Box;
                o.center = b.center;
                o.size = Vector3.Max(b.size, Vector3.one * 0.1f);
                o.carving = true;
                o.carveOnlyStationary = true;
                if (wall) walls++; else racks++;
            }
        }
        if (walls + racks > 0 && GameDebug.CheatsEnabled)
            Debug.Log($"[Warehouse] Gắn vật cản NavMesh: {walls} mảng tường, {racks} kệ kho (NPC phải vào bằng cửa kho).");
    }

    /// <summary>Khung bao các mesh của t, tính theo toạ độ local của t (NavMeshObstacle dùng toạ độ local, trước scale).</summary>
    private static bool LocalBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = t.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return any;
    }
}
