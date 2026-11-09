using UnityEngine;

/// <summary>
/// Cho biết scene đang mở có phải game siêu thị không (scene có GameManager).
/// Game có 2 chế độ: siêu thị (scene này) và nấu ăn - giao hàng của Nam (Assets/FoodDelivery). Các hệ thống tự tạo
/// của siêu thị (menu, âm thanh, HUD, lưu game...) chỉ được sinh ra khi điều này đúng, để không xuất hiện trong map của Nam.
/// </summary>
public static class SupermarketScene
{
    public static bool IsActive => Object.FindAnyObjectByType<GameManager>() != null;
}
