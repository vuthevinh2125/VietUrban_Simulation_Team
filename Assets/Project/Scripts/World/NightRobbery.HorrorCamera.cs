using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "CAMERA CA ĐÊM" (~75 s): lúc đóng cửa, khách đã về hết, người chơi đang ở trong cửa hàng.
///  1. Đèn tắt bớt; loa siêu thị tự rè rè, có tiếng thì thầm.
///  2. Màn hình an ninh chia 4 ô (cửa ra vào / quầy / dãy kệ / kho): ngoài cửa kính có một "ma váy trắng" tóc xoã, da tái, máu loang.
///     Phóng to: nó áp bàn tay lên kính. Nhìn tận mắt: không có ai - nhưng dấu tay máu trên kính là thật.
///  3. Camera dãy kệ: nó đã ở BÊN TRONG, đầu vặn ngược ra sau. Đèn tắt phụt, đèn sự cố đỏ quạch, máu nhỏ giọt,
///     trên tường hiện dòng chữ máu "MÀY THẤY TAO RỒI". Camera kho: một con quỷ gầy trơ xương bám ngược trên trần.
///  4. Lựa chọn (6 s): ra dãy kệ kiểm tra / chạy ra cửa / trốn sau quầy gọi bảo vệ - mỗi nhánh một màn rượt / hù riêng, máu bắn màn hình.
///  5. "Sáng hôm sau": két thiếu tiền thay bằng tiền âm phủ (nhánh kiểm tra), hoặc chỉ là ký ức rợn người.
/// "Ma" nằm trên layer riêng: camera an ninh thấy, mắt thường không thấy, trừ lúc hù. Báo cáo cuối ngày chờ đoạn phim xong.
/// </summary>
public partial class NightRobbery
{
    private class Feed { public Camera cam; public RenderTexture rt; public RawImage img; public TMP_Text label; }
    private readonly List<Feed> feeds = new List<Feed>();
    private GameObject cctvPanel;
    private RawImage cctvGrain;
    private TMP_Text cctvClock;
    private AudioSource cctvHum;
    private int ghostLayer = -1, cctvFocus = -1;
    private float glitchUntil;
    private Light redLight;

