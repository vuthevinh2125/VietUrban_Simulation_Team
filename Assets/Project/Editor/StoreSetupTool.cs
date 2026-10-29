using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Thiết lập cửa hàng chính (Supermarket_full):
///  - Biến mọi kệ/tủ có bày hàng trang trí thành kệ xếp hàng (Shelf), ô xếp đặt đúng chỗ hàng trang trí cũ.
///  - Tắt toàn bộ hàng trang trí (cửa hàng bắt đầu trống).
///  - Tắt và gom khu nhà cũ vào "_Archived_OldStore".
///  - Gỡ script AI gắn nhầm lên tường.
/// Không xoá vĩnh viễn gì cả: mọi thứ chỉ bị TẮT, Ctrl+Z để hoàn tác.
/// Menu: Tools > Store Setup > 1. Preview  rồi  2. Apply
/// </summary>
public static class StoreSetupTool
{
    private const string StoreRootName = "Supermarket_full";
    private const string ProductsName = "Products";
    private const string FurnitureName = "Furniture";
    private const string OldStoreAnchor = "JeonbyeongwonTailor";
    private const string ArchiveName = "_Archived_OldStore";
    private const string SlotsName = "ShelfSlots";

    private static readonly string[] OldStoreNames = { "raystore_fresh", "Shelf_Flat", "Monitor", "Keyboard", "Mouse", "Box", "Cardboard Box" };
    private static readonly string[] NotShelfNames = { "checkout", "counter", "door", "anti-theft", "basket", "cart", "computer", "cash" };
    private static readonly string[] FreshNames = { "fridge", "freezer", "fresh", "cooler", "refriger" };
    private static readonly string[] NeverArchiveNames = { "road", "street", "walk", "sideway", "pavement", "lane", "grass", "tree", "light", "navmesh", "camera", "manager", "canvas", "eventsystem", "player", "npc", "car", "honda", "c125" };

    private const float MinSlotSpacing = 0.15f;
    private const float SlotSpacingX = 0.22f;   // khoảng cách ô theo chiều dài kệ
    private const float SlotSpacingZ = 0.25f;   // khoảng cách ô theo chiều sâu kệ
    private const int MaxSlotsPerShelf = 40;
    private const int MinProductsForShelf = 2;

    [MenuItem("Tools/Store Setup/1. Preview (không thay đổi gì)")]
    private static void Preview() => Run(apply: false);

    [MenuItem("Tools/Store Setup/2. Apply")]
    private static void Apply()
    {
        if (!EditorUtility.DisplayDialog("Store Setup",
                "Sẽ: tạo kệ xếp hàng trong Supermarket_full, TẮT hàng trang trí, TẮT khu nhà cũ.\n" +
                "Không xoá vĩnh viễn, có thể Ctrl+Z.\n\nTiếp tục?", "Áp dụng", "Huỷ"))
            return;
        Run(apply: true);
    }

