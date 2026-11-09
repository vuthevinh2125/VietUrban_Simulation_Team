using UnityEngine;


public class AI_Movement : MonoBehaviour
{
    Animator animator;
    CharacterController controller;
    public float moveSpeed = 0.2f;
    public float rotationSpeed = 5f;
    float walkTime;
    public float walkCounter;
    float waitTime;
    public float waitCounter;
    int WalkDirection; 
    public bool isWalking;
    public float gravity = -9.8f; //character controller does not have gravity like rigid
    float verticalVelocity;
    Vector3 lastPosition;
    float stuckCounter;
    public float stuckInterval = 0.5f;
    public float stuckDistance = 0.02f;
    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        walkTime = Random.Range(6, 10);
        waitTime = Random.Range(3, 6);
        waitCounter = waitTime;
        walkCounter = walkTime;
        lastPosition = transform.position;
        ChooseDirection();
    }
    void Update()
    {
        //attach to the ground, prevent from flying in the air
        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime; //if it is falling, add vervelo with gravity 
        }
        if (isWalking) //check if the animals is running
        {
            animator.SetBool("isRunning", true);
            walkCounter -= Time.deltaTime;
            float targetY;
            if (WalkDirection == 0)
            {
                targetY = 0f;
            }
            else if (WalkDirection == 1)
            {
                targetY = 90f;
            }
            else if (WalkDirection == 2)
            {
                targetY = -90f;
            }
            else if (WalkDirection == 3)
            {
                targetY = 180f;
            }
            else
            {
                targetY = 0f;
            }
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(0f, targetY, 0f), rotationSpeed * Time.deltaTime);//gradually rotate from angle a to target angle b with ratio t: quaternion.slerp(a, b, t)

            Vector3 motion = transform.forward * moveSpeed;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
            stuckCounter += Time.deltaTime;
            if (stuckCounter >= stuckInterval)
            {
                float distanceMoved = Vector3.Distance(transform.position, lastPosition); //tinh khoang cach vi tri hien tai va vi tri last position
                if (distanceMoved < stuckDistance)
                {
                    isWalking = false;
                    animator.SetBool("isRunning", false);
                    waitCounter = waitTime;
                }
                lastPosition = transform.position;
                stuckCounter = 0f;
            }
            if (walkCounter <= 0)
            {
                isWalking = false;
                animator.SetBool("isRunning", false);
                waitCounter = waitTime;
            }
        }
        else
        {
            waitCounter -= Time.deltaTime;
            if (waitCounter <= 0)
            {
                ChooseDirection();
            }
        }
    }
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isWalking)
        {
            return;
        }
        if (hit.normal.y > 0.4f)
        {
            return;
        }
        isWalking = false;
        animator.SetBool("isRunning", false);
        waitCounter = Random.Range(0.5f, 1.5f);
    }

    public void ChooseDirection()
    {
        int newDirection;
        int attempts = 0;
        do
        {
            newDirection = Random.Range(0, 4);
            attempts++;
        }
        while (newDirection == WalkDirection && attempts < 5);

            WalkDirection = newDirection;
            isWalking = true;
            walkCounter = walkTime;
    }
}