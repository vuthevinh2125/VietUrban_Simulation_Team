using UnityEngine;

public class CookingBatchDebug : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private RecipeData recipeData;
    private Order order;
    private CookingBatch batch;
    private bool ticking;



    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y)) {
            ticking = false;
            order = new Order(recipeData, 3, 1, 10000);
            batch = new CookingBatch(order);
            Log("Start Cooking", batch.StartCooking());
            Log("Stage", batch.Stage(null));
            Log("Stage", batch.Stage(new CookingResult()));
            Log("Stage", batch.Stage(new CookingResult()));
            bool started = batch.StartCooking();
            Log("Start Cooking", started);
            ticking = started;
            Log("Stage", batch.Stage(new CookingResult()));
       
        }

        if (ticking == true && batch != null) 
        {
            
            bool finished = batch.Tick(Time.deltaTime);
            if (finished == true)
            {

                Debug.Log("done" + batch.Progress);
                ticking = false;

            }
        }
    }
    private void Log(string label, bool status)
    {
        Debug.Log(label + " -" + status + " | state " + batch.State + " | staged " + batch.StagedCount + " | progress " + batch.Progress);
    }
}
