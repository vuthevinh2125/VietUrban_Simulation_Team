using UnityEngine;

public class IngredientStation : InteractableObject
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private float capacity;
    [SerializeField] private float currentAmount;
    [SerializeField] private float portionPerClick;
    [SerializeField] private GameObject fullVisual;
    [SerializeField] private GameObject emptyVisual;
    [SerializeField] private Transform ingredientVisualRoot;
    [SerializeField] private ItemData ingredient;
    [SerializeField] private CookingSession cookingSession;
    [SerializeField] private string stationId;
    private GameObject ingredientVisualInstance;
    private string savedStationId;

    public ItemData Ingredient
    {
        get { return ingredient; }
    }

    public void FillFromInventory()
    {
        Fill(capacity);
    }

    public bool CanFillFromInventory(ItemData item)
    {
        if (item == null || ingredient == null || item != ingredient || currentAmount >= capacity)
        {
            return false;
        }

        return GameSession.Current.GetAmount(ingredient.itemId) > 0f;
    }

    private void Awake()
    {
        savedStationId = stationId;
        if (string.IsNullOrEmpty(savedStationId))
        {
            savedStationId = CreateStationId();
        }
        float savedAmount;
        if (GameSession.Current.TryGetStationStock(savedStationId, out savedAmount))
        {
            currentAmount = Mathf.Clamp(savedAmount, 0f, capacity);
        }
        else
        {
            currentAmount = Mathf.Clamp(currentAmount, 0f, capacity);
        }
    }

    private string CreateStationId()
    {
        string path = gameObject.scene.name;
        Transform current = transform;
        while (current != null)
        {
            path = current.GetSiblingIndex() + "_" + current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private void Start()
    {
        Transform stockVisual = transform.Find("StockVisual");
        if (stockVisual != null)
        {
            fullVisual = stockVisual.gameObject;
        }
        CreateIngredientVisual();
        UpdateVisual();
    }

    private void CreateIngredientVisual()
    {
        if (ingredient == null || ingredient.worldPrefab == null || ingredientVisualRoot == null)
        {
            return;
        }

        ingredientVisualInstance = Instantiate(ingredient.worldPrefab, ingredientVisualRoot);
        ingredientVisualInstance.transform.localPosition = Vector3.zero;
        ingredientVisualInstance.transform.localRotation = Quaternion.identity;
    }

    public override void Interact()
    {
        if (cookingSession == null || cookingSession.CanAccept == false)
        {
            Debug.Log("start an order first");
            return;
        }
        if (cookingSession.HasIngredientOnPlate(ingredient))
        {
            return;
        }
        float taken = Take();
        if (taken <= 0f)
        {
            Debug.Log("ingredient station is empty");
            return;
        }
        if (cookingSession != null)
        {
            cookingSession.AddedIngredient(ingredient, taken);
        }
        Debug.Log("ingredient added to plate data");
    }

    public override string GetInteractionPrompt()
    {
        if (cookingSession == null || cookingSession.CanAccept == false)
        {
            return "start an order first";
        }
        if (cookingSession.HasIngredientOnPlate(ingredient))
        {
            return "pick one per batch";
        }
        return "click to add ingredient";
    }
    public float Take()
    {
        if (currentAmount <= 0)
        {
            return 0f;
        }
        float realTaken = Mathf.Min(portionPerClick, currentAmount);
        currentAmount -= realTaken;
        SaveCurrentStock();
        UpdateVisual();
        return realTaken;
    }
    public void Fill(float amount)
    {
        float space = capacity - currentAmount;
        float haveInInventory = GameSession.Current.GetAmount(ingredient.itemId); //tranh loi go itemId khong khop thi kho tra ve 0
        float toFill = Mathf.Min(amount, space, haveInInventory);
        if (toFill <= 0)
        {
            return;
        }
        if (GameSession.Current.TryRemove(ingredient.itemId, toFill))
        {
            currentAmount += toFill;
            SaveCurrentStock();
        }
        UpdateVisual();
    }

    private void SaveCurrentStock()
    {
        GameSession.Current.SetStationStock(savedStationId, currentAmount);
        SaveSystem.SaveGame(GameSession.Current);
    }

    public void UpdateVisual()
    {
        bool hasStock = currentAmount > 0f;
        bool hasIngredientModel = ingredientVisualInstance != null;
        if (fullVisual != null)
        {
            fullVisual.SetActive(hasStock && !hasIngredientModel);
        }
        if (emptyVisual != null)
        {
            emptyVisual.SetActive(!hasStock);
        }
        if (ingredientVisualInstance != null)
        {
            ingredientVisualInstance.SetActive(hasStock);
        }
    }
}
