using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chọn một quầy Checkout trong Hierarchy -> Tools > Store Setup > 3. Setup Checkout & Customers
///  - Gắn CheckoutCounter + tạo CustomerPoint (chỗ khách đứng) và ItemDropPoint (chỗ đặt hàng trên quầy)
///  - Tạo CustomerSpawner (một lần) với điểm sinh/ra về ngoài cửa, dùng prefab NPC_01, NPC_02
///  - Kiểm tra NavMesh: ngoài cửa -> kệ -> quầy -> lối ra, ghi vào Logs/CheckoutSetup.txt
/// Ctrl+Z để hoàn tác.
/// </summary>
public static class CheckoutSetupTool
{
    private const string MenuPath = "Tools/Store Setup/3. Setup Checkout & Customers (chọn 1 quầy Checkout trước)";
    private const string StoreRootName = "Supermarket_full";

    [MenuItem(MenuPath, true)]
    private static bool Validate() => Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);

    [MenuItem(MenuPath)]
    private static void Setup()
    {
        GameObject counterGo = Selection.activeGameObject;
        if (!TryBounds(counterGo.transform, out Bounds bounds))
        {
            EditorUtility.DisplayDialog("Checkout Setup", $"'{counterGo.name}' không có mesh nào.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Setup Checkout");
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("# Checkout setup on " + counterGo.name + "\n");

        // ---------- 1) CheckoutCounter ----------
        CheckoutCounter counter = counterGo.GetComponent<CheckoutCounter>();
        if (counter == null) counter = Undo.AddComponent<CheckoutCounter>(counterGo);

        Vector3 storeCenter = GetStoreCenter(bounds.center);
        Vector3 toStore = storeCenter - bounds.center; toStore.y = 0f;

        // Phía khách đứng: trục của quầy hướng vào trong cửa hàng nhất
        Transform ct = counterGo.transform;
        Vector3[] axes = { ct.forward, -ct.forward, ct.right, -ct.right };
        Vector3 side = axes.OrderByDescending(a => Vector3.Dot(Flat(a), toStore.normalized)).First();
        side = Flat(side);
        float half = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z))));

        Vector3 customerPos = bounds.center + side * (half + 0.45f);
        customerPos.y = bounds.min.y;
        if (NavMesh.SamplePosition(customerPos, out NavMeshHit cp, 1.5f, NavMesh.AllAreas)) customerPos = cp.position;

        Transform customerPoint = CreateOrGetChild(ct, "CustomerPoint");
        customerPoint.position = customerPos;
        customerPoint.rotation = Quaternion.LookRotation(-side, Vector3.up); // nhìn vào quầy, hàng chờ kéo dài sau lưng

        Transform dropPoint = CreateOrGetChild(ct, "ItemDropPoint");
        dropPoint.position = new Vector3(bounds.center.x, bounds.max.y + 0.02f, bounds.center.z) + side * Mathf.Min(0.15f, half * 0.5f);
        dropPoint.rotation = Quaternion.LookRotation(-side, Vector3.up);

        var so = new SerializedObject(counter);
        so.FindProperty("customerPoint").objectReferenceValue = customerPoint;
        so.FindProperty("itemDropPoint").objectReferenceValue = dropPoint;
        so.FindProperty("queueDirection").vector3Value = Vector3.back;
        so.ApplyModifiedProperties();
        if (PrefabUtility.IsPartOfPrefabInstance(counter)) PrefabUtility.RecordPrefabInstancePropertyModifications(counter);
        log.AppendLine($"Counter at {bounds.center:F2}, customer point {customerPoint.position:F2}, drop point {dropPoint.position:F2}");

        // ---------- 2) CustomerSpawner ----------
        CustomerSpawner spawner = Object.FindFirstObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            var go = new GameObject("CustomerSpawner");
            Undo.RegisterCreatedObjectUndo(go, "Create spawner");
            spawner = go.AddComponent<CustomerSpawner>();

            Transform anchor = FindSceneTransform("Exit_Point") ?? FindSceneTransform("DeliveryPoint");
            Vector3 spawnPos = anchor != null ? anchor.position : bounds.center + side * -4f;
            go.transform.position = spawnPos;

            Transform spawnPoint = CreateOrGetChild(go.transform, "SpawnPoint");
            spawnPoint.position = spawnPos;
            Transform exitPoint = CreateOrGetChild(go.transform, "ExitPoint");
            exitPoint.position = spawnPos;

            var prefabs = new[] { "Assets/Project/Prefabs/Characters/NPC_01.prefab", "Assets/Project/Prefabs/Characters/NPC_02.prefab" }
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();

            var sso = new SerializedObject(spawner);
            sso.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            sso.FindProperty("exitPoint").objectReferenceValue = exitPoint;
            SerializedProperty arr = sso.FindProperty("customerPrefabs");
            arr.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            sso.ApplyModifiedProperties();
            log.AppendLine($"Created CustomerSpawner at {spawnPos:F2} with {prefabs.Length} prefab(s)");
        }
        else log.AppendLine($"CustomerSpawner already exists at {spawner.transform.position:F2}");

        // ---------- 3) Kiểm tra NavMesh ----------
        Vector3 spawn = spawner.spawnPoint != null ? spawner.spawnPoint.position : spawner.transform.position;
        Shelf anyShelf = Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None).OrderBy(s => (s.transform.position - customerPos).sqrMagnitude).FirstOrDefault();
        Vector3 shelfPos = anyShelf != null ? anyShelf.transform.position : storeCenter;
        log.AppendLine("\n## NavMesh paths");
        string a = PathStatus(spawn, shelfPos);
        string b = PathStatus(shelfPos, customerPos);
        string c = PathStatus(customerPos, spawner.exitPoint != null ? spawner.exitPoint.position : spawn);
        log.AppendLine($"spawn -> shelf '{(anyShelf ? anyShelf.name : "-")}': {a}");
        log.AppendLine($"shelf -> checkout: {b}");
        log.AppendLine($"checkout -> exit: {c}");

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(counterGo.scene);
        Selection.activeGameObject = counterGo;

        string file = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "CheckoutSetup.txt");
        File.WriteAllText(file, log.ToString());
        Debug.Log($"[Checkout Setup] Xong. Đường đi: ngoài->kệ {a}, kệ->quầy {b}, quầy->ra {c}. " +
                  "Chấm xanh lá = chỗ khách đứng, khung vàng = chỗ đặt hàng; kéo chỉnh nếu lệch. Nhớ Ctrl+S.", counterGo);
    }

    // ================= Helpers =================
    private static string PathStatus(Vector3 from, Vector3 to)
    {
        if (!NavMesh.SamplePosition(from, out NavMeshHit a, 3f, NavMesh.AllAreas)) return "START NOT ON NAVMESH";
        if (!NavMesh.SamplePosition(to, out NavMeshHit b, 3f, NavMesh.AllAreas)) return "END NOT ON NAVMESH";
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        return path.status == NavMeshPathStatus.PathComplete ? "OK" : path.status.ToString();
    }

    private static Vector3 GetStoreCenter(Vector3 fallback)
    {
        Transform store = FindSceneTransform(StoreRootName);
        return store != null && TryBounds(store, out Bounds b) ? b.center : fallback;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

    private static Transform CreateOrGetChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) { Undo.RecordObject(t, "Move point"); return t; }
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create point");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform FindSceneTransform(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t;
        return null;
    }

    private static bool TryBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }
}
