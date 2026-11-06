using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Cảnh mở đầu khi bắt đầu New Game (~45 giây + phần đọc thư, [Space] để bỏ qua). Cốt truyện: cửa hàng của bà Lan.
///  0. Tiêu đề trên nền đen.
///  1. HỒI TƯỞNG (màu nâu cũ): ba mươi năm trước, cô Lan trẻ đứng trước cửa hàng mới mở, rao khách rồi bước vào trong.
///  2. Chớp trắng -> HIỆN TẠI: camera lượn vòng quanh cửa hàng giữa phố (không bay ra ngoài bản đồ).
///  3. Cảnh "anh hùng" của cửa hàng: góc thấp, đẩy chậm vào mặt tiền.
///  4. LÁ THƯ của bà (tương tác): lật trang, rồi chọn lời hồi âm (chỉ để kể chuyện, không thưởng gì).
///  5. Vào trong: gian nhỏ đang mở; trôi về mắt người chơi và trả điều khiển.
/// Kỹ thuật quay: chuyển động ease-in-out (smootherstep), camera rung tay rất nhẹ (Perlin noise), đổi tiêu cự (FOV) khi đẩy vào,
/// góc thấp, đặt chủ thể lệch 1/3 khung hình, khung đen điện ảnh trên / dưới.
/// Đường quay tính từ cửa ra vào (EntranceDoor) và khung toà "Supermarket_full". Cô Lan là model khách NPC_Ch37 (bỏ script mua sắm).
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    public static bool Playing { get; private set; }

    /// <summary>Model khách dùng làm cô Lan thời trẻ (không có thì lấy một model khách bất kỳ).</summary>
    public static string LanModel = "NPC_Ch37";

    private Action onDone;
    private bool skip;
    private Camera cam;
    private float baseFov = 50f;
    private CanvasGroup captionGroup, titleGroup, letterGroup;
    private TMP_Text caption, letterBody, letterFooter;
    private Image fade;
    private Volume flashback;        // hậu kỳ URP cho đoạn hồi tưởng: giảm bão hoà, ngả vàng ấm, viền tối, hạt phim
    private GameObject letterButtons, choiceButtons;
    private Button nextButton;
    private int page;
    private int choice = -1;
    private readonly List<Canvas> hidden = new List<Canvas>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    private Vector3 doorG, outward, side, center, inside;
    private GameObject lan;
    private NavMeshAgent lanAgent;
    private Animator lanAnim;
    private bool lanHasSpeed;

    private static readonly string[] Pages =
    {
        "My dear,\n\n" +
        "Thirty years ago I opened this little shop with one shelf of rice, a basket of eggs and a coffee pot. " +
        "The whole street came to me for breakfast.\n\n" +
        "My knees are tired now, and it is time for me to rest.",

        "So the shop is yours. It is small, and most of the shelves are empty.\n\n" +
        "Open early. Keep it clean. Be fair with your prices. " +
        "Take care of the neighbours, and they will take care of you.\n\n" +
        "Write back and tell me your plans.\n\n<align=right>- Grandma Lan</align>",
    };

    private static readonly string[] Replies =
    {
        "\"I'll keep your prices fair, Grandma.\"",
        "\"I'll make it the biggest store in VietUrban!\"",
        "\"I'll take care of the neighbours, just like you did.\"",
    };


    /// <summary>Chiếu cảnh mở đầu rồi gọi done (vd. bắt đầu hướng dẫn). Không tìm được cửa hàng thì gọi done ngay.</summary>
    public static void Play(Action done)
    {
        if (Playing) return;
        var go = new GameObject("IntroCutscene");
        IntroCutscene c = go.AddComponent<IntroCutscene>();
        c.onDone = done;
        c.StartCoroutine(c.Run());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) skip = true;
        if (lanHasSpeed && lanAgent != null && lanAgent.enabled) lanAnim.SetFloat("Speed", lanAgent.velocity.magnitude);
    }

    // ================= Kịch bản =================
    private IEnumerator Run()
    {
        if (!FindPlaces(out Camera playerCam))
        {
            Finish();
            yield break;
        }
        Playing = true;
        UIState.Open(this);
        PlayerController.InputFrozen = true;
        WeatherSystem.ForceWeather(WeatherSystem.Weather.Sunny); // ngày đầu tiên trời đẹp
        HideOtherCanvases();
        HidePlayer(playerCam);
        BuildCamera(playerCam);
        SpeechBubble.ViewCamera = cam;   // lời thoại quay mặt về camera cảnh quay
        BuildUi();
        SpawnLan();
        Vector3 endPos = playerCam.transform.position;
        Quaternion endRot = playerCam.transform.rotation;

        // 0. Tiêu đề
        Vector3 wideA = doorG + outward * 15f - side * 9f + Vector3.up * 0.9f;
        Place(wideA, doorG + side * 2f + Vector3.up * 2.6f, 45f);
        fade.color = Color.black;
        yield return Tween(0.8f, t => titleGroup.alpha = t);
        yield return Wait(2f);
        yield return Tween(0.6f, t => titleGroup.alpha = 1f - t);

        // 1. Hồi tưởng: góc thấp bên kia đường, trượt ngang chậm; cô Lan rao khách
        flashback.weight = 1f;
        StartCoroutine(Tween(1.4f, t => fade.color = new Color(0f, 0f, 0f, 1f - t)));
        Say(lan, "Fresh eggs and hot coffee!", 2.5f, 3.5f);
        yield return Shot(wideA, wideA + side * 5f, doorG + side * 2f + Vector3.up * 2.6f, doorG + side * 1f + Vector3.up * 2.2f, 45f, 40f, 7.5f,
                          "Thirty years ago, a young woman named Lan opened a tiny grocery on this street corner.");

        // Cận cảnh cô Lan (lệch 1/3 khung), rồi cô quay vào trong
        Vector3 lanHead = (lan != null ? lan.transform.position : doorG + outward * 2f) + Vector3.up * 1.55f;
        Vector3 closeA = lanHead + outward * 3.2f + side * 1.6f - Vector3.up * 0.25f;
        Say(lan, "Come in, come in!", 0.8f, 2.5f);
        StartCoroutine(WalkLanLater(2.6f, doorG - outward * 4f));
        yield return Shot(closeA, closeA + outward * 0.5f, lanHead - side * 0.6f, lanHead - side * 0.4f - Vector3.up * 0.2f, 38f, 34f, 5f,
                          "Every morning, the whole street came to her for breakfast.");

        // Chớp trắng -> hiện tại
        yield return Tween(0.35f, t => fade.color = new Color(1f, 1f, 1f, t));
        flashback.weight = 0f;
        if (lan != null) lan.SetActive(false);
        StartCoroutine(Tween(0.7f, t => fade.color = new Color(1f, 1f, 1f, 1f - t)));

        // 2. Hiện tại: lượn vòng quanh cửa hàng (bán kính 45 m, cao 24 m - luôn ở trong thành phố)
        yield return Orbit(45f, 24f, -55f, 5f, 7.5f, "Thirty years later, the street has grown into VietUrban - a city of motorbikes, street food and neon signs.");

        // 3. Mặt tiền cửa hàng: góc thấp, đẩy vào, thu hẹp tiêu cự
        Vector3 heroA = doorG + outward * 18f + side * 3f + Vector3.up * 0.5f;
        yield return Shot(heroA, doorG + outward * 10f + side * 1.5f + Vector3.up * 0.9f, doorG + Vector3.up * 3.2f, doorG + Vector3.up * 2.6f, 52f, 40f, 6.5f,
                          "Grandma Lan's shop is still here. This morning, a letter arrived for you.");

        // 4. Lá thư (tương tác)
        yield return Tween(0.4f, t => captionGroup.alpha = 1f - t);
        yield return LetterRoutine();

        // 5. Vào trong cửa hàng
        yield return Shot(doorG + outward * 3f + Vector3.up * 1.8f, doorG - outward * 5f + Vector3.up * 1.9f,
                          inside + Vector3.up * 1.6f, inside + Vector3.up * 1.2f, 50f, 46f, 6f,
                          "One small hall is open. The rest of the building is empty - for now.");
        yield return Shot(doorG - outward * 5f + Vector3.up * 1.9f, doorG - outward * 6f + side * 1.5f + Vector3.up * 2.1f,
                          inside + Vector3.up * 1.2f, inside + side * 4f + Vector3.up * 1.2f, 46f, 50f, 4.5f,
                          "Stock the shelves, keep the street happy, and grow Grandma's shop into the best supermarket in town.");

        // 6. Về mắt người chơi
        if (!skip)
        {
            Vector3 p0 = cam.transform.position;
            Quaternion r0 = cam.transform.rotation;
            float f0 = cam.fieldOfView;
            yield return Tween(2.2f, t =>
            {
                float k = Smoother(t);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, endPos, k), Quaternion.Slerp(r0, endRot, k));
                cam.fieldOfView = Mathf.Lerp(f0, baseFov, k);
                captionGroup.alpha = 1f - t;
            });
        }
        else yield return Tween(0.25f, t => fade.color = new Color(0f, 0f, 0f, Mathf.Max(fade.color.a, t)), true);
        Finish();
    }

    private void Finish()
    {
        if (SpeechBubble.ViewCamera == cam) SpeechBubble.ViewCamera = null;
        if (cam != null) Destroy(cam.gameObject);
        if (flashback != null) { Destroy(flashback.profile); Destroy(flashback.gameObject); }
        if (lan != null) Destroy(lan);
        foreach (Canvas c in hidden) if (c != null) c.enabled = true;
        hidden.Clear();
        foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
        hiddenRenderers.Clear();
        if (Playing)
        {
            PlayerController.InputFrozen = false;
            UIState.Close(this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        Playing = false;
        Action done = onDone;
        onDone = null;
        Destroy(gameObject);
        done?.Invoke();
    }

    // ================= Lá thư =================
    private IEnumerator LetterRoutine()
    {
        if (skip) yield break;
        // Camera trôi rất chậm trên mặt tiền trong lúc đọc thư
        StartCoroutine(Drift(doorG + outward * 10f + side * 1.5f + Vector3.up * 0.9f, doorG + Vector3.up * 2.6f));
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        page = 0;
        ShowPage();
        yield return Tween(0.8f, t => letterGroup.alpha = t);

        while (!skip && choice < 0) yield return null;
        if (skip) yield break;

        // Lời hồi âm
        choiceButtons.SetActive(false);
        letterBody.text = $"<i>Dear Grandma,\n\n{Replies[choice].Trim('"')}\n\nI will open the shop tomorrow morning. Rest well.</i>";
        letterFooter.text = "<size=80%>Sent.</size>";
        yield return Wait(3.5f);
        yield return Tween(0.6f, t => letterGroup.alpha = 1f - t);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ShowPage()
    {
        letterBody.text = "<i>" + Pages[page] + "</i>";
        letterFooter.text = $"<size=80%>Page {page + 1} / {Pages.Length}</size>";
        bool last = page == Pages.Length - 1;
        letterButtons.SetActive(true);
        choiceButtons.SetActive(false);
        UIKit.SetLabel(nextButton, last ? "Write back" : "Next page");
    }

    private void OnNext()
    {
        AudioManager.Play("ui_click", 0.6f);
        if (page < Pages.Length - 1) { page++; ShowPage(); return; }
        letterButtons.SetActive(false);
        choiceButtons.SetActive(true);
        letterBody.text = "<i>Dear Grandma,\n\n...</i>";
        letterFooter.text = "<size=80%>Choose your reply</size>";
    }

    // ================= Vị trí =================
    private bool FindPlaces(out Camera playerCam)
    {
        playerCam = null;
        foreach (ItemPickup hands in FindObjectsByType<ItemPickup>())
            if (hands != null && hands.playerCamera != null) { playerCam = hands.playerCamera; break; }
        if (playerCam == null) playerCam = Camera.main;
        if (playerCam == null) return false;

        GameObject store = GameObject.Find("Supermarket_full");
        EntranceDoor[] doors = FindObjectsByType<EntranceDoor>();
        if (store == null || doors.Length == 0) return false;
        Bounds sb = default; bool any = false;
        foreach (Renderer r in store.GetComponentsInChildren<Renderer>())
        {
            if (!any) { sb = r.bounds; any = true; } else sb.Encapsulate(r.bounds);
        }
        if (!any) return false;

        Vector3 door = Vector3.zero;
        foreach (EntranceDoor d in doors) door += d.transform.position;
        door /= doors.Length;
        center = new Vector3(sb.center.x, sb.min.y, sb.center.z);
        outward = door - sb.center; outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.back;
        outward.Normalize();
        side = Vector3.Cross(Vector3.up, outward);
        doorG = new Vector3(door.x, sb.min.y, door.z);
        inside = doorG - outward * 12f;
        return true;
    }

    // ================= Cô Lan =================
    private void SpawnLan()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null) return;
        GameObject prefab = spawner.FindPrefab(LanModel);
        if (prefab == null) prefab = spawner.RandomCustomerPrefab();
        if (prefab == null) return;

        Vector3 at = doorG + outward * 2f + side * 1f;
        if (NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas)) at = hit.position;
        var holder = new GameObject("_LanHolder");
        holder.SetActive(false);
        lan = Instantiate(prefab, at, Quaternion.LookRotation(outward), holder.transform);
        foreach (NPCShopper s in lan.GetComponentsInChildren<NPCShopper>(true)) DestroyImmediate(s);
        lan.transform.SetParent(null, true);
        Destroy(holder);
        lan.name = "Young Lan";
        Rigidbody rb = lan.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        lanAgent = lan.GetComponent<NavMeshAgent>();
        if (lanAgent != null) { lanAgent.enabled = true; lanAgent.speed = 1.2f; lanAgent.avoidancePriority = 30; }
        lanAnim = lan.GetComponentInChildren<Animator>();
        if (lanAnim != null)
            foreach (AnimatorControllerParameter p in lanAnim.parameters) if (p.name == "Speed") lanHasSpeed = true;
    }

    private IEnumerator WalkLanLater(float delay, Vector3 to)
    {
        float end = Time.unscaledTime + delay;
        while (Time.unscaledTime < end && !skip) yield return null;
        if (skip || lanAgent == null || !lanAgent.isOnNavMesh) yield break;
        if (NavMesh.SamplePosition(to, out NavMeshHit hit, 4f, NavMesh.AllAreas)) to = hit.position;
        lanAgent.isStopped = false;
        lanAgent.SetDestination(to);
    }

    private void Say(GameObject who, string text, float delay, float duration)
    {
        if (who != null) StartCoroutine(SayLater(who, text, delay, duration));
    }

    private IEnumerator SayLater(GameObject who, string text, float delay, float duration)
    {
        float end = Time.unscaledTime + delay;
        while (Time.unscaledTime < end && !skip) yield return null;
        if (!skip && who != null && who.activeInHierarchy) SpeechBubble.Show(who.transform, text, new Color(1f, 0.92f, 0.65f), 2.1f, duration);
    }

    // ================= Quay phim =================
    /// <summary>Đặt camera + rung tay rất nhẹ (Perlin noise, ±0.35°) cho cảm giác máy quay thật.</summary>
    private void Place(Vector3 pos, Vector3 look, float fov)
    {
        float n = Time.unscaledTime * 0.35f;
        Quaternion shake = Quaternion.Euler((Mathf.PerlinNoise(n, 0.3f) - 0.5f) * 0.7f, (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 0.7f, 0f);
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos) * shake);
        cam.fieldOfView = fov;
    }

    private IEnumerator Shot(Vector3 fromPos, Vector3 toPos, Vector3 fromLook, Vector3 toLook, float fov0, float fov1, float duration, string text)
    {
        if (skip) yield break;
        if (text != null)
        {
            caption.text = text;
            StartCoroutine(Tween(0.6f, t => captionGroup.alpha = t));
        }
        float t0 = Time.unscaledTime;
        while (!skip)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - t0) / duration);
            float k = Smoother(t);
            Place(Vector3.Lerp(fromPos, toPos, k), Vector3.Lerp(fromLook, toLook, k), Mathf.Lerp(fov0, fov1, k));
            if (t >= 1f) break;
            yield return null;
        }
    }

    /// <summary>Lượn vòng quanh cửa hàng (độ, so với hướng ra cửa) ở độ cao cố định, luôn nhìn về cửa hàng.</summary>
    private IEnumerator Orbit(float radius, float height, float fromDeg, float toDeg, float duration, string text)
    {
        if (skip) yield break;
        caption.text = text;
        StartCoroutine(Tween(0.6f, t => captionGroup.alpha = t));
        float t0 = Time.unscaledTime;
        while (!skip)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - t0) / duration);
            float a = Mathf.Lerp(fromDeg, toDeg, Smoother(t));
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * outward;
            Vector3 pos = center + dir * radius + Vector3.up * Mathf.Lerp(height, height * 0.8f, t);
            Place(pos, center + Vector3.up * 4f, 50f);
            if (t >= 1f) break;
            yield return null;
        }
    }

    private IEnumerator Drift(Vector3 pos, Vector3 look)
    {
        float t0 = Time.unscaledTime;
        while (!skip && letterGroup != null && (letterGroup.alpha > 0f || Time.unscaledTime - t0 < 1f))
        {
            float s = (Time.unscaledTime - t0) * 0.08f;
            Place(pos + outward * Mathf.Min(1.5f, s) + Vector3.up * Mathf.Min(0.3f, s * 0.2f), look, 40f);
            yield return null;
        }
    }

    /// <summary>Hồi tưởng bằng hậu kỳ URP (không phủ màu lên giao diện): giảm bão hoà -60, ngả vàng ấm nhẹ, sáng hơn chút, viền tối, hạt phim.</summary>
    private void BuildFlashbackLook()
    {
        var go = new GameObject("FlashbackVolume");
        go.transform.SetParent(transform, false);
        flashback = go.AddComponent<Volume>();
        flashback.isGlobal = true;
        flashback.priority = 100f;
        flashback.weight = 0f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(-60f);
        ca.colorFilter.Override(new Color(1f, 0.93f, 0.8f));
        ca.postExposure.Override(0.25f);
        ca.contrast.Override(-5f);
        Vignette vg = profile.Add<Vignette>(true);
        vg.intensity.Override(0.4f);
        vg.smoothness.Override(0.55f);
        FilmGrain fg = profile.Add<FilmGrain>(true);
        fg.type.Override(FilmGrainLookup.Medium2);
        fg.intensity.Override(0.35f);
        flashback.profile = profile;
    }

    private void BuildCamera(Camera like)
    {
        var go = new GameObject("IntroCamera");
        go.transform.SetParent(transform, false);
        cam = go.AddComponent<Camera>();
        cam.CopyFrom(like);
        baseFov = like.fieldOfView;
        cam.depth = 100f;
        cam.targetTexture = null;
        cam.farClipPlane = Mathf.Max(like.farClipPlane, 600f);
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        BuildFlashbackLook();
    }

    // ================= Giao diện =================
    private void BuildUi()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, 900);
        Transform root = canvas.transform;

        Bar(root, true);
        Bar(root, false);

        GameObject capGo = UIKit.Rect("Caption", root, new Vector2(1500, 110), new Vector2(0, -470));
        captionGroup = capGo.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;
        captionGroup.blocksRaycasts = false;
        caption = UIKit.Text(capGo.transform, "", 32, new Vector2(1500, 110), Vector2.zero, TextAlignmentOptions.Center, false, new Color(1f, 1f, 1f, 0.95f));
        caption.textWrappingMode = TextWrappingModes.Normal;

        UIKit.Text(root, "[Space] Skip", 22, new Vector2(300, 40), new Vector2(800, 505), TextAlignmentOptions.Right, false, new Color(1f, 1f, 1f, 0.6f));

        BuildLetter(root);

        fade = UIKit.FullScreen("Fade", root, Color.black).GetComponent<Image>();
        fade.raycastTarget = false;

        GameObject titleGo = UIKit.Rect("Title", root, new Vector2(1600, 300), Vector2.zero);
        titleGroup = titleGo.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleGroup.blocksRaycasts = false;
        UIKit.Text(titleGo.transform, "VIETURBAN", 110, new Vector2(1600, 140), new Vector2(0, 50), TextAlignmentOptions.Center, true, new Color(1f, 0.84f, 0.3f));
        UIKit.Text(titleGo.transform, "SUPERMARKET SIMULATOR", 44, new Vector2(1600, 70), new Vector2(0, -50), TextAlignmentOptions.Center, true);
    }

    private void BuildLetter(Transform root)
    {
        GameObject letter = UIKit.Panel("Letter", root, new Vector2(860, 640), new Vector2(0, 20), new Color(0.96f, 0.92f, 0.82f, 0.98f));
        letterGroup = letter.AddComponent<CanvasGroup>();
        letterGroup.alpha = 0f;
        Color ink = new Color(0.25f, 0.18f, 0.12f);
        letterBody = UIKit.Text(letter.transform, "", 29, new Vector2(760, 400), new Vector2(0, 85), TextAlignmentOptions.TopLeft, false, ink);
        letterBody.textWrappingMode = TextWrappingModes.Normal;
        letterFooter = UIKit.Text(letter.transform, "", 24, new Vector2(760, 40), new Vector2(0, -150), TextAlignmentOptions.Center, false, new Color(0.45f, 0.3f, 0.15f));

        letterButtons = UIKit.Rect("PageButtons", letter.transform, new Vector2(760, 70), new Vector2(0, -250));
        nextButton = UIKit.Btn(letterButtons.transform, "Next page", new Vector2(300, 60), Vector2.zero, OnNext, new Color(0.45f, 0.3f, 0.15f), 26);

        choiceButtons = UIKit.Rect("Choices", letter.transform, new Vector2(760, 220), new Vector2(0, -240));
        for (int i = 0; i < Replies.Length; i++)
        {
            int captured = i;
            UIKit.Btn(choiceButtons.transform, Replies[i], new Vector2(740, 56), new Vector2(0, 70 - i * 66), () =>
            {
                AudioManager.Play("ui_click", 0.6f);
                choice = captured;
            }, new Color(0.38f, 0.26f, 0.14f), 23);
        }
        choiceButtons.SetActive(false);
    }

    private static void Bar(Transform root, bool top)
    {
        GameObject g = UIKit.Rect(top ? "BarTop" : "BarBottom", root);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 120f);
        rt.anchoredPosition = Vector2.zero;
        Image img = g.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    /// <summary>Ẩn hình nhân vật người chơi (thân, tay, đồ đang cầm) trong lúc chiếu; xong thì hiện lại.</summary>
    private void HidePlayer(Camera playerCam)
    {
        CharacterController cc = playerCam.GetComponentInParent<CharacterController>();
        Transform root = cc != null ? cc.transform : playerCam.transform.root;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hiddenRenderers.Add(r);
        }
    }

    private void HideOtherCanvases()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>())
        {
            if (c == null || !c.enabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.enabled = false;
            hidden.Add(c);
        }
    }

    // ================= Thời gian (không phụ thuộc timeScale) =================
    private static float Smoother(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (!skip && Time.unscaledTime < end) yield return null;
    }

    /// <summary>Chạy step(t) với t từ 0 tới 1 trong seconds giây. Bấm bỏ qua thì nhảy tới cuối (trừ khi runOnSkip).</summary>
    private IEnumerator Tween(float seconds, Action<float> step, bool runOnSkip = false)
    {
        float t0 = Time.unscaledTime;
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - t0) / Mathf.Max(0.01f, seconds));
            step(t);
            if (t >= 1f || (skip && !runOnSkip)) { step(1f); yield break; }
            yield return null;
        }
    }
}
