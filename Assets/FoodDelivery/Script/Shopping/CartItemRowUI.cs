using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class CartItemRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameQuantityText; 
    [SerializeField] private TMP_Text lineTotalText;  
    [SerializeField] private Button removeButton;
    private Action onRemoveAction;

    public void Setup(CartEntry entry, Action onRemove)
    {
        string unit = "";
        if (entry.item.sellType == SellType.Gram)
        {
            unit = "g";
        }
        nameQuantityText.text = $"{entry.item.itemName} x {entry.amount:0}{unit}";
        lineTotalText.text = $"Subtotal: {entry.LineTotal:N0}";
        onRemoveAction = onRemove;
        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(HandleRemoveClicked);
    }
    private void HandleRemoveClicked()
    {
        if (onRemoveAction != null)
        {
            onRemoveAction();
        }
    }
}