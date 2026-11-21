using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class AcceptedOrderCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text remainText;
    [SerializeField] private TMP_Text addressText;
    [SerializeField] private TMP_Text deadlineText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image statusBadge;
    [SerializeField] private Button cookButton;
    public void SetOrder(Order order, string address, string deadline, string statusLabel, Color statusColor, bool canCook, Action cookAction)
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
        if (remainText != null)
        {
            remainText.text = "remaining: " + order.RemainingCount;
        }
        if (addressText != null)
        {
            addressText.text = address.ToLowerInvariant();
        }
        if (deadlineText != null)
        {
            deadlineText.text = deadline;
        }
        if (statusText != null)
        {
            statusText.text = statusLabel;
        }
        if (statusBadge != null)
        {
            statusBadge.color = statusColor;
        }
        if (cookButton != null)
        {
            cookButton.interactable = canCook;
            SetButtonText(canCook, statusLabel);
            cookButton.onClick.RemoveAllListeners();
            if (cookAction != null && canCook)
            {
                cookButton.onClick.AddListener(delegate { cookAction(); });
            }
        }
    }
    private void FindReferencesIfNeeded()
    {
        if (titleText == null)
        {
            titleText = FindText("TitleText");
        }
        if (remainText == null)
        {
            remainText = FindText("Remain");
        }
        if (remainText == null)
        {
            remainText = FindText("QuantityText");
        }
        if (addressText == null)
        {
            addressText = FindText("AddressText");
        }
        if (deadlineText == null)
        {
            deadlineText = FindText("DeadlineText");
        }
        if (statusText == null)
        {
            statusText = FindText("StatusText");
        }
        if (statusBadge == null)
        {
            Transform badge = FindChild("StatusBadge");
            if (badge != null)
            {
                statusBadge = badge.GetComponent<Image>();
            }
        }
        if (cookButton == null)
        {
            Transform button = FindChild("AcceptButton");
            if (button == null)
            {
                button = FindChild("CookButton");
            }
            if (button != null)
            {
                cookButton = button.GetComponent<Button>();
            }
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
    private void SetButtonText(bool canCook, string statusLabel)
    {
        if (cookButton == null)
        {
            return;
        }
        TMP_Text buttonText = cookButton.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
        {
            if (canCook)
            {
                buttonText.text = "cook";
            }
            else
            {
                buttonText.text = statusLabel;
            }
        }
    }
}
