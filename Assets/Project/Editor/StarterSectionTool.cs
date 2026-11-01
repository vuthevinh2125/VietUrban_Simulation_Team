using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using TMPro;

/// <summary>
/// 20b. Chia siêu thị: lúc đầu chỉ mở gian nhỏ phía tây-trước (cạnh quầy thu ngân), mua dần các gian còn lại.
///  - Gian khởi đầu: x &lt; SplitX, z &lt; BackZ (khoảng 8 x 9 m: 7 kệ + 6 tủ đông).
///  - 1. East Hall (Lv2): phía đông-trước (x &gt; SplitX, FrontZ &lt; z &lt; BackZ): khu rau quả + 2 dãy kệ.
///  - 2. Chilled Aisle (Lv3): phía tây-giữa (x &lt; SplitX, BackZ &lt; z &lt; RearZ): tủ lạnh, tủ đông, 1 dãy kệ.
///  - 3. Center Aisles (Lv4): phía đông-giữa (x &gt; SplitX, BackZ &lt; z &lt; RearZ): 4 dãy kệ dài.
///  - 4. Rear Hall (Lv5): phía sau (z &gt; RearZ): tủ lạnh, tủ đông, quầy deli.
/// Mở theo thứ tự 1 -> 4 (StoreExpansion.requires).
/// Vách ngăn dựng bằng tấm tường của gói ModernSupermarket, có BoxCollider + NavMeshObstacle (carve) nên KHÔNG cần bake lại NavMesh.
/// Kệ trong gian chưa mua bị ẩn (Shelf.OnDisable tự rút khỏi StockManager -> khách không đi tìm hàng ở đó).
/// Chạy lại được: lần sau xoá vách / gian cũ rồi dựng lại.
/// </summary>
/// <summary>
/// 20. Chia siêu thị thành các gian mở dần (bước 1: phân tích bố cục).
/// In ra Console / Editor.log: khung siêu thị, vị trí quầy thu ngân, cửa, và từng kệ / tủ trong 'Supermarket_full/Furniture'
/// (tâm, kích thước, hướng dài, loại) để chọn chỗ đặt vách ngăn.
/// </summary>
public static class StarterSectionTool
{
    private const string StoreRoot = "Supermarket_full";

