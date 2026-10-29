using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Máy tính trong cửa hàng: nhìn vào + [E]/click để mở giao diện quản lý (Đặt hàng / Kho / Giá / Tài chính).
/// Giao diện hiện lên PHỦ KÍN MÀN HÌNH (Screen Space Overlay), không phụ thuộc vị trí canvas trong scene.
/// Khi mở, nhân vật bị khoá (UIState) nhưng thời gian trong game vẫn chạy như game gốc.
/// </summary>
public class ComputerInteract : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject computerUI; // Kéo ComputerUI vào đây

    [Tooltip("Luôn tắt giao diện khi bắt đầu game (kể cả khi lỡ lưu scene lúc ComputerUI đang bật)")]
    public bool hideOnStart = true;

    [Tooltip("Hiện giao diện phủ kín màn hình thay vì nằm trên màn hình 3D")]
    public bool showAsScreenOverlay = true;

    [Tooltip("Giao diện vẽ đè lên các UI khác (HUD...) với thứ tự này")]
    public int overlaySortingOrder = 100;

    public bool IsOpen => computerUI != null && computerUI.activeSelf;

    void Awake()
    {
        if (computerUI == null) computerUI = FindComputerUI();
        if (computerUI == null)
            Debug.LogError("[Computer] Không tìm thấy ComputerUI trong scene. Kéo ComputerUI vào ô 'Computer UI'.", this);

        if (showAsScreenOverlay) ConvertToScreenOverlay();
    }

    /// <summary>Tự tìm panel giao diện máy tính khi ô Computer UI bị trống.</summary>
    public static GameObject FindComputerUI()
    {
        // Ưu tiên panel có bộ lọc cửa hàng (chính là giao diện mua hàng)
        ShopFilterManager shop = FindFirstObjectByType<ShopFilterManager>(FindObjectsInactive.Include);
        if (shop != null) return shop.gameObject;

        foreach (RectTransform t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "ComputerUI") return t.gameObject;
        return null;
    }

    void Start()
    {
        if (hideOnStart && computerUI != null && computerUI.activeSelf)
        {
            computerUI.SetActive(false);
            UIState.Close(this);
        }
    }

    void Update()
    {
        // UI bị tắt từ chỗ khác (ví dụ nút X) thì nhả khoá
        if (UIState.IsOpenedBy(this) && !IsOpen)
        {
            if (debugLogs) Debug.LogWarning("[Computer] UI đã bị tắt từ chỗ khác -> nhả khoá.", this);
            UIState.Close(this);
            return;
        }

        // Esc luôn đóng được máy tính, bất kể ai đã mở nó
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseComputer();
    }

    [Header("Debug")]
    [Tooltip("Ghi log chi tiết khi mở/đóng máy tính (tắt đi khi đã chạy ổn)")]
    public bool debugLogs = true;

    public void OpenComputer()
    {
        if (computerUI == null) { Debug.LogError("[Computer] Chưa gán ComputerUI!", this); return; }
        computerUI.SetActive(true);
        UIState.Open(this);
        if (debugLogs) LogState("OPEN");
    }

    /// <summary>Gắn được vào nút X trên giao diện.</summary>
    public void CloseComputer()
    {
        if (computerUI != null) computerUI.SetActive(false);
        UIState.Close(this);
        if (debugLogs) LogState("CLOSE");
    }

    void OnDisable() => UIState.Close(this);

    void LateUpdate()
    {
        // Phát hiện giao diện bị tắt bởi script khác ngay sau khi mở
        if (debugLogs && wasOpenLastFrame && !IsOpen)
            Debug.LogWarning($"[Computer] ComputerUI vừa bị TẮT bởi script khác (frame {Time.frameCount}).", this);
        wasOpenLastFrame = IsOpen;
    }

    private bool wasOpenLastFrame;

    private void LogState(string action)
    {
        Canvas canvas = computerUI != null ? computerUI.GetComponentInParent<Canvas>(true) : null;
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        RectTransform rt = computerUI != null ? computerUI.GetComponent<RectTransform>() : null;
        Debug.Log($"[Computer] {action} frame={Time.frameCount} on '{name}' | uiActiveSelf={computerUI?.activeSelf} " +
                  $"uiActiveInHierarchy={computerUI?.activeInHierarchy} | rootCanvas='{(root != null ? root.name : "none")}' " +
                  $"mode={(root != null ? root.renderMode.ToString() : "-")} enabled={(root != null && root.enabled)} " +
                  $"sort={(root != null ? root.sortingOrder : 0)} | uiRect={(rt != null ? rt.rect.ToString() : "-")} " +
                  $"uiScale={(rt != null ? rt.lossyScale.ToString("F4") : "-")} | UIState.open={UIState.IsBlockingUIOpen}\n{System.Environment.StackTrace}", this);
    }

    /// <summary>
    /// Đổi canvas chứa ComputerUI từ World Space sang Screen Space Overlay,
    /// giữ nguyên bố cục bằng cách scale theo kích thước thiết kế ban đầu.
    /// </summary>
    private void ConvertToScreenOverlay()
    {
        if (computerUI == null) return;

        Canvas canvas = null;
        for (Transform t = computerUI.transform; t != null; t = t.parent)
        {
            Canvas c = t.GetComponent<Canvas>();
            if (c != null) canvas = c; // lấy canvas gốc trên cùng
        }
        if (debugLogs) Debug.Log($"[Computer] Awake on '{name}': canvas='{(canvas != null ? canvas.name : "none")}' " +
                                 $"mode={(canvas != null ? canvas.renderMode.ToString() : "-")}", this);
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 designSize = canvasRect != null ? canvasRect.sizeDelta : new Vector2(1920, 1080);
        if (designSize.x < 100f || designSize.y < 100f) designSize = new Vector2(1920, 1080);

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortingOrder;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = designSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // Bỏ độ nghiêng/lệch sâu dùng cho màn hình 3D
        RectTransform uiRect = computerUI.GetComponent<RectTransform>();
        if (uiRect != null)
        {
            uiRect.localRotation = Quaternion.identity;
            Vector3 p = uiRect.localPosition;
            uiRect.localPosition = new Vector3(p.x, p.y, 0f);
        }
    }
}
