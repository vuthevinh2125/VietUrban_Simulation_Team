using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;
    public Transform playerCamera;

    /// <summary>true = tạm khoá xoay camera bằng chuột (vd. đang kéo chuột để xoay nội thất). Vẫn đi lại được.</summary>
    public static bool LookLocked;
    /// <summary>Khoá cả nhìn lẫn di chuyển (chế độ đo hiệu năng cố định của FpsLogger).</summary>
    public static bool InputFrozen;

    private float cameraPitch = 0f;
    private Rigidbody rb;
    private Animator animator;

    private float moveX;
    private float moveZ;
    private float stepDistance; // quãng đường từ bước chân trước (m)
    private bool isSprinting;
    private bool jumpRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { LookLocked = false; InputFrozen = false; }

    /// <summary>Đặt vị trí + hướng nhìn (dùng cho chế độ đo hiệu năng cố định).</summary>
    public void SetPose(Vector3 position, float yaw, float pitch)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        if (rb != null) { rb.position = position; rb.rotation = transform.rotation; rb.linearVelocity = Vector3.zero; }
        cameraPitch = Mathf.Clamp(pitch, -85f, 85f);
        if (playerCamera != null) playerCamera.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        // Đang có menu / giao diện mở (menu chính lúc vào game) thì để chuột tự do
        if (!UIState.IsBlockingUIOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (UIState.IsBlockingUIOpen || InputFrozen)
        {
            moveX = 0f;
            moveZ = 0f;
            jumpRequested = false;
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }

        float mouseX = LookLocked ? 0f : Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = LookLocked ? 0f : Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        if (playerCamera != null)
        {
            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);
            playerCamera.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }

        moveX = Input.GetAxis("Horizontal");
        moveZ = Input.GetAxis("Vertical");
        isSprinting = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            jumpRequested = true;
        }

        // Bước chân: mỗi 0.65 m (chạy 0.95 m) một tiếng, chỉ khi đang chạm đất
        Vector3 flat = rb != null ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
        if (flat.sqrMagnitude > 0.25f && IsGrounded())
        {
            stepDistance += flat.magnitude * Time.deltaTime;
            if (stepDistance >= (isSprinting ? 0.95f : 0.65f))
            {
                stepDistance = 0f;
                AudioManager.Play("footstep_" + Random.Range(1, 5), isSprinting ? 0.32f : 0.24f);
            }
        }

        if (animator != null)
        {
            float currentSpeed = new Vector2(moveX, moveZ).magnitude;
            animator.SetFloat("Speed", currentSpeed > 0f ? (isSprinting ? 1.0f : 0.3f) : 0.0f);
        }
    }

    void FixedUpdate()
    {
        if (UIState.IsBlockingUIOpen || InputFrozen)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        Vector3 moveDirection = (transform.forward * moveZ + transform.right * moveX).normalized;
        Vector3 moveVelocity = moveDirection * currentSpeed;

        moveVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = moveVelocity;

        if (jumpRequested)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (animator != null) animator.SetTrigger("Jump");
            jumpRequested = false;
        }
    }

    private bool IsGrounded()
    {
        float rayLength = 0.3f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(rayOrigin, Vector3.down, rayLength);
    }
}