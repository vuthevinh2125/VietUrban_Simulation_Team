using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Mục tiêu theo dõi")]
    public Transform target;
    public Transform headPoint;

    [Header("Cài đặt Zoom & Xoay")]
    public float zoomSpeed = 2f;
    public float minZoom = 0f;
    public float maxZoom = 1f;
    public float mouseSensitivity = 3f; 

    [Header("Góc nhìn thứ 3 (TPS)")]
    public Vector3 tpsOffset = new Vector3(0f, 0f, -4f);

    private float currentZoom = 1f;
    private float yaw = 0f;   
    private float pitch = 0f; 

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        if (target == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentZoom -= scroll * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -35f, 60f); 

        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);

        if (currentZoom <= 0.05f)
        {
            transform.position = headPoint != null ? headPoint.position : target.position + Vector3.up * 1.5f;
            transform.rotation = camRotation;

            target.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else
        {
            Vector3 finalOffset = camRotation * (tpsOffset * currentZoom);
            transform.position = target.position + Vector3.up * 1.5f + finalOffset;
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}