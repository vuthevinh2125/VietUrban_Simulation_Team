using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cảm biến dùng chung cho cửa tự động (EntranceDoor, AutoDoor) - hoạt động như cảm biến cửa thật:
///  - Ai đứng ngay ngưỡng cửa (vùng hẹp "core") -> mở.
///  - Người chơi / khách trong vùng rộng "wide" -> chỉ mở khi đang ĐI VỀ PHÍA cửa.
///    Khách đứng yên xếp hàng gần cửa, người đi ngang qua hoặc đi ra xa sẽ không giữ cửa mở.
///  - Nhân viên / bảo vệ / NPC khác: chỉ tính trong vùng hẹp.
///  - Người đi bộ ngoài phố (PedestrianWalker): bỏ qua hoàn toàn.
/// Vận tốc tự tính từ vị trí giữa các lần quét nên dùng được cho mọi kiểu di chuyển.
/// </summary>
public static class DoorSensor
{
    private const float MinApproachSpeed = 0.35f; // m/s
    private const float ApproachDot = 0.35f;      // hướng đi phải lệch không quá ~70° so với hướng tới cửa

    private struct Track { public Vector3 pos; public float time; public Vector3 vel; }

    private static readonly List<Transform> playerParts = new List<Transform>();
    private static float nextPlayerFind;
    private static readonly List<NavMeshAgent> agents = new List<NavMeshAgent>();
    private static float nextAgentScan;
    private static readonly Dictionary<Transform, Track> tracks = new Dictionary<Transform, Track>();
    private static float nextCleanup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        playerParts.Clear(); nextPlayerFind = 0f;
        agents.Clear(); nextAgentScan = 0f;
        tracks.Clear(); nextCleanup = 0f;
    }

    /// <param name="wide">Vùng phát hiện người đang đi tới (người chơi, khách).</param>
    /// <param name="core">Vùng ngưỡng cửa: ai đứng đây cửa cũng mở.</param>
    public static bool AnyoneAt(Bounds wide, Bounds core)
    {
        Cleanup();
        Vector3 center = core.center;
        Bounds near = core;
        near.Expand(new Vector3(2.4f, 0f, 2.4f)); // trong khoảng ~1.2 m quanh ngưỡng: luôn mở

        foreach (Transform p in PlayerParts())
            if (p != null && Wants(p, wide, near, center)) return true;

        foreach (NPCShopper npc in NPCShopper.Active)
            if (npc != null && Wants(npc.transform, wide, near, center)) return true;

        if (Time.time >= nextAgentScan)
        {
            nextAgentScan = Time.time + 2f;
            agents.Clear();
            foreach (NavMeshAgent a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                if (a.GetComponent<NPCShopper>() == null && a.GetComponent<PedestrianWalker>() == null) agents.Add(a);
        }
        foreach (NavMeshAgent a in agents)
            if (a != null && a.isActiveAndEnabled && Inside(core, a.transform.position)) return true;
        return false;
    }

    private static bool Wants(Transform t, Bounds wide, Bounds near, Vector3 center)
    {
        Vector3 pos = t.position;
        Vector3 vel = Velocity(t);
        if (Inside(near, pos)) return true;
        if (!Inside(wide, pos)) return false;

        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        if (flat.magnitude < MinApproachSpeed) return false; // đứng yên (xếp hàng, đứng nhìn) không mở cửa
        Vector3 toDoor = center - pos; toDoor.y = 0f;
        if (toDoor.sqrMagnitude < 0.01f) return true;
        return Vector3.Dot(flat.normalized, toDoor.normalized) > ApproachDot;
    }

    /// <summary>Vận tốc ước lượng theo vị trí ở các lần quét trước (cập nhật tối đa ~10 lần/giây).</summary>
    private static Vector3 Velocity(Transform t)
    {
        float now = Time.time;
        if (!tracks.TryGetValue(t, out Track tr))
        {
            tracks[t] = new Track { pos = t.position, time = now, vel = Vector3.zero };
            return Vector3.zero;
        }
        float dt = now - tr.time;
        if (dt >= 0.1f)
        {
            Vector3 v = (t.position - tr.pos) / dt;
            tr.vel = dt > 1f ? v : Vector3.Lerp(tr.vel, v, 0.6f); // lâu không quét thì lấy luôn giá trị mới
            tr.pos = t.position;
            tr.time = now;
            tracks[t] = tr;
        }
        return tr.vel;
    }

    private static void Cleanup()
    {
        if (Time.time < nextCleanup) return;
        nextCleanup = Time.time + 10f;
        var dead = new List<Transform>();
        foreach (var kv in tracks) if (kv.Key == null || Time.time - kv.Value.time > 10f) dead.Add(kv.Key);
        foreach (Transform d in dead) tracks.Remove(d);
    }

    /// <summary>So theo mặt phẳng XZ, chiều cao nới rộng (vị trí có thể ở ngang hông hoặc dưới chân).</summary>
    private static bool Inside(Bounds b, Vector3 pos)
    {
        return pos.x >= b.min.x && pos.x <= b.max.x &&
               pos.z >= b.min.z && pos.z <= b.max.z &&
               pos.y >= b.min.y - 2f && pos.y <= b.max.y + 2f;
    }

    /// <summary>
    /// Các điểm đại diện cho người chơi đang đi bộ: object có PlayerController, Rigidbody / CharacterController của nó
    /// và camera chính nếu là con của người chơi. Đang lái xe (nhân vật bị tắt) thì danh sách rỗng -> cửa không mở theo xe.
    /// </summary>
    private static List<Transform> PlayerParts()
    {
        bool stale = playerParts.Count == 0 || playerParts.Exists(x => x == null);
        if (stale && Time.time >= nextPlayerFind)
        {
            nextPlayerFind = Time.time + 1f;
            playerParts.Clear();
            foreach (PlayerController pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                playerParts.Add(pc.transform);
                Rigidbody rb = pc.GetComponentInParent<Rigidbody>() ?? pc.GetComponentInChildren<Rigidbody>();
                if (rb != null && !playerParts.Contains(rb.transform)) playerParts.Add(rb.transform);
                CharacterController cc = pc.GetComponentInParent<CharacterController>() ?? pc.GetComponentInChildren<CharacterController>();
                if (cc != null && !playerParts.Contains(cc.transform)) playerParts.Add(cc.transform);
                Camera cam = pc.GetComponentInChildren<Camera>();
                if (cam != null && !playerParts.Contains(cam.transform)) playerParts.Add(cam.transform);
            }
        }
        active.Clear();
        foreach (Transform t in playerParts)
            if (t != null && t.gameObject.activeInHierarchy) active.Add(t);
        return active;
    }

    private static readonly List<Transform> active = new List<Transform>();
}
