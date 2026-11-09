using UnityEngine;

public class ShippingBox : InteractableObject
{
    private ShippingShelf shelf;
    private int slotIndex;
    private Order order;
    public Order Order
    {
        get
        {
            return order;
        }
    }
    public int OrderId
    {
        get
        {
            if( order == null)
            {
                return 0;
            }
            return order.OrderId;
        }
    }
    public int SlotIndex
    {
        get { return slotIndex; }
    }
    public int DishCount
    {
        get
        {
            if (order == null)
            {
                return 0;
            }
            return order.ProducedCount;
        }
    }
    public bool SetUp(Order newOrder, ShippingShelf newShelf, int newSlotIndex)
    {
        if (newOrder == null)
        {
            return false;
        }
        if (order != null)
        {
            return false;
        }
        if (newShelf == null)
        {
            return false;
        }
        if (newOrder.OrderStatus != Order.OrderState.Closed)
        {
            return false;
        }
        if (newOrder.Recipe == null)
        {
            return false;
        }
        order = newOrder;
        shelf = newShelf;
        slotIndex = newSlotIndex;
        ItemName = "Order #" + newOrder.OrderId + ": " + newOrder.Recipe.RecipeName;
        return true;
    }
    public override void Interact()
    {
        if (order == null)
        {
            Debug.Log("box has no order");
            return;
        }
        if (shelf == null || !shelf.TryCollectBox(this))
        {
            Debug.Log("could not collect the shipping box", this);
        }
    }
}
