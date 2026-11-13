using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "ĐÁM MA NỬA ĐÊM" (~70 s): đang giao hàng đêm (xe máy hoặc ô tô) nghe tiếng kèn đám ma lệch tông, tiếng khóc than vọng lại.
/// Một đoàn đưa tang băng qua đường trước mặt (xe tự phanh dừng cách 10 m): áo tang trắng loang máu, da tái xám, khăn tang / mũ mấn,
/// di ảnh mờ nhoè, phướn trắng, bốn người khiêng quan tài đỏ đang rỉ máu, vàng mã bay. Giữa đường cả đoàn đứng sững, nhạc tắt ngấm,
/// tất cả quay lưng lại - rồi "rắc, rắc" từng cái đầu vặn ngược 180 độ nhìn thẳng vào bạn, cổ dài ra, giật cục; vàng mã hoá màu máu.
/// Trong quan tài có tiếng gõ. Lựa chọn (6 s):
///  - Bấm còi: nắp quan tài bật tung, một con quỷ gầy trơ xương ngồi dậy thét lên; cả đoàn lao về phía bạn, con quỷ bò bốn chân
///    phóng tới - máu bắn kín màn hình. Đèn sáng lại: đường trống trơn, vàng mã và vũng máu, dấu tay máu trên xe. Ví toàn vàng mã (mất tiền).
///  - Tắt máy, cúi đầu chờ: đầu họ vặn trở lại, đoàn đi tiếp; bà cụ đi cuối (mặt bê bết máu, hốc mắt trống) cúi sát: "Cảm ơn con...
///    đã không nhìn vào quan tài. Đừng ngoái lại." - trong gương: con quỷ ngồi ngay sau lưng... rồi biến mất. Nén nhang trên yên (+$20, +6 XP).
///  - Quay đầu xe: rẽ sang phố khác - lại gặp đúng đoàn người ấy đứng chờ giữa đường, đầu đã vặn ngược sẵn; chỉ còn bấm còi hoặc chờ.
/// Sáng hôm sau: bản tin "phường xác nhận đêm qua không có đám tang nào".
/// </summary>
public partial class NightRobbery
{
    private Transform coffin, coffinLid, procession;
    private HorrorCreature corpse;
    /// <summary>Một "người" trong đoàn đưa tang - thực ra là quỷ (HorrorCreature), đi lê từng bước theo đích.</summary>
    private class Demon { public Transform t; public HorrorCreature c; public Vector3 target, face; public float speed; public bool arrived = true; public bool old; }
    private readonly List<Demon> demons = new List<Demon>();
    private readonly List<Demon> bearers = new List<Demon>();
    private AudioSource funeralMusic, heartbeat;
    private ParticleSystem paperWhite, paperGold, coffinDrips;
    private RawImage screenBlood;
    private static Mesh coffinMesh, lidMesh, bannerMesh, portraitMesh, paperMesh;
    private static readonly Color YouSpeak = new Color(0.55f, 0.85f, 1f);
    private float nextTrail;

