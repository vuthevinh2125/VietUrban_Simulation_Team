using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dựng thành phố Việt (VietDistrict) bao quanh siêu thị, siêu thị ở trung tâm:
///  1. Cất các phần thành phố kiểu Mỹ cũ (đường POLYGON, cây xăng, bệnh viện, sở cứu hoả, đồn cảnh sát, khu phố mới phía đông...)
///     vào object tắt "_Archived_OldCity" (không xoá, menu 20b khôi phục được).
///  2. Đo mặt bằng siêu thị (cửa hàng, kho, phần mở rộng, bãi đỗ, điểm giao hàng, điểm khách ra vào) để chừa đúng ô trung tâm.
///  3. Dựng khu phố (scene chỉ lưu object gốc + component, khối dựng lại bằng code mỗi lần mở scene).
/// Xong: chọn 'NavMesh Manager' > Bake, rồi Ctrl+S.
/// </summary>
[InitializeOnLoad]
public static class VietDistrictTool
{
    private const string Props = "Assets/POLYGON city pack/Prefabs/Props/";
    private const string Lamps = "Assets/POLYGON city pack/Prefabs/Lamps/";
    private const string ArchiveName = "_Archived_OldCity";

    private static readonly string[] OldCity =
    {
        "CityExpansion_East", "FireDepartment", "Gas + car station", "Hospital", "PoliceStation", "ResidentialLine", "ResidentialLine (1)",
        "ResidentialLine (2)", "Electricity", "Electricity (1)", "RoadLane3", "RoadLine", "Roadlane2", "Station pavement", "LeftFuelPavement",
        "FreeLand", "Supermarket", "Wall_Green_HalfDetail",
    };
    private static readonly string[] OldPrefixes = { "Building_", "Street 3 Prefab" };
    private static readonly string[] StoreRoots =
    {
        "Supermarket_full", "Warehouse", "StoreExpansion_1", "StoreExpansion_2", "StoreSections", "Expansion_StoreExpansion1",
        "Expansion_StoreExpansion2", "Expansion_Warehouse", "StoreSign_OpenClosed", "Parking_checkOut_prefab", "Floor_Squared_Gray", "CheckOutItemGroup",
    };
    private static readonly string[] StorePoints = { "DeliveryPoint", "Exit_Point", "Pay_Point", "Shopping_Point", "CustomerSpawner", "Player" };

