using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Biến object đang chọn (ví dụ iMac ở quầy) thành máy tính quản lý:
/// nhìn vào + [E] sẽ mở giao diện quản lý phủ kín màn hình.
/// Cách dùng: chọn object trong Hierarchy -> Tools > Computer > Use Selected Object As Computer.
/// Mọi thay đổi đều Undo (Ctrl+Z) được.
/// </summary>
public static class ComputerSetupTool
{
    private const string MenuPath = "Tools/Computer/Use Selected Object As Computer";

    [MenuItem(MenuPath, true)]
    private static bool Validate() => Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);

    [MenuItem(MenuPath)]
    private static void UseSelectedAsComputer()
    {
        GameObject target = Selection.activeGameObject;

        ComputerInteract oldInteract = Object.FindFirstObjectByType<ComputerInteract>(FindObjectsInactive.Include);
        GameObject computerUI = oldInteract != null && oldInteract.computerUI != null ? oldInteract.computerUI : FindInScene("ComputerUI");
        if (computerUI == null)
        {
            EditorUtility.DisplayDialog("Computer Setup", "Không tìm thấy object 'ComputerUI' trong scene.", "OK");
            return;
        }
        if (target.transform.IsChildOf(computerUI.transform.root) && computerUI.transform.root.GetComponent<Canvas>() != null)
        {
            EditorUtility.DisplayDialog("Computer Setup", "Hãy chọn cái MÁY TÍNH trong scene (ví dụ Computer_dfc1a9), không phải object giao diện.", "OK");
            return;
        }

        if (!TryGetLocalBounds(target.transform, out Bounds local))
        {
            EditorUtility.DisplayDialog("Computer Setup", $"'{target.name}' không có mesh nào để làm vùng bấm.", "OK");
            return;
        }

        Vector3 worldSize = Vector3.Scale(local.size, target.transform.lossyScale);
        float largest = Mathf.Max(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y), Mathf.Abs(worldSize.z));
        if (largest < 0.2f &&
            !EditorUtility.DisplayDialog("Computer Setup",
                $"'{target.name}' chỉ rộng khoảng {largest * 100f:0} cm, có vẻ không phải máy tính.\n\n" +
                "Với iMac ở quầy, hãy chọn 'Computer_dfc1a9'.", "Vẫn tiếp tục", "Huỷ"))
            return;

        Undo.SetCurrentGroupName("Use Selected Object As Computer");
        int group = Undo.GetCurrentGroup();

        // 1) Gỡ script khỏi máy tính cũ, gắn vào object mới
        foreach (ComputerInteract ci in Object.FindObjectsByType<ComputerInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ci.gameObject != target) Undo.DestroyObjectImmediate(ci);

        ComputerInteract interact = target.GetComponent<ComputerInteract>();
        if (interact == null) interact = Undo.AddComponent<ComputerInteract>(target);
        Undo.RecordObject(interact, "Assign Computer UI");
        interact.computerUI = computerUI;
        interact.showAsScreenOverlay = true;

        // 2) Vùng bấm: BoxCollider bao trọn máy tính + layer Interactable để tia nhìn bắt được
        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(target);
        Undo.RecordObject(box, "Fit Collider");
        box.center = local.center;
        box.size = Vector3.Max(local.size, Vector3.one * 0.02f);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            Undo.RecordObject(target, "Set Layer");
            target.layer = interactableLayer;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(target.scene);

        Debug.Log($"[Computer Setup] '{target.name}' giờ là máy tính quản lý: nhìn vào + [E] để mở, [Esc] để đóng. Nhớ Ctrl+S để lưu scene.", target);
    }

    // ================= Helpers =================
    private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return any;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }
}
