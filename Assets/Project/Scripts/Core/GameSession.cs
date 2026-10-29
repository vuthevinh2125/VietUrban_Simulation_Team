using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chơi lại từ đầu / về menu chính: đưa mọi dữ liệu "static" (cấp độ, giấy phép, giá bán, giá thị trường,
/// quầy đã mở, khoá giao diện...) về mặc định rồi nạp lại scene. Không làm vậy thì số liệu cũ còn sót lại sau khi nạp scene.
/// </summary>
public static class GameSession
{
    public static void ReloadScene()
    {
        StoreProgress.Load(1, 0);
        LicenseManager.LoadOwned(null);
        PriceManager.ClearCustomPrices();
        MarketPrices.Import(null, new System.Collections.Generic.Dictionary<string, ItemData>());
        LockedCheckout.LoadUnlocked(null);
        UIState.CloseAll();
        PlayerController.LookLocked = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