    private IEnumerator RunFuneral(PlayerCarController ride)
    {
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (city == null || city.peopleModels == null || city.peopleModels.Length == 0) yield break;
        if (!SetupRide(ride, city)) yield break;
        if (!mournersPrepped) StartCoroutine(PrepMourners());

        (Vector3, Vector3) Chase() => (youPos - youDir * 7f + Right(youDir) * 1.4f + Vector3.up * 2.6f, youPos + youDir * 8f + Vector3.up * 0.8f);
        (Vector3, Vector3) Rider() => (youPos - youDir * 3.2f + Right(youDir) * 0.7f + Vector3.up * 1.35f, ProcCenter() + Vector3.up * 1.3f);

        // ===== 1. Tiếng kèn từ xa =====
        vYou = vYouWant = 7f;
        yield return Shot(3f, "ĐƠN HÀNG ĐÊM", Subtitle(),
            () => (youPos + youDir * 7f + Right(youDir) * 2f + Vector3.up * 1.5f, youPos + Vector3.up * 0.9f),
            () => Say("Bạn", Wet ? "Mưa đêm, đường vắng tanh..." : "Khuya rồi, đường vắng tanh...", YouSpeak));
        float sAhead = sYou + 30f;
        SpawnProcession(city, sAhead, false);
        stopAtS = sAhead - 11f;                                   // xe tự phanh dừng cách đoàn người 11 m
        if (funeralMusic != null) funeralMusic.volume = 0.3f;
        yield return Shot(3f, null, null, Chase,
            () => Say("Bạn", "Tiếng kèn... lệch tông thế nhỉ. Giờ này mà có đám ma à?", YouSpeak));
        if (funeralMusic != null) funeralMusic.volume = 0.9f;
        HorrorAudio.Play3D(HorrorAudio.Wail(), ProcCenter() + Vector3.up * 1.5f, 0.6f, false, 8f, 80f);

        // ===== 2. Đoàn đưa tang băng qua =====
        yield return Shot(3.6f, null, null,
            () => (youPos + youDir * 15f - Right(youDir) * 5f + Vector3.up * 1.7f, ProcCenter() + Vector3.up * 1.2f),
            () => Say("", "<i>Một đoàn đưa tang lặng lẽ băng qua đường - dáng ai cũng cao lêu nghêu, gù gập, bước đi giật cục. Vàng mã bay lả tả.</i>", Color.white));
        yield return Shot(3.2f, null, null,
            () => (coffin != null ? coffin.position - youDir * 3.2f + Right(youDir) * 2f + Vector3.up * 0.5f : youPos, coffin != null ? coffin.position + Vector3.down * 0.3f : youPos),
            () => Say("", "<i>Chiếc quan tài sơn đỏ trên vai bốn người khiêng... máu đang rỉ ra từ khe nắp, nhỏ giọt xuống đường.</i>", Color.white));
        Demon close = FaceMover();
        cam.fieldOfView = 36f;
        yield return Shot(3f, null, null, () => FaceCam(close),
            () => Say("", "<i>Dưới lớp khăn tang... không phải mặt người. Da xám ngoét, hốc mắt đen ngòm, máu rỉ từ khoé miệng.</i>", Color.white));
        cam.fieldOfView = 52f;

        // ===== 3. Đứng sững, quay lưng, rồi vặn ngược đầu nhìn bạn =====
        yield return Stare();
        yield return Shot(2.8f, null, null, Rider,
            () => Say("", "<i>Tiếng kèn tắt ngấm. Cả đoàn đứng sững giữa đường... rồi từ từ quay lưng lại phía bạn.</i>", Color.white));
        yield return TwistHeads();
        yield return Shot(3.2f, null, null, Rider,
            () => Say("", "<i>Rắc. Rắc. Rắc. Từng cái đầu vặn ngược ra sau lưng... nhìn thẳng vào bạn.</i>", Color.white));
        Demon face = FaceMover();
        cam.fieldOfView = 32f;
        HorrorAudio.Play2D(HorrorAudio.Whisper(), 0.7f);
        yield return Shot(2.6f, null, null, () => FaceCam(face), () => Say("", "<i>Một tiếng thì thầm sát bên tai: \"...nhìn gì...\"</i>", Color.white));
        cam.fieldOfView = 52f;
        // Tiếng gõ trong quan tài
        StartCoroutine(Knocks(3));
        yield return Shot(3.2f, null, null,
            () => (coffin != null ? coffin.position - youDir * 2.6f + Right(youDir) * 1.2f + Vector3.up * 0.8f : youPos, coffin != null ? coffin.position : youPos),
            () => Say("", "<i>Cộc... cộc... cộc. Có tiếng gõ từ BÊN TRONG quan tài.</i>", Color.white));

        int pick = -1;
        yield return Choose(6f, new[] { "Bấm còi giục họ đi", rideIsBike ? "Tắt máy, cúi đầu chờ" : "Tắt máy, cúi đầu chờ trong xe", "Quay đầu xe, đi đường khác" },
            () => Rider(), i => pick = i);
        if (pick == 2)
        {
            yield return Cut("Bạn quay đầu, rẽ sang phố khác...", "Bạn quay đầu xe, rẽ vào một con phố khác. Tiếng kèn đã tắt hẳn.");
            ClearProcession();
            SpawnProcession(city, sYou + 17f, true);
            stopAtS = sYou + 6f;
            yield return Shot(2.8f, null, null, Rider,
                () => Say("Bạn", "Không thể nào... lại là họ?!", YouSpeak));
            yield return Shot(2.2f, null, null, () => FaceCam(FaceMover()), () => Say("", "<i>Họ đứng đó, đầu vẫn vặn ngược, như đã chờ sẵn.</i>", Color.white));
            int k = -1;
            yield return Choose(5f, new[] { "Bấm còi giục họ đi", "Tắt máy, cúi đầu chờ" }, () => Rider(), i => k = i);
            pick = k == 1 ? 1 : 0;
        }

        bool lucky = pick == 1;
        if (lucky) yield return FuneralWait();
        else yield return FuneralHonk();

        // Kết
        StopFuneralAudio();
        stopAtS = float.NaN;
        vYouWant = 6f;
        yield return Shot(3f, null, null,
            () => (youPos + youDir * 3.2f + Right(youDir) * 0.9f + Vector3.up * 1.6f, RiderHead),
            () => Say("Bạn", lucky ? "Thôi... giao nốt đơn rồi về. Đêm nay đừng ngoái lại nữa." : "Không nhìn thấy gì hết... không nhìn thấy gì hết... đi giao hàng thôi.", YouSpeak));
        ClearProcession();
        if (screenBlood != null) Destroy(screenBlood.gameObject);
        screenBlood = null;
        TeardownRide();
        ApplyFuneral(lucky);
        horrorNews = $"<b>BẢN TIN SÁNG</b> {lastWhen} {lastStreet}, nhiều người kể nghe tiếng kèn đám ma lúc nửa đêm và thấy vệt máu dài trên đường. Phường xác nhận: <b>không có đám tang nào</b> được đăng ký.";
    }

    /// <summary>Cả đoàn đứng sững, quay LƯNG về phía người chơi; nhạc tắt, tim đập; đèn đường quanh đó tắt bớt; vàng mã hoá đỏ.</summary>
    private IEnumerator Stare()
    {
        foreach (Demon m in demons)
        {
            if (m.t == null) continue;
            m.target = m.t.position; m.arrived = true;
            m.face = m.t.position + youDir * 5f;                       // quay lưng lại phía người chơi
            m.c.mode = HorrorCreature.Mode.Stand;
        }
        if (funeralMusic != null) funeralMusic.Stop();
        if (paperWhite != null) { var em = paperWhite.emission; em.rateOverTime = 1f; }
        if (paperGold != null)
        {
            var em = paperGold.emission; em.rateOverTime = 3f;
            var r = paperGold.GetComponent<ParticleSystemRenderer>(); if (r != null) r.sharedMaterial = RuntimeMaterials.Lit(HorrorFx.BloodWet);
        }
        heartbeat = HorrorAudio.Play2D(HorrorAudio.Heartbeat(), 0.8f, true);
        foreach (NightLight nl in NightLight.All)
            if (nl != null && (nl.transform.position - youPos).sqrMagnitude < 40f * 40f && Random.value < 0.6f) nl.SetOn(false);
        yield return null;
    }

