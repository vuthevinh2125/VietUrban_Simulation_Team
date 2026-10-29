using UnityEngine;
using TMPro;

public class ItemPickup : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;

    private bool canPickUp = false;
    private InventoryManager playerInventory;
    private TextMeshProUGUI interactText;

    private void Start()
    {
        GameObject textObj = GameObject.Find("InteractText");
        if (textObj != null)
        {
            interactText = textObj.GetComponent<TextMeshProUGUI>();
            interactText.text = "";
        }
        else
        {
            Debug.LogWarning("WARNING: InteractText not found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger entered by: " + other.gameObject.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player detected. Ready to pick up.");
            canPickUp = true;
            playerInventory = other.GetComponent<InventoryManager>();

            if (interactText != null && item != null)
            {
                interactText.text = "Press [F] to pick up " + item.itemName;
            }
        }
        else
        {
            Debug.Log("Non-player object entered trigger.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canPickUp = false;
            playerInventory = null;

            if (interactText != null)
            {
                interactText.text = "";
            }
        }
    }

    private void Update()
    {
        if (canPickUp && Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log("F key pressed.");

            if (playerInventory != null && item != null)
            {
                bool wasPickedUp = playerInventory.AddItem(item, amount);

                if (wasPickedUp)
                {
                    Debug.Log("Item picked up successfully.");
                    InventoryUI ui = FindAnyObjectByType<InventoryUI>();
                    if (ui != null)
                    {
                        ui.UpdateUI();
                    }

                    if (interactText != null)
                    {
                        interactText.text = "";
                    }

                    Destroy(gameObject);
                }
                else
                {
                    Debug.Log("Inventory full. Cannot pick up.");
                }
            }
        }
    }
}