    private static void Run(bool apply)
    {
        var sb = new StringBuilder();
        sb.AppendLine(apply ? "# STORE SETUP - APPLY" : "# STORE SETUP - PREVIEW (chưa thay đổi gì)");

        GameObject storeGo = FindSceneObject(StoreRootName);
        if (storeGo == null) { Fail($"Không tìm thấy '{StoreRootName}' trong scene."); return; }
        Transform store = storeGo.transform;
        Transform products = store.Find(ProductsName);
        Transform furniture = store.Find(FurnitureName);
        if (products == null || furniture == null) { Fail($"'{StoreRootName}' thiếu nhóm '{ProductsName}' hoặc '{FurnitureName}'."); return; }

        if (apply)
        {
            Undo.SetCurrentGroupName("Store Setup");
        }
        int undoGroup = Undo.GetCurrentGroup();

        // ---------- 1) Ghép hàng trang trí vào kệ ----------
        // Object đang tắt thì renderer không có kích thước -> tạm bật nhóm hàng trang trí để đo (chạy lại nhiều lần vẫn đúng)
        bool productsWereActive = products.gameObject.activeSelf;
        if (!productsWereActive) products.gameObject.SetActive(true);

        List<Transform> fixtures = PieceRoots(furniture);
        List<Transform> items = PieceRoots(products);
        var fixtureBounds = new Dictionary<Transform, Bounds>();
        foreach (Transform f in fixtures)
            if (TryBounds(f, out Bounds b)) fixtureBounds[f] = b;
        var itemBounds = new Dictionary<Transform, Bounds>();
        foreach (Transform item in items)
            if (TryBounds(item, out Bounds ib)) itemBounds[item] = ib;

        if (!productsWereActive) products.gameObject.SetActive(false);

        var byFixture = new Dictionary<Transform, List<Bounds>>();
        int orphan = 0;
        foreach (Transform item in items)
        {
            if (!itemBounds.TryGetValue(item, out Bounds ib)) continue;
            Transform best = null;
            float bestVol = float.MaxValue;
            foreach (var kv in fixtureBounds)
            {
                Bounds fb = kv.Value;
                fb.Expand(0.1f);
                if (!fb.Contains(ib.center)) continue;
                float vol = kv.Value.size.x * kv.Value.size.y * kv.Value.size.z;
                if (vol < bestVol) { bestVol = vol; best = kv.Key; }
            }
            if (best == null) { orphan++; continue; }
            if (!byFixture.TryGetValue(best, out var list)) byFixture[best] = list = new List<Bounds>();
            list.Add(ib);
        }

        sb.AppendLine($"\n## Kệ/tủ trong {FurnitureName}: {fixtures.Count} | hàng trang trí: {items.Count} | hàng không nằm trên kệ nào: {orphan}");
        int shelfCount = 0, slotTotal = 0;
        foreach (var kv in byFixture.OrderBy(k => k.Key.name))
        {
            Transform f = kv.Key;
            string lower = f.name.ToLower();
            bool excluded = NotShelfNames.Any(n => lower.Contains(n));
            List<Vector3> slots = SlotsFromItems(f, kv.Value);
            bool make = !excluded && slots.Count >= MinProductsForShelf;
            string type = FreshNames.Any(n => lower.Contains(n)) ? "Fresh" : "Shelf";
            sb.AppendLine($"- {(make ? "[KỆ]" : "[bỏ qua]")} {f.name} pos={f.position:F1} ô={slots.Count} loại={type}{(excluded ? " (không phải kệ)" : "")}");
            if (!make) continue;
            shelfCount++;
            slotTotal += slots.Count;
            if (apply) MakeShelf(f, slots, type, fixtureBounds[f]);
        }
        sb.AppendLine($"=> {shelfCount} kệ xếp hàng, tổng {slotTotal} ô.");
        sb.AppendLine("\nKệ/tủ KHÔNG có hàng trang trí (giữ nguyên, không thành kệ):");
        foreach (Transform f in fixtures.Where(f => !byFixture.ContainsKey(f)).OrderBy(f => f.name).Take(60))
            sb.AppendLine($"  · {f.name} pos={f.position:F1}");

        // ---------- 2) Tắt hàng trang trí ----------
        sb.AppendLine($"\n## Tắt nhóm hàng trang trí '{ProductsName}' ({items.Count} món)");
        if (apply && products.gameObject.activeSelf)
        {
            Undo.RecordObject(products.gameObject, "Hide decorative products");
            products.gameObject.SetActive(false);
        }

        // ---------- 3) Gom và tắt khu nhà cũ ----------
        sb.AppendLine("\n## Khu nhà cũ sẽ bị tắt và gom vào " + ArchiveName);
        List<Transform> old = FindOldStoreObjects(store);
        foreach (Transform t in old) sb.AppendLine($"- {t.name} pos={t.position:F1}");
        if (apply && old.Count > 0)
        {
            GameObject archive = FindSceneObject(ArchiveName);
            if (archive == null)
            {
                archive = new GameObject(ArchiveName);
                Undo.RegisterCreatedObjectUndo(archive, "Create archive");
            }
            foreach (Transform t in old)
            {
                Undo.SetTransformParent(t, archive.transform, "Archive old store");
                Undo.RecordObject(t.gameObject, "Disable old store");
                t.gameObject.SetActive(false);
            }
        }

        // ---------- 4) Gỡ script AI gắn nhầm (vật không có Animator mà có NavMeshAgent/SecurityGuard) ----------
        sb.AppendLine("\n## Gỡ AI gắn nhầm lên đồ vật");
        foreach (NavMeshAgent agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (agent.GetComponentInChildren<Animator>(true) != null) continue;
            sb.AppendLine($"- {agent.name}: gỡ NavMeshAgent{(agent.GetComponent<SecurityGuard>() ? " + SecurityGuard" : "")}");
            if (!apply) continue;
            var guard = agent.GetComponent<SecurityGuard>();
            if (guard != null) Undo.DestroyObjectImmediate(guard);
            Undo.DestroyObjectImmediate(agent);
        }

        if (apply)
        {
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(storeGo.scene);
            sb.AppendLine("\nĐÃ ÁP DỤNG. Việc tiếp theo: chọn 'NavMesh Manager' -> Bake lại NavMesh, rồi Ctrl+S.");
        }

        string file = Path.Combine(Directory.GetCurrentDirectory(), "Logs", apply ? "StoreSetup_Apply.txt" : "StoreSetup_Preview.txt");
        File.WriteAllText(file, sb.ToString());
        Debug.Log($"[Store Setup] {(apply ? "Đã áp dụng" : "Xem trước")}: {shelfCount} kệ, {slotTotal} ô, tắt {items.Count} món trang trí, tắt {old.Count} object nhà cũ. Chi tiết: {file}");
    }

