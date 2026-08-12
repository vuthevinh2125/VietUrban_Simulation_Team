using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string promptMessage = "Press E to pick up";

    [Header("Giá trị của vật phẩm")]
    public int itemValue = 500; 

    public void Interact()
    {
        Debug.Log("Đã nhặt vật phẩm!");

        GameManager.Instance.currentMoney += itemValue;
        GameManager.Instance.UpdateUI();

        Destroy(gameObject);
    }
}