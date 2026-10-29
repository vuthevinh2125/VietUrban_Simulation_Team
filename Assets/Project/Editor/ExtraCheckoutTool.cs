using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Store Setup > 11. Setup Extra Checkouts
/// Lấy 3 quầy thu ngân còn lại trong cửa hàng (gần quầy chính nhất), gắn CheckoutCounter (chạy lại công cụ số 3 cho từng quầy)
/// và LockedCheckout: ẩn khi vào game, mua trên máy tính (tab Staff) khi đủ cấp + tiền.
/// </summary>
public static class ExtraCheckoutTool
{
    private const string StoreRoot = "Supermarket_full";
    private const string SetupMenu = "Tools/Store Setup/3. Setup Checkout & Customers (chọn 1 quầy Checkout trước)";
    private static readonly (int level, float price)[] Tiers = { (3, 800f), (5, 1500f), (7, 2500f) };

    [MenuItem("Tools/Store Setup/11. Setup Extra Checkouts")]
    private static void Run()
    {
        CheckoutCounter main = Object.FindObjectsByType<CheckoutCounter>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetComponent<LockedCheckout>() == null);
        if (main == null) { EditorUtility.DisplayDialog("Extra Checkouts", "Chưa có quầy chính (chạy công cụ số 3 trước).", "OK"); return; }

        GameObject store = GameObject.Find(StoreRoot);
        IEnumerable<Transform> pool = store != null ? store.GetComponentsInChildren<Transform>() : Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var candidates = pool
            .Where(t => t.name.StartsWith("Checkout_") && t.gameObject != main.gameObject && t.GetComponentInChildren<Renderer>() != null)
            .Where(t => t.parent == null || !t.parent.name.StartsWith("Checkout_")) // chỉ lấy gốc của quầy
            .OrderBy(t => (t.position - main.transform.position).sqrMagnitude)
            .Take(Tiers.Length)
            .ToList();

        var log = new StringBuilder($"# Extra checkouts (quầy chính: {main.name})\n");
        Undo.SetCurrentGroupName("Setup extra checkouts");
        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject go = candidates[i].gameObject;
            Selection.activeGameObject = go;
            EditorApplication.ExecuteMenuItem(SetupMenu); // điểm khách đứng + chỗ đặt hàng giống quầy chính

            LockedCheckout lockC = go.GetComponent<LockedCheckout>();
            if (lockC == null) lockC = Undo.AddComponent<LockedCheckout>(go);
            Undo.RecordObject(lockC, "Locked checkout");
            lockC.order = i + 1;
            lockC.requiredLevel = Tiers[i].level;
            lockC.price = Tiers[i].price;
            lockC.displayName = $"Checkout counter #{i + 2}";
            lockC.id = $"checkout_{i + 2}";
            EditorUtility.SetDirty(lockC);
            if (PrefabUtility.IsPartOfPrefabInstance(lockC)) PrefabUtility.RecordPrefabInstancePropertyModifications(lockC);
            log.AppendLine($"- #{i + 2}: {go.name} tại {go.transform.position:F1} -> cấp {lockC.requiredLevel}, {lockC.price}$");
        }
        EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        Selection.activeGameObject = main.gameObject;
        Debug.Log("[Extra Checkouts] " + log);
        EditorUtility.DisplayDialog("Extra Checkouts", log + "\nCác quầy này sẽ ẩn khi chơi cho tới khi mua (tab Staff trên máy tính).\nBấm Ctrl+S để lưu scene.", "OK");
    }
}
