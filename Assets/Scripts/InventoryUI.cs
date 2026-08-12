using System.Collections.Generic;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public InventoryManager inventoryManager;
    public Transform itemsParent;
    public GameObject slotPrefab;

    private List<InventorySlotUI> slots = new List<InventorySlotUI>();

    void Start()
    {
        UpdateUI();
    }

    public void UpdateUI()
    {
        foreach (Transform child in itemsParent)
        {
            Destroy(child.gameObject);
        }

        slots.Clear();

        for (int i = 0; i < inventoryManager.inventoryCapacity; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, itemsParent);
            InventorySlotUI slotUI = newSlot.GetComponent<InventorySlotUI>();
            slots.Add(slotUI);

            if (i < inventoryManager.inventory.Count)
            {
                slotUI.UpdateSlot(inventoryManager.inventory[i]);
            }
            else
            {
                slotUI.ClearSlot();
            }
        }
    }
}