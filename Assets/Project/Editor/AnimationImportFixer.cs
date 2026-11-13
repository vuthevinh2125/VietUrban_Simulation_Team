using UnityEditor;
using UnityEngine;

/// <summary>
/// Hoạt ảnh Mixamo trong Assets/Project/Resources/Animations phải import dạng Humanoid (avatar tạo từ chính model)
/// thì mới dùng chung được cho mọi nhân vật (ClipPlayer). Script này tự lo việc đó:
///  - Khi import / import lại: đặt Animation Type = Humanoid, Avatar = Create From This Model, lặp cho các hoạt ảnh đứng / đi.
///  - Khi Editor nạp xong script: file nào còn sai thiết lập thì sửa và import lại ngay (không cần bấm gì trong Inspector).
/// Thả thêm file FBX mới vào thư mục trên cũng được thiết lập tự động.
/// </summary>
public class AnimationImportFixer : AssetPostprocessor
{
    private const string Folder = "Assets/Project/Resources/Animations/";

    // Hoạt ảnh chạy một lần (không lặp): ngã xuống, chỉ tay, quay đầu
    private static bool OneShot(string path) =>
        path.Contains("Dying") || path.Contains("Pointing") || path.Contains("Turn_180");

    private static bool Ours(string path) => path.Replace('\\', '/').StartsWith(Folder) && path.ToLowerInvariant().EndsWith(".fbx");

    void OnPreprocessModel()
    {
        if (!Ours(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        Apply(mi, assetPath);
    }

    private static bool Apply(ModelImporter mi, string path)
    {
        bool changed = false;
        if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; changed = true; }
        if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
        if (!mi.importAnimation) { mi.importAnimation = true; changed = true; }
        if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        // Lặp + giữ nguyên hướng / độ cao (bake vào pose) để nhân vật đứng tại chỗ, không trôi
        ModelImporterClipAnimation[] clips = mi.clipAnimations;
        if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            bool loop = !OneShot(path), clipChanged = false;
            foreach (ModelImporterClipAnimation c in clips)
            {
                if (c.loopTime != loop) { c.loopTime = loop; clipChanged = true; }
                if (!c.lockRootRotation) { c.lockRootRotation = true; clipChanged = true; }
                if (!c.lockRootHeightY) { c.lockRootHeightY = true; clipChanged = true; }
                if (!c.keepOriginalOrientation) { c.keepOriginalOrientation = true; clipChanged = true; }
                if (!c.keepOriginalPositionY) { c.keepOriginalPositionY = true; clipChanged = true; }
            }
            if (clipChanged || mi.clipAnimations == null || mi.clipAnimations.Length == 0) { mi.clipAnimations = clips; changed = true; }
        }
        return changed;
    }

    [InitializeOnLoadMethod]
    private static void FixExisting()
    {
        // Chạy sau khi Editor nạp xong (không import trong lúc đang nạp script)
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Ours(path)) continue;
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                if (Apply(mi, path)) { mi.SaveAndReimport(); n++; }
            }
            if (n > 0) Debug.Log($"[Animations] Đã chuyển {n} hoạt ảnh Mixamo sang Humanoid và import lại.");
        };
    }
}
