using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cửa ra vào tự động nhiều cánh.
///  - Kiểu mặc định: CỬA KÉO (sliding = true) - mỗi cánh trượt ngang sang hai bên, tách ra từ giữa, giống cửa siêu thị thật.
///  - sliding = false: kiểu cũ, mỗi cánh xoay quanh bản lề một góc openAngles[i] (độ, quanh trục Y).
/// Cánh có tên chứa "glass" là tấm kính cố định, không di chuyển.
/// Mở khi người chơi / khách tới gần hoặc đang đi về phía cửa, nhân viên / bảo vệ phải tới sát cửa;
/// người đi bộ ngoài phố không làm cửa mở; đóng sau holdOpenTime giây khi không còn ai. Xem DoorSensor.
/// </summary>
public class EntranceDoor : MonoBehaviour
{
    [Header("Leaves")]
    public Transform[] leaves;
    [Tooltip("Góc mở của từng cánh khi sliding = false (độ, quanh trục Y). Mở sai chiều thì đổi dấu.")]
    public float[] openAngles;

    [Header("Kiểu cửa")]
    [Tooltip("Cửa kéo: cánh trượt ngang sang hai bên thay vì xoay")]
    public bool sliding = true;
    [Tooltip("Cánh trượt đi bao nhiêu phần bề rộng của nó (1 = trượt hết bề rộng)")]
    [Range(0.5f, 1.1f)] public float slideFraction = 0.95f;

    [Header("Detection")]
    [Tooltip("Vùng phát hiện mở rộng ra quanh các cánh cửa (mét)")]
    public Vector3 detectPadding = new Vector3(1.5f, 1f, 2.5f);
    public float checkInterval = 0.1f;
    [Tooltip("Tốc độ mở / đóng (1 / giây)")]
    public float openSpeed = 2.2f;
    [Tooltip("Giữ cửa mở thêm bao lâu sau khi người cuối cùng đi qua (giây) - tránh cửa đóng mở liên tục")]
    public float holdOpenTime = 1.5f;

    private Quaternion[] closedRot, openRot;
    private Vector3[] closedPos, slideOffset; // vị trí world lúc đóng + quãng trượt khi mở
    private bool[] moving;
    private Bounds area;
    private Bounds core; // vùng ngưỡng cửa
    private float lastSeen = -999f;
    private bool isOpen;
    private float nextCheck;
    private float t; // 0 = đóng, 1 = mở
    private readonly List<Collider> leafColliders = new List<Collider>();
    private bool collidersEnabled = true;

    void Start()
    {
        if (leaves == null) leaves = new Transform[0];
        int n = leaves.Length;
        closedRot = new Quaternion[n];
        openRot = new Quaternion[n];
        closedPos = new Vector3[n];
        slideOffset = new Vector3[n];
        moving = new bool[n];

        // Khung bao của cả bộ cửa và của riêng các cánh di chuyển
        bool hasArea = false, hasDoor = false;
        Bounds doorBounds = default;
        var leafBounds = new Bounds[n];
        for (int i = 0; i < n; i++)
        {
            if (leaves[i] == null) continue;
            bool isGlass = leaves[i].name.ToLower().Contains("glass");
            moving[i] = !isGlass;
            closedRot[i] = leaves[i].localRotation;
            closedPos[i] = leaves[i].position;
            float angle = openAngles != null && i < openAngles.Length ? openAngles[i] : 90f;
            openRot[i] = closedRot[i] * Quaternion.Euler(0f, isGlass ? 0f : angle, 0f);
            if (!isGlass) leafColliders.AddRange(leaves[i].GetComponentsInChildren<Collider>());

            bool hasLeaf = false;
            foreach (Renderer r in leaves[i].GetComponentsInChildren<Renderer>())
            {
                if (!hasLeaf) { leafBounds[i] = r.bounds; hasLeaf = true; } else leafBounds[i].Encapsulate(r.bounds);
                if (!hasArea) { area = r.bounds; hasArea = true; } else area.Encapsulate(r.bounds);
            }
            if (hasLeaf && !isGlass)
            {
                if (!hasDoor) { doorBounds = leafBounds[i]; hasDoor = true; } else doorBounds.Encapsulate(leafBounds[i]);
            }
        }
        if (!hasArea) area = new Bounds(transform.position, Vector3.one);
        if (!hasDoor) doorBounds = area;

        // Cửa kéo: trượt dọc theo trục ngang dài hơn của bộ cửa, mỗi cánh đi về phía nó đang lệch so với tâm
        Vector3 axis = doorBounds.size.x >= doorBounds.size.z ? Vector3.right : Vector3.forward;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            if (leaves[i] == null || !moving[i]) continue;
            float side = Vector3.Dot(leafBounds[i].center - doorBounds.center, axis);
            float sign = Mathf.Abs(side) > 0.01f ? Mathf.Sign(side) : (k % 2 == 0 ? -1f : 1f);
            float width = Mathf.Abs(Vector3.Dot(leafBounds[i].size, axis));
            if (width < 0.05f) width = 1f;
            slideOffset[i] = axis * sign * width * slideFraction;
            k++;
        }

        core = area;
        core.Expand(new Vector3(1.6f, 1f, 1.6f));
        area.Expand(detectPadding * 2f);
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            if (DoorSensor.AnyoneAt(area, core)) lastSeen = Time.time;
            bool open = Time.time - lastSeen < holdOpenTime;
            if (open && !isOpen) AudioManager.PlayAt("door_slide", transform.position, 0.7f);
            isOpen = open;
        }

        // Cánh cửa đang mở / đang chuyển động thì không chặn người (tránh cánh cửa đẩy người chơi)
        bool shouldCollide = !isOpen && t <= 0.001f;
        if (shouldCollide != collidersEnabled)
        {
            collidersEnabled = shouldCollide;
            foreach (Collider c in leafColliders) if (c != null) c.enabled = shouldCollide;
        }

        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.deltaTime * openSpeed);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < leaves.Length; i++)
        {
            if (leaves[i] == null || !moving[i]) continue;
            if (sliding)
            {
                leaves[i].localRotation = closedRot[i];
                leaves[i].position = closedPos[i] + slideOffset[i] * eased;
            }
            else
            {
                leaves[i].position = closedPos[i];
                leaves[i].localRotation = Quaternion.Slerp(closedRot[i], openRot[i], eased);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.4f);
        Bounds b = area.size == Vector3.zero ? new Bounds(transform.position, Vector3.one) : area;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
        if (core.size != Vector3.zero) Gizmos.DrawWireCube(core.center, core.size);
    }
}
