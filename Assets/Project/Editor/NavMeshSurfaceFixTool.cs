using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene chỉ nên có MỘT NavMeshSurface. Surface cũ còn dữ liệu sẽ tạo lưới chồng lên lưới mới và không bao giờ nối
/// với nhau -> khách đứng nhầm lưới cũ thì đường đi luôn PathInvalid.
/// Công cụ giữ lại surface tên "NavMesh Manager", tắt và gỡ dữ liệu của các surface khác. Ctrl+Z để hoàn tác.
/// Menu: Tools > Store Setup > 6. Remove Duplicate NavMesh Surfaces
/// </summary>
public static class NavMeshSurfaceFixTool
{
    private const string KeepName = "NavMesh Manager";

    [MenuItem("Tools/Store Setup/6. Remove Duplicate NavMesh Surfaces")]
    private static void Run()
    {
        NavMeshSurface[] all = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        NavMeshSurface keep = all.FirstOrDefault(s => s.name == KeepName) ?? all.FirstOrDefault(s => s.enabled && s.navMeshData != null);
        if (keep == null)
        {
            EditorUtility.DisplayDialog("NavMesh Surfaces", "Không tìm thấy NavMeshSurface nào.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Remove duplicate NavMesh surfaces");
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder($"Giữ lại: {keep.name} (data: {(keep.navMeshData ? keep.navMeshData.name : "NONE")})\n");
        int removed = 0;

        foreach (NavMeshSurface s in all)
        {
            if (s == keep) continue;
            Undo.RecordObject(s, "Disable surface");
            log.AppendLine($"Tắt: {s.name} (data: {(s.navMeshData ? s.navMeshData.name : "none")})");
            s.navMeshData = null;
            s.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(s)) PrefabUtility.RecordPrefabInstancePropertyModifications(s);
            removed++;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(keep.gameObject.scene);
        string msg = removed == 0 ? "Scene chỉ có 1 NavMeshSurface, không cần sửa." : $"{log}\nĐã tắt {removed} surface thừa.\nBấm Ctrl+S rồi Play thử (không cần Bake lại).";
        Debug.Log("[NavMesh Surfaces] " + msg);
        EditorUtility.DisplayDialog("NavMesh Surfaces", msg, "OK");
    }
}
