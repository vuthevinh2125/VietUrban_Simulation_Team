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

    // ================= 15. Nhà kho =================
    [MenuItem("Tools/Store Setup/15. Build Warehouse (chọn 1 object đánh dấu vị trí)")]
    private static void BuildWarehouse()
    {
        GameObject spot = Selection.activeGameObject;
        if (spot == null || EditorUtility.IsPersistent(spot))
        {
            EditorUtility.DisplayDialog("Warehouse", "Tạo 1 GameObject rỗng đặt ở chỗ muốn làm CỬA nhà kho (trên mặt đất),\n" +
                "xoay cho mũi tên xanh dương (Z) chỉ vào phía trong nhà kho, chọn nó rồi chạy lại.", "OK");
            return;
        }
        float width = 8f, depth = 6f;
        Undo.SetCurrentGroupName("Build warehouse");
        var root = new GameObject("Warehouse").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Warehouse");
        root.SetPositionAndRotation(spot.transform.position, Quaternion.Euler(0f, spot.transform.eulerAngles.y, 0f));

        BuildRoom(root, width, depth, frontWall: true, frontDoor: true, out float wallHeight);

        // Kệ kho dọc tường sau
        GameObject rack = AssetDatabase.LoadAssetAtPath<GameObject>(RackPrefab);
        int racks = 0;
        if (rack != null)
        {
            Bounds rb = LocalBounds(rack);
            bool longX = rb.size.x >= rb.size.z;
            float len = longX ? rb.size.x : rb.size.z, deep = longX ? rb.size.z : rb.size.x;
            int count = Mathf.Clamp(Mathf.FloorToInt((width - 0.6f) / (len + 0.2f)), 1, 3);
            for (int i = 0; i < count; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(rack, root);
                Quaternion r = longX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                float x = (i - (count - 1) * 0.5f) * (len + 0.2f);
                Vector3 localCenter = new Vector3(x, 0f, depth - deep * 0.5f - 0.25f);
                Place(inst.transform, root, r, localCenter, rb, bottomAt: 0f);
                // Kệ cố định của nhà kho: không dời được, không đưa vào danh sách nội thất trong file lưu
                PlacedFurniture pf = inst.GetComponent<PlacedFurniture>();
                if (pf != null) Object.DestroyImmediate(pf);
                racks++;
            }
        }

        StoreExpansion exp = CreateExpansion(root, "Warehouse", "A storage building next to the store for your boxes", 3, 1500f,
            new[] { root.gameObject }, new GameObject[0]);
        Finish(exp, $"Đã dựng nhà kho {width}x{depth}m (tường cao {wallHeight:F1}m) với {racks} kệ kho.");
    }

    // ================= 16. Mở rộng siêu thị =================
    [MenuItem("Tools/Store Setup/16. Build Store Expansion (chọn các mảng tường ngoài)")]
    private static void BuildAnnex()
    {
        GameObject[] walls = Selection.gameObjects.Where(g => !EditorUtility.IsPersistent(g)).ToArray();
        if (walls.Length == 0 || !TryWorldBounds(walls, out Bounds wb))
        {
            EditorUtility.DisplayDialog("Store Expansion", "Chọn 1 hoặc nhiều mảng TƯỜNG NGOÀI liền nhau của siêu thị (giữ Ctrl để chọn nhiều) rồi chạy lại.\n" +
                "Phòng mở rộng sẽ được dựng ở phía ngoài các mảng tường đó.", "OK");
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

        int[] levels = { 4, 6, 8, 10 };
        float[] prices = { 2500f, 4000f, 6000f, 8000f };
        int t = Mathf.Clamp(index - 1, 0, levels.Length - 1);
        StoreExpansion exp = CreateExpansion(root, $"Store Expansion #{index}", "More floor space for shelves and fridges", levels[t], prices[t],
            new[] { root.gameObject }, walls);
        exp.order = 10 + index;
        Finish(exp, $"Đã dựng phòng mở rộng rộng {width:F1}m x sâu 6m, mở thông qua {walls.Length} mảng tường.");
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
            Edge(root, wall, null, new Vector3(-width / 2f, 0f, depth), new Vector3(width / 2f, 0f, depth));   // sau
            Edge(root, wall, null, new Vector3(-width / 2f, 0f, 0f), new Vector3(-width / 2f, 0f, depth));     // trái
            Edge(root, wall, null, new Vector3(width / 2f, 0f, 0f), new Vector3(width / 2f, 0f, depth));       // phải
            if (frontWall) Edge(root, wall, frontDoor ? door : null, new Vector3(-width / 2f, 0f, 0f), new Vector3(width / 2f, 0f, 0f));
        }
        if (ceiling != null)
        {
            Bounds cb = LocalBounds(ceiling);
            Tile(root, ceiling, cb, width, depth, wallHeight, topAt: false);
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
    private static void Tile(Transform root, GameObject prefab, Bounds b, float width, float depth, float y, bool topAt)
    {
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
                Place(inst.transform, root, Quaternion.identity, c, sb, topAt ? y - sb.size.y : y);
            }
    }

    /// <summary>Dựng tường dọc một cạnh (a -> b), chia đều thành các mảng; mảng giữa có thể là cửa.</summary>
    private static void Edge(Transform root, GameObject wall, GameObject middle, Vector3 a, Vector3 b)
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
            Quaternion r = px ? Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up)) : Quaternion.LookRotation(dir);
            Vector3 c = a + dir * (length / n) * (i + 0.5f);
            Place(inst.transform, root, r, c, sb, 0f);
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
        Debug.Log("[Expansion] " + full);
        EditorUtility.DisplayDialog("Expansion", full, "OK");
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
