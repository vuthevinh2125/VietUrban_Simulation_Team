using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ test cho chế độ nấu ăn - giao hàng (map của Nam, scene FoodMap). Chỉ chạy trong Editor khi đang Play,
/// không sửa scene hay code của Nam: chỉ dời người chơi tới chỗ cần thử, bấm Stop là mọi thứ về như cũ.
///  - Tools > Food Delivery Test > Move Player To Kitchen: tới khu bếp (bàn nấu, bát nguyên liệu, kệ đóng gói).
///  - Tools > Food Delivery Test > Move Player To Market: tới khu chợ mua nguyên liệu.
///  - Tools > Food Delivery Test > Move Player To Start: về chỗ đứng trước lần dời đầu tiên.
/// Cách dời người chơi dùng chung với FoodModeSpawn (vào map là đứng trước bếp).
/// </summary>
public static class FoodModeTestTools
{
    private const string Menu = "Tools/Food Delivery Test/";
    private static Vector3? startSpot;

    [MenuItem(Menu + "Move Player To Kitchen")]
    private static void ToKitchen() => Teleport(FoodModeSpawn.KitchenSpot, FoodModeSpawn.KitchenYaw, "the kitchen");

    [MenuItem(Menu + "Move Player To Market")]
    private static void ToMarket()
    {
        GameObject market = GameObject.Find("Vegetable_market_with_objects");
        if (market == null) { Fail("Could not find 'Vegetable_market_with_objects' in this scene."); return; }
        Transform m = market.transform;
        Vector3 spot = m.position + m.forward * 4f; // đứng trước sạp, quay mặt vào sạp
        Teleport(spot, Quaternion.LookRotation(-m.forward).eulerAngles.y, "the market");
    }

    [MenuItem(Menu + "Move Player To Start")]
    private static void ToStart()
    {
        if (startSpot == null) { Fail("Use one of the other two items first; the start point is remembered then."); return; }
        Teleport(startSpot.Value, 0f, "the start point", snap: false);
    }

    [MenuItem(Menu + "Move Player To Kitchen", true)]
    [MenuItem(Menu + "Move Player To Market", true)]
    [MenuItem(Menu + "Move Player To Start", true)]
    private static bool CanUse() => Application.isPlaying;

    private static void Teleport(Vector3 spot, float yaw, string label, bool snap = true)
    {
        GameObject player = GameObject.Find(FoodModeSpawn.PlayerName);
        if (player == null) { Fail($"No '{FoodModeSpawn.PlayerName}' in the open scene. Enter Food Delivery Mode and start a game first."); return; }
        if (startSpot == null) startSpot = player.transform.position;
        FoodModeSpawn.MovePlayer(spot, yaw, snap);
        Debug.Log($"[FoodModeTest] Player moved to {label}.");
    }

    private static void Fail(string message) => EditorUtility.DisplayDialog("Food Delivery Test", message, "OK");
}
