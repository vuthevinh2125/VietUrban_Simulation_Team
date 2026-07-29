using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Speed")]
    public float moveSpeed = 10f;
    public float turnSpeed = 720f;
    public float jumpForce = 5f;

    [Header("Interaction")]
    public float interactRange = 2f;

    private Rigidbody rb;
    private Animator animator;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("Error: No Animator found!");
        }
    }

    void Update()
    {
        // Kiểm tra xem nhân vật có đang chạm đất không
        isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.01f;

        // Xử lý Nhảy
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactRange);
        Interactable targetItem = null;

        // Quét xem có vật phẩm nào xung quanh không
        foreach (var hitCollider in hitColliders)
        {
            Interactable interactable = hitCollider.GetComponent<Interactable>();
            if (interactable != null)
            {
                targetItem = interactable;
                break; // Tìm thấy 1 cái là dừng quét ngay
            }
        }

        // Nếu thấy vật phẩm trong tầm với
        if (targetItem != null)
        {
            // Bảo GameManager hiện cái dòng chữ trong script Interactable lên
            GameManager.Instance.ShowInteractText(targetItem.promptMessage);

            // Chờ người chơi bấm phím E
            if (Input.GetKeyDown(KeyCode.E))
            {
                targetItem.Interact();
                GameManager.Instance.HideInteractText(); // Lụm xong thì tắt chữ đi
            }
        }
        else
        {
            // Nếu không có vật phẩm nào ở gần thì tắt chữ đi
            GameManager.Instance.HideInteractText();
        }
    }

    void FixedUpdate()
    {
        // Lấy Input từ bàn phím
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // --- BẮT ĐẦU PHẦN SỬA ĐỒNG BỘ CAMERA ---
        // Lấy hướng nhìn hiện tại của Camera chính
        Transform camTransform = Camera.main.transform;
        Vector3 camForward = camTransform.forward;
        Vector3 camRight = camTransform.right;

        // Bỏ qua trục Y (lên/xuống) để nhân vật không bị bay lên trời hoặc lún xuống đất
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // Tính toán hướng đi mới: W luôn là thẳng theo camera, A D là dạt sang 2 bên
        Vector3 movement = (camForward * moveZ + camRight * moveX).normalized;
        // --- KẾT THÚC PHẦN SỬA ---

        // Di chuyển vật lý
        Vector3 moveVelocity = movement * moveSpeed;
        moveVelocity.y = rb.linearVelocity.y; // Giữ nguyên vận tốc rơi tự do của nhân vật
        rb.linearVelocity = moveVelocity;

        // Xử lý xoay người mặt về phía đang chạy
        if (movement != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, turnSpeed * Time.fixedDeltaTime);
        }

        // Xử lý Animation chạy/đi bộ
        if (animator != null)
        {
            float currentSpeed = movement.magnitude;

            if (currentSpeed > 0f)
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    animator.SetFloat("Speed", 1.0f);
                }
                else
                {
                    animator.SetFloat("Speed", 0.3f);
                }
            }
            else
            {
                animator.SetFloat("Speed", 0.0f);
            }
        }
    }

    // Hàm này giúp vẽ một vòng tròn màu vàng dưới chân nhân vật trong cửa sổ Scene
    // Nhờ đó bạn sẽ thấy chính xác tầm với (interactRange) của nhân vật tới đâu
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}