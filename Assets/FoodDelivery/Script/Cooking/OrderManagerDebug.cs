
using UnityEngine;

public class OrderManagerDebug : MonoBehaviour
{
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private RecipeData recipeData;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            TestOrderManager();
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            TestConfirmOrder();
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            TestMakeUpOrder();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            TestCreateShippingBox();
        }
    }
    private void TestOrderManager()
    {
        Order order = new Order(recipeData, 3, 100, 100);
        orderManager.AddOrder(order);
        Debug.Log("added order: " + orderManager.GetOrderById(100));
        Order foundOrder = orderManager.GetOrderById(100);
        Debug.Log("GetOrderById: " + foundOrder);
        bool selected = orderManager.SelectOrder(100);
        Debug.Log("SelectOrder: " + selected);
        bool accepted = orderManager.AcceptOrder(100);
        Debug.Log("accepted order: " + accepted);
        Debug.Log("current status: " + order.OrderStatus);
    }
    private void TestConfirmOrder()
    {
        bool confirmed = orderManager.ConfirmSelectedOrder();
        Debug.Log("confirmed order: " + confirmed);
        Debug.Log("current status: " + orderManager.SelectedOrder.OrderStatus);
    }
    private void TestMakeUpOrder()
    {
        bool makeUp = orderManager.MakeUpSelectedOrder();
        Debug.Log("make up order: " + makeUp);
        Debug.Log("current status: " + orderManager.SelectedOrder.OrderStatus);
    }
    private void TestCreateShippingBox()
    {
        bool created = orderManager.CreateShippingBox();
        Debug.Log("created shipping box: " + created);
    }
}
