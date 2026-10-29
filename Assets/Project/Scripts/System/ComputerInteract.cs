using UnityEngine;

/// <summary>
/// Máy tính trong cửa hàng: nhìn vào + [E]/click để mở giao diện (Đặt hàng / Kho / Giá / Tài chính).
/// Khi mở, nhân vật bị khoá (UIState) nhưng thời gian trong game vẫn chạy như game gốc.
/// </summary>
public class ComputerInteract : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject computerUI; // Kéo ComputerUI vào đây

    [Tooltip("Luôn tắt giao diện khi bắt đầu game (kể cả khi lỡ lưu scene lúc ComputerUI đang bật)")]
    public bool hideOnStart = true;

    public bool IsOpen => computerUI != null && computerUI.activeSelf;

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
            UIState.Close(this);
            return;
        }

        // Esc luôn đóng được máy tính, bất kể ai đã mở nó
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseComputer();
    }

    public void OpenComputer()
    {
        if (computerUI == null) return;
        computerUI.SetActive(true);
        UIState.Open(this);
    }

    /// <summary>Gắn được vào nút X trên giao diện.</summary>
    public void CloseComputer()
    {
        if (computerUI != null) computerUI.SetActive(false);
        UIState.Close(this);
    }

    void OnDisable() => UIState.Close(this);
}
