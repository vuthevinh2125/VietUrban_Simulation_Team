using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Cửa tự động: mở khi người chơi HOẶC khách/NPC (NavMeshAgent) đứng trong vùng collider của object này,
/// đóng khi không còn ai. Tự quét vùng định kỳ nên không cần Rigidbody, nhiều người đi qua cùng lúc vẫn đúng.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AutoDoor : MonoBehaviour
{
    [Header("Door Settings")]
    public Transform doorModel;
    [Tooltip("Góc muốn mở TƯƠNG ĐỐI so với lúc đóng")]
    public Vector3 openRotationOffset = new Vector3(0, 90, 0);
    [Tooltip("Thời gian mở cửa (giây)")]
    public float openDuration = 0.5f;

    [Header("Detection")]
    [Tooltip("Bao lâu kiểm tra vùng cửa một lần (giây)")]
    public float checkInterval = 0.15f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen = false;
    private Coroutine animationCoroutine;
    private Collider area;
    private float nextCheckTime;
    private readonly Collider[] hits = new Collider[32];

    void Start()
    {
        area = GetComponent<Collider>();
        if (doorModel != null)
        {
            Vector3 closedEuler = doorModel.localEulerAngles;
            closedRotation = doorModel.localRotation;
            openRotation = Quaternion.Euler(closedEuler + openRotationOffset);
        }
    }

    void Update()
    {
        if (doorModel == null || area == null || Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        bool someoneInside = IsAnyoneInside();
        if (someoneInside != isOpen) SetOpen(someoneInside);
    }

    private bool IsAnyoneInside()
    {
        Bounds b = area.bounds;

        int count = Physics.OverlapBoxNonAlloc(b.center, b.extents, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;
            if (c.CompareTag("Player") || c.transform.root.CompareTag("Player")) return true;
            if (c.GetComponentInParent<NavMeshAgent>() != null) return true;
        }

        // Khách không có collider vẫn được tính theo vị trí
        foreach (NPCShopper npc in NPCShopper.Active)
        {
            if (npc != null && b.Contains(npc.transform.position)) return true;
        }
        return false;
    }

    private void SetOpen(bool open)
    {
        if (open && !isOpen) AudioManager.PlayAt("door_slide", transform.position, 0.7f);
        isOpen = open;
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateDoor(open ? openRotation : closedRotation));
    }

    private IEnumerator AnimateDoor(Quaternion endRot)
    {
        Quaternion startRot = doorModel.localRotation;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            doorModel.localRotation = Quaternion.Slerp(startRot, endRot, elapsed / openDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        doorModel.localRotation = endRot;
    }
}
