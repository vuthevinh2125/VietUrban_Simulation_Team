using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chế độ nấu ăn - giao hàng (map của Nam, scene FoodMap): mỗi khi vào map, đưa người chơi tới ngay trước khu bếp
/// thay vì điểm xuất phát gốc cách bếp ~100 m. Làm từ phía code siêu thị, không sửa scene hay code của Nam:
/// muốn bỏ thì xoá file này là người chơi lại xuất phát ở chỗ Nam đặt.
/// </summary>
public static class FoodModeSpawn
{
    public const string SceneName = "FoodMap";
    public const string PlayerName = "PlayerCapsule";

    // Khoảng trống giữa bàn nấu (bát nguyên liệu, đĩa ở z = 318.6) và kệ đóng gói (z = 316.2), quay mặt về phía bếp (+z).
    public static readonly Vector3 KitchenSpot = new Vector3(333.5f, 0.5f, 317.2f);
    public const float KitchenYaw = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == SceneName) Schedule(); // bấm Play thẳng trong scene FoodMap
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneName) Schedule();
    }

    // Đợi một khung hình cho player (Starter Assets) khởi tạo xong rồi mới dời.
    private static void Schedule() => new GameObject("FoodModeSpawn").AddComponent<FoodModeSpawnRunner>();

    /// <summary>Dời người chơi tới điểm cho trước, đặt chân lên sàn ngay dưới. Trả về false nếu không có người chơi.</summary>
    public static bool MovePlayer(Vector3 spot, float yaw, bool snapToFloor = true)
    {
        GameObject player = GameObject.Find(PlayerName);
        if (player == null) return false;

        // Bắn tia từ cao 2 m (thấp hơn mái che / trần) xuống để đứng đúng mặt sàn, không rơi xuyên sàn hay đứng lên mái.
        if (snapToFloor && Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            spot = hit.point + Vector3.up * 0.1f;

        // CharacterController ghi đè vị trí nếu còn bật, nên tắt tạm khi dời.
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
        return true;
    }
}

/// <summary>Chạy một lần sau khi FoodMap nạp xong: dời người chơi tới bếp, khoá chuột rồi tự huỷ.</summary>
public class FoodModeSpawnRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        if (!FoodModeSpawn.MovePlayer(FoodModeSpawn.KitchenSpot, FoodModeSpawn.KitchenYaw))
            Debug.LogWarning($"[FoodModeSpawn] No '{FoodModeSpawn.PlayerName}' in {FoodModeSpawn.SceneName}; the player keeps Nam's start point.");

        // Starter Assets chỉ khoá chuột khi cửa sổ game vừa được focus (OnApplicationFocus). Vào FoodMap từ menu
        // (chuột đang hiện) thì không có lần focus nào nên chuột không bị khoá, xoay camera không được.
        // Khoá chuột vào giữa màn hình như khi chơi; điện thoại và menu tạm dừng của Nam tự mở khoá rồi trả lại trạng thái này.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Destroy(gameObject);
    }
}
