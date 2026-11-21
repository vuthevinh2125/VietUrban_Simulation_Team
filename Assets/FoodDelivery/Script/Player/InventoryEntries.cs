using UnityEngine;
[System.Serializable]
public class InventoryEntries
{
    public string itemId;
    public float amount;
    public InventoryEntries(string itemId, float amount)
    {
        this.itemId = itemId;
        this.amount = amount;
    }
    public InventoryEntries() { }
}
