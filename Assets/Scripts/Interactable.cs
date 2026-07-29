using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string promptMessage = "Press E to pick up";

    [Header("Giá trị của vật phẩm")]
    public int itemValue = 500; // Có thể chỉnh số tiền tùy ý trên Unity cho từng cục hàng

    public void Interact()
    {
        Debug.Log("Đã nhặt vật phẩm!");

        // 1. Cộng tiền thông qua GameManager
        GameManager.Instance.currentMoney += itemValue;

        // 2. Cập nhật lại giao diện UI
        GameManager.Instance.UpdateUI();

        // 3. Xóa vật phẩm khỏi map
        Destroy(gameObject);
    }
}