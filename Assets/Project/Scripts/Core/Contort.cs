using UnityEngine;

/// <summary>
/// Vặn vẹo cơ thể người (kinh dị) chồng lên hoạt ảnh, chạy sau Animator mỗi frame (LateUpdate):
///  - Đầu vặn ngược ra sau (twist), nghiêng gãy cổ (tilt), cúi / ngửa (pitch); cổ kéo dài (neckStretch).
///  - Tay dài ngoằng (armStretch), lưng ưỡn gập ngược (spineBack).
///  - Giật cục (jitter): đầu / thân giật lên từng cơn ngắn.
/// Đặt giá trị *Target; giá trị thật đuổi theo với tốc độ speed (độ / giây). Vặn đầu nhanh có tiếng "rắc".
/// </summary>
public class Contort : MonoBehaviour
{
    public float twistTarget, tiltTarget, pitchTarget, spineBackTarget;
    public float neckStretchTarget = 1f, armStretchTarget = 1f;
    public float jitter;
    public float speed = 240f;

    private float twist, tilt, pitch, spineBack, neckStretch = 1f, armStretch = 1f;
    private Animator anim;
    private Transform head, neck, spine, chest, uL, uR, lL, lR, handL, handR;
    private Quaternion headBase, neckBase, spineBase;
    private ClipPlayer cp;
    private float nextJerk, jerkUntil;
    private Vector3 jerk;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim == null || !anim.isHuman) { enabled = false; return; }
        head = anim.GetBoneTransform(HumanBodyBones.Head);
        neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        uL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); uR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        lL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); lR = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        handL = anim.GetBoneTransform(HumanBodyBones.LeftHand); handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (head != null) headBase = head.localRotation;
        if (neck != null) neckBase = neck.localRotation;
        if (spine != null) spineBase = spine.localRotation;
        cp = anim.GetComponent<ClipPlayer>();
    }

    /// <summary>Vặn ngay lập tức (không đuổi dần), có tiếng xương kêu.</summary>
    public void Snap(float twistDeg, float tiltDeg = 0f)
    {
        twistTarget = twist = twistDeg;
        tiltTarget = tilt = tiltDeg;
        if (head != null) HorrorAudio.Play3D(HorrorAudio.Crack(), head.position, 0.9f, false, 2f, 30f);
    }

    void LateUpdate()
    {
        if (anim == null) return;
        if (cp == null) cp = anim.GetComponent<ClipPlayer>();
        bool animated = cp != null && cp.Current != null;
        if (!animated)
        {
            // không có hoạt ảnh ghi đè mỗi frame -> tự trả xương về gốc trước khi vặn (tránh cộng dồn)
            if (head != null) head.localRotation = headBase;
            if (neck != null) neck.localRotation = neckBase;
            if (spine != null) spine.localRotation = spineBase;
        }
        float dt = Time.deltaTime;
        float before = twist;
        twist = Mathf.MoveTowards(twist, twistTarget, speed * dt);
        tilt = Mathf.MoveTowards(tilt, tiltTarget, speed * dt);
        pitch = Mathf.MoveTowards(pitch, pitchTarget, speed * dt);
        spineBack = Mathf.MoveTowards(spineBack, spineBackTarget, speed * 0.5f * dt);
        neckStretch = Mathf.MoveTowards(neckStretch, neckStretchTarget, 1.5f * dt);
        armStretch = Mathf.MoveTowards(armStretch, armStretchTarget, 1.5f * dt);
        if (Mathf.Abs(twist - before) > 0.01f && Mathf.Abs(twist) > 120f && Mathf.Abs(before) <= 120f && head != null)
            HorrorAudio.Play3D(HorrorAudio.Crack(), head.position, 0.8f, false, 2f, 30f);

        // giật cục
        if (jitter > 0f && Time.time >= nextJerk)
        {
            nextJerk = Time.time + Random.Range(0.25f, 1.2f) / Mathf.Max(0.2f, jitter);
            jerkUntil = Time.time + Random.Range(0.06f, 0.14f);
            jerk = new Vector3(Random.Range(-25f, 25f), Random.Range(-35f, 35f), Random.Range(-30f, 30f)) * Mathf.Clamp01(jitter);
        }
        Vector3 j = Time.time < jerkUntil ? jerk : Vector3.zero;

        Vector3 up = transform.up, fwd = transform.forward, right = transform.right;
        if (spine != null && Mathf.Abs(spineBack) > 0.1f) spine.rotation = Quaternion.AngleAxis(-spineBack, right) * spine.rotation;
        if (neck != null && Mathf.Abs(neckStretch - 1f) > 0.01f)
        {
            neck.localScale = new Vector3(1f, neckStretch, 1f);
            if (head != null) head.localScale = new Vector3(1f, 1f / neckStretch, 1f);
        }
        if (head != null)
        {
            Quaternion q = Quaternion.AngleAxis(twist + j.y, up) * Quaternion.AngleAxis(tilt + j.z, fwd) * Quaternion.AngleAxis(pitch + j.x, right);
            head.rotation = q * head.rotation;
        }
        if (Mathf.Abs(armStretch - 1f) > 0.01f)
        {
            foreach (Transform t in new[] { uL, uR, lL, lR }) if (t != null) t.localScale = new Vector3(1f, armStretch, 1f);
            if (handL != null) handL.localScale = new Vector3(1f, 1f / (armStretch * armStretch), 1f);
            if (handR != null) handR.localScale = new Vector3(1f, 1f / (armStretch * armStretch), 1f);
        }
    }
}