    // ================= Tạo kệ =================
    private static void MakeShelf(Transform fixture, List<Vector3> slotPositions, string displayType, Bounds worldBounds)
    {
        GameObject go = fixture.gameObject;

        Shelf shelf = go.GetComponent<Shelf>();
        if (shelf == null) shelf = Undo.AddComponent<Shelf>(go);

        // Xoá ô cũ do công cụ tạo lần trước (nếu chạy lại)
        Transform oldSlots = fixture.Find(SlotsName);
        if (oldSlots != null) Undo.DestroyObjectImmediate(oldSlots.gameObject);

        var slotsRoot = new GameObject(SlotsName).transform;
        Undo.RegisterCreatedObjectUndo(slotsRoot.gameObject, "Create slots");
        slotsRoot.SetParent(fixture, false);

        // Sắp xếp: thấp -> cao, rồi trái -> phải theo hướng kệ
        Vector3 right = fixture.right;
        var ordered = slotPositions.OrderBy(p => Mathf.Round(p.y * 10f)).ThenBy(p => Vector3.Dot(p, right)).ToList();

        var slotTransforms = new Transform[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            var s = new GameObject($"Slot_{i:00}").transform;
            s.SetParent(slotsRoot, false);
            s.position = ordered[i];
            s.rotation = Quaternion.Euler(0f, fixture.eulerAngles.y, 0f);
            slotTransforms[i] = s;
        }

        var so = new SerializedObject(shelf);
        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = slotTransforms.Length;
        for (int i = 0; i < slotTransforms.Length; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotTransforms[i];
        so.FindProperty("acceptsDisplayType").stringValue = displayType;
        so.ApplyModifiedProperties();

        // Vùng nhắm để người chơi xếp hàng: BoxCollider bao kệ, layer Interactable
        if (go.GetComponent<Collider>() == null)
        {
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            Vector3 c = fixture.InverseTransformPoint(worldBounds.center);
            Vector3 s = fixture.InverseTransformVector(worldBounds.size);
            box.center = c;
            box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) { Undo.RecordObject(go, "Set layer"); go.layer = layer; }

        if (PrefabUtility.IsPartOfPrefabInstance(shelf)) PrefabUtility.RecordPrefabInstancePropertyModifications(shelf);
    }

    // ================= Helpers =================
    /// <summary>Các "món" trong một nhóm: gốc prefab ngoài cùng (nằm dưới nhóm) của từng renderer.</summary>
    private static List<Transform> PieceRoots(Transform group)
    {
        var set = new HashSet<Transform>();
        foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
        {
            // Món = prefab instance ngoài cùng nằm DƯỚI group; không có prefab thì lấy chính object có renderer
            Transform piece = null;
            for (Transform p = r.transform; p != null && p != group; p = p.parent)
                if (PrefabUtility.IsAnyPrefabInstanceRoot(p.gameObject)) piece = p;
            set.Add(piece != null ? piece : r.transform);
        }
        return set.ToList();
    }

    private static bool TryBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    /// <summary>
    /// Trải lưới ô xếp phủ mặt đáy của từng món trang trí (theo hướng của kệ), bỏ ô trùng,
    /// giới hạn MaxSlotsPerShelf ô mỗi kệ (lấy đều nhau).
    /// </summary>
    private static List<Vector3> SlotsFromItems(Transform fixture, List<Bounds> items)
    {
        Quaternion rot = Quaternion.Euler(0f, fixture.eulerAngles.y, 0f);
        Quaternion inv = Quaternion.Inverse(rot);
        Vector3 origin = fixture.position;
        var points = new List<Vector3>();

        foreach (Bounds b in items)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.min.y, (i & 2) == 0 ? b.min.z : b.max.z);
                Vector3 l = inv * (corner - origin);
                minX = Mathf.Min(minX, l.x); maxX = Mathf.Max(maxX, l.x);
                minZ = Mathf.Min(minZ, l.z); maxZ = Mathf.Max(maxZ, l.z);
            }

