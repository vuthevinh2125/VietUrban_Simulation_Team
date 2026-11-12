using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra kích thước hiển thị của mọi sản phẩm trên mọi loại kệ trong scene đang mở, xuất ra Logs/ProductSizes.csv.
/// Mỗi dòng: kích thước gốc của model, hệ số phóng kệ dùng (Shelf.FitFactor) và kích thước thật trên từng loại kệ.
/// Đánh dấu: cùng sản phẩm to nhỏ khác nhau giữa các kệ, hệ số chạm giới hạn min / max (model sai tỉ lệ),
/// và sản phẩm bị phóng lệch hẳn so với các sản phẩm cùng nhóm (to nhỏ không cân đối).
/// Menu: Tools > Store Setup > Report Product Sizes
/// </summary>
public static class ProductSizeReportTool
{
    private class ShelfType { public Shelf shelf; public string key; public Vector3 cell; }
    private class Row { public ItemData item; public Vector3 native; public string group; public float kMin = float.MaxValue, kMax = 0f, hMin = float.MaxValue, hMax = 0f; public bool clampMin, clampMax; public readonly List<string> cells = new List<string>(); }

    [MenuItem("Tools/Store Setup/Report Product Sizes")]
    public static void Run()
    {
        MethodInfo fit = typeof(Shelf).GetMethod("FitFactor", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo ensure = typeof(Shelf).GetMethod("EnsureCell", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo cellField = typeof(Shelf).GetField("cell", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fit == null || ensure == null || cellField == null) { Debug.LogError("[ProductSizes] Shelf.FitFactor / EnsureCell / cell not found."); return; }

        // Một kệ đại diện cho mỗi loại (loại hàng nhận + kích thước ô)
        var types = new List<ShelfType>();
        var seen = new HashSet<string>();
        foreach (Shelf s in Object.FindObjectsByType<Shelf>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.Capacity < 2) continue;
            ensure.Invoke(s, null);
            Vector3 c = (Vector3)cellField.GetValue(s);
            string key = $"{(string.IsNullOrEmpty(s.acceptsDisplayType) ? "any" : s.acceptsDisplayType)} {c.x:F2}x{c.y:F2}x{c.z:F2}";
            if (seen.Add(key)) types.Add(new ShelfType { shelf = s, key = key + " '" + s.name + "'", cell = c });
        }
        if (types.Count == 0) { EditorUtility.DisplayDialog("Report Product Sizes", "Open the supermarket scene first (no shelves found).", "OK"); return; }

        var rows = new List<Row>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null) continue;
            var row = new Row { item = item, group = DemandModel.GroupOf(item).ToString() };
            rows.Add(row);
            GameObject prefab = item.ResolveProductPrefab();
            if (prefab == null) continue;

            GameObject go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            Vector3 wanted = item.shelfScale != Vector3.zero ? item.shelfScale : Vector3.one;
            row.native = Vector3.Scale(ProductFit.LocalBounds(go.transform).size, wanted);
            Object.DestroyImmediate(go);

            foreach (ShelfType t in types)
            {
                string accepts = t.shelf.acceptsDisplayType;
                if (!string.IsNullOrEmpty(accepts) && !string.Equals(item.displayType, accepts, System.StringComparison.OrdinalIgnoreCase)) continue;
                float k = (float)fit.Invoke(t.shelf, new object[] { row.native });
                float h = row.native.y * k;
                row.kMin = Mathf.Min(row.kMin, k); row.kMax = Mathf.Max(row.kMax, k);
                row.hMin = Mathf.Min(row.hMin, h); row.hMax = Mathf.Max(row.hMax, h);
                float lo = t.shelf.keepRealSize ? Mathf.Min(t.shelf.minFitScale, 0.35f) : t.shelf.minFitScale;
                if (Mathf.Approximately(k, lo)) row.clampMin = true;
                if (!t.shelf.keepRealSize && Mathf.Approximately(k, t.shelf.maxFitScale)) row.clampMax = true;
                row.cells.Add($"{t.key}: x{k:F2} -> {row.native.x * k * 100f:F0}x{h * 100f:F0}x{row.native.z * k * 100f:F0} cm");
            }
        }

        // Hệ số phóng trung vị của mỗi nhóm hàng: sản phẩm lệch quá 1.6 lần so với nhóm là to / nhỏ bất thường
        var groupK = new Dictionary<string, List<float>>();
        foreach (Row r in rows) if (r.kMax > 0f) { if (!groupK.ContainsKey(r.group)) groupK[r.group] = new List<float>(); groupK[r.group].Add(r.kMin); }
        var median = new Dictionary<string, float>();
        foreach (var kv in groupK) { kv.Value.Sort(); median[kv.Key] = kv.Value[kv.Value.Count / 2]; }

        var sb = new StringBuilder("product,group,displayType,native_cm,k_min,k_max,height_min_cm,height_max_cm,flags,per_shelf\n");
        int flagged = 0;
        var summary = new StringBuilder();
        foreach (Row r in rows)
        {
            var flags = new List<string>();
            if (r.item.ResolveProductPrefab() == null) flags.Add("no prefab");
            else if (r.kMax <= 0f) flags.Add("no shelf accepts it");
            else
            {
                if (r.hMax / Mathf.Max(0.001f, r.hMin) > 1.1f) flags.Add($"size differs x{r.hMax / r.hMin:F2} between shelves");
                if (r.clampMin) flags.Add("model too big (k at min)");
                if (r.clampMax) flags.Add("model too small (k at max)");
                float m = median[r.group];
                bool realSize = types[0].shelf.keepRealSize; // giữ cỡ thật thì hệ số 1 là bình thường, không so với nhóm
                if (!realSize && (r.kMin > m * 1.6f || r.kMin < m / 1.6f)) flags.Add($"scaled x{r.kMin:F2} vs group median x{m:F2}");
            }
            if (flags.Count > 0) { flagged++; summary.AppendLine($"  {r.item.itemName}: {string.Join("; ", flags)}"); }
            sb.AppendLine(string.Join(",",
                Csv(r.item.itemName), r.group, Csv(r.item.displayType),
                $"{r.native.x * 100f:F1}x{r.native.y * 100f:F1}x{r.native.z * 100f:F1}",
                r.kMax > 0f ? r.kMin.ToString("F2") : "", r.kMax > 0f ? r.kMax.ToString("F2") : "",
                r.kMax > 0f ? (r.hMin * 100f).ToString("F1") : "", r.kMax > 0f ? (r.hMax * 100f).ToString("F1") : "",
                Csv(string.Join("; ", flags)), Csv(string.Join(" | ", r.cells))));
        }

        string dir = Path.Combine(Application.dataPath, "..", "Logs");
        Directory.CreateDirectory(dir);
        string path = Path.GetFullPath(Path.Combine(dir, "ProductSizes.csv"));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        Debug.Log($"[ProductSizes] {rows.Count} products, {types.Count} shelf types, {flagged} flagged. Saved to {path}\n{summary}");
        EditorUtility.RevealInFinder(path);
    }

    private static string Csv(string s) => s == null ? "" : (s.Contains(",") || s.Contains("\"") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);
}
