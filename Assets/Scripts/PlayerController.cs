using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Speed")]
    public float moveSpeed = 10f;
    public float turnSpeed = 720f;
    public float jumpForce = 5f;

    [Header("Interaction & Carry")]
    public float interactRange = 2f;
    public Transform holdPoint;
    public float throwForce = 15f;

    private Rigidbody rb;
    private Animator animator;
    private bool isGrounded;
    private Rigidbody heldItemRb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.01f;

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (animator != null) animator.SetTrigger("Jump");
        }

        if (heldItemRb == null)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactRange);
            Interactable targetItem = null;

            foreach (var hitCollider in hitColliders)
            {
                Interactable interactable = hitCollider.GetComponent<Interactable>();
                if (interactable != null)
                {
                    targetItem = interactable;
                    break;
                }
            }

            if (targetItem != null)
            {
                GameManager.Instance.ShowInteractText(targetItem.promptMessage);

                if (Input.GetKeyDown(KeyCode.E))
                {
                    GameManager.Instance.HideInteractText();
                    PickUpItem(targetItem.gameObject);
                }
            }
            else
            {
                GameManager.Instance.HideInteractText();
            }
        }
        else
        {
            GameManager.Instance.ShowInteractText("Press E to Drop | Left Click to Throw");

            if (Input.GetKeyDown(KeyCode.E))
            {
                DropItem();
            }
            else if (Input.GetMouseButtonDown(0))
            {
                ThrowItem();
            }
        }
    }

    void FixedUpdate()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Transform camTransform = Camera.main.transform;
        Vector3 camForward = camTransform.forward;
        Vector3 camRight = camTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 movement = (camForward * moveZ + camRight * moveX).normalized;

        Vector3 moveVelocity = movement * moveSpeed;
        moveVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = moveVelocity;

        if (movement != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, turnSpeed * Time.fixedDeltaTime);
        }

        if (animator != null)
        {
            float currentSpeed = movement.magnitude;
            animator.SetFloat("Speed", currentSpeed > 0f ? (Input.GetKey(KeyCode.LeftShift) ? 1.0f : 0.3f) : 0.0f);
        }
    }

    private void PickUpItem(GameObject item)
    {
        heldItemRb = item.GetComponent<Rigidbody>();
        if (heldItemRb != null)
        {
            heldItemRb.useGravity = false;
            heldItemRb.isKinematic = true;

            Collider itemCollider = heldItemRb.GetComponent<Collider>();
            if (itemCollider != null) itemCollider.enabled = false;

            heldItemRb.transform.position = holdPoint.position;
            heldItemRb.transform.parent = holdPoint;
        }
    }

    private void DropItem()
    {
        if (heldItemRb != null)
        {
            heldItemRb.useGravity = true;
            heldItemRb.isKinematic = false;

            Collider itemCollider = heldItemRb.GetComponent<Collider>();
            if (itemCollider != null) itemCollider.enabled = true;

            heldItemRb.transform.parent = null;
            heldItemRb = null;
            GameManager.Instance.HideInteractText();
        }
    }

    private void ThrowItem()
    {
        if (heldItemRb != null)
        {
            heldItemRb.useGravity = true;
            heldItemRb.isKinematic = false;

            Collider itemCollider = heldItemRb.GetComponent<Collider>();
            if (itemCollider != null) itemCollider.enabled = true;

            heldItemRb.transform.parent = null;

            Vector3 throwDirection = Camera.main.transform.forward;
            heldItemRb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            heldItemRb = null;
            GameManager.Instance.HideInteractText();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}