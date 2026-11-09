using System;
using System.Collections.Generic;
[System.Serializable]
public class PlayerData
{
    public int money = 200000;
    public List<InventoryEntries> inventory = new();

    public void AddItem(string itemId, float amount)
    {
        foreach (var entry in inventory)
        {
            if (itemId == entry.itemId)
            {
                entry.amount += amount;
                return;
            }
            
        }
        inventory.Add(new InventoryEntries(itemId, amount));
    }
    public float GetAmount(string itemId)
    {
        foreach(var entry in inventory)
        {
            if (entry.itemId == itemId)
            {
                return entry.amount;
            }
            
        }
        return 0f;
    }
    public bool TryRemove(string itemId, float amount)
    {
        foreach(var entry in inventory)
        {
            if (entry.itemId == itemId)
            {
                if (entry.amount < amount)
                {
                    return false;
                }
                entry.amount -= amount;
                return true;
            }
        }
        return false;
    }

}
