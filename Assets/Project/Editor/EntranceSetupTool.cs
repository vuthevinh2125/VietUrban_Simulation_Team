using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Mở lối vào cửa hàng cho khách:
///  - Cánh cửa lối vào + cổng chống trộm: gắn NavMeshModifier "Ignore From Build" để khi Bake NavMesh lối đi không bị bịt.
///  - Cửa lối vào: gắn EntranceDoor để tự mở khi có người tới gần.
/// Menu: Tools > Store Setup > 4. Open Store Entrances. Sau đó Bake lại NavMesh.
/// </summary>
public static class EntranceSetupTool
{
    private const string StoreRootName = "Supermarket_full";
    private static readonly string[] DoorRootPrefixes = { "Door_c5bu08" };
    private static readonly string[] IgnoreRootPrefixes = { "Door_c5bu08", "Anti-theftDoor" };

    [MenuItem("Tools/Store Setup/4. Open Store Entrances (rồi Bake NavMesh)")]
    private static void Run()
    {
        Transform store = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == StoreRootName && t.parent == null);
        if (store == null) { EditorUtility.DisplayDialog("Entrance Setup", $"Không tìm thấy '{StoreRootName}'.", "OK"); return; }

        Undo.SetCurrentGroupName("Open Store Entrances");
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder();

        // Gốc của mỗi cửa/cổng = transform có tên bắt đầu bằng tiền tố và cha của nó không có tiền tố đó
        var all = store.GetComponentsInChildren<Transform>(true);
        List<Transform> ignoreRoots = all.Where(t => IgnoreRootPrefixes.Any(p => t.name.StartsWith(p)) &&
                                                     (t.parent == null || !IgnoreRootPrefixes.Any(p => t.parent.name.StartsWith(p)))).ToList();

        foreach (Transform root in ignoreRoots)
        {
            bool isDoor = DoorRootPrefixes.Any(p => root.name.StartsWith(p));

            // Các cánh cửa: con có tên chứa "Door" (không phải tấm kính) và có renderer
            Transform[] leaves = isDoor
                ? root.Cast<Transform>().Where(t => t.name.Contains("Door") && !t.name.Contains("Glass") && t.GetComponentInChildren<Renderer>(true) != null).ToArray()
                : new Transform[0];

            if (isDoor)
            {
                // Cửa: chỉ bỏ qua CÁNH CỬA, khung + vách kính vẫn là vật cản (không cho đi xuyên kính)
                SetIgnore(root, false);
                foreach (Transform leaf in leaves) SetIgnore(leaf, true);
                log.AppendLine($"- NavMesh ignore leaves of {root.name}: {string.Join(", ", leaves.Select(l => l.name))}");
            }
            else
            {
                SetIgnore(root, true); // cổng chống trộm: bỏ qua cả cụm
                log.AppendLine($"- NavMesh ignore: {root.name}");
            }

            if (!isDoor || leaves.Length == 0) continue;

            EntranceDoor door = root.GetComponent<EntranceDoor>();
            if (door == null) door = Undo.AddComponent<EntranceDoor>(root.gameObject);
            var dso = new SerializedObject(door);
            SerializedProperty lp = dso.FindProperty("leaves");
            SerializedProperty ap = dso.FindProperty("openAngles");
            lp.arraySize = leaves.Length;
            ap.arraySize = leaves.Length;
            for (int i = 0; i < leaves.Length; i++)
            {
                lp.GetArrayElementAtIndex(i).objectReferenceValue = leaves[i];
                ap.GetArrayElementAtIndex(i).floatValue = leaves[i].name.ToLower().Contains("right") ? -90f : 90f;
            }
            dso.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(door)) PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            log.AppendLine($"  + EntranceDoor với {leaves.Length} cánh: {string.Join(", ", leaves.Select(l => l.name))}");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(store.gameObject.scene);

        string summary = $"Đã xử lý {ignoreRoots.Count} cửa/cổng.\n\nViệc tiếp theo:\n1. Chọn 'NavMesh Manager' -> Bake\n2. Ctrl+S";
        Debug.Log("[Entrance Setup]\n" + log + summary);
        EditorUtility.DisplayDialog("Entrance Setup", summary, "OK");
    }

    /// <summary>Bật/tắt "Ignore From Build" cho NavMeshModifier trên object (tạo modifier nếu cần bật).</summary>
    private static void SetIgnore(Transform t, bool ignore)
    {
        NavMeshModifier mod = t.GetComponent<NavMeshModifier>();
        if (!ignore)
        {
            if (mod != null) Undo.DestroyObjectImmediate(mod); // gỡ hẳn để không ghi đè cài đặt của các cánh cửa
            return;
        }
        if (mod == null) mod = Undo.AddComponent<NavMeshModifier>(t.gameObject);
        var so = new SerializedObject(mod);
        SerializedProperty p = so.FindProperty("m_IgnoreFromBuild");
        if (p != null) p.boolValue = ignore;
        SerializedProperty children = so.FindProperty("m_ApplyToChildren");
        if (children != null) children.boolValue = true;
        so.ApplyModifiedProperties();
        if (PrefabUtility.IsPartOfPrefabInstance(mod)) PrefabUtility.RecordPrefabInstancePropertyModifications(mod);
    }
}
