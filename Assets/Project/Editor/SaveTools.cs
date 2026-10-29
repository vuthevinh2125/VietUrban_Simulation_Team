using UnityEditor;
using UnityEngine;

/// <summary>Menu Tools > Save: xoá file lưu (chơi lại từ đầu) / mở thư mục chứa file lưu.</summary>
public static class SaveTools
{
    [MenuItem("Tools/Save/Delete Save (New Game)")]
    private static void Delete()
    {
        if (!EditorUtility.DisplayDialog("Delete Save", "Xoá file lưu? Lần chơi sau sẽ bắt đầu lại từ ngày 1.\n\n" + SaveManager.SavePath, "Xoá", "Huỷ")) return;
        SaveManager.DeleteSave();
    }

    [MenuItem("Tools/Save/Open Save Folder")]
    private static void Open() => EditorUtility.RevealInFinder(SaveManager.SavePath);
}
