using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tạo danh mục NỘI THẤT bán trên máy tính từ gói ModernSupermarket:
///  - Mỗi món: tạo prefab riêng ở Assets/Project/Prefabs/Furniture, tự dò các mặt kệ bằng tia chiếu
///    (mỗi tầng phải có khoảng trống phía trên) rồi sinh ô xếp hàng + Shelf + vùng va chạm + NavMeshObstacle.
///  - Tạo FurnitureData (tên, giá, cấp yêu cầu) ở Assets/Project/Data/Furniture.
///  - Gắn danh sách vào FurnitureCatalog bên cạnh ShopCatalog (máy tính).
/// Menu: Tools > Store Setup > 9. Build Furniture Catalog. Chạy lại được (giá đã sửa trong FurnitureData được giữ).
/// </summary>
public static class FurnitureSetupTool
{
    private const string SourceFolder = "Assets/Assets_ThirdParty/ModernSupermarket/Prefabs/Furniture";
    private const string PrefabFolder = "Assets/Project/Prefabs/Furniture";
    private const string DataFolder = "Assets/Project/Data/Furniture";

    private const float Step = 0.05f;          // bước lưới tia chiếu
    private const float SlotX = 0.22f, SlotZ = 0.25f;
    private const float MinClearance = 0.12f;  // khoảng trống tối thiểu phía trên một tầng
    private const float MaxLevelHeight = 1.8f; // không xếp trên nóc kệ
    private const float Inset = 0.03f;         // chừa mép tầng
    private const float MinNormalY = 0.7f;     // chấp nhận tầng kệ / sạp nghiêng tới ~45 độ

    private struct Entry
    {
        public string name, source, type; public float price; public int level;
        public Entry(string n, string s, float p, int l, string t = "Shelf") { name = n; source = s; price = p; level = l; type = t; }
    }

    private static readonly Entry[] Entries =
    {
        new Entry("Display Table", "Table_5ma72f", 100f, 1),
        new Entry("Small Shelf", "Shelf_6h7vop", 150f, 1),
        new Entry("Shelf", "Shelf_kicg9f", 250f, 1),
        new Entry("Large Shelf", "Shelf_hler4q", 350f, 2),
        new Entry("Produce Stall", "Stall_6f5bo1", 120f, 2),
        new Entry("Wooden Stall", "Stall_94wthk", 160f, 3),
        new Entry("Fridge", "Fridge_4ttiif", 500f, 6, "Fresh"),
        new Entry("Double Fridge", "Fridge_f13xen", 700f, 7, "Fresh"),
        new Entry("Freezer", "Freezer_9olxw5", 600f, 8, "Fresh"),
    };

