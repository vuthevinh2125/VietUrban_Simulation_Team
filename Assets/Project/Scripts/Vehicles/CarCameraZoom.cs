using UnityEngine;

/// <summary>
/// Camera khi lái xe. [V] đổi góc nhìn thứ nhất / thứ ba, chuột để nhìn quanh, thả chuột một lúc thì camera tự quay về phía trước.
/// Xe máy: vị trí mắt tự tính từ yên xe (DriverSeat) - ngồi giữa yên, cao ngang đầu người lái, không bị lệch sang trái
/// như vị trí ghế lái ô tô. Góc nhìn thứ ba của xe máy cũng gần và thấp hơn ô tô.
/// </summary>
public class CarCameraZoom : MonoBehaviour
{
    [Header("Camera Positions")]
    public Vector3 fpPos = new Vector3(-0.3f, 0.86f, 0.17f);
    public float tpDistance = 6f;
    public float tpHeight = 2f;

    [Header("Motorcycle (tự tính từ yên xe)")]
    public bool autoMotorcycleSeat = true;
    [Tooltip("Mắt người lái cao hơn mặt yên bao nhiêu")]
    public float riderEyeHeight = 0.72f;
    [Tooltip("Mắt người lái lùi sau tâm yên bao nhiêu")]
    public float riderEyeBack = 0.12f;
    public float bikeTpDistance = 3.4f;
    public float bikeTpHeight = 1.1f;

    [Header("Controls")]
    public float mouseSensitivity = 3f;
    public float cameraSmoothness = 10f;
    [Tooltip("Không rê chuột bao nhiêu giây thì camera tự quay về phía trước")]
    public float recenterDelay = 1.5f;
    public float recenterSpeed = 2.5f;

    private float yaw = 0f;
    private float pitch = 0f;
    private bool isThirdPerson = false;
    private float mouseX, mouseY;
    private float lastLookTime;

    private Vector3 activeFp;
    private float activeTpDistance, activeTpHeight;

    void OnEnable()
    {
        yaw = 0f;
        pitch = 0f;
        lastLookTime = -100f;
        ResolveSeat();
        transform.localPosition = isThirdPerson ? TpPosition(Quaternion.identity) : activeFp;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Xe máy: đặt mắt ngay trên yên (giữa xe) thay cho vị trí ghế lái ô tô.</summary>
    private void ResolveSeat()
    {
        activeFp = fpPos;
        activeTpDistance = tpDistance;
        activeTpHeight = tpHeight;
        if (!autoMotorcycleSeat || transform.parent == null) return;
        MotorcycleInteraction bike = GetComponentInParent<MotorcycleInteraction>();
        if (bike == null) return;

        Transform root = bike.transform;
        Vector3 seat = bike.driverSeat != null ? bike.driverSeat.position : root.position + root.up * 0.6f;
        Vector3 eye = seat + root.up * riderEyeHeight - root.forward * riderEyeBack;
        // Đặt đúng giữa xe theo chiều ngang
        Vector3 local = root.InverseTransformPoint(eye);
        local.x = 0f;
        eye = root.TransformPoint(local);
        activeFp = transform.parent.InverseTransformPoint(eye);
        activeTpDistance = bikeTpDistance;
        activeTpHeight = bikeTpHeight;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V)) isThirdPerson = !isThirdPerson;
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
    }

    void LateUpdate()
    {
        if (Mathf.Abs(mouseX) > 0.05f || Mathf.Abs(mouseY) > 0.05f)
        {
            yaw += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -60f, 70f);
            if (yaw > 180f) yaw -= 360f;
            if (yaw < -180f) yaw += 360f;
            lastLookTime = Time.time;
        }
        else if (Time.time - lastLookTime > recenterDelay)
        {
            // Tự quay về nhìn thẳng phía trước (hơi cúi xuống đường)
            float k = 1f - Mathf.Exp(-recenterSpeed * Time.deltaTime);
            yaw = Mathf.Lerp(yaw, 0f, k);
            pitch = Mathf.Lerp(pitch, isThirdPerson ? 8f : 4f, k);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.localRotation = rot;
        Vector3 targetPos = isThirdPerson ? TpPosition(rot) : activeFp;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * cameraSmoothness);
    }

    private Vector3 TpPosition(Quaternion rot) => activeFp + rot * new Vector3(0f, activeTpHeight, -activeTpDistance);
}
