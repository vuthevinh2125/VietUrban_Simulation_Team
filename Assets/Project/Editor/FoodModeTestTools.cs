using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ test cho chế độ nấu ăn - giao hàng (map của Nam, scene FoodMap). Chỉ chạy trong Editor khi đang Play,
/// không sửa scene hay code của Nam: chỉ dời người chơi tới chỗ cần thử, bấm Stop là mọi thứ về như cũ.
///  - Tools > Food Delivery Test > Move Player To Kitchen: tới khu bếp (bàn nấu, bát nguyên liệu, kệ đóng gói).
///  - Tools > Food Delivery Test > Move Player To Market: tới khu chợ mua nguyên liệu.
///  - Tools > Food Delivery Test > Move Player To Start: về điểm xuất phát lúc vào map.
/// </summary>
public static class FoodModeTestTools
{
    private const string Menu = "Tools/Food Delivery Test/";
    private const string PlayerName = "PlayerCapsule";

    // Khoảng trống giữa bàn nấu (bát, đĩa ở z = 318.6) và kệ đóng gói (z = 316.2), quay mặt về phía bếp (+z).
    private static readonly Vector3 KitchenSpot = new Vector3(333.5f, 0.5f, 317.2f);
    private static Vector3? startSpot;

    [MenuItem(Menu + "Move Player To Kitchen")]
    private static void ToKitchen() => Teleport(KitchenSpot, 0f, "the kitchen");

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
        GameObject player = GameObject.Find(PlayerName);
        if (player == null) { Fail($"No '{PlayerName}' in the open scene. Enter Food Delivery Mode and start a game first."); return; }
        if (startSpot == null) startSpot = player.transform.position;

        // Đặt chân xuống sàn ngay dưới điểm đến: bắn tia từ cao 2 m (thấp hơn mái che / trần) xuống, tránh rơi xuyên sàn
        // hay đứng lên mái.
        if (snap && Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            spot = hit.point + Vector3.up * 0.1f;

        // CharacterController ghi đè vị trí nếu còn bật, nên tắt tạm khi dời.
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
        Debug.Log($"[FoodModeTest] Player moved to {label} at {spot:F1}.");
    }

    private static void Fail(string message) => EditorUtility.DisplayDialog("Food Delivery Test", message, "OK");
}
