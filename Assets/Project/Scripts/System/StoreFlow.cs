using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Luồng đi một chiều của khách ở sảnh trước siêu thị, để khách không chen qua khu thu ngân:
///  - VÀO: từ vỉa hè -> cửa phía cổng vào (cửa gần các cổng xoay có mũi tên) -> qua cổng vào -> bắt đầu mua.
///  - RA (sau khi trả tiền / bỏ về): đi tiếp qua làn thu ngân -> cửa còn lại (cửa ra) -> ra vỉa hè bên ngoài
///    -> đi theo đường ngoài về điểm biến mất.
/// Trước đây khách vào và ra cùng một cửa sát quầy thu ngân, nên người mới vào, người đang xếp hàng và người
/// trả tiền xong (đẩy xe) đứng chắn nhau ở lối đi hẹp cạnh quầy.
/// Các điểm được tính lúc chạy game từ chính bố cục scene (không cần đặt tay):
///  - cửa ra vào = các EntranceDoor; hướng "ra phố" = từ tâm toà nhà Supermarket_full tới cửa;
///  - cửa vào = cửa gần điểm sinh khách nhất, cửa ra = cửa còn lại;
///  - lối vào = đi thẳng từ cửa vào qua các cổng xoay có mũi tên (tên "Door_o8w2gy", trong 3 m hai bên cửa) tới hết hàng cổng.
/// Không tìm được đủ (scene khác) thì trả về false và khách đi như cũ.
/// </summary>
public static class StoreFlow
{
    private static bool initialized, valid;
    private static Vector3[] entry, exit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { initialized = false; valid = false; entry = null; exit = null; }

    /// <summary>Các điểm khách đi qua khi vào cửa hàng (theo thứ tự), trước khi đi tới kệ.</summary>
    public static bool EntryRoute(out Vector3[] points) { Init(); points = entry; return valid; }

    /// <summary>Các điểm khách đi qua khi ra về (theo thứ tự), trước điểm biến mất.</summary>
    public static bool ExitRoute(out Vector3[] points) { Init(); points = exit; return valid; }

    private static void Init()
    {
        if (initialized) return;
        initialized = true;

        EntranceDoor[] doors = Object.FindObjectsByType<EntranceDoor>(FindObjectsSortMode.None);
        GameObject store = GameObject.Find("Supermarket_full");
        if (doors.Length < 2 || store == null)
        {
            Debug.LogWarning($"[StoreFlow] Không đủ dữ liệu (cửa: {doors.Length}, toà nhà: {store != null}) - khách đi như cũ.");
            return;
        }

        // Hướng ra phố và trục ngang của mặt tiền
        Renderer[] rs = store.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Debug.LogWarning("[StoreFlow] Toà nhà không có renderer - khách đi như cũ."); return; }
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Vector3 doorMid = Vector3.zero;
        foreach (EntranceDoor d in doors) doorMid += d.transform.position;
        doorMid /= doors.Length;
        Vector3 outward = doorMid - b.center; outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) { Debug.LogWarning("[StoreFlow] Không xác định được hướng ra phố - khách đi như cũ."); return; }
        outward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, outward);

        float Depth(Vector3 p) => Vector3.Dot(doorMid - p, outward); // càng lớn càng vào sâu trong cửa hàng
        float Lateral(Vector3 p) => Vector3.Dot(p - doorMid, side);

        // Cửa vào = cửa gần điểm sinh khách nhất (khách đi bộ tới từ đó); cửa ra = cửa còn lại xa nhất
        CustomerSpawner spawner = Object.FindFirstObjectByType<CustomerSpawner>();
        Vector3 spawn = spawner != null && spawner.spawnPoint != null ? spawner.spawnPoint.position : doorMid;
        EntranceDoor inDoor = doors[0], outDoor = doors[0];
        foreach (EntranceDoor d in doors)
        {
            if (Flat(d.transform.position, spawn) < Flat(inDoor.transform.position, spawn)) inDoor = d;
        }
        foreach (EntranceDoor d in doors)
            if (Flat(d.transform.position, inDoor.transform.position) > Flat(outDoor.transform.position, inDoor.transform.position)) outDoor = d;
        if (inDoor == outDoor) { Debug.LogWarning("[StoreFlow] Chỉ có một cửa, giữ đường đi cũ."); return; }

        // Cổng xoay có mũi tên chỉ vào trong ngay sau cửa vào: lối vào nằm thẳng từ cửa vào đi vào trong,
        // tới hết hàng cổng xa nhất (trong khoảng 3 m hai bên cửa vào)
        float inLat = Lateral(inDoor.transform.position);
        float gateDepth = 0f;
        int gateCount = 0;
        foreach (Transform t in store.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Door_o8w2gy")) continue;
            if (Mathf.Abs(Lateral(t.position) - inLat) > 3f) continue;
            float dep = Depth(t.position);
            if (dep < 0.5f || dep > 12f) continue;
            gateDepth = Mathf.Max(gateDepth, dep);
            gateCount++;
        }
        if (gateCount == 0) gateDepth = 6f;
        Vector3 gate = inDoor.transform.position - outward * gateDepth;

        Vector3 inPos = inDoor.transform.position, outPos = outDoor.transform.position;
        entry = new[]
        {
            Snap(inPos + outward * 2.5f),   // vỉa hè trước cửa vào
            Snap(inPos - outward * 1.5f),   // vừa qua cửa vào
            PastGates(gate, outward),       // đi thẳng qua các cổng có mũi tên, vào trong cửa hàng
        };
        exit = new[]
        {
            Snap(outPos - outward * 1.5f),  // trước cửa ra (sau làn thu ngân)
            Snap(outPos + outward * 2.5f),  // vỉa hè trước cửa ra, rồi đi đường ngoài
        };
        valid = true;
        Debug.Log($"[StoreFlow] Cửa vào {inDoor.name} {inPos:F1} -> qua {gateCount} cổng tới {entry[2]:F1}; cửa ra {outDoor.name} {outPos:F1}");
    }

    private static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    /// <summary>
    /// Điểm ngay sau hàng cổng, thẳng hàng với cửa vào. Lần đầu lấy 1.5 m sau cổng rồi bám NavMesh bán kính 2.5 m nên bị
    /// kéo lệch 2.8 m sang làn thu ngân (sau cổng là quầy dịch vụ) -> nay thử gần dần và chỉ bám trong 0.8 m.
    /// </summary>
    private static Vector3 PastGates(Vector3 gate, Vector3 outward)
    {
        foreach (float d in new[] { 1.0f, 0.6f, 0.3f, 0f })
            if (NavMesh.SamplePosition(gate - outward * d, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) return hit.position;
        return Snap(gate);
    }

    private static Vector3 Snap(Vector3 p) =>
        NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) ? hit.position : p;
}
