using System.Collections.Generic;
using UnityEngine;
[System.Serializable]

public class RecipeIngredient
{
    public ItemData ingredient;
    public float amount;
}
[CreateAssetMenu(fileName = "RecipeData", menuName = "Scriptable Objects/RecipeData")]
public class RecipeData : ScriptableObject
{
    [SerializeField] private string recipeName;
    [SerializeField] private ItemData dishItem;
    [SerializeField] private List<RecipeIngredient> ingredients;
    [SerializeField] private float cookTime;
    [SerializeField] private GameObject completedDish;
    [SerializeField] private GameObject incompletedDish;

    public string RecipeName
    {
        get {
            return recipeName;
        }
    }
    public ItemData DishItem
    {
        get
        {
            return dishItem;
        }
    }
    public List<RecipeIngredient> Ingredients
    {
        get {  return ingredients; }
    }
    public float CookTime
    {
        get { return cookTime; }
    }
    public GameObject CompletedDish
    {
        get
        {
            return completedDish;
        }
    }
    public GameObject IncompletedDish
    {
        get
        {
            return incompletedDish;
        }
    }

}