    static VietDistrictTool()
    {
        // Trước khi vào Play: xoá khối dựng trong Editor (không lưu vào scene) để khỏi trùng với khối dựng lúc chơi
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.ExitingEditMode) return;
            foreach (VietDistrict d in Object.FindObjectsByType<VietDistrict>(FindObjectsInactive.Include))
                for (int i = d.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(d.transform.GetChild(i).gameObject);
        };
    }

    [MenuItem("Tools/Store Setup/20. Build Viet City (sieu thi o trung tam)")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Viet City",
                "Sẽ cất thành phố cũ (đường, cây xăng, bệnh viện, khu phố phía đông...) vào '_Archived_OldCity' (tắt, không xoá) " +
                "và dựng thành phố Việt bao quanh siêu thị. Tiếp tục?", "Dựng", "Huỷ")) return;

        int archived = ArchiveOldCity(scene);
        Rect store = MeasureStore(scene, out string note);

        GameObject go = GameObject.Find(VietDistrict.RootName);
        if (go == null)
        {
            go = new GameObject(VietDistrict.RootName);
            Undo.RegisterCreatedObjectUndo(go, "Viet city");
        }
        go.SetActive(true);
        VietDistrict d = go.GetComponent<VietDistrict>();
        if (d == null) d = Undo.AddComponent<VietDistrict>(go);
        Undo.RecordObject(d, "Viet city");
        d.storeRect = store;
        d.lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Lamps + "street_lamp 1 prefab.prefab");
        d.benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Bench 2 prefab.prefab");
        string models = SetupRealModels(d);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        float t0 = Time.realtimeSinceStartup;
        d.Rebuild();
        float secs = Time.realtimeSinceStartup - t0;

        EditorSceneManager.MarkSceneDirty(go.scene);
        string msg = $"Đã cất {archived} nhóm object của thành phố cũ vào '{ArchiveName}'.\n" +
                     $"Mặt bằng siêu thị: x {store.xMin:0} .. {store.xMax:0}, z {store.yMin:0} .. {store.yMax:0}{note}\n" +
                     $"Thành phố Việt: {d.City.width:0} x {d.City.height:0} m, {d.Edges.Count} đoạn phố, {d.AddressCount} địa chỉ giao hàng (dựng {secs:0.0} s).\n" +
                     models + "\n\n" +
                     "BƯỚC TIẾP: chọn 'NavMesh Manager' > Bake (có thể mất 1 - 3 phút), rồi Ctrl+S.";
        Debug.Log("[VietCity] " + msg);
        EditorUtility.DisplayDialog("Viet City", msg, "OK");
        Selection.activeGameObject = go;
    }

    /// <summary>
    /// Model thật cho người: model FBX humanoid của các NPC khách (bỏ model lao công) + prefab NPC để lấy vật liệu.
    /// Bật Read/Write cho các FBX đó (cần để nướng tư thế lúc chạy, kể cả bản build).
    /// </summary>
    private static string SetupRealModels(VietDistrict d)
    {
        var models = new List<GameObject>();
        var mats = new List<GameObject>();
        var paths = new HashSet<string>();
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null && spawner.customerPrefabs != null)
            foreach (GameObject p in spawner.customerPrefabs)
            {
                if (p == null || p.name == StaffManager.JanitorModel) continue;
                SkinnedMeshRenderer smr = p.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null || smr.sharedMesh == null) continue;
                string path = AssetDatabase.GetAssetPath(smr.sharedMesh);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentInChildren<Animator>(true) == null || models.Contains(model)) continue;
                models.Add(model);
                mats.Add(p);
                paths.Add(path);
            }
        int changed = 0;
        foreach (string path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp) || imp.isReadable) continue;
            imp.isReadable = true;
            imp.SaveAndReimport();
            changed++;
        }
        d.peopleModels = models.ToArray();
        d.peopleMaterials = mats.ToArray();
        d.bikePrefabs = new GameObject[0]; // xe máy công cộng dựng bằng code (BikeModels), không dùng xe của người chơi
        CopyTaxi();
        return $"Model thật: {models.Count} người" + (changed > 0 ? $" (đã bật Read/Write cho {changed} file FBX)" : "") + ".";
    }

    /// <summary>Đưa xe Taxi (gói Free American Sedans) vào Resources/Vehicles để dòng xe ô tô dùng được lúc chạy.</summary>
    private static void CopyTaxi()
    {
        const string src = "Assets/High Matters/Free American Sedans/Prefabs/Taxi.prefab", dst = "Assets/Resources/Vehicles/Taxi.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null || AssetDatabase.LoadAssetAtPath<GameObject>(src) == null) return;
        AssetDatabase.CopyAsset(src, dst);
    }

    [MenuItem("Tools/Store Setup/20b. Restore Old City (bo thanh pho Viet)")]
    public static void Restore()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject archive = null;
        foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == ArchiveName) archive = r;
        if (archive != null)
        {
            for (int i = archive.transform.childCount - 1; i >= 0; i--)
                Undo.SetTransformParent(archive.transform.GetChild(i), null, "Restore old city");
            Undo.DestroyObjectImmediate(archive);
        }
        GameObject go = GameObject.Find(VietDistrict.RootName);
        if (go != null) Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Viet City", "Đã khôi phục thành phố cũ và gỡ thành phố Việt. Nhớ Bake NavMesh lại rồi Ctrl+S.", "OK");
    }

    private static int ArchiveOldCity(Scene scene)
    {
        GameObject archive = null;
        var move = new List<GameObject>();
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (r.name == ArchiveName) { archive = r; continue; }
            bool old = System.Array.IndexOf(OldCity, r.name) >= 0;
            foreach (string p in OldPrefixes) if (r.name.StartsWith(p)) old = true;
            if (old) move.Add(r);
        }
        if (move.Count == 0) return 0;
        if (archive == null)
        {
            archive = new GameObject(ArchiveName);
            Undo.RegisterCreatedObjectUndo(archive, "Archive old city");
        }
        archive.SetActive(false);
        foreach (GameObject g in move) Undo.SetTransformParent(g.transform, archive.transform, "Archive old city");
        return move.Count;
    }

    private static Rect MeasureStore(Scene scene, out string note)
    {
        note = "";
        var roots = new Dictionary<string, GameObject>();
        foreach (GameObject r in scene.GetRootGameObjects()) if (!roots.ContainsKey(r.name)) roots[r.name] = r;
        Bounds b = default;
        bool any = false;
        void Add(Bounds x)
        {
            if (!any) { b = x; any = true; } else b.Encapsulate(x);
        }
        foreach (string n in StoreRoots)
        {
            if (!roots.TryGetValue(n, out GameObject g)) continue;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Bounds rb = r.bounds;
                if (rb.size.x > 120f || rb.size.z > 120f || rb.size.sqrMagnitude < 1e-6f) continue;
                Add(rb);
            }
        }
        foreach (string n in StorePoints)
            if (roots.TryGetValue(n, out GameObject g)) Add(new Bounds(g.transform.position, Vector3.one * 2f));
        if (!any)
        {
            note = " (không tìm thấy object siêu thị -> dùng mặc định)";
            return Rect.MinMaxRect(-150f, 16f, -70f, 64f);
        }
        float xMin = b.min.x - 3f, xMax = b.max.x + 3f, zMax = b.max.z + 3f;
        const float front = 16f; // mép vỉa hè trước cửa (đại lộ cũ z 0 .. 16)
        if (b.min.z < front - 1f) note = $" (lưu ý: có phần cửa hàng thò ra tới z = {b.min.z:0.0}, trước mép vỉa hè z = 16)";
        if (xMax - xMin < 40f) { float c = (xMin + xMax) * 0.5f; xMin = c - 20f; xMax = c + 20f; }
        if (zMax - front < 30f) zMax = front + 30f;
        return Rect.MinMaxRect(xMin, front, xMax, zMax);
    }
}
