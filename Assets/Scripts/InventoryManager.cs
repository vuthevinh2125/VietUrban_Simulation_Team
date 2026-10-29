using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public InventorySlot(ItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void AddAmount(int value)
    {
        amount += value;
    }
}

public class InventoryManager : MonoBehaviour
{
    public List<InventorySlot> inventory = new List<InventorySlot>();
    public int inventoryCapacity = 20;

    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
        if (itemToAdd.isStackable)
        {
            foreach (InventorySlot slot in inventory)
            {
                if (slot.item == itemToAdd && slot.amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slot.amount;
                    if (spaceLeft >= amountToAdd)
                    {
                        slot.AddAmount(amountToAdd);
                        return true;
                    }
                    else
                    {
                        slot.AddAmount(spaceLeft);
                        amountToAdd -= spaceLeft;
                    }
                }
            }
        }

        if (inventory.Count < inventoryCapacity)
        {
            inventory.Add(new InventorySlot(itemToAdd, amountToAdd));
            return true;
        }

        return false;
    }
}