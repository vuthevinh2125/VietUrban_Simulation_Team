using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Store Setup > 10. Build Staff Models & Shopping Props
///  - Model nhân viên từ 2 nhân vật Mixamo bạn tải về (Ch12 / Ch13): chuyển sang rig Humanoid để dùng chung
///    bộ chuyển động đứng yên / đi bộ (NPC_Animator, tham số Speed). Model THẤP hơn (thường là nữ) = thu ngân,
///    model còn lại = nhân viên xếp kệ. Sai thì dùng 10b để đổi.
///  - Giỏ hàng và xe đẩy cho khách (từ gói ModernSupermarket), đã gỡ va chạm, gốc đặt ở đáy.
/// Tất cả lưu trong Assets/Project/Resources để game tự nạp, không cần sửa scene.
/// </summary>
public static class StaffModelTool
{
    private const string CharFolder = "Assets/Project/Prefabs/Characters";
    private const string ControllerPath = "Assets/Assets_ThirdParty/Materials/NPC_Animator.controller";
    private const string FallbackModel = CharFolder + "/Guard.fbx";
    private const string FallbackController = CharFolder + "/GuardController.controller";
    private const string PropsFolder = "Assets/Assets_ThirdParty/ModernSupermarket/Prefabs/Props/Utility";
    private const string OutFolder = "Assets/Project/Resources";

    private static readonly string[] StaffModels = { CharFolder + "/Ch12_nonPBR.fbx", CharFolder + "/Ch13_nonPBR.fbx" };

    [MenuItem("Tools/Store Setup/10. Build Staff Models & Shopping Props")]
    private static void Run() => Build(false);

    [MenuItem("Tools/Store Setup/10b. Swap Cashier and Restocker Models")]
    private static void Swap() => Build(true);

    private static void Build(bool swap)
    {
        EnsureFolder(OutFolder);
        EnsureFolder(OutFolder + "/Props");
        var log = new List<string>();

        // ---------- Nhân viên ----------
        var models = new List<(string path, float height)>();
        foreach (string path in StaffModels)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add($"Không thấy {path}"); continue; }
            MakeHumanoid(path);
            GameObject m = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            models.Add((path, Height(m)));
        }

        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (models.Count >= 2)
        {
            var ordered = models.OrderBy(m => m.height).ToList(); // thấp hơn = nữ = thu ngân
            if (swap) ordered.Reverse();
            BuildStaffPrefab(ordered[0].path, ctrl, "Staff_Cashier", log);
            BuildStaffPrefab(ordered[1].path, ctrl, "Staff_Restocker", log);
            log.Add($"Thu ngân = {System.IO.Path.GetFileNameWithoutExtension(ordered[0].path)} (cao {ordered[0].height:F2}m), " +
                    $"xếp kệ = {System.IO.Path.GetFileNameWithoutExtension(ordered[1].path)} (cao {ordered[1].height:F2}m)");
        }
        else
        {
            RuntimeAnimatorController guardCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackController);
            BuildStaffPrefab(FallbackModel, guardCtrl, "Staff_Cashier", log);
            BuildStaffPrefab(FallbackModel, guardCtrl, "Staff_Restocker", log);
            log.Add("Không đủ 2 model Ch12/Ch13 -> dùng tạm Guard cho cả hai.");
        }

        // ---------- Giỏ / xe đẩy ----------
        BuildProp($"{PropsFolder}/Basket_x5gi0j.prefab", "Basket", true, log); // giỏ lưới sắt có quai (ở cửa hàng)
        BuildProp($"{PropsFolder}/Cart_nabnuh.prefab", "Cart", true, log);

        AssetDatabase.SaveAssets();
        string msg = string.Join("\n", log);
        Debug.Log("[Staff Model] " + msg);
        EditorUtility.DisplayDialog("Staff Models & Props", msg + "\n\nBấm Play lại / thuê lại nhân viên để thấy model mới.", "OK");
    }

    /// <summary>Rig Humanoid (avatar tạo từ chính model) để dùng được chuyển động Mixamo dùng chung.</summary>
    private static void MakeHumanoid(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        if (importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }
        MixamoMaterialFixer.Fix(path); // tách texture + vật liệu URP để model không bị trắng
    }

    private static void BuildStaffPrefab(string modelPath, RuntimeAnimatorController ctrl, string outName, List<string> log)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { log.Add($"Không thấy {modelPath}"); return; }

        var root = new GameObject(outName);
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            body.transform.SetParent(root.transform, false);
            float h = Height(body);
            if (h > 0f && (h < 1.2f || h > 2.4f)) body.transform.localScale *= 1.72f / h;

            Animator anim = body.GetComponent<Animator>();
            if (anim == null) anim = body.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = 1.6f;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.1f;

            PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/{outName}.prefab");
            log.Add($"Đã tạo {outName} từ {System.IO.Path.GetFileName(modelPath)} (avatar: {(anim.avatar != null && anim.avatar.isHuman ? "Humanoid" : "KHÔNG humanoid - chuyển động có thể không chạy")})");
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// <summary>Giỏ / xe đẩy chỉ để trưng: gỡ va chạm + script, gốc ở giữa đáy, xe đẩy dài theo trục Z.</summary>
    private static void BuildProp(string srcPath, string outName, bool alongZ, List<string> log)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
        if (src == null) { log.Add($"Không thấy {srcPath}"); return; }
        var root = new GameObject(outName);
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.transform.SetParent(root.transform, false);
            foreach (Rigidbody rb in body.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (MonoBehaviour mb in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);

            Bounds b = ProductFit.LocalBounds(root.transform);
            if (alongZ && b.size.x > b.size.z) body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            b = ProductFit.LocalBounds(root.transform);
            body.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

            PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/Props/{outName}.prefab");
            log.Add($"Đã tạo {outName} ({b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m)");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static float Height(GameObject go)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r is SkinnedMeshRenderer smr && smr.sharedMesh != null
                ? new Bounds(r.transform.TransformPoint(smr.sharedMesh.bounds.center), Vector3.Scale(smr.sharedMesh.bounds.size, r.transform.lossyScale))
                : r.bounds;
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        return any ? b.size.y : 0f;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
