using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tạo model nhân viên (khác với khách) từ Guard.fbx + GuardController, lưu vào Resources/Staff_Worker.prefab
/// để StaffManager tự dùng khi thuê thu ngân / nhân viên xếp kệ.
/// Menu: Tools > Store Setup > 10. Build Staff Model
/// </summary>
public static class StaffModelTool
{
    private const string ModelPath = "Assets/Project/Prefabs/Characters/Guard.fbx";
    private const string ControllerPath = "Assets/Project/Prefabs/Characters/GuardController.controller";
    private const string OutFolder = "Assets/Project/Resources";
    private const string OutPath = OutFolder + "/Staff_Worker.prefab";

    [MenuItem("Tools/Store Setup/10. Build Staff Model")]
    private static void Run()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (model == null) { EditorUtility.DisplayDialog("Staff Model", "Không thấy " + ModelPath, "OK"); return; }

        var root = new GameObject("Staff_Worker");
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            // Đưa về chiều cao người thật (~1.75m) nếu model bị scale lạ
            Bounds b = default; bool any = false;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && (b.size.y < 1.2f || b.size.y > 2.4f)) body.transform.localScale *= 1.75f / Mathf.Max(0.01f, b.size.y);

            Animator anim = body.GetComponent<Animator>();
            if (anim == null) anim = body.AddComponent<Animator>();
            if (controller != null) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = 1.8f;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.1f;

            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/Project", "Resources");
            PrefabUtility.SaveAsPrefabAsset(root, OutPath);
            Debug.Log($"[Staff Model] Đã tạo {OutPath} (controller: {(controller != null ? controller.name : "KHÔNG CÓ")}, cao {(any ? b.size.y : 0f):F2}m).");
            EditorUtility.DisplayDialog("Staff Model", $"Đã tạo model nhân viên:\n{OutPath}\n\nThuê nhân viên mới (hoặc bấm Play lại) để thấy model mới.", "OK");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
