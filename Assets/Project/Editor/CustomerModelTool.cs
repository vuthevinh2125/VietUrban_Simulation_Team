using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Store Setup > 12. Build Customer Models
/// Mọi nhân vật Mixamo "ChXX_nonPBR.fbx" mới trong Prefabs/Characters (trừ 2 model nhân viên) -> prefab khách giống NPC_01:
/// rig Humanoid, NPC_Animator (đứng / đi bộ), cùng NavMeshAgent + CapsuleCollider như NPC_01.
/// Rồi thêm hết vào CustomerSpawner để khách đa dạng. Chạy lại được.
/// </summary>
public static class CustomerModelTool
{
    private const string CharFolder = "Assets/Project/Prefabs/Characters";
    private const string OutFolder = CharFolder + "/Customers";
    private const string Template = CharFolder + "/NPC_01.prefab";
    private const string ControllerPath = "Assets/Assets_ThirdParty/Materials/NPC_Animator.controller";
    private static readonly string[] StaffModels = { "Ch12_nonPBR", "Ch13_nonPBR" };

    [MenuItem("Tools/Store Setup/12. Build Customer Models")]
    private static void Run()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(Template);
        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (template == null) { EditorUtility.DisplayDialog("Customer Models", "Không thấy " + Template, "OK"); return; }
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(CharFolder, "Customers");

        var models = Directory.GetFiles(CharFolder, "Ch*_nonPBR*.fbx")
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !StaffModels.Any(s => Path.GetFileNameWithoutExtension(p) == s))
            .OrderBy(p => p).ToList();

        var built = new List<GameObject>();
        var log = new List<string>();
        try
        {
            for (int i = 0; i < models.Count; i++)
            {
                string path = models[i];
                EditorUtility.DisplayProgressBar("Customer Models", Path.GetFileName(path), (float)i / models.Count);
                MakeHumanoid(path);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    string id = Path.GetFileNameWithoutExtension(path).Replace("_nonPBR", "").Replace(" (1)", "").Replace(" ", "");
                    inst.name = "NPC_" + id;
                    Animator anim = inst.GetComponent<Animator>();
                    if (anim == null) anim = inst.AddComponent<Animator>();
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;

                    // Sao chép NavMeshAgent / CapsuleCollider / Rigidbody của NPC_01 cho giống hệt khách cũ
                    CopyComponent<NavMeshAgent>(template, inst);
                    CopyComponent<CapsuleCollider>(template, inst);
                    CopyComponent<Rigidbody>(template, inst);

                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(inst, $"{OutFolder}/{inst.name}.prefab");
                    if (prefab != null) built.Add(prefab);
                    log.Add($"{inst.name}{(anim.avatar != null && anim.avatar.isHuman ? "" : " (KHÔNG humanoid!)")}");
                }
                finally { Object.DestroyImmediate(inst); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // Thêm vào CustomerSpawner (giữ NPC_01 / NPC_02 cũ)
        CustomerSpawner spawner = Object.FindFirstObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        string spawnInfo = "Không thấy CustomerSpawner trong scene.";
        if (spawner != null)
        {
            var list = (spawner.customerPrefabs ?? new GameObject[0]).Where(p => p != null).ToList();
            foreach (GameObject p in built) if (!list.Contains(p)) list.Add(p);
            Undo.RecordObject(spawner, "Customer models");
            spawner.customerPrefabs = list.ToArray();
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            spawnInfo = $"CustomerSpawner giờ có {list.Count} kiểu khách.";
        }

        Debug.Log($"[Customer Models] {built.Count} model: {string.Join(", ", log)}. {spawnInfo}");
        EditorUtility.DisplayDialog("Customer Models", $"Đã tạo {built.Count} kiểu khách mới:\n{string.Join("\n", log)}\n\n{spawnInfo}\nBấm Ctrl+S để lưu scene.", "OK");
    }

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

    private static void CopyComponent<T>(GameObject from, GameObject to) where T : Component
    {
        T src = from.GetComponent<T>();
        if (src == null) return;
        T dst = to.GetComponent<T>();
        if (dst == null) dst = to.AddComponent<T>();
        EditorUtility.CopySerialized(src, dst);
    }
}
