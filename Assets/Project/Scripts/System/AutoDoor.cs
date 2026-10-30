using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Cửa tự động: mở khi người chơi hoặc khách đứng trong vùng collider của object này (nới thêm detectPadding),
/// nhân viên / bảo vệ phải vào hẳn vùng collider; người đi bộ ngoài phố bỏ qua. Đóng sau holdOpenTime giây khi không còn ai.
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
    [Tooltip("Giữ cửa mở thêm bao lâu sau khi người cuối cùng đi qua (giây)")]
    public float holdOpenTime = 1.2f;
    [Tooltip("Nới vùng phát hiện ra mỗi phía (mét) cho người chơi / khách")]
    public float detectPadding = 0.5f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen = false;
    private Coroutine animationCoroutine;
    private Collider area;
    private float nextCheckTime;
    private float lastSeen = -999f;

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

        Bounds core = area.bounds;
        Bounds wide = core;
        wide.Expand(detectPadding * 2f);
        if (DoorSensor.AnyoneAt(wide, core)) lastSeen = Time.time;
        bool open = Time.time - lastSeen < holdOpenTime;
        if (open != isOpen) SetOpen(open);
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
