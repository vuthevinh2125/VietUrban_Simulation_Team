using UnityEngine;
public enum ItemCategory
{
    FoodBuy, Fuel, Repair, FoodSell
}
public enum SellType
{
    Gram, Quantity
}
[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemId;
    public string itemName;
    public ItemCategory category;
    public SellType sellType;
    public int basePriceBuy;
    public string description;
    public float limitPerBuying;
    public Sprite icon;

}