    /// <summary>Từng người một vặn ngược đầu 180 độ (có tiếng rắc), vài người cổ dài ra, nghiêng gãy, giật cục.</summary>
    private IEnumerator TwistHeads()
    {
        var list = new List<Demon>(demons);
        int i = 0;
        foreach (Demon m in list)
        {
            if (m.t == null) continue;
            float side = (i % 2 == 0 ? 1f : -1f);
            m.c.headSpeed = 1400f;
            m.c.headTwistTarget = 180f * side;
            m.c.headTiltTarget = i % 3 == 0 ? 35f * side : 0f;
            m.c.twitch = 1.4f;
            m.c.jawOpen = i % 2 == 0 ? 0.6f : 0.25f;
            i++;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.18f) { Drive(); yield return null; }
        }
    }

    private IEnumerator Knocks(int n)
    {
        for (int k = 0; k < n; k++)
        {
            if (coffin != null) HorrorAudio.Play3D(HorrorAudio.Bang(), coffin.position, 0.8f, false, 4f, 50f);
            if (coffinLid != null) coffinLid.localPosition = new Vector3(0f, 0.03f, 0f);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.12f) yield return null;
            if (coffinLid != null) coffinLid.localPosition = Vector3.zero;
            t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.75f) yield return null;
        }
    }

    private IEnumerator FuneralWait()
    {
        if (heartbeat != null) heartbeat.volume = 0.4f;
        yield return Shot(2.6f, null, null,
            () => (RiderHead + Vector3.up * 0.05f - youDir * 0.25f, ProcCenter() + Vector3.up * 0.3f),
            () => Say("", "<i>Bạn tắt máy, cúi đầu thật thấp, nhắm chặt mắt. Không ai thở mạnh.</i>", Color.white));
        // Đầu vặn trở lại, đoàn đi tiếp; bà cụ đi cuối rẽ về phía bạn
        Demon old = null;
        foreach (Demon m in demons) if (m.t != null && m.old) old = m;
        foreach (Demon m in demons)
        {
            if (m.t == null) continue;
            m.c.headSpeed = 300f; m.c.headTwistTarget = 0f; m.c.headTiltTarget = 0f; m.c.twitch = 0.6f; m.c.jawOpen = 0f;
            if (m == old) continue;
            DemonGo(m, m.t.position - Right(youDir) * 16f, 0.9f);
        }
        if (funeralMusic != null) { funeralMusic.volume = 0.35f; funeralMusic.Play(); }
        Vector3 side = rideIsBike ? -Right(youDir) * 1.1f : DoorOut * (carHalfW + 0.7f);
        if (old != null)
        {
            Vector3 stop = youPos + side + youDir * 0.6f;
            DemonGo(old, stop, 0.7f);
            old.face = RiderHead.WithY(old.t.position.y);
        }
        yield return Shot(4.2f, null, null,
            () => (youPos - youDir * 4.5f - side.normalized * 1.5f + Vector3.up * 1.8f, youPos + side * 0.7f + Vector3.up * 1.1f),
            () => Say("", "<i>Rắc... những cái đầu từ từ vặn trở lại. Cả đoàn lê bước đi tiếp. Chỉ có cái bóng đội mũ mấn đi cuối là rẽ về phía bạn.</i>", Color.white));
        if (old != null && old.t != null)
        {
            old.t.position = old.target; old.arrived = true;
            old.c.mode = HorrorCreature.Mode.Stand; old.c.headTiltTarget = 25f; old.c.twitch = 1f; old.c.jawOpen = 0.35f;
            cam.fieldOfView = 34f;
            HorrorAudio.Play2D(HorrorAudio.Breath(), 0.6f);
            yield return Shot(3f, null, null, () => FaceCam(old),
                () => Say("Bà cụ", "Cảm ơn con... đã không nhìn vào quan tài.", MournCol));
            yield return Shot(2.6f, null, null, () => FaceCam(old),
                () => Say("Bà cụ", "Đi đi. Và đừng... ngoái... lại.", MournCol));
            cam.fieldOfView = 52f;
            old.c.jawOpen = 0f;
            DemonGo(old, old.t.position - Right(youDir) * 14f, 0.8f);
        }
        yield return Shot(2.6f, null, null, () => (youPos + youDir * 6f + Vector3.up * 2.2f, youPos - Right(youDir) * 8f + youDir * 6f),
            () => Say("", "<i>Những bóng trắng lêu nghêu khuất dần vào con ngõ tối. Tiếng kèn xa dần... rồi tắt hẳn.</i>", Color.white));
        // Gương chiếu hậu: con quỷ ngồi ngay sau lưng
        ClearProcession();
        Vector3 back = rideIsBike ? youPos - youDir * 0.55f : youPos - youDir * 0.9f + DoorOut * 0.35f;
        corpse = HorrorCreature.Create(Props(), back - Vector3.up * (rideIsBike ? 0.15f : 0.55f), Quaternion.LookRotation(youDir));
        corpse.mode = HorrorCreature.Mode.Stand; corpse.twitch = 1.2f;
        yield return Shot(2.4f, null, null, () => (RiderHead + youDir * 0.5f + Vector3.up * 0.1f, RiderHead + youDir * 0.6f + Vector3.up * 0.1f),
            () => Say("", "<i>Bạn lỡ liếc gương chiếu hậu...</i>", Color.white));
        cam.fieldOfView = 40f;
        yield return Shot(1.4f, null, null, () => (RiderHead + youDir * 0.9f + Vector3.up * 0.05f, corpse != null ? corpse.HeadPos : RiderHead),
            () => { if (corpse != null) corpse.Scream(0.8f); shake = 0.6f; });
        cam.fieldOfView = 52f;
        if (corpse != null) Destroy(corpse.gameObject);
        corpse = null;
        yield return BlackHold(1.2f, "", "");
        yield return FadeIn(0.6f);
        yield return Cut("Một lúc sau...", rideIsBike ? "Phía sau trống trơn. Trên yên xe có một nén nhang... vẫn còn đang cháy."
                                                      : "Ghế sau trống trơn. Trên ghế phụ có một nén nhang... vẫn còn đang cháy.");
    }

    private IEnumerator FuneralHonk()
    {
        AudioManager.Play(Random.value < 0.5f ? "horn_1" : "horn_2", 0.7f);
        yield return Shot(1.6f, null, null, () => (RiderHead - youDir * 0.3f + Vector3.up * 0.1f, ProcCenter() + Vector3.up * 1.2f),
            () => Say("", "<i>BÍNNN! Tiếng còi xé toạc màn đêm.</i>", Color.white));
        // Nắp quan tài bật tung, con quỷ ngồi dậy
        CoffinBurst();
        if (heartbeat != null) { heartbeat.pitch = 1.6f; heartbeat.volume = 1f; }
        yield return Shot(3.2f, null, null,
            () => (coffin != null ? coffin.position - youDir * 3.4f + Right(youDir) * 1.6f + Vector3.up * 0.9f : youPos, corpse != null ? corpse.HeadPos : ProcCenter()),
            () => { Say("", "<i>RẦM! Nắp quan tài bật tung. Một thứ gầy trơ xương, máu me bê bết từ từ ngồi dậy...</i>", Color.white); });
        if (corpse != null) corpse.Scream(1f);
        shake = 0.5f;
        yield return Shot(1.6f, null, null,
            () => (corpse != null ? corpse.HeadPos - youDir * 1.6f + Vector3.up * 0.2f : youPos, corpse != null ? corpse.HeadPos : ProcCenter()), () => { });
        // Cả đoàn lao tới, con quỷ bò bốn chân phóng tới
        foreach (Demon m in demons)
        {
            if (m.t == null) continue;
            DemonGo(m, Vector3.Lerp(m.t.position, youPos, 0.65f), 3.6f);
            m.c.mode = HorrorCreature.Mode.Crawl; m.c.gait = 2.2f;      // đổ rạp xuống bò bốn chân lao tới
            m.c.headTwistTarget = 0f; m.c.twitch = 1.6f; m.c.jawOpen = 1f;
        }
        if (corpse != null)
        {
            corpse.transform.SetParent(Props(), true);
            corpse.mode = HorrorCreature.Mode.Crawl; corpse.gait = 2.4f;
        }
        Vector3 cStart = corpse != null ? corpse.transform.position : ProcCenter();
        Vector3 cEnd = RiderHead + youDir * 0.9f;
        float t0 = Time.unscaledTime;
        Say("", "<i>Cả đoàn đổ rạp xuống, bò bốn chân như nhện, lao về phía bạn.</i>", Color.white);
        Vector3 camPos = RiderHead - youDir * 0.2f;
        while (Time.unscaledTime - t0 < 2.6f)
        {
            Drive();
            float u = (Time.unscaledTime - t0) / 2.6f;
            if (corpse != null)
            {
                Vector3 p = Vector3.Lerp(cStart, cEnd.WithY(GroundAt(cEnd)), u * u);
                corpse.transform.SetPositionAndRotation(p, Quaternion.LookRotation((-youDir).WithY(0f)));
            }
            cam.transform.SetPositionAndRotation(camPos + Random.insideUnitSphere * 0.02f * u, Quaternion.LookRotation((corpse != null ? corpse.HeadPos : ProcCenter()) - camPos));
            yield return null;
        }
        if (corpse != null) { corpse.mode = HorrorCreature.Mode.Reach; corpse.transform.position = (RiderHead + youDir * 0.7f).WithY(RiderHead.y - 1.9f); corpse.Scream(1f); }
        Scare();
        BloodOnScreen();
        yield return Shot(0.5f, null, null, () => (RiderHead, corpse != null ? corpse.HeadPos : RiderHead + youDir), () => { });
        if (heartbeat != null) heartbeat.Stop();
        ClearProcession();
        if (corpse != null) Destroy(corpse.gameObject);
        corpse = null;
        // Hiện trường: vàng mã, vũng máu, dấu tay máu trên xe
        ScatterPaper(youPos + youDir * 9f, 9f, 70);
        Vector3 poolAt = youPos + youDir * 6f;
        Destroy(HorrorFx.Pool(poolAt.WithY(GroundAt(poolAt) + 0.02f), 3.2f, null), 90f);   // để lại trên đường một lúc
        LeaveHandprints();
        yield return BlackHold(1.8f, "", "");
        yield return FadeIn(0.8f);
        yield return Shot(3.2f, null, null,
            () => (youPos + youDir * 4f + Right(youDir) * 2.5f + Vector3.up * 2.3f, youPos + youDir * 9f),
            () => Say("", "<i>Đèn đường sáng lại. Con phố trống trơn - chỉ còn vàng mã và một vũng máu lớn giữa đường.</i>", Color.white));
        yield return Shot(2.8f, null, null,
            () => (youPos + DoorOut * 2.2f + youDir * 0.5f + Vector3.up * 1.3f, youPos + Vector3.up * (rideIsBike ? 0.9f : 1.1f)),
            () => Say("", rideIsBike ? "<i>Trên yên xe và tay lái... in đầy những dấu bàn tay máu.</i>" : "<i>Trên cửa kính xe... in đầy những dấu bàn tay máu.</i>", Color.white));
    }

    /// <summary>Máu bắn kín màn hình rồi chảy mờ dần.</summary>
    private void BloodOnScreen()
    {
        if (canvas == null) return;
        if (screenBlood != null) Destroy(screenBlood.gameObject);
        screenBlood = HorrorFx.ScreenBlood(canvas.transform);
        screenBlood.transform.SetSiblingIndex(Mathf.Max(0, screenBlood.transform.parent.childCount - 2));
        StartCoroutine(FadeScreenBlood(screenBlood));
    }

    private IEnumerator FadeScreenBlood(RawImage img)
    {
        float t0 = Time.unscaledTime;
        while (img != null && Time.unscaledTime - t0 < 6f)
        {
            float u = (Time.unscaledTime - t0) / 6f;
            img.color = new Color(1f, 1f, 1f, 1f - u * u);
            img.uvRect = new Rect(0f, u * 0.08f, 1f, 1f);              // máu chảy xuống
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    /// <summary>Dấu bàn tay máu lên xe (kính cửa ô tô / yên xe máy), mất sau 90 s.</summary>
    private void LeaveHandprints()
    {
        if (car == null) return;
        Transform ct = car.transform;
        var holder = new GameObject("BloodyHands").transform;
        holder.SetParent(ct, true);
        for (int i = 0; i < 4; i++)
        {
            Vector3 p, n;
            if (rideIsBike) { p = ct.position + Vector3.up * (0.75f + i * 0.04f) + ct.forward * (-0.2f + i * 0.18f) + ct.right * Random.Range(-0.08f, 0.08f); n = Vector3.up; }
            else { p = DriverHead + DoorOut * (carHalfW - Mathf.Abs(seatLocal.x) + 0.02f) + youDir * (-0.35f + i * 0.25f) + Vector3.down * Random.Range(0.05f, 0.25f); n = DoorOut; }
            HorrorFx.Decal(HorrorFx.Hand(), p, n, rideIsBike ? ct.forward : Vector3.up + Random.insideUnitSphere * 0.3f, new Vector2(0.2f, 0.24f), holder);
        }
        Destroy(holder.gameObject, 90f);
    }

    /// <summary>Nắp quan tài văng ra; con quỷ trong quan tài hiện ra, ngồi dậy.</summary>
    private void CoffinBurst()
    {
        if (coffin == null) return;
        HorrorAudio.Play3D(HorrorAudio.Bang(), coffin.position, 1f, false, 4f, 60f);
        foreach (Demon m in bearers) if (m.t != null) { m.target = m.t.position; m.arrived = true; }
        bearers.Clear();
        // hạ quan tài xuống đường
        Vector3 ground = coffin.position.WithY(GroundAt(coffin.position) + 0.3f);
        coffin.SetPositionAndRotation(ground, coffin.rotation);
        if (coffinLid != null)
        {
            coffinLid.SetParent(Props(), true);
            coffinLid.position = ground + coffin.right * 0.9f + Vector3.up * 0.15f;
            coffinLid.rotation = coffin.rotation * Quaternion.Euler(0f, 25f, 70f);
        }
        corpse = HorrorCreature.Create(Props(), ground + Vector3.up * 0.1f - Vector3.up * 1.0f, Quaternion.LookRotation((youPos - ground).WithY(0f)));
        corpse.mode = HorrorCreature.Mode.Stand;
        corpse.twitch = 1.5f;
        corpse.jawOpen = 0.4f;
        HorrorFx.Pool(ground.WithY(GroundAt(ground) + 0.02f), 2.4f, Props());
    }

    // ================= Dựng đoàn đưa tang =================
    private Vector3 ProcCenter()
    {
        Vector3 c = Vector3.zero; int n = 0;
        foreach (Demon m in demons) if (m.t != null) { c += m.t.position; n++; }
        return n > 0 ? c / n : youPos + youDir * 20f;
    }

    /// <summary>Con gần người chơi nhất (để cận mặt).</summary>
    private Demon FaceMover()
    {
        Demon best = null; float bd = float.MaxValue;
        foreach (Demon m in demons)
        {
            if (m.t == null) continue;
            float d = (m.t.position - youPos).sqrMagnitude;
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }

    private (Vector3, Vector3) FaceCam(Demon m)
    {
        if (m == null || m.t == null) return (youPos + Vector3.up * 1.5f, youPos + youDir * 10f);
        Vector3 h = m.c.HeadPos;
        Vector3 to = RiderHead - h; to.y = 0f; to = to.sqrMagnitude > 0.01f ? to.normalized : -youDir;
        return (h + to * 1.3f + Vector3.down * 0.05f, h);
    }

    private void DemonGo(Demon m, Vector3 to, float speed)
    {
        if (m == null || m.t == null) return;
        m.target = to.WithY(m.t.position.y); m.speed = speed; m.arrived = false;
        if (m.c.mode == HorrorCreature.Mode.Stand) m.c.mode = HorrorCreature.Mode.Walk;
        m.c.gait = Mathf.Clamp(speed / 0.9f, 0.6f, 3f);
    }

    /// <summary>Mỗi frame: đoàn quỷ lê bước tới đích (bám mặt đường), tới nơi thì đứng, quay mặt theo face.</summary>
    private void UpdateDemons(float dt)
    {
        foreach (Demon m in demons)
        {
            if (m.t == null) continue;
            if (!m.arrived)
            {
                Vector3 d = m.target - m.t.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.08f) { m.arrived = true; if (m.c.mode == HorrorCreature.Mode.Walk) m.c.mode = HorrorCreature.Mode.Stand; }
                else
                {
                    Vector3 p = m.t.position + d / dist * Mathf.Min(dist, m.speed * dt);
                    p.y = Mathf.Lerp(m.t.position.y, GroundAt(p), 1f - Mathf.Exp(-12f * dt));
                    m.t.position = p;
                    m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(d / dist), 1f - Mathf.Exp(-8f * dt));
                }
            }
            else
            {
                Vector3 f = m.face - m.t.position; f.y = 0f;
                if (f.sqrMagnitude > 0.01f) m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(f), 1f - Mathf.Exp(-4f * dt));
            }
        }
    }

    /// <summary>
    /// Đoàn 10 người cắt ngang đường ở quãng sAhead, đi từ vỉa hè bên phải sang trái: người cầm phướn, người ôm di ảnh,
    /// 4 người khiêng quan tài (2 x 2), 4 người đi sau. standing = dựng sẵn giữa đường, quay lưng, đầu đã vặn ngược nhìn người chơi.
    /// </summary>
    private void SpawnProcession(VietDistrict city, float sAhead, bool standing)
    {
        ClearProcession();
        procession = new GameObject("FuneralProcession").transform;
        procession.SetParent(transform, false);
        scamPeople.Add(procession.gameObject);
        rail.Sample(sAhead, 0f, out Vector3 mid, out Vector3 dir);
        Vector3 across = -Right(dir);
        float startLat = 4.5f, endLat = -(lane + 2.6f) - 3f;
        float crossLen = startLat - endLat;
        var slots = new List<(float back, float side, string tag)>
        {
            (0f, 0f, "banner"), (1.4f, 0f, "portrait"),
            (2.9f, -0.42f, "bear"), (2.9f, 0.42f, "bear"), (4.4f, -0.42f, "bear"), (4.4f, 0.42f, "bear"),
            (5.9f, -0.3f, "hood"), (5.9f, 0.4f, "hood"), (7.3f, 0f, ""), (8.6f, 0.2f, "old"),
        };
        bearers.Clear();
        demons.Clear();
        int idx = 0;
        foreach (var sl in slots)
        {
            float lat = standing ? -sl.back * 0.55f + 1.6f : startLat + sl.back;
            rail.Sample(sAhead + sl.side + (standing ? (idx % 3 - 1) * 0.9f : 0f), lat, out Vector3 at, out _);
            at.y = GroundAt(at);
            Vector3 goal = at + across * (standing ? 0f : crossLen - 1f);
            HorrorCreature c = HorrorCreature.Create(procession, at, Quaternion.LookRotation(standing ? dir : across));
            c.Mourning(sl.tag == "hood" || sl.tag == "banner" || sl.tag == "portrait" || sl.tag == "old");
            c.transform.localScale = Vector3.one * (sl.tag == "old" ? 0.82f : Random.Range(0.86f, 1.02f));
            c.twitch = 0.5f;
            var m = new Demon { t = c.transform, c = c, target = at, face = at + dir * 5f, speed = 0.9f, old = sl.tag == "old" };
            demons.Add(m);
            idx++;
            if (standing)
            {
                c.mode = HorrorCreature.Mode.Stand;
                c.headSpeed = 5000f; c.headTwistTarget = 180f * (idx % 2 == 0 ? 1f : -1f); c.headTiltTarget = idx % 3 == 0 ? 30f : 0f;
                c.twitch = 1.4f; c.jawOpen = idx % 2 == 0 ? 0.5f : 0.2f;
            }
            else DemonGo(m, goal, 0.9f);
            if (sl.tag == "bear") bearers.Add(m);
            if (sl.tag == "banner") HoldDemonProp(c, BannerMesh(), new Vector3(0.35f, 0.2f, 0.25f));
            if (sl.tag == "portrait") HoldDemonProp(c, PortraitMesh(), new Vector3(0f, 1.45f, 0.32f));
        }
        // Quan tài (thân + nắp riêng) trên vai 4 người khiêng, rỉ máu
        var cg = new GameObject("Coffin");
        cg.transform.SetParent(procession, false);
        cg.AddComponent<MeshFilter>().sharedMesh = CoffinMesh();
        cg.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        coffin = cg.transform;
        var lg = new GameObject("CoffinLid");
        lg.transform.SetParent(coffin, false);
        lg.AddComponent<MeshFilter>().sharedMesh = LidMesh();
        lg.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        coffinLid = lg.transform;
        UpdateCoffin(0f);
        coffinDrips = HorrorFx.Drips(coffin.position - Vector3.up * 0.3f, 6f, coffin);
        // Vàng mã bay
        Vector3 c0 = ProcCenter();
        paperWhite = PaperFall(c0, new Color(0.96f, 0.95f, 0.9f), standing ? 1f : 7f);
        paperGold = PaperFall(c0, standing ? HorrorFx.BloodWet : new Color(0.95f, 0.78f, 0.25f), standing ? 3f : 4f);
        if (!standing) funeralMusic = HorrorAudio.Play3D(HorrorAudio.FuneralBand(), c0 + Vector3.up * 1.5f, 0.9f, true, 10f, 140f, coffin);
        if (standing && rail != null) { HorrorFx.Pool(c0.WithY(GroundAt(c0) + 0.02f), 2.6f, Props()); }
    }

    /// <summary>Đặt quan tài giữa 4 người khiêng, ngang vai (gọi mỗi frame); vệt máu nhỏ xuống đường theo đoàn.</summary>
    private void UpdateCoffin(float dt)
    {
        if (coffin == null || bearers.Count == 0) return;
        Vector3 c = Vector3.zero; int n = 0;
        Vector3 fwd = Vector3.zero;
        foreach (Demon m in bearers) if (m.t != null) { c += m.t.position; fwd += m.t.forward; n++; }
        if (n == 0) return;
        c /= n; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(fwd.normalized);
        float bob = Mathf.Sin(Time.time * 3.2f) * 0.02f;
        coffin.SetPositionAndRotation(c + Vector3.up * (1.62f + bob), rot);   // trên vai đoàn quỷ cao lêu nghêu
        if (paperWhite != null) paperWhite.transform.position = c + Vector3.up * 4f;
        if (paperGold != null) paperGold.transform.position = c + Vector3.up * 4f;
        if (dt > 0f && Time.time >= nextTrail)
        {
            nextTrail = Time.time + 0.5f;
            HorrorFx.Decal(HorrorFx.Splat(), c.WithY(GroundAt(c) + 0.015f), Vector3.up, fwd, new Vector2(0.5f, 0.6f), Props(), 0.85f);
        }
    }

    private void ClearProcession()
    {
        foreach (Demon m in demons) if (m.t != null) Destroy(m.t.gameObject);
        demons.Clear();
        bearers.Clear();
        if (procession != null) Destroy(procession.gameObject);
        procession = null; coffin = null; coffinLid = null; paperWhite = paperGold = null; coffinDrips = null;
        if (funeralMusic != null) Destroy(funeralMusic.gameObject);
        funeralMusic = null;
    }

    private void StopFuneralAudio()
    {
        if (funeralMusic != null) Destroy(funeralMusic.gameObject);
        if (heartbeat != null) Destroy(heartbeat.gameObject);
        funeralMusic = heartbeat = null;
        NightLight.SetAll(true);
    }

    private static void ApplyFuneral(bool lucky)
    {
        EconomyManager eco = EconomyManager.Instance;
        if (lucky)
        {
            if (eco != null) eco.AddIncome(20f, MoneyCategory.Other, "A stranger's lucky money (funeral night)");
            StoreProgress.AddXP(6);
            ProgressHUD.Toast("<color=#BFD7FF>Trong túi bạn có thêm một phong bao đỏ: +$20 \"lộc\", +6 XP.</color>");
        }
        else
        {
            float cash = eco != null ? Mathf.Round(Mathf.Clamp(eco.Money * 0.1f, 25f, 80f)) : 0f;
            if (eco != null && cash > 0f) eco.Charge(cash, MoneyCategory.Other, "Cash turned into joss paper (funeral night)");
            ProgressHUD.Toast($"<color=#FF8A7A>Mở ví ra... toàn vàng mã dính máu. Mất {EconomyManager.Format(cash)} tiền mặt.</color>");
        }
    }

    // ================= Đạo cụ =================
    /// <summary>Đạo cụ (phướn / di ảnh) gắn trước người con quỷ đi đầu.</summary>
    private static void HoldDemonProp(HorrorCreature c, Mesh mesh, Vector3 local)
    {
        var g = new GameObject(mesh.name);
        g.transform.SetParent(c.transform, false);
        g.transform.localPosition = local;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
    }

    private static Mesh CoffinMesh()
    {
        if (coffinMesh != null) return coffinMesh;
        var mb = new VMeshBuilder();
        Color wood = new Color(0.42f, 0.07f, 0.05f), gold = new Color(0.92f, 0.72f, 0.2f);
        mb.Add(VShape.RoundBox(0.08f, 10), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.62f, 0.5f, 2.05f)), wood);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0.24f, 0f), Quaternion.identity, new Vector3(0.5f, 0.02f, 1.9f)), new Color(0.05f, 0.01f, 0.01f)); // lòng quan tài tối
        for (int s = -1; s <= 1; s += 2)
        {
            mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(s * 0.315f, 0.05f, 0f), Quaternion.identity, new Vector3(0.012f, 0.05f, 1.95f)), gold); // nẹp vàng
            mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(s * 0.316f, 0.12f, 0.3f), Quaternion.identity, new Vector3(0.008f, 0.12f, 0.05f)), new Color(0.5f, 0.02f, 0.02f)); // máu rỉ khe nắp
        }
        mb.Add(VShape.Torus(0.16f, 18), Matrix4x4.TRS(new Vector3(0f, 0.12f, 1.07f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f)), new Color(0.96f, 0.96f, 0.92f)); // vòng hoa trắng
        mb.Add(VShape.Torus(0.1f, 18), Matrix4x4.TRS(new Vector3(0f, 0.12f, 1.08f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.36f, 0.36f, 0.36f)), new Color(0.95f, 0.85f, 0.3f));
        coffinMesh = mb.ToMesh(RealModels.MeshPrefix + "_Coffin");
        coffinMesh.hideFlags = HideFlags.DontSave;
        return coffinMesh;
    }

    private static Mesh LidMesh()
    {
        if (lidMesh != null) return lidMesh;
        var mb = new VMeshBuilder();
        Color wood = new Color(0.42f, 0.07f, 0.05f), gold = new Color(0.92f, 0.72f, 0.2f), red = new Color(0.7f, 0.05f, 0.04f);
        mb.Add(VShape.RoundBox(0.3f, 12), Matrix4x4.TRS(new Vector3(0f, 0.3f, 0f), Quaternion.identity, new Vector3(0.66f, 0.16f, 2.1f)), wood);       // nắp khum
        mb.Add(VShape.RoundBox(0.2f, 10), Matrix4x4.TRS(new Vector3(0f, 0.39f, 0f), Quaternion.identity, new Vector3(0.5f, 0.03f, 1.75f)), red);        // khăn phủ đỏ
        mb.Add(VShape.Sphere(10), Matrix4x4.TRS(new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(0.22f, 0.02f, 0.22f)), gold);             // chữ "Thọ"
        lidMesh = mb.ToMesh(RealModels.MeshPrefix + "_CoffinLid");
        lidMesh.hideFlags = HideFlags.DontSave;
        return lidMesh;
    }

    private static Mesh BannerMesh()
    {
        if (bannerMesh != null) return bannerMesh;
        var mb = new VMeshBuilder();
        Color pole = new Color(0.55f, 0.42f, 0.25f), cloth = new Color(0.97f, 0.97f, 0.95f);
        mb.Add(VShape.Cylinder(8), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.035f, 2.8f, 0.035f)), pole);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 1.32f, 0.02f), Quaternion.identity, new Vector3(0.7f, 0.05f, 0.05f)), pole);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0.58f, 0.03f), Quaternion.identity, new Vector3(0.6f, 1.45f, 0.01f)), cloth);
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0.1f, 0.3f, 0.036f), Quaternion.identity, new Vector3(0.18f, 0.5f, 0.004f)), new Color(0.5f, 0.03f, 0.03f)); // vệt máu
        for (int k = 0; k < 4; k++)
            mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(-0.22f + k * 0.15f, -0.25f, 0.03f), Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 6f : -6f)), new Vector3(0.1f, 0.3f, 0.008f)), cloth);
        bannerMesh = mb.ToMesh(RealModels.MeshPrefix + "_Banner");
        bannerMesh.hideFlags = HideFlags.DontSave;
        return bannerMesh;
    }

    private static Mesh PortraitMesh()
    {
        if (portraitMesh != null) return portraitMesh;
        var mb = new VMeshBuilder();
        mb.Add(VShape.Cube, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.32f, 0.42f, 0.03f)), new Color(0.06f, 0.05f, 0.04f));
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0f, 0f, 0.016f), Quaternion.identity, new Vector3(0.25f, 0.34f, 0.005f)), new Color(0.45f, 0.45f, 0.45f));
        mb.Add(VShape.Sphere(10), Matrix4x4.TRS(new Vector3(0f, 0.04f, 0.02f), Quaternion.identity, new Vector3(0.11f, 0.14f, 0.005f)), new Color(0.3f, 0.3f, 0.3f));
        for (int s = -1; s <= 1; s += 2)   // mắt trong ảnh bị cào nát, máu chảy
            mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(s * 0.025f, 0.02f, 0.024f), Quaternion.identity, new Vector3(0.02f, 0.09f, 0.003f)), new Color(0.5f, 0.02f, 0.02f));
        mb.Add(VShape.Cube, Matrix4x4.TRS(new Vector3(0.1f, 0.15f, 0.02f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.18f, 0.035f, 0.004f)), Color.black);
        portraitMesh = mb.ToMesh(RealModels.MeshPrefix + "_Portrait");
        portraitMesh.hideFlags = HideFlags.DontSave;
        return portraitMesh;
    }

    private static Mesh PaperMesh()
    {
        if (paperMesh != null) return paperMesh;
        var mb = new VMeshBuilder();
        mb.Add(VShape.Cube, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.11f, 0.003f, 0.15f)), Color.white);
        paperMesh = mb.ToMesh(RealModels.MeshPrefix + "_Paper");
        paperMesh.hideFlags = HideFlags.DontSave;
        return paperMesh;
    }

    /// <summary>Vàng mã bay lả tả (hạt là tờ giấy mỏng, rơi chậm, lật lượn).</summary>
    private ParticleSystem PaperFall(Vector3 at, Color col, float rate)
    {
        var g = new GameObject("JossPaper");
        g.transform.SetParent(procession, false);
        g.transform.position = at + Vector3.up * 4f;
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 5f; main.loop = true;
        main.startLifetime = 6f; main.startSpeed = 0.3f; main.gravityModifier = 0.04f;
        main.startSize = 1f;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(6f, 0.2f, 3f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-3f, 3f); rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.6f; nz.frequency = 0.4f;
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = PaperMesh();
        r.sharedMaterial = RuntimeMaterials.Lit(col);
        ps.Play();
        return ps;
    }

    /// <summary>Vàng mã nằm rải trên mặt đường (sau khi đoàn người biến mất).</summary>
    private void ScatterPaper(Vector3 center, float radius, int count)
    {
        var mb = new VMeshBuilder();
        for (int i = 0; i < count; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = new Vector3(r.x, 0.02f, r.y);
            float k = Random.value;
            mb.Add(VShape.Cube, Matrix4x4.TRS(p, Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f)), new Vector3(0.11f, 0.004f, 0.15f)),
                   k < 0.45f ? new Color(0.96f, 0.95f, 0.9f) : k < 0.8f ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.5f, 0.03f, 0.03f));
        }
        var g = new GameObject("JossPaperOnRoad");
        g.transform.SetParent(transform, false);
        g.transform.position = new Vector3(center.x, GroundAt(center), center.z);
        g.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("JossPaperOnRoad");
        g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        Destroy(g, 90f);
    }
}
