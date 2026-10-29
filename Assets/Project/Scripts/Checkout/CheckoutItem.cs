using UnityEngine;

/// <summary>Một món hàng khách đặt trên quầy, chờ người chơi click để quét.</summary>
public class CheckoutItem : MonoBehaviour
{
    public ItemData item;
    public float price;
    public CheckoutCounter counter;

    public string DisplayName => item != null ? item.itemName : "Item";
}
