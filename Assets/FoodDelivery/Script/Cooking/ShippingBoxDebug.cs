using System.Collections.Generic;
using UnityEngine;

public class ShippingBoxDebug : MonoBehaviour
{
    [SerializeField] private ShippingBox boxPrefab;
    [SerializeField] private RecipeData recipeData;
    [SerializeField] private ShippingShelf shelf;

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            Order order = new Order(recipeData, 3, 1, 10000);
            Order order2 = new Order(recipeData, 2, 2, 10000);
            List<CookingResult> resultList = new List<CookingResult>();
            resultList.Add(new CookingResult());
            order.StartMaking();
            order.MarkCooking();
            order.AddCookedResults(resultList);
            order.Confirm();
            order2.StartMaking();

            ShippingBox box1 = Instantiate(boxPrefab);
            ShippingBox box2 = Instantiate(boxPrefab);

            Log("box 1 SetUp(order)", box1.SetUp(order, shelf, 0), box1);
            Log("box 1 SetUp(order)2", box1.SetUp(order, shelf, 0), box1);
            Log("box 2 SetUp(order)null", box2.SetUp(null, shelf, 1), box2);
            Log("box 2 SetUp(order)2", box2.SetUp(order2, shelf, 1), box2);
            Log("box 2 SetUp(order)", box2.SetUp(order, shelf, 1), box2);


        }
    }
    public void Log(string label, bool result, ShippingBox box)
    {
        if (box.Order == null)
        {
            Debug.Log(label + " | " + result + " box has no order ");
        }
        else
        {
            Debug.Log(label + " - " + result + " | Box name: " + box.ItemName + " | id: " + box.Order.OrderId + " | dish count: " + box.DishCount);
        }

    }
}
