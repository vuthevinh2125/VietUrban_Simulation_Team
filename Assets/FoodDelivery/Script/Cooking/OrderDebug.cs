using UnityEngine;
using System.Collections.Generic;
public class OrderDebug : MonoBehaviour
{
     private Order order;
    [SerializeField] private RecipeData recipeData;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) {
            order = new Order(recipeData, 3, 1, 10000);
            Debug.Log("Order id: " + order.OrderId + " | unit price: " + order.UnitPrice + " | total: " + order.Total + " | is created: " + order.IsCreated);
            Log("StartMaking", order.StartMaking(), order.IsCreated);
            Log("Cooking", order.MarkCooking(), order.IsCreated);
            List<CookingResult> resultList = new List<CookingResult>();
            resultList.Add(new CookingResult());
            resultList.Add(new CookingResult());
            Log("Cooked", order.AddCookedResults(resultList), order.IsCreated);

            Log("StartMakeUp", order.StartMakeUp(), order.IsCreated);
            Log("Cooking", order.MarkCooking(), order.IsCreated);
            resultList = new List<CookingResult>();
            resultList.Add(new CookingResult());
            resultList.Add(new CookingResult());
            resultList.Add(new CookingResult());
            Log("Cooked", order.AddCookedResults(resultList), order.IsCreated);
            bool isCreated =  order.MarkBoxCreated();
            Debug.Log("Is created truoc confirm: " + isCreated);
            Log("Confirm", order.Confirm(), order.IsCreated);
            Log("Confirm", order.Confirm(), order.IsCreated);
            isCreated = order.MarkBoxCreated();
            Debug.Log("Is created sau confirm lan 1: " + isCreated);
            isCreated = order.MarkBoxCreated();
            Debug.Log("Is created sau confirm lan 2: " + isCreated);
            Log("Cooked", order.AddCookedResults(null), order.IsCreated);
        }

    }
    private void Log(string label, bool status, bool isCreated)
    {
        Debug.Log(label + " -" + status + " | " + order.OrderStatus + " | produced " + order.ProducedCount + " | results " + order.Results.Count + " | complete " + order.IsComplete + "| created: " + order.IsCreated);


    }
}
