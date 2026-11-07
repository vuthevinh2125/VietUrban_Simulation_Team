using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dời điểm giao hàng (MarketManager.deliveryPoint) ra một khoảng đất trống cạnh cửa hàng, ngoài lối đi của khách.
/// Trước: thùng hàng được thả ngay trên vỉa hè trước cửa vào (-117, 16.8) - khách và người đi bộ đi xuyên / xô đổ đống thùng.
///
/// Cách chọn (quét lưới 1 m quanh mặt tiền và hai bên hông cửa hàng):
///  - có mặt đất ngang với cửa vào (lệch tối đa 0.6 m), khoảng 4 x 2 x 4 m phía trên trống (không có collider nào);
///  - KHÔNG nằm trên vỉa hè (vùng NavMesh "Sidewalks" của người đi bộ) hay lòng đường xe chạy -> không chắn ai.
///    Lần 1 loại cả vùng "Walkable" thì không còn ô nào (bãi đất trống cũng được bake là Walkable);
///  - không nằm trong toà nhà hay chỗ của phần mở rộng / nhà kho chưa mở khoá (đo cả renderer đang ẩn);
///  - cách đường khách đi (điểm sinh khách -> cửa vào, cửa ra -> điểm biến mất) ít nhất 3 m;
///  - gần cửa vào nhất (tối đa 18 m) để người chơi không phải bê thùng đi xa.
/// Chỉ đổi vị trí object DeliveryPoint (có Undo). Xem chỗ mới trong Scene view rồi Ctrl+S nếu thấy hợp lý.
/// </summary>
public static class DeliveryPointTool
{
    private const float MaxDoorDistance = 18f, RouteClearance = 3f;
    private static readonly Vector3 FreeHalf = new Vector3(2f, 1f, 2f);

