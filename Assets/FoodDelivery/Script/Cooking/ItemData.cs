using UnityEngine;
public enum ItemCategory
{
    FoodBuy = 0,
    Consume = 1,
    FoodSell = 3
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
    public GameObject worldPrefab;
    [Min(0f)] public float cookingVisualSize;
    public Vector3 cookingVisualRotation;
    [Min(0f)] public float staminaRestore;

}