            int nx = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / SlotSpacingX));
            int nz = Mathf.Max(1, Mathf.RoundToInt((maxZ - minZ) / SlotSpacingZ));
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 local = new Vector3(minX + (ix + 0.5f) * (maxX - minX) / nx, 0f, minZ + (iz + 0.5f) * (maxZ - minZ) / nz);
                    Vector3 world = origin + rot * local;
                    world.y = b.min.y;
                    points.Add(world);
                }
        }

        var result = new List<Vector3>();
        foreach (Vector3 p in points)
            if (!result.Any(q => Vector3.Distance(p, q) < MinSlotSpacing)) result.Add(p);

        if (result.Count > MaxSlotsPerShelf)
        {
            result = result.OrderBy(p => Mathf.Round(p.y * 10f)).ThenBy(p => (inv * (p - origin)).x).ToList();
            var picked = new List<Vector3>();
            for (int i = 0; i < MaxSlotsPerShelf; i++)
                picked.Add(result[Mathf.FloorToInt(i * (float)result.Count / MaxSlotsPerShelf)]);
            result = picked;
        }
        return result;
    }

    private static List<Transform> FindOldStoreObjects(Transform store)
    {
        var result = new List<Transform>();
        GameObject anchor = FindSceneObject(OldStoreAnchor);
        Bounds area = default;
        bool hasArea = anchor != null && TryBounds(anchor.transform, out area);
        if (hasArea) area.Expand(new Vector3(2f, 50f, 2f));

        foreach (GameObject root in store.gameObject.scene.GetRootGameObjects())
        {
            Transform t = root.transform;
            if (t == store || root.name == ArchiveName) continue;
            string lower = root.name.ToLower();
            if (NeverArchiveNames.Any(n => lower.Contains(n))) continue;
            if (root.GetComponentInChildren<Camera>(true) || root.GetComponentInChildren<NavMeshAgent>(true) ||
                root.GetComponentInChildren<GameManager>(true) || root.GetComponentInChildren<MarketManager>(true) ||
                root.GetComponentInChildren<Canvas>(true) || root.CompareTag("Player")) continue;

            bool byName = root.name == OldStoreAnchor || OldStoreNames.Any(n => root.name.StartsWith(n));
            bool inArea = hasArea && TryBounds(t, out Bounds rb) && area.Contains(new Vector3(rb.center.x, area.center.y, rb.center.z))
                          && rb.size.x < 30f && rb.size.z < 30f;
            if (byName || inArea) result.Add(t);
        }
        return result;
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.parent == null && t.gameObject.scene.IsValid()) return t.gameObject;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }

    private static void Fail(string msg)
    {
        Debug.LogError("[Store Setup] " + msg);
        EditorUtility.DisplayDialog("Store Setup", msg, "OK");
    }
}
