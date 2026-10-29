using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nối NavMesh vỉa hè với sàn trong cửa hàng qua từng cửa ra vào bằng NavMeshLink (đi 2 chiều).
/// Dùng khi bản đồ NavMesh cho thấy trong/ngoài bị tách rời ở ngưỡng cửa.
/// Menu: Tools > Store Setup > 5. Connect Entrances (NavMesh Links)
/// </summary>
public static class EntranceLinkTool
{
    private const string LinkName = "EntranceLink";
    private const float OutsideDistance = 1.8f;  // điểm đầu cầu: trước cửa (phía vỉa hè)
    private const float InsideDistance = 2.2f;   // điểm cuối cầu: trong cửa hàng
    private const float LinkWidth = 1.4f;

    [MenuItem("Tools/Store Setup/5. Connect Entrances (NavMesh Links)")]
    private static void Run()
    {
        EntranceDoor[] doors = Object.FindObjectsByType<EntranceDoor>(FindObjectsSortMode.None);
        if (doors.Length == 0)
        {
            EditorUtility.DisplayDialog("Entrance Links", "Chưa có EntranceDoor nào. Chạy '4. Open Store Entrances' trước.", "OK");
            return;
        }

        CustomerSpawner spawner = Object.FindFirstObjectByType<CustomerSpawner>();
        Vector3 outsideRef = spawner != null && spawner.spawnPoint != null ? spawner.spawnPoint.position : doors[0].transform.position - Vector3.forward * 5f;

        Undo.SetCurrentGroupName("Connect Entrances");
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder();

        foreach (EntranceDoor door in doors)
        {
            Transform t = door.transform;

            // Pháp tuyến cửa = trục ngang mỏng nhất của khung cửa; hướng ra ngoài = phía điểm sinh khách
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Vector3 normal = !any || b.size.z <= b.size.x ? Vector3.forward : Vector3.right;
            Vector3 center = new Vector3(t.position.x, any ? b.min.y : t.position.y, t.position.z);
            if (Vector3.Dot(outsideRef - center, normal) > 0f) normal = -normal; // normal luôn chỉ VÀO TRONG

            Vector3 start = Snap(center - normal * OutsideDistance);
            Vector3 end = Snap(center + normal * InsideDistance);

            Transform linkT = t.Find(LinkName);
            if (linkT == null)
            {
                var go = new GameObject(LinkName);
                Undo.RegisterCreatedObjectUndo(go, "Create link");
                go.transform.SetParent(t, false);
                linkT = go.transform;
            }
            Undo.RecordObject(linkT, "Place link");
            linkT.SetPositionAndRotation(center, Quaternion.LookRotation(normal, Vector3.up));
            linkT.localScale = Vector3.one;

            NavMeshLink link = linkT.GetComponent<NavMeshLink>();
            if (link == null) link = Undo.AddComponent<NavMeshLink>(linkT.gameObject);
            Undo.RecordObject(link, "Configure link");
            link.startPoint = linkT.InverseTransformPoint(start);
            link.endPoint = linkT.InverseTransformPoint(end);
            link.width = LinkWidth;
            link.bidirectional = true;
            link.autoUpdate = true;
            EditorUtility.SetDirty(link);

            log.AppendLine($"- {t.name}: link {start:F2} -> {end:F2}");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(doors[0].gameObject.scene);

        string msg = $"Đã nối {doors.Length} cửa.\n\n{log}\nBấm Ctrl+S rồi Play thử. Không cần Bake lại.";
        Debug.Log("[Entrance Links]\n" + msg);
        EditorUtility.DisplayDialog("Entrance Links", msg, "OK");
    }

    private static Vector3 Snap(Vector3 p) =>
        NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : p;
}
