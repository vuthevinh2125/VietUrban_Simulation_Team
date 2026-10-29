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
            hands.SetPrompt(target != null ? "[Hold LMB] Sweep" : $"Dirty spots: {dirt}");
        // Cán chổi nghiêng ra trước, đầu chổi chạm sàn phía trước mặt; quét = đưa qua đưa lại
        broom.localRotation = Quaternion.Euler(Mathf.Abs(Mathf.Sin(sweepPhase)) * -3f, Mathf.Sin(sweepPhase) * 16f, 0f);
    }

    // ================= Gậy =================
    private void UpdateBat()
    {
        bool thiefAround = false;
        foreach (NPCShopper n in NPCShopper.Active)
            if (n != null && n.IsFleeingThief && Vector3.Distance(n.transform.position, transform.position) < 15f) { thiefAround = true; break; }
        hands.SetPrompt(thiefAround ? "<color=#FF6B6B>Thief! Get close and swing [LMB]!</color>" : "[LMB] Swing");

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
    /// <summary>
    /// Chổi: gốc = chỗ tay cầm (góc dưới phải màn hình). Cán chéo xuống tới đầu chổi nằm phía trước, sát mép dưới màn hình;
    /// lông chổi xoè ra và chĩa xuống sàn nên nhìn rõ cả túm lông. Quét = xoay quanh tay cầm qua trái / phải.
    /// </summary>
    private static Transform BuildBroom(Transform parent)
    {
        var root = new GameObject("Broom").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0.38f, -0.22f, 0.48f);
        Color wood = new Color(0.62f, 0.42f, 0.22f), red = new Color(0.8f, 0.18f, 0.15f), straw = new Color(0.93f, 0.78f, 0.38f), strawDark = new Color(0.78f, 0.6f, 0.25f);

        Vector3 head = new Vector3(-0.26f, -0.27f, 0.74f);          // đầu chổi so với tay cầm
        Vector3 back = -head.normalized * 0.12f;                     // đoạn cán thò ra sau tay
        Vector3 a = back, b = head;
        Transform handle = Part(root, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(0.028f, (b - a).magnitude * 0.5f, 0.028f), wood);
        handle.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        Transform cap = Part(root, PrimitiveType.Cylinder, a, new Vector3(0.034f, 0.025f, 0.034f), red);
        cap.localRotation = handle.localRotation;

        // Đầu chổi nằm ngang, hơi quay theo hướng cán
        var headRoot = new GameObject("Head").transform;
        headRoot.SetParent(root, false);
        headRoot.localPosition = head;
        headRoot.localRotation = Quaternion.Euler(0f, -18f, 0f);
        Part(headRoot, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.055f, 0.07f), red);
        Part(headRoot, PrimitiveType.Cube, new Vector3(0f, -0.035f, 0f), new Vector3(0.31f, 0.02f, 0.075f), new Color(0.25f, 0.25f, 0.28f));

        // Lông chổi chĩa xuống, xoè như cái quạt, 2 lớp lệch nhau
        const int strands = 15;
        for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < strands; i++)
            {
                float u = i / (float)(strands - 1) - 0.5f;
                float len = 0.2f - Mathf.Abs(u) * 0.04f + (i % 3) * 0.012f;
                Transform strand = Part(headRoot, PrimitiveType.Cube, Vector3.zero, new Vector3(0.018f, len, 0.014f), (i + layer) % 2 == 0 ? straw : strawDark);
                Quaternion tilt = Quaternion.Euler((layer - 0.5f) * 14f, 0f, u * 36f);
                strand.localRotation = tilt;
                strand.localPosition = new Vector3(u * 0.27f, -0.045f, (layer - 0.5f) * 0.03f) + tilt * new Vector3(0f, -len * 0.5f, 0f);
            }
        return root;
    }

    /// <summary>Gậy bóng chày to bản: cán quấn băng đen, núm chặn, thân gỗ sẫm phình to, đai thép + đinh tán.</summary>
    private static Transform BuildBat(Transform parent)
    {
        var root = new GameObject("Bat").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0.48f, -0.42f, 0.85f);
        root.localScale = Vector3.one * 0.9f;
        Color wood = new Color(0.36f, 0.2f, 0.1f), woodLight = new Color(0.5f, 0.3f, 0.15f), tape = new Color(0.08f, 0.08f, 0.09f),
              steel = new Color(0.62f, 0.64f, 0.68f), red = new Color(0.78f, 0.1f, 0.08f);

        Part(root, PrimitiveType.Sphere, new Vector3(0f, -0.25f, 0f), new Vector3(0.075f, 0.035f, 0.075f), tape);          // núm chặn
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), new Vector3(0.048f, 0.13f, 0.048f), tape);         // cán quấn băng
        for (int i = 0; i < 4; i++)                                                                                       // vệt băng quấn
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, -0.2f + i * 0.055f, 0f), new Vector3(0.052f, 0.006f, 0.052f), red);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.07f, 0.06f, 0.07f), woodLight);       // cổ gậy
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.1f, 0.07f, 0.1f), woodLight);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.135f, 0.15f, 0.135f), wood);          // thân to
        Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.53f, 0f), new Vector3(0.135f, 0.05f, 0.135f), wood);            // đầu gậy bo tròn
        foreach (float y in new[] { 0.27f, 0.47f })                                                                      // 2 đai thép
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(0.142f, 0.012f, 0.142f), steel);
        for (int i = 0; i < 8; i++)                                                                                       // đinh tán quanh thân
        {
            float a = i * 45f * Mathf.Deg2Rad;
            float y = i % 2 == 0 ? 0.33f : 0.41f;
            Transform stud = Part(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.068f, y, Mathf.Sin(a) * 0.068f), Vector3.one * 0.022f, steel);
            stud.localRotation = Quaternion.identity;
        }
        return root;
    }

    private static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        RuntimeMaterials.Apply(r, color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }
}
