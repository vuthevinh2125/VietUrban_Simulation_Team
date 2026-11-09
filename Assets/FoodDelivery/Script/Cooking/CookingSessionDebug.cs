using UnityEngine;

public class CookingSessionDebug : MonoBehaviour
{
    [SerializeField] private CookingSession session;
    [SerializeField] private RecipeData recipeData;
    private int count;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            count++;
            Order order = new Order(recipeData, 3, count, 10000);
            order.StartMaking();
            bool ok = session.BeginOrder(order);
            Debug.Log("BeginOrder: " + ok + " | order id: " + order.OrderId + " | status: " + order.OrderStatus);
        }
    }
}