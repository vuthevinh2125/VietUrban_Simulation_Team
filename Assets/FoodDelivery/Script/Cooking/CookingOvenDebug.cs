using UnityEngine;

public class CookingOvenDebug : MonoBehaviour
{
    [SerializeField] private RecipeData recipeData;
    [SerializeField] private CookingOven oven;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void OnEnable()
    {
        oven.OnBatchDone += HandleBatchDone;
    }
    public void OnDisable()
    {
        oven.OnBatchDone -= HandleBatchDone;
    }
    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.U))
        {
            Order order = new Order(recipeData, 3, 1, 10000);
            order.StartMaking();
            CookingBatch batch = new CookingBatch(order);
            batch.Stage(new CookingResult());
            batch.Stage(new CookingResult());
            bool firstTry = oven.StartBatch(batch);
            bool secondeTry = oven.StartBatch(batch);
            Debug.Log(firstTry + " | " + secondeTry);
        }
    }
    public void HandleBatchDone(CookingBatch finishedBatch)
    {
        Debug.Log("produced: " + finishedBatch.Order.ProducedCount + " | status: " + finishedBatch.Order.OrderStatus);
    }
    
}
