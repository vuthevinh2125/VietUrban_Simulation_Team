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
    private const float Inset = 0.05f;         // chừa mép tầng

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
                log.AppendLine($"- {e.name} ({e.source}): {levels} tầng, {slots} ô, loại {e.type}");
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
    /// Bắn tia từ trên xuống khắp món đồ, lấy mọi mặt nằm ngang có khoảng trống phía trên,
    /// gom theo tầng rồi trải lưới ô xếp hàng trên từng tầng.
    /// </summary>
    private static List<Vector3> FindSlots(Transform root, Bounds wb, out int levelCount)
    {
        float baseY = wb.min.y;
        var hits = new List<Vector3>();
        for (float x = wb.min.x + Step * 0.5f; x < wb.max.x; x += Step)
            for (float z = wb.min.z + Step * 0.5f; z < wb.max.z; z += Step)
            {
                Vector3 origin = new Vector3(x, wb.max.y + 0.5f, z);
                var column = new List<(Vector3 p, bool openSky)>();
                foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, wb.size.y + 1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!h.transform.IsChildOf(root) || h.normal.y < 0.95f) continue;
                    float height = h.point.y - baseY;
                    if (height < 0.03f || height > MaxLevelHeight) continue;
                    float clear = ClearanceAbove(h.point, root);
                    if (clear < MinClearance) continue; // tầng trên quá sát
                    column.Add((h.point, clear >= 5f));
                }
                // Mặt "lộ thiên" (nóc kệ, nóc tủ lạnh, nắp tủ đông) chỉ dùng khi bên dưới không còn tầng nào khác (bàn, sạp)
                bool hasCovered = column.Any(c => !c.openSky);
                foreach (var c in column) if (!hasCovered || !c.openSky) hits.Add(c.p);
            }

        // Gom theo tầng (lệch < 2cm)
        var levels = new List<List<Vector3>>();
        foreach (Vector3 p in hits.OrderBy(p => p.y))
        {
            List<Vector3> lv = levels.LastOrDefault();
            if (lv == null || Mathf.Abs(lv[0].y - p.y) > 0.02f) levels.Add(lv = new List<Vector3>());
            lv.Add(p);
        }

        var result = new List<Vector3>();
        levelCount = 0;
        foreach (List<Vector3> lv in levels)
        {
            if (lv.Count < 12) continue; // mặt quá nhỏ (tay nắm, viền...)
            float y = lv.Average(p => p.y);
            float minX = lv.Min(p => p.x) + Inset, maxX = lv.Max(p => p.x) - Inset;
            float minZ = lv.Min(p => p.z) + Inset, maxZ = lv.Max(p => p.z) - Inset;
            if (maxX - minX < 0.12f || maxZ - minZ < 0.12f) continue;

            int nx = Mathf.Max(1, Mathf.FloorToInt((maxX - minX) / SlotX));
            int nz = Mathf.Max(1, Mathf.FloorToInt((maxZ - minZ) / SlotZ));
            var set = new HashSet<Vector2Int>(lv.Select(p => new Vector2Int(Mathf.RoundToInt(p.x / Step), Mathf.RoundToInt(p.z / Step))));
            int added = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    float x = minX + (ix + 0.5f) * (maxX - minX) / nx;
                    float z = minZ + (iz + 0.5f) * (maxZ - minZ) / nz;
                    // chỉ đặt ô nơi thật sự có mặt kệ (bỏ chỗ khuyết)
                    if (!set.Contains(new Vector2Int(Mathf.RoundToInt(x / Step), Mathf.RoundToInt(z / Step)))) continue;
                    result.Add(new Vector3(x, y, z));
                    added++;
                }
            if (added > 0) levelCount++;
        }
        return result;
    }

    /// <summary>Khoảng trống phía trên một điểm, chỉ tính các phần của chính món đồ (tối đa 5m).</summary>
    private static float ClearanceAbove(Vector3 point, Transform root)
    {
        float best = 5f;
        foreach (RaycastHit h in Physics.RaycastAll(point + Vector3.up * 0.005f, Vector3.up, 5f, ~0, QueryTriggerInteraction.Ignore))
            if (h.transform.IsChildOf(root)) best = Mathf.Min(best, h.distance);
        return best;
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
