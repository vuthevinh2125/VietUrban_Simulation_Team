using System;
using System.Collections.Generic;
[System.Serializable]
public class PlayerData
{
    public int money = 200000;
    public int reputation = 50;
    public float stamina = 100f;
    public bool businessRegistered;
    public bool businessSuspended;
    public List<InventoryEntries> inventory = new();
    public List<StationStockEntry> stationStocks = new();

    public bool CanSell
    {
        get { return businessRegistered && !businessSuspended && reputation >= 0; }
    }

    public bool TryGetStationStock(string stationId, out float amount)
    {
        amount = 0f;
        if (string.IsNullOrEmpty(stationId))
        {
            return false;
        }
        if (stationStocks == null)
        {
            stationStocks = new List<StationStockEntry>();
        }
        foreach (StationStockEntry entry in stationStocks)
        {
            if (entry != null && entry.stationId == stationId)
            {
                amount = entry.amount;
                return true;
            }
        }
        return false;
    }

    public void SetStationStock(string stationId, float amount)
    {
        if (string.IsNullOrEmpty(stationId))
        {
            return;
        }
        if (stationStocks == null)
        {
            stationStocks = new List<StationStockEntry>();
        }
        foreach (StationStockEntry entry in stationStocks)
        {
            if (entry != null && entry.stationId == stationId)
            {
                entry.amount = amount;
                return;
            }
        }
        stationStocks.Add(new StationStockEntry(stationId, amount));
    }

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

[System.Serializable]
public class StationStockEntry
{
    public string stationId;
    public float amount;

    public StationStockEntry(string stationId, float amount)
    {
        this.stationId = stationId;
        this.amount = amount;
    }
}
