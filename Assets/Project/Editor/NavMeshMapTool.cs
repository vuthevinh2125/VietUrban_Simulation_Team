using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Vẽ bản đồ ASCII khả năng đi lại trong cửa hàng (từ điểm sinh khách):
///   S = điểm sinh, C = quầy thu ngân,  # = khách đi tới được,  o = có NavMesh nhưng KHÔNG tới được,  . = không có NavMesh
/// Kèm danh sách các vật có tên chứa "Door"/"Gate" nằm sát ranh giới giữa vùng # và o.
/// Menu: Tools > Debug > NavMesh Connectivity Map -> Logs/NavMeshMap.txt
/// </summary>
public static class NavMeshMapTool
{
    private const string StoreRootName = "Supermarket_full";
    private const float Step = 0.5f;

    [MenuItem("Tools/Debug/NavMesh Connectivity Map")]
    private static void Run()
    {
        Transform store = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == StoreRootName && t.parent == null);
        CustomerSpawner spawner = Object.FindFirstObjectByType<CustomerSpawner>();
        if (store == null || spawner == null || spawner.spawnPoint == null)
        {
            EditorUtility.DisplayDialog("NavMesh Map", "Cần có Supermarket_full và CustomerSpawner (đã có SpawnPoint).", "OK");
            return;
        }

        Bounds area = default;
        bool any = false;
        foreach (Renderer r in store.GetComponentsInChildren<Renderer>())
        {
            if (!any) { area = r.bounds; any = true; } else area.Encapsulate(r.bounds);
        }
        area.Encapsulate(spawner.spawnPoint.position);
        area.Expand(new Vector3(3f, 0f, 3f));

        float floorY = spawner.spawnPoint.position.y;
        if (!NavMesh.SamplePosition(spawner.spawnPoint.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
        {
            EditorUtility.DisplayDialog("NavMesh Map", "Điểm sinh khách không nằm trên NavMesh.", "OK");
            return;
        }

        int nx = Mathf.CeilToInt(area.size.x / Step);
        int nz = Mathf.CeilToInt(area.size.z / Step);
        var grid = new char[nx, nz];
        var path = new NavMeshPath();
        int reach = 0, island = 0;

        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                Vector3 p = new Vector3(area.min.x + (ix + 0.5f) * Step, floorY, area.min.z + (iz + 0.5f) * Step);
                if (!NavMesh.SamplePosition(p, out NavMeshHit h, Step * 0.5f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - floorY) > 0.6f)
                { grid[ix, iz] = '.'; continue; }
                NavMesh.CalculatePath(startHit.position, h.position, NavMesh.AllAreas, path);
                bool ok = path.status == NavMeshPathStatus.PathComplete;
                grid[ix, iz] = ok ? '#' : 'o';
                if (ok) reach++; else island++;
            }

        Mark(grid, area, spawner.spawnPoint.position, 'S');
        foreach (CheckoutCounter c in Object.FindObjectsByType<CheckoutCounter>(FindObjectsSortMode.None))
            if (c.customerPoint != null) Mark(grid, area, c.customerPoint.position, 'C');

        var sb = new StringBuilder();
        sb.AppendLine($"# NavMesh connectivity from spawn {startHit.position:F1}  (cell = {Step}m)");
        sb.AppendLine($"# reachable cells: {reach}, unreachable navmesh cells: {island}");
        sb.AppendLine($"# x: {area.min.x:F1} (left) -> {area.max.x:F1} (right);  z: {area.max.z:F1} (top) -> {area.min.z:F1} (bottom)");
        for (int iz = nz - 1; iz >= 0; iz--)
        {
            sb.Append($"{area.min.z + (iz + 0.5f) * Step,7:F1} ");
            for (int ix = 0; ix < nx; ix++) sb.Append(grid[ix, iz]);
            sb.AppendLine();
        }

        // Vật có tên door/gate gần ranh giới # / o
        sb.AppendLine("\n## Door/Gate objects near the border between reachable (#) and unreachable (o):");
        var borderPoints = new List<Vector3>();
        for (int ix = 1; ix < nx - 1; ix++)
            for (int iz = 1; iz < nz - 1; iz++)
                if (grid[ix, iz] == 'o' && (grid[ix + 1, iz] == '#' || grid[ix - 1, iz] == '#' || grid[ix, iz + 1] == '#' || grid[ix, iz - 1] == '#'))
                    borderPoints.Add(new Vector3(area.min.x + (ix + 0.5f) * Step, floorY, area.min.z + (iz + 0.5f) * Step));

        var reported = new HashSet<Transform>();
        foreach (Renderer r in store.GetComponentsInChildren<Renderer>())
        {
            Transform root = r.transform;
            while (root.parent != null && root.parent != store && root.parent.parent != store) root = root.parent;
            if (reported.Contains(root)) continue;
            string n = root.name.ToLower();
            if (!(n.Contains("door") || n.Contains("gate") || n.Contains("checkout") || n.Contains("barrier") || n.Contains("rail"))) continue;
            Bounds b = r.bounds; b.Expand(1f);
            if (borderPoints.Any(bp => b.Contains(new Vector3(bp.x, b.center.y, bp.z))))
            {
                reported.Add(root);
                sb.AppendLine($"- {root.name} pos={root.position:F1}");
            }
        }

        string file = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "NavMeshMap.txt");
        File.WriteAllText(file, sb.ToString());
        Debug.Log($"[NavMesh Map] reachable {reach}, unreachable {island}. {file}");
    }

    private static void Mark(char[,] grid, Bounds area, Vector3 p, char c)
    {
        int ix = Mathf.FloorToInt((p.x - area.min.x) / Step);
        int iz = Mathf.FloorToInt((p.z - area.min.z) / Step);
        if (ix >= 0 && iz >= 0 && ix < grid.GetLength(0) && iz < grid.GetLength(1)) grid[ix, iz] = c;
    }
}
