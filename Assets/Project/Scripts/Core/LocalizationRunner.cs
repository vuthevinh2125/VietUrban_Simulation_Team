using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chạy nền cho <see cref="Loc"/>: gắn bộ dịch vào mọi ô chữ TextMeshPro của game siêu thị (kể cả chữ có sẵn trong scene,
/// prefab), vẽ lại toàn bộ chữ khi đổi ngôn ngữ, và trong Editor / bản Development ghi các câu chưa có trong bảng ra
/// loc_missing.txt (thư mục lưu game) để bổ sung bản dịch.
/// Không đụng tới chữ trong ô nhập liệu (TMP_InputField) và chữ trong chế độ giao đồ ăn (scene không có GameManager).
/// </summary>
public class LocalizationRunner : MonoBehaviour
{
    private static LocalizationRunner instance;
    private readonly List<TMP_Text> pending = new List<TMP_Text>();
    private bool supermarket;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        Loc.Load();
        var go = new GameObject("Localization");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LocalizationRunner>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        Loc.OnChanged += RefreshAll;
        SceneManager.sceneLoaded += OnSceneLoaded;
        supermarket = SupermarketScene.IsActive;
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        Loc.OnChanged -= RefreshAll;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        FlushMissing();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        FlushMissing();
        supermarket = SupermarketScene.IsActive;
        if (supermarket) AttachAll();
    }

    void Start()
    {
        if (supermarket) AttachAll();
    }

    /// <summary>Chữ được tạo ở nơi khác ShopUI / UIKit (prefab, scene...): gắn bộ dịch lần đầu chữ được vẽ, rồi vẽ lại.</summary>
    private void OnTextChanged(Object o)
    {
        if (!supermarket || !(o is TMP_Text t) || t.textPreprocessor != null) return;
        if (t.GetComponentInParent<TMP_InputField>(true) != null) { t.textPreprocessor = Loc.Raw; return; }
        t.textPreprocessor = Loc.TextPreprocessor;
        pending.Add(t);
    }

    void LateUpdate()
    {
        if (pending.Count > 0)
        {
            foreach (TMP_Text t in pending) if (t != null) Reparse(t);
            pending.Clear();
        }
    }

    private void AttachAll()
    {
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.textPreprocessor != null) continue;
            if (t.GetComponentInParent<TMP_InputField>(true) != null) { t.textPreprocessor = Loc.Raw; continue; }
            t.textPreprocessor = Loc.TextPreprocessor;
            Reparse(t);
        }
    }

    /// <summary>Đổi ngôn ngữ: đánh dấu mọi chữ cần dịch lại (kể cả bảng đang ẩn); TextMeshPro tự vẽ lại ở lần cập nhật giao diện kế tiếp.</summary>
    private void RefreshAll()
    {
        if (!supermarket) return;
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.textPreprocessor == null && t.GetComponentInParent<TMP_InputField>(true) == null) t.textPreprocessor = Loc.TextPreprocessor;
            if (t.textPreprocessor == Loc.TextPreprocessor) Reparse(t);
        }
    }

    /// <summary>Gán lại chữ để TextMeshPro đọc lại (qua bộ dịch) - rẻ hơn ForceMeshUpdate và không dựng lưới cho chữ đang ẩn.</summary>
    private static void Reparse(TMP_Text t)
    {
        string s = t.text;
        if (string.IsNullOrEmpty(s)) return;
        t.text = string.Empty;
        t.text = s;
    }

    private void FlushMissing()
    {
        if (!Loc.MissingDirty) return;
        Loc.MissingDirty = false;
        try { File.WriteAllLines(Path.Combine(Application.persistentDataPath, "loc_missing.txt"), Loc.Missing); }
        catch (System.Exception e) { Debug.LogWarning("[Loc] Could not write loc_missing.txt: " + e.Message); }
    }

    void OnApplicationQuit() { FlushMissing(); Loc.RemoveRuntimeFonts(); }

}
