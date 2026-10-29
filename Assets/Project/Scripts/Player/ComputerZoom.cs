using UnityEngine;
using System.Collections;

/// <summary>
/// (Tuỳ chọn) Hiệu ứng đưa camera zoom vào màn hình máy tính.
/// - Không còn tự bật khi click chuột (trước đây bấm nút trên UI cũng kích hoạt nhầm).
/// - Nếu object bị tắt giữa chừng, camera luôn được trả về nhân vật ngay lập tức.
/// </summary>
public class ComputerZoom : MonoBehaviour
{
    public Camera playerCam;
    public Transform zoomPoint;
    public MonoBehaviour cameraFollowScript;

    [Header("Kéo 3D_Computer_Canvas vào đây")]
    public Canvas uiCanvas;

    [Tooltip("Bật nếu muốn click chuột trực tiếp vào object này để zoom (mặc định TẮT vì dễ bấm nhầm khi dùng UI)")]
    public bool openOnMouseClick = false;

    private Vector3 normalLocalPos;
    private Quaternion normalLocalRot;
    private Transform originalParent;
    private bool isZoomed = false;

    public bool IsZoomed => isZoomed;

    void Update()
    {
        if (!isZoomed) return;

        if (uiCanvas != null && playerCam != null)
            uiCanvas.worldCamera = playerCam;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleComputer();
    }

    void OnMouseDown()
    {
        if (openOnMouseClick && !isZoomed && !UIState.IsBlockingUIOpen)
            ToggleComputer();
    }

    public void ToggleComputer()
    {
        if (playerCam == null || zoomPoint == null)
        {
            Debug.LogWarning("[ComputerZoom] Chưa gán Player Cam hoặc Zoom Point.", this);
            return;
        }

        isZoomed = !isZoomed;
        if (isZoomed) UIState.Open(this); else UIState.Close(this);

        if (cameraFollowScript != null)
            cameraFollowScript.enabled = !isZoomed;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = !isZoomed;

        StopAllCoroutines();

        if (isZoomed)
        {
            originalParent = playerCam.transform.parent;
            normalLocalPos = playerCam.transform.localPosition;
            normalLocalRot = playerCam.transform.localRotation;

            playerCam.transform.SetParent(null);
            StartCoroutine(TransitionToComputer(zoomPoint.position, zoomPoint.rotation));
        }
        else
        {
            StartCoroutine(TransitionBackToPlayer());
        }
    }

    /// <summary>Bị tắt giữa chừng (ví dụ nút X tắt cả ComputerUI): trả camera về ngay.</summary>
    void OnDisable()
    {
        if (!isZoomed) return;

        isZoomed = false;
        StopAllCoroutines();
        RestoreCameraInstantly();

        if (cameraFollowScript != null) cameraFollowScript.enabled = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        UIState.Close(this);
    }

    private void RestoreCameraInstantly()
    {
        if (playerCam == null) return;
        playerCam.transform.SetParent(originalParent);
        playerCam.transform.localPosition = normalLocalPos;
        playerCam.transform.localRotation = normalLocalRot;
    }

    IEnumerator TransitionToComputer(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = playerCam.transform.position;
        Quaternion startRot = playerCam.transform.rotation;

        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime * 4f;
            playerCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            playerCam.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }

        playerCam.transform.position = targetPos;
        playerCam.transform.rotation = targetRot;
    }

    IEnumerator TransitionBackToPlayer()
    {
        playerCam.transform.SetParent(originalParent);

        Vector3 startPos = playerCam.transform.localPosition;
        Quaternion startRot = playerCam.transform.localRotation;

        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime * 4f;
            playerCam.transform.localPosition = Vector3.Lerp(startPos, normalLocalPos, t);
            playerCam.transform.localRotation = Quaternion.Lerp(startRot, normalLocalRot, t);
            yield return null;
        }

        RestoreCameraInstantly();
    }
}
