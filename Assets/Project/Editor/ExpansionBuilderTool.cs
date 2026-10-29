using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Dựng phần mở rộng bằng các khối kiến trúc của gói ModernSupermarket (sàn, tường, trần):
///  15. Build Warehouse          : chọn 1 object đánh dấu vị trí (mũi tên xanh dương = hướng vào trong) -> nhà kho 8x6m
///                                 có cửa lớn + 3 kệ kho thùng, ẩn tới khi mua.
///  16. Build Store Expansion    : chọn các mảng TƯỜNG NGOÀI của siêu thị muốn mở -> dựng phòng mở rộng phía ngoài,
///                                 mua xong tường biến mất, phòng mới nối liền siêu thị.
/// Xong nhớ Bake lại NavMesh (chọn "NavMesh Manager" > Bake) rồi Ctrl+S.
/// </summary>
public static class ExpansionBuilderTool
{
    private const string Arch = "Assets/Assets_ThirdParty/ModernSupermarket/Prefabs/Architecture";
    private const string RackPrefab = "Assets/Project/Prefabs/Furniture/Storage_Rack.prefab";
    private const string StoreRoot = "Supermarket_full";
    /// <summary>Bật khi chạy tự động (không hiện hộp thoại).</summary>
    public static bool Silent;

    private static void Dialog(string title, string msg)
    {
        Debug.Log($"[Expansion] {title}: {msg}");
        if (!Silent) EditorUtility.DisplayDialog(title, msg, "OK");
    }

    // ================= 15. Nhà kho =================
    [MenuItem("Tools/Store Setup/15. Build Warehouse (chọn 1 object đánh dấu vị trí)")]
    private static void BuildWarehouse()
    {
        GameObject spot = Selection.activeGameObject;
        if (spot == null || EditorUtility.IsPersistent(spot))
        {
            Dialog("Warehouse", "Tạo 1 GameObject rỗng đặt ở chỗ muốn làm CỬA nhà kho (trên mặt đất),\n" +
                "xoay cho mũi tên xanh dương (Z) chỉ vào phía trong nhà kho, chọn nó rồi chạy lại.");
            return;
        }
        float width = 8f, depth = 6f;
        Undo.SetCurrentGroupName("Build warehouse");
        var root = new GameObject("Warehouse").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Warehouse");
        root.SetPositionAndRotation(spot.transform.position, Quaternion.Euler(0f, spot.transform.eulerAngles.y, 0f));

        BuildRoom(root, width, depth, frontWall: true, frontDoor: true, out float wallHeight);

        // Kệ kho: kín tường sau + dọc hai tường bên (chừa lối vào ở cửa)
        GameObject rack = AssetDatabase.LoadAssetAtPath<GameObject>(RackPrefab);
        int racks = 0;
        if (rack != null)
        {
            Bounds rb = LocalBounds(rack);
            bool longX = rb.size.x >= rb.size.z;
            float len = longX ? rb.size.x : rb.size.z, deep = longX ? rb.size.z : rb.size.x;
            Quaternion along = longX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);   // chạy dọc trục X của nhà kho
            const float gap = 0.1f, margin = 0.25f;

            // Tường sau
            int back = Mathf.Max(1, Mathf.FloorToInt((width - 2f * margin + gap) / (len + gap)));
            for (int i = 0; i < back; i++)
            {
                float x = (i - (back - 1) * 0.5f) * (len + gap);
                AddRack(rack, root, along, new Vector3(x, 0f, depth - deep * 0.5f - margin), rb); racks++;
            }
            // Hai tường bên: từ sau kệ tường sau tới cách cửa 2m
            float zFrom = 2f, zTo = depth - deep - margin - 0.9f;
            int side = Mathf.Max(0, Mathf.FloorToInt((zTo - zFrom + gap) / (len + gap)));
            Quaternion across = along * Quaternion.Euler(0f, 90f, 0f);
            for (int i = 0; i < side; i++)
            {
                float z = zFrom + len * 0.5f + i * (len + gap);
                AddRack(rack, root, across, new Vector3(-width / 2f + deep * 0.5f + margin, 0f, z), rb); racks++;
                AddRack(rack, root, across, new Vector3(width / 2f - deep * 0.5f - margin, 0f, z), rb); racks++;
            }
        }

