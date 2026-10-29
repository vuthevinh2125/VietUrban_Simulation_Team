using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Cleanup > Remove Missing Scripts
// Removes "Missing Script" components from every prefab in Assets/Project
// and from every GameObject in the currently open scene(s).
public static class RemoveMissingScriptsTool
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts")]
    public static void Run()
    {
        int removed = 0;

        // 1) Prefab assets first, so prefab instances in the scene are fixed too
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Cleanup] {path}: removed {count} missing script(s)");
                removed += count;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2) Objects in the open scene(s)
        for (int s = 0; s < EditorSceneManager.sceneCount; s++)
        {
            var scene = EditorSceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (count > 0)
                    {
                        Debug.Log($"[Cleanup] {scene.name}/{t.name}: removed {count} missing script(s)", t.gameObject);
                        removed += count;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Cleanup] Done. Total missing scripts removed: {removed}. Remember to save the scene (Ctrl+S).");
    }
}