    [MenuItem("Tools/Store Setup/21. Move Delivery Point to an empty lot")]
    private static void Run()
    {
        MarketManager market = Object.FindAnyObjectByType<MarketManager>(FindObjectsInactive.Include);
        if (market == null || market.deliveryPoint == null) { EditorUtility.DisplayDialog("Delivery point", "No MarketManager with a Delivery Point in the open scene.", "OK"); return; }
        GameObject store = GameObject.Find("Supermarket_full");
        if (store == null) { EditorUtility.DisplayDialog("Delivery point", "Supermarket_full not found.", "OK"); return; }

        Physics.SyncTransforms();
        Bounds storeB = RendererBounds(store, false);
        var blocked = new List<Bounds> { Grow(storeB, 1f) };
        foreach (string n in new[] { "StoreExpansion_1", "StoreExpansion_2", "Warehouse", "Expansion_StoreExpansion1", "Expansion_StoreExpansion2", "Expansion_Warehouse" })
        {
            GameObject g = FindIncludingInactive(n);
            if (g != null && TryBounds(g, out Bounds b)) blocked.Add(Grow(b, 1.5f));
        }

        // Cửa vào / ra và đường khách đi
        var doors = new List<Vector3>();
        foreach (EntranceDoor d in Object.FindObjectsByType<EntranceDoor>()) doors.Add(d.transform.position);
        if (doors.Count == 0) doors.Add(market.deliveryPoint.position);
        float doorY = doors[0].y;
        var route = new List<(Vector3 a, Vector3 b)>();
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        foreach (Vector3 d in doors)
        {
            if (spawner != null && spawner.spawnPoint != null) route.Add((spawner.spawnPoint.position, d));
            if (spawner != null && spawner.exitPoint != null) route.Add((d, spawner.exitPoint.position));
        }

        int sidewalks = NavMesh.GetAreaFromName("Sidewalks"), walkable = NavMesh.GetAreaFromName("Walkable");
        int walkMask = sidewalks >= 0 ? 1 << sidewalks : 0;
        int x0 = Mathf.FloorToInt(storeB.min.x - 16f), x1 = Mathf.CeilToInt(storeB.max.x + 16f);
        int z0 = Mathf.FloorToInt(storeB.min.z - 10f), z1 = Mathf.CeilToInt(storeB.min.z + 20f);
        var map = new char[z1 - z0 + 1, x1 - x0 + 1];
        var groundNames = new Dictionary<string, int>();
        var blockerNames = new Dictionary<string, int>();

        Vector3 best = Vector3.zero;
        float bestScore = float.MaxValue;
        int tried = 0, onGround = 0, free = 0, offPath = 0;
        for (int xi = x0; xi <= x1; xi++)
            for (int zi = z0; zi <= z1; zi++)
            {
                tried++;
                ref char cell = ref map[zi - z0, xi - x0];
                cell = 'B';
                var p = new Vector3(xi, doorY, zi);
                if (Inside(blocked, p)) continue;
                cell = 'g';
                if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - doorY) > 0.6f) continue;
                onGround++;
                Vector3 ground = hit.point;
                Count(groundNames, hit.collider.name);
                cell = 'r';
                if (NearTrafficRoad(ground)) continue;
                cell = 'x';
                int nb = Physics.OverlapBoxNonAlloc(ground + Vector3.up * (FreeHalf.y + 0.3f), FreeHalf, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (nb > 0) { for (int k = 0; k < nb; k++) Count(blockerNames, overlap[k].name); continue; }
                free++;
                cell = 's';
                // Không đặt lên vùng đi lại của NPC (vỉa hè, đường, sàn): kiểm tra 5 điểm trong ô 4 x 4 m
                bool onPath = false;
                foreach (Vector3 o in new[] { Vector3.zero, new Vector3(1.8f, 0, 1.8f), new Vector3(-1.8f, 0, 1.8f), new Vector3(1.8f, 0, -1.8f), new Vector3(-1.8f, 0, -1.8f) })
                    if (walkMask != 0 && NavMesh.SamplePosition(ground + o, out NavMeshHit nh, 0.4f, walkMask)) { onPath = true; break; }
                if (onPath) continue;
                cell = 'c';
                float clearance = float.MaxValue;
                foreach (var seg in route) clearance = Mathf.Min(clearance, DistToSegment(ground, seg.a, seg.b));
                if (clearance < RouteClearance) continue;
                offPath++;
                cell = 'f';
                float doorDist = float.MaxValue;
                foreach (Vector3 d in doors) doorDist = Mathf.Min(doorDist, Flat(ground - d));
                if (doorDist > MaxDoorDistance) continue;
                if (doorDist < bestScore) { bestScore = doorDist; best = ground; }
            }

        var sb = new StringBuilder();
        // Bản đồ chẩn đoán (Logs/DeliveryPointTool.txt): B toà nhà / phần mở rộng, g không có mặt đất ngang cửa,
        // r mặt đường / vỉa hè, x có vật cản, s vỉa hè người đi bộ (NavMesh), c sát lối khách đi, f đạt, D cửa, * chỗ chọn
        var mapText = new StringBuilder();
        mapText.AppendLine("Ground under the spots: " + Top(groundNames));
        mapText.AppendLine("Colliders filling the 4x2x4 m box: " + Top(blockerNames));
        mapText.AppendLine($"x {x0}..{x1} (left to right), z {z1}..{z0} (top to bottom)");
        for (int zi = z1; zi >= z0; zi--)
        {
            for (int xi = x0; xi <= x1; xi++)
            {
                char ch = map[zi - z0, xi - x0];
                foreach (Vector3 d in doors) if (Mathf.RoundToInt(d.x) == xi && Mathf.RoundToInt(d.z) == zi) ch = 'D';
                if (bestScore < float.MaxValue && Mathf.RoundToInt(best.x) == xi && Mathf.RoundToInt(best.z) == zi) ch = '*';
                mapText.Append(ch == '\0' ? ' ' : ch);
            }
            mapText.AppendLine();
        }
        sb.AppendLine($"[DeliveryPointTool] Checked {tried} spots: {onGround} on level ground, {free} with 4x2x4 m free, {offPath} off every walking area and >= {RouteClearance} m from the customer route.");
        try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs", "DeliveryPointTool.txt"), sb.ToString() + "\n" + mapText); }
        catch (System.Exception e) { Debug.LogWarning("[DeliveryPointTool] Could not write the map: " + e.Message); }
        if (bestScore == float.MaxValue)
        {
            sb.AppendLine("No suitable empty spot within " + MaxDoorDistance + " m of the entrance. The delivery point was NOT moved.");
            Debug.LogWarning(sb.ToString());
            EditorUtility.DisplayDialog("Delivery point", "No suitable empty spot found near the entrance. Nothing was changed (see Console).", "OK");
            return;
        }

