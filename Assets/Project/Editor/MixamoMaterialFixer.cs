using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sửa nhân vật Mixamo (ChXX_nonPBR.fbx) bị TRẮNG TINH: texture nằm nhúng trong file fbx nên vật liệu URP không thấy.
/// Cách sửa (giống cách NPC_01 / Ch28 đang dùng): tách texture ra file riêng, tạo vật liệu URP Lit cho từng vật liệu
/// của model (tóc / lông mi có cắt alpha), rồi gán lại (remap) vào fbx.
/// Menu: Tools > Store Setup > 13. Fix Character Colors (Mixamo). Công cụ 10 và 12 cũng tự gọi.
/// </summary>
public static class MixamoMaterialFixer
{
    private const string CharFolder = "Assets/Project/Prefabs/Characters";
    private const string TexRoot = CharFolder + "/Textures";
    private const string MatRoot = CharFolder + "/Materials";

    [MenuItem("Tools/Store Setup/13. Fix Character Colors (Mixamo)")]
    private static void FixAll()
    {
        string[] fbx = Directory.GetFiles(CharFolder, "Ch*_nonPBR*.fbx").Select(p => p.Replace('\\', '/')).ToArray();
        int ok = 0;
        try
        {
            for (int i = 0; i < fbx.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Fix Character Colors", Path.GetFileName(fbx[i]), (float)i / fbx.Length);
                if (Fix(fbx[i])) ok++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log($"[Mixamo] Đã sửa màu {ok}/{fbx.Length} nhân vật.");
        EditorUtility.DisplayDialog("Fix Character Colors", $"Đã sửa màu {ok}/{fbx.Length} nhân vật.\nBấm Play lại để xem.", "OK");
    }

    /// <summary>Sửa một file fbx. Trả về true nếu đã có vật liệu có texture.</summary>
    public static bool Fix(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return false;
        string name = Path.GetFileNameWithoutExtension(fbxPath).Replace(" ", "");
        string id = name.Split('_')[0]; // "Ch01"
        string texDir = $"{TexRoot}/{name}";
        string matDir = $"{MatRoot}/{name}";
        EnsureFolder(texDir);
        EnsureFolder(matDir);

        // 1) Tách texture nhúng ra file
        if (!AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }).Any())
        {
            importer.ExtractTextures(texDir);
            AssetDatabase.Refresh();
        }
        Texture2D diffuse = FindTex(texDir, "Diffuse");
        Texture2D normal = FindTex(texDir, "Normal");
        if (normal != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
        if (diffuse == null) { Debug.LogWarning($"[Mixamo] {name}: không tách được texture màu."); return false; }

        // 2) Tên các vật liệu trong model
        var matNames = new HashSet<string>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is Material m) matNames.Add(m.name);
        if (matNames.Count == 0)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model != null)
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials) if (m != null) matNames.Add(m.name);
        }

        // 3) Tạo vật liệu URP Lit + gán lại vào fbx
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string matName in matNames)
        {
            string path = $"{matDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(lit != null ? lit : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", diffuse);
            mat.SetTexture("_MainTex", diffuse);
            mat.SetColor("_BaseColor", Color.white);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetFloat("_Smoothness", 0.2f);

            string lower = matName.ToLower();
            if (lower.Contains("hair") || lower.Contains("lash") || lower.Contains("brow"))
            {
                // Tóc / lông mi: cắt theo alpha, vẽ 2 mặt
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        Debug.Log($"[Mixamo] {name}: {matNames.Count} vật liệu dùng {diffuse.name}.");
        return true;
    }

    private static Texture2D FindTex(string dir, string kind)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p).IndexOf(kind, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
