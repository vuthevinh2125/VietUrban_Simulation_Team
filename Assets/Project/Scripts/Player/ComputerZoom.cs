using UnityEngine;
using System.Collections;

public class ComputerZoom : MonoBehaviour
{
    public Camera playerCam;
    public Transform zoomPoint;
    public MonoBehaviour cameraFollowScript;

    [Header("Kéo 3D_Computer_Canvas vào đây")]
    public Canvas uiCanvas;

    private Vector3 normalLocalPos;
    private Quaternion normalLocalRot;
    private Transform originalParent;
    private bool isZoomed = false;

    void Update()
    {
        if (isZoomed)
        {
            if (uiCanvas != null && playerCam != null)
            {
                uiCanvas.worldCamera = playerCam;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleComputer();
            }
        }
    }

    void OnMouseDown()
    {
        if (!isZoomed)
        {
            ToggleComputer();
        }
    }

    public void ToggleComputer()
    {
        isZoomed = !isZoomed;
        if (isZoomed) UIState.Open(this); else UIState.Close(this);

        if (cameraFollowScript != null)
            cameraFollowScript.enabled = !isZoomed;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = !isZoomed;

        if (isZoomed)
        {
            originalParent = playerCam.transform.parent;
            normalLocalPos = playerCam.transform.localPosition;
            normalLocalRot = playerCam.transform.localRotation;

            playerCam.transform.SetParent(null);

            StopAllCoroutines();
            StartCoroutine(TransitionToComputer(zoomPoint.position, zoomPoint.rotation));
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(TransitionBackToPlayer());

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    IEnumerator TransitionToComputer(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = playerCam.transform.position;
        Quaternion startRot = playerCam.transform.rotation;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 4f;
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
            t += Time.deltaTime * 4f;
            playerCam.transform.localPosition = Vector3.Lerp(startPos, normalLocalPos, t);
            playerCam.transform.localRotation = Quaternion.Lerp(startRot, normalLocalRot, t);
            yield return null;
        }

        playerCam.transform.localPosition = normalLocalPos;
        playerCam.transform.localRotation = normalLocalRot;
    }
}