using UnityEngine;
public class CookingSessionDebug : MonoBehaviour
{
    [SerializeField] private CookingSession session;
    [SerializeField] private RecipeData recipeData;
    [SerializeField] private KeyCode testKey = KeyCode.F9;
    private int count;
    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            count++;
            Order order = new Order(recipeData, 3, count, 10000);
            order.StartMaking();
            bool ok = session.BeginOrder(order);
            Debug.Log("begin order: " + ok.ToString().ToLowerInvariant() + " | order id: " + order.OrderId + " | status: " + order.OrderStatus.ToString().ToLowerInvariant());
        }
    }
}
