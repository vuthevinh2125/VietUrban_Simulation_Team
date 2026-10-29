using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 19. Dựng khu phố mới phía ĐÔNG thành phố (bằng các prefab của POLYGON City Pack) để đi giao hàng xa hơn:
///  - Nối tiếp đại lộ chính (z 0..16) sang phía đông, thêm 4 đường ngang + 3 đường dọc, ngã tư có vạch qua đường.
///  - 12 khu nhà: nền vỉa hè, nhà cửa quay mặt ra đường (mỗi nhà là một địa chỉ giao hàng), đèn đường, cây, ghế, thùng rác.
///  - Nối với đường dọc cũ (x 14..30) ở hai đầu nam / bắc.
/// Chạy lại sẽ xoá khu cũ rồi dựng lại (cùng hạt giống ngẫu nhiên nên giống hệt). Xong nhớ Bake NavMesh rồi Ctrl+S.
/// </summary>
public static class CityExpansionTool
{
    private const string Floor = "Assets/POLYGON city pack/Prefabs/Floor/";
    private const string Builds = "Assets/POLYGON city pack/Prefabs/Buildings/";
    private const string Props = "Assets/POLYGON city pack/Prefabs/Props/";
    private const string Lamps = "Assets/POLYGON city pack/Prefabs/Lamps/";
    private const string OutDir = "Assets/Project/Generated/City";
    private const string RootName = "CityExpansion_East";
    private const float RoadY = 0f;      // prefab đường có mặt ở y = 0.1
    private const float LotTop = 0.18f;

    private class RoadDef
    {
        public bool alongX; public float c0, c1, a0, a1; public string name; public int lanes;
        public RoadDef(bool alongX, float c0, float c1, float a0, float a1, string name, int lanes = 3)
        { this.alongX = alongX; this.c0 = c0; this.c1 = c1; this.a0 = a0; this.a1 = a1; this.name = name; this.lanes = lanes; }
        public Rect Area => alongX ? Rect.MinMaxRect(a0, c0, a1, c1) : Rect.MinMaxRect(c0, a0, c1, a1);
    }

    // Hướng mặt tiền (trục cục bộ) của các toà nhà; không có trong bảng thì coi là -Z
    private static readonly Dictionary<string, Vector3> Fronts = new Dictionary<string, Vector3>
    {
        { "Fire_department_prefab", Vector3.back }, { "Police_station_prefab", Vector3.left }, { "Shop_B_prefab", Vector3.left },
        { "Car_repair_prefab", Vector3.right }, { "Building_D1_prefab", Vector3.left }, { "Building_K_prefab", Vector3.forward },
        { "Building_C1_prefab", Vector3.forward }, { "Bank_prefab", Vector3.back },
    };

    private static readonly string[] BuildingPool =
    {
        "Bank_prefab", "Fire_department_prefab", "Police_station_prefab", "Shop_A_prefab", "Shop_B_prefab", "Shop_C_prefab",
        "Car_repair_prefab", "Building_A1_prefab", "Building_C1_prefab", "Building_D1_prefab", "Building_D_prefab",
        "Building_H_prefab", "Building_I_1_prefab", "Building_I_2_Prefab", "Building_I_3_prefab", "Building_J_prefab", "Building_K_prefab",
        "Building_N_Prefab", "Building_O_PREFAB", "Building_R_Prefab", "Building_S_prefab", "Building_T_prefab", "Building_W_prefab",
        "building_X_prefab", "Building_Y_prefab", "Building_Z_Prefab", "Bulding_C_prefab 1", "Building_A_prefab",
    };

    private static System.Random rng;
    private static Transform root, roadsRoot, blocksRoot, buildingsRoot, propsRoot;
    private static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

    [MenuItem("Tools/Store Setup/19. Build City Expansion (khu phố mới phía đông)")]
    public static void Build()
    {
        rng = new System.Random(20261030);
        prefabCache.Clear();
        GameObject old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "City expansion");
        roadsRoot = Child("Roads"); blocksRoot = Child("Blocks"); buildingsRoot = Child("Buildings"); propsRoot = Child("Props");

