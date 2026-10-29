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
        UIState.Open(this);
        if (pauseGame) Time.timeScale = 0f;
    }

    void Update()
    {
        // Script khác (CameraFollow...) có thể khoá chuột lại, ép hiện chuột khi panel đang mở
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }

    void OnDisable()
    {
        UIState.Close(this);
        if (pauseGame) Time.timeScale = 1f;
    }
}