    [MenuItem("Tools/Store Setup/9. Build Furniture Catalog")]
    private static void Run()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);
        var log = new StringBuilder("# Furniture catalog\n");
        var datas = new List<FurnitureData>();

        try
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                Entry e = Entries[i];
                EditorUtility.DisplayProgressBar("Furniture", e.name, (float)i / Entries.Length);
                GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceFolder}/{e.source}.prefab");
                if (src == null) { log.AppendLine($"- {e.name}: KHÔNG thấy {e.source}"); continue; }

                GameObject prefab = BuildPrefab(src, e, out int slots, out int levels);
                log.AppendLine($"- {e.name} ({e.source}): {levels} tầng, {slots} ô, loại {e.type}   [{lastDiag}]");
                if (prefab == null) continue;

                string dataPath = $"{DataFolder}/{e.name.Replace(' ', '_')}.asset";
                FurnitureData data = AssetDatabase.LoadAssetAtPath<FurnitureData>(dataPath);
                bool isNew = data == null;
                if (isNew)
                {
                    data = ScriptableObject.CreateInstance<FurnitureData>();
                    AssetDatabase.CreateAsset(data, dataPath);
                    data.price = e.price;
                    data.requiredLevel = e.level;
                }
                data.displayName = e.name;
                data.prefab = prefab;
                data.displayType = e.type;
                EditorUtility.SetDirty(data);
                datas.Add(data);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.SaveAssets();
        string catalogInfo = AssignToCatalog(datas);
        log.AppendLine(catalogInfo);

        string file = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "FurnitureSetup.txt");
        File.WriteAllText(file, log.ToString());
        Debug.Log("[Furniture] " + log);
        EditorUtility.DisplayDialog("Furniture", $"Đã tạo {datas.Count} món nội thất.\n{catalogInfo}\n\nBấm Ctrl+S để lưu scene.\nChi tiết: Logs/FurnitureSetup.txt", "OK");
    }

    // ================= Tạo prefab =================
    private static GameObject BuildPrefab(GameObject src, Entry e, out int slotCount, out int levelCount)
    {
        slotCount = 0; levelCount = 0;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        try
        {
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = e.name.Replace(' ', '_');
            inst.transform.SetPositionAndRotation(new Vector3(0f, -1000f, 0f), Quaternion.identity);

            // Xoá kết quả của lần chạy trước (nếu prefab nguồn đã từng được xử lý)
            Transform old = inst.transform.Find("ShelfSlots");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Va chạm tạm bằng mesh để tia chiếu trúng đúng từng tầng kệ
            var tempColliders = new List<MeshCollider>();
            var disabled = new List<Collider>();
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) if (c.enabled) { c.enabled = false; disabled.Add(c); }
            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                tempColliders.Add(mc);
            }
            Physics.SyncTransforms();

            Bounds wb = WorldBounds(inst);
            List<Vector3> slots = FindSlots(inst.transform, wb, out levelCount);

            foreach (MeshCollider mc in tempColliders) Object.DestroyImmediate(mc);
            foreach (Collider c in disabled) c.enabled = true;

            // Ô xếp hàng
            Transform slotsRoot = new GameObject("ShelfSlots").transform;
            slotsRoot.SetParent(inst.transform, false);
            var slotTransforms = new Transform[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                Transform s = new GameObject($"Slot_{i:00}").transform;
                s.SetParent(slotsRoot, false);
                s.position = slots[i];
                s.rotation = inst.transform.rotation;
                slotTransforms[i] = s;
            }
            slotCount = slots.Count;

            Shelf shelf = inst.GetComponent<Shelf>();
            if (shelf == null) shelf = inst.AddComponent<Shelf>();
            shelf.slots = slotTransforms;
            shelf.acceptsDisplayType = e.type;

            // Vùng ngắm để xếp hàng / đặt giá + chặn người chơi
            Bounds lb = ProductFit.LocalBounds(inst.transform);
            BoxCollider box = inst.GetComponent<BoxCollider>();
            if (box == null) box = inst.AddComponent<BoxCollider>();
            box.center = lb.center;
            box.size = lb.size;

            // Khách đi vòng qua nội thất mới đặt
            NavMeshObstacle obstacle = inst.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = inst.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = lb.center;
            obstacle.size = lb.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            if (inst.GetComponent<PlacedFurniture>() == null) inst.AddComponent<PlacedFurniture>();

            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0) inst.layer = layer;

            inst.transform.position = Vector3.zero;
            return PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabFolder}/{inst.name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(inst);
        }
    }

    /// <summary>
    /// Bắn tia từ trên xuống khắp món đồ, lấy mọi mặt (gần) nằm ngang có khoảng trống phía trên.
    /// Gom các điểm liền nhau thành từng "tầng" (chấp nhận tầng kệ hơi nghiêng như kệ siêu thị),
    /// rồi trải lưới ô xếp hàng trên từng tầng, mỗi ô đặt đúng độ cao mặt kệ tại chỗ đó.
    /// </summary>
    private static string lastDiag = "";

    private static List<Vector3> FindSlots(Transform root, Bounds wb, out int levelCount)
    {
        float baseY = wb.min.y;
        int rawHits = 0, flatHits = 0, tooHigh = 0, tooTight = 0;
        var grid = new Dictionary<Vector2Int, List<Vector3>>(); // mỗi cột lưới có thể có nhiều tầng
        int cols = Mathf.CeilToInt(wb.size.x / Step), rows = Mathf.CeilToInt(wb.size.z / Step);
        for (int ix = 0; ix < cols; ix++)
            for (int iz = 0; iz < rows; iz++)
            {
                Vector3 origin = new Vector3(wb.min.x + (ix + 0.5f) * Step, wb.max.y + 0.5f, wb.min.z + (iz + 0.5f) * Step);
                var column = new List<(Vector3 p, bool openSky)>();
                foreach (RaycastHit h in CastThrough(origin, Vector3.down, wb.size.y + 1f, root))
                {
                    rawHits++;
                    if (h.normal.y < MinNormalY) continue;
                    flatHits++;
                    float height = h.point.y - baseY;
                    if (height < 0.03f || height > MaxLevelHeight) { tooHigh++; continue; }
                    float clear = ClearanceAbove(h.point, root);
                    if (clear < MinClearance) { tooTight++; continue; } // tầng trên quá sát
                    column.Add((h.point, clear >= 5f));
                }
                // Mặt "lộ thiên" (nóc kệ, nóc tủ lạnh, nắp tủ đông) chỉ dùng khi bên dưới không còn tầng nào khác (bàn, sạp)
                bool hasCovered = column.Any(c => !c.openSky);
                var keep = column.Where(c => !hasCovered || !c.openSky).Select(c => c.p).ToList();
                if (keep.Count > 0) grid[new Vector2Int(ix, iz)] = keep;
            }

        // Gom thành tầng: điểm ở 2 ô kề nhau lệch độ cao < 4cm thì cùng tầng
        var visited = new HashSet<(Vector2Int, int)>();
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        var levels = new List<Dictionary<Vector2Int, Vector3>>();
        foreach (var kv in grid)
            for (int k = 0; k < kv.Value.Count; k++)
            {
                if (visited.Contains((kv.Key, k))) continue;
                var level = new Dictionary<Vector2Int, Vector3>();
                var stack = new Stack<(Vector2Int, int)>();
                stack.Push((kv.Key, k));
                visited.Add((kv.Key, k));
                while (stack.Count > 0)
                {
                    var (cell, idx) = stack.Pop();
                    Vector3 p = grid[cell][idx];
                    level[cell] = p;
                    foreach (Vector2Int d in dirs)
                    {
                        Vector2Int nc = cell + d;
                        if (!grid.TryGetValue(nc, out List<Vector3> list)) continue;
                        for (int j = 0; j < list.Count; j++)
                        {
                            if (visited.Contains((nc, j)) || Mathf.Abs(list[j].y - p.y) > 0.04f) continue;
                            visited.Add((nc, j));
                            stack.Push((nc, j));
                        }
                    }
                }
                levels.Add(level);
            }

        var result = new List<Vector3>();
        var used = new HashSet<int>();
        levelCount = 0;
        lastDiag = $"tia trúng {rawHits}, mặt phẳng {flatHits}, ngoài độ cao {tooHigh}, quá sát {tooTight}, " +
                   $"nhóm mặt {levels.Count} (lớn nhất {(levels.Count > 0 ? levels.Max(l => l.Count) : 0)} điểm), cao {wb.size.y:F2}m";
        foreach (var level in levels)
        {
            if (level.Count < 12) continue; // mặt quá nhỏ (tay nắm, viền...)
            int minIx = level.Keys.Min(c => c.x), maxIx = level.Keys.Max(c => c.x);
            int minIz = level.Keys.Min(c => c.y), maxIz = level.Keys.Max(c => c.y);
            float minX = wb.min.x + (minIx + 0.5f) * Step + Inset, maxX = wb.min.x + (maxIx + 0.5f) * Step - Inset;
            float minZ = wb.min.z + (minIz + 0.5f) * Step + Inset, maxZ = wb.min.z + (maxIz + 0.5f) * Step - Inset;
            if (maxX - minX < 0.06f || maxZ - minZ < 0.06f) continue;

            int nx = Mathf.Max(1, Mathf.FloorToInt((maxX - minX) / SlotX + 0.5f));
            int nz = Mathf.Max(1, Mathf.FloorToInt((maxZ - minZ) / SlotZ + 0.5f));
            int added = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    float x = minX + (ix + 0.5f) * (maxX - minX) / nx;
                    float z = minZ + (iz + 0.5f) * (maxZ - minZ) / nz;
                    var cell = new Vector2Int(Mathf.FloorToInt((x - wb.min.x) / Step), Mathf.FloorToInt((z - wb.min.z) / Step));
                    if (!level.TryGetValue(cell, out Vector3 surface)) continue; // chỗ khuyết, không có mặt kệ
                    result.Add(new Vector3(x, surface.y, z));
                    added++;
                }
            if (added > 0)
            {
                levelCount++;
                foreach (Vector2Int c in level.Keys) used.Add(c.x * 100000 + c.y + Mathf.RoundToInt(level[c].y * 1000f) * 7919);
            }
        }

        // Lượt 2 - kệ lưới sắt (tia lọt qua khe nên các điểm chạm rời rạc): gom các điểm còn lại theo độ cao,
        // tầng nào có đủ điểm trải rộng thì phủ ô lên cả khung chữ nhật của tầng đó.
        var rest = new List<Vector3>();
        foreach (var kv in grid)
            foreach (Vector3 p in kv.Value)
                if (!used.Contains(kv.Key.x * 100000 + kv.Key.y + Mathf.RoundToInt(p.y * 1000f) * 7919)) rest.Add(p);
        rest.Sort((a, b) => a.y.CompareTo(b.y));
        var cluster = new List<Vector3>();
        for (int i = 0; i <= rest.Count; i++)
        {
            if (i < rest.Count && (cluster.Count == 0 || rest[i].y - cluster[cluster.Count - 1].y <= 0.03f)) { cluster.Add(rest[i]); continue; }
            if (cluster.Count >= 6)
            {
                float minX = cluster.Min(p => p.x) , maxX = cluster.Max(p => p.x);
                float minZ = cluster.Min(p => p.z) , maxZ = cluster.Max(p => p.z);
                if (maxX - minX >= 0.25f && maxZ - minZ >= 0.15f)
                {
                    float y = cluster.Average(p => p.y);
                    int nx = Mathf.Max(1, Mathf.FloorToInt((maxX - minX) / SlotX + 0.5f));
                    int nz = Mathf.Max(1, Mathf.FloorToInt((maxZ - minZ) / SlotZ + 0.5f));
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                            result.Add(new Vector3(minX + (ix + 0.5f) * (maxX - minX) / nx, y, minZ + (iz + 0.5f) * (maxZ - minZ) / nz));
                    levelCount++;
                }
            }
            cluster.Clear();
            if (i < rest.Count) cluster.Add(rest[i]);
        }
        return result;
    }

    /// <summary>
    /// Mọi điểm tia chạm vào món đồ theo thứ tự từ gần tới xa.
    /// (Physics.RaycastAll chỉ trả 1 điểm cho mỗi collider, nên kệ / tủ làm từ 1 mesh liền sẽ mất các tầng bên dưới.
    /// Ở đây bắn tiếp từ ngay sau điểm vừa chạm cho tới hết.)
    /// </summary>
    private static List<RaycastHit> CastThrough(Vector3 origin, Vector3 dir, float distance, Transform root)
    {
        var result = new List<RaycastHit>();
        Vector3 o = origin;
        float left = distance;
        for (int i = 0; i < 64 && left > 0f; i++)
        {
            if (!Physics.Raycast(o, dir, out RaycastHit h, left, ~0, QueryTriggerInteraction.Ignore)) break;
            if (h.transform.IsChildOf(root)) result.Add(h);
            float moved = h.distance + 0.002f;
            o += dir * moved;
            left -= moved;
        }
        return result;
    }

    /// <summary>Khoảng trống phía trên một điểm, chỉ tính các phần của chính món đồ (tối đa 5m).</summary>
    private static float ClearanceAbove(Vector3 point, Transform root)
    {
        foreach (RaycastHit h in CastThrough(point + Vector3.up * 0.005f, Vector3.up, 5f, root))
            return h.distance;
        return 5f;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static string AssignToCatalog(List<FurnitureData> datas)
    {
        ShopCatalog shop = Object.FindFirstObjectByType<ShopCatalog>(FindObjectsInactive.Include);
        if (shop == null) return "Không thấy ShopCatalog (máy tính) trong scene - hãy mở scene chính rồi chạy lại.";
        FurnitureCatalog cat = shop.GetComponent<FurnitureCatalog>();
        if (cat == null) cat = Undo.AddComponent<FurnitureCatalog>(shop.gameObject);
        Undo.RecordObject(cat, "Assign furniture");
        cat.furniture = datas;
        EditorUtility.SetDirty(cat);
        if (PrefabUtility.IsPartOfPrefabInstance(cat)) PrefabUtility.RecordPrefabInstancePropertyModifications(cat);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        return $"Đã gắn {datas.Count} món vào máy tính ({shop.name}).";
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
