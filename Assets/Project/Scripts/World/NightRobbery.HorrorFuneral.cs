using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "ĐÁM MA NỬA ĐÊM" (~80 s): đang giao hàng đêm (xe máy hoặc ô tô) nghe tiếng kèn đám ma (phường kèn kiểu Việt: kèn bầu, trống,
/// não bạt, phách), tiếng khóc vọng lại. Một đoàn đưa tang băng qua đường trước mặt (xe tự phanh dừng cách 11 m): người mặc áo sô
/// trắng, khăn tang / mũ mấn, tay cầm nắm hương khói nghi ngút, phướn trắng, di ảnh, bốn người khiêng quan tài đỏ có bát hương
/// và nến trên nắp, vàng mã bay, khói hương mù mịt. Giữa đường cả đoàn đứng sững, nhạc tắt ngấm. Có tiếng gõ trong quan tài.
/// Họ đặt quan tài xuống - nắp từ từ trượt ra - ông cụ trong quan tài (da tái, máu me) NGỒI DẬY, cười khanh khách; rồi cả đoàn
/// vặn ngược đầu nhìn bạn và cùng cười theo, máu chảy ra từ miệng. Lựa chọn (6 s):
///  - Bấm còi: tiếng cười tắt phụt; ông cụ bò ra khỏi quan tài, cả đoàn đổ rạp xuống lao về phía bạn - máu bắn kín màn hình.
///    Đèn sáng lại: đường trống trơn, vàng mã và vũng máu, dấu tay máu trên xe. Ví toàn vàng mã (mất tiền).
///  - Tắt máy, cúi đầu chờ: tiếng cười tắt dần, mở mắt ra thì phố trống trơn, chỉ còn nén hương cháy dở giữa đường. Bạn đi giao tiếp
///    như thường... nhưng cứ một quãng lại thấy một bóng mặt trắng cười toét lấp ló ven đường (con hề), mỗi lần một gần hơn,
///    để lại vệt máu - rồi nó nhảy xổ ra ngay trước mặt. Tỉnh lại: dấu tay máu trên xe (+$20 "lộc", +6 XP).
///  - Quay đầu xe: rẽ sang phố khác - lại gặp đúng đoàn người ấy đứng chờ giữa đường, ông cụ vẫn ngồi trong quan tài cười;
///    chỉ còn bấm còi hoặc chờ.
/// Sáng hôm sau: bản tin "phường xác nhận đêm qua không có đám tang nào".
/// </summary>
public partial class NightRobbery
{
    private Transform coffin, coffinLid, procession;
    private RigMonster corpse;
    /// <summary>Một người trong đoàn đưa tang (RigMonster: model người mặc đồ tang), đi lê từng bước theo đích.</summary>
    private class Demon { public Transform t; public RigMonster c; public Vector3 target, face; public float speed; public bool arrived = true; public bool old; }
    private readonly List<Demon> demons = new List<Demon>();
    private readonly List<Demon> bearers = new List<Demon>();
    private AudioSource funeralMusic, heartbeat, crowdLaugh, chanting;
    private ParticleSystem paperWhite, paperGold, paperHell, coffinDrips, procMist;
    private RawImage screenBlood;
    private static Mesh coffinMesh, lidMesh, bannerMesh, portraitMesh, paperMesh, incenseMesh, altarMesh;
    private static readonly Color YouSpeak = new Color(0.55f, 0.85f, 1f);
    private float nextTrail;

    private IEnumerator RunFuneral(PlayerCarController ride)
    {
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (city == null || city.peopleModels == null || city.peopleModels.Length == 0) yield break;
        if (MournerPrefab(0) == null) { ProgressHUD.Toast("Thiếu model người cho đoàn đưa tang."); yield break; }
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
        if (funeralMusic != null) funeralMusic.volume = 0.35f;
        yield return Shot(3.2f, null, null, Chase,
            () => Say("Bạn", "Tiếng kèn đám ma... Giờ này mà vẫn còn đưa tang à?", YouSpeak));
        if (funeralMusic != null) funeralMusic.volume = 1f;
        HorrorAudio.Play3D(HorrorAudio.Wail(), ProcCenter() + Vector3.up * 1.5f, 0.5f, false, 8f, 80f);

        // ===== 2. Đoàn đưa tang băng qua =====
        yield return Shot(4f, null, null,
            () => (youPos + youDir * 15f - Right(youDir) * 5f + Vector3.up * 1.7f, ProcCenter() + Vector3.up * 1.2f),
            () => Say("", "<i>Một đoàn đưa tang lặng lẽ băng qua đường. Áo sô trắng, khăn tang, mũ mấn; ai cũng cầm nắm hương, khói bay mù mịt. Vàng mã rơi lả tả.</i>", Color.white));
        yield return Shot(3.4f, null, null,
            () => (coffin != null ? coffin.position - youDir * 3.2f + Right(youDir) * 2f + Vector3.up * 0.4f : youPos, coffin != null ? coffin.position + Vector3.down * 0.2f : youPos),
            () => Say("", "<i>Chiếc quan tài sơn đỏ, trên nắp đặt bát hương và hai ngọn nến... máu đang rỉ ra từ khe nắp, nhỏ giọt xuống đường.</i>", Color.white));
        Demon close = FaceMover();
        cam.fieldOfView = 36f;
        yield return Shot(3f, null, null, () => FaceCam(close),
            () => Say("", "<i>Gương mặt người nào cũng trắng bệch, mắt mở trừng trừng không chớp. Không ai khóc cả.</i>", Color.white));
        cam.fieldOfView = 52f;

        // ===== 3. Đứng sững, tiếng gõ trong quan tài =====
        yield return Stare();
        yield return Shot(2.8f, null, null, Rider,
            () => Say("", "<i>Tiếng kèn tắt ngấm. Cả đoàn đứng sững giữa đường... quay lưng về phía bạn.</i>", Color.white));
        StartCoroutine(Knocks(3));
        yield return Shot(3.2f, null, null,
            () => (coffin != null ? coffin.position - youDir * 2.6f + Right(youDir) * 1.2f + Vector3.up * 0.8f : youPos, coffin != null ? coffin.position : youPos),
            () => Say("", "<i>Cộc... cộc... cộc. Có tiếng gõ từ BÊN TRONG quan tài.</i>", Color.white));

        // ===== 4. Quan tài mở - ông cụ ngồi dậy cười, cả đoàn cười theo =====
        yield return CoffinOpen(false);
        yield return CrowdLaughs();

        int pick = -1;
        yield return Choose(6f, new[] { "Bấm còi giục họ đi", rideIsBike ? "Tắt máy, cúi đầu chờ" : "Tắt máy, cúi đầu chờ trong xe", "Quay đầu xe, đi đường khác" },
            () => Rider(), i => pick = i);
        if (pick == 2)
        {
            yield return Cut("Bạn quay đầu, rẽ sang phố khác...", "Bạn quay đầu xe, rẽ vào một con phố khác. Tiếng cười vẫn văng vẳng sau lưng.");
            ClearProcession();
            SpawnProcession(city, sYou + 17f, true);
            stopAtS = sYou + 6f;
            yield return CoffinOpen(true);
            if (crowdLaugh == null) crowdLaugh = HorrorAudio.Play3D(HorrorAudio.CrowdLaugh(), ProcCenter() + Vector3.up * 1.5f, 1f, true, 30f, 160f);
            foreach (Demon m in demons) if (m.t != null) { m.c.Laugh(30f); m.c.twitch = 0f; m.c.AnimSpeed(0f); }
            yield return Shot(2.8f, null, null, Rider,
                () => Say("Bạn", "Không thể nào... lại là họ?!", YouSpeak));
            yield return Shot(2.2f, null, null, () => FaceCam(FaceMover()), () => Say("", "<i>Họ đứng chờ sẵn giữa đường, đầu vặn ngược, vẫn đang cười.</i>", Color.white));
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
        if (!lucky)
        {
            vYouWant = 6f;
            yield return Shot(3f, null, null,
                () => (youPos + youDir * 3.2f + Right(youDir) * 0.9f + Vector3.up * 1.6f, RiderHead),
                () => Say("Bạn", "Không nhìn thấy gì hết... không nhìn thấy gì hết... đi giao hàng thôi.", YouSpeak));
        }
        ClearProcession();
        if (screenBlood != null) Destroy(screenBlood.gameObject);
        screenBlood = null;
        TeardownRide();
        ApplyFuneral(lucky);
        horrorNews = $"<b>BẢN TIN SÁNG</b> {lastWhen} {lastStreet}, nhiều người kể nghe tiếng kèn đám ma và tiếng cười rất đông lúc nửa đêm, sáng ra thấy vệt máu dài trên đường. Phường xác nhận: <b>không có đám tang nào</b> được đăng ký.";
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
        if (chanting != null) chanting.Stop();
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

    /// <summary>
    /// Đặt quan tài xuống, nắp trượt ra; ông cụ ngồi dậy (trồi lên khỏi lòng quan tài), vặn đầu nhìn bạn, cười khanh khách.
    /// quick = dựng sẵn (lần gặp lại): ông cụ đã ngồi trong quan tài mở nắp, đang cười.
    /// </summary>
    private IEnumerator CoffinOpen(bool quick)
    {
        if (coffin == null) yield break;
        foreach (Demon m in bearers) if (m.t != null) { m.target = m.t.position; m.arrived = true; }
        bearers.Clear();
        Vector3 road = coffin.position.WithY(GroundAt(coffin.position));
        coffin.SetPositionAndRotation(road + Vector3.up * 0.3f, coffin.rotation);
        if (coffinDrips != null) coffinDrips.transform.position = road + Vector3.up * 0.05f;
        HorrorFx.Pool(road + Vector3.up * 0.02f, 2.2f, Props());
        if (corpse != null) Destroy(corpse.gameObject);
        GameObject pf = CorpsePrefab();
        Quaternion faceYou = Quaternion.LookRotation((youPos - road).WithY(0f).sqrMagnitude > 0.01f ? (youPos - road).WithY(0f) : -youDir);
        corpse = pf != null ? RigMonster.Create(Props(), road, faceYou, pf, 1.62f, true) : null;
        if (corpse != null)
        {
            DressCorpse(corpse);
            corpse.mode = HorrorCreature.Mode.Stand;
            corpse.hunch = 0f; corpse.twitch = 0f;                    // ngồi im phăng phắc, mặt hướng thẳng về phía bạn
            corpse.headSpeed = 160f;
            corpse.AnimSpeed(0f);
        }
        Vector3 Sit() => road + Vector3.down * 0.5f;          // ngồi trong lòng quan tài: chân / hông khuất dưới thành quan tài
        if (quick)
        {
            if (coffinLid != null) { coffinLid.SetParent(Props(), true); coffinLid.position = road + coffin.right * 0.95f + Vector3.up * 0.12f; coffinLid.rotation = coffin.rotation * Quaternion.Euler(0f, 25f, 75f); }
            if (corpse != null)
            {
                corpse.transform.position = Sit();
                corpse.headTwistTarget = 0f;
                corpse.headPitch = -30f;                              // ngẩng mặt lên nhìn bạn mà cười
                corpse.Laugh(30f);
                HorrorAudio.Play3D(HorrorAudio.OldLaugh(), corpse.HeadPos, 1f, false, 22f, 140f);
            }
            yield break;
        }
        HorrorAudio.Play3D(HorrorAudio.Bang(), road, 0.6f, false, 4f, 60f);
        // Nắp trượt dần sang bên, đổ xuống đường
        Vector3 lid0 = coffinLid != null ? coffinLid.position : road;
        Quaternion lidR0 = coffinLid != null ? coffinLid.rotation : Quaternion.identity;
        if (coffinLid != null) coffinLid.SetParent(Props(), true);
        if (corpse != null) { corpse.transform.position = Sit(); corpse.Rise(1.4f, 3.2f); }
        float t0 = Time.unscaledTime;
        Say("", "<i>Họ đặt quan tài xuống giữa đường. Cạch... Nắp quan tài từ từ trượt ra.</i>", Color.white);
        while (Time.unscaledTime - t0 < 3.4f)
        {
            Drive();
            float u = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - t0) / 3.4f);
            if (coffinLid != null)
            {
                coffinLid.position = Vector3.Lerp(lid0, road + coffin.right * 0.95f + Vector3.up * 0.12f, u);
                coffinLid.rotation = Quaternion.Slerp(lidR0, coffin.rotation * Quaternion.Euler(0f, 25f, 75f), Mathf.Clamp01(u * 1.6f - 0.6f));
            }
            Vector3 c = coffin.position;
            Vector3 camP = c - youDir * 3.4f + Right(youDir) * 1.8f + Vector3.up * 0.7f;
            cam.transform.SetPositionAndRotation(camP, Quaternion.LookRotation(c + Vector3.up * 0.35f - camP));
            yield return null;
        }
        // Ông cụ ngồi dậy, từ từ quay đầu về phía bạn
        if (corpse != null) { corpse.headTwistTarget = 0f; corpse.jawOpen = 0.3f; }
        if (heartbeat != null) { heartbeat.pitch = 1.3f; }
        yield return Shot(3.2f, null, null,
            () => (coffin != null ? coffin.position - youDir * 2.6f + Right(youDir) * 1.4f + Vector3.up * 1.1f : youPos, corpse != null ? corpse.HeadPos : ProcCenter()),
            () => Say("", "<i>Một ông cụ tóc bạc, da trắng bệch, máu khô bê bết khoé miệng... từ từ NGỒI DẬY trong quan tài. Ngồi im phăng phắc, mắt nhìn thẳng vào bạn.</i>", Color.white));
        if (corpse != null)
        {
            corpse.Laugh(30f);
            corpse.twitch = 0f;
            corpse.headSpeed = 40f; corpse.headPitch = -30f;          // từ từ ngẩng mặt lên nhìn bạn mà cười
            HorrorAudio.Play3D(HorrorAudio.OldLaugh(), corpse.HeadPos, 1f, false, 22f, 140f);
            HorrorFx.Drips(corpse.HeadPos + corpse.transform.forward * 0.1f - Vector3.up * 0.12f, 6f, corpse.Anim != null ? corpse.Anim.GetBoneTransform(HumanBodyBones.Head) : corpse.transform);
        }
        cam.fieldOfView = 34f;
        yield return Shot(3.4f, null, null, () => FaceCamOf(corpse),
            () => Say("Ông cụ", "<i>Khà... khà... khà khà khà khà...</i>", MournCol));
        cam.fieldOfView = 52f;
    }

    /// <summary>Cả đoàn vặn ngược đầu (rắc rắc) nhìn bạn rồi cùng cười theo ông cụ; máu chảy ra từ miệng, khói hương đỏ quạch.</summary>
    private IEnumerator CrowdLaughs()
    {
        var list = new List<Demon>(demons);
        int i = 0;
        foreach (Demon m in list)
        {
            if (m.t == null) continue;
            float side = i % 2 == 0 ? 1f : -1f;
            m.c.headSpeed = 1400f;
            m.c.headTwistTarget = 180f * side;
            m.c.headTiltTarget = i % 3 == 0 ? 25f * side : 0f;
            m.c.twitch = 1.1f;
            i++;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.14f) { Drive(); yield return null; }
        }
        yield return Shot(2.2f, null, null,
            () => (youPos - youDir * 3.2f + Right(youDir) * 0.7f + Vector3.up * 1.35f, ProcCenter() + Vector3.up * 1.3f),
            () => Say("", "<i>Rắc. Rắc. Rắc. Từng cái đầu vặn ngược ra sau lưng... nhìn thẳng vào bạn.</i>", Color.white));
        // Cười theo
        foreach (Demon m in list)
        {
            if (m.t == null) continue;
            m.c.Laugh(40f);
            m.c.twitch = 0f; m.c.AnimSpeed(0f);                       // đứng im phăng phắc mà cười
            Transform head = m.c.Anim != null && m.c.Anim.isHuman ? m.c.Anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null && Random.value < 0.7f) HorrorFx.Drips(m.c.HeadPos - Vector3.up * 0.12f, 4f, head);
        }
        if (crowdLaugh != null) Destroy(crowdLaugh.gameObject);
        crowdLaugh = HorrorAudio.Play3D(HorrorAudio.CrowdLaugh(), ProcCenter() + Vector3.up * 1.5f, 1f, true, 30f, 160f);
        if (procMist != null) { var main = procMist.main; main.startColor = new Color(0.55f, 0.12f, 0.1f, 0.45f); }
        shake = 0.25f;
        yield return Shot(3.4f, null, null,
            () => (youPos - youDir * 3.2f + Right(youDir) * 0.7f + Vector3.up * 1.35f, ProcCenter() + Vector3.up * 1.3f),
            () => Say("", "<i>Rồi cả đoàn... cùng CƯỜI theo. Tiếng cười dội khắp con phố vắng. Máu ứa ra từ những cái miệng đang há hốc.</i>", Color.white));
        Demon face = FaceMover();
        cam.fieldOfView = 32f;
        yield return Shot(2.6f, null, null, () => FaceCam(face), () => Say("", "<i>Cười... cười mãi không dứt.</i>", Color.white));
        cam.fieldOfView = 52f;
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

    /// <summary>
    /// Cúi đầu chờ: tiếng cười tắt dần, mở mắt ra thì phố trống trơn. Đi giao tiếp như thường - con hề cứ lấp ló ven đường,
    /// mỗi lần một gần hơn, rồi nhảy xổ ra trước mặt.
    /// </summary>
    private IEnumerator FuneralWait()
    {
        if (heartbeat != null) heartbeat.volume = 0.4f;
        yield return Shot(3f, null, null,
            () => (RiderHead + Vector3.up * 0.05f - youDir * 0.25f, ProcCenter() + Vector3.up * 0.3f),
            () => Say("", "<i>Bạn tắt máy, cúi đầu thật thấp, nhắm chặt mắt. Tiếng cười cứ vang mãi... vang mãi... rồi nhỏ dần.</i>", Color.white));
        if (crowdLaugh != null) { StartCoroutine(FadeOutSource(crowdLaugh, 2.5f)); crowdLaugh = null; }
        yield return BlackHold(2.2f, "", "<i>...Im lặng.</i>");
        // Mở mắt: phố trống trơn, chỉ còn hương cháy dở, vàng mã, vũng máu
        Vector3 c = ProcCenter();
        ClearProcession();
        if (corpse != null) Destroy(corpse.gameObject);
        corpse = null;
        NightLight.SetAll(true);
        ScatterPaper(c, 7f, 60);
        Destroy(HorrorFx.Pool(c.WithY(GroundAt(c) + 0.02f), 2.6f, null), 120f);
        Incense(c.WithY(GroundAt(c)), Props(), 1.3f);
        yield return FadeIn(0.8f);
        yield return Shot(3.2f, null, null,
            () => (youPos + youDir * 4f + Right(youDir) * 2.5f + Vector3.up * 2.3f, c),
            () => Say("", "<i>Bạn mở mắt. Con phố trống trơn. Giữa đường chỉ còn vàng mã, một vũng máu... và một nắm hương vẫn đang cháy.</i>", Color.white));
        yield return PeekRide();
    }

    /// <summary>
    /// Đi giao tiếp (góc nhìn thứ nhất, ngồi trên xe): con hề lấp ló phía trước từng quãng ngắn - giật cục, chớp tắt, nhảy vị trí,
    /// đầu vặn lắc; mỗi lần một gần hơn, để lại vệt máu. Rồi "bộp" - máu rơi trúng kính chắn gió, chảy ròng ròng; qua lớp máu mờ mờ
    /// thấy nó đứng chắn ngay giữa đường... rồi nó lao thẳng vào mặt.
    /// </summary>
    private IEnumerator PeekRide()
    {
        stopAtS = float.NaN;
        float room = rail != null ? rail.Length - sYou - 30f : 80f;
        vYouWant = Mathf.Clamp(room / 18f, 3f, 6f);           // không chạy quá cuối đường ray
        GameObject pf = RigMonster.Prefab("WhiteClown") ?? FiendPrefab(true);
        RigMonster clown = pf != null ? RigMonster.Create(Props(), youPos + youDir * 40f, Quaternion.identity, pf, 1.95f) : null;
        if (clown != null) { DressFiend(clown, false, false); clown.gameObject.SetActive(false); clown.jawOpen = 0.4f; }
        // Mắt người lái nhìn ra phía trước (trong cabin / trên yên xe)
        Vector3 Eye() => rideIsBike ? RiderHead + youDir * 0.12f : LookBackEye + youDir * 0.1f;
        float lookYaw = 0f;
        void Pov(float jitter)
        {
            Vector3 e = Eye();
            Vector3 f = Quaternion.AngleAxis(lookYaw, Vector3.up) * youDir;
            cam.transform.SetPositionAndRotation(e + Random.insideUnitSphere * jitter, Quaternion.LookRotation(f * 10f + Vector3.down * 0.6f));
        }
        cam.fieldOfView = 62f;
        float tA = Time.unscaledTime;
        Say("Bạn", "Thôi... giao nốt đơn rồi về. Đừng nghĩ gì nữa.", YouSpeak);
        while (Time.unscaledTime - tA < 3f) { Drive(); Pov(0f); yield return null; }

        var peeks = new (float ahead, float side, float show, string line)[]
        {
            (30f, 1f, 1.1f, "<i>Ven đường phía trước... hình như có ai đứng nhìn?</i>"),
            (26f, -1f, 1.2f, "<i>Lại nữa. Bên kia đường. Một khuôn mặt trắng bệch... đang cười.</i>"),
            (22f, 0f, 1.2f, "<i>Nó đứng ngay giữa đường! Người nó giật giật như hình bị nhiễu...</i>"),
            (15f, 1f, 1.1f, "<i>Lần nào nó cũng gần hơn.</i>"),
            (10f, -1f, 1f, "<i>Gần hơn nữa...</i>"),
        };
        if (heartbeat == null) heartbeat = HorrorAudio.Play2D(HorrorAudio.Heartbeat(), 0.35f, true);
        for (int k = 0; k < peeks.Length; k++)
        {
            var pk = peeks[k];
            float gap = Random.Range(1.6f, 2.4f) - k * 0.15f;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < gap) { Drive(); lookYaw = Mathf.Lerp(lookYaw, 0f, 0.05f); Pov(0f); yield return null; }
            float lat = pk.side == 0f ? 0f : pk.side * (lane + 2.6f);
            rail.Sample(sYou + pk.ahead, lat, out Vector3 p, out Vector3 d);
            p.y = GroundAt(p);
            HorrorFx.Decal(HorrorFx.Drag(), p + Vector3.up * 0.015f, Vector3.up, d, new Vector2(0.8f, 2.2f), Props(), 0.9f);   // vệt máu nó để lại
            HorrorFx.Decal(HorrorFx.Splat(), p + Vector3.up * 0.016f, Vector3.up, d, new Vector2(1.1f, 1.1f), Props(), 0.9f);
            HorrorAudio.Play3D(k % 2 == 0 ? HorrorAudio.Whisper() : HorrorAudio.Breath(), p + Vector3.up * 1.5f, 0.8f, false, 10f, 60f);
            if (k == 2 || k == 4) HorrorAudio.Play3D(HorrorAudio.OldLaugh(), p + Vector3.up * 1.6f, 0.6f, false, 12f, 80f);
            Say("", pk.line, Color.white);
            if (heartbeat != null) { heartbeat.volume = 0.4f + k * 0.12f; heartbeat.pitch = 1f + k * 0.1f; }
            // Hiện ra kiểu giật cục: chớp tắt, nhảy chỗ, đầu lắc ngoặt, hoạt ảnh khựng / tua nhanh
            float t1 = Time.unscaledTime, nextGlitch = 0f;
            Vector3 basePos = p;
            while (Time.unscaledTime - t1 < pk.show)
            {
                Drive();
                if (clown != null && Time.unscaledTime >= nextGlitch)
                {
                    nextGlitch = Time.unscaledTime + Random.Range(0.04f, 0.13f);
                    bool on = Random.value > 0.28f;
                    clown.gameObject.SetActive(on);
                    if (on)
                    {
                        Vector3 jump = Right(youDir) * Random.Range(-0.35f, 0.35f) + youDir * Random.Range(-0.4f, 0.2f);
                        clown.transform.SetPositionAndRotation(basePos + jump, Quaternion.LookRotation((youPos - basePos).WithY(0f)) * Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f));
                        clown.mode = HorrorCreature.Mode.Stand;
                        clown.headSpeed = 4000f; clown.twitch = 1.8f;
                        clown.headTiltTarget = Random.Range(-60f, 60f);
                        clown.headTwistTarget = Random.value < 0.25f ? Random.Range(-140f, 140f) : 0f;
                        clown.hunch = Random.Range(-10f, 35f);
                        clown.AnimSpeed(Random.value < 0.5f ? 0f : Random.Range(1.5f, 3f));   // khựng hình / tua nhanh
                        if (Random.value < 0.3f) clown.Laugh(0.3f);
                    }
                    if (Random.value < 0.25f) HorrorAudio.Play2D(HorrorAudio.Static(), 0.12f);
                }
                // mắt người lái bị hút về phía nó
                Vector3 to = (basePos - youPos).WithY(0f);
                float want = Mathf.Clamp(Vector3.SignedAngle(youDir.WithY(0f), to, Vector3.up), -35f, 35f) * 0.6f;
                lookYaw = Mathf.Lerp(lookYaw, want, 0.08f);
                Pov(0.004f);
                yield return null;
            }
            if (clown != null) { clown.gameObject.SetActive(false); clown.AnimSpeed(-1f); }
        }
        // Yên ắng một lúc...
        Say("", "<i>Một quãng dài không thấy gì nữa. Bạn thở ra...</i>", Color.white);
        float tq = Time.unscaledTime;
        while (Time.unscaledTime - tq < 2.2f) { Drive(); lookYaw = Mathf.Lerp(lookYaw, 0f, 0.05f); Pov(0f); yield return null; }

        // Máu nhỏ xuống KÍNH CHẮN GIÓ (vật thật trên xe, không phải lên màn hình): từng giọt "tách", rồi chảy thành vệt dài
        Transform wind = new GameObject("WindshieldBlood").transform;
        wind.SetParent(Props(), false);
        void HoldWind() { if (car != null) wind.SetPositionAndRotation(car.transform.position, car.transform.rotation); }
        HoldWind();
        Vector3 E0 = Eye();
        float glassDist = rideIsBike ? 0.28f : 0.62f;
        Vector3 gN = (-youDir + Vector3.up * (rideIsBike ? 0.1f : 0.65f)).normalized;      // kính nghiêng về phía người lái
        Vector3 gUp = Vector3.ProjectOnPlane(Vector3.up, gN).normalized;
        Vector3 gRight = Vector3.Cross(gUp, gN).normalized;
        Vector3 gC = E0 + youDir * glassDist + Vector3.down * 0.04f;
        var trails = new List<(Transform t, Vector3 top, float len, float speed)>();
        vYouWant = 1.2f;
        rail.Sample(sYou + 9f, 0f, out Vector3 sp, out _);
        sp.y = GroundAt(sp);
        Say("", rideIsBike ? "<i>Tách... tách... Có gì đó nhỏ xuống kính mũ bảo hiểm. Màu đỏ.</i>" : "<i>Tách... tách... Có gì đó nhỏ xuống kính chắn gió. Màu đỏ.</i>", Color.white);
        float tb = Time.unscaledTime, nextDrop = 0f, nextG = 0f;
        int drops = 0;
        while (Time.unscaledTime - tb < 5.4f)
        {
            Drive();
            HoldWind();
            float now = Time.unscaledTime - tb;
            if (now >= nextDrop && drops < 30)
            {
                nextDrop = now + Mathf.Lerp(0.4f, 0.07f, drops / 29f);                    // nhỏ giọt dồn dập dần
                drops++;
                Vector3 at = gC + gRight * Random.Range(-0.32f, 0.32f) * (rideIsBike ? 0.5f : 1f) + gUp * Random.Range(0.05f, 0.25f);
                float sz = (drops % 7 == 0 ? Random.Range(0.14f, 0.22f) : Random.Range(0.05f, 0.12f)) * (rideIsBike ? 0.6f : 1f);
                HorrorFx.Decal(HorrorFx.Splat(), at, gN, gUp, new Vector2(sz, sz), wind, 0.95f);
                if (Random.value < 0.8f)
                {
                    GameObject tr = HorrorFx.Decal(HorrorFx.Drag(), at, gN, gUp, new Vector2(sz * 0.35f, 0.01f), wind, 0.9f);
                    trails.Add((tr.transform, tr.transform.localPosition, 0.01f, Random.Range(0.04f, 0.1f)));
                }
                HorrorAudio.Play2D(HorrorAudio.Drip(), 0.7f);
            }
            for (int i = 0; i < trails.Count; i++)                                      // vệt máu chảy dài xuống
            {
                var tr = trails[i];
                if (tr.t == null) continue;
                tr.len = Mathf.Min(tr.len + tr.speed * Time.unscaledDeltaTime, 0.35f);
                Vector3 downLocal = Quaternion.Inverse(wind.rotation) * (-gUp);
                tr.t.localPosition = tr.top + downLocal * tr.len * 0.5f;
                Vector3 s = tr.t.localScale; tr.t.localScale = new Vector3(s.x, tr.len, s.z);
                trails[i] = tr;
            }
            // Cùng lúc: qua lớp máu, nó lờ mờ đứng giữa đường, chớp giật
            if (clown != null && now > 0.8f && Time.unscaledTime >= nextG)
            {
                nextG = Time.unscaledTime + Random.Range(0.05f, 0.2f);
                bool on = Random.value > 0.4f;
                clown.gameObject.SetActive(on);
                if (on)
                {
                    clown.transform.SetPositionAndRotation(sp + Right(youDir) * Random.Range(-0.25f, 0.25f), Quaternion.LookRotation((youPos - sp).WithY(0f)));
                    clown.mode = HorrorCreature.Mode.Stand;
                    clown.headTiltTarget = Random.Range(-45f, 45f); clown.headSpeed = 4000f; clown.twitch = 2f;
                    clown.AnimSpeed(Random.value < 0.5f ? 0f : 2.5f);
                }
            }
            lookYaw = Mathf.Lerp(lookYaw, 0f, 0.1f);
            Pov(0.002f);
            yield return null;
        }
        // Biến mất. Im bặt. Tim ngừng đập...
        if (clown != null) clown.gameObject.SetActive(false);
        if (heartbeat != null) heartbeat.volume = 0f;
        vYouWant = 0f;
        Say("", "", Color.white);
        float tw = Time.unscaledTime;
        while (Time.unscaledTime - tw < 1.3f) { Drive(); HoldWind(); Pov(0f); yield return null; }

        // ...rồi từ xa nó LAO tới: người nằm ngang như bay, hai tay duỗi thẳng ra trước đòi vồ, mặt ngẩng lên nhìn bạn
        Vector3 eye = Eye();
        rail.Sample(sYou + 30f, 0f, out Vector3 far, out _);
        Vector3 farP = far.WithY(eye.y - 0.25f);
        Vector3 endP = eye + youDir * 0.55f + Vector3.down * 0.2f;
        if (clown != null)
        {
            clown.gameObject.SetActive(true);
            clown.mode = HorrorCreature.Mode.Crawl; clown.gait = 3f;
            clown.AnimSpeed(0f);                                                        // khựng ở một dáng chạy
            clown.reachArms = true;
            clown.headPitch = -78f; clown.headTiltTarget = 0f; clown.headTwistTarget = 0f; clown.headSpeed = 6000f; clown.twitch = 0.6f;
            clown.Laugh(3f);
        }
        HorrorAudio.Play2D(HorrorAudio.Scream(), 1f);
        HorrorAudio.Play2D(HorrorAudio.Sting(), 0.9f);
        float tj = Time.unscaledTime;
        const float dur = 1.05f;                                                        // đủ để thấy rõ mặt nó, vẫn đủ nhanh để giật mình
        while (Time.unscaledTime - tj < dur)
        {
            Drive(); HoldWind();
            float u = Mathf.Clamp01((Time.unscaledTime - tj) / dur);
            float k = Mathf.Pow(u, 2.3f);                                               // xa thì chậm (thấy rõ nó), gần thì vụt tới
            if (clown != null)
            {
                Vector3 toMe = (eye - farP).normalized;
                // thân nằm ngang: trục "trên" của thân chĩa thẳng về phía người chơi, bụng úp xuống
                Quaternion lie = Quaternion.LookRotation(Vector3.down, toMe);
                Vector3 p = Vector3.Lerp(farP, endP, k);
                clown.transform.SetPositionAndRotation(p, lie);
                clown.transform.position += p - clown.HeadPos;                              // đầu bay đúng đường thẳng tới mặt bạn
            }
            Vector3 look = clown != null ? clown.HeadPos : eye + youDir;
            cam.transform.SetPositionAndRotation(eye + Random.insideUnitSphere * 0.04f * u, Quaternion.LookRotation(Vector3.Lerp(youDir, (look - eye).normalized, 0.8f)));
            yield return null;
        }
        if (clown != null) { clown.transform.position += endP - clown.HeadPos; }
        Scare();
        BloodOnScreen();
        shake = 1f;
        yield return Shot(0.6f, null, null, () => (eye, clown != null ? clown.HeadPos : eye + youDir), () => { });
        if (wind != null) Destroy(wind.gameObject);
        if (clown != null) Destroy(clown.gameObject);
        if (heartbeat != null) { Destroy(heartbeat.gameObject); heartbeat = null; }
        cam.fieldOfView = 52f;
        vYou = vYouWant = 0f;
        yield return BlackHold(1.8f, "", "");
        LeaveHandprints();
        yield return FadeIn(0.8f);
        yield return Shot(3f, null, null,
            () => (youPos + DoorOut * 2.2f + youDir * 0.5f + Vector3.up * 1.3f, youPos + Vector3.up * (rideIsBike ? 0.9f : 1.1f)),
            () => Say("", rideIsBike ? "<i>Xe dừng giữa đường. Trên yên và tay lái... in đầy dấu bàn tay máu.</i>" : "<i>Xe dừng giữa đường. Trên cửa kính... in đầy dấu bàn tay máu.</i>", Color.white));
        yield return Shot(3f, null, null,
            () => (youPos + youDir * 3.2f + Right(youDir) * 0.9f + Vector3.up * 1.6f, RiderHead),
            () => Say("Bạn", "Giao xong đơn này... mình về. Ngay.", YouSpeak));
    }

    private static IEnumerator FadeOutSource(AudioSource s, float seconds)
    {
        if (s == null) yield break;
        float v0 = s.volume, t0 = Time.unscaledTime;
        while (s != null && Time.unscaledTime - t0 < seconds) { s.volume = v0 * (1f - (Time.unscaledTime - t0) / seconds); yield return null; }
        if (s != null) Destroy(s.gameObject);
    }

    /// <summary>Góc (độ) cần vặn đầu từ hướng thân sang hướng một điểm (để nhìn thẳng vào người chơi).</summary>
    private static float SignedYaw(Transform t, Vector3 at)
    {
        Vector3 to = (at - t.position).WithY(0f);
        if (to.sqrMagnitude < 0.01f) return 0f;
        float a = Vector3.SignedAngle(t.forward.WithY(0f), to, Vector3.up);
        return Mathf.Clamp(a, -175f, 175f);
    }

    private (Vector3, Vector3) FaceCamOf(RigMonster c)
    {
        if (c == null) return (youPos + Vector3.up * 1.5f, youPos + youDir * 10f);
        Vector3 h = c.HeadPos;
        Vector3 to = RiderHead - h; to.y = 0f; to = to.sqrMagnitude > 0.01f ? to.normalized : -youDir;
        return (h + to * 1.2f + Vector3.up * 0.05f, h);
    }

    private IEnumerator FuneralHonk()
    {
        AudioManager.Play(Random.value < 0.5f ? "horn_1" : "horn_2", 0.7f);
        yield return Shot(1.6f, null, null, () => (RiderHead - youDir * 0.3f + Vector3.up * 0.1f, ProcCenter() + Vector3.up * 1.2f),
            () => Say("", "<i>BÍNNN! Tiếng còi xé toạc màn đêm.</i>", Color.white));
        // Tiếng cười tắt phụt. Im lặng chết chóc.
        if (crowdLaugh != null) { Destroy(crowdLaugh.gameObject); crowdLaugh = null; }
        foreach (Demon m in demons) if (m.t != null) { m.c.StopLaugh(); m.c.jawOpen = 0.8f; m.c.twitch = 1.6f; m.c.AnimSpeed(-1f); }
        if (corpse != null) { corpse.StopLaugh(); corpse.jawOpen = 1f; corpse.headPitch = float.NaN; corpse.AnimSpeed(-1f); }
        if (heartbeat != null) { heartbeat.pitch = 1.6f; heartbeat.volume = 1f; }
        yield return Shot(2.6f, null, null,
            () => (coffin != null ? coffin.position - youDir * 3.4f + Right(youDir) * 1.6f + Vector3.up * 0.9f : youPos, corpse != null ? corpse.HeadPos : ProcCenter()),
            () => Say("", "<i>Tiếng cười tắt phụt. Im lặng. Ông cụ trong quan tài há miệng thật rộng... rồi trèo ra ngoài.</i>", Color.white));
        if (corpse != null) corpse.Scream(1f);
        shake = 0.5f;
        // Cả đoàn đổ rạp xuống lao tới, ông cụ bò bằng bốn chân phóng tới
        foreach (Demon m in demons)
        {
            if (m.t == null) continue;
            DemonGo(m, Vector3.Lerp(m.t.position, youPos, 0.65f), 3.6f);
            m.c.mode = HorrorCreature.Mode.Crawl; m.c.gait = 2.2f;
            m.c.headTwistTarget = 0f; m.c.twitch = 1.6f;
        }
        if (corpse != null) { corpse.mode = HorrorCreature.Mode.Crawl; corpse.gait = 2.4f; corpse.headTwistTarget = 0f; }
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
                p.y = Mathf.Lerp(cStart.y, GroundAt(p), Mathf.Clamp01(u * 3f));       // trèo ra khỏi quan tài
                corpse.transform.SetPositionAndRotation(p, Quaternion.LookRotation((-youDir).WithY(0f)));
            }
            cam.transform.SetPositionAndRotation(camPos + Random.insideUnitSphere * 0.02f * u, Quaternion.LookRotation((corpse != null ? corpse.HeadPos : ProcCenter()) - camPos));
            yield return null;
        }
        if (corpse != null) { corpse.mode = HorrorCreature.Mode.Reach; corpse.PlaceHeadAt(RiderHead + youDir * 0.75f); corpse.Scream(1f); }
        Scare();
        BloodOnScreen();
        yield return Shot(0.5f, null, null, () => (RiderHead, corpse != null ? corpse.HeadPos : RiderHead + youDir), () => { });
        if (heartbeat != null) heartbeat.Stop();
        ClearProcession();
        if (corpse != null) Destroy(corpse.gameObject);
        corpse = null;
        // Hiện trường: vàng mã, vũng máu, hương cháy dở, dấu tay máu trên xe
        ScatterPaper(youPos + youDir * 9f, 9f, 70);
        Vector3 poolAt = youPos + youDir * 6f;
        Destroy(HorrorFx.Pool(poolAt.WithY(GroundAt(poolAt) + 0.02f), 3.2f, null), 120f);
        Incense(poolAt.WithY(GroundAt(poolAt)) + youDir * 1.2f, Props(), 1.2f);
        LeaveHandprints();
        yield return BlackHold(1.8f, "", "");
        NightLight.SetAll(true);
        yield return FadeIn(0.8f);
        yield return Shot(3.2f, null, null,
            () => (youPos + youDir * 4f + Right(youDir) * 2.5f + Vector3.up * 2.3f, youPos + youDir * 9f),
            () => Say("", "<i>Đèn đường sáng lại. Con phố trống trơn - chỉ còn vàng mã, nắm hương cháy dở và một vũng máu lớn giữa đường.</i>", Color.white));
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

    /// <summary>Dấu bàn tay máu lên xe (kính cửa ô tô / yên xe máy): còn nguyên suốt đêm, sáng hôm sau thì biến mất.</summary>
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
        rideBlood.Add(holder.gameObject);
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
            float height = sl.tag == "old" ? 1.55f : Random.Range(1.62f, 1.78f);
            GameObject pf = MournerPrefab(idx);
            RigMonster c = pf != null ? RigMonster.Create(procession, at, Quaternion.LookRotation(standing ? dir : across), pf, height, true) : null;
            if (c == null) { idx++; continue; }
            DressMourner(c.gameObject, c.Anim, sl.tag == "hood" || sl.tag == "banner" || sl.tag == "portrait" || sl.tag == "old");
            c.twitch = 0.15f; c.hunch = Random.Range(4f, 12f);   // cúi đầu lặng lẽ
            if (sl.tag != "bear" && sl.tag != "banner" && sl.tag != "portrait") HoldIncense(c);
            var m = new Demon { t = c.transform, c = c, target = at, face = at + dir * 5f, speed = 0.9f, old = sl.tag == "old" };
            demons.Add(m);
            idx++;
            if (standing)
            {
                c.mode = HorrorCreature.Mode.Stand;
                c.headSpeed = 5000f; c.headTwistTarget = 180f * (idx % 2 == 0 ? 1f : -1f); c.headTiltTarget = idx % 3 == 0 ? 30f : 0f;
                c.twitch = 1.1f;
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
        // Bát hương + hai ngọn nến trên nắp quan tài, khói nghi ngút; ánh nến đỏ hắt lên
        var altar = new GameObject("CoffinAltar");
        altar.transform.SetParent(coffinLid, false);
        altar.transform.localPosition = new Vector3(0f, 0.42f, 0.72f);
        altar.AddComponent<MeshFilter>().sharedMesh = AltarMesh();
        altar.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        HorrorFx.Smoke(altar.transform.position + Vector3.up * 0.32f, altar.transform, 7f, 0.16f, new Color(0.82f, 0.82f, 0.8f, 0.5f));
        var candle = new GameObject("CandleLight").AddComponent<Light>();
        candle.transform.SetParent(altar.transform, false);
        candle.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        candle.type = LightType.Point; candle.range = 4.5f; candle.intensity = 1.6f; candle.color = new Color(1f, 0.55f, 0.25f);
        candle.gameObject.AddComponent<CandleFlicker>();
        // Sương khói hương bao quanh đoàn người
        procMist = HorrorFx.Smoke(coffin.position - Vector3.up * 1.2f, coffin, 5f, 2.2f, new Color(0.7f, 0.72f, 0.72f, 0.28f), 0.12f, 6f);
        // Vàng mã bay
        Vector3 c0 = ProcCenter();
        paperWhite = PaperFall(c0, new Color(0.96f, 0.95f, 0.9f), standing ? 1f : 7f);
        paperGold = PaperFall(c0, standing ? HorrorFx.BloodWet : new Color(0.95f, 0.78f, 0.25f), standing ? 3f : 4f);
        paperHell = PaperFall(c0, Color.white, standing ? 3f : 6f, true);                 // tiền âm phủ rải theo đoàn
        HorrorFx.ScatterHellMoney(c0.WithY(GroundAt(c0)), 4f, standing ? 50 : 15, Props());
        if (!standing) funeralMusic = HorrorAudio.Play3D(HorrorAudio.Funeral(), c0 + Vector3.up * 1.5f, 1f, true, 32f, 220f, coffin);   // to, vọng xa
        if (!standing && HorrorAudio.HasRecorded("FuneralVN") && HorrorAudio.Chanting() != null)
            chanting = HorrorAudio.Play3D(HorrorAudio.Chanting(), c0 + Vector3.up * 1.5f, 0.9f, true, 25f, 180f, coffin);   // tiếng tụng kinh đi cùng đoàn
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
        coffin.SetPositionAndRotation(c + Vector3.up * (1.42f + bob), rot);   // trên vai người khiêng
        if (paperWhite != null) paperWhite.transform.position = c + Vector3.up * 4f;
        if (paperGold != null) paperGold.transform.position = c + Vector3.up * 4f;
        if (paperHell != null) paperHell.transform.position = c + Vector3.up * 4f;
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
        procession = null; coffin = null; coffinLid = null; paperWhite = paperGold = paperHell = null; coffinDrips = null; procMist = null;
        if (funeralMusic != null) Destroy(funeralMusic.gameObject);
        if (crowdLaugh != null) Destroy(crowdLaugh.gameObject);
        if (chanting != null) Destroy(chanting.gameObject);
        funeralMusic = null; crowdLaugh = null; chanting = null;
    }

    private void StopFuneralAudio()
    {
        if (funeralMusic != null) Destroy(funeralMusic.gameObject);
        if (heartbeat != null) Destroy(heartbeat.gameObject);
        if (crowdLaugh != null) Destroy(crowdLaugh.gameObject);
        funeralMusic = heartbeat = crowdLaugh = null;
        NightLight.SetAll(true);
    }

    private static void ApplyFuneral(bool lucky)
    {
        EconomyManager eco = EconomyManager.Instance;
        if (lucky)
        {
            if (eco != null) eco.AddIncome(20f, MoneyCategory.Other, "A stranger's lucky money (funeral night)");
            StoreProgress.AddXP(6);
            ProgressHUD.Toast("<color=#BFD7FF>Về tới nơi, trong túi bạn có thêm một phong bao đỏ dính máu: +$20 \"lộc\", +6 XP.</color>");
        }
        else
        {
            float cash = eco != null ? Mathf.Round(Mathf.Clamp(eco.Money * 0.1f, 25f, 80f)) : 0f;
            if (eco != null && cash > 0f) eco.Charge(cash, MoneyCategory.Other, "Cash turned into joss paper (funeral night)");
            ProgressHUD.Toast($"<color=#FF8A7A>Mở ví ra... toàn vàng mã dính máu. Mất {EconomyManager.Format(cash)} tiền mặt.</color>");
        }
    }

    // ================= Quái (model thật) =================
    /// <summary>Con hề (clown = true) hoặc con trùm khăn; thiếu con nào thì dùng con còn lại.</summary>
    private static GameObject FiendPrefab(bool clown)
    {
        GameObject a = RigMonster.Prefab(clown ? "WhiteClown" : "Hooded");
        if (a != null) return a;
        GameObject[] all = RigMonster.Prefabs();
        return all.Length > 0 ? all[Random.Range(0, all.Length)] : null;
    }

    /// <summary>Máu me loang khắp người (vẽ lên texture, giữ nguyên màu gốc), khăn tang / mũ mấn trắng dính máu, máu nhỏ giọt từ miệng.</summary>
    private static void DressFiend(RigMonster c, bool hood, bool mourning)
    {
        if (c == null) return;
        IEnumerator e = PaintOutfitCo(c.gameObject, false, null, FiendTag, FiendGore);
        while (e.MoveNext()) { }
        if (mourning && c.Anim != null)
        {
            MourningMeshes();
            AttachToBone(c.Anim, HumanBodyBones.Head, hood ? hoodMesh : headbandMesh, new Vector3(0f, 0.13f, 0.005f), hood ? "MourningHood" : "Headband");
        }
        Transform head = c.Anim != null && c.Anim.isHuman ? c.Anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head != null && Random.value < 0.6f) HorrorFx.Drips(c.HeadPos + c.transform.forward * 0.08f - Vector3.up * 0.1f, 3f, head);
    }

    // ================= Người đưa tang / ông cụ trong quan tài =================
    /// <summary>Model người cho đoàn đưa tang (lấy vòng quanh danh sách đã chuẩn bị từ chập tối).</summary>
    private GameObject MournerPrefab(int i)
    {
        if (mournerPrefabs.Count > 0) return mournerPrefabs[i % mournerPrefabs.Count];
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        return spawner != null ? spawner.RandomCustomerPrefab() : null;
    }

    private GameObject CorpsePrefab()
    {
        if (corpsePrefab != null) return corpsePrefab;
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        return spawner != null ? spawner.RandomCustomerPrefab() : MournerPrefab(3);
    }

    /// <summary>Ông cụ trong quan tài: áo liệm trắng, da trắng bệch xám xanh, máu khô bê bết; khăn tang trắng trên đầu.</summary>
    private static void DressCorpse(RigMonster c)
    {
        IEnumerator e = PaintOutfitCo(c.gameObject, false, MourningColors, CorpseTag, 1f);
        while (e.MoveNext()) { }
        if (c.Anim != null && c.Anim.isHuman)
        {
            MourningMeshes();
            AttachToBone(c.Anim, HumanBodyBones.Head, headbandMesh, new Vector3(0f, 0.13f, 0.005f), "Headband");
        }
    }

    /// <summary>Nắm ba nén hương ở tay phải, đầu hương đỏ, khói bay lên theo bước đi.</summary>
    private static void HoldIncense(RigMonster c)
    {
        if (c.Anim == null || !c.Anim.isHuman) return;
        GameObject g = AttachToBone(c.Anim, HumanBodyBones.RightHand, IncenseMesh(), new Vector3(0f, -0.02f, 0.04f), "Incense");
        if (g == null) return;
        HorrorFx.Smoke(g.transform.position + Vector3.up * 0.34f, g.transform, 4f, 0.07f, new Color(0.85f, 0.85f, 0.83f, 0.45f), 0.3f, 2.6f);
    }

    /// <summary>Nắm hương cắm dưới đất (cháy dở, khói).</summary>
    private static void Incense(Vector3 at, Transform parent, float scale)
    {
        var g = new GameObject("IncenseOnRoad");
        g.transform.SetParent(parent, true);
        g.transform.SetPositionAndRotation(at, Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f)));
        g.transform.localScale = Vector3.one * scale;
        g.AddComponent<MeshFilter>().sharedMesh = IncenseMesh();
        g.AddComponent<MeshRenderer>().sharedMaterial = VPalette.Material;
        HorrorFx.Smoke(at + Vector3.up * 0.36f * scale, g.transform, 5f, 0.08f, new Color(0.85f, 0.85f, 0.83f, 0.5f), 0.3f, 3f);
    }

    /// <summary>Ba nén hương (thân đỏ nâu, chân tre vàng, đầu than hồng).</summary>
    private static Mesh IncenseMesh()
    {
        if (incenseMesh != null) return incenseMesh;
        var mb = new VMeshBuilder();
        Color stick = new Color(0.55f, 0.12f, 0.08f), bamboo = new Color(0.85f, 0.7f, 0.35f), ember = new Color(1f, 0.35f, 0.08f);
        for (int k = -1; k <= 1; k++)
        {
            Quaternion r = Quaternion.Euler(k * 4f, 0f, k * 5f);
            Vector3 o = new Vector3(k * 0.012f, 0f, 0f);
            mb.Add(VShape.Cylinder(6), Matrix4x4.TRS(o + r * new Vector3(0f, 0.06f, 0f), r, new Vector3(0.006f, 0.12f, 0.006f)), bamboo);
            mb.Add(VShape.Cylinder(6), Matrix4x4.TRS(o + r * new Vector3(0f, 0.22f, 0f), r, new Vector3(0.009f, 0.2f, 0.009f)), stick);
            mb.Add(VShape.Sphere(6), Matrix4x4.TRS(o + r * new Vector3(0f, 0.325f, 0f), r, new Vector3(0.012f, 0.02f, 0.012f)), ember);
        }
        incenseMesh = mb.ToMesh(RealModels.MeshPrefix + "_Incense");
        incenseMesh.hideFlags = HideFlags.DontSave;
        return incenseMesh;
    }

    /// <summary>Bát hương sứ (vẽ chữ Thọ), nhang cắm, hai ngọn nến đỏ hai bên, đĩa hoa quả.</summary>
    private static Mesh AltarMesh()
    {
        if (altarMesh != null) return altarMesh;
        var mb = new VMeshBuilder();
        Color bowl = new Color(0.92f, 0.9f, 0.82f), blue = new Color(0.2f, 0.3f, 0.6f), ash = new Color(0.55f, 0.53f, 0.5f), red = new Color(0.75f, 0.06f, 0.05f), flame = new Color(1f, 0.75f, 0.25f);
        mb.Add(VShape.Cylinder(14), Matrix4x4.TRS(new Vector3(0f, 0.05f, 0f), Quaternion.identity, new Vector3(0.16f, 0.1f, 0.16f)), bowl);
        mb.Add(VShape.Cylinder(14), Matrix4x4.TRS(new Vector3(0f, 0.06f, 0f), Quaternion.identity, new Vector3(0.162f, 0.025f, 0.162f)), blue);
        mb.Add(VShape.Cylinder(14), Matrix4x4.TRS(new Vector3(0f, 0.1f, 0f), Quaternion.identity, new Vector3(0.14f, 0.005f, 0.14f)), ash);
        for (int k = 0; k < 5; k++)
        {
            float a = k * 72f * Mathf.Deg2Rad;
            Vector3 o = new Vector3(Mathf.Cos(a) * 0.03f, 0.2f, Mathf.Sin(a) * 0.03f);
            mb.Add(VShape.Cylinder(6), Matrix4x4.TRS(o, Quaternion.Euler(Mathf.Sin(a) * 6f, 0f, Mathf.Cos(a) * 6f), new Vector3(0.007f, 0.2f, 0.007f)), new Color(0.55f, 0.12f, 0.08f));
            mb.Add(VShape.Sphere(6), Matrix4x4.TRS(o + Vector3.up * 0.1f, Quaternion.identity, new Vector3(0.012f, 0.016f, 0.012f)), new Color(1f, 0.35f, 0.08f));
        }
        for (int s = -1; s <= 1; s += 2)
        {
            mb.Add(VShape.Cylinder(10), Matrix4x4.TRS(new Vector3(s * 0.2f, 0.09f, 0f), Quaternion.identity, new Vector3(0.04f, 0.18f, 0.04f)), red);
            mb.Add(VShape.Sphere(8), Matrix4x4.TRS(new Vector3(s * 0.2f, 0.2f, 0f), Quaternion.identity, new Vector3(0.022f, 0.045f, 0.022f)), flame);
        }
        mb.Add(VShape.Cylinder(12), Matrix4x4.TRS(new Vector3(0f, 0.01f, -0.18f), Quaternion.identity, new Vector3(0.2f, 0.02f, 0.2f)), new Color(0.85f, 0.7f, 0.3f));
        for (int k = 0; k < 4; k++)
            mb.Add(VShape.Sphere(8), Matrix4x4.TRS(new Vector3(-0.05f + (k % 2) * 0.1f, 0.05f + (k / 2) * 0.05f, -0.18f), Quaternion.identity, Vector3.one * 0.06f), k % 2 == 0 ? new Color(0.9f, 0.55f, 0.1f) : new Color(0.3f, 0.6f, 0.15f));
        altarMesh = mb.ToMesh(RealModels.MeshPrefix + "_Altar");
        altarMesh.hideFlags = HideFlags.DontSave;
        return altarMesh;
    }

    // ================= Đạo cụ =================
    /// <summary>Đạo cụ (phướn / di ảnh) gắn trước người con quỷ đi đầu.</summary>
    private static void HoldDemonProp(RigMonster c, Mesh mesh, Vector3 local)
    {
        var g = new GameObject(mesh.name);
        g.transform.SetParent(c.transform, false);
        float k = Mathf.Max(1e-4f, c.transform.localScale.y);     // bù tỉ lệ của model (giữ đạo cụ đúng cỡ thật)
        g.transform.localScale = Vector3.one / k;
        g.transform.localPosition = local / k;
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
    private ParticleSystem PaperFall(Vector3 at, Color col, float rate, bool hellMoney = false)
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
        r.mesh = hellMoney ? HorrorFx.HellNoteMesh() : PaperMesh();
        r.sharedMaterial = hellMoney ? HorrorFx.HellNoteMaterial() : RuntimeMaterials.Lit(col);
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
        Destroy(HorrorFx.ScatterHellMoney(g.transform.position, radius * 0.9f, count, null), 90f);   // tiền âm phủ vương vãi
    }
}
