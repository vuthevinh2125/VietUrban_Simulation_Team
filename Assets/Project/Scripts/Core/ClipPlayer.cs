using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Phát hoạt ảnh Mixamo (Resources/Animations/*.fbx, import Humanoid) lên bất kỳ nhân vật Humanoid nào bằng Playables API,
/// không cần sửa Animator Controller:
///  - Play(tên, lặp?, thời gian chuyển): chuyển mượt (crossfade) từ hoạt ảnh đang phát.
///  - Hoạt ảnh không lặp thì giữ khung cuối (vd. ngã xuống), hoặc chuyển sang hoạt ảnh xếp hàng (Then).
///  - Stop(): bỏ graph, nhân vật quay lại Animator Controller gốc (đi / chạy theo tham số Speed).
/// Clip được nạp một lần và dùng chung (cache theo tên).
/// </summary>
public class ClipPlayer : MonoBehaviour
{
    private static readonly Dictionary<string, AnimationClip> cache = new Dictionary<string, AnimationClip>();

    /// <summary>Hoạt ảnh trong Resources/Animations/&lt;tên&gt;.fbx (null nếu không có).</summary>
    public static AnimationClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (cache.TryGetValue(name, out AnimationClip c)) return c;
        c = null;
        foreach (AnimationClip a in Resources.LoadAll<AnimationClip>("Animations/" + name))
            if (a != null && !a.name.StartsWith("__preview__")) { c = a; break; }
        // Chỉ nhận hoạt ảnh Humanoid (dùng chung cho mọi nhân vật Mixamo). Clip Generic phát lên nhân vật Humanoid
        // sẽ làm nhân vật đơ -> coi như không có, nơi gọi tự dùng cách cũ.
        if (c != null && !c.humanMotion)
        {
            Debug.LogWarning($"[ClipPlayer] '{name}' chưa import dạng Humanoid (Inspector > Rig > Animation Type = Humanoid) - bỏ qua.");
            c = null;
        }
        cache[name] = c;
        return c;
    }

    public static bool Has(string name) => Clip(name) != null;

    /// <summary>Bộ phát của animator (tạo nếu chưa có).</summary>
    public static ClipPlayer On(Animator a)
    {
        if (a == null) return null;
        ClipPlayer p = a.GetComponent<ClipPlayer>();
        if (p == null) p = a.gameObject.AddComponent<ClipPlayer>();
        p.animator = a;
        return p;
    }

    private Animator animator;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private readonly AnimationClipPlayable[] slot = new AnimationClipPlayable[2];
    private readonly bool[] loops = new bool[2];
    private readonly float[] lengths = new float[2];
    private int cur = -1, prev = -1;
    private float fadeT, fadeDur;
    private string queued; private bool queuedLoop;

    public string Current { get; private set; }

    /// <summary>Hoạt ảnh hiện tại (không lặp) đã chạy hết.</summary>
    public bool Finished => cur >= 0 && slot[cur].IsValid() && !loops[cur] && slot[cur].GetTime() >= lengths[cur] - 0.02f;

    private void Ensure()
    {
        if (graph.IsValid()) return;
        animator.applyRootMotion = false;   // nhân vật đứng tại chỗ, vị trí do code điều khiển
        graph = PlayableGraph.Create("ClipPlayer_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);
        graph.Play();
        cur = prev = -1;
    }

    /// <summary>Phát hoạt ảnh (trả về false nếu không có clip). time01: bắt đầu từ vị trí ngẫu nhiên / chỉ định (0..1).</summary>
    public bool Play(string clipName, bool loop = true, float fade = 0.3f, float speed = 1f, float time01 = 0f)
    {
        AnimationClip clip = Clip(clipName);
        if (clip == null || animator == null || !animator.isHuman) return false;
        if (Current == clipName && cur >= 0 && loop && loops[cur]) return true; // đang phát rồi
        Ensure();
        int next = cur < 0 ? 0 : 1 - cur;
        if (slot[next].IsValid()) { graph.Disconnect(mixer, next); slot[next].Destroy(); }
        var cp = AnimationClipPlayable.Create(graph, clip);
        cp.SetApplyFootIK(true);
        cp.SetSpeed(speed);
        cp.SetTime(Mathf.Clamp01(time01) * clip.length);
        graph.Connect(cp, 0, mixer, next);
        slot[next] = cp; loops[next] = loop; lengths[next] = clip.length;
        prev = cur; cur = next;
        fadeT = 0f; fadeDur = prev < 0 ? 0f : Mathf.Max(0f, fade);
        if (fadeDur <= 0f) { mixer.SetInputWeight(cur, 1f); if (prev >= 0) mixer.SetInputWeight(prev, 0f); }
        Current = clipName;
        queued = null;
        return true;
    }

    /// <summary>Hoạt ảnh phát tiếp khi hoạt ảnh không lặp hiện tại chạy xong.</summary>
    public void Then(string clipName, bool loop = true) { queued = clipName; queuedLoop = loop; }

    /// <summary>Bỏ hoạt ảnh, trả nhân vật về Animator Controller gốc.</summary>
    public void Stop()
    {
        if (graph.IsValid()) graph.Destroy();
        cur = prev = -1; Current = null; queued = null;
    }

    void Update()
    {
        if (!graph.IsValid()) return;
        for (int i = 0; i < 2; i++)
        {
            if (!slot[i].IsValid() || lengths[i] <= 0f) continue;
            double t = slot[i].GetTime();
            if (loops[i]) { if (t >= lengths[i]) slot[i].SetTime(t % lengths[i]); }
            else if (t >= lengths[i]) slot[i].SetTime(lengths[i]);   // giữ khung cuối
        }
        if (fadeDur > 0f && prev >= 0)
        {
            fadeT += Time.deltaTime;
            float w = Mathf.Clamp01(fadeT / fadeDur);
            mixer.SetInputWeight(cur, w);
            mixer.SetInputWeight(prev, 1f - w);
        }
        else if (cur >= 0) mixer.SetInputWeight(cur, 1f);
        if (queued != null && Finished) { string q = queued; queued = null; Play(q, queuedLoop, 0.35f); }
    }

    void OnDisable() { if (graph.IsValid()) graph.Stop(); }
    void OnEnable() { if (graph.IsValid()) graph.Play(); }
    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    // ================= Đồ cầm tay =================
    private static Mesh phoneMesh;

    /// <summary>Gắn điện thoại vào tay phải (cho hoạt ảnh nhắn tin / quay phim). Gỡ bằng RemovePhone.</summary>
    public static void AttachPhone(Animator a)
    {
        if (a == null || !a.isHuman) return;
        Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null || hand.Find("Phone") != null) return;
        if (phoneMesh == null)
        {
            var mb = new VMeshBuilder();
            mb.Add(VShape.RoundBox(0.25f, 8), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.075f, 0.15f, 0.01f)), new Color(0.08f, 0.08f, 0.09f));
            mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0f, 0.0055f), Quaternion.identity, new Vector3(0.064f, 0.13f, 0.001f)), new Color(0.35f, 0.55f, 0.85f));
            phoneMesh = mb.ToMesh(RealModels.MeshPrefix + "_Phone");
            phoneMesh.hideFlags = HideFlags.DontSave;
        }
        var g = new GameObject("Phone");
        g.transform.SetParent(hand, false);
        // Bù tỉ lệ của xương tay (model Mixamo hay có scale 0.01 ở gốc)
        Vector3 ls = hand.lossyScale;
        g.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z));
        g.transform.position = hand.position + hand.up * 0.08f + hand.forward * 0.03f;
        g.transform.rotation = hand.rotation * Quaternion.Euler(0f, 0f, 0f);
        g.AddComponent<MeshFilter>().sharedMesh = phoneMesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
    }

    public static void RemovePhone(Animator a)
    {
        if (a == null || !a.isHuman) return;
        Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand);
        Transform p = hand != null ? hand.Find("Phone") : null;
        if (p != null) Destroy(p.gameObject);
    }
}
