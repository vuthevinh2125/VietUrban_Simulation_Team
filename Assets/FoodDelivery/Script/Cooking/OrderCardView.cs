using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text idText;
    [SerializeField] private TMP_Text dishText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text customerText;
    [SerializeField] private TMP_Text addressText;
    [SerializeField] private TMP_Text payText;
    [SerializeField] private TMP_Text deadlineText;
    [SerializeField] private Button acceptButton;

    private void Awake()
    {
        FindReferencesIfNeeded();
    }

    public void SetOrder(Order order, Action acceptAction)
    {
        if (order == null)
        {
            return;
        }

        FindReferencesIfNeeded();

        if (idText != null)
        {
            idText.text = "Đơn #" + order.OrderId;
        }

        if (dishText != null)
        {
            if (order.Recipe != null)
            {
                dishText.text = order.Recipe.RecipeName;
            }
            else
            {
                dishText.text = "Chưa xác định món";
            }
        }

        if (countText != null)
        {
            countText.text = order.RequiredCount + " phần";
        }

        if (statusText != null)
        {
            statusText.text = "Đang chờ xác nhận";
        }

        if (customerText != null)
        {
            customerText.text = order.CustomerName;
        }

        if (addressText != null)
        {
            addressText.text = order.DeliveryAddress;
        }

        if (payText != null)
        {
            payText.text = order.Total.ToString("N0") + " đ";
        }

        if (deadlineText != null)
        {
            if (order.HasDeliveryDeadline)
            {
                deadlineText.text = "Hạn giao: " + order.ExpectedDeliveryAt.ToString("HH:mm");
            }
            else
            {
                deadlineText.text = string.Empty;
            }
        }

        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            if (acceptAction != null)
            {
                acceptButton.onClick.AddListener(delegate { acceptAction(); });
            }
        }
    }

    private void FindReferencesIfNeeded()
    {
        if (idText == null)
        {
            idText = FindText("IdText");
        }
        if (dishText == null)
        {
            dishText = FindText("DishText");
        }
        if (countText == null)
        {
            countText = FindText("CountText");
        }
        if (statusText == null)
        {
            statusText = FindText("StatusText");
        }
        if (customerText == null)
        {
            customerText = FindText("CustomerText");
        }
        if (addressText == null)
        {
            addressText = FindText("AddressText");
        }
        if (payText == null)
        {
            payText = FindText("PayText");
        }
        if (deadlineText == null)
        {
            deadlineText = FindText("DeadlineText");
        }
        if (acceptButton == null)
        {
            Transform buttonTransform = FindChild("AcceptButton");
            if (buttonTransform != null)
            {
                acceptButton = buttonTransform.GetComponent<Button>();
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
}
