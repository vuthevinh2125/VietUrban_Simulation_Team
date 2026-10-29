using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCShopper : MonoBehaviour
{
    /// <summary>Tất cả khách đang có trong scene (AutoDoor dùng để mở cửa).</summary>
    public static readonly List<NPCShopper> Active = new List<NPCShopper>();

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public enum NPCState { GoToShelf, PickItem, GoToCheckout, Checkout, Leave }

    [Header("Trạng thái hiện tại")]
    public NPCState currentState;

    [Header("Bản đồ siêu thị")]
    public Transform[] shelves;
    public Transform checkoutCounter;
    public Transform exitDoor;

    [Header("Đồ trên bàn thanh toán")]
    public GameObject checkoutItemsGroup; // Kéo nhóm đồ ở quầy thu ngân vào đây

    [Header("Hành vi ngẫu nhiên (Supermarket Simulator)")]
    public int minItemsToBuy = 1;
    public int maxItemsToBuy = 4;
    private int targetItemCount;
    private int currentItemCount = 0;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform currentTarget;

    // Biến phụ trợ chống kẹt
    private float stuckTimer = 0f;
    private Vector3 lastPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        targetItemCount = Random.Range(minItemsToBuy, maxItemsToBuy + 1);
        lastPosition = transform.position;

        // Ban đầu ẩn đồ trên bàn thanh toán đi
        if (checkoutItemsGroup != null) checkoutItemsGroup.SetActive(false);

        ChangeState(NPCState.GoToShelf);
    }

    void Update()
    {
        // Cập nhật animation di chuyển
        if (animator != null)
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }

        // Xử lý khi đang di chuyển
        if ((currentState == NPCState.GoToShelf || currentState == NPCState.GoToCheckout || currentState == NPCState.Leave) && currentTarget != null)
        {
            if (!agent.pathPending)
            {
                if (Vector3.Distance(transform.position, lastPosition) < 0.02f)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer > 2f)
                    {
                        Debug.LogError($"[BẮT BỆNH KẸT] PathStatus: {agent.pathStatus} | Khoảng cách đến đích: {Vector3.Distance(transform.position, currentTarget.position):F2}m | Mục tiêu: {currentTarget.name}");

                        agent.SetDestination(currentTarget.position);
                        stuckTimer = 0f;
                    }
                }
                else
                {
                    stuckTimer = 0f;
                }
                lastPosition = transform.position;

                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

                if (distanceToTarget <= agent.stoppingDistance + 0.5f)
                {
                    if (currentState == NPCState.GoToShelf)
                    {
                        StartCoroutine(HandlePickItem());
                    }
                    else if (currentState == NPCState.GoToCheckout)
                    {
                        StartCoroutine(HandleCheckout());
                    }
                    else if (currentState == NPCState.Leave)
                    {
                        Debug.Log("NPC đã ra đến cửa và rời khỏi siêu thị.");
                        Destroy(gameObject);
                    }
                }
            }
        }
    }

    private void ChangeState(NPCState newState)
    {
        currentState = newState;
        stuckTimer = 0f;

        if (agent != null) agent.isStopped = false;

        switch (currentState)
        {
            case NPCState.GoToShelf:
                if (shelves.Length > 0)
                {
                    Transform randomShelf = shelves[Random.Range(0, shelves.Length)];
                    currentTarget = randomShelf;
                    agent.SetDestination(currentTarget.position);
                    Debug.Log("NPC đang đi đến kệ hàng: " + randomShelf.name);
                }
                break;

            case NPCState.GoToCheckout:
                if (checkoutCounter != null)
                {
                    currentTarget = checkoutCounter;
                    agent.SetDestination(currentTarget.position);
                    Debug.Log("NPC đang đi thẳng ra quầy thu ngân.");
                }
                else
                {
                    ChangeState(NPCState.Leave);
                }
                break;

            case NPCState.Leave:
                if (checkoutItemsGroup != null) checkoutItemsGroup.SetActive(false);

                if (exitDoor != null)
                {
                    currentTarget = exitDoor;
                    agent.SetDestination(currentTarget.position);
                    Debug.Log("NPC đã thanh toán xong, đang đi ra cửa.");
                }
                else
                {
                    Destroy(gameObject);
                }
                break;
        }
    }

    private IEnumerator HandlePickItem()
    {
        currentState = NPCState.PickItem;
        currentTarget = null;

        if (agent != null) agent.isStopped = true;
        if (animator != null) animator.SetFloat("Speed", 0f);

        Debug.Log("NPC đang đứng chọn đồ tại kệ...");

        yield return new WaitForSeconds(Random.Range(2f, 4f));

        currentItemCount++;

        if (currentItemCount < targetItemCount)
        {
            ChangeState(NPCState.GoToShelf);
        }
        else
        {
            ChangeState(NPCState.GoToCheckout);
        }
    }

    private IEnumerator HandleCheckout()
    {
        currentState = NPCState.Checkout;
        currentTarget = null;

        if (agent != null) agent.isStopped = true;
        if (animator != null) animator.SetFloat("Speed", 0f);

        if (checkoutItemsGroup != null) checkoutItemsGroup.SetActive(true);

        Debug.Log("NPC đang đứng thanh toán tại quầy...");

        yield return new WaitForSeconds(Random.Range(3f, 5f));

        ChangeState(NPCState.Leave);
    }
}