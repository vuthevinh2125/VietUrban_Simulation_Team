using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class TrackingOrderCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text ingredientText;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Image progressFill;
    [SerializeField] private Button redoButton;
    [SerializeField] private Button createBoxButton;
    public void SetOrder(Order order, float progress, Action redoAction, Action createBoxAction)
    {
        if (order == null)
        {
            return;
        }
        FindReferencesIfNeeded();
        string recipeName = "unknown dish";
        if (order.Recipe != null)
        {
            recipeName = order.Recipe.RecipeName.ToLowerInvariant();
        }
        if (titleText != null)
        {
            titleText.text = "order #" + order.OrderId + " - " + recipeName;
        }
        if (quantityText != null)
        {
            quantityText.text = order.ProducedCount + "/" + order.RequiredCount;
        }
        if (ingredientText != null)
        {
            ingredientText.text = BuildIngredientText(order);
        }
        if (progressFill != null)
        {
            progressFill.fillAmount = Mathf.Clamp01(progress);
        }
        if (progressLabel != null)
        {
            progressLabel.text = GetProgressLabel(order, progress);
        }
        bool canRedo = order.OrderStatus == Order.OrderState.WaitingConfirm && !order.IsComplete;
        bool canCreateBox = order.OrderStatus == Order.OrderState.WaitingConfirm || order.OrderStatus == Order.OrderState.Closed && !order.IsCreated;
        SetButton(redoButton, canRedo, "make up", redoAction);
        SetButton(createBoxButton, canCreateBox, "create box", createBoxAction);
    }
    private void FindReferencesIfNeeded()
    {
        if (titleText == null)
        {
            titleText = FindText("TitleText");
        }
        if (quantityText == null)
        {
            quantityText = FindText("QuantityText");
        }
        if (ingredientText == null)
        {
            ingredientText = FindText("IngredientText");
        }
        if (progressLabel == null)
        {
            progressLabel = FindText("ProgressLabel");
        }
        if (progressFill == null)
        {
            Transform progress = FindChild("ProgressBar");
            if (progress != null)
            {
                Transform fill = progress.Find("Fill");
                if (fill != null)
                {
                    progressFill = fill.GetComponent<Image>();
                }
            }
        }
        if (redoButton == null)
        {
            Transform button = FindChild("RedoButton");
            if (button != null)
            {
                redoButton = button.GetComponent<Button>();
            }
        }
        if (createBoxButton == null)
        {
            Transform button = FindChild("CreateBoxButton");
            if (button != null)
            {
                createBoxButton = button.GetComponent<Button>();
            }
        }
    }
    private string BuildIngredientText(Order order)
    {
        if (order.Recipe == null || order.Recipe.Ingredients == null || order.Recipe.Ingredients.Count == 0)
        {
            return "ingredients unavailable";
        }
        string text = "ingredients: ";
        for (int index = 0; index < order.Recipe.Ingredients.Count; index++)
        {
            RecipeIngredient ingredient = order.Recipe.Ingredients[index];
            if (ingredient == null || ingredient.ingredient == null)
            {
                continue;
            }
            if (text != "ingredients: ")
            {
                text += ", ";
            }
            text += ingredient.ingredient.itemName.ToLowerInvariant() + " x" + ingredient.amount;
        }
        return text;
    }
    private string GetProgressLabel(Order order, float progress)
    {
        if (order.OrderStatus == Order.OrderState.Cooking || order.OrderStatus == Order.OrderState.Making)
        {
            return "cooking " + Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%";
        }
        if (order.OrderStatus == Order.OrderState.WaitingConfirm)
        {
            return "cooking complete";
        }
        if (order.OrderStatus == Order.OrderState.Closed && order.IsCreated)
        {
            return "box created";
        }
        return "ready to cook";
    }
    private void SetButton(Button button, bool visible, string label, Action action)
    {
        if (button == null)
        {
            return;
        }
        button.gameObject.SetActive(visible);
        button.interactable = visible;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
        }
        button.onClick.RemoveAllListeners();
        if (visible && action != null)
        {
            button.onClick.AddListener(delegate { action(); });
        }
    }
    private TMP_Text FindText(string childName)
    {
        Transform child = FindChild(childName);
        if (child == null)
        {
            return null;
        }
        return child.GetComponent<TMP_Text>();
    }
    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                return child;
            }
        }
        return null;
    }
}
