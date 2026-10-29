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

    private float cameraPitch = 0f;
    private Rigidbody rb;
    private Animator animator;

    private float moveX;
    private float moveZ;
    private bool isSprinting;
    private bool jumpRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => LookLocked = false;

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
        if (UIState.IsBlockingUIOpen)
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

        if (animator != null)
        {
            float currentSpeed = new Vector2(moveX, moveZ).magnitude;
            animator.SetFloat("Speed", currentSpeed > 0f ? (isSprinting ? 1.0f : 0.3f) : 0.0f);
        }
    }

    void FixedUpdate()
    {
        if (UIState.IsBlockingUIOpen)
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