    private IEnumerator RunCamera(bool natural)
    {
        PlayerController pc = StorePlayer();
        Camera main = Camera.main;
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (pc == null || main == null || city == null) yield break;
        cameraRunning = true;
        Playing = true;
        car = null; rail = null;
        if (!mournersPrepped) StartCoroutine(PrepMourners());
        bool wasFrozen = PlayerController.InputFrozen;
        PlayerController.InputFrozen = true;
        Vector3 P = main.transform.position;
        Vector3 F0 = main.transform.forward; F0.y = 0f; F0 = F0.sqrMagnitude > 0.01f ? F0.normalized : Vector3.forward;
        float floorY = GroundAtStore(P - Vector3.up * 1.2f);
        float eyeH = P.y - floorY;
        Vector3 Floor(Vector3 v) => new Vector3(v.x, floorY, v.z);
        Vector3 Eye(Vector3 v) => new Vector3(v.x, floorY + eyeH, v.z);

        // Hai hướng thoáng dài nhất quanh người chơi: "dãy kệ" (ưu tiên hướng đang nhìn) và "kho"
        Vector3 d = F0, d2 = -F0; float L = 6f, L2 = 5f, best = -1f, best2 = -1f;
        var dists = new float[24];
        for (int k = 0; k < 24; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward;
            dists[k] = Physics.Raycast(new Vector3(P.x, floorY + 1f, P.z), dir, out RaycastHit h, 18f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 18f;
            float score = Mathf.Min(dists[k], 14f) + 2.5f * Vector3.Dot(dir, F0);
            if (dists[k] >= 4f && score > best) { best = score; d = dir; L = Mathf.Min(dists[k] - 0.8f, 13f); }
        }
        for (int k = 0; k < 24; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward;
            if (Vector3.Dot(dir, d) > 0.3f) continue;
            if (dists[k] > best2) { best2 = dists[k]; d2 = dir; L2 = Mathf.Clamp(dists[k] - 0.8f, 3f, 12f); }
        }
        float ceil = Physics.Raycast(P, Vector3.up, out RaycastHit up, 6f, ~0, QueryTriggerInteraction.Ignore) ? up.point.y - 0.05f : P.y + 1.6f;
        Vector3 door = StoreDoor(out Vector3 outward, out Bounds storeB);
        door.y = floorY;
        CheckoutCounter counter = CheckoutCounter.All.Count > 0 ? CheckoutCounter.All[0] : FindAnyObjectByType<CheckoutCounter>();
        Vector3 cpos = counter != null ? Floor(counter.transform.position) : Floor(P + F0 * 3f);
        Vector3 far = Floor(P + d * L), mid = Floor(P + d * (L * 0.45f)), behindMe = Floor(P - d * 0.6f);

        // "Ma váy trắng" (layer riêng: chỉ camera an ninh thấy) + con quỷ trong kho
        ghostLayer = FreeLayer();
        Mover ghost = MakeGhoul(ghostPrefab, Floor(door + outward * 0.8f), -outward, GhostDress, 1f, "ghost", true, "ghost");
        if (ghost != null) SetLayer(ghost.t.gameObject, ghostLayer);
        Vector3 hangAt = P + d2 * (L2 * 0.8f);
        HorrorCreature beast = HorrorCreature.Create(Props(), new Vector3(hangAt.x, ceil, hangAt.z), Quaternion.LookRotation(-d2) * Quaternion.Euler(0f, 0f, 180f));
        beast.mode = HorrorCreature.Mode.Hang; beast.twitch = 1.4f;
        beast.SetLayer(ghostLayer);

        BeginFilm();
        if (ghostLayer >= 0) cam.cullingMask &= ~(1 << ghostLayer);
        StoreDoor(out _, out Bounds sb); sb.Expand(2f);
        HideStorePeople(sb);
        GrabStoreLights();
        BuildCctv(new[]
        {
            ("CAM 01 - CỬA RA VÀO", door - outward * 3.2f + Vector3.up * 2.7f, door + outward * 1.5f + Vector3.up * 1f),
            ("CAM 02 - QUẦY THU NGÂN", cpos + (P - cpos).WithY(0f).normalized * 3f + Vector3.up * 2.7f, cpos + Vector3.up * 0.9f),
            ("CAM 03 - DÃY KỆ GIỮA", P - d * 1.2f + Vector3.up * (ceil - P.y - 0.2f), far + Vector3.up * 1f),
            ("CAM 04 - KHO HÀNG", P + d2 * (L2 * 0.15f) + Vector3.up * (ceil - P.y - 0.2f), hangAt + Vector3.up * (ceil - floorY - 1.4f - (hangAt.y - floorY))),
        });

        // 1. Đóng cửa
        DimStoreLights(0.5f, 0f);
        Vector3 head = P;
        yield return Shot(3.2f, "CA ĐÊM", $"{GameClock()} - cửa hàng đã đóng cửa",
            () => (head, head + F0 * 6f + Vector3.down * 0.3f),
            () => Say("Bạn", "Khách về hết rồi. Xem lại camera lần cuối rồi khoá cửa về.", YouSpeak));
        // 2. Loa siêu thị tự rè
        HorrorAudio.Play2D(HorrorAudio.Static(), 0.35f);
        HorrorAudio.Play2D(HorrorAudio.Whisper(), 0.8f);
        yield return Shot(3f, null, null, () => (head, head + F0 * 5f + Vector3.up * 1.2f),
            () => { Say("", "<i>Loa siêu thị bỗng rè rè... rồi một giọng thì thầm, không nghe rõ chữ.</i>", Color.white); DimStoreLights(0.45f, 0.4f); });
        DimStoreLights(0.45f, 0f);
        yield return Shot(2.2f, null, null, () => (head, head + F0 * 5f + Vector3.up * 1.2f),
            () => Say("Bạn", "Ai... ai bật loa đấy?", YouSpeak));
        // 3. Bốn ô camera
        ShowCctv(-2);
        yield return Shot(3.8f, null, null, () => (head, head + F0 * 6f),
            () => Say("", "<i>Bốn ô camera. Cửa ra vào... có ai đó đứng sát ngoài cửa kính.</i>", Color.white));
        ShowCctv(0);
        yield return Shot(1.6f, null, null, () => (head, head + F0 * 6f), () => Say("", "<i>Phóng to CAM 01.</i>", Color.white));
        if (ghost != null && ghost.cp != null) ghost.cp.Play(ClipPlayer.Has("Pointing") ? "Pointing" : "Walking", false, 0.2f, 0.6f);
        HorrorAudio.Play2D(HorrorAudio.Bang(), 0.5f);
        HorrorFx.Decal(HorrorFx.Hand(), door + Vector3.up * 1.35f + outward * 0.06f + Vector3.Cross(Vector3.up, outward) * 0.2f, outward, Vector3.up, new Vector2(0.22f, 0.26f), Props());
        yield return Shot(3.4f, null, null, () => (head, head + F0 * 6f),
            () => Say("", "<i>Một bóng váy trắng, tóc xoã che kín mặt. Nó áp bàn tay lên kính... để lại một vệt máu.</i>", Color.white));
        // 4. Nhìn tận mắt: không có ai, nhưng dấu tay máu là thật
        ShowCctv(-1);
        heartbeat = HorrorAudio.Play2D(HorrorAudio.Heartbeat(), 0.45f, true);
        yield return Shot(3.4f, null, null, () => (Eye(door - outward * 2.5f), door + Vector3.up * 1.35f),
            () => Say("Bạn", "Không có ai... Nhưng vết tay máu kia... là thật!", YouSpeak));
        // 5. Nó đã ở bên trong, đầu vặn ngược
        if (ghost != null)
        {
            ghost.t.SetPositionAndRotation(far, Quaternion.LookRotation(d));      // quay lưng về phía camera...
            Contort c = ghost.t.GetComponent<Contort>();
            if (c != null) { c.speed = 3000f; c.twistTarget = 180f; c.tiltTarget = 30f; c.jitter = 0.9f; }   // ...nhưng đầu vặn ngược nhìn lại
            GhoulIdle(ghost, "Walking");
        }
        ShowCctv(2);
        Glitch(0.9f);
        yield return Shot(3.6f, null, null, () => (head, head + F0 * 6f),
            () => Say("", "<i>CAM 03. Nó đã ở BÊN TRONG cửa hàng, cuối dãy kệ. Thân quay lưng... nhưng cái đầu vặn ngược, nhìn thẳng vào camera.</i>", Color.white));
        // 6. Mất điện, đèn sự cố đỏ, chữ máu
        ShowCctv(-1);
        DimStoreLights(0.03f, 0f);
        redLight = new GameObject("EmergencyLight").AddComponent<Light>();
        redLight.transform.SetParent(Props(), false);
        redLight.transform.position = P + Vector3.up * 1.2f + F0 * 1.5f;
        redLight.type = LightType.Point; redLight.range = 14f; redLight.intensity = 2.4f; redLight.color = new Color(1f, 0.08f, 0.05f);
        HorrorAudio.Play2D(HorrorAudio.Drip(), 0.8f);
        Vector3 wallP = P + F0 * 3f, wallN = -F0;
        if (Physics.Raycast(P, F0, out RaycastHit wh, 8f, ~0, QueryTriggerInteraction.Ignore)) { wallP = wh.point; wallN = wh.normal; }
        HorrorFx.Writing("MÀY THẤY TAO RỒI", wallP.WithY(floorY + 1.6f), wallN, 0.34f, Props());
        HorrorFx.Drips(wallP.WithY(floorY + 2.4f) + wallN * 0.1f, 4f, Props());
        yield return Shot(3.8f, null, null, () => (head, wallP.WithY(floorY + 1.55f)),
            () => Say("", "<i>Đèn tắt phụt. Ánh đèn sự cố đỏ quạch. Trên tường... từng chữ máu từ từ hiện ra.</i>", Color.white));
        // 7. Camera kho: thứ bám trên trần
        ShowCctv(3);
        Glitch(0.5f);
        yield return Shot(3.4f, null, null, () => (head, head + F0 * 6f),
            () => Say("", "<i>CAM 04 - kho hàng. Trên trần... có một thứ gầy trơ xương đang bám ngược. Không phải người.</i>", Color.white));
        ShowCctv(-1);

        int pick = -1;
        yield return Choose(6f, new[] { "Ra dãy kệ kiểm tra", "Chạy ra cửa", "Trốn sau quầy, gọi bảo vệ" }, () => (head, wallP.WithY(floorY + 1.5f)), i => pick = i);
        string morning;
        bool lose = false;
        if (heartbeat != null) { heartbeat.pitch = 1.3f; heartbeat.volume = 0.7f; }
        if (pick == 0)
        {
            // Đi dọc dãy kệ: vệt máu kéo lê, cuối dãy chỉ có vũng máu... quay lại: nó ngay trước mặt rồi lao tới
            if (ghost != null) ghost.t.gameObject.SetActive(false);
            for (int k = 1; k <= 4; k++) HorrorFx.Decal(HorrorFx.Drag(), Floor(P + d * (k * L / 4.5f)) + Vector3.up * 0.012f, Vector3.up, d, new Vector2(0.8f, L / 4f), Props(), 0.95f);
            HorrorFx.Pool(Floor(P + d * (L - 0.3f)) + Vector3.up * 0.014f, 1.8f, Props());
            Vector3 walkEnd = Eye(P + d * (L - 1.2f));
            float t0 = Time.unscaledTime, nextStep = 0f;
            int step = 0;
            Say("Bạn", "Có... có ai ở đây không?", YouSpeak);
            while (Time.unscaledTime - t0 < 5f)
            {
                float u = (Time.unscaledTime - t0) / 5f;
                Vector3 pos = Vector3.Lerp(head, walkEnd, Mathf.SmoothStep(0f, 1f, u)) + Vector3.up * Mathf.Sin(u * 32f) * 0.025f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(d + Vector3.down * 0.25f));
                if (Time.unscaledTime >= nextStep) { nextStep = Time.unscaledTime + 0.55f; AudioManager.Play("footstep_" + (1 + step++ % 4), 0.35f); }
                if (redLight != null) redLight.transform.position = pos + Vector3.up * 0.8f;
                UpdateCctv();
                yield return null;
            }
            yield return Shot(2.4f, null, null, () => (walkEnd, walkEnd + d * 3f + Vector3.down * 1.2f),
                () => Say("", "<i>Vệt máu kéo lê dẫn tới cuối dãy... chỉ còn một vũng máu lớn. Sau lưng, có tiếng thở khò khè.</i>", Color.white));
            HorrorAudio.Play2D(HorrorAudio.Breath(), 0.9f);
            Vector3 gp = Floor(walkEnd - d * 0.9f);
            if (ghost != null)
            {
                ghost.t.gameObject.SetActive(true);
                ghost.t.SetPositionAndRotation(gp, Quaternion.LookRotation(d));
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.twistTarget = 0f; c.tiltTarget = 50f; c.neckStretchTarget = 1.5f; c.armStretchTarget = 1.4f; c.jitter = 1.2f; }
            }
            if (ghostLayer >= 0) cam.cullingMask |= 1 << ghostLayer;
            yield return Turn(walkEnd, walkEnd + d * 3f, Eye(gp) + Vector3.down * 0.1f, 0.35f);
            if (ghost != null) GhoulGo(ghost, Floor(walkEnd), 4.5f, walkEnd);
            HorrorAudio.Play2D(HorrorAudio.Scream(), 1f);
            yield return Shot(0.45f, null, null, () => (walkEnd, Eye(gp) + Vector3.down * 0.1f), () => { });
            Scare(); BloodOnScreen();
            yield return Shot(0.5f, null, null, () => (walkEnd, Eye(gp) + Vector3.down * 0.1f), () => { });
            morning = "Bạn tỉnh dậy trên sàn cửa hàng lúc trời hửng sáng, người dính đầy máu - không phải máu của bạn. Trong két, một xấp tiền âm phủ nằm lẫn với tiền thật.";
            lose = true;
        }
        else if (pick == 1)
        {
            // Chạy ra cửa: cửa khoá; ngoài kính con quỷ lao vào đập, máu quệt kính; quay lại - nó ở ngay sau lưng
            Vector3 atDoor = Eye(door - outward * 0.9f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 1.6f)
            {
                float u = (Time.unscaledTime - t0) / 1.6f;
                Vector3 pos = Vector3.Lerp(head, atDoor, Mathf.SmoothStep(0f, 1f, u)) + Vector3.up * Mathf.Sin(u * 40f) * 0.04f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((door + Vector3.up * 1.3f) - pos));
                UpdateCctv();
                yield return null;
            }
            HorrorAudio.Play2D(HorrorAudio.Bang(), 0.8f);
            yield return Shot(2.2f, null, null, () => (atDoor, door + Vector3.up * 1.3f),
                () => Say("", "<i>Cửa kính khoá chặt. Bạn đập cửa, giật tay nắm... không nhúc nhích.</i>", Color.white));
            beast.transform.SetPositionAndRotation(Floor(door + outward * 0.75f), Quaternion.LookRotation(-outward));
            beast.mode = HorrorCreature.Mode.Reach; beast.SetLayer(0);
            beast.Scream(1f);
            HorrorAudio.Play2D(HorrorAudio.Bang(), 1f);
            for (int k = 0; k < 3; k++)
                HorrorFx.Decal(HorrorFx.Hand(), door + Vector3.up * (1.1f + k * 0.25f) + outward * 0.07f + Vector3.Cross(Vector3.up, outward) * (-0.35f + k * 0.35f), outward, Vector3.up, new Vector2(0.22f, 0.26f), Props());
            shake = 0.8f;
            yield return Shot(2.4f, null, null, () => (atDoor, beast.HeadPos),
                () => Say("", "<i>RẦM! Một thứ gầy trơ xương lao sầm vào kính, há hàm răng nhọn hoắt, móng tay cào ken két.</i>", Color.white));
            HorrorAudio.Play2D(HorrorAudio.Breath(), 1f);
            Vector3 gp = Floor(atDoor - outward * 0.8f);
            if (ghost != null)
            {
                ghost.t.SetPositionAndRotation(gp, Quaternion.LookRotation(outward));
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.twistTarget = 0f; c.tiltTarget = -60f; c.neckStretchTarget = 1.8f; c.jitter = 1.2f; }
            }
            if (ghostLayer >= 0) cam.cullingMask |= 1 << ghostLayer;
            yield return Shot(1.6f, null, null, () => (atDoor, beast.HeadPos), () => Say("", "<i>Sau lưng bạn... có tiếng thở.</i>", Color.white));
            yield return Turn(atDoor, beast.HeadPos, Eye(gp) + Vector3.up * 0.1f, 0.3f);
            Scare(); BloodOnScreen();
            yield return Shot(0.6f, null, null, () => (atDoor, Eye(gp) + Vector3.up * 0.1f), () => { });
            morning = "Bạn không nhớ mình ra khỏi cửa hàng bằng cách nào. Sáng ra, cửa kính chi chít dấu tay máu - in từ BÊN NGOÀI.";
        }
        else
        {
            // Trốn sau quầy: bảo vệ nói điều không thể; đôi chân trần bê bết máu đi ngang; con quỷ thò đầu ngược xuống ngay trước mặt
            Vector3 hide = Floor(cpos - (P - cpos).WithY(0f).normalized * 0.9f) + Vector3.up * 0.75f;
            Vector3 lookOut = Floor(cpos + (P - cpos).WithY(0f).normalized * 2.5f) + Vector3.up * 0.2f;
            HorrorAudio.Play2D(HorrorAudio.PhoneBuzz(), 0.6f);
            yield return Shot(3.6f, null, null, () => (hide, lookOut),
                () => Say("Bảo vệ (điện thoại)", "Alô? Cửa hàng anh khoá rồi mà. Với lại... camera số 3 với số 4 hỏng từ tuần trước, đã thay đâu?", new Color(0.8f, 0.9f, 1f)));
            if (ghost != null)
            {
                ghost.t.SetPositionAndRotation(Floor(lookOut - Vector3.Cross(Vector3.up, (lookOut - hide).WithY(0f).normalized) * 3f), Quaternion.identity);
                ghost.t.gameObject.SetActive(true);
                GhoulGo(ghost, Floor(lookOut + Vector3.Cross(Vector3.up, (lookOut - hide).WithY(0f).normalized) * 3f), 0.6f);
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.twistTarget = 0f; c.tiltTarget = 0f; c.jitter = 0.6f; }
            }
            if (ghostLayer >= 0) cam.cullingMask |= 1 << ghostLayer;
            StartCoroutine(Footsteps(6, 0.75f));
            yield return Shot(5f, null, null, () => (hide, lookOut),
                () => Say("", "<i>Bạn bịt chặt miệng. Một đôi chân trần bê bết máu chậm rãi đi ngang qua quầy... để lại dấu chân đỏ.</i>", Color.white));
            if (heartbeat != null) heartbeat.Stop();
            yield return Shot(2f, null, null, () => (hide, lookOut), () => Say("", "<i>Im lặng. Hoàn toàn im lặng.</i>", Color.white));
            Vector3 faceAt = hide + (lookOut - hide).normalized * 0.75f;
            beast.SetLayer(0);
            beast.mode = HorrorCreature.Mode.Hang;
            beast.transform.SetPositionAndRotation(new Vector3(faceAt.x, faceAt.y + 2.25f, faceAt.z), Quaternion.LookRotation((faceAt - hide).WithY(0f)) * Quaternion.Euler(0f, 0f, 180f));
            yield return Shot(1.4f, null, null, () => (hide, hide + Vector3.up * 2f + (lookOut - hide).normalized * 0.4f),
                () => Say("", "<i>Có thứ gì đó nhỏ giọt lên vai bạn. Bạn từ từ ngẩng lên...</i>", Color.white));
            HorrorAudio.Play2D(HorrorAudio.Drip(), 1f);
            beast.Scream(1f);
            yield return Turn(hide, hide + Vector3.up * 2f + (lookOut - hide).normalized * 0.4f, beast.HeadPos, 0.25f);
            Scare(); BloodOnScreen();
            yield return Shot(0.6f, null, null, () => (hide, beast.HeadPos), () => { });
            morning = "Bảo vệ tới thì cửa hàng trống trơn. Trên trần nhà phía trên quầy... có những vết móng cào sâu hoắm, vẫn còn rỉ máu.";
        }

        // Màn đen -> sáng hôm sau
        if (heartbeat != null) Destroy(heartbeat.gameObject);
        heartbeat = null;
        yield return BlackHold(4f, "SÁNG HÔM SAU", $"<i>{morning}</i>");
        RemoveCctv();
        ClearGhouls();
        ClearProps();
        if (screenBlood != null) Destroy(screenBlood.gameObject);
        screenBlood = null;
        RestoreStoreLights();
        ShowStorePeople();
        EndFilm();
        PlayerController.InputFrozen = wasFrozen;
        Playing = false;
        cameraRunning = false;
        if (lose)
        {
            EconomyManager eco = EconomyManager.Instance;
            float cash = eco != null ? Mathf.Round(Mathf.Clamp(eco.Money * 0.08f, 20f, 90f)) : 0f;
            if (eco != null && cash > 0f) eco.Charge(cash, MoneyCategory.Other, "Cash swapped for hell money (night shift)");
            ProgressHUD.Toast($"<color=#FF8A7A>Két thu ngân thiếu {EconomyManager.Format(cash)} - thay vào đó là một xấp tiền âm phủ dính máu.</color>");
        }
        else ProgressHUD.Toast("<color=#BFD7FF>Ca đêm kết thúc. Bạn tự hứa sẽ không bao giờ ở lại cửa hàng một mình nữa.</color>");
        horrorNews = "<b>BẢN TIN SÁNG</b> Người dân quanh siêu thị VietUrban kể đêm qua nghe tiếng thét và thấy một bóng váy trắng đứng sau cửa kính rất lâu sau giờ đóng cửa.";
    }

    private IEnumerator Footsteps(int n, float gap)
    {
        for (int k = 0; k < n; k++)
        {
            AudioManager.Play("footstep_" + (1 + k % 4), 0.3f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < gap) yield return null;
        }
    }

    private static string GameClock()
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? gm.ClockString : "12:00 AM";
    }

    // ================= Màn hình camera an ninh (4 ô) =================
    private void BuildCctv((string label, Vector3 pos, Vector3 look)[] cams)
    {
        RemoveCctv();
        cctvPanel = UIKit.FullScreen("CctvScreen", canvas.transform, Color.black);
        cctvPanel.GetComponent<Image>().raycastTarget = false;
        cctvPanel.transform.SetSiblingIndex(0);   // dưới khung đen điện ảnh / phụ đề
        Camera like = Camera.main;
        foreach (var c in cams)
        {
            var go = new GameObject("CCTV_" + c.label);
            go.transform.SetParent(Props(), false);
            var cm = go.AddComponent<Camera>();
            if (like != null) cm.CopyFrom(like);
            cm.cullingMask = ~0;
            cm.transform.SetPositionAndRotation(c.pos, Quaternion.LookRotation(c.look - c.pos));
            cm.fieldOfView = 70f; cm.depth = 90f;
            var rt = new RenderTexture(400, 225, 16) { filterMode = FilterMode.Point, name = "CCTV" };
            cm.targetTexture = rt; cm.enabled = false;
            var img = UIKit.Rect("Feed", cctvPanel.transform).AddComponent<RawImage>();
            img.texture = rt; img.raycastTarget = false;
            img.color = new Color(0.72f, 0.86f, 0.78f);
            TMP_Text t = UIKit.Text(img.transform, c.label, 26, new Vector2(800, 40), Vector2.zero, TextAlignmentOptions.TopLeft, true, new Color(0.92f, 0.97f, 0.92f));
            var trt = t.rectTransform; trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 1f); trt.anchoredPosition = new Vector2(18f, -12f);
            feeds.Add(new Feed { cam = cm, rt = rt, img = img, label = t });
        }
        var noise = new Texture2D(160, 90, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[160 * 90];
        for (int i = 0; i < px.Length; i++) { byte v = (byte)Random.Range(0, 256); px[i] = new Color32(v, v, v, (byte)Random.Range(20, 70)); }
        noise.SetPixels32(px); noise.Apply();
        cctvGrain = UIKit.Rect("Grain", cctvPanel.transform).AddComponent<RawImage>();
        Stretch(cctvGrain.rectTransform, Vector2.zero, Vector2.one);
        cctvGrain.texture = noise; cctvGrain.raycastTarget = false;
        cctvClock = UIKit.Text(cctvPanel.transform, "", 30, new Vector2(700, 44), new Vector2(560, -330), TextAlignmentOptions.Right, true, new Color(0.92f, 0.97f, 0.92f));
        cctvPanel.SetActive(false);
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = new Vector2(3f, 3f); rt.offsetMax = new Vector2(-3f, -3f);
    }

    /// <summary>-1 tắt, -2 lưới 4 ô, 0..3 phóng to một camera.</summary>
    private void ShowCctv(int mode)
    {
        if (cctvPanel == null) return;
        cctvFocus = mode;
        cctvPanel.SetActive(mode != -1);
        for (int i = 0; i < feeds.Count; i++)
        {
            Feed f = feeds[i];
            bool on = mode == -2 || mode == i;
            f.cam.enabled = on;
            f.img.gameObject.SetActive(on);
            if (mode == -2) Stretch(f.img.rectTransform, new Vector2(i % 2 * 0.5f, i < 2 ? 0.5f : 0f), new Vector2(i % 2 * 0.5f + 0.5f, i < 2 ? 1f : 0.5f));
            else if (on) Stretch(f.img.rectTransform, Vector2.zero, Vector2.one);
        }
        if (mode != -1 && cctvHum == null) cctvHum = HorrorAudio.Play2D(HorrorAudio.Static(), 0.12f, true);
        if (mode == -1 && cctvHum != null) { Destroy(cctvHum.gameObject); cctvHum = null; }
    }

    private void Glitch(float seconds)
    {
        glitchUntil = Time.unscaledTime + seconds;
        HorrorAudio.Play2D(HorrorAudio.Static(), 0.5f);
    }

    /// <summary>Mỗi frame trong đoạn phim cửa hàng: hạt nhiễu chạy, giờ chạy, nhiễu sọc khi glitch.</summary>
    private void UpdateCctv()
    {
        if (cctvPanel == null || !cctvPanel.activeSelf) return;
        if (cctvGrain != null) cctvGrain.uvRect = new Rect(Random.value, Random.value, 1f, 1f);
        if (cctvClock != null) cctvClock.text = (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "<color=#FF3B30>●</color> REC  " : "      ") + GameClock();
        bool g = Time.unscaledTime < glitchUntil;
        foreach (Feed f in feeds)
        {
            if (f.img == null || !f.img.gameObject.activeSelf) continue;
            f.img.uvRect = g ? new Rect(Random.Range(-0.05f, 0.05f), Random.Range(-0.03f, 0.03f), 1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            f.img.color = g && Random.value < 0.4f ? new Color(0.95f, 0.98f, 0.95f) : new Color(0.72f, 0.86f, 0.78f);
        }
    }

    private void RemoveCctv()
    {
        if (cctvHum != null) Destroy(cctvHum.gameObject);
        cctvHum = null;
        foreach (Feed f in feeds)
        {
            if (f.cam != null) Destroy(f.cam.gameObject);
            if (f.rt != null) { f.rt.Release(); Destroy(f.rt); }
        }
        feeds.Clear();
        if (cctvPanel != null) Destroy(cctvPanel);
        cctvPanel = null; cctvGrain = null; cctvClock = null; cctvFocus = -1;
        if (redLight != null) Destroy(redLight.gameObject);
        redLight = null;
    }
}
