using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nhân viên lau nhà: tìm vết bẩn gần nhất chưa ai nhận (DirtManager) -> đi tới -> cầm chổi quét cho tới khi sạch.
/// Không có vết bẩn thì về chỗ nghỉ của nhân viên (StaffManager.TryIdleSpot) để khỏi đứng chắn lối đi của khách.
/// Quét bằng nhân viên không cộng XP (XP quét nhà chỉ dành cho người chơi).
///
/// Model: một model khách được dành riêng (StaffManager.JanitorModel). Cây chổi không gắn cứng vào xương tay
/// (trục xương mỗi model một khác) mà mỗi frame được đặt nối từ bàn tay phải xuống sàn trước mặt -> tay chuyển động thì chổi đi theo.
/// Khi quét: chổi đưa qua lại và người hơi xoay theo (chưa có clip quét riêng; nếu Animator có tham số bool "Sweeping" thì dùng clip đó).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Janitor : MonoBehaviour
{
    public float sweepTime = 2.5f;   // giây để quét sạch một vết bẩn đầy
    public float moveTimeout = 30f;

    private static readonly HashSet<DirtSpot> claimed = new HashSet<DirtSpot>();
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int SweepingHash = Animator.StringToHash("Sweeping");

    private NavMeshAgent agent;
    private Animator animator;
    private bool hasSpeed, hasSweepAnim;
    private Transform hand;           // xương bàn tay phải (model Humanoid), không có thì ước lượng
    private Transform broom;
    private DirtSpot target;
    private int idleIndex;
    private float sweepPhase;
    private bool sweeping, chasingDirt;
    private Quaternion sweepBaseRot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => claimed.Clear();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = true;
        agent.areaMask |= AreaBit("Walkable") | AreaBit("Sidewalks") | AreaBit("Jump");
        agent.avoidancePriority = StaffManager.StaffAvoidancePriority; // nhường đường cho khách
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == "Speed") hasSpeed = true;
                if (p.name == "Sweeping" && p.type == AnimatorControllerParameterType.Bool) hasSweepAnim = true;
            }
            if (animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        idleIndex = Random.Range(0, 100);
        broom = BuildBroom(transform);
    }

    IEnumerator Start()
    {
        yield return null;
        while (true)
        {
            yield return Work();
            Release();
            yield return new WaitForSeconds(1f);
        }
    }

    void Update()
    {
        if (hasSpeed) animator.SetFloat(SpeedHash, agent.enabled ? agent.velocity.magnitude : 0f);
        if (hasSweepAnim) animator.SetBool(SweepingHash, sweeping);
    }

    /// <summary>Sau khi Animator đã đặt tư thế: nối chổi từ bàn tay xuống sàn.</summary>
    void LateUpdate()
    {
        if (broom == null) return;
        Vector3 fwd = transform.forward, right = transform.right, up = Vector3.up;
        Vector3 grip = hand != null ? hand.position : transform.position + up * 1.0f + right * 0.28f + fwd * 0.25f;
        float groundY = transform.position.y + 0.01f;

        Vector3 tip;
        if (sweeping)
        {
            sweepPhase += Time.deltaTime * (hasSweepAnim ? 0f : 7f);
            // Đầu chổi trên sàn, trước mặt; có clip quét thì đi theo tay, không có thì tự đưa qua lại
            Vector3 under = new Vector3(grip.x, groundY, grip.z);
            tip = under + fwd * 0.45f + (hasSweepAnim ? Vector3.zero : right * Mathf.Sin(sweepPhase) * 0.35f);
            if (!hasSweepAnim) transform.rotation = sweepBaseRot * Quaternion.Euler(0f, Mathf.Sin(sweepPhase) * 12f, 0f);
        }
        else
        {
            // Đi / đứng: chổi dựng gần thẳng đứng bên phải người, đầu chổi chạm đất
            tip = new Vector3(grip.x, groundY, grip.z) + right * 0.08f + fwd * 0.12f;
        }

        Vector3 dir = grip - tip;
        if (dir.sqrMagnitude < 0.01f) dir = up;
        dir.Normalize();
        Vector3 face = Vector3.ProjectOnPlane(fwd, dir);
        if (face.sqrMagnitude < 0.001f) face = Vector3.ProjectOnPlane(right, dir);
        broom.SetPositionAndRotation(tip, Quaternion.LookRotation(face, dir));
    }

    void OnDestroy() => Release();

    private void Release()
    {
        if (target != null) claimed.Remove(target);
        claimed.RemoveWhere(s => s == null);
        target = null;
        sweeping = false;
    }

    // ================= Công việc =================
    private IEnumerator Work()
    {
        target = FindDirt();
        if (target == null)
        {
            yield return GoIdle();
            yield return new WaitForSeconds(2f);
            yield break;
        }
        claimed.Add(target);

        chasingDirt = true;
        yield return MoveTo(target.transform.position, 0.6f);
        chasingDirt = false;
        if (target == null) yield break;                                              // người chơi đã quét mất
        if (Flat(transform.position, target.transform.position) > 1.5f) yield break; // không tới được

        Face(target.transform.position);
        sweepBaseRot = transform.rotation;
        sweeping = true;
        float nextSound = 0f;
        while (target != null)
        {
            if (Time.time >= nextSound) { AudioManager.PlayAt("sweep", transform.position, 0.35f); nextSound = Time.time + 0.6f; }
            if (target.Clean(Time.deltaTime / Mathf.Max(0.1f, sweepTime), false)) break;
            yield return null;
        }
        sweeping = false;
        transform.rotation = sweepBaseRot;
    }

    /// <summary>Vết bẩn gần nhất chưa có nhân viên khác nhận.</summary>
    private DirtSpot FindDirt()
    {
        DirtSpot best = null;
        float bestD = float.MaxValue;
        foreach (DirtSpot s in DirtManager.Spots)
        {
            if (s == null || claimed.Contains(s)) continue;
            float d = (s.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    private IEnumerator GoIdle()
    {
        if (!StaffManager.TryIdleSpot(transform.position, idleIndex, out Vector3 spot)) yield break;
        if (Flat(transform.position, spot) < 1.5f) yield break;
        yield return MoveTo(spot, 0.6f);
    }

    // ================= Di chuyển =================
    private IEnumerator MoveTo(Vector3 targetPos, float stopDistance)
    {
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit h, 4f, NavMesh.AllAreas)) agent.Warp(h.position);
            else yield break;
        }
        Vector3 dest = NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : targetPos;
        agent.isStopped = false;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(dest);
        float t = 0f;
        while (t < moveTimeout)
        {
            t += Time.deltaTime;
            if (chasingDirt && target == null) break; // vết bẩn đã bị người chơi quét mất
            if (!agent.pathPending && !agent.isOnOffMeshLink && Flat(transform.position, dest) <= stopDistance) break;
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid) break;
            yield return null;
        }
        agent.isStopped = true;
        agent.ResetPath();
    }

    private void Face(Vector3 point)
    {
        Vector3 d = point - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
    }

    private static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    private static int AreaBit(string areaName)
    {
        int a = NavMesh.GetAreaFromName(areaName);
        return a >= 0 ? 1 << a : 0;
    }

    // ================= Cây chổi =================
    /// <summary>
    /// Chổi đứng: gốc (0,0,0) là mép lông chổi chạm sàn, trục Y local chạy dọc cán lên tay cầm.
    /// Cán gỗ dài 1.25 m, đầu chổi đỏ, túm lông vàng xoè như chổi Việt Nam.
    /// </summary>
    private static Transform BuildBroom(Transform owner)
    {
        var root = new GameObject("Broom").transform;
        root.SetParent(owner, false);
        Color wood = new Color(0.62f, 0.42f, 0.22f), red = new Color(0.8f, 0.18f, 0.15f),
              straw = new Color(0.93f, 0.78f, 0.38f), strawDark = new Color(0.78f, 0.6f, 0.25f);

        const float bristle = 0.2f, handleLen = 1.25f;
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, bristle + 0.05f + handleLen * 0.5f, 0f), new Vector3(0.03f, handleLen * 0.5f, 0.03f), wood);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, bristle + 0.05f + handleLen, 0f), new Vector3(0.036f, 0.025f, 0.036f), red);
        Part(root, PrimitiveType.Cube, new Vector3(0f, bristle + 0.025f, 0f), new Vector3(0.3f, 0.05f, 0.07f), red);
        const int strands = 13;
        for (int i = 0; i < strands; i++)
        {
            float u = i / (float)(strands - 1) - 0.5f;
            float len = bristle - Mathf.Abs(u) * 0.04f;
            Transform s = Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.022f, len, 0.03f), i % 2 == 0 ? straw : strawDark);
            Quaternion tilt = Quaternion.Euler(0f, 0f, u * 30f);
            s.localRotation = tilt;
            s.localPosition = new Vector3(u * 0.3f, bristle, 0f) + tilt * new Vector3(0f, -len * 0.5f, 0f);
        }
        return root;
    }

    private static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        r.SetPropertyBlock(block);
        return go.transform;
    }
}