        StoreExpansion exp = CreateExpansion(root, "Warehouse", "A storage building next to the store for your boxes", 3, 1500f,
            new[] { root.gameObject }, new GameObject[0]);
        Finish(exp, $"Đã dựng nhà kho {width}x{depth}m (tường cao {wallHeight:F1}m) với {racks} kệ kho.");
    }

    private static void AddRack(GameObject rack, Transform root, Quaternion rot, Vector3 localCenter, Bounds rb)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(rack, root);
        Place(inst.transform, root, rot, localCenter, rb, bottomAt: 0f);
        // Kệ cố định của nhà kho: không dời được, không đưa vào danh sách nội thất trong file lưu
        PlacedFurniture pf = inst.GetComponent<PlacedFurniture>();
        if (pf != null) Object.DestroyImmediate(pf);
    }

    // ================= 16. Mở rộng siêu thị =================
    [MenuItem("Tools/Store Setup/16. Build Store Expansion (chọn các mảng tường ngoài)")]
    private static void BuildAnnex()
    {
        GameObject[] walls = Selection.gameObjects.Where(g => !EditorUtility.IsPersistent(g)).ToArray();
        if (walls.Length == 0 || !TryWorldBounds(walls, out Bounds wb))
        {
            Dialog("Store Expansion", "Chọn 1 hoặc nhiều mảng TƯỜNG NGOÀI liền nhau của siêu thị (giữ Ctrl để chọn nhiều) rồi chạy lại.\n" +
                "Phòng mở rộng sẽ được dựng ở phía ngoài các mảng tường đó.");
            return;
        }
        Vector3 storeCenter = GameObject.Find(StoreRoot) is GameObject store && TryWorldBounds(new[] { store }, out Bounds sb) ? sb.center : wb.center - Vector3.forward;
        Vector3 outward = wb.center - storeCenter; outward.y = 0f;
        // Tường chạy dọc trục dài hơn của khung chọn -> hướng ra ngoài là trục còn lại
        Vector3 wallDir = wb.size.x >= wb.size.z ? Vector3.right : Vector3.forward;
        Vector3 normal = wallDir == Vector3.right ? Vector3.forward : Vector3.right;
        if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
        float width = wallDir == Vector3.right ? wb.size.x : wb.size.z;
        float thickness = wallDir == Vector3.right ? wb.size.z : wb.size.x;

        // Độ cao sàn bên trong siêu thị
        float floorY = wb.min.y;
        if (Physics.Raycast(wb.center - normal * 1.2f + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            floorY = hit.point.y;

        int index = Object.FindObjectsByType<StoreExpansion>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(e => e.name.StartsWith("Expansion_Store")) + 1;
        Undo.SetCurrentGroupName("Build store expansion");
        var root = new GameObject($"StoreExpansion_{index}").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Expansion");
        Vector3 origin = new Vector3(wb.center.x, floorY, wb.center.z) + normal * (thickness * 0.5f);
        root.SetPositionAndRotation(origin, Quaternion.LookRotation(normal));

        BuildRoom(root, width, 6f, frontWall: false, frontDoor: false, out _);

        // Tường cũ: mua xong thì ẩn. Không tính vào NavMesh khi bake (để phòng mới nối liền),
        // nhưng có NavMeshObstacle cắt đường khi tường còn hiện -> chưa mua thì khách không đi qua được.
        foreach (GameObject w in walls)
        {
            NavMeshModifier mod = w.GetComponent<NavMeshModifier>();
            if (mod == null) mod = Undo.AddComponent<NavMeshModifier>(w);
            mod.ignoreFromBuild = true;
            NavMeshObstacle obs = w.GetComponent<NavMeshObstacle>();
            if (obs == null) obs = Undo.AddComponent<NavMeshObstacle>(w);
            Bounds lb = LocalBounds(w);
            Vector3 ls = w.transform.lossyScale; // NavMeshObstacle tính theo toạ độ local (trước scale)
            obs.shape = NavMeshObstacleShape.Box;
            obs.center = Div(lb.center, ls);
            obs.size = Vector3.Max(Div(lb.size, ls), Vector3.one * 0.05f);
            obs.carving = true;
            if (PrefabUtility.IsPartOfPrefabInstance(w)) { PrefabUtility.RecordPrefabInstancePropertyModifications(mod); PrefabUtility.RecordPrefabInstancePropertyModifications(obs); }
        }

        // Kệ / tủ kê sát bức tường này: mua xong dời ra sát tường mới để mở lối đi vào phần mở rộng
        var movers = new List<Transform>();
        Transform furnitureRoot = GameObject.Find(StoreRoot)?.transform.Find("Furniture");
        if (furnitureRoot != null)
        {
            Vector3 along = wallDir;
            float d0 = Vector3.Dot(wb.center, normal);
            float sMin = Vector3.Dot(wb.min, along), sMax = Vector3.Dot(wb.max, along);
            foreach (Transform f in furnitureRoot)
            {
                if (!f.gameObject.activeInHierarchy || !TryWorldBounds(new[] { f.gameObject }, out Bounds fb)) continue;
                float a = Vector3.Dot(fb.min, normal), b = Vector3.Dot(fb.max, normal);
                float dMin = Mathf.Min(a, b), dMax = Mathf.Max(a, b);
                float s1 = Vector3.Dot(fb.min, along), s2 = Vector3.Dot(fb.max, along);
                float lo = Mathf.Min(s1, s2), hi = Mathf.Max(s1, s2);
                bool againstWall = dMax <= d0 + 0.25f && dMin >= d0 - 1.35f;
                bool insideSpan = lo >= sMin - 0.15f && hi <= sMax + 0.15f;
                if (!againstWall || !insideSpan) continue;
                movers.Add(f);
                // Bỏ "Batching Static": mesh gộp tĩnh không dời theo transform khi chơi
                foreach (Transform ch in f.GetComponentsInChildren<Transform>(true))
                {
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(ch.gameObject);
                    if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                    {
                        Undo.RecordObject(ch.gameObject, "Static flags");
                        GameObjectUtility.SetStaticEditorFlags(ch.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                    }
                }
                // Không nướng vào NavMesh, thay bằng NavMeshObstacle để dời đi vẫn đúng đường đi
                NavMeshModifier fm = f.GetComponent<NavMeshModifier>();
                if (fm == null) fm = Undo.AddComponent<NavMeshModifier>(f.gameObject);
                fm.ignoreFromBuild = true;
                NavMeshObstacle fo = f.GetComponent<NavMeshObstacle>();
                if (fo == null) fo = Undo.AddComponent<NavMeshObstacle>(f.gameObject);
                Bounds flb = LocalBounds(f.gameObject);
                Vector3 fls = f.lossyScale;
                fo.shape = NavMeshObstacleShape.Box;
                fo.center = Div(flb.center, fls);
                fo.size = Vector3.Max(Div(flb.size, fls), Vector3.one * 0.05f);
                fo.carving = true;
                if (PrefabUtility.IsPartOfPrefabInstance(f)) { PrefabUtility.RecordPrefabInstancePropertyModifications(fm); PrefabUtility.RecordPrefabInstancePropertyModifications(fo); }
            }
        }

        int[] levels = { 4, 6, 8, 10 };
        float[] prices = { 2500f, 4000f, 6000f, 8000f };
        int t = Mathf.Clamp(index - 1, 0, levels.Length - 1);
        StoreExpansion exp = CreateExpansion(root, $"Store Expansion #{index}", "More floor space for shelves and fridges", levels[t], prices[t],
            new[] { root.gameObject }, walls);
        exp.order = 10 + index;
        exp.moveOnUnlock = movers.ToArray();
        exp.moveOffset = normal * 6f;
        Finish(exp, $"Đã dựng phòng mở rộng rộng {width:F1}m x sâu 6m, mở thông qua {walls.Length} mảng tường; {movers.Count} kệ/tủ sát tường sẽ được dời ra tường mới khi mua.");
    }

    // ================= 17. Tường chặn cả 2 mặt =================
    private const string ColliderDir = "Assets/Project/Generated/Colliders";

    /// <summary>
    /// Tường / trần / sàn của gói ModernSupermarket dùng MeshCollider là mặt phẳng 1 mặt:
    /// từ trong ra bị chặn nhưng từ ngoài đi xuyên vào được. Tạo mesh va chạm 2 mặt rồi gán cho prefab + scene.
    /// </summary>
    [MenuItem("Tools/Store Setup/17. Fix Wall Colliders (chặn cả 2 mặt tường)")]
    public static void FixWallColliders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Project/Generated")) AssetDatabase.CreateFolder("Assets/Project", "Generated");
        if (!AssetDatabase.IsValidFolder(ColliderDir)) AssetDatabase.CreateFolder("Assets/Project/Generated", "Colliders");
        var cache = new Dictionary<Mesh, Mesh>();
        int prefabs = 0, sceneCount = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Arch }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
                if (TryDoubleSide(mc, cache)) dirty = true;
            if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (MeshCollider mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(mc)) continue;
            Undo.RecordObject(mc, "Double-sided collider");
            if (!TryDoubleSide(mc, cache)) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(mc)) PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
            sceneCount++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Dialog("Wall colliders", $"Đã tạo {cache.Count} mesh va chạm 2 mặt, sửa {prefabs} prefab và {sceneCount} collider trong scene.\nNhấn Ctrl+S để lưu scene.");
    }

    private static bool TryDoubleSide(MeshCollider mc, Dictionary<Mesh, Mesh> cache)
    {
        Mesh src = mc.sharedMesh;
        if (src == null || mc.convex) return false;
        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath) || srcPath.StartsWith(ColliderDir) || !srcPath.Contains("ModernSupermarket")) return false;
        Vector3 size = src.bounds.size;
        if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) > 0.25f) return false; // chỉ các tấm mỏng (tường, trần, sàn)

        if (!cache.TryGetValue(src, out Mesh twoSided))
        {
            string file = $"{ColliderDir}/{src.name.Replace('/', '_')}_{AssetDatabase.AssetPathToGUID(srcPath).Substring(0, 6)}_2s.asset";
            twoSided = AssetDatabase.LoadAssetAtPath<Mesh>(file);
            if (twoSided == null)
            {
                twoSided = BuildDoubleSided(src);
                AssetDatabase.CreateAsset(twoSided, file);
            }
            cache[src] = twoSided;
        }
        mc.sharedMesh = twoSided;
        return true;
    }

    private static Mesh BuildDoubleSided(Mesh src)
    {
        Vector3[] verts = src.vertices;
        var tris = new List<int>();
        for (int sub = 0; sub < src.subMeshCount; sub++) tris.AddRange(src.GetTriangles(sub));
        int n = tris.Count;
        for (int i = 0; i + 2 < n; i += 3) { tris.Add(tris[i]); tris.Add(tris[i + 2]); tris.Add(tris[i + 1]); }
        var m = new Mesh { name = src.name + "_2s" };
        if (verts.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts;
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    // ================= Dựng phòng =================
    /// <summary>Phòng chữ nhật trong toạ độ của root: gốc ở giữa mép trước, +Z vào trong. Sàn ở y = 0.</summary>
    private static void BuildRoom(Transform root, float width, float depth, bool frontWall, bool frontDoor, out float wallHeight)
    {
        GameObject floor = Load("Floor"), wall = Load("Wall"), door = Load("WallDoorLarge") ?? Load("WallDoor"), ceiling = Load("Ceiling");
        wallHeight = 3f;

        if (floor != null)
        {
            Bounds fb = LocalBounds(floor);
            Tile(root, floor, fb, width, depth, 0f, topAt: true);
        }
        if (wall != null)
        {
            Bounds wlb = LocalBounds(wall);
            wallHeight = wlb.size.y;
            Vector3 mid = new Vector3(0f, 0f, depth * 0.5f);
            Edge(root, wall, null, new Vector3(-width / 2f, 0f, depth), new Vector3(width / 2f, 0f, depth), mid);   // sau
            Edge(root, wall, null, new Vector3(-width / 2f, 0f, 0f), new Vector3(-width / 2f, 0f, depth), mid);     // trái
            Edge(root, wall, null, new Vector3(width / 2f, 0f, 0f), new Vector3(width / 2f, 0f, depth), mid);       // phải
            if (frontWall) Edge(root, wall, frontDoor ? door : null, new Vector3(-width / 2f, 0f, 0f), new Vector3(width / 2f, 0f, 0f), mid);
        }
        if (ceiling != null)
        {
            Bounds cb = LocalBounds(ceiling);
            Tile(root, ceiling, cb, width, depth, wallHeight, topAt: false);
            // Mái nhìn từ bên ngoài: tấm trần lật ngửa lên trên
            Tile(root, ceiling, cb, width, depth, wallHeight + 0.03f, topAt: false, Quaternion.Euler(0f, 0f, 180f));
        }

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(0f, wallHeight - 0.4f, depth * 0.5f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = Mathf.Max(width, depth) * 1.2f;
        l.intensity = 3f;
        l.color = new Color(1f, 0.96f, 0.88f);
        l.shadows = LightShadows.None;
    }

    /// <summary>Lát kín hình chữ nhật bằng các tấm (sàn / trần), co giãn cho vừa khít.</summary>
    private static void Tile(Transform root, GameObject prefab, Bounds b, float width, float depth, float y, bool topAt, Quaternion? rot = null)
    {
        Quaternion q = rot ?? Quaternion.identity;
        int nx = Mathf.Max(1, Mathf.RoundToInt(width / Mathf.Max(0.1f, b.size.x)));
        int nz = Mathf.Max(1, Mathf.RoundToInt(depth / Mathf.Max(0.1f, b.size.z)));
        Vector3 scale = new Vector3(width / (nx * b.size.x), 1f, depth / (nz * b.size.z));
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                inst.transform.localScale = Vector3.Scale(inst.transform.localScale, scale);
                Bounds sb = new Bounds(Vector3.Scale(b.center, scale), Vector3.Scale(b.size, scale));
                Vector3 c = new Vector3(-width / 2f + (i + 0.5f) * width / nx, 0f, (j + 0.5f) * depth / nz);
                Place(inst.transform, root, q, c, sb, topAt ? y - sb.size.y : y);
            }
    }

    /// <summary>Dựng tường dọc một cạnh (a -> b), chia đều thành các mảng; mảng giữa có thể là cửa.
    /// Mặt chính của mảng tường (trục Z của nó) quay vào trong phòng; thêm một mảng quay lưng ra ngoài để nhìn từ ngoài cũng thấy tường.</summary>
    private static void Edge(Transform root, GameObject wall, GameObject middle, Vector3 a, Vector3 b, Vector3 roomCenter)
    {
        Vector3 d = b - a;
        float length = d.magnitude;
        Vector3 dir = d / length;
        Bounds wb = LocalBounds(wall);
        bool longX = wb.size.x >= wb.size.z;
        float pieceLen = longX ? wb.size.x : wb.size.z;
        int n = Mathf.Max(1, Mathf.RoundToInt(length / pieceLen));
        if (middle != null && n % 2 == 0) n++; // số lẻ để có mảng chính giữa làm cửa
        for (int i = 0; i < n; i++)
        {
            GameObject prefab = middle != null && i == n / 2 ? middle : wall;
            Bounds pb = LocalBounds(prefab);
            bool px = pb.size.x >= pb.size.z;
            float plen = px ? pb.size.x : pb.size.z;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            float k = (length / n) / plen; // co giãn theo chiều dài cho khít
            Vector3 s = inst.transform.localScale;
            if (px) s.x *= k; else s.z *= k;
            inst.transform.localScale = s;
            Bounds sb = new Bounds(new Vector3(pb.center.x * (px ? k : 1f), pb.center.y, pb.center.z * (px ? 1f : k)),
                                   new Vector3(pb.size.x * (px ? k : 1f), pb.size.y, pb.size.z * (px ? 1f : k)));
            Vector3 c = a + dir * (length / n) * (i + 0.5f);
            Vector3 inward = Vector3.Cross(dir, Vector3.up);
            Vector3 toCenter = roomCenter - c; toCenter.y = 0f;
            if (Vector3.Dot(inward, toCenter) < 0f) inward = -inward;
            // Prefab dài theo X: Z của nó là pháp tuyến; dài theo Z: X của nó là pháp tuyến
            Quaternion r = px ? Quaternion.LookRotation(inward) : Quaternion.LookRotation(Vector3.Cross(inward, Vector3.up));
            Place(inst.transform, root, r, c, sb, 0f);

            // Mặt ngoài
            var outer = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            outer.name = prefab.name + "_Outside";
            outer.transform.localScale = s;
            Place(outer.transform, root, r * Quaternion.Euler(0f, 180f, 0f), c, sb, 0f);
        }
    }

    /// <summary>Đặt object (đã scale) để tâm khung của nó nằm ở localCenter (x,z) và đáy ở bottomAt, trong toạ độ root.</summary>
    private static void Place(Transform t, Transform root, Quaternion localRot, Vector3 localCenter, Bounds b, float bottomAt)
    {
        t.localRotation = localRot;
        Vector3 offset = localRot * b.center;
        Vector3 targetCenter = new Vector3(localCenter.x, bottomAt + b.extents.y, localCenter.z);
        t.localPosition = targetCenter - offset;
    }

    // ================= Tiện ích =================
    private static StoreExpansion CreateExpansion(Transform near, string name, string description, int level, float price, GameObject[] show, GameObject[] hide)
    {
        var go = new GameObject("Expansion_" + name.Replace(" ", "").Replace("#", ""));
        Undo.RegisterCreatedObjectUndo(go, "Expansion");
        go.transform.position = near.position;
        StoreExpansion e = go.AddComponent<StoreExpansion>();
        e.id = go.name.ToLower();
        e.displayName = name;
        e.description = description;
        e.requiredLevel = level;
        e.price = price;
        e.showOnUnlock = show;
        e.hideOnUnlock = hide;
        return e;
    }

    private static void Finish(StoreExpansion e, string msg)
    {
        EditorSceneManager.MarkSceneDirty(e.gameObject.scene);
        Selection.activeGameObject = e.gameObject;
        string full = msg + $"\nGiá: cấp {e.requiredLevel}, {e.price}$ (sửa trong component StoreExpansion trên '{e.name}').\n\n" +
                      "BƯỚC TIẾP: chọn 'NavMesh Manager' trong Hierarchy > NavMeshSurface > Bake, rồi Ctrl+S.";
        Dialog("Expansion", full);
    }

    private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Arch}/{name}.prefab");

    /// <summary>Khung bao renderer của prefab / object theo trục của chính nó (đơn vị mét thật, đã gồm scale hiện tại).</summary>
    private static Bounds LocalBounds(GameObject go)
    {
        bool isAsset = EditorUtility.IsPersistent(go);
        GameObject inst = isAsset ? (GameObject)PrefabUtility.InstantiatePrefab(go) : go;
        try
        {
            Matrix4x4 toLocal = Matrix4x4.TRS(inst.transform.position, inst.transform.rotation, Vector3.one).inverse;
            Bounds b = default; bool any = false;
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                Bounds rb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = rb.center + Vector3.Scale(rb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = toLocal.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one);
        }
        finally { if (isAsset) Object.DestroyImmediate(inst); }
    }

    private static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / Safe(b.x), a.y / Safe(b.y), a.z / Safe(b.z));
    private static float Safe(float v) => Mathf.Approximately(v, 0f) ? 1f : v;

    private static bool TryWorldBounds(IEnumerable<GameObject> gos, out Bounds b)
    {
        b = default; bool any = false;
        foreach (GameObject g in gos)
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
        return any;
    }
}
