//using UnityEngine;

//public class CookingDebug : MonoBehaviour
//{
//    // Start is called once before the first execution of Update after the MonoBehaviour is created
//    [SerializeField] private CookingSession session;

//    public void OnEnable()
//    {
//        session.OnProgress += HandleProgress;
//        session.OnDishCooked += HandleDishCooked;
//    }
//    public void OnDisable()
//    {
//        session.OnDishCooked -= HandleDishCooked;
//        session.OnProgress -= HandleProgress;
//    }

//    public void HandleProgress(float progress)
//    {
//        Debug.Log(progress);
//    }
//    public void HandleDishCooked(RecipeData recipe, CookingResult result)
//    {
//        Debug.Log($"{recipe.RecipeName}: dung = {result.isCorrect}, thieu {result.ingredientMissingCount}, thua {result.ingredientExtraCount}");
//    }
//    void Start()
//    {
        
//    }

//    // Update is called once per frame
//    void Update()
//    {
        
//    }
//}
