using UnityEngine;

/// <summary>Một món nội thất bán trên máy tính (kệ, tủ lạnh, tủ đông...).</summary>
[CreateAssetMenu(fileName = "New Furniture", menuName = "Supermarket/Furniture")]
public class FurnitureData : ScriptableObject
{
    public string displayName;
    public float price = 200f;
    [Tooltip("Cấp cửa hàng tối thiểu để mua")]
    public int requiredLevel = 1;
    [Tooltip("Prefab đã có Shelf + ô xếp hàng (tạo bằng Tools > Store Setup > 9)")]
    public GameObject prefab;
    [Tooltip("Shelf = hàng khô, Fresh = tủ lạnh / tủ đông")]
    public string displayType = "Shelf";
    public Sprite icon;
}
