using UnityEditor;
using UnityEngine;

/// <summary>Tạo biển OPEN/CLOSED ngay trước camera của cửa sổ Scene, đặt sát mặt đất. Sau đó tự kéo tới cửa hàng.</summary>
public static class StoreSignTool
{
    [MenuItem("Tools/Store Setup/8. Create Open-Closed Sign")]
    private static void Create()
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            Transform cam = view.camera.transform;
            pos = cam.position + cam.forward * 3f;
            Vector3 face = -cam.forward; face.y = 0f;
            if (face.sqrMagnitude > 0.001f) rot = Quaternion.LookRotation(-face); // mặt chữ quay về phía camera
        }
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        var go = new GameObject("StoreSign_OpenClosed");
        go.transform.SetPositionAndRotation(pos, rot);
        go.AddComponent<StoreSign>().BuildVisual(true);
        Undo.RegisterCreatedObjectUndo(go, "Create Store Sign");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[Store Sign] Đã tạo biển OPEN/CLOSED. Kéo nó tới cạnh cửa ra vào rồi bấm Ctrl+S. (Biển tự dựng hình khi bấm Play)");
    }
}
