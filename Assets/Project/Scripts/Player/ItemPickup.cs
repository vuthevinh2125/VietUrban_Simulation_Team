using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public Camera playerCamera;
    public Transform holdPosition;
    public float pickupRange = 3f;
    public LayerMask interactableLayer;

    private Interactable currentItem;

    public static bool isComputerOpen = false;

    void Update()
    {
        if (playerCamera == null) return;

        if (isComputerOpen) return;

        if (currentItem == null)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
            {
                TryPickUp();
            }
        }
        else
        {
           
            if (Input.GetKeyDown(KeyCode.E)) // setting keycap to throw box 
            {
                DropItem();
            }
            // place item on shelf
            else if (Input.GetMouseButtonDown(0))
            {
                TryPlaceItemOnShelf();
            }
            // pick up item from shelf
            else if (Input.GetMouseButtonDown(1))
            {
                TryTakeItemFromShelf();
            }
        }
    }

    private void TryPlaceItemOnShelf()
    {
        if (playerCamera == null) return;
        if (currentItem.productPrefab == null || currentItem.productCount <= 0) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer))
        {
            Shelf targetShelf = hit.collider.GetComponent<Shelf>();
            if (targetShelf != null)
            {
                Transform spot = targetShelf.GetAvailableSlot();
                if (spot != null)
                {
                    currentItem.productCount--;
                    currentItem.UpdateVisuals();

                    GameObject newProduct = Instantiate(currentItem.productPrefab);
                    targetShelf.AddItemToShelf(newProduct, holdPosition, currentItem.itemData);

                    Debug.Log("Current count: " + currentItem.productCount);
                }
            }
        }
    }

    private void TryTakeItemFromShelf()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer))
        {
            Shelf targetShelf = hit.collider.GetComponent<Shelf>();
            if (targetShelf != null)
            {
                if (currentItem.productCount < currentItem.visualItems.Length)
                {
                    GameObject retrievedItem = targetShelf.RemoveItemFromShelf();

                    if (retrievedItem != null)
                    {
                        Destroy(retrievedItem);
                        currentItem.productCount++;
                        currentItem.UpdateVisuals();

                        Debug.Log("Current count " + currentItem.productCount);
                    }
                    else
                    {
                        Debug.Log("Shelf is empty, nothing to retrieve!");
                    }
                }
                else
                {
                    Debug.Log("Shelf is full, cannot retrieve more items!");
                }
            }
        }
    }

    private void TryPickUp()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupRange, interactableLayer))
        {
            ComputerZoom computer = hit.collider.GetComponent<ComputerZoom>();
            if (computer != null)
            {
                computer.ToggleComputer();
                return;
            }

            ComputerInteract computerInteract = hit.collider.GetComponent<ComputerInteract>();
            if (computerInteract != null)
            {
                computerInteract.OpenComputer();
                return;
            }

            Interactable item = hit.collider.GetComponent<Interactable>();
            if (item != null)
            {
                currentItem = item;
                currentItem.OnPickedUp(holdPosition);
            }
        }
    }

    private void DropItem()
    {
        if (currentItem != null)
        {
            currentItem.OnDropped();
            currentItem = null;
        }
    }
}