        Transform dp = market.deliveryPoint;
        Vector3 old = dp.position;
        Undo.RecordObject(dp, "Move Delivery Point");
        dp.position = best + Vector3.up * 0.1f;
        // Hàng thùng xếp quay về phía cửa gần nhất
        Vector3 toDoor = doors[0] - best;
        foreach (Vector3 d in doors) if (Flat(d - best) < Flat(toDoor)) toDoor = d - best;
        toDoor.y = 0f;
        if (toDoor.sqrMagnitude > 0.01f) dp.rotation = Quaternion.LookRotation(-toDoor.normalized, Vector3.up);
        EditorSceneManager.MarkSceneDirty(dp.gameObject.scene);
        Selection.activeGameObject = dp.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        sb.AppendLine($"Delivery point moved from {old:F1} to {dp.position:F1}, {bestScore:F1} m from the nearest entrance door. Check it in the Scene view, then save the scene (Ctrl+S). Undo with Ctrl+Z.");
        Debug.Log(sb.ToString(), dp);
    }

    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    private static readonly Collider[] overlap = new Collider[16];

    // Các đoạn đường xe chạy (giống TrafficSystem.BuildNetwork): đại lộ cũ z = 8 từ x = -175 tới 22, đường dọc x = 22.
    // Lần 2 nhận mặt đường theo tên object (Street / Road / Pavement...) thì cả khu bị loại vì mặt đất quanh cửa hàng đều
    // là các tấm "Street ... Prefab" -> nay chỉ loại phần lòng đường xe chạy (cách tim đường dưới 8 m).
    private static readonly (Vector2 a, Vector2 b)[] TrafficRoads =
    {
        (new Vector2(-175f, 8f), new Vector2(22f, 8f)),
        (new Vector2(22f, -90f), new Vector2(22f, 98f)),
    };

    private static bool NearTrafficRoad(Vector3 p)
    {
        foreach (var r in TrafficRoads)
            if (DistToSegment(p, new Vector3(r.a.x, 0f, r.a.y), new Vector3(r.b.x, 0f, r.b.y)) < 8f) return true;
        return false;
    }

    private static void Count(Dictionary<string, int> d, string key) => d[key] = d.TryGetValue(key, out int n) ? n + 1 : 1;

    private static string Top(Dictionary<string, int> d)
    {
        var list = new List<KeyValuePair<string, int>>(d);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        var sb = new StringBuilder();
        for (int i = 0; i < list.Count && i < 12; i++) sb.Append(list[i].Key).Append(" x").Append(list[i].Value).Append(", ");
        return sb.ToString();
    }

    private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(p, a + ab * t);
    }

    private static bool Inside(List<Bounds> list, Vector3 p)
    {
        foreach (Bounds b in list)
            if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z) return true;
        return false;
    }

    private static Bounds Grow(Bounds b, float m) { b.Expand(new Vector3(m * 2f, 0f, m * 2f)); return b; }

    private static Bounds RendererBounds(GameObject g, bool includeInactive)
    {
        TryBounds(g, out Bounds b, includeInactive);
        return b;
    }

    private static bool TryBounds(GameObject g, out Bounds b, bool includeInactive = true)
    {
        b = new Bounds(g.transform.position, Vector3.zero);
        bool any = false;
        // Dựa vào lưới (MeshFilter) chứ không dùng Renderer.bounds: renderer của phần mở rộng chưa mở khoá đang ẩn thì bounds rỗng
        foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(includeInactive))
        {
            if (mf.sharedMesh == null) continue;
            Bounds lb = mf.sharedMesh.bounds;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
            }
        }
        return any;
    }

    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }
}
