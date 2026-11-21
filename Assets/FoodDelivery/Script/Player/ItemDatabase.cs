using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new List<ItemData>();
    public IReadOnlyList<ItemData> Items
    {
        get
        {
            return items;
        }
    }
    public ItemData GetById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }
        foreach (ItemData item in items)
        {
            if (item != null && item.itemId == itemId)
            {
                return item;
            }
        }
        return null;
    }
}
