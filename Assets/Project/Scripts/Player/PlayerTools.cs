using UnityEngine;

/// <summary>
/// Dụng cụ cầm tay của người chơi:
///  [1] Tay không (dùng thùng, máy tính, quầy... như bình thường)
///  [2] Chổi  : nhìn vào vết bẩn, GIỮ chuột trái để quét sạch
///  [3] Gậy   : chuột trái để vung gậy, trúng kẻ trộm đang chạy thì bắt được (lấy lại tiền hàng)
/// Đang cầm thùng thì không lấy dụng cụ được. Tự gắn vào người chơi (cùng ItemPickup).
/// </summary>
public class PlayerTools : MonoBehaviour
{
    public enum Tool { None, Broom, Bat }

    public static PlayerTools Instance { get; private set; }
    public Tool Current { get; private set; } = Tool.None;

    public float sweepTime = 1.2f;      // giây để quét sạch 1 vết bẩn
    public float batReach = 2.6f;

    private ItemPickup hands;
    private Camera cam;
    private Transform broom, bat;
    private float swingTimer = -1f;
    private bool swingChecked;
    private float sweepPhase, nextSweepSound;

    void Awake()
    {
        Instance = this;
        hands = GetComponent<ItemPickup>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        cam = hands != null ? hands.playerCamera : Camera.main;
        if (cam == null) return;
        broom = BuildBroom(cam.transform);
        bat = BuildBat(cam.transform);
        broom.gameObject.SetActive(false);
        bat.gameObject.SetActive(false);
    }

    void Update()
    {
        if (cam == null || broom == null) return;
        if (UIState.IsBlockingUIOpen || (FurniturePlacer.Instance != null && FurniturePlacer.Instance.IsBusy)) return;

        bool holdingBox = hands != null && hands.HeldBox != null;
        if (holdingBox && Current != Tool.None) Equip(Tool.None);
        if (!holdingBox)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(Tool.None);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(Current == Tool.Broom ? Tool.None : Tool.Broom);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Equip(Current == Tool.Bat ? Tool.None : Tool.Bat);
        }

        if (Current == Tool.Broom) UpdateBroom();
        else if (Current == Tool.Bat) UpdateBat();
    }

    public void Equip(Tool tool)
    {
        if (Current == tool) return;
        Current = tool;
        if (broom != null) broom.gameObject.SetActive(tool == Tool.Broom);
        if (bat != null) bat.gameObject.SetActive(tool == Tool.Bat);
        swingTimer = -1f;
        if (hands != null) hands.SetPrompt(null);
        AudioManager.Play("box_pickup", 0.3f);
    }

    // ================= Chổi =================
    private void UpdateBroom()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        DirtSpot target = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            target = DirtManager.Nearest(hit.point, 0.9f);

        bool sweeping = target != null && Input.GetMouseButton(0);
        if (sweeping)
        {
            sweepPhase += Time.deltaTime * 9f;
            if (Time.time >= nextSweepSound) { AudioManager.Play("sweep", 0.6f); nextSweepSound = Time.time + 0.45f; }
            if (target.Clean(Time.deltaTime / Mathf.Max(0.1f, sweepTime))) hands.SetPrompt("Clean!");
        }
        else sweepPhase = Mathf.MoveTowards(sweepPhase, 0f, Time.deltaTime * 6f);

        int dirt = DirtManager.Count;
        if (!sweeping)
            hands.SetPrompt(target != null ? "[Hold LMB] Sweep" : $"Broom   ·   Dirty spots: {dirt}   ·   [1] Hands");
        broom.localRotation = Quaternion.Euler(25f, -15f, -30f + Mathf.Sin(sweepPhase) * 28f);
    }

    // ================= Gậy =================
    private void UpdateBat()
    {
        bool thiefAround = false;
        foreach (NPCShopper n in NPCShopper.Active)
            if (n != null && n.IsFleeingThief && Vector3.Distance(n.transform.position, transform.position) < 15f) { thiefAround = true; break; }
        hands.SetPrompt(thiefAround ? "<color=#FF6B6B>Thief! Get close and swing [LMB]!</color>" : "Bat   ·   [LMB] Swing   ·   [1] Hands");

        if (swingTimer < 0f && Input.GetMouseButtonDown(0))
        {
            swingTimer = 0f;
            swingChecked = false;
        }

        if (swingTimer >= 0f)
        {
            swingTimer += Time.deltaTime;
            float t = Mathf.Clamp01(swingTimer / 0.35f);
            float angle = t < 0.4f ? Mathf.Lerp(0f, -60f, t / 0.4f) : Mathf.Lerp(-60f, 50f, (t - 0.4f) / 0.6f);
            bat.localRotation = Quaternion.Euler(10f + angle, -20f, -15f + angle * 0.3f);
            if (!swingChecked && t >= 0.55f)
            {
                swingChecked = true;
                AudioManager.Play(TryHitThief() ? "bat_hit" : "bat_swing", 0.8f);
            }
            if (swingTimer >= 0.45f) swingTimer = -1f;
        }
        else bat.localRotation = Quaternion.Euler(10f, -20f, -15f);
    }

    /// <summary>Kẻ trộm đang chạy ở trước mặt, trong tầm gậy -> bắt.</summary>
    private bool TryHitThief()
    {
        Vector3 origin = cam.transform.position;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
        foreach (NPCShopper n in NPCShopper.Active)
        {
            if (n == null || !n.IsFleeingThief) continue;
            Vector3 d = n.transform.position + Vector3.up - origin;
            float dist = new Vector2(d.x, d.z).magnitude;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (dist <= batReach && Vector3.Angle(fwd, flat) < 70f)
            {
                n.Catch();
                return true;
            }
        }
        return false;
    }

    // ================= Model (dựng bằng khối đơn giản) =================
    private static Transform BuildBroom(Transform parent)
    {
        var root = new GameObject("Broom").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0.38f, -0.55f, 0.75f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.55f, 0.03f), new Color(0.55f, 0.38f, 0.2f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, -0.56f, 0f), new Vector3(0.26f, 0.05f, 0.07f), new Color(0.2f, 0.45f, 0.8f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, -0.65f, 0f), new Vector3(0.28f, 0.14f, 0.05f), new Color(0.95f, 0.8f, 0.3f));
        return root;
    }

    private static Transform BuildBat(Transform parent)
    {
        var root = new GameObject("Bat").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0.4f, -0.4f, 0.65f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.28f, 0.06f), new Color(0.75f, 0.58f, 0.35f));
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, -0.2f, 0f), new Vector3(0.035f, 0.14f, 0.035f), new Color(0.15f, 0.15f, 0.15f));
        return root;
    }

    private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        RuntimeMaterials.Apply(r, color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
