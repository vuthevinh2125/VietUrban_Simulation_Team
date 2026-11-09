using System.Collections.Generic;
using UnityEngine;

public class ShippingShelfDebug : MonoBehaviour
{
    [SerializeField]private ShippingShelf shelf;
    [SerializeField] private RecipeData recipeData;
    private int count;
    private Order lastOrder;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            lastOrder = CreatedClosedOrder();
            Log("N", shelf.TryCreateBox(lastOrder), lastOrder);
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (lastOrder == null)
            {
                Debug.Log("last order is unavailable");
                return;
            }
            Log("M", shelf.TryCreateBox(lastOrder), lastOrder);
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            count++;
            Order orderNotConfirm = new Order(recipeData, 3, count, 10000);

            orderNotConfirm.StartMaking();
            orderNotConfirm.MarkCooking();
            List<CookingResult> resultList = new List<CookingResult>();
            resultList.Add(new CookingResult());
            orderNotConfirm.AddCookedResults(resultList);
            Log("Z", shelf.TryCreateBox(orderNotConfirm), orderNotConfirm);
        }
    }
    public Order CreatedClosedOrder()
    {
        count++;
        Order order = new Order(recipeData, 3, count, 10000);
        
        order.StartMaking();
        order.MarkCooking();
        List<CookingResult> resultList = new List<CookingResult>();
        resultList.Add(new CookingResult());
        order.AddCookedResults(resultList);
        order.Confirm();
        return order;
    }
    public void Log(string label, bool ok, Order order)
    {
        Debug.Log(label + " | Ok: " + ok + " | Id: " + order.OrderId + " | status: " + order.OrderStatus + " | is created: " + order.IsCreated + " | available slot: " + shelf.FindFreeSlot());
    }
}
