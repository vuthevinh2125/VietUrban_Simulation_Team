using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    public Sprite icon;

    [TextArea(3, 5)]
    public string description;

    [Header("Inventory Settings")]
    public bool isStackable = true;
    public int maxStack = 99;

    [Header("Economy")]
    public int buyPrice;
    public int sellPrice;
}