using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Đưa sản phẩm trong gói ModernSupermarket vào hệ thống bán hàng:
///  - Mỗi prefab sản phẩm -> 1 ItemData (tên, giá nhập theo loại hàng, số món/thùng theo kích thước, Fresh/Shelf, icon chụp từ model)
///  - Thùng hàng dùng chung Crate_Generic (thùng carton rỗng, giao hàng nào thì chứa hàng đó)
///  - Gắn danh mục vào ShopCatalog trên máy tính -> máy tính tự sinh thẻ sản phẩm
/// Chạy lại được nhiều lần: ItemData đã có thì giữ nguyên giá bạn đã sửa.
/// Menu: Tools > Store Setup > 7. Import Products
/// </summary>
public static class ProductImportTool
{
    private const string SourceFolder = "Assets/Assets_ThirdParty/ModernSupermarket/Prefabs/Props/Products";
    private const string DataFolder = "Assets/Project/Data/Products";
    private const string IconFolder = "Assets/Project/Data/Icons";
    private const string CratePath = "Assets/Project/Prefabs/Building/Crate_Generic.prefab";
    private static readonly string[] CrateTemplates =
    {
        "Assets/Project/Prefabs/Building/Cardboard Box (Closed) (1).prefab",
        "Assets/Project/Prefabs/Building/Cardboard Box (Closed).prefab"
    };

    private static readonly string[] Excluded = { "Book", "Decoration", "Paintings", "Vase", "Slate", "ToyCar", "Bag_", "Alcool" };
    private static readonly string[] FreshKeys = { "Butter", "Cheese", "Chicken", "CrabSticks", "Cream", "Ham", "MeatTray", "Milk", "Frozen", "Pizza", "Sushis", "Cake" };

    // Giá NHẬP 1 món ($). Thứ tự quan trọng: khoá dài đặt trước khoá ngắn (BeerKeg trước Beer).
    private static readonly (string key, float price)[] Prices =
    {
        ("BeerKeg", 40f), ("BeerPack", 6f), ("Beer", 1.5f), ("Alcohol", 9f), ("Bleach", 2.5f), ("Butter", 2.2f),
        ("Cake", 4f), ("CandyPack", 1.8f), ("CansPack", 4f), ("Capsule", 3f), ("Cereals", 3f), ("Cheese", 3.5f),
        ("Chicken", 5f), ("Chips", 1.5f), ("ChocolatePowder", 3f), ("ChocolateSpread", 2.8f), ("Cleanser", 2.5f),
        ("Coffee", 5f), ("CrabSticks", 3f), ("Cream", 1.8f), ("FlourPack", 1.2f), ("FrozenPack", 3.5f), ("Frozen", 3f),
        ("Ham", 3.5f), ("JuiceBrick", 1.2f), ("Laundry", 6f), ("MashPotatoe", 2f), ("MeatTray", 6f), ("MilkBrick", 1.2f),
        ("Milk", 1.5f), ("Oil", 3f), ("PaperTowel", 2f), ("Pasta", 1.2f), ("Pepper", 1.5f), ("PetFood", 4f), ("Pizza", 4f),
        ("PotatoeBag", 3f), ("RicePack", 2.5f), ("Salt", 0.8f), ("Sauce", 1.8f), ("Shampoo", 3.5f), ("SlicedBread", 1.5f),
        ("SliceBread", 1.5f), ("Soap", 1.5f), ("Soda", 1f), ("Sugar", 1.2f), ("Sushis", 6f), ("SweetJar", 2.5f),
        ("Teabox", 2.5f), ("TinCan", 1.2f), ("ToiletPaper", 4f), ("Toothpaste", 2f), ("WaterBottle", 0.6f), ("WaterPack", 3f)
    };

    private struct PendingIcon { public GameObject prefab; public ItemData data; public string path; }
    private static List<PendingIcon> pendingIcons;
    private static double iconStartTime;

