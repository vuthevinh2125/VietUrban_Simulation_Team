using UnityEngine;

/// <summary>
/// Máy tính trong cửa hàng: click vào để mở giao diện (Đặt hàng / Kho / Giá / Tài chính).
/// Khi mở, nhân vật bị khoá (UIState) nhưng thời gian trong game vẫn chạy như game gốc.
/// </summary>
public class ComputerInteract : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject computerUI; // Kéo ComputerUI vào đây

    public bool IsOpen => computerUI != null && computerUI.activeSelf;

    void Update()
    {
        if (!UIState.IsOpenedBy(this)) return;

        // UI bị tắt từ chỗ khác (ví dụ nút X) thì nhả khoá
        if (!IsOpen)
        {
            UIState.Close(this);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) CloseComputer();
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
