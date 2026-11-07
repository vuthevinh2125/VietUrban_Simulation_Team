using UnityEngine;

/// <summary>
/// Thùng hàng nằm yên trên mặt đất thì "đứng im" (Rigidbody kinematic): NPC không xô đổ được.
/// Trước: thùng luôn là vật lý động, khách / người đi bộ (collider kinematic, di chuyển bằng NavMeshAgent) đi qua đống thùng
/// ở chỗ giao hàng là xô đổ cả chồng ra vỉa hè (người dùng thấy khi đặt nhiều hàng 7/11).
/// Sau: thùng đứng yên 0.6 s -> kinematic -> NPC không đẩy được (NPC đi xuyên qua đống thùng).
/// Đã thử thêm NavMeshObstacle để NPC đi vòng: đống thùng lớn chắn hết vỉa hè thì khách đứng chết trước đống thùng
/// (tránh va chạm cục bộ không tìm được lối) -> bỏ.
/// Người chơi / nhân viên nhấc thùng lên (Interactable.OnPickedUp) thì các thùng chồng phía trên được "đánh thức" để rơi
/// xuống như thường; thả ra thì thùng rơi rồi lại đứng im.
/// StockManager.Register tự gắn component này vào mọi thùng hàng.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BoxRest : MonoBehaviour
{
    private const float SettleSeconds = 0.6f, StillSpeed = 0.05f;
    private static readonly Collider[] hits = new Collider[24];

    private Rigidbody rb;
    private Interactable box;
    private Collider col;
    private float still;
    private bool resting;
    private Bounds restBounds; // chỗ thùng nằm lúc đứng im (lúc được nhấc lên thì thùng đã ở trên tay)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        box = GetComponent<Interactable>();
        col = GetComponent<Collider>();
    }

    void FixedUpdate()
    {
        if (box != null && box.IsHeld)
        {
            if (resting) { resting = false; WakeAbove(); }
            still = 0f;
            return;
        }
        if (resting) return;
        if (rb.isKinematic) { still = 0f; return; } // trên kệ kho / đang do script khác giữ
        bool calm = rb.linearVelocity.sqrMagnitude < StillSpeed * StillSpeed && rb.angularVelocity.sqrMagnitude < 0.05f;
        still = calm ? still + Time.fixedDeltaTime : 0f;
        if (still >= SettleSeconds) Rest();
    }

    private void Rest()
    {
        resting = true;
        if (col != null) restBounds = col.bounds;
        rb.isKinematic = true;
    }

    /// <summary>Cho thùng rơi / bị đẩy lại như vật lý thường.</summary>
    public void Wake()
    {
        if (!resting) return;
        resting = false;
        still = 0f;
        rb.isKinematic = false;
        rb.WakeUp();
    }

    /// <summary>Thùng vừa được nhấc lên: đánh thức các thùng đang chồng phía trên nó.</summary>
    private void WakeAbove()
    {
        if (col == null) return;
        Bounds b = restBounds;
        Vector3 center = b.center + Vector3.up * (b.extents.y + 1f);
        int n = Physics.OverlapBoxNonAlloc(center, new Vector3(b.extents.x, 1f, b.extents.z), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            BoxRest other = hits[i] != null ? hits[i].GetComponentInParent<BoxRest>() : null;
            if (other != null && other != this) other.Wake();
        }
    }
}
