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

    [Header("Car (tự tính từ thân xe)")]
    [Tooltip("Ô tô: tự đặt mắt người lái và camera thứ ba theo kích thước thân xe (xe thay vỏ có hình dạng khác xe gốc)")]
    public bool autoCarFit = true;
    [Tooltip("Ô tô vào xe là nhìn từ phía sau (góc thứ ba); [V] để đổi")]
    public bool carStartsThirdPerson = true;

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
    private bool viewChosen;
    private bool thirdPersonOnly;  // xe không có kính trong suốt: nhìn từ ghế lái chỉ thấy vỏ xe -> khoá góc thứ ba
    private Transform vehicle;   // gốc xe (trục thật: Y lên, Z hướng chạy); camera có thể nằm dưới một khung con bị xoay

    void OnEnable()
    {
        yaw = 0f;
        pitch = 0f;
        lastLookTime = -100f;
        ResolveSeat();
        vehicle = VehicleRoot();
        Place(Quaternion.identity, 1f);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Xe máy: đặt mắt ngay trên yên (giữa xe) thay cho vị trí ghế lái ô tô.</summary>
    private void ResolveSeat()
    {
        activeFp = fpPos;
        activeTpDistance = tpDistance;
        activeTpHeight = tpHeight;
        if (transform.parent == null) return;
        MotorcycleInteraction bike = GetComponentInParent<MotorcycleInteraction>();
        if (bike == null) { ResolveCar(); return; }
        if (!autoMotorcycleSeat) return;

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

    /// <summary>
    /// Ô tô "thay vỏ" (có con "Skin_..." do VehicleManager gắn): đo thân xe mới -> mắt người lái ở 78% chiều cao,
    /// lệch trái 22% bề ngang, ngang tâm xe theo chiều dài (+2%, hàng ghế trước); camera thứ ba lùi 1.35 x chiều dài,
    /// cao 1.3 x chiều cao. Xe gốc (Tocus) giữ vị trí ghế lái đã chỉnh tay (fpPos).
    /// Model không có kính trong suốt (vd. xe thể thao Car4: cả thân xe một vật liệu đục) thì chỉ dùng góc thứ ba.
    /// </summary>
    private void ResolveCar()
    {
        CarInteraction car = GetComponentInParent<CarInteraction>();
        if (car == null) return;
        if (!viewChosen) { isThirdPerson = carStartsThirdPerson; viewChosen = true; }
        Transform root = car.transform;
        Transform skin = null;
        foreach (Transform ch in root) if (ch.name.StartsWith("Skin_")) { skin = ch; break; }
        thirdPersonOnly = false;
        if (skin == null || !autoCarFit) return;
        if (!ModelFit.MeshBounds(skin, root, out Bounds b)) return;
        VehicleManager.Entry entry = VehicleManager.Get(root.name);
        bool customEye = entry != null && entry.fpEye != Vector3.zero;
        Vector3 eye = customEye ? b.min + Vector3.Scale(b.size, entry.fpEye)
                                : new Vector3(b.center.x - b.size.x * 0.22f, b.min.y + b.size.y * 0.78f, b.center.z + b.size.z * 0.02f);
        activeFp = transform.parent.InverseTransformPoint(root.TransformPoint(eye));
        activeTpDistance = Mathf.Max(4.5f, b.size.z * 1.35f);
        activeTpHeight = Mathf.Max(1.6f, b.size.y * 1.3f);

        bool glass = false;
        foreach (Renderer r in skin.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.renderQueue >= 2500) { glass = true; break; }
        thirdPersonOnly = !glass && !customEye; // có vị trí mắt riêng (camera nắp capo) thì vẫn cho góc thứ nhất
        if (thirdPersonOnly) isThirdPerson = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (thirdPersonOnly) ProgressHUD.Toast("This car only has the chase camera.");
            else isThirdPerson = !isThirdPerson;
        }
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

        Place(Quaternion.Euler(pitch, yaw, 0f), Time.deltaTime * cameraSmoothness);
    }

    /// <summary>
    /// Đặt camera theo trục của XE (không theo khung cha trực tiếp): khung cha của camera ở một số xe nhập về bị xoay
    /// (vd. -90° quanh X), khi đó "phía sau, phía trên" tính theo khung cha sẽ thành "phía dưới" -> camera chui gầm xe.
    /// </summary>
    private void Place(Quaternion look, float t)
    {
        Transform parent = transform.parent;
        if (parent == null) return;
        if (vehicle == null) vehicle = VehicleRoot();
        Quaternion world = vehicle.rotation * look;
        Vector3 eye = parent.TransformPoint(activeFp);
        Vector3 pos = isThirdPerson ? eye + world * new Vector3(0f, activeTpHeight, -activeTpDistance) : eye;
        transform.localRotation = Quaternion.Inverse(parent.rotation) * world;
        transform.localPosition = Vector3.Lerp(transform.localPosition, parent.InverseTransformPoint(pos), Mathf.Clamp01(t));
    }

    private Transform VehicleRoot()
    {
        MotorcycleInteraction bike = GetComponentInParent<MotorcycleInteraction>();
        if (bike != null) return bike.transform;
        CarInteraction car = GetComponentInParent<CarInteraction>();
        return car != null ? car.transform : transform.parent;
    }
}
