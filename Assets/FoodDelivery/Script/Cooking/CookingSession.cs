using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class CookingResult
{
    public int ingredientMissingCount;
    public int ingredientExtraCount;
    public bool isCorrect;


}
//hold the ingredient then evaluate it, assign the result to current batch
public class CookingSession : MonoBehaviour
{
    private RecipeData currentRecipe;
    private CookingBatch currentBatch;
    [SerializeField] CookingOven oven;
    private Dictionary<string, float> addedAmount = new Dictionary<string, float>();
    public bool CanAccept
    {
        get
        {
            if (currentBatch != null && currentBatch.State == BatchState.Staging)
            {
                return true;
            }
            return false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            CookingResult result = Evaluate();

            Debug.Log($"thieu {result.ingredientMissingCount},  thua {result.ingredientExtraCount} + ket qua: {result.isCorrect}");
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("startCookingBatch: " + StartCookingBatch());
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            //StageDish();
            Debug.Log("stageDish: " + StageDish());
        }
    }
    public void AddedIngredient(ItemData ingredient, float amount)
    {
        if (CanAccept == false)
        {
            return;
        }
        if (addedAmount.TryGetValue(ingredient.itemId, out float currentAmount))
        {
            addedAmount[ingredient.itemId] = currentAmount + amount;
        }
        else
        {
            addedAmount[ingredient.itemId] = amount;
        }
        foreach (var pair in addedAmount)
        {
            Debug.Log(pair.Key + ": " + pair.Value);
        }
    }
    public CookingResult Evaluate()
    {
        if (currentRecipe == null)
        {

            Debug.Log("missing current recipe");
            return new CookingResult();
        }
        CookingResult result = new CookingResult();
        HashSet<string> recipeId = new HashSet<string>();
        foreach (RecipeIngredient need in currentRecipe.Ingredients)
        {
            addedAmount.TryGetValue(need.ingredient.itemId, out float have);
            recipeId.Add(need.ingredient.itemId);
            if (have < need.amount)
            {
                result.ingredientMissingCount++;
            }
            if (have > need.amount)
            {
                result.ingredientExtraCount++;
            }

        }
        foreach (var pair in addedAmount)
        {
            if (!recipeId.Contains(pair.Key))
            {
                result.ingredientExtraCount++;
            }
        }
        if (result.ingredientMissingCount == 0 && result.ingredientExtraCount == 0)
        {
            result.isCorrect = true;
        }
        return result;
    }
    public bool BeginOrder(Order order) //start making an order
    {
        if (order == null || order.OrderStatus != Order.OrderState.Making || order.Recipe == null)
        {
            return false;
        }
        // Keep one order on the plate at a time. Once its batch is sent to
        // the oven, currentBatch becomes null and another order can begin.
        if (addedAmount.Count > 0 || currentBatch != null)
        {
            return false;
        }
        currentRecipe = order.Recipe;
        currentBatch = new CookingBatch(order);
        return true;
    }
    public bool StageDish() {
        if (currentBatch == null || CanAccept == false || addedAmount.Count == 0)
        {
            Debug.Log("khong co me");
            return false;
        }
        if (CanAccept == false)
        {
            Debug.Log("dia k nhan");
            return false;
        }
        if (addedAmount.Count == 0)
        {
            Debug.Log("dia trong");
            return false;
        }
        CookingResult result = Evaluate();
        if (currentBatch.Stage(result) == false){
            Debug.Log("me tu choi mon");
            return false;
        }
        addedAmount.Clear();
        return true;
    } //assign the dish which is just maked to the oven

    public bool StartCookingBatch()
    {
        if (currentBatch == null || oven == null)
        {
            return false;
        }
        if (oven.StartBatch(currentBatch) == false){
            return false;
        }
        currentBatch = null;
        return true;
    }
}
