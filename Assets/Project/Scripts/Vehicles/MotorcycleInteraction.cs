using UnityEngine;

[RequireComponent(typeof(PlayerCarController))]
public class MotorcycleInteraction : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform driverSeat;
    public Transform exitPoint;
    public GameObject motorcycleCamera;
    public GameObject playerCamera;

    [Header("Settings")]
    public float interactionDistance = 3.0f;

    private bool isPlayerMounted = false;
    private PlayerCarController bikeController;

    // Cached squared distance to save CPU power
    private float sqrInteractionDistance;

    void Start()
    {
        bikeController = GetComponent<PlayerCarController>();

        // Pre-calculate the squared distance once at startup
        sqrInteractionDistance = interactionDistance * interactionDistance;

        if (motorcycleCamera != null) motorcycleCamera.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        // OPTIMIZATION: Separate the logic to avoid doing math while driving
        if (isPlayerMounted)
        {
            // If already on the bike, just listen for the dismount key
            if (Input.GetKeyDown(KeyCode.F))
            {
                DismountBike();
            }
        }
        else
        {
            // Check distance using sqrMagnitude (much faster than Vector3.Distance)
            float currentSqrDistance = (player.position - transform.position).sqrMagnitude;

            if (currentSqrDistance <= sqrInteractionDistance)
            {
                // Chỉ xe gần nhất mới lên (tránh lên 2 xe đỗ sát nhau), không lên khi đang mở bảng giao diện
                if (Input.GetKeyDown(KeyCode.F) && VehicleManager.IsNearestMountable(transform, player.position))
                {
                    MountBike();
                }
            }
        }
    }

    void MountBike()
    {
        isPlayerMounted = true;

        if (player != null)
        {
            player.gameObject.SetActive(false);
            player.SetParent(transform);
            // Optional: Snap player strictly to the seat position if you re-enable the mesh later
            player.position = driverSeat != null ? driverSeat.position : transform.position;
        }

        if (bikeController != null) bikeController.isDriving = true;

        SwitchCameras(false);
        Debug.Log("Mounted the motorcycle.");
    }

    void DismountBike()
    {
        isPlayerMounted = false;

        if (bikeController != null) bikeController.isDriving = false;

        if (player != null)
        {
            player.SetParent(null);
            if (exitPoint != null) player.position = exitPoint.position;
            player.gameObject.SetActive(true);
        }

        SwitchCameras(true);
        Debug.Log("Dismounted the motorcycle.");
    }

    // Clean code helper method to toggle cameras instantly
    private void SwitchCameras(bool isPlayerCamActive)
    {
        if (playerCamera != null) playerCamera.SetActive(isPlayerCamActive);
        if (motorcycleCamera != null) motorcycleCamera.SetActive(!isPlayerCamActive);
    }
}