    [MenuItem("Tools/Store Setup/20a. Analyse Store Layout (chỉ in ra, không thay đổi gì)")]
    private static void Analyse()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Layout] ===== BEGIN =====");
        GameObject store = GameObject.Find(StoreRoot);
        if (store == null) { Debug.LogWarning($"[Layout] Không thấy '{StoreRoot}'"); return; }
        sb.AppendLine($"[Layout] store pos {V(store.transform.position)} rotY {store.transform.eulerAngles.y:F0} bounds {B(WorldBounds(store))}");

        foreach (Transform ch in store.transform)
            sb.AppendLine($"[Layout] child '{ch.name}' active {ch.gameObject.activeSelf} children {ch.childCount} bounds {B(WorldBounds(ch.gameObject))}");

        // Sàn: renderer phẳng thấp có tên floor
        Bounds floor = default; bool anyFloor = false;
        foreach (Renderer r in store.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.ToLower().Contains("floor")) continue;
            if (!anyFloor) { floor = r.bounds; anyFloor = true; } else floor.Encapsulate(r.bounds);
        }
        sb.AppendLine($"[Layout] floor {(anyFloor ? B(floor) : "none")}");

        foreach (CheckoutCounter c in Object.FindObjectsByType<CheckoutCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"[Layout] checkout '{c.name}' active {c.gameObject.activeInHierarchy} pos {V(c.transform.position)} locked {c.GetComponentInParent<LockedCheckout>(true) != null}");
        foreach (AutoDoor d in Object.FindObjectsByType<AutoDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"[Layout] door '{d.name}' pos {V(d.transform.position)}");
        foreach (EntranceDoor d in Object.FindObjectsByType<EntranceDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"[Layout] entrance '{d.name}' pos {V(d.transform.position)}");
        foreach (StoreExpansion e in Object.FindObjectsByType<StoreExpansion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"[Layout] expansion '{e.id}' lv {e.requiredLevel} ${e.price} pos {V(e.transform.position)} show {e.showOnUnlock?.Length} hide {e.hideOnUnlock?.Length} move {e.moveOnUnlock?.Length}");
        CustomerSpawner sp = Object.FindFirstObjectByType<CustomerSpawner>();
        if (sp != null) sb.AppendLine($"[Layout] spawner pos {V(sp.transform.position)}");

        Transform furn = store.transform.Find("Furniture");
        if (furn != null)
        {
            int i = 0;
            foreach (Transform f in furn)
            {
                Bounds b = WorldBounds(f.gameObject);
                string kind = f.GetComponentInChildren<CheckoutCounter>(true) != null ? "CHECKOUT"
                    : f.GetComponentInChildren<LockedCheckout>(true) != null ? "LOCKEDCHECKOUT"
                    : f.GetComponentsInChildren<Shelf>(true).Length + " shelves";
                bool obstacle = f.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null;
                bool builtIn = f.GetComponent<BuiltInFurniture>() != null;
                sb.AppendLine($"[Layout] F{i++:00} '{f.name}' active {f.gameObject.activeSelf} {kind} center {V(b.center)} size {V(b.size)} rotY {f.eulerAngles.y:F0} obstacle {obstacle} builtIn {builtIn}");
            }
        }

        // Tường: bắn tia ngang từ tâm sàn ra 4 hướng
        if (anyFloor)
        {
            Vector3 o = new Vector3(floor.center.x, floor.max.y + 1.2f, floor.center.z);
            foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                sb.AppendLine(Physics.Raycast(o, dir, out RaycastHit h, 200f, ~0, QueryTriggerInteraction.Ignore)
                    ? $"[Layout] ray {V(dir)} hit '{h.collider.name}' at {V(h.point)} dist {h.distance:F1}"
                    : $"[Layout] ray {V(dir)} no hit");
        }
        sb.AppendLine("[Layout] ===== END =====");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Store layout", "Đã in bố cục siêu thị ra Console. Báo lại cho Claude là xong.", "OK");
    }


    // ================= 20b. Dựng các gian =================
    // Toạ độ lấy từ 20a (1/11): sàn x -127.0..-105.7, z 20.2 (cửa vào) .. 50.4; quầy thu ngân z 24-25.4.
    private const float SplitX = -119.2f;  // giữa dãy kệ x -120.x và dãy x -117.x
    private const float BackZ = 35.95f;    // sau tủ đông z 35.5 / đầu kệ z 35, trước dãy kệ z 38.7
    private const float FrontZ = 28.45f;   // vách East Hall phía trước (chừa lối đi quanh quầy thu ngân)
    private const float RearZ = 45.0f;     // sau đầu các dãy kệ z 44.2 / tủ đông z 44.8, trước khu tủ lạnh phía sau
    private const float HallItemsFromZ = 26.0f;
    private const string RootName = "StoreSections";

    [MenuItem("Tools/Store Setup/20b. Build Starter Sections (siêu thị nhỏ lúc đầu, mở dần)")]
    private static void Build()
    {
        GameObject store = GameObject.Find(StoreRoot);
        Transform furn = store != null ? store.transform.Find("Furniture") : null;
        if (furn == null) { EditorUtility.DisplayDialog("Sections", $"Không thấy '{StoreRoot}/Furniture'.", "OK"); return; }
        Undo.SetCurrentGroupName("Build store sections");

        // Chạy lại: xoá bản cũ, bật lại toàn bộ kệ
        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            foreach (StoreExpansion e in old.GetComponentsInChildren<StoreExpansion>(true))
                if (e.showOnUnlock != null) foreach (GameObject g in e.showOnUnlock) if (g != null) { Undo.RecordObject(g, "Show"); g.SetActive(true); }
            Undo.DestroyObjectImmediate(old);
        }

        Bounds floor = default; bool anyFloor = false;
        foreach (Renderer r in store.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.ToLower().Contains("floor")) continue;
            if (!anyFloor) { floor = r.bounds; anyFloor = true; } else floor.Encapsulate(r.bounds);
        }
        float minX = anyFloor ? floor.min.x : -127f, maxX = anyFloor ? floor.max.x : -105.7f;
        float floorY = Physics.Raycast(new Vector3(-123f, 3f, 31f), Vector3.down, out RaycastHit fh, 6f, ~0, QueryTriggerInteraction.Ignore) ? fh.point.y : 0.2f;

        // Phân loại kệ / tủ (chỉ đồ có Shelf; quầy thu ngân, cửa chắn, bàn trang trí giữ nguyên)
        var hall1 = new List<GameObject>(); var hall2 = new List<GameObject>(); var hall3 = new List<GameObject>(); var hall4 = new List<GameObject>();
        int starter = 0;
        foreach (Transform f in furn)
        {
            if (!f.gameObject.activeSelf) continue;
            if (f.GetComponentInChildren<CheckoutCounter>(true) != null || f.GetComponentInChildren<LockedCheckout>(true) != null) continue;
            if (f.GetComponentsInChildren<Shelf>(true).Length == 0) continue;
            Vector3 c = WorldBounds(f.gameObject).center;
            if (c.z > RearZ) hall4.Add(f.gameObject);
            else if (c.z > BackZ) (c.x < SplitX ? hall2 : hall3).Add(f.gameObject);
            else if (c.x > SplitX && c.z > HallItemsFromZ) hall1.Add(f.gameObject);
            else starter++;
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Sections");
        root.transform.position = new Vector3(0f, floorY, 0f);
        Vector3 starterCenter = new Vector3((minX + SplitX) * 0.5f, 0f, (FrontZ + BackZ) * 0.5f);
        Vector3 hall3Center = new Vector3((SplitX + maxX) * 0.5f, 0f, (BackZ + RearZ) * 0.5f);
        const string buy = "<size=60%>buy it on the computer (Expand tab)</size>";

        // Vách và gian làm nó biến mất (mỗi vách chỉ ngăn giữa vùng đã mở và gian vừa mua)
        GameObject wH1Front = Wall(root.transform, "Wall_EastHall_Front", new Vector3(SplitX, 0f, FrontZ), new Vector3(maxX + 0.2f, 0f, FrontZ), starterCenter, Vector3.back, $"<b>COMING SOON: East Hall</b>\n{buy}");
        GameObject wH1Side = Wall(root.transform, "Wall_EastHall_Side", new Vector3(SplitX, 0f, FrontZ), new Vector3(SplitX, 0f, BackZ), starterCenter, Vector3.left, "<b>COMING SOON: East Hall</b>");
        GameObject wH2 = Wall(root.transform, "Wall_ChilledAisle", new Vector3(minX - 0.2f, 0f, BackZ), new Vector3(SplitX, 0f, BackZ), starterCenter, Vector3.back, $"<b>COMING SOON: Chilled Aisle</b>\n{buy}");
        GameObject wH3Front = Wall(root.transform, "Wall_CenterAisles_Front", new Vector3(SplitX, 0f, BackZ), new Vector3(maxX + 0.2f, 0f, BackZ), starterCenter, Vector3.back, $"<b>COMING SOON: Center Aisles</b>\n{buy}");
        GameObject wH3Side = Wall(root.transform, "Wall_CenterAisles_Side", new Vector3(SplitX, 0f, BackZ), new Vector3(SplitX, 0f, RearZ), hall3Center, Vector3.left, "<b>COMING SOON: Center Aisles</b>");
        GameObject wH4 = Wall(root.transform, "Wall_RearHall", new Vector3(minX - 0.2f, 0f, RearZ), new Vector3(maxX + 0.2f, 0f, RearZ), starterCenter, Vector3.back, $"<b>COMING SOON: Rear Hall</b>\n{buy}");

        StoreExpansion s1 = Section(root.transform, "section_easthall", "East Hall", "Produce corner and two more aisles", 2, 250f, -4, "", hall1.ToArray(), new[] { wH1Front, wH1Side });
        StoreExpansion s2 = Section(root.transform, "section_chilled", "Chilled Aisle", "Fridges, freezers and one more aisle", 3, 400f, -3, s1.id, hall2.ToArray(), new[] { wH2 });
        StoreExpansion s3 = Section(root.transform, "section_center", "Center Aisles", "Four long aisles in the middle of the store", 4, 550f, -2, s2.id, hall3.ToArray(), new[] { wH3Front, wH3Side });
        StoreExpansion s4 = Section(root.transform, "section_rear", "Rear Hall", "Back fridges, freezers and the deli corner", 5, 750f, -1, s3.id, hall4.ToArray(), new[] { wH4 });

        // Phòng mở rộng #1 / #2 nằm ngoài các gian trong cùng -> mở hết các gian trước
        foreach (StoreExpansion e in Object.FindObjectsByType<StoreExpansion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.id != null && e.id.StartsWith("expansion_storeexpansion")) { Undo.RecordObject(e, "Requires"); e.requires = s4.id; EditorUtility.SetDirty(e); }

        // Ẩn ngay trong Editor để nhìn thấy kết quả (lúc chơi StoreExpansion cũng tự ẩn)
        foreach (GameObject g in hall1.Concat(hall2).Concat(hall3).Concat(hall4)) { Undo.RecordObject(g, "Hide"); g.SetActive(false); }

        EditorSceneManager.MarkSceneDirty(store.scene);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("Store sections",
            $"Gian khởi đầu: {starter} kệ / tủ.\n1. East Hall (Lv2, $250): {hall1.Count}\n2. Chilled Aisle (Lv3, $400): {hall2.Count}\n" +
            $"3. Center Aisles (Lv4, $550): {hall3.Count}\n4. Rear Hall (Lv5, $750): {hall4.Count}\nMở theo thứ tự 1 -> 4; Store Expansion #1 / #2 cần mở Rear Hall trước.\n\n" +
            "Không cần bake lại NavMesh (vách có NavMeshObstacle). Bấm Ctrl+S để lưu scene. Muốn huỷ: Ctrl+Z.", "OK");
    }

    private static GameObject Wall(Transform root, string name, Vector3 a, Vector3 b, Vector3 roomCenter, Vector3 signFacing, string sign)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(root, false);
        GameObject wallPrefab = ExpansionBuilderTool.Load("Wall");
        float height = 3f;
        if (wallPrefab != null)
        {
            height = ExpansionBuilderTool.LocalBounds(wallPrefab).size.y;
            ExpansionBuilderTool.Edge(go.transform, wallPrefab, null, a, b, roomCenter);
        }
        else
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            cube.transform.localPosition = (a + b) * 0.5f + Vector3.up * height * 0.5f;
            cube.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(b - a, Vector3.up));
            cube.transform.localScale = new Vector3((b - a).magnitude, height, 0.15f);
        }
        // Chặn người chơi + cắt NavMesh cho khách (không cần bake lại)
        Vector3 mid = (a + b) * 0.5f + Vector3.up * height * 0.5f;
        Vector3 dir = b - a;
        bool alongX = Mathf.Abs(dir.x) >= Mathf.Abs(dir.z);
        Vector3 size = alongX ? new Vector3(Mathf.Abs(dir.x), height, 0.25f) : new Vector3(0.25f, height, Mathf.Abs(dir.z));
        BoxCollider col = go.AddComponent<BoxCollider>();
        col.center = mid; col.size = size;
        NavMeshObstacle obs = go.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box; obs.center = mid; obs.size = size + new Vector3(alongX ? 0f : 0.2f, 0f, alongX ? 0.2f : 0f);
        obs.carving = true; obs.carveOnlyStationary = true;
        go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        // Biển "Coming soon" phía gian đang mở
        var t = new GameObject("Sign");
        t.transform.SetParent(go.transform, false);
        t.transform.localPosition = (a + b) * 0.5f + Vector3.up * Mathf.Min(2.0f, height * 0.65f) + signFacing * 0.2f;
        t.transform.localRotation = Quaternion.LookRotation(-signFacing);
        TextMeshPro tmp = t.AddComponent<TextMeshPro>();
        tmp.text = sign;
        tmp.fontSize = 3.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.3f);
        tmp.rectTransform.sizeDelta = new Vector2(Mathf.Min(8f, dir.magnitude - 0.5f), 1.6f);
        return go;
    }

    private static StoreExpansion Section(Transform root, string id, string name, string desc, int level, float price, int order, string requires, GameObject[] show, GameObject[] hide)
    {
        var go = new GameObject("Section_" + name.Replace(" ", ""));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(root, false);
        StoreExpansion e = go.AddComponent<StoreExpansion>();
        e.id = id; e.displayName = name; e.description = desc;
        e.requiredLevel = level; e.price = price; e.order = order; e.requires = requires;
        e.showOnUnlock = show; e.hideOnUnlock = hide;
        return e;
    }

    private static Bounds WorldBounds(GameObject g)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static string V(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
    private static string B(Bounds b) => $"min {V(b.min)} max {V(b.max)}";
}
