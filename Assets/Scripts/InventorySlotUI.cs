using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;

    public void UpdateSlot(InventorySlot slot)
    {
        if (slot != null && slot.item != null)
        {
            icon.sprite = slot.item.icon;
            icon.enabled = true;

            if (slot.amount > 1)
            {
                amountText.text = slot.amount.ToString();
                amountText.enabled = true;
            }
            else
            {
                amountText.enabled = false;
            }
        }
        else
        {
            ClearSlot();
        }
    }

    public void ClearSlot()
    {
        icon.sprite = null;
        icon.enabled = false;
        amountText.text = "";
        amountText.enabled = false;
    }
}