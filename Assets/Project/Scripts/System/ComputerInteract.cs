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

    [Tooltip("Giao diện chiếm bao nhiêu phần màn hình (0.9 = 90%)")]
    [Range(0.3f, 1f)] public float screenFill = 0.9f;

    [Tooltip("Độ tối của nền phía sau giao diện (0 = không có nền)")]
    [Range(0f, 1f)] public float backdropOpacity = 0.6f;

    private GameObject backdrop;

    public bool IsOpen => computerUI != null && computerUI.activeSelf;

    void Awake()
    {
        if (computerUI == null) computerUI = FindComputerUI();
        if (debugLogs && GameDebug.Verbose && computerUI != null) Debug.Log($"[Computer] '{name}' dùng giao diện '{computerUI.name}'.", this);
        if (computerUI == null)
            Debug.LogError("[Computer] Không tìm thấy ComputerUI trong scene. Kéo ComputerUI vào ô 'Computer UI'.", this);

        if (showAsScreenOverlay) ConvertToScreenOverlay();
    }

    /// <summary>Bật các object cha (Canvas...) nếu lỡ bị tắt trong Hierarchy, nếu không giao diện sẽ không hiện.</summary>
    private void EnsureParentsActive()
    {
        for (Transform p = computerUI.transform.parent; p != null; p = p.parent)
        {
            if (!p.gameObject.activeSelf)
            {
                Debug.LogWarning($"[Computer] '{p.name}' đang bị tắt nên giao diện không hiện được -> tự bật lại. " +
                                 "Nên bật nó trong Hierarchy rồi lưu scene.", p);
                p.gameObject.SetActive(true);
            }
        }

        Canvas canvas = computerUI.GetComponentInParent<Canvas>(true);
        if (canvas != null && !canvas.enabled) canvas.enabled = true;
    }

    /// <summary>Tự tìm panel giao diện máy tính khi ô Computer UI bị trống.</summary>
    public static GameObject FindComputerUI()
    {
        // 1) Đúng tên "ComputerUI"
        foreach (RectTransform t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "ComputerUI" && t.gameObject.scene.IsValid()) return t.gameObject;

        // 2) Panel có UIMouseControl (panel giao diện chính)
        UIMouseControl mouse = FindFirstObjectByType<UIMouseControl>(FindObjectsInactive.Include);
        if (mouse != null) return mouse.gameObject;

        // 3) ShopFilterManager NẰM NGOÀI CÙNG (bỏ qua các panel con như Header_Panel)
        foreach (ShopFilterManager shop in FindObjectsByType<ShopFilterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform p = shop.transform.parent;
            if (p == null || p.GetComponentInParent<ShopFilterManager>(true) == null) return shop.gameObject;
        }
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
        // Nền tối luôn bật/tắt theo giao diện (kể cả khi đóng bằng nút X)
        if (backdrop != null && computerUI != null && backdrop.activeSelf != computerUI.activeSelf)
            backdrop.SetActive(computerUI.activeSelf);

        // UI bị tắt từ chỗ khác (ví dụ nút X) thì nhả khoá
        if (UIState.IsOpenedBy(this) && !IsOpen)
        {
            if (debugLogs && GameDebug.Verbose) Debug.LogWarning("[Computer] UI đã bị tắt từ chỗ khác -> nhả khoá.", this);
            UIState.Close(this);
            return;
        }

        // Esc luôn đóng được máy tính, bất kể ai đã mở nó
        // Esc đang dùng để đóng bảng giấy phép thì không đóng luôn máy tính
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape) && !LicensePanel.IsOpen && LicensePanel.ClosedFrame != Time.frameCount
            && !FurniturePanel.IsOpen && FurniturePanel.ClosedFrame != Time.frameCount
            && !StaffPanel.IsOpen && StaffPanel.ClosedFrame != Time.frameCount
            && !ExpansionPanel.IsOpen && ExpansionPanel.ClosedFrame != Time.frameCount
            && !StatsPanel.IsOpen && StatsPanel.ClosedFrame != Time.frameCount
            && !VehiclePanel.IsOpen && VehiclePanel.ClosedFrame != Time.frameCount) CloseComputer();
    }

    [Header("Debug")]
    [Tooltip("Ghi log chi tiết khi mở/đóng máy tính (tắt đi khi đã chạy ổn)")]
    public bool debugLogs = true; // chỉ in khi GameDebug.Verbose = true

    public void OpenComputer()
    {
        if (computerUI == null) { Debug.LogError("[Computer] Chưa gán ComputerUI!", this); return; }
        EnsureParentsActive();
        if (backdrop != null) backdrop.SetActive(true);
        computerUI.SetActive(true);
        UIState.Open(this);
        if (debugLogs && GameDebug.Verbose) LogState("OPEN");
    }

    /// <summary>Gắn được vào nút X trên giao diện.</summary>
    public void CloseComputer()
    {
        if (computerUI != null) computerUI.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);
        UIState.Close(this);
        if (debugLogs && GameDebug.Verbose) LogState("CLOSE");
    }

    void OnDisable() => UIState.Close(this);

    void LateUpdate()
    {
        // Phát hiện giao diện bị tắt bởi script khác ngay sau khi mở
        if (debugLogs && GameDebug.Verbose && wasOpenLastFrame && !IsOpen)
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
        if (debugLogs && GameDebug.Verbose) Debug.Log($"[Computer] Awake on '{name}': canvas='{(canvas != null ? canvas.name : "none")}' " +
                                 $"mode={(canvas != null ? canvas.renderMode.ToString() : "-")}", this);
        if (canvas == null) return;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Một máy tính khác đã chuyển canvas rồi -> dùng chung lớp nền
            Transform existing = canvas.transform.Find("ComputerBackdrop");
            if (existing != null) backdrop = existing.gameObject;
            return;
        }

        // Kích thước thiết kế gốc của giao diện (đo khi canvas còn là World Space)
        RectTransform uiRect = computerUI.GetComponent<RectTransform>();
        Vector2 designSize = uiRect != null ? uiRect.rect.size : Vector2.zero;
        if (designSize.x < 100f || designSize.y < 100f) designSize = new Vector2(1280, 720);

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortingOrder;

        // Giữ đúng tỉ lệ thiết kế: cả giao diện luôn nằm gọn trong màn hình, chiếm tối đa screenFill
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = designSize / Mathf.Clamp(screenFill, 0.3f, 1f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // Panel cố định kích thước thiết kế, đặt giữa màn hình (không kéo giãn theo màn hình nữa)
        if (uiRect != null)
        {
            uiRect.anchorMin = uiRect.anchorMax = uiRect.pivot = new Vector2(0.5f, 0.5f);
            uiRect.sizeDelta = designSize;
            uiRect.anchoredPosition3D = Vector3.zero;
            uiRect.localRotation = Quaternion.identity;
            uiRect.localScale = Vector3.one;
        }

        CreateBackdrop(canvas.transform);
    }

    /// <summary>Lớp nền tối phủ toàn màn hình phía sau giao diện, chặn click xuyên ra thế giới.</summary>
    private void CreateBackdrop(Transform canvasTransform)
    {
        if (backdropOpacity <= 0f) return;

        backdrop = new GameObject("ComputerBackdrop", typeof(RectTransform), typeof(Image));
        backdrop.layer = canvasTransform.gameObject.layer;
        RectTransform rt = backdrop.GetComponent<RectTransform>();
        rt.SetParent(canvasTransform, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, backdropOpacity);
        backdrop.SetActive(computerUI.activeSelf);
    }
}
