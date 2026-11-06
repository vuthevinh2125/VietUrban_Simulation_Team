using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Test mức scene (chạy trong Test Runner, tab EditMode, nhưng mỗi test VÀO PLAY MODE với scene thật):
/// bổ sung cho các test hàm thuần - bắt lỗi chỉ xuất hiện khi các hệ thống chạy cùng nhau trong scene
/// (thiếu tham chiếu, lỗi khởi tạo, thứ tự Awake). Mọi Debug.LogError trong lúc chạy làm test thất bại
/// (mặc định của Unity Test Framework).
///
/// Viết dạng EditMode + EnterPlayMode vì dự án không dùng asmdef: test PlayMode thuần phải nằm trong
/// một asmdef riêng, mà asmdef thì không tham chiếu được code game (Assembly-CSharp).
///
/// File lưu thật của người chơi (save.json) được sao lưu trước khi test ghi và trả lại sau đó
/// (kể cả khi test lỗi giữa chừng: TearDown trả lại từ bản sao lưu trên đĩa).
/// </summary>
public class SceneSmokeTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private static string Backup => SaveManager.SavePath + ".scenetest-backup";
    private static string NoSaveMarker => SaveManager.SavePath + ".scenetest-none";

    private static void OpenGameScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == ScenePath) return;
        if (active.isDirty) Assert.Ignore("Scene đang mở có thay đổi chưa lưu - lưu lại rồi chạy test.");
        EditorSceneManager.OpenScene(ScenePath);
    }

    private static void BackupSave()
    {
        if (File.Exists(Backup) || File.Exists(NoSaveMarker)) return; // đã sao lưu (lần chạy trước bị ngắt)
        if (File.Exists(SaveManager.SavePath)) File.Copy(SaveManager.SavePath, Backup);
        else File.WriteAllText(NoSaveMarker, "");
    }

    private static void RestoreSave()
    {
        if (File.Exists(Backup))
        {
            File.Copy(Backup, SaveManager.SavePath, true);
            File.Delete(Backup);
        }
        else if (File.Exists(NoSaveMarker))
        {
            if (File.Exists(SaveManager.SavePath)) File.Delete(SaveManager.SavePath);
            File.Delete(NoSaveMarker);
        }
    }

    [TearDown]
    public void TearDown() => RestoreSave();

    private static IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    [UnityTest]
    public IEnumerator Scene_Starts_WithCoreSystems()
    {
        OpenGameScene();
        yield return new EnterPlayMode();
        yield return Frames(10);

        Assert.IsNotNull(GameManager.Instance, "GameManager");
        Assert.IsNotNull(EconomyManager.Instance, "EconomyManager");
        Assert.IsNotNull(SaveManager.Instance, "SaveManager");
        Assert.IsNotNull(MarketManager.Instance, "MarketManager");
        Assert.IsNotNull(MenuSystem.Instance, "MenuSystem");
        Assert.IsTrue(MenuSystem.Instance.IsMainMenuOpen, "Game phải mở bằng menu chính");
        Assert.Greater(CheckoutCounter.All.Count, 0, "Scene phải có ít nhất một quầy thu ngân");
        Assert.IsNotNull(MarketManager.Instance.deliveryPoint, "MarketManager chưa gán Delivery Point");

        ShopCatalog cat = Object.FindAnyObjectByType<ShopCatalog>(FindObjectsInactive.Include);
        Assert.IsNotNull(cat, "ShopCatalog");
        Assert.Greater(cat.items.Count, 0, "Danh mục hàng trống");
        foreach (ItemData item in cat.items)
            if (item != null) Assert.IsNotNull(item.cratePrefab, $"'{item.itemName}' chưa gán Crate Prefab");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator Order_TakesMoney_AndDeliversBoxes()
    {
        OpenGameScene();
        yield return new EnterPlayMode();
        yield return Frames(10);

        ShopCatalog cat = Object.FindAnyObjectByType<ShopCatalog>(FindObjectsInactive.Include);
        ItemData item = cat.items.Find(i => i != null && i.cratePrefab != null);
        Assert.IsNotNull(item, "Không có mặt hàng nào đặt được");

        EconomyManager eco = EconomyManager.Instance;
        MarketManager market = MarketManager.Instance;
        eco.AddIncome(1000f, MoneyCategory.Other, "Scene test");
        int boxesBefore = CountBoxes(item);
        float moneyBefore = eco.Money;

        market.ClearCart();
        market.AddToCart(item, 2);
        float total = market.CartTotal;
        Assert.AreEqual(item.BoxPrice * 2f, total, 0.01f, "Tổng giỏ = 2 x giá thùng");
        market.Checkout();
        yield return null;

        Assert.AreEqual(moneyBefore - total, eco.Money, 0.01f, "Tiền phải giảm đúng bằng tổng giỏ");
        Assert.AreEqual(boxesBefore + 2, CountBoxes(item), "Phải có thêm đúng 2 thùng hàng");
        Assert.AreEqual(0, market.CartBoxCount, "Giỏ phải trống sau khi đặt");

        // Không đủ tiền: không trừ tiền, không sinh thùng
        eco.LoadState(1f, GameManager.Instance.currentDay);
        market.AddToCart(item, 1);
        int boxesMid = CountBoxes(item);
        market.Checkout();
        yield return null;
        Assert.AreEqual(1f, eco.Money, 0.001f, "Thiếu tiền thì không được trừ");
        Assert.AreEqual(boxesMid, CountBoxes(item), "Thiếu tiền thì không được giao hàng");
        market.ClearCart();

        yield return new ExitPlayMode();
    }

    private static int CountBoxes(ItemData item)
    {
        int n = 0;
        foreach (Interactable box in StockManager.Boxes)
            if (box != null && box.itemData == item) n++;
        return n;
    }

    [UnityTest]
    public IEnumerator Save_ThenLoad_RestoresProgress()
    {
        OpenGameScene();
        yield return new EnterPlayMode();
        yield return Frames(10);
        BackupSave();

        GameManager gm = GameManager.Instance;
        EconomyManager eco = EconomyManager.Instance;
        eco.LoadState(1234.5f, 7);
        gm.LoadDay(7);
        SaveManager.Instance.Save();
        Assert.IsTrue(File.Exists(SaveManager.SavePath), "Không ghi được file lưu");

        eco.LoadState(10f, 1);
        gm.LoadDay(1);
        SaveManager.Instance.Load();
        yield return null;

        Assert.AreEqual(1234.5f, eco.Money, 0.01f, "Tiền sau khi nạp");
        Assert.AreEqual(7, gm.currentDay, "Ngày sau khi nạp");

        RestoreSave();
        yield return new ExitPlayMode();
    }

    // ---------- T11: mua xe (FR13) ----------
    [UnityTest]
    public IEnumerator Vehicle_Purchase_ChecksLevelAndMoney()
    {
        OpenGameScene();
        yield return new EnterPlayMode();
        yield return Frames(10);

        VehicleManager.Entry e = System.Array.Find(VehicleManager.Catalog, v => !v.starter && v.level > 1 && v.price > 0f && VehicleManager.Exists(v.id));
        Assert.IsNotNull(e, "Cần một xe bán được trong scene (không phải xe có sẵn, có yêu cầu cấp độ)");
        EconomyManager eco = EconomyManager.Instance;
        int day = GameManager.Instance.currentDay;

        StoreProgress.Load(e.level - 1, 0);
        eco.LoadState(e.price + 100f, day);
        Assert.AreEqual(VehicleManager.BuyResult.LevelTooLow, VehicleManager.CanBuy(e), "Chưa đủ cấp thì khoá");
        Assert.IsFalse(VehicleManager.Buy(e), "Chưa đủ cấp thì không mua được");

        StoreProgress.Load(e.level, 0);
        eco.LoadState(e.price - 1f, day);
        Assert.AreEqual(VehicleManager.BuyResult.NotEnoughMoney, VehicleManager.CanBuy(e), "Thiếu tiền");
        Assert.IsFalse(VehicleManager.Buy(e));
        Assert.AreEqual(e.price - 1f, eco.Money, 0.01f, "Mua hỏng thì không trừ tiền");

        eco.LoadState(e.price + 100f, day);
        Assert.IsTrue(VehicleManager.Buy(e), "Đủ cấp, đủ tiền thì mua được");
        Assert.AreEqual(100f, eco.Money, 0.01f, "Tiền giảm đúng giá xe");
        Assert.IsTrue(VehicleManager.Owns(e.id));
        Assert.AreEqual(VehicleManager.BuyResult.AlreadyOwned, VehicleManager.CanBuy(e), "Đã có thì không mua lại");
        GameObject vehicle = VehicleManager.FindObject(e.id);
        Assert.IsNotNull(vehicle, "Xe phải có trong scene");
        Assert.IsTrue(vehicle.activeInHierarchy, "Xe mua xong phải hiện ra trước cửa hàng");

        yield return new ExitPlayMode();
    }

    // ---------- T14: chốt ngày, sang ngày mới, hết tiền (FR5) ----------
    [UnityTest]
    public IEnumerator DayEnd_ChargesRent_AndGameOverWhenBroke()
    {
        OpenGameScene();
        yield return new EnterPlayMode();
        yield return Frames(10);
        BackupSave(); // sang ngày mới thì game tự lưu

        GameManager gm = GameManager.Instance;
        EconomyManager eco = EconomyManager.Instance;
        int day = gm.currentDay;
        eco.LoadState(500f, day);
        float rent = eco.CurrentRent;
        Assert.Greater(rent, 0f, "Tiền thuê mỗi ngày phải dương");

        DailySummary summary = eco.CloseDay();
        Assert.AreEqual(500f - rent, eco.Money, 0.01f, "Chốt ngày phải trừ tiền thuê");
        Assert.AreEqual(rent, summary.Get(MoneyCategory.Rent), 0.01f, "Báo cáo ngày ghi tiền thuê");
        Assert.AreEqual(500f - rent, summary.closingBalance, 0.01f, "Số dư cuối ngày");

        // StartNextDay là hàm riêng do nút Continue của báo cáo ngày gọi -> gọi qua reflection
        var next = typeof(GameManager).GetMethod("StartNextDay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(next, "GameManager.StartNextDay");
        next.Invoke(gm, null);
        yield return null;
        Assert.AreEqual(day + 1, gm.currentDay, "Còn tiền thì sang ngày mới");
        Assert.IsFalse(gm.IsGameOver);

        eco.LoadState(-10f, gm.currentDay);
        next.Invoke(gm, null);
        yield return null;
        Assert.IsTrue(gm.IsGameOver, "Tiền âm khi sang ngày mới thì Game Over");
        Assert.AreEqual(day + 1, gm.currentDay, "Game Over thì không sang ngày");
        Assert.AreEqual(0f, Time.timeScale, "Game Over dừng thời gian");

        Time.timeScale = 1f;
        RestoreSave();
        yield return new ExitPlayMode();
    }
}
