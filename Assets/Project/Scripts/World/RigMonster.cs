using UnityEngine;

/// <summary>
/// Quái vật có khung xương thật (model Humanoid trong Resources/Horror: con hề WhiteClown, con trùm khăn...), dùng thay cho
/// quỷ dựng bằng code (HorrorCreature) trong các đoạn phim kinh dị. Cùng "bảng điều khiển" với HorrorCreature để code đoạn phim
/// dùng như nhau: mode (đứng / lê bước / bò lao tới / vồ), gait, twitch, vặn đầu (headTwistTarget, headTiltTarget, headSpeed), jawOpen.
///  - Hoạt ảnh thật bằng ClipPlayer (Mixamo: Injured_Hurting_Idle, Walking, Running, Yelling), tốc độ khớp với tốc độ di chuyển.
///  - Vặn đầu / giật cục / gù lưng bằng Contort (chạy sau Animator).
///  - Rise(): trồi dần lên (ngồi dậy từ quan tài).
/// </summary>
public class RigMonster : MonoBehaviour
{
    /// <summary>Tên model trong Resources/Horror (thiếu con nào thì bỏ qua con đó).</summary>
    public static readonly string[] Models = { "WhiteClown", "Hooded" };

    public HorrorCreature.Mode mode = HorrorCreature.Mode.Stand;
    public float gait = 1f;
    public float twitch;
    public float headTwistTarget, headTiltTarget, headSpeed = 240f;
    public float jawOpen;
    public float hunch;          // gù lưng (độ), cộng thêm khi lê bước / bò

    public Animator Anim { get; private set; }
    public string ModelName { get; private set; }
    private ClipPlayer cp;
    private Contort ct;
    private Transform head;
    private HorrorCreature.Mode shown = (HorrorCreature.Mode)(-1);
    private float shownSpeed = -1f, screamUntil, idleSpeed;
    private float riseFrom, riseT0 = -1f, riseDur, laughUntil, laughPhase;

    private static GameObject[] prefabs;

    /// <summary>Các model quái có trong Resources/Horror.</summary>
    public static GameObject[] Prefabs()
    {
        if (prefabs != null) return prefabs;
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (string n in Models)
        {
            GameObject g = Resources.Load<GameObject>("Horror/" + n);
            if (g != null && g.GetComponentInChildren<Animator>(true) != null) list.Add(g);
        }
        prefabs = list.ToArray();
        return prefabs;
    }

    public static bool Available => Prefabs().Length > 0;

    /// <summary>Model theo tên (null nếu không có).</summary>
    public static GameObject Prefab(string name)
    {
        foreach (GameObject g in Prefabs()) if (g.name == name) return g;
        return null;
    }

    /// <summary>
    /// Dựng một con quái (prefab = null: lấy ngẫu nhiên). height = chiều cao (m), chân chạm pos.
    /// </summary>
    public static RigMonster Create(Transform parent, Vector3 pos, Quaternion rot, GameObject prefab = null, float height = 1.95f, bool strip = false)
    {
        GameObject[] all = Prefabs();
        if (prefab == null)
        {
            if (all.Length == 0) return null;
            prefab = all[Random.Range(0, all.Length)];
        }
        GameObject go;
        if (strip)
        {
            // Model người của game (khách hàng...): tạo dưới gốc đang tắt, gỡ hết script / NavMeshAgent trước khi chúng kịp chạy
            var holder = new GameObject("_FiendHolder");
            holder.SetActive(false);
            go = Instantiate(prefab, pos, rot, holder.transform);
            MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--) if (scripts[i] != null) DestroyImmediate(scripts[i]);
            foreach (UnityEngine.AI.NavMeshAgent a in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(a);
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
            go.transform.SetParent(parent, true);
            Destroy(holder);
            go.SetActive(true);
        }
        else go = Instantiate(prefab, pos, rot, parent);
        go.name = "Fiend_" + prefab.name;
        RigMonster m = go.AddComponent<RigMonster>();
        m.ModelName = prefab.name;
        m.Setup(height);
        return m;
    }

