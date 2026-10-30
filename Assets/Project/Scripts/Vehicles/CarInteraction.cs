using UnityEngine;

public class CarInteraction : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform driverSeat;
    public Transform exitPoint;
    public GameObject carCamera;
    public GameObject playerCamera;

    [Header("Settings")]
    public float interactionDistance = 3f;

    private bool isPlayerInside = false;
    private PlayerCarController carController;

    void Start()
    {
        carController = GetComponent<PlayerCarController>();
        if (carCamera != null) carCamera.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);

        if (distance <= interactionDistance)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (!isPlayerInside)
                {
                    // Chỉ xe gần nhất mới lên, không lên khi đang mở bảng giao diện
                    if (VehicleManager.IsNearestMountable(transform, player.position)) EnterCar();
                }
                else
                {
                    ExitCar();
                }
            }
        }
    }

    void EnterCar()
    {
        isPlayerInside = true;

        if (player != null)
        {
            player.gameObject.SetActive(false);
            player.SetParent(transform);
        }

        if (carController != null)
        {
            carController.isDriving = true;
        }

        if (playerCamera != null) playerCamera.SetActive(false);
        if (carCamera != null) carCamera.SetActive(true);

        Debug.Log("Entered vehicle successfully.");
    }

    void ExitCar()
    {
        isPlayerInside = false;

        if (carController != null)
        {
            carController.isDriving = false;
        }

        if (player != null)
        {
            player.SetParent(null);
            if (exitPoint != null)
            {
                player.position = exitPoint.position;
            }
            player.gameObject.SetActive(true);
        }

        if (carCamera != null) carCamera.SetActive(false);
        if (playerCamera != null) playerCamera.SetActive(true);

        Debug.Log("Exited vehicle successfully.");
    }
}