using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nhân viên xếp kệ: tìm thùng hàng còn hàng (không ai cầm) mà trong cửa hàng có kệ đang bán đúng mặt hàng đó
/// và chưa đầy -> đi tới nhặt thùng -> mang tới kệ -> xếp từng món -> thùng rỗng thì vứt, còn hàng thì đặt xuống.
/// Không tự mở kệ mới cho mặt hàng mới (người chơi quyết định bày gì ở đâu, giống game gốc).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Restocker : MonoBehaviour
{
    public float placeInterval = 0.35f;
    public float moveTimeout = 30f;

    private static readonly HashSet<Interactable> claimedBoxes = new HashSet<Interactable>();
    private static readonly HashSet<Shelf> claimedShelves = new HashSet<Shelf>();
    private readonly Dictionary<Interactable, float> skipUntil = new Dictionary<Interactable, float>();

    private NavMeshAgent agent;
    private Animator animator;
    private bool hasSpeed;
    private Transform hand;
    private Interactable carrying;
    private Interactable myBox;
    private Shelf myShelf;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = true;
        agent.areaMask |= AreaBit("Walkable") | AreaBit("Sidewalks") | AreaBit("Jump");
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == "Speed") hasSpeed = true;

        agent.avoidancePriority = StaffManager.StaffAvoidancePriority; // nhường đường cho khách
        idleIndex = Random.Range(0, 100);

        hand = new GameObject("Hand").transform;
        hand.SetParent(transform, false);
        hand.localPosition = new Vector3(0f, 1.0f, 0.45f);
    }

    public bool debugLogs = false;
    private float nextIdleNotice;
    private int idleIndex;

    /// <summary>Hết việc: về chỗ nghỉ của nhân viên (kệ kho / chỗ nhận hàng) thay vì đứng giữa lối đi.</summary>
    private IEnumerator GoIdle()
    {
        if (!StaffManager.TryIdleSpot(transform.position, idleIndex, out Vector3 spot)) yield break;
        if (Flat(transform.position, spot) < 1.5f) yield break;
        yield return MoveTo(spot, 0.6f);
    }

    IEnumerator Start()
    {
        yield return null;
        if (debugLogs && GameDebug.Verbose) Debug.Log($"[Restocker] {name} bắt đầu làm việc tại {transform.position:F1} (onNavMesh={agent.isOnNavMesh}).", this);
        while (true)
        {
            yield return Work();
            Release();
            yield return new WaitForSeconds(1.5f);
        }
    }

    void Update()
    {
        if (hasSpeed) animator.SetFloat("Speed", agent.enabled ? agent.velocity.magnitude : 0f);
    }

    void OnDestroy() => Release();

    /// <summary>Cho nghỉ việc: thả thùng đang cầm xuống đất trước khi biến mất.</summary>
    public void PrepareToLeave()
    {
        StopAllCoroutines();
        DropCarried();
        Release();
    }

    // ================= Công việc =================
    private IEnumerator Work()
    {
        if (!FindJob(out Interactable box, out Shelf shelf))
        {
            // Không có kệ nào cần xếp: mang thùng nằm dưới sàn cất lên kệ kho (nếu có kệ kho còn chỗ)
            if (FindStoreJob(out Interactable loose, out StorageRack rackJob))
            {
                myBox = loose;
                claimedBoxes.Add(loose);
                yield return MoveTo(loose.transform.position, 1.1f);
                if (loose == null || loose.IsHeld || Flat(transform.position, loose.transform.position) > 1.6f)
                {
                    if (loose != null) skipUntil[loose] = Time.time + 30f;
                    yield break;
                }
                loose.OnPickedUp(hand);
                carrying = loose;
                yield return StoreCarried(rackJob);
                yield break;
            }
            if (Time.time >= nextIdleNotice)
            {
                nextIdleNotice = Time.time + 20f;
                SpeechBubble.Show(transform, "No boxes to restock", Color.white, 2.1f, 3f);
                if (debugLogs && GameDebug.Verbose) Debug.Log($"[Restocker] {name}: không có việc. Cần thùng còn hàng của mặt hàng ĐANG BÀY trên kệ (kệ chưa đầy).", this);
            }
            yield return GoIdle();
            yield break;
        }
        myBox = box; myShelf = shelf;
        if (debugLogs && GameDebug.Verbose) Debug.Log($"[Restocker] {name}: lấy thùng {box.DisplayName} ({box.productCount}) tại {box.transform.position:F1} -> kệ {shelf.name} ({shelf.Count}/{shelf.Capacity}).", this);
        claimedBoxes.Add(box);
        claimedShelves.Add(shelf);

        // 1) Đi tới thùng
        yield return MoveTo(box.transform.position, 1.1f);
        if (box == null || box.IsHeld || Flat(transform.position, box.transform.position) > 1.6f)
        {
            if (box != null) skipUntil[box] = Time.time + 30f; // không tới được, tạm bỏ qua thùng này
            if (debugLogs && GameDebug.Verbose) Debug.LogWarning($"[Restocker] {name}: không tới được thùng (đứng ở {transform.position:F1}). Bỏ qua thùng này 30 giây.", this);
            yield break;
        }
        box.OnPickedUp(hand);
        carrying = box;

        // 2) Mang tới kệ
        Vector3 stand = StandPoint(shelf);
        yield return MoveTo(stand, 0.5f);
        if (shelf == null) { DropCarried(); yield break; }
        Face(shelf.transform.position);
        // Có hàng hết hạn thì vứt đi trước khi xếp mới
        if (shelf.SpoiledCount > 0)
        {
            int thrown = FreshnessManager.ThrowAway(shelf);
            if (thrown > 0) SpeechBubble.Show(transform, $"Threw away {thrown} expired", new Color(1f, 0.7f, 0.5f), 2.1f, 2.5f);
            yield return new WaitForSeconds(0.6f);
        }
        else if (box != null && box.itemData != null && DemandModel.Current(box.itemData) >= 1.5f)
            SpeechBubble.Show(transform, $"Restocking {box.itemData.itemName} (HOT)", new Color(1f, 0.85f, 0.3f), 2.1f, 2.5f);

        // 3) Xếp từng món
        while (box != null && !box.IsEmpty && shelf.CanPlace(box.itemData, out _))
        {
            GameObject prefab = box.ProductPrefab;
            if (prefab == null) break;
            GameObject product = Instantiate(prefab);
            if (!shelf.TryAddProduct(product, box.transform, box.itemData)) { Destroy(product); break; }
            box.RemoveOne();
            yield return new WaitForSeconds(placeInterval);
        }

        // 4) Thùng rỗng thì vứt, còn hàng thì cất lên kệ kho (nếu có) hoặc đặt xuống
        if (box != null && box.IsEmpty) { carrying = null; box.Discard(); }
        else
        {
            StorageRack rack = StorageRack.FindFree(transform.position);
            if (rack != null) yield return StoreCarried(rack);
            else DropCarried();
        }
    }

    /// <summary>Mang thùng đang cầm tới kệ kho và cất vào ô trống (kệ đầy thì đặt xuống).</summary>
    private IEnumerator StoreCarried(StorageRack rack)
    {
        if (carrying == null) yield break;
        if (rack == null || !rack.HasFreeSlot) { DropCarried(); yield break; }
        yield return MoveTo(rack.StandPoint(transform.position), 0.6f);
        if (rack == null || carrying == null) { DropCarried(); yield break; }
        Face(rack.transform.position);
        Interactable b = carrying;
        carrying = null;
        if (!rack.TryStore(b, transform.position + Vector3.up)) { carrying = b; DropCarried(); }
        else if (debugLogs && GameDebug.Verbose) Debug.Log($"[Restocker] {name}: cất thùng {b.DisplayName} lên kệ kho.", this);
    }

    /// <summary>Thùng còn hàng đang nằm dưới sàn (không trên kệ kho) + kệ kho còn chỗ.</summary>
    private bool FindStoreJob(out Interactable best, out StorageRack rack)
    {
        best = null; rack = null;
        if (StorageRack.All.Count == 0) return false;
        float bestD = float.MaxValue;
        foreach (Interactable box in StockManager.Boxes)
        {
            if (box == null || box.IsHeld || box.IsEmpty || box.itemData == null || claimedBoxes.Contains(box)) continue;
            if (skipUntil.TryGetValue(box, out float until) && Time.time < until) continue;
            if (StorageRack.IsStored(box)) continue;
            float d = (box.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = box; }
        }
        if (best == null) return false;
        rack = StorageRack.FindFree(best.transform.position);
        return rack != null;
    }

    private void DropCarried()
    {
        if (carrying == null) return;
        Interactable b = carrying;
        carrying = null;
        b.OnDropped();
        b.transform.position = transform.position + transform.forward * 0.7f + Vector3.up * 0.4f;
        b.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    private void Release()
    {
        if (myBox != null) claimedBoxes.Remove(myBox);
        if (myShelf != null) claimedShelves.Remove(myShelf);
        claimedBoxes.RemoveWhere(b => b == null);
        claimedShelves.RemoveWhere(s => s == null);
        myBox = null; myShelf = null;
    }

    /// <summary>
    /// Chọn việc xếp kệ theo ĐỘ CẤP BÁCH thay vì thùng gần nhất (trước 1/11):
    ///   điểm = (1 - tỉ lệ còn hàng bán được)^1.5 x nhu cầu lúc này (DemandModel) / (1 + quãng đường / 20 m)
    ///  - Kệ đã bán HẾT vẫn được xếp lại đúng món cũ (Shelf.LastItem); trước đây kệ trống bị bỏ quên.
    ///  - Hàng hết hạn trên kệ không tính là còn hàng (nhân viên vứt đi rồi xếp mới).
    ///  - Hàng tươi chỉ xếp thêm khi kệ còn dưới 60% (bày ít, bán nhanh, tránh hỏng).
    /// </summary>
    private bool FindJob(out Interactable bestBox, out Shelf bestShelf)
    {
        bestBox = null; bestShelf = null;
        float bestScore = 0f;
        Vector3 me = transform.position;
        foreach (Interactable box in StockManager.Boxes)
        {
            if (box == null || box.IsHeld || box.IsEmpty || box.itemData == null || claimedBoxes.Contains(box)) continue;
            if (skipUntil.TryGetValue(box, out float until) && Time.time < until) continue;
            ItemData item = box.itemData;
            float demand = DemandModel.Current(item);
            bool perishable = Freshness.ShelfLifeDays(item) > 0f;
            Vector3 boxPos = box.transform.position;
            float toBox = Flat(me, boxPos);

            foreach (Shelf shelf in StockManager.Shelves)
            {
                if (shelf == null || claimedShelves.Contains(shelf) || shelf.LastItem != item) continue;
                if (shelf.Capacity == 0) continue;
                float fill = shelf.SellableCount / (float)shelf.Capacity;
                if (fill >= 1f) continue;
                if (perishable && fill >= 0.6f) continue;
                if (shelf.SpoiledCount == 0 && !shelf.CanPlace(item, out _)) continue;
                float urgency = Mathf.Pow(1f - fill, 1.5f) * demand;
                float score = urgency / (1f + (toBox + Flat(boxPos, shelf.transform.position)) / 20f);
                if (score > bestScore) { bestScore = score; bestBox = box; bestShelf = shelf; }
            }
        }
        return bestBox != null;
    }

    // ================= Di chuyển =================
    private IEnumerator MoveTo(Vector3 target, float stopDistance)
    {
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit h, 4f, NavMesh.AllAreas)) agent.Warp(h.position);
            else yield break;
        }
        Vector3 dest = NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : target;
        agent.isStopped = false;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(dest);
        float t = 0f;
        while (t < moveTimeout)
        {
            t += Time.deltaTime;
            if (!agent.pathPending && !agent.isOnOffMeshLink && Flat(transform.position, dest) <= stopDistance) break;
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid) break;
            yield return null;
        }
        agent.isStopped = true;
        agent.ResetPath();
    }

    private static Vector3 StandPoint(Shelf shelf)
    {
        Vector3 center = Vector3.zero;
        int n = 0;
        if (shelf.slots != null)
            foreach (Transform s in shelf.slots)
                if (s != null) { center += s.position; n++; }
        center = n > 0 ? center / n : shelf.transform.position;
        center.y = shelf.transform.position.y;
        return NavMesh.SamplePosition(center, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : center;
    }

    private void Face(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
    }

    private static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    private static int AreaBit(string areaName)
    {
        int area = NavMesh.GetAreaFromName(areaName);
        return area >= 0 ? 1 << area : 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        claimedBoxes.Clear();
        claimedShelves.Clear();
    }
}
