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

        hand = new GameObject("Hand").transform;
        hand.SetParent(transform, false);
        hand.localPosition = new Vector3(0f, 1.0f, 0.45f);
    }

    public bool debugLogs = true;
    private float nextIdleNotice;

    IEnumerator Start()
    {
        yield return null;
        if (debugLogs) Debug.Log($"[Restocker] {name} bắt đầu làm việc tại {transform.position:F1} (onNavMesh={agent.isOnNavMesh}).", this);
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
            if (Time.time >= nextIdleNotice)
            {
                nextIdleNotice = Time.time + 20f;
                SpeechBubble.Show(transform, "No boxes to restock", Color.white, 2.1f, 3f);
                if (debugLogs) Debug.Log($"[Restocker] {name}: không có việc. Cần thùng còn hàng của mặt hàng ĐANG BÀY trên kệ (kệ chưa đầy).", this);
            }
            yield break;
        }
        myBox = box; myShelf = shelf;
        if (debugLogs) Debug.Log($"[Restocker] {name}: lấy thùng {box.DisplayName} ({box.productCount}) tại {box.transform.position:F1} -> kệ {shelf.name} ({shelf.Count}/{shelf.Capacity}).", this);
        claimedBoxes.Add(box);
        claimedShelves.Add(shelf);

        // 1) Đi tới thùng
        yield return MoveTo(box.transform.position, 1.1f);
        if (box == null || box.IsHeld || Flat(transform.position, box.transform.position) > 1.6f)
        {
            if (box != null) skipUntil[box] = Time.time + 30f; // không tới được, tạm bỏ qua thùng này
            if (debugLogs) Debug.LogWarning($"[Restocker] {name}: không tới được thùng (đứng ở {transform.position:F1}). Bỏ qua thùng này 30 giây.", this);
            yield break;
        }
        box.OnPickedUp(hand);
        carrying = box;

        // 2) Mang tới kệ
        Vector3 stand = StandPoint(shelf);
        yield return MoveTo(stand, 0.5f);
        if (shelf == null) { DropCarried(); yield break; }
        Face(shelf.transform.position);

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

        // 4) Thùng rỗng thì vứt, còn hàng thì đặt xuống
        if (box != null && box.IsEmpty) { carrying = null; box.Discard(); }
        else DropCarried();
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

    /// <summary>Thùng gần nhất có kệ (đang bán đúng mặt hàng đó, chưa đầy, chưa ai nhận) để xếp.</summary>
    private bool FindJob(out Interactable bestBox, out Shelf bestShelf)
    {
        bestBox = null; bestShelf = null;
        float bestDist = float.MaxValue;
        foreach (Interactable box in StockManager.Boxes)
        {
            if (box == null || box.IsHeld || box.IsEmpty || box.itemData == null || claimedBoxes.Contains(box)) continue;
            if (skipUntil.TryGetValue(box, out float until) && Time.time < until) continue;

            Shelf target = null;
            float bestFill = 1f;
            foreach (Shelf shelf in StockManager.Shelves)
            {
                if (shelf == null || shelf.IsEmpty || shelf.CurrentItem != box.itemData || claimedShelves.Contains(shelf)) continue;
                if (!shelf.CanPlace(box.itemData, out _)) continue;
                float fill = shelf.Count / (float)Mathf.Max(1, shelf.Capacity);
                if (fill < bestFill) { bestFill = fill; target = shelf; }
            }
            if (target == null) continue;

            float d = (box.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestBox = box; bestShelf = target; }
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
