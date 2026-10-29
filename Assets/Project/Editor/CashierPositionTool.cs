using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Đặt chỗ đứng của thu ngân bằng cách kéo thả lúc đang chơi:
///  1. Play, thuê thu ngân, kéo từng người (hoặc model con của họ) tới đúng chỗ + xoay đúng hướng trong cửa sổ Scene.
///  2. Bấm Tools > Store Setup > 14. Remember Cashier Positions (khi đang Play).
///  3. Dừng Play: vị trí được ghi vào "CashierPoint" của từng quầy trong scene. Bấm Ctrl+S.
/// Lần sau thuê thu ngân sẽ đứng đúng chỗ đó.
/// </summary>
[InitializeOnLoad]
public static class CashierPositionTool
{
    private const string Key = "VietUrban.CashierPositions";

    [System.Serializable] private class Entry { public string counter; public Vector3 pos; public float yaw; }
    [System.Serializable] private class Data { public List<Entry> list = new List<Entry>(); }

    static CashierPositionTool()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.HasKey(Key)) Apply();
        };
    }

    [MenuItem("Tools/Store Setup/14. Remember Cashier Positions (khi đang Play)")]
    private static void Remember()
    {
        if (!Application.isPlaying || StaffManager.Instance == null)
        {
            EditorUtility.DisplayDialog("Cashier Positions", "Hãy bấm Play, thuê thu ngân và kéo họ tới chỗ muốn đứng trước, rồi bấm lại menu này.", "OK");
            return;
        }
        var data = new Data();
        foreach (var c in StaffManager.Instance.Cashiers)
        {
            if (c.counter == null || c.body == null) continue;
            // Lấy theo model nhìn thấy được (người dùng có thể đã kéo model con thay vì gốc)
            Animator anim = c.body.GetComponentInChildren<Animator>();
            Transform t = anim != null ? anim.transform : c.body.transform;
            data.list.Add(new Entry { counter = c.counter.name, pos = t.position, yaw = t.eulerAngles.y });
        }
        EditorPrefs.SetString(Key, JsonUtility.ToJson(data));
        EditorUtility.DisplayDialog("Cashier Positions", $"Đã nhớ vị trí {data.list.Count} thu ngân.\nDừng Play để ghi vào scene, rồi bấm Ctrl+S.", "OK");
    }

    private static void Apply()
    {
        var data = JsonUtility.FromJson<Data>(EditorPrefs.GetString(Key));
        EditorPrefs.DeleteKey(Key);
        if (data == null || data.list.Count == 0) return;

        var counters = Object.FindObjectsByType<CheckoutCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int done = 0;
        foreach (Entry e in data.list)
        {
            CheckoutCounter counter = System.Array.Find(counters, c => c.name == e.counter);
            if (counter == null) continue;

            Transform point = counter.cashierPoint != null ? counter.cashierPoint : counter.transform.Find("CashierPoint");
            if (point == null)
            {
                point = new GameObject("CashierPoint").transform;
                Undo.RegisterCreatedObjectUndo(point.gameObject, "Cashier point");
                point.SetParent(counter.transform, false);
            }
            Undo.RecordObject(point, "Cashier point");
            point.SetPositionAndRotation(e.pos, Quaternion.Euler(0f, e.yaw, 0f));

            Undo.RecordObject(counter, "Cashier point");
            counter.cashierPoint = point;
            EditorUtility.SetDirty(counter);
            if (PrefabUtility.IsPartOfPrefabInstance(counter)) PrefabUtility.RecordPrefabInstancePropertyModifications(counter);
            EditorSceneManager.MarkSceneDirty(counter.gameObject.scene);
            done++;
        }
        Debug.Log($"[Cashier Positions] Đã ghi chỗ đứng của {done} thu ngân vào scene (CashierPoint). Nhớ Ctrl+S.");
        EditorUtility.DisplayDialog("Cashier Positions", $"Đã ghi chỗ đứng của {done} thu ngân vào scene.\nBấm Ctrl+S để lưu.", "OK");
    }
}
