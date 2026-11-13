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
        var playerRenderers = new List<Renderer>();                  // ẩn thân người chơi (máy quay phim nhìn từ chỗ đứng của bạn)
        foreach (Renderer r in pc.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; playerRenderers.Add(r); }
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
        // Mặt kính cửa thật (dò tia từ ngoài đường vào) để dấu tay nằm đúng trên kính; chỗ đứng nhìn tận mắt lùi vào trong
        // vừa đủ, không lọt vào tường / kệ (máy quay không bị che)
        Vector3 glassSide = Vector3.Cross(Vector3.up, outward);
        Vector3 handAt = Floor(door) + Vector3.up * 1.35f + glassSide * 0.2f + outward * 0.06f;
        Vector3 probe = Floor(door + outward * 2.5f) + Vector3.up * 1.35f + glassSide * 0.2f;
        if (Physics.Raycast(probe, -outward, out RaycastHit gh, 4f, ~0, QueryTriggerInteraction.Ignore) && gh.distance > 1.2f)
            handAt = gh.point + outward * 0.012f;
        float backOff = 2.5f;
        if (Physics.Raycast(handAt - outward * 0.15f, -outward, out RaycastHit bh, 3f, ~0, QueryTriggerInteraction.Ignore))
            backOff = Mathf.Clamp(bh.distance - 0.45f, 0.8f, 2.5f);
        Vector3 seeEye = Eye(handAt - outward * (0.15f + backOff)) + Vector3.down * 0.05f;
        Vector3 far = Floor(P + d * L), mid = Floor(P + d * (L * 0.45f)), behindMe = Floor(P - d * 0.6f);

        // "Ma váy trắng" (layer riêng: chỉ camera an ninh thấy) + con quỷ trong kho
        ghostLayer = FreeLayer();
        Mover ghost = MakeGhoul(ghostPrefab, Floor(door + outward * 0.8f), -outward, GhostDress, 1f, "ghost", true, "ghost");
        if (ghost != null) SetLayer(ghost.t.gameObject, ghostLayer);
        Vector3 hangAt = P + d2 * (L2 * 0.8f);
        HorrorCreature beast = HorrorCreature.Create(Props(), new Vector3(hangAt.x, ceil, hangAt.z), Quaternion.LookRotation(-d2) * Quaternion.Euler(0f, 0f, 180f));
        beast.mode = HorrorCreature.Mode.Hang; beast.twitch = 1.4f;
        beast.SetLayer(ghostLayer);
        beast.gameObject.SetActive(false);                                      // chỉ hiện ở nhánh chạy ra cửa / trốn sau quầy

        BeginFilm();
        StartDread();
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
        HorrorFx.Decal(HorrorFx.Hand(), handAt, outward, Vector3.up, new Vector2(0.24f, 0.28f), Props());
        HorrorFx.Decal(HorrorFx.Splat(), handAt + Vector3.down * 0.25f + outward * 0.002f, outward, Vector3.up, new Vector2(0.18f, 0.5f), Props(), 0.85f);   // máu chảy xuống từ dấu tay
        yield return Shot(3f, null, null, () => (head, head + F0 * 6f),
            () => Say("", "<i>Một bóng váy trắng, tóc xoã che kín mặt. Nó áp bàn tay lên kính... để lại một vệt máu.</i>", Color.white));
        // 4. Nhìn tận mắt: không có ai, nhưng dấu tay máu là thật
        ShowCctv(-1);
        heartbeat = HorrorAudio.Play2D(HorrorAudio.Heartbeat(), 0.45f, true);
        yield return Shot(3.4f, null, null, () => (seeEye, handAt),
            () => Say("Bạn", "Không có ai... Nhưng vết tay máu kia... là thật!", YouSpeak));
        // 5. Nó đã ở bên trong, đầu vặn ngược
        if (ghost != null)
        {
            ghost.t.SetPositionAndRotation(far, Quaternion.LookRotation(d));      // quay lưng về phía camera...
            Contort c = ghost.t.GetComponent<Contort>();
            if (c != null) { c.speed = 3000f; c.twistTarget = 180f; c.tiltTarget = 30f; c.jitter = 0.9f; }   // ...nhưng đầu vặn ngược nhìn lại
            GhoulIdle(ghost);                                                     // đứng im lắc lư (không đứng khựng giữa bước đi)
        }
        ShowCctv(2);
        Glitch(0.9f);
        yield return Shot(3.6f, null, null, () => (head, head + F0 * 6f),
            () => Say("", "<i>CAM 03. Nó đã ở BÊN TRONG cửa hàng, cuối dãy kệ. Thân quay lưng... nhưng cái đầu vặn ngược, nhìn thẳng vào camera.</i>", Color.white));
        ShowCctv(-1);
        // 6. Mất điện, đèn sự cố đỏ, chữ máu
        DimStoreLights(0.03f, 0f);
        redLight = new GameObject("EmergencyLight").AddComponent<Light>();
        redLight.transform.SetParent(Props(), false);
        redLight.transform.position = P + Vector3.up * 1.2f + F0 * 1.5f;
        redLight.type = LightType.Point; redLight.range = 14f; redLight.intensity = 2.4f; redLight.color = new Color(1f, 0.08f, 0.05f);
        HorrorAudio.Play2D(HorrorAudio.Drip(), 0.8f);
        // Chọn một mảng TƯỜNG thật (thẳng đứng, rộng, cách 3-9 m, trong tầm nhìn) để viết chữ máu - không dán lên kệ sát mặt
        Vector3 F0txt = F0;
        Vector3 wallP = P + F0 * 4f, wallN = -F0; float wallD = 4f, bestWall = -1f;
        for (int k = -6; k <= 6; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 10f, 0f) * F0;
            if (!Physics.Raycast(new Vector3(P.x, floorY + 1.7f, P.z), dir, out RaycastHit wh, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (wh.distance < 3f || Mathf.Abs(wh.normal.y) > 0.3f) continue;
            Bounds cb = wh.collider.bounds;
            if (Mathf.Max(cb.size.x, cb.size.z) < 2.5f || cb.size.y < 1.8f) continue;           // phải là mảng tường lớn
            float score = 3f - Mathf.Abs(wh.distance - 5.5f) * 0.4f - Mathf.Abs(k) * 0.12f + Vector3.Dot(-dir, wh.normal);
            if (score > bestWall) { bestWall = score; wallP = wh.point; wallN = wh.normal; wallD = wh.distance; }
        }
        wallP = wallP.WithY(floorY + 1.7f);
        // Chữ máu viết ngay dưới SÀN trước mặt (chỗ chắc chắn nhìn thấy, không bị kệ che)
        Vector3 floorTxt = Floor(P + F0 * 2.3f);
        for (int k = 0; k < 9; k++)
        {
            Vector3 dirF = Quaternion.Euler(0f, (k % 2 == 0 ? 1 : -1) * (k / 2) * 12f, 0f) * F0;
            Vector3 cand = Floor(P + dirF * (k < 5 ? 2.3f : 1.8f)) + Vector3.up * 0.02f;
            if (!Physics.Linecast(head, cand + Vector3.up * 0.05f, ~0, QueryTriggerInteraction.Ignore)
                && !Physics.CheckSphere(cand + Vector3.up * 0.3f, 0.25f, ~0, QueryTriggerInteraction.Ignore)) { floorTxt = cand; F0txt = dirF; break; }
        }
        HorrorFx.Writing("MÀY THẤY TAO RỒI", floorTxt, Vector3.up, 0.3f, Props(), F0txt);
        HorrorFx.Pool(floorTxt + F0txt * 0.6f, 1.4f, Props());
        HorrorFx.Decal(HorrorFx.Drag(), floorTxt - F0txt * 0.7f + Vector3.up * 0.004f, Vector3.up, F0txt, new Vector2(0.7f, 1.6f), Props(), 0.9f);
        yield return Shot(3.4f, null, null, () => (head, floorTxt),
            () => Say("", "<i>Đèn tắt phụt. Ánh đèn sự cố đỏ quạch. Bạn nhìn xuống... ngay dưới chân, từng chữ máu từ từ hiện ra.</i>", Color.white));
        // 8. MỒI NHỬ: tiếng hàng rơi ở dãy kệ (dãy đã ghi bằng Shift+F5; chưa ghi thì dùng dãy trước mặt bạn)
        bool hasSpot = LoadF1Spot(out Vector3 spotPos, out Vector3 spotFwd);
        Vector3 S = hasSpot ? Floor(spotPos) : Floor(P);                           // đầu dãy kệ (chỗ đứng nhìn xuống dãy)
        Vector3 dA = hasSpot ? spotFwd : F0;                                       // hướng nhìn xuống dãy
        Vector3 o1 = S + Vector3.up * 1f;
        float LA = Physics.Raycast(o1, dA, out RaycastHit ah, 25f, ~0, QueryTriggerInteraction.Ignore) ? ah.distance - 0.5f : 25f;
        Vector3 sideV = Vector3.Cross(Vector3.up, dA);                             // bên phải khi nhìn xuống dãy
        float endT = Mathf.Max(3f, LA - 1.2f);
        bool hadShelf = false;
        for (float t = 1.2f; t < LA - 0.5f; t += 0.4f)                              // cuối dãy = chỗ hai bên hết kệ (lối đi ngang)
        {
            Vector3 q = o1 + dA * t;
            bool sl = Physics.Raycast(q, -sideV, 1.6f, ~0, QueryTriggerInteraction.Ignore), sr = Physics.Raycast(q, sideV, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            if (sl || sr) hadShelf = true;
            else if (hadShelf) { endT = t + 0.5f; break; }
        }
        Vector3 endX = S + dA * endT;
        Vector3 aisleEnd = Eye(endX) + Vector3.down * 0.3f;
        Vector3 dropAt = S + dA * (endT * 0.5f);
        SpawnFallingGoods(dropAt + sideV * 0.6f + Vector3.up * 1.5f, -sideV);
        AudioClip drop = Resources.Load<AudioClip>("Audio/box_drop");
        for (int k = 0; k < 3; k++)
            if (drop != null) StartCoroutine(PlayLater(drop, dropAt + Vector3.up * 0.8f, 1f, 0.05f + k * 0.28f));
            else StartCoroutine(PlayLater(HorrorAudio.Bang(), dropAt + Vector3.up * 0.8f, 0.6f, 0.05f + k * 0.28f));
        Vector3 toAisle = Eye(S) + dA * 3f + Vector3.down * 0.3f;
        yield return Turn(head, head + F0 * 6f, toAisle, 0.7f);
        yield return Shot(2.8f, null, null, () => (head, toAisle),
            () => Say("", "<i>RẦM... lộp cộp lộp cộp. Có tiếng hàng rơi loảng xoảng trong dãy kệ đằng kia.</i>", Color.white));
        yield return Shot(2f, null, null, () => (head, toAisle), () => Say("Bạn", "Hàng rơi à...?", YouSpeak));

        int pick = -1;
        yield return Choose(6f, new[] { "Ra cuối dãy kiểm tra", "Chạy ra cửa", "Trốn sau quầy, gọi bảo vệ" }, () => (head, aisleEnd), i => pick = i);
        string morning;
        bool lose = false;
        if (heartbeat != null) { heartbeat.pitch = 1.3f; heartbeat.volume = 0.7f; }
        if (pick == 0)
        {
            // Đi ra đầu dãy kệ -> ma chạy vụt qua cuối dãy (phải sang trái) -> đi xuống cuối dãy ngó trái phải: không có ai ->
            // vài giây sau quay về quầy thu ngân: ma bay lao tới hù
            if (ghost != null) ghost.t.gameObject.SetActive(false);                   // không để con ma đứng sẵn đâu đó trên đường đi
            if (ghostLayer >= 0) cam.cullingMask |= 1 << ghostLayer;
            if (heartbeat != null) { heartbeat.volume = 0.45f; heartbeat.pitch = 1.05f; }
            Vector3 atS = Eye(S);
            Vector3 downAisle = atS + dA * 6f + Vector3.down * 0.3f;
            float walkT = Mathf.Clamp(Vector3.Distance(head.WithY(0f), atS.WithY(0f)) / 1.4f, 1.2f, 8f);
            Say("", "<i>Bạn rời quầy, chậm rãi đi ra phía dãy kệ.</i>", Color.white);
            yield return Walk(head, atS, atS + dA * 0.1f + Vector3.down * 0.1f + (atS - head).WithY(0f).normalized * 3f, walkT);
            yield return Turn(atS, atS + (atS - head).WithY(0f).normalized * 3f, downAisle, 0.8f);
            yield return Shot(2f, null, null, () => (atS, downAisle), () => Say("", "<i>Hàng rơi vương vãi giữa lối đi. Dãy kệ tối om, chỉ có ánh đèn đỏ.</i>", Color.white));
            // Bóng ma chạy vụt ngang qua cuối dãy: từ PHẢI sang TRÁI
            if (ghost != null)
            {
                ghost.t.gameObject.SetActive(true);
                ghost.t.SetPositionAndRotation(endX + sideV * 4.5f, Quaternion.LookRotation(-sideV));
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.twistTarget = 0f; c.tiltTarget = 15f; c.pitchTarget = 0f; c.neckStretchTarget = 1f; c.armStretchTarget = 1.2f; c.jitter = 0.3f; }
                GhoulGo(ghost, endX - sideV * 4.5f, 9f);
            }
            HorrorAudio.Play3D(HorrorAudio.Wind(), endX + Vector3.up * 1.2f, 0.8f, false, 6f, 40f);
            StartCoroutine(Footsteps(6, 0.11f));
            yield return Shot(1.1f, null, null, () => (atS, aisleEnd + Vector3.up * 0.25f), () => Say("", "<i>VỤT! Một bóng trắng chạy vụt qua cuối dãy.</i>", Color.white));
            if (ghost != null) ghost.t.gameObject.SetActive(false);
            if (heartbeat != null) { heartbeat.volume = 0.85f; heartbeat.pitch = 1.3f; }
            yield return Shot(1.8f, null, null, () => (atS, aisleEnd), () => Say("Bạn", "Cái gì... vừa chạy qua?!", YouSpeak));
            // Đi xuống cuối dãy, ngó trái phải
            Vector3 atEnd = Eye(endX);
            yield return Walk(atS, atEnd, atEnd + dA * 3f + Vector3.down * 0.3f, Mathf.Clamp(endT / 1.3f, 2f, 7f));
            Vector3 lookF = atEnd + dA * 4f + Vector3.down * 0.2f, lookL = atEnd - sideV * 5f + Vector3.down * 0.2f, lookR = atEnd + sideV * 5f + Vector3.down * 0.2f;
            Say("", "<i>Bạn ra tới cuối dãy. Ngó sang trái...</i>", Color.white);
            yield return Turn(atEnd, lookF, lookL, 0.9f);
            yield return HoldLook(atEnd, lookL, 1f);
            Say("", "<i>...ngó sang phải...</i>", Color.white);
            yield return Turn(atEnd, lookL, lookR, 1.3f);
            yield return HoldLook(atEnd, lookR, 1f);
            yield return Turn(atEnd, lookR, lookF, 0.8f);
            Say("Bạn", "Không có ai cả... chắc mình hoa mắt rồi.", YouSpeak);
            float tq = Time.unscaledTime;
            while (Time.unscaledTime - tq < 3.5f)                                   // thả lỏng giả: tim chậm dần rồi tắt
            {
                float u = (Time.unscaledTime - tq) / 3.5f;
                if (heartbeat != null) { heartbeat.volume = Mathf.Lerp(0.6f, 0f, u); heartbeat.pitch = Mathf.Lerp(1.1f, 0.9f, u); }
                Vector3 sway = new Vector3(Mathf.Sin(u * 7f) * 0.01f, Mathf.Sin(u * 11f) * 0.008f, 0f);
                cam.transform.SetPositionAndRotation(atEnd + sway, Quaternion.LookRotation(lookF - atEnd));
                UpdateGhouls(Time.deltaTime);
                yield return null;
            }
            // Quay người, đi ngược về phía quầy thu ngân... tới GIỮA dãy thì ma bay lao tới từ đầu dãy
            Vector3 lookBack = atS + Vector3.down * 0.2f;
            Say("", "<i>Bạn thở ra, quay người đi về quầy thu ngân...</i>", Color.white);
            yield return Turn(atEnd, lookF, lookBack, 1f);
            Vector3 midA = Eye(S + dA * (endT * 0.5f));
            yield return Walk(atEnd, midA, lookBack, Mathf.Clamp(endT * 0.5f / 1.2f, 1.5f, 4f));
            yield return HoldLook(midA, lookBack, 0.5f);
            Say("", "", Color.white);
            GameObject pf = RigMonster.Prefab("WhiteClown") ?? FiendPrefab(true);
            Vector3 flyFrom = atS - dA * 2f + Vector3.down * 0.25f;
            RigMonster demon = pf != null ? RigMonster.Create(Props(), flyFrom, Quaternion.identity, pf, 1.95f) : null;
            if (demon != null) DressFiend(demon, false, false);
            HorrorAudio.Play2D(HorrorAudio.Scream(), 1f);
            HorrorAudio.Play2D(HorrorAudio.Sting(), 1f);
            yield return FlyAt(demon, flyFrom, midA, 1f);
            shake = 1f;
            BloodOnScreen();
            yield return HoldLook(midA, demon != null ? demon.HeadPos : lookBack, 0.7f);
            if (demon != null) Destroy(demon.gameObject);
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
            beast.gameObject.SetActive(true);
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
            Vector3 outDir = (lookOut - hide).WithY(0f).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, outDir);
            Vector3 front = Floor(hide + outDir * 4.2f);                          // cách chỗ trốn ~4 m: thấy rõ toàn thân
            hide += across * 0.55f + Vector3.up * 0.15f;                          // ló ra từ cạnh quầy
            lookOut = front + Vector3.up * 0.95f;
            cam.fieldOfView = 58f;
            if (ghost != null)
            {
                ghost.t.SetPositionAndRotation(Floor(front - across * 4f), Quaternion.LookRotation(across));
                ghost.t.gameObject.SetActive(true);
                GhoulGo(ghost, front, 0.85f);
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.twistTarget = 0f; c.tiltTarget = 0f; c.pitchTarget = 0f; c.jitter = 0.6f; }
            }
            if (ghostLayer >= 0) cam.cullingMask |= 1 << ghostLayer;
            StartCoroutine(Footsteps(6, 0.75f));
            yield return Shot(4.6f, null, null, () => (hide, lookOut),
                () => Say("", "<i>Bạn bịt chặt miệng. Một đôi chân trần bê bết máu chậm rãi đi tới... để lại dấu chân đỏ.</i>", Color.white));
            // Nó dừng ngay trước mặt, từ từ cúi gập cổ xuống nhìn vào chỗ bạn trốn; máu nhỏ giọt xuống sàn
            if (ghost != null)
            {
                ghost.t.position = front; ghost.arrived = true; ghost.face = hide; GhoulIdle(ghost);
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.speed = 60f; c.pitchTarget = 70f; c.tiltTarget = 25f; c.neckStretchTarget = 1.4f; c.jitter = 0.3f; }
                HorrorFx.Drips(front + Vector3.up * 1.45f + (hide - front).WithY(0f).normalized * 0.25f, 7f, Props());
                for (int k = 0; k < 4; k++) HorrorFx.Decal(HorrorFx.Splat(), front + (hide - front).WithY(0f).normalized * (0.3f + k * 0.12f) + across * Random.Range(-0.2f, 0.2f) + Vector3.up * 0.012f, Vector3.up, across, new Vector2(0.25f, 0.25f), Props(), 0.9f);
            }
            HorrorAudio.Play2D(HorrorAudio.Breath(), 0.8f);
            HorrorAudio.Play2D(HorrorAudio.Drip(), 0.8f);
            yield return Shot(3.6f, null, null, () => (hide, front + Vector3.up * 1.1f),
                () => Say("", "<i>Nó dừng lại. Ngay trước mặt bạn. Cái cổ từ từ gập xuống... tóc xoã chạm đất, máu nhỏ tong tỏng. Nó đang tìm.</i>", Color.white));
            // ...rồi bỏ đi
            if (ghost != null)
            {
                Contort c = ghost.t.GetComponent<Contort>(); if (c != null) { c.speed = 240f; c.pitchTarget = 0f; c.tiltTarget = 0f; c.neckStretchTarget = 1f; }
                GhoulGo(ghost, Floor(front + across * 6f), 0.85f);
            }
            StartCoroutine(Footsteps(5, 0.75f));
            yield return Shot(3f, null, null, () => (hide, lookOut + across * 1.5f),
                () => Say("", "<i>Rồi nó lê bước đi tiếp... khuất dần.</i>", Color.white));
            if (ghost != null) ghost.t.gameObject.SetActive(false);
            if (heartbeat != null) heartbeat.Stop();
            yield return Shot(2f, null, null, () => (hide, lookOut), () => Say("", "<i>Im lặng. Hoàn toàn im lặng.</i>", Color.white));
            Vector3 faceAt = hide + (lookOut - hide).normalized * 0.75f;
            beast.gameObject.SetActive(true);
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
        StopDread();
        EndFilm();
        PlayerController.InputFrozen = wasFrozen;
        foreach (Renderer r in playerRenderers) if (r != null) r.enabled = true;
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

    /// <summary>Đi bộ (máy quay ngang tầm mắt, nhún theo bước, tiếng chân) từ a tới b, mắt nhìn về look.</summary>
    private IEnumerator Walk(Vector3 a, Vector3 b, Vector3 look, float seconds)
    {
        float t0 = Time.unscaledTime, nextStep = 0f; int step = 0;
        while (Time.unscaledTime - t0 < seconds)
        {
            float u = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - t0) / seconds);
            Vector3 pos = Vector3.Lerp(a, b, u) + Vector3.up * Mathf.Sin(u * seconds * 11f) * 0.02f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            if (Time.unscaledTime >= nextStep) { nextStep = Time.unscaledTime + 0.55f; AudioManager.Play("footstep_" + (1 + step++ % 4), 0.35f); }
            if (redLight != null) redLight.transform.position = pos + Vector3.up * 0.8f;
            UpdateCctv(); UpdateGhouls(Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>Đứng yên nhìn một điểm trong một lúc.</summary>
    private IEnumerator HoldLook(Vector3 pos, Vector3 look, float seconds)
    {
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < seconds) { cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos)); UpdateGhouls(Time.deltaTime); yield return null; }
    }

    /// <summary>
    /// Quỷ bay lao tới mặt: người nằm ngang, hai tay duỗi thẳng ra trước đòi vồ, mặt ngẩng lên nhìn; xa thì chậm (thấy rõ), gần thì vụt tới.
    /// Máy quay đứng ở eye, nhìn theo nó.
    /// </summary>
    private IEnumerator FlyAt(RigMonster m, Vector3 from, Vector3 eye, float dur)
    {
        Vector3 endP = eye + (from - eye).WithY(0f).normalized * 0.5f + Vector3.down * 0.15f;
        if (m != null)
        {
            m.gameObject.SetActive(true);
            m.mode = HorrorCreature.Mode.Crawl; m.gait = 3f;
            m.AnimSpeed(0f);
            m.reachArms = true;
            m.headPitch = -78f; m.headTiltTarget = 0f; m.headTwistTarget = 0f; m.headSpeed = 6000f; m.twitch = 0.6f;
            m.Laugh(3f);
        }
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < dur)
        {
            float u = Mathf.Clamp01((Time.unscaledTime - t0) / dur);
            Vector3 p = Vector3.Lerp(from, endP, Mathf.Pow(u, 2.3f));
            if (m != null)
            {
                m.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.down, (eye - from).normalized));
                m.transform.position += p - m.HeadPos;
            }
            Vector3 look = m != null ? m.HeadPos : endP;
            cam.transform.SetPositionAndRotation(eye + Random.insideUnitSphere * 0.04f * u, Quaternion.LookRotation(look - eye));
            yield return null;
        }
        if (m != null) m.transform.position += endP - m.HeadPos;
    }

    // ================= Chỗ đứng đầu dãy kệ cho F1 (ghi bằng Shift+F5) =================
    private static bool spotLoaded, spotOk;
    private static Vector3 spotP, spotF;

    private static string SpotFile => System.IO.Path.Combine(Application.dataPath, "Project/Resources/Horror/F1Spot.json");

    [System.Serializable] private class SpotData { public float x, y, z, fx, fz; }

    /// <summary>Ghi chỗ người chơi đang đứng + hướng đang nhìn làm "đầu dãy kệ" cho sự kiện camera ca đêm.</summary>
    private static void SaveF1Spot(Vector3 p, Vector3 f)
    {
        f.y = 0f; f.Normalize();
        spotP = p; spotF = f; spotOk = spotLoaded = true;
        try
        {
            string json = JsonUtility.ToJson(new SpotData { x = p.x, y = p.y, z = p.z, fx = f.x, fz = f.z });
            System.IO.File.WriteAllText(SpotFile, json);
        }
        catch (System.Exception e) { Debug.LogWarning("[Horror] Không ghi được F1Spot.json: " + e.Message); }
    }

    private static bool LoadF1Spot(out Vector3 p, out Vector3 f)
    {
        if (!spotLoaded)
        {
            spotLoaded = true;
            string json = null;
            try { if (System.IO.File.Exists(SpotFile)) json = System.IO.File.ReadAllText(SpotFile); } catch { }
            if (json == null) { TextAsset ta = Resources.Load<TextAsset>("Horror/F1Spot"); if (ta != null) json = ta.text; }
            if (!string.IsNullOrEmpty(json))
            {
                SpotData d = JsonUtility.FromJson<SpotData>(json);
                spotP = new Vector3(d.x, d.y, d.z); spotF = new Vector3(d.fx, 0f, d.fz).normalized; spotOk = spotF.sqrMagnitude > 0.5f;
            }
        }
        p = spotP; f = spotF;
        return spotOk;
    }

    /// <summary>Phát âm 3D sau một khoảng trễ.</summary>
    private static IEnumerator PlayLater(AudioClip clip, Vector3 at, float vol, float delay)
    {
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < delay) yield return null;
        HorrorAudio.Play3D(clip, at, vol, false, 6f, 50f);
    }

    /// <summary>Vài hộp hàng rơi từ kệ xuống sàn (vật lý thật), lăn ra lối đi.</summary>
    private void SpawnFallingGoods(Vector3 at, Vector3 push)
    {
        Color[] cols = { new Color(0.85f, 0.75f, 0.2f), new Color(0.8f, 0.25f, 0.2f), new Color(0.25f, 0.5f, 0.8f), new Color(0.9f, 0.9f, 0.85f) };
        for (int k = 0; k < 6; k++)
        {
            var g = GameObject.CreatePrimitive(k % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            g.name = "FallenGoods";
            g.transform.SetParent(Props(), true);
            g.transform.position = at + new Vector3(Random.Range(-0.3f, 0.3f), k * 0.12f, Random.Range(-0.3f, 0.3f));
            g.transform.localScale = k % 3 == 0 ? new Vector3(0.09f, 0.08f, 0.09f) : new Vector3(Random.Range(0.14f, 0.25f), Random.Range(0.1f, 0.2f), Random.Range(0.1f, 0.18f));
            g.transform.rotation = Random.rotation;
            RuntimeMaterials.Apply(g.GetComponent<Renderer>(), cols[k % cols.Length]);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearVelocity = push * Random.Range(1f, 2.2f) + Vector3.up * Random.Range(0f, 0.8f);
            rb.angularVelocity = Random.insideUnitSphere * 6f;
        }
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