    private void Setup(float height)
    {
        Anim = GetComponentInChildren<Animator>(true);
        if (Anim != null)
        {
            Anim.applyRootMotion = false;
            Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (Anim.isHuman) head = Anim.GetBoneTransform(HumanBodyBones.Head);
            cp = ClipPlayer.On(Anim);
        }
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.updateWhenOffscreen = true;   // không biến mất khi vặn vẹo ra ngoài khung bao
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        SetHeight(height);
        ct = gameObject.AddComponent<Contort>();
        idleSpeed = Random.Range(0.5f, 0.75f);
        Apply(true);
    }

    /// <summary>Đổi chiều cao (đo từ gót chân tới đỉnh đầu ở tư thế gốc).</summary>
    public void SetHeight(float height)
    {
        transform.localScale = Vector3.one;
        float h = 0f;
        if (head != null) h = (head.position.y - transform.position.y) / 0.9f;   // khớp đầu ~ 90% chiều cao
        if (h < 0.3f)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) h = Mathf.Max(h, r.bounds.max.y - transform.position.y);
        }
        if (h > 0.3f) transform.localScale = Vector3.one * (height / h);
    }

    /// <summary>Vị trí đầu (để đặt máy quay nhìn vào mặt).</summary>
    public Vector3 HeadPos => head != null ? head.position + transform.up * 0.04f * transform.localScale.y : transform.position + Vector3.up * 1.75f;

    /// <summary>Dời cả con sao cho đầu nằm đúng điểm này.</summary>
    public void PlaceHeadAt(Vector3 at) => transform.position += at - HeadPos;

    /// <summary>Gào thét: tiếng thét + ngửa cổ.</summary>
    public void Scream(float volume)
    {
        HorrorAudio.Play3D(HorrorAudio.Scream(), HeadPos, volume, false, 3f, 60f);
        screamUntil = Time.time + 1.3f;
    }

    /// <summary>Cười ngặt nghẽo: ngửa cổ, đầu giật giật theo nhịp cười, vai rung.</summary>
    public void Laugh(float seconds)
    {
        laughUntil = Time.time + seconds;
        laughPhase = Random.value * 6f;
    }

    public void StopLaugh() => laughUntil = 0f;

    /// <summary>Ép tốc độ hoạt ảnh (0 = khựng hình, >1 tua nhanh; âm = trả về bình thường) - cho hiệu ứng giật cục.</summary>
    public void AnimSpeed(float speed)
    {
        if (cp == null) return;
        if (speed < 0f) { shownSpeed = -1f; Apply(true); return; }
        Apply(false);                       // đổi dáng (mode) trước nếu vừa đổi, rồi mới khựng / tua
        cp.SetSpeed(speed);
    }

    public bool Laughing => Time.time < laughUntil;

    /// <summary>Trồi dần lên từ dưới (depth m) trong seconds giây.</summary>
    public void Rise(float depth, float seconds)
    {
        riseFrom = depth;
        riseDur = Mathf.Max(0.05f, seconds);
        riseT0 = Time.time;
        transform.position -= Vector3.up * depth;
    }

    void Update()
    {
        if (riseT0 >= 0f)
        {
            float u = Mathf.Clamp01((Time.time - riseT0) / riseDur);
            float prev = Mathf.Clamp01((Time.time - Time.deltaTime - riseT0) / riseDur);
            float k = riseFrom * (Smooth(u) - Smooth(prev));
            transform.position += Vector3.up * k;
            if (u >= 1f) riseT0 = -1f;
        }
        Apply(false);
    }

    private static float Smooth(float u) => u * u * (3f - 2f * u);

    /// <summary>Ghi đè góc cúi / ngửa đầu (độ, âm = ngửa lên); NaN = theo trạng thái.</summary>
    public float headPitch = float.NaN;

    /// <summary>Hai tay duỗi thẳng ra trước (theo hướng "trên" của thân) như đòi vồ lấy - dùng khi lao người nằm ngang tới.</summary>
    public bool reachArms;

    private Transform uL, lL, hL, uR, lR, hR;

    void LateUpdate()
    {
        if (!reachArms || Anim == null || !Anim.isHuman) return;
        if (uL == null)
        {
            uL = Anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); lL = Anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); hL = Anim.GetBoneTransform(HumanBodyBones.LeftHand);
            uR = Anim.GetBoneTransform(HumanBodyBones.RightUpperArm); lR = Anim.GetBoneTransform(HumanBodyBones.RightLowerArm); hR = Anim.GetBoneTransform(HumanBodyBones.RightHand);
        }
        Vector3 ahead = transform.up;                                   // thân nằm ngang: "trên" của thân = hướng lao tới
        Aim(uL, lL, hL, (ahead - transform.right * 0.18f + transform.forward * 0.1f).normalized);
        Aim(uR, lR, hR, (ahead + transform.right * 0.18f + transform.forward * 0.1f).normalized);
    }

    private static void Aim(Transform upper, Transform lower, Transform hand, Vector3 dir)
    {
        if (upper == null || lower == null) return;
        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, dir) * upper.rotation;
        if (hand != null) lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, dir) * lower.rotation;
    }

    private void Apply(bool force)
    {
        if (ct != null)
        {
            bool laugh = Time.time < laughUntil;                       // cười: ngồi / đứng yên, chỉ hơi ngửa mặt (không gật gù)
            ct.speed = headSpeed;
            ct.twistTarget = headTwistTarget;
            ct.tiltTarget = headTiltTarget;
            ct.pitchTarget = !float.IsNaN(headPitch) ? headPitch : Time.time < screamUntil ? -38f : laugh ? -12f : -jawOpen * 22f;
            ct.jitter = twitch * 0.8f;
            float back = mode == HorrorCreature.Mode.Crawl ? -55f : mode == HorrorCreature.Mode.Walk ? -14f : mode == HorrorCreature.Mode.Reach ? -20f : 0f;
            ct.spineBackTarget = reachArms ? 0f : back - hunch;
            ct.armStretchTarget = mode == HorrorCreature.Mode.Crawl || mode == HorrorCreature.Mode.Reach ? 1.3f : 1f;
        }
        if (cp == null) return;
        string clip; float speed;
        switch (mode)
        {
            case HorrorCreature.Mode.Walk: clip = "Walking"; speed = Mathf.Clamp(0.72f * gait, 0.35f, 1.6f); break;
            case HorrorCreature.Mode.Crawl: clip = "Running"; speed = Mathf.Clamp(0.45f * gait, 0.7f, 1.7f); break;
            case HorrorCreature.Mode.Reach: clip = ClipPlayer.Has("Yelling") ? "Yelling" : "Angry_Gesture"; speed = 1.3f; break;
            default: clip = "Injured_Hurting_Idle"; speed = idleSpeed; break;
        }
        if (!ClipPlayer.Has(clip)) clip = "Walking";
        if (!force && mode == shown && Mathf.Abs(speed - shownSpeed) < 0.02f) return;
        cp.Play(clip, true, mode == HorrorCreature.Mode.Crawl ? 0.12f : 0.3f, speed, mode == shown ? 0f : Random.value);
        shown = mode; shownSpeed = speed;
    }
}

/// <summary>Ánh nến lung linh (đổi cường độ ngẫu nhiên, mượt).</summary>
public class CandleFlicker : MonoBehaviour
{
    private Light l;
    private float baseI, cur, next, target;

    void Start() { l = GetComponent<Light>(); baseI = l != null ? l.intensity : 1f; cur = target = baseI; }

    void Update()
    {
        if (l == null) return;
        if (Time.time >= next) { next = Time.time + Random.Range(0.04f, 0.16f); target = baseI * Random.Range(0.55f, 1.15f); }
        cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
        l.intensity = cur;
    }
}
