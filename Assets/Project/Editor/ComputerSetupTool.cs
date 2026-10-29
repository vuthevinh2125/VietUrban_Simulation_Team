using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Chuyển giao diện máy tính (Canvas chứa ComputerUI) sang một máy tính / màn hình khác trong scene.
/// Cách dùng: chọn màn hình mới trong Hierarchy -> Tools > Computer > Move Computer UI To Selected Object.
/// Mọi thay đổi đều Undo (Ctrl+Z) được.
/// </summary>
public static class ComputerSetupTool
{
    private const string MoveMenu = "Tools/Computer/Move Computer UI To Selected Object";
    private const string FlipMenu = "Tools/Computer/Flip Computer UI 180 Degrees";
    private const float SurfaceOffset = 0.003f; // đặt UI nhô ra trước mặt kính 3mm
    private const float FillRatio = 0.92f;      // UI chiếm 92% bề rộng màn hình

    [MenuItem(MoveMenu, true)]
    private static bool ValidateMove() => Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);

    [MenuItem(MoveMenu)]
    private static void MoveComputerUI()
    {
        GameObject target = Selection.activeGameObject;

        ComputerInteract oldInteract = Object.FindFirstObjectByType<ComputerInteract>(FindObjectsInactive.Include);
        GameObject computerUI = oldInteract != null && oldInteract.computerUI != null ? oldInteract.computerUI : FindInScene("ComputerUI");
        if (computerUI == null)
        {
            EditorUtility.DisplayDialog("Computer Setup", "Không tìm thấy object 'ComputerUI' trong scene.", "OK");
            return;
        }

        RectTransform canvasRect = FindRootCanvas(computerUI.transform);
        if (canvasRect == null)
        {
            EditorUtility.DisplayDialog("Computer Setup", "ComputerUI không nằm trong Canvas nào.", "OK");
            return;
        }
        if (target.transform.IsChildOf(canvasRect))
        {
            EditorUtility.DisplayDialog("Computer Setup", "Hãy chọn MÀN HÌNH máy tính (iMac), không phải object trong Canvas.", "OK");
            return;
        }

        // Chặn chọn nhầm (ví dụ con chuột, bàn phím)
        if (TryGetBounds(target.transform, Quaternion.identity, canvasRect, out Vector3 tMin, out Vector3 tMax, localSpaceOf: target.transform))
        {
            Vector3 s = Vector3.Scale(tMax - tMin, target.transform.lossyScale);
            float largest = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (largest < 0.2f &&
                !EditorUtility.DisplayDialog("Computer Setup",
                    $"'{target.name}' chỉ rộng khoảng {largest * 100f:0} cm, có vẻ không phải màn hình.\n\n" +
                    "Với iMac ở quầy, hãy chọn object 'MonitorScreen' (bên trong Computer_dfc1a9).",
                    "Vẫn tiếp tục", "Huỷ"))
                return;
        }

        Undo.SetCurrentGroupName("Move Computer UI");
        int group = Undo.GetCurrentGroup();

        // 1) Chuyển script mở máy tính sang màn hình mới
        if (oldInteract != null && oldInteract.gameObject != target)
            Undo.DestroyObjectImmediate(oldInteract);

        ComputerInteract interact = target.GetComponent<ComputerInteract>();
        if (interact == null) interact = Undo.AddComponent<ComputerInteract>(target);
        Undo.RecordObject(interact, "Assign Computer UI");
        interact.computerUI = computerUI;

        // 2) Collider + layer Interactable để tia nhìn của người chơi bắt được
        if (!TryGetBounds(target.transform, target.transform.rotation, canvasRect, out Vector3 min, out Vector3 max, localSpaceOf: target.transform))
        {
            EditorUtility.DisplayDialog("Computer Setup", "Object được chọn không có mesh nào để đo kích thước.", "OK");
            Undo.CollapseUndoOperations(group);
            return;
        }

        if (target.GetComponent<Collider>() == null)
        {
            BoxCollider box = Undo.AddComponent<BoxCollider>(target);
            box.center = (min + max) * 0.5f;
            box.size = max - min;
        }

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            Undo.RecordObject(target, "Set Layer");
            target.layer = interactableLayer;
        }

        // 3) Xoay canvas theo mặt màn hình, quay về phía camera Scene view, rồi đặt khớp vào
        Undo.RecordObject(canvasRect, "Rotate Computer UI");
        canvasRect.rotation = ComputeScreenRotation(target.transform, canvasRect, canvasRect.rotation);
        PlaceCanvas(canvasRect, target.transform);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(target.scene);
        Selection.activeGameObject = canvasRect.gameObject;

        Debug.Log($"[Computer Setup] Đã chuyển giao diện máy tính sang '{target.name}'. " +
                  "Nếu UI bị quay ngược, dùng Tools > Computer > Flip Computer UI 180 Degrees. Nhớ Ctrl+S để lưu scene.", target);
    }

    [MenuItem(FlipMenu)]
    private static void FlipCanvas()
    {
        ComputerInteract interact = Object.FindFirstObjectByType<ComputerInteract>(FindObjectsInactive.Include);
        if (interact == null || interact.computerUI == null) return;

        RectTransform canvasRect = FindRootCanvas(interact.computerUI.transform);
        if (canvasRect == null) return;

        Undo.RecordObject(canvasRect, "Flip Computer UI");
        canvasRect.rotation = Quaternion.AngleAxis(180f, Vector3.up) * canvasRect.rotation;
        PlaceCanvas(canvasRect, interact.transform);
        EditorSceneManager.MarkSceneDirty(interact.gameObject.scene);
    }

    // ================= Helpers =================
    /// <summary>
    /// Mặt màn hình = trục mỏng nhất của mesh. Canvas nhìn thấy từ phía -forward,
    /// nên forward được chọn hướng RA XA camera Scene view (người dùng đang nhìn vào màn hình).
    /// </summary>
    private static Quaternion ComputeScreenRotation(Transform screen, Transform ignoreUnder, Quaternion fallback)
    {
        if (!TryGetBounds(screen, Quaternion.identity, ignoreUnder, out Vector3 min, out Vector3 max, localSpaceOf: screen))
            return fallback;

        Vector3 size = Vector3.Scale(max - min, screen.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        Vector3[] axes = { screen.right, screen.up, screen.forward };
        float[] dims = { size.x, size.y, size.z };

        int thin = 0;
        for (int i = 1; i < 3; i++) if (dims[i] < dims[thin]) thin = i;

        // Không rõ trục mỏng (vật gần như khối vuông) -> giữ hướng cũ
        float second = Mathf.Min(dims[(thin + 1) % 3], dims[(thin + 2) % 3]);
        if (dims[thin] > second * 0.6f) return fallback;

        Vector3 normal = axes[thin].normalized;

        int upIndex = -1;
        float bestUp = -1f;
        for (int i = 0; i < 3; i++)
        {
            if (i == thin) continue;
            float d = Mathf.Abs(Vector3.Dot(axes[i], Vector3.up));
            if (d > bestUp) { bestUp = d; upIndex = i; }
        }
        Vector3 up = axes[upIndex] * Mathf.Sign(Vector3.Dot(axes[upIndex], Vector3.up));

        Vector3 center = screen.TransformPoint((min + max) * 0.5f);
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 toViewer = view != null && view.camera != null
            ? view.camera.transform.position - center
            : -(fallback * Vector3.forward);

        if (Vector3.Dot(normal, toViewer) > 0f) normal = -normal;
        return Quaternion.LookRotation(normal, up);
    }

    private static void PlaceCanvas(RectTransform canvasRect, Transform screen)
    {
        Quaternion rot = canvasRect.rotation;
        if (!TryGetBounds(screen, rot, canvasRect, out Vector3 min, out Vector3 max, localSpaceOf: null)) return;

        Vector3 size = max - min;
        Vector2 canvasSize = canvasRect.sizeDelta;
        float scale = Mathf.Min(size.x * FillRatio / Mathf.Max(1f, canvasSize.x),
                                size.y * FillRatio / Mathf.Max(1f, canvasSize.y));

        // UI nhìn thấy từ phía -forward của canvas, nên mặt trước màn hình là min.z
        Vector3 centerInFrame = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, min.z - SurfaceOffset);

        Undo.RecordObject(canvasRect, "Place Computer UI");
        canvasRect.position = rot * centerInFrame;
        canvasRect.localScale = new Vector3(scale, scale, scale);
    }

    /// <summary>
    /// Đo khung bao các mesh của obj. localSpaceOf != null: đo trong không gian local của transform đó;
    /// null: đo trong hệ trục xoay theo frameRotation (toạ độ đã xoay ngược).
    /// </summary>
    private static bool TryGetBounds(Transform obj, Quaternion frameRotation, Transform ignoreUnder,
                                     out Vector3 min, out Vector3 max, Transform localSpaceOf)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        Quaternion inv = Quaternion.Inverse(frameRotation);
        bool any = false;

        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || (ignoreUnder != null && mf.transform.IsChildOf(ignoreUnder))) continue;
            Renderer r = mf.GetComponent<Renderer>();
            if (r != null && !r.enabled) continue;

            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 world = mf.transform.TransformPoint(corner);
                Vector3 p = localSpaceOf != null ? localSpaceOf.InverseTransformPoint(world) : inv * world;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                any = true;
            }
        }
        return any;
    }

    private static RectTransform FindRootCanvas(Transform t)
    {
        RectTransform result = null;
        for (Transform p = t; p != null; p = p.parent)
            if (p.GetComponent<Canvas>() != null) result = p as RectTransform;
        return result;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }
}