        // ===== Mạng đường =====
        var xRoads = new List<RoadDef>
        {
            new RoadDef(true, -96f, -84f, 14f, 234f, "Tran Hung Dao St"),
            new RoadDef(true, -48f, -36f, 30f, 234f, "Le Loi St"),
            new RoadDef(true, 0f, 16f, 30f, 234f, "Nguyen Hue Ave", 4),
            new RoadDef(true, 48f, 60f, 30f, 234f, "Hai Ba Trung St"),
            new RoadDef(true, 92f, 104f, 14f, 234f, "Ly Thuong Kiet St"),
        };
        var zRoads = new List<RoadDef>
        {
            new RoadDef(false, 86f, 98f, -96f, 104f, "Pasteur St"),
            new RoadDef(false, 154f, 166f, -96f, 104f, "Dien Bien Phu St"),
            new RoadDef(false, 222f, 234f, -96f, 104f, "Vo Van Kiet St"),
        };

        // Ngã tư / chỗ giao (kể cả 2 chỗ nối với đường dọc cũ x 14..30)
        var junctions = new List<Rect>();
        foreach (RoadDef h in xRoads)
            foreach (RoadDef v in zRoads)
            {
                Rect a = h.Area, b = v.Area;
                if (a.Overlaps(b)) junctions.Add(Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax)));
            }
        junctions.Add(Rect.MinMaxRect(14f, -96f, 30f, -84f));
        junctions.Add(Rect.MinMaxRect(14f, 92f, 30f, 104f));

        foreach (Rect j in junctions) Paved(j);
        foreach (RoadDef r in xRoads.Concat(zRoads)) BuildRoad(r, junctions);

        // ===== Khu nhà =====
        float[] bx = { 30f, 86f, 98f, 154f, 166f, 222f };
        float[] bz = { -84f, -48f, -36f, 0f, 16f, 48f, 60f, 92f };
        int number = 1;
        for (int i = 0; i < bx.Length; i += 2)
            for (int k = 0; k < bz.Length; k += 2)
            {
                Rect block = Rect.MinMaxRect(bx[i], bz[k], bx[i + 1], bz[k + 1]);
                BuildBlock(block, xRoads, zRoads, ref number);
            }

        // ===== Nền cỏ bao quanh khu mới =====
        Ground(Rect.MinMaxRect(0f, -130f, 270f, 140f));

        // Tĩnh để gộp mesh, đỡ nặng
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.GetComponent<Light>() == null)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        int addresses = root.GetComponentsInChildren<DeliveryAddress>(true).Length;
        string msg = $"Đã dựng khu phố mới phía đông: {xRoads.Count + zRoads.Count} tuyến đường, 12 khu nhà, {addresses} địa chỉ giao hàng.\n\nBƯỚC TIẾP: chọn 'NavMesh Manager' > Bake, rồi Ctrl+S.";
        Debug.Log("[City] " + msg);
        if (!ExpansionBuilderTool.Silent) EditorUtility.DisplayDialog("City expansion", msg, "OK");
        Selection.activeGameObject = root.gameObject;
    }

    // ================= Đường =================
    private static void BuildRoad(RoadDef r, List<Rect> junctions)
    {
        var parent = new GameObject(r.name).transform;
        parent.SetParent(roadsRoot, false);

        // Cắt đoạn đường thành các khúc giữa các ngã tư
        var cuts = new List<(float a, float b)>();
        foreach (Rect j in junctions)
        {
            Rect area = r.Area;
            if (!area.Overlaps(j)) continue;
            cuts.Add(r.alongX ? (j.xMin, j.xMax) : (j.yMin, j.yMax));
        }
        cuts.Sort((p, q) => p.a.CompareTo(q.a));
        float cursor = r.a0;
        var spans = new List<(float a, float b, bool crossA, bool crossB)>();
        bool prevJunction = false;
        foreach (var c in cuts)
        {
            if (c.a > cursor + 0.5f) spans.Add((cursor, c.a, prevJunction, true));
            cursor = Mathf.Max(cursor, c.b);
            prevJunction = true;
        }
        if (r.a1 > cursor + 0.5f) spans.Add((cursor, r.a1, prevJunction, false));

        float width = r.c1 - r.c0;
        int strips = Mathf.RoundToInt(width / 4f);
        foreach (var s in spans)
        {
            float a = s.a, b = s.b;
            // Vạch qua đường sát ngã tư
            if (s.crossA && b - a > 10f) { Crosswalk(r, a, parent); a += 4f; }
            if (s.crossB && b - a > 10f) { Crosswalk(r, b - 4f, parent); b -= 4f; }
            for (int k = 0; k < strips; k++)
            {
                string prefab; float yaw;
                bool first = k == 0, last = k == strips - 1;
                if (first) { prefab = "Street 7 Prefab"; yaw = r.alongX ? 0f : 90f; }
                else if (last) { prefab = "Street 7 Prefab"; yaw = r.alongX ? 180f : 270f; }
                else { prefab = "Street 3 Prefab"; yaw = r.alongX ? 0f : 90f; }
                float cross = r.c0 + 2f + k * 4f;
                TileStrip(Floor + prefab + ".prefab", parent, r.alongX, cross, a, b, yaw);
            }
        }
    }

    /// <summary>Lát một dải 4m dọc theo đường từ a tới b (mỗi tấm 4m, co giãn tấm cho khít).</summary>
    private static void TileStrip(string prefabPath, Transform parent, bool alongX, float cross, float a, float b, float yaw)
    {
        float len = b - a;
        int n = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
        float piece = len / n;
        for (int i = 0; i < n; i++)
        {
            float along = a + piece * (i + 0.5f);
            Vector3 pos = alongX ? new Vector3(along, RoadY, cross) : new Vector3(cross, RoadY, along);
            GameObject g = Spawn(prefabPath, parent, pos, yaw);
            if (g == null) return;
            Vector3 sc = g.transform.localScale;
            sc.x *= piece / 4f; // trục X cục bộ của tấm luôn chạy dọc đường
            g.transform.localScale = sc;
        }
    }

    private static void Crosswalk(RoadDef r, float at, Transform parent)
    {
        float width = r.c1 - r.c0;
        int n = Mathf.RoundToInt(width / 4f);
        for (int k = 0; k < n; k++)
        {
            float cross = r.c0 + 2f + k * 4f;
            float along = at + 2f;
            Vector3 pos = r.alongX ? new Vector3(along, RoadY, cross) : new Vector3(cross, RoadY, along);
            // Sọc vằn chạy song song hướng xe đi
            Spawn(Floor + "Street 8 Prefab.prefab", parent, pos, r.alongX ? 90f : 0f);
        }
    }

    /// <summary>Mặt đường trơn ở ngã tư.</summary>
    private static void Paved(Rect area)
    {
        var parent = new GameObject("Junction").transform;
        parent.SetParent(roadsRoot, false);
        int nx = Mathf.Max(1, Mathf.RoundToInt(area.width / 4f)), nz = Mathf.Max(1, Mathf.RoundToInt(area.height / 4f));
        float px = area.width / nx, pz = area.height / nz;
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                GameObject g = Spawn(Floor + "Street 4 Prefab.prefab", parent, new Vector3(area.xMin + px * (i + 0.5f), RoadY, area.yMin + pz * (k + 0.5f)), 0f);
                if (g != null) g.transform.localScale = Vector3.Scale(g.transform.localScale, new Vector3(px / 4f, 1f, pz / 4f));
            }
    }

    // ================= Khu nhà =================
    private static void BuildBlock(Rect block, List<RoadDef> xRoads, List<RoadDef> zRoads, ref int number)
    {
        var parent = new GameObject($"Block_{block.xMin:0}_{block.yMin:0}").transform;
        parent.SetParent(blocksRoot, false);

        // Nền khu nhà
        GameObject lot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lot.name = "Lot";
        lot.transform.SetParent(parent, false);
        lot.transform.position = new Vector3(block.center.x, LotTop * 0.5f, block.center.y);
        lot.transform.localScale = new Vector3(block.width, LotTop, block.height);
        lot.GetComponent<Renderer>().sharedMaterial = Mat("City_Lot", new Color(0.6f, 0.58f, 0.54f));

        // Viền vỉa hè 2m quanh khu
        string tile = Floor + "Sideway 4 prefab.prefab";
        for (float x = block.xMin + 1f; x < block.xMax; x += 2f)
        {
            Spawn(tile, parent, new Vector3(x, 0f, block.yMin + 1f), 0f);
            Spawn(tile, parent, new Vector3(x, 0f, block.yMax - 1f), 0f);
        }
        for (float z = block.yMin + 3f; z < block.yMax - 2f; z += 2f)
        {
            Spawn(tile, parent, new Vector3(block.xMin + 1f, 0f, z), 0f);
            Spawn(tile, parent, new Vector3(block.xMax - 1f, 0f, z), 0f);
        }

        // 4 cạnh: hướng ra đường + tên đường
        var edges = new List<(Vector3 a, Vector3 b, Vector3 normal, string street, float depthLimit)>
        {
            (new Vector3(block.xMin, 0, block.yMin), new Vector3(block.xMax, 0, block.yMin), Vector3.back, StreetAt(xRoads, block.yMin - 2f, true), block.height * 0.5f - 2.5f),
            (new Vector3(block.xMax, 0, block.yMax), new Vector3(block.xMin, 0, block.yMax), Vector3.forward, StreetAt(xRoads, block.yMax + 2f, true), block.height * 0.5f - 2.5f),
            (new Vector3(block.xMin, 0, block.yMax), new Vector3(block.xMin, 0, block.yMin), Vector3.left, StreetAt(zRoads, block.xMin - 2f, false), block.width * 0.5f - 2.5f),
            (new Vector3(block.xMax, 0, block.yMin), new Vector3(block.xMax, 0, block.yMax), Vector3.right, StreetAt(zRoads, block.xMax + 2f, false), block.width * 0.5f - 2.5f),
        };

        var placed = new List<Bounds>();
        foreach (var e in edges)
        {
            Vector3 dir = (e.b - e.a).normalized;
            float len = Vector3.Distance(e.a, e.b);
            // Đèn đường + cây trên vỉa hè
            for (float s = 6f; s < len - 4f; s += 16f)
            {
                Vector3 p = e.a + dir * s - e.normal * 0.6f;
                GameObject lamp = Spawn(Lamps + "street_lamp 1 prefab.prefab", propsRoot, p + Vector3.up * 0.2f, YawFacing(Vector3.back, e.normal));
                SetupLampLights(lamp);
                if (s + 8f < len - 4f && rng.NextDouble() < 0.7)
                    Spawn(Props + "Tree prefab.prefab", propsRoot, e.a + dir * (s + 8f) - e.normal * 1.0f + Vector3.up * 0.2f, (float)rng.NextDouble() * 360f);
                else if (s + 8f < len - 4f && rng.NextDouble() < 0.5)
                    Spawn(Props + (rng.NextDouble() < 0.5 ? "Bench 2 prefab.prefab" : "Bin prefab.prefab"), propsRoot, e.a + dir * (s + 8f) - e.normal * 1.2f + Vector3.up * 0.2f, YawFacing(Vector3.back, e.normal));
            }

            // Nhà dọc theo cạnh, quay mặt ra đường
            float cursor = 3f;
            int guard = 0;
            while (cursor < len - 6f && guard++ < 20)
            {
                GameObject b = null; Bounds wb = default; float footprint = 0f, depth = 0f;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    string name = BuildingPool[rng.Next(BuildingPool.Length)];
                    Vector3 front = Fronts.TryGetValue(name, out Vector3 f) ? f : Vector3.back;
                    GameObject cand = Spawn(Builds + name + ".prefab", buildingsRoot, Vector3.zero, YawFacing(front, e.normal));
                    if (cand == null) continue;
                    wb = WorldBounds(cand);
                    footprint = Mathf.Abs(Vector3.Dot(wb.size, new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z))));
                    depth = Mathf.Abs(Vector3.Dot(wb.size, new Vector3(Mathf.Abs(e.normal.x), 0f, Mathf.Abs(e.normal.z))));
                    if (depth <= e.depthLimit && cursor + footprint <= len - 3f) { b = cand; break; }
                    Object.DestroyImmediate(cand);
                }
                if (b == null) break;

                // Đặt: mép trước sát vỉa hè (lùi 2.5m từ mép khu), mép bên bắt đầu ở cursor
                Vector3 frontLine = e.a - e.normal * 2.5f;
                Vector3 targetCenter = frontLine + dir * (cursor + footprint * 0.5f) - e.normal * (depth * 0.5f);
                Vector3 offset = new Vector3(targetCenter.x - wb.center.x, LotTop - wb.min.y, targetCenter.z - wb.center.z);
                b.transform.position += offset;
                wb.center += offset;

                Bounds test = wb; test.Expand(new Vector3(-0.4f, 0f, -0.4f));
                if (placed.Any(p => p.Intersects(test))) { Object.DestroyImmediate(b); cursor += 4f; continue; }
                placed.Add(wb);

                // Địa chỉ giao hàng
                var addr = b.AddComponent<DeliveryAddress>();
                addr.address = $"{number} {e.street}";
                addr.doorPoint = frontLine + dir * (cursor + footprint * 0.5f) + e.normal * 1.5f + Vector3.up * 0.2f;
                b.name = $"{addr.address} ({b.name})";
                number++;

                cursor += footprint + 1.5f + (float)rng.NextDouble() * 3f;
            }
        }
    }

    private static string StreetAt(List<RoadDef> roads, float coord, bool alongX)
    {
        foreach (RoadDef r in roads) if (coord >= r.c0 - 0.5f && coord <= r.c1 + 0.5f) return r.name;
        return alongX ? "Ton Duc Thang St" : "Nam Ky Khoi Nghia St";
    }

    /// <summary>Góc quay quanh Y để hướng cục bộ 'front' nhìn theo 'facing' (thế giới).</summary>
    private static float YawFacing(Vector3 front, Vector3 facing)
    {
        float yf = Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg;
        float yn = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        return yn - yf;
    }

    /// <summary>Đèn đường: giữ đèn nhưng chỉ bật ban đêm (NightLight), không đổ bóng, tầm gần.</summary>
    private static void SetupLampLights(GameObject lamp)
    {
        if (lamp == null) return;
        foreach (Light l in lamp.GetComponentsInChildren<Light>(true))
        {
            l.shadows = LightShadows.None;
            l.range = Mathf.Min(l.range, 12f);
            l.enabled = false;
            if (l.GetComponent<NightLight>() == null) l.gameObject.AddComponent<NightLight>();
        }
    }

    private static void Ground(Rect area)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "Ground";
        g.transform.SetParent(root, false);
        g.transform.position = new Vector3(area.center.x, -0.03f, area.center.y);
        g.transform.localScale = new Vector3(area.width / 10f, 1f, area.height / 10f);
        g.GetComponent<Renderer>().sharedMaterial = Mat("City_Ground", new Color(0.36f, 0.45f, 0.3f));
    }

    // ================= Tiện ích =================
    private static Transform Child(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    private static GameObject Spawn(string path, Transform parent, Vector3 pos, float yaw)
    {
        if (!prefabCache.TryGetValue(path, out GameObject prefab))
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            prefabCache[path] = prefab;
            if (prefab == null) Debug.LogWarning("[City] Không thấy prefab " + path);
        }
        if (prefab == null) return null;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
        return g;
    }

    private static Bounds WorldBounds(GameObject g)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b : new Bounds(g.transform.position, Vector3.one);
    }

    private static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Project/Generated")) AssetDatabase.CreateFolder("Assets/Project", "Generated");
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Project/Generated", "City");
        string path = $"{OutDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Material src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Resources/Materials/RuntimeLit.mat");
        m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
