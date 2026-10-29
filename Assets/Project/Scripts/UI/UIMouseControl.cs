using UnityEngine;

/// <summary>
/// Gắn vào bất kỳ panel UI nào cần dùng chuột. Khi panel bật: hiện chuột + khoá nhân vật.
/// Không còn đóng băng thời gian (Time.timeScale) nữa, trừ khi bật pauseGame.
/// </summary>
public class UIMouseControl : MonoBehaviour
{
    [Tooltip("Bật nếu muốn dừng cả game khi panel này mở (ví dụ menu Pause)")]
    public bool pauseGame = false;

    void OnEnable()
    {
        if (debugLogs && GameDebug.Verbose) Debug.Log($"[Computer] Panel '{name}' được BẬT (frame {Time.frameCount}).", this);
        UIState.Open(this);
        if (pauseGame) Time.timeScale = 0f;
    }

    void Update()
    {
        // Script khác (CameraFollow...) có thể khoá chuột lại, ép hiện chuột khi panel đang mở
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }

    [Tooltip("Ghi log ai đã bật/tắt panel này (dùng để tìm lỗi)")]
    public bool debugLogs = true; // chỉ in khi GameDebug.Verbose = true

    void OnDisable()
    {
        if (debugLogs && GameDebug.Verbose)
            Debug.Log($"[Computer] Panel '{name}' bị TẮT (frame {Time.frameCount}). Ai gọi:\n{System.Environment.StackTrace}", this);
        UIState.Close(this);
        if (pauseGame) Time.timeScale = 1f;
    }
}
