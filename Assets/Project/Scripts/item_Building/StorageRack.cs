using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Kệ kho chứa thùng (giống Supermarket Simulator): cất thùng hàng gọn lên kệ thay vì để bừa dưới sàn.
///  - Cầm thùng, nhìn vào kệ kho, bấm chuột trái để cất thùng vào ô trống.
///  - Tay không nhìn vào thùng trên kệ, bấm E để lấy ra.
///  - Nhân viên xếp kệ lấy hàng từ đây, rảnh thì mang thùng nằm dưới sàn cất lên kệ kho.
/// Mua trong tab Furnitures ("Storage Rack").
/// </summary>
public class StorageRack : MonoBehaviour
{
    public static readonly List<StorageRack> All = new List<StorageRack>();

    [Tooltip("Ô đặt thùng (công cụ Tools > Store Setup > 9 tự tạo)")]
    public Transform[] boxSlots;

    private Interactable[] stored;

    public int Capacity => boxSlots != null ? boxSlots.Length : 0;
    public int FreeSlots { get { Cleanup(); int n = 0; for (int i = 0; i < stored.Length; i++) if (stored[i] == null) n++; return n; } }
    public bool HasFreeSlot => FreeSlots > 0;

    void Awake() => stored = new Interactable[Capacity];
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    /// <summary>Cất thùng vào ô trống gần điểm nhắm nhất. Trả về false nếu kệ đầy.</summary>
    public bool TryStore(Interactable box, Vector3 aimPoint)
    {
        if (box == null) return false;
        Cleanup();
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < stored.Length; i++)
        {
            if (stored[i] != null || boxSlots[i] == null) continue;
            float d = (boxSlots[i].position - aimPoint).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best < 0) return false;
        Place(box, best);
        return true;
    }

    public bool Contains(Interactable box) { Cleanup(); return System.Array.IndexOf(stored, box) >= 0; }

    /// <summary>Thùng đang cất gần điểm nhắm nhất (để lấy ra khi tia nhắm trúng khung kệ).</summary>
    public Interactable NearestStored(Vector3 point, float maxDistance = 0.8f)
    {
        Cleanup();
        Interactable best = null;
        float bestD = maxDistance * maxDistance;
        foreach (Interactable b in stored)
        {
            if (b == null) continue;
            float d = (b.transform.position - point).sqrMagnitude;
            if (d < bestD) { bestD = d; best = b; }
        }
        return best;
    }

    /// <summary>Thùng nằm sẵn đúng chỗ ô trống (sau khi nạp file lưu) thì nhận lại vào kệ.</summary>
    public void AdoptNearbyBoxes(IEnumerable<Interactable> boxes)
    {
        Cleanup();
        foreach (Interactable b in boxes)
        {
            if (b == null || b.IsHeld || Contains(b)) continue;
            for (int i = 0; i < stored.Length; i++)
            {
                if (stored[i] != null || boxSlots[i] == null) continue;
                Vector3 d = b.transform.position - boxSlots[i].position; d.y = 0f;
                if (d.magnitude < 0.35f && Mathf.Abs(b.transform.position.y - boxSlots[i].position.y) < 0.6f) { Place(b, i); break; }
            }
        }
    }

    /// <summary>Điểm đứng trước kệ để cất / lấy thùng.</summary>
    public Vector3 StandPoint()
    {
        Vector3 c = transform.position + transform.forward * 0.9f;
        return NavMesh.SamplePosition(c, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : c;
    }

    private void Place(Interactable box, int index)
    {
        if (box.IsHeld) box.OnDropped();
        Rigidbody rb = box.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        Transform slot = boxSlots[index];
        Bounds lb = ProductFit.LocalBounds(box.transform);
        float lift = -lb.min.y * box.transform.lossyScale.y + 0.01f;
        box.transform.SetPositionAndRotation(slot.position + Vector3.up * lift, slot.rotation);
        stored[index] = box;
        StockManager.NotifyChanged();
    }

    /// <summary>Bỏ ô của thùng đã bị lấy đi / bị huỷ / bị cầm lên.</summary>
    private void Cleanup()
    {
        if (stored == null || stored.Length != Capacity) stored = new Interactable[Capacity];
        for (int i = 0; i < stored.Length; i++)
        {
            Interactable b = stored[i];
            if (b == null) continue;
            if (b.IsHeld || (b.transform.position - boxSlots[i].position).sqrMagnitude > 1f) stored[i] = null;
        }
    }

    /// <summary>Kệ kho còn chỗ, gần vị trí này nhất.</summary>
    public static StorageRack FindFree(Vector3 from)
    {
        StorageRack best = null;
        float bestD = float.MaxValue;
        foreach (StorageRack r in All)
        {
            if (r == null || !r.HasFreeSlot) continue;
            float d = (r.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = r; }
        }
        return best;
    }

    public static bool IsStored(Interactable box)
    {
        foreach (StorageRack r in All) if (r != null && r.Contains(box)) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();
}
