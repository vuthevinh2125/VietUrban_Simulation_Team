using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject highlight;
    [SerializeField] private TMP_Text amountText;
    private Action clicked;
    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }
    public void SetItem(ItemData item, float amount, Action onClick)
    {
        clicked = onClick;
        if (button != null)
        {
            button.interactable = item != null;
        }
        if (iconImage != null)
        {
            iconImage.sprite = null;
            if (item != null)
            {
                iconImage.sprite = item.icon;
            }
            iconImage.enabled = item != null && item.icon != null;
        }
        if (amountText != null)
        {
            amountText.text = FormatAmount(item, amount);
            amountText.enabled = item != null;
        }
        SetHighlighted(false);
    }
    public void SetOrder(Order order, Action onClick)
    {
        clicked = onClick;
        if (button != null)
        {
            button.interactable = order != null;
        }
        ItemData dish = null;
        if (order != null && order.Recipe != null)
        {
            dish = order.Recipe.DishItem;
        }
        if (iconImage != null)
        {
            iconImage.sprite = null;
            if (dish != null)
            {
                iconImage.sprite = dish.icon;
            }
            iconImage.enabled = dish != null && dish.icon != null;
        }
        if (amountText != null)
        {
            amountText.text = string.Empty;
            if (order != null)
            {
                amountText.text = "x" + order.ProducedCount;
            }
            amountText.enabled = order != null;
        }
        SetHighlighted(false);
    }
    public void SetEmpty()
    {
        clicked = null;
        if (button != null)
        {
            button.interactable = false;
        }
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (amountText != null)
        {
            amountText.text = string.Empty;
            amountText.enabled = false;
        }
        SetHighlighted(false);
    }
    public void SetHighlighted(bool isHighlighted)
    {
        if (highlight != null)
        {
            highlight.SetActive(isHighlighted);
            Image highlightImage = highlight.GetComponent<Image>();
            if (highlightImage != null)
            {
                highlightImage.enabled = isHighlighted;
            }
        }
    }
    private void HandleClick()
    {
        if (clicked != null)
        {
            clicked();
        }
    }
    private string FormatAmount(ItemData item, float amount)
    {
        if (item != null && item.sellType == SellType.Gram)
        {
            if (amount >= 1000f)
            {
                return (amount / 1000f).ToString("0.##") + "kg";
            }
            return amount.ToString("0.##") + "g";
        }
        return "x" + amount.ToString("0.##");
    }
}
