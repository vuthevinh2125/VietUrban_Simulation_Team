using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cửa ra vào nhiều cánh (cửa đôi): tự mở khi người chơi hoặc khách (NavMeshAgent) tới gần, đóng khi không còn ai.
/// Mỗi cánh xoay quanh bản lề (pivot) của nó một góc openAngles[i] độ quanh trục Y.
/// Góc âm/dương sai chiều thì đổi dấu trong Inspector.
/// </summary>
public class EntranceDoor : MonoBehaviour
{
    [Header("Leaves")]
    public Transform[] leaves;
    [Tooltip("Góc mở của từng cánh (độ, quanh trục Y). Mở sai chiều thì đổi dấu.")]
    public float[] openAngles;

    [Header("Detection")]
    [Tooltip("Vùng phát hiện mở rộng ra quanh các cánh cửa (mét)")]
    public Vector3 detectPadding = new Vector3(1.5f, 1f, 2f);
    public float checkInterval = 0.15f;
    public float openSpeed = 3f;

    private Quaternion[] closedRot;
    private Quaternion[] openRot;
    private Bounds area;
    private bool isOpen;
    private float nextCheck;
    private float t; // 0 = đóng, 1 = mở
    private readonly Collider[] hits = new Collider[32];

    void Start()
    {
        if (leaves == null) leaves = new Transform[0];
        closedRot = new Quaternion[leaves.Length];
        openRot = new Quaternion[leaves.Length];

        bool hasArea = false;
        for (int i = 0; i < leaves.Length; i++)
        {
            if (leaves[i] == null) continue;
            closedRot[i] = leaves[i].localRotation;
            float angle = openAngles != null && i < openAngles.Length ? openAngles[i] : 90f;
            if (leaves[i].name.ToLower().Contains("glass")) angle = 0f; // tấm kính cố định, không xoay
            openRot[i] = closedRot[i] * Quaternion.Euler(0f, angle, 0f);

            foreach (Renderer r in leaves[i].GetComponentsInChildren<Renderer>())
            {
                if (!hasArea) { area = r.bounds; hasArea = true; } else area.Encapsulate(r.bounds);
            }
        }
        if (!hasArea) area = new Bounds(transform.position, Vector3.one);
        area.Expand(detectPadding * 2f);
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            isOpen = IsAnyoneNear();
        }

        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.deltaTime * openSpeed);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < leaves.Length; i++)
            if (leaves[i] != null) leaves[i].localRotation = Quaternion.Slerp(closedRot[i], openRot[i], eased);
    }

    private bool IsAnyoneNear()
    {
        int count = Physics.OverlapBoxNonAlloc(area.center, area.extents, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;
            if (c.CompareTag("Player") || c.transform.root.CompareTag("Player")) return true;
            if (c.GetComponentInParent<NavMeshAgent>() != null) return true;
        }
        foreach (NPCShopper npc in NPCShopper.Active)
            if (npc != null && area.Contains(npc.transform.position)) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.4f);
        Bounds b = area.size == Vector3.zero ? new Bounds(transform.position, Vector3.one) : area;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