    [MenuItem("Tools/Store Setup/7. Import Products (gói ModernSupermarket)")]
    private static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SourceFolder });
        var prefabs = guids.Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == SourceFolder)
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
            .Where(g => g != null && !g.name.Contains("_Pack") && !g.name.Contains("_Var") && !Excluded.Any(e => g.name.StartsWith(e)))
            .OrderBy(g => g.name)
            .ToList();

        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("Import Products", "Không tìm thấy prefab sản phẩm trong\n" + SourceFolder, "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Import Products",
                $"Tạo/cập nhật {prefabs.Count} mặt hàng từ gói ModernSupermarket và đưa lên máy tính?\n\n" +
                "Dữ liệu lưu ở Assets/Project/Data. Chạy lại được, giá bạn đã sửa sẽ được giữ.", "Import", "Huỷ"))
            return;

        EnsureFolder(DataFolder);
        EnsureFolder(IconFolder);
        GameObject crate = EnsureGenericCrate();

        var usedNames = new Dictionary<string, int>();
        var items = new List<ItemData>();
        pendingIcons = new List<PendingIcon>();
        AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(256, prefabs.Count * 2));

        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                EditorUtility.DisplayProgressBar("Import Products", prefab.name, (float)i / prefabs.Count);

                string pretty = PrettyName(prefab.name);
                usedNames.TryGetValue(pretty, out int n);
                usedNames[pretty] = n + 1;
                if (n > 0) pretty = $"{pretty} {n + 1}";

                string assetPath = $"{DataFolder}/{SafeFileName(pretty)}.asset";
                ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                bool isNew = data == null;
                if (isNew)
                {
                    data = ScriptableObject.CreateInstance<ItemData>();
                    AssetDatabase.CreateAsset(data, assetPath);
                }

                data.itemName = pretty;
                data.description = prefab.name;
                data.brand = string.IsNullOrEmpty(data.brand) ? "VietUrban" : data.brand;
                data.displayType = FreshKeys.Any(k => prefab.name.StartsWith(k)) ? "Fresh" : "Shelf";
                data.productPrefab = prefab;
                data.cratePrefab = crate;
                data.shelfScale = Vector3.one;
                if (isNew)
                {
                    data.unitPrice = PriceOf(prefab.name);
                    data.itemsPerBox = ItemsPerBox(prefab);
                    data.shelfCapacity = data.itemsPerBox * 2;
                }
                EditorUtility.SetDirty(data);
                items.Add(data);

                AssetPreview.GetAssetPreview(prefab); // xếp hàng chụp icon
                pendingIcons.Add(new PendingIcon { prefab = prefab, data = data, path = $"{IconFolder}/{SafeFileName(pretty)}.png" });
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.SaveAssets();
        AssignToCatalog(items);

        // Icon được Unity tạo bất đồng bộ -> chờ xong rồi lưu
        iconStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= SaveIconsWhenReady;
        EditorApplication.update += SaveIconsWhenReady;

        Debug.Log($"[Import Products] {items.Count} mặt hàng ({items.Count(d => d.displayType == "Fresh")} hàng tươi/lạnh). " +
                  $"Thùng dùng chung: {(crate ? CratePath : "KHÔNG TẠO ĐƯỢC")}. Đang chụp icon...");
    }

    // ================= Icon =================
    private static void SaveIconsWhenReady()
    {
        bool timedOut = EditorApplication.timeSinceStartup - iconStartTime > 60.0;
        if (AssetPreview.IsLoadingAssetPreviews() && !timedOut) return;
        EditorApplication.update -= SaveIconsWhenReady;

        int saved = 0;
        foreach (PendingIcon p in pendingIcons)
        {
            if (p.data == null) continue;
            if (p.data.icon != null && File.Exists(p.path)) continue; // đã có icon
            Texture2D preview = AssetPreview.GetAssetPreview(p.prefab);
            if (preview == null) continue;

            var rt = RenderTexture.GetTemporary(preview.width, preview.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(preview, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(p.path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(p.path);
            if (AssetImporter.GetAtPath(p.path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            p.data.icon = AssetDatabase.LoadAssetAtPath<Sprite>(p.path);
            EditorUtility.SetDirty(p.data);
            saved++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Import Products] Đã lưu {saved} icon. Bấm Ctrl+S để lưu scene rồi mở máy tính trong game để xem danh mục.");
        EditorUtility.DisplayDialog("Import Products", $"Xong! {pendingIcons.Count} mặt hàng, {saved} icon mới.\n\nBấm Ctrl+S để lưu scene.", "OK");
    }

    // ================= Thùng hàng dùng chung =================
    private static GameObject EnsureGenericCrate()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CratePath);
        if (existing != null) return existing;

        GameObject template = CrateTemplates.Select(AssetDatabase.LoadAssetAtPath<GameObject>).FirstOrDefault(g => g != null);
        if (template == null) { Debug.LogError("[Import Products] Không tìm thấy prefab thùng mẫu."); return null; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Crate_Generic";

        Interactable box = inst.GetComponent<Interactable>();
        if (box != null)
        {
            if (box.visualItems != null)
                foreach (GameObject v in box.visualItems) if (v != null) Object.DestroyImmediate(v);
            if (box.productPrefab != null && box.productPrefab.transform.IsChildOf(inst.transform)) Object.DestroyImmediate(box.productPrefab);
            box.visualItems = new GameObject[0];
            box.productPrefab = null;
            box.itemData = null;
            box.productCount = 12;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, CratePath);
        Object.DestroyImmediate(inst);
        return saved;
    }

    // ================= Danh mục trên máy tính =================
    private static void AssignToCatalog(List<ItemData> items)
    {
        ShopFilterManager filter = Object.FindFirstObjectByType<ShopFilterManager>(FindObjectsInactive.Include);
        if (filter == null) { Debug.LogWarning("[Import Products] Không tìm thấy giao diện máy tính (ShopFilterManager) để gắn danh mục."); return; }

        // ComputerUI = ShopFilterManager ngoài cùng (Header_Panel cũng có ShopFilterManager)
        foreach (ShopFilterManager f in Object.FindObjectsByType<ShopFilterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (f.transform.parent == null || f.transform.parent.GetComponentInParent<ShopFilterManager>(true) == null) { filter = f; break; }

        ShopCatalog catalog = filter.GetComponent<ShopCatalog>();
        if (catalog == null) catalog = Undo.AddComponent<ShopCatalog>(filter.gameObject);
        var so = new SerializedObject(catalog);
        SerializedProperty list = so.FindProperty("items");
        list.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(filter.gameObject.scene);
    }

    // ================= Helpers =================
    private static float PriceOf(string prefabName)
    {
        foreach (var (key, price) in Prices)
            if (prefabName.StartsWith(key)) return price;
        return 2f;
    }

    private static int ItemsPerBox(GameObject prefab)
    {
        Bounds b = default;
        bool any = false;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
            }
        }
        float volume = any ? Mathf.Max(0.0001f, b.size.x * b.size.y * b.size.z) : 0.002f;
        int count = Mathf.Clamp(Mathf.RoundToInt(0.024f / volume), 4, 24);
        return count + (count % 2); // số chẵn cho đẹp
    }

    private static string PrettyName(string prefabName)
    {
        // "ChocolateSpread_xhkyij" -> "Chocolate Spread", "TinCan_60afsy_02" -> "Tin Can 2"
        string[] parts = prefabName.Split('_');
        string baseName = Regex.Replace(parts[0], "(?<=[a-z])(?=[A-Z])", " ");
        string last = parts[parts.Length - 1];
        if (parts.Length >= 3 && int.TryParse(last, out int number)) baseName += " " + number;
        return baseName.Replace("Potatoe", "Potato").Trim();
    }

    private static string SafeFileName(string s) => Regex.Replace(s, @"[^A-Za-z0-9 _-]", "").Replace(' ', '_');

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
