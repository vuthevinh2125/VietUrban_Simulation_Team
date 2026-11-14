using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "ĐƠN GIAO CHO CHÍNH MÌNH" (~75 s): đang giao hàng đêm, điện thoại rung - đơn lạ: người nhận "Chủ siêu thị VietUrban",
/// địa chỉ chính cửa hàng của bạn, giờ giao "ngày mai". Không huỷ được. Tin nhắn liên tiếp: "Tôi đang đợi bạn." -
/// "Bạn quên cái này." kèm một tấm ảnh chụp chính chiếc xe của bạn từ ngay phía sau, vừa chụp. Ngoái lại: đường trống trơn,
/// chỉ có một vệt máu kéo lê dài dẫn tới tận bánh xe. Bạn vòng về cửa hàng: lẽ ra đang mở nhưng tối om; trên cửa kính
/// hai vệt tay máu kéo dài từ trên xuống; ở quầy là "bạn" (cùng quần áo, mặt tái xám, máu loang) đứng quay lưng - cái đầu vặn ngược 180 độ nhìn ra;
/// rồi nó chậm rãi đi ra cửa, để lại dấu chân máu, áp mặt và hai bàn tay máu lên kính. Lựa chọn (6 s):
///  - Mở cửa vào xem: trong tối, dấu chân máu dẫn ra sau quầy; điện thoại trên quầy sáng màn hình "Người nhận: BẠN"... sau lưng: nó.
///  - Gọi điện về cửa hàng: nó nhấc máy, giọng của bạn: "...Thế ai đang đứng ngoài cửa kia?" - rồi đập sầm vào kính.
///  - Quay xe bỏ chạy: trong gương, nó chạy đuổi theo, đầu vặn ngược, nhanh hơn cả xe... rồi ở ngay bên cạnh.
/// Giật mình tỉnh lại giữa đường giao hàng; điện thoại: "Đơn #0000 đã giao thành công, người nhận ký bằng chữ ký của bạn";
/// trên xe in một dấu tay máu. Sáng hôm sau: camera cửa hàng ghi được "bạn" đứng ở quầy lúc 22:47.
/// </summary>
public partial class NightRobbery
{
    private IEnumerator RunDouble(PlayerCarController ride)
    {
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (city == null || city.peopleModels == null || city.peopleModels.Length == 0) yield break;
        if (!SetupRide(ride, city)) yield break;
        if (!mournersPrepped) StartCoroutine(PrepMourners());
        GameManager gm = GameManager.Instance;
        int day = gm != null ? gm.currentDay : 1;
        string tomorrow = GameManager.DateOfDay(day + 1).ToString("dd/MM");
        (Vector3, Vector3) Phone() => (RiderHead + youDir * 0.6f + Vector3.up * 0.15f, RiderHead);

        // ===== 1. Đơn lạ =====
        vYou = vYouWant = 7.5f;
        yield return Shot(3f, "ĐƠN HÀNG ĐÊM", Subtitle(),
            () => (youPos + youDir * 7f + Right(youDir) * 2f + Vector3.up * 1.5f, youPos + Vector3.up * 0.9f),
            () => Say("Bạn", "Đơn cuối rồi. Giao xong là về đóng cửa hàng.", YouSpeak));
        vYouWant = 0f;
        PhoneNote("Đơn mới #0000 - giao gấp", $"Người nhận: <b>CHỦ SIÊU THỊ VIETURBAN</b>\nĐịa chỉ: <b>Siêu thị VietUrban</b> (cửa hàng của bạn)\nGiao lúc: <color=#FF6B5B>22:47 - {tomorrow} (ngày mai)</color>");
        yield return Shot(3.6f, null, null, Phone,
            () => Say("Bạn", "Đơn gửi tới... chính cửa hàng mình? Người nhận là mình? Lỗi app à?", YouSpeak));
        HorrorAudio.Play2D(HorrorAudio.Static(), 0.25f);
        yield return Shot(2.6f, null, null, Phone,
            () => Say("", "<i>Bạn bấm huỷ. Đơn không huỷ được. Bấm lại... màn hình nhiễu sọc.</i>", Color.white));
        // ===== 2. Tin nhắn + tấm ảnh chụp từ phía sau =====
        PhoneNote("Tin nhắn từ: Người nhận #0000", "<i>Tôi đang đợi bạn.</i>");
        yield return Shot(2.6f, null, null, Phone, () => Say("", "", Color.white));
        // Tấm ảnh chụp từ phía sau: ngay sau xe có "bạn" đứng nhìn - mặt trắng bệch, máu loang, đầu nghiêng gãy
        Mover stalker = MakeDouble((youPos - youDir * (rideIsBike ? 2.4f : carHalfL + 1.4f) + Right(youDir) * 0.5f).WithY(GroundAt(youPos)), -youDir);
        if (stalker != null) { Contort sc = stalker.t.GetComponent<Contort>(); if (sc != null) { sc.speed = 9000f; sc.tiltTarget = 40f; sc.neckStretchTarget = 1.3f; } }
        Texture photo = SnapBehind();
        if (stalker != null) { ghouls.Remove(stalker); Destroy(stalker.t.gameObject); }
        PhoneNote("Tin nhắn từ: Người nhận #0000", "<i>Bạn quên cái này.</i>   <color=#9AA3AE>[ảnh - vừa xong]</color>");
        RawImage pic = null;
        if (phonePanel != null && photo != null)
        {
            pic = UIKit.Rect("Photo", phonePanel.transform, new Vector2(320, 180), new Vector2(0, -200)).AddComponent<RawImage>();
            pic.texture = photo;
        }
        yield return Shot(3.8f, null, null, Phone,
            () => Say("Bạn", "Ảnh chụp xe mình... từ ngay phía sau. Và người đứng sau xe... mặc đúng cái áo của mình?!", YouSpeak));
        ClosePhone();
        // ===== 3. Ngoái lại: vệt máu kéo lê tới tận bánh xe =====
        for (int k = 1; k <= 6; k++)
        {
            rail.Sample(sYou - k * 2.2f, 0.2f * Mathf.Sin(k), out Vector3 p, out Vector3 dd);
            HorrorFx.Decal(HorrorFx.Drag(), p.WithY(GroundAt(p) + 0.015f), Vector3.up, dd, new Vector2(0.9f, 2.4f), Props(), 0.95f);
        }
        heartbeat = HorrorAudio.Play2D(HorrorAudio.Heartbeat(), 0.5f, true);
        yield return Turn(LookBackEye, LookBackEye + youDir * 5f, LookBackEye - youDir * 8f + Vector3.down * 1.2f, 0.6f);
        yield return Shot(3.2f, null, null, () => (LookBackEye, youPos - youDir * 10f),
            () => Say("", "<i>Đường phía sau trống trơn. Chỉ có một vệt máu kéo lê thật dài... dẫn tới tận bánh xe của bạn.</i>", Color.white));

        // ===== 4. Về cửa hàng =====
        CheckoutCounter counter = CheckoutCounter.All.Count > 0 ? CheckoutCounter.All[0] : FindAnyObjectByType<CheckoutCounter>();
        // Một cửa thật, gần quầy thu ngân nhất (trước: trung bình 2 cửa = cột tường giữa hai cửa)
        Vector3 door = StoreDoor(out Vector3 outward, out Bounds store, counter != null ? counter.transform.position : (Vector3?)null);
        Vector3 cpos = counter != null ? (counter.cashierPoint != null ? counter.cashierPoint.position : counter.transform.position) : door - outward * 6f;
        Vector3 toFront = counter != null && counter.customerPoint != null ? counter.customerPoint.position - cpos : outward;
        toFront.y = 0f; toFront = toFront.sqrMagnitude > 0.01f ? toFront.normalized : outward;
        float floorY = GroundAtStore(cpos.WithY(store.min.y + 0.05f));
        cpos.y = floorY; door.y = floorY;
        Vector3 Eye(Vector3 v) => v.WithY(floorY + 1.62f);
        HideStorePeople(store);
        GrabStoreLights();
        DimStoreLights(0.08f, 0f);
        Mover dbl = MakeDouble(cpos, -toFront);                  // quay lưng ra phía quầy khách
        Vector3 outside = door + outward * 4.6f + Vector3.up * 1.65f;
        var lamp = new GameObject("CounterLight").AddComponent<Light>();
        lamp.transform.SetParent(Props(), false);
        lamp.type = LightType.Point; lamp.range = 3.5f; lamp.intensity = 2.4f; lamp.color = new Color(1f, 0.5f, 0.42f);
        lamp.transform.position = cpos + toFront * 0.6f + Vector3.up * 0.9f;
        // Hai vệt tay máu kéo dài trên kính (không dùng chữ viết - bỏ câu xưng "mày")
        Vector3 glassSide = Vector3.Cross(Vector3.up, outward);
        HorrorFx.Decal(HorrorFx.Drag(), door + Vector3.up * 1.35f + outward * 0.08f + glassSide * 0.22f, outward, Vector3.up, new Vector2(0.32f, 1.1f), Props(), 0.95f);
        HorrorFx.Decal(HorrorFx.Drag(), door + Vector3.up * 1.25f + outward * 0.081f - glassSide * 0.2f, outward, Vector3.up, new Vector2(0.3f, 0.95f), Props(), 0.9f);
        HorrorFx.Decal(HorrorFx.Hand(), door + Vector3.up * 1.85f + outward * 0.082f + glassSide * 0.22f, outward, Vector3.up, new Vector2(0.2f, 0.24f), Props());
        yield return Cut("Mười lăm phút sau...", "Bạn vòng xe về cửa hàng. Giờ này cửa hàng lẽ ra vẫn đang mở, đông khách...");
        vYou = vYouWant = 0f;                                    // xe "đứng nguyên" ngoài đường giao hàng
        yield return Shot(3.6f, null, null, () => (outside + outward * 1.5f + Vector3.up * 0.3f, door + Vector3.up * 1.6f),
            () => Say("", "<i>...nhưng bên trong tối om. Trên cửa kính, hai vệt tay máu kéo dài từ trên xuống, như có ai bị lôi đi.</i>", Color.white));
        cam.fieldOfView = 34f;
        yield return Shot(3.2f, null, null, () => (outside, Eye(cpos) + Vector3.down * 0.2f),
            () => Say("", "<i>Chỉ có ngọn đèn quầy thu ngân là sáng. Có người đang đứng đó, quay lưng ra... mặc đúng chiếc áo của bạn.</i>", Color.white));
        // ===== 5. Đầu vặn ngược ra nhìn =====
        Contort ct = dbl != null ? dbl.t.GetComponent<Contort>() : null;
        if (ct != null) { ct.speed = 120f; ct.twistTarget = 180f; ct.tiltTarget = 15f; ct.jitter = 0.4f; }
        yield return Shot(3.4f, null, null, () => (outside, Eye(cpos)),
            () => Say("", "<i>Cái đầu từ từ... vặn ngược ra sau, kêu răng rắc. Khuôn mặt tái xám, bê bết máu - là khuôn mặt của bạn.</i>", Color.white));
        cam.fieldOfView = 52f;
        // ===== 6. Nó đi ra cửa, áp mặt lên kính =====
        if (ct != null) { ct.speed = 400f; ct.twistTarget = 0f; ct.tiltTarget = 25f; ct.jitter = 0.8f; }
        Vector3 glassIn = door - outward * 0.45f;
        GhoulGo(dbl, glassIn, 0.9f, door + outward * 5f);
        yield return Shot(4.2f, null, null, () => (outside + outward * 0.6f, dbl != null ? dbl.t.position + Vector3.up * 1.4f : door),
            () => Say("", "<i>Nó chậm rãi bước ra phía cửa. Mỗi bước để lại một dấu chân máu.</i>", Color.white));
        if (dbl != null) { dbl.t.position = glassIn.WithY(floorY); dbl.arrived = true; dbl.face = door + outward * 5f; }
        HorrorAudio.Play2D(HorrorAudio.Bang(), 0.9f);
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        HorrorFx.Decal(HorrorFx.Hand(), door + Vector3.up * 1.45f + outward * 0.07f + side * 0.28f, outward, Vector3.up, new Vector2(0.22f, 0.26f), Props());
        HorrorFx.Decal(HorrorFx.Hand(), door + Vector3.up * 1.45f + outward * 0.07f - side * 0.28f, outward, Vector3.up, new Vector2(0.22f, 0.26f), Props());
        HorrorFx.Decal(HorrorFx.Splat(), door + Vector3.up * 1.6f + outward * 0.075f, outward, Vector3.up, new Vector2(0.4f, 0.5f), Props(), 0.8f);
        cam.fieldOfView = 40f;
        shake = 0.4f;
        yield return Shot(3f, null, null, () => (door + outward * 1.6f + Vector3.up * 1.6f, door + Vector3.up * 1.55f),
            () => Say("", "<i>RẦM. Nó áp mặt và hai bàn tay máu lên kính, nhìn thẳng vào bạn. Môi nó mấp máy: \"vào... đi...\"</i>", Color.white));
        cam.fieldOfView = 52f;

        int pick = -1;
        yield return Choose(6f, new[] { "Mở cửa vào xem", "Gọi điện về cửa hàng", "Quay xe bỏ chạy" },
            () => (outside + outward * 0.8f, door + Vector3.up * 1.5f), i => pick = i);

        if (pick == 0)
        {
            if (dbl != null) dbl.t.gameObject.SetActive(false);
            Vector3 inside = Eye(door - outward * 1.4f);
            Vector3 eye = Eye(cpos + toFront * 1.7f);
            AudioManager.Play("door_slide", 0.5f);
            yield return Shot(3f, null, null, () => (inside, Eye(cpos) + Vector3.down * 0.6f),
                () => Say("", "<i>Bạn bước vào. Nó đã biến mất. Dấu chân máu dẫn ngược vào phía sau quầy...</i>", Color.white));
            // Đèn chớp: mỗi lần sáng lên, "bạn" lại đứng gần hơn - từ cuối cửa hàng lao dần tới
            Vector3 deep = -outward;
            float[] steps = { 9f, 6.5f, 4.2f, 2.4f };
            Say("", "<i>Đèn bỗng chập chờn...</i>", Color.white);
            for (int k = 0; k < steps.Length; k++)
            {
                DimStoreLights(0.01f, 0f);
                if (dbl != null) dbl.t.gameObject.SetActive(false);
                float tk = Time.unscaledTime, darkFor = Random.Range(0.35f, 0.65f);
                while (Time.unscaledTime - tk < darkFor) { Drive(); cam.transform.SetPositionAndRotation(inside, Quaternion.LookRotation(deep)); yield return null; }
                if (dbl != null)
                {
                    dbl.t.gameObject.SetActive(true);
                    Vector3 at = (inside + deep * steps[k]).WithY(floorY);
                    dbl.t.SetPositionAndRotation(at, Quaternion.LookRotation(-deep));
                    dbl.arrived = true; dbl.face = inside; GhoulIdle(dbl);
                    if (ct != null) { ct.speed = 6000f; ct.twistTarget = 0f; ct.tiltTarget = Random.Range(-60f, 60f); ct.neckStretchTarget = 1f + k * 0.15f; ct.jitter = 1.3f; }
                    HorrorFx.Decal(HorrorFx.Splat(), at + Vector3.up * 0.012f, Vector3.up, deep, new Vector2(0.6f, 0.6f), Props(), 0.9f);
                }
                DimStoreLights(0.6f, 0f);
                HorrorAudio.Play2D(HorrorAudio.Crack(), 0.7f);
                HorrorAudio.Play2D(HorrorAudio.Static(), 0.2f);
                tk = Time.unscaledTime;
                while (Time.unscaledTime - tk < Random.Range(0.25f, 0.4f)) { Drive(); cam.transform.SetPositionAndRotation(inside + Random.insideUnitSphere * 0.006f * k, Quaternion.LookRotation(deep)); yield return null; }
            }
            DimStoreLights(0.08f, 0f);
            if (dbl != null) dbl.t.gameObject.SetActive(false);
            PhoneNote("Đơn #0000 - đã tới nơi", "Người nhận đang ở <b>ngay sau lưng bạn</b>.");
            yield return Shot(3f, null, null, () => (eye, Eye(cpos) + Vector3.down * 0.5f),
                () => Say("", "<i>Trên quầy, một chiếc điện thoại sáng màn hình. Người nhận: chính là BẠN.</i>", Color.white));
            ClosePhone();
            HorrorAudio.Play2D(HorrorAudio.Breath(), 1f);
            Vector3 behind = eye + toFront * 0.85f;
            if (dbl != null)
            {
                dbl.t.gameObject.SetActive(true);
                dbl.t.SetPositionAndRotation(behind.WithY(floorY), Quaternion.LookRotation(-toFront));
                if (ct != null) { ct.twistTarget = 0f; ct.tiltTarget = -55f; ct.neckStretchTarget = 1.7f; ct.jitter = 1.2f; }
            }
            yield return Shot(1.4f, null, null, () => (eye, Eye(cpos)), () => Say("", "<i>Sau lưng... có tiếng thở. Đúng nhịp thở của bạn.</i>", Color.white));
            yield return Turn(eye, Eye(cpos), Eye(behind) + Vector3.up * 0.05f, 0.3f);
            HorrorAudio.Play2D(HorrorAudio.Scream(), 1f);
            Scare(); BloodOnScreen();
            yield return Shot(0.6f, null, null, () => (eye, Eye(behind)), () => { });
        }
        else if (pick == 1)
        {
            AudioSource ring = HorrorAudio.Play3D(HorrorAudio.PhoneBuzz(), Eye(cpos), 1f, true, 3f, 40f);
            yield return Shot(2.8f, null, null, () => (outside, Eye(cpos)),
                () => Say("", "<i>Bạn bấm gọi số cửa hàng. Trong kia, điện thoại ở quầy đổ chuông... Nó quay đầu lại, nhìn chiếc điện thoại.</i>", Color.white));
            if (ring != null) Destroy(ring.gameObject);
            if (dbl != null) { dbl.t.position = cpos.WithY(floorY); dbl.t.rotation = Quaternion.LookRotation(toFront); dbl.arrived = true; dbl.face = door; if (dbl.cp != null) { dbl.cp.Play(ClipPlayer.Has("Texting") ? "Texting" : "Walking", true, 0.2f, 0.4f); ClipPlayer.AttachPhone(dbl.anim); } }
            cam.fieldOfView = 34f;
            yield return Shot(3f, null, null, () => (outside, Eye(cpos)),
                () => Say("Giọng của bạn", "Alô? Ai đấy? Tôi đang ở cửa hàng đây mà.", DoubleCol));
            if (ct != null) { ct.twistTarget = 180f; ct.speed = 900f; ct.jitter = 1f; }
            yield return Shot(2.6f, null, null, () => (outside, Eye(cpos)),
                () => Say("Giọng của bạn", "...Thế ai đang đứng ngoài cửa kia?", DoubleCol));
            cam.fieldOfView = 52f;
            if (dbl != null) { GhoulGo(dbl, glassIn, 5f, door + outward * 5f); if (ct != null) { ct.twistTarget = 0f; ct.neckStretchTarget = 1.6f; } }
            yield return Shot(0.7f, null, null, () => (door + outward * 1.2f + Vector3.up * 1.6f, door + Vector3.up * 1.5f), () => { });
            HorrorAudio.Play2D(HorrorAudio.Bang(), 1f);
            HorrorAudio.Play2D(HorrorAudio.Scream(), 1f);
            Scare(); BloodOnScreen();
            yield return Shot(0.6f, null, null, () => (door + outward * 0.9f + Vector3.up * 1.6f, door + Vector3.up * 1.55f), () => { });
        }
        else
        {
            // Bỏ chạy: nó đuổi theo, đầu vặn ngược, nhanh hơn cả xe
            RestoreStoreLights(); ShowStorePeople();
            yield return Cut("", pick < 0 ? "Bạn đứng chết lặng một lúc lâu, rồi quay xe chạy thục mạng." : "Bạn quay xe, chạy thục mạng ra khỏi con phố.");
            stopAtS = float.NaN;
            vYou = vYouWant = 7f;
            rail.Sample(sYou - 22f, 0f, out Vector3 back, out _);
            if (dbl != null)
            {
                dbl.t.gameObject.SetActive(true);
                dbl.t.SetPositionAndRotation(back.WithY(GroundAt(back)), Quaternion.LookRotation(youDir));
                if (ct != null) { ct.twistTarget = 180f; ct.speed = 2000f; ct.tiltTarget = 20f; ct.jitter = 1.1f; ct.armStretchTarget = 1.3f; }
            }
            float t0 = Time.unscaledTime;
            Say("", "<i>Bạn liếc gương chiếu hậu... nó đang CHẠY theo, đầu vặn ngược, nhanh hơn cả xe.</i>", Color.white);
            while (Time.unscaledTime - t0 < 3.6f)
            {
                Drive();
                float u = (Time.unscaledTime - t0) / 3.6f;
                rail.Sample(sYou - Mathf.Lerp(22f, 1.2f, u), rideIsBike ? -0.9f : -(carHalfW + 0.6f) * doorSide * -1f, out Vector3 p, out Vector3 dd);
                if (dbl != null)
                {
                    dbl.t.SetPositionAndRotation(p.WithY(GroundAt(p)), Quaternion.LookRotation(dd));
                    if (dbl.cp != null) dbl.cp.Play("Running", true, 0.1f, 1.5f);
                }
                Vector3 cp0 = LookBackEye + youDir * 0.15f;
                cam.transform.SetPositionAndRotation(cp0, Quaternion.LookRotation(dbl != null ? dbl.t.position + Vector3.up * 1.4f - cp0 : -youDir));
                yield return null;
            }
            // Nó bắt kịp, ĐẬP vào cửa kính ngay cạnh bạn - dấu tay máu in lên kính, mặt áp sát
            vYouWant = 0f;
            Vector3 winOut = rideIsBike ? Right(youDir) * -1f : DoorOut;
            Vector3 winAt = rideIsBike ? RiderHead + winOut * 0.5f : DriverHead + DoorOut * (carHalfW - Mathf.Abs(seatLocal.x) + 0.03f);
            if (dbl != null)
            {
                dbl.t.SetPositionAndRotation((winAt + winOut * 0.45f).WithY(GroundAt(winAt)), Quaternion.LookRotation(-winOut));
                if (dbl.cp != null) dbl.cp.Play(ClipPlayer.Has("Angry_Gesture") ? "Angry_Gesture" : "Yelling", true, 0.1f, 1.8f);
                if (ct != null) { ct.twistTarget = 0f; ct.tiltTarget = 25f; ct.jitter = 1.5f; }
            }
            var glassHold = new GameObject("WindowBlood").transform;
            glassHold.SetParent(car != null ? car.transform : Props(), true);
            for (int k = 0; k < 5; k++)
            {
                HorrorAudio.Play2D(HorrorAudio.Bang(), 0.7f + k * 0.06f);
                Vector3 up = Vector3.up + Vector3.Cross(Vector3.up, winOut) * Random.Range(-0.4f, 0.4f);
                HorrorFx.Decal(HorrorFx.Hand(), winAt + youDir * Random.Range(-0.35f, 0.35f) + Vector3.up * Random.Range(-0.2f, 0.15f) + winOut * (0.002f * k), winOut, up, new Vector2(0.2f, 0.24f), glassHold);
                if (k % 2 == 0) HorrorFx.Decal(HorrorFx.Splat(), winAt + youDir * Random.Range(-0.3f, 0.3f) + Vector3.down * 0.15f, winOut, Vector3.up, new Vector2(0.22f, 0.4f), glassHold, 0.9f);
                shake = 0.35f;
                float tk = Time.unscaledTime;
                Vector3 e0 = LookBackEye;
                while (Time.unscaledTime - tk < 0.32f) { Drive(); cam.transform.SetPositionAndRotation(e0 + Random.insideUnitSphere * 0.01f, Quaternion.LookRotation(winAt + Vector3.up * 0.05f - e0)); yield return null; }
            }
            rideBlood.Add(glassHold.gameObject);                             // dấu tay trên kính còn tới sáng
            Scare(); BloodOnScreen();
            HorrorAudio.Play2D(HorrorAudio.Scream(), 1f);
            Vector3 sideAt = dbl != null ? dbl.t.position + Vector3.up * 1.5f : RiderHead - youDir;
            yield return Turn(LookBackEye, LookBackEye + youDir * 5f, sideAt, 0.2f);
            yield return Shot(0.6f, null, null, () => (LookBackEye, sideAt), () => { });
        }

        // Giật mình tỉnh lại giữa đường
        if (heartbeat != null) Destroy(heartbeat.gameObject);
        heartbeat = null;
        yield return BlackHold(1.8f, "", "");
        ClearGhouls();
        ClearProps();
        RestoreStoreLights(); ShowStorePeople();
        vYou = vYouWant = 0f;
        yield return FadeIn(0.8f);
        yield return Shot(3.2f, null, null, () => (youPos + youDir * 3.2f + Right(youDir) * 0.9f + Vector3.up * 1.6f, RiderHead),
            () => Say("", "<i>Bạn giật mình. Xe vẫn đứng nguyên giữa đường giao hàng, máy vẫn nổ. Chưa hề đi đâu cả.</i>", Color.white));
        LeaveHandprints();
        yield return Shot(2.8f, null, null,
            () => (youPos + DoorOut * 2.2f + youDir * 0.5f + Vector3.up * 1.3f, youPos + Vector3.up * (rideIsBike ? 0.9f : 1.1f)),
            () => Say("", "<i>...Nhưng trên xe có những dấu bàn tay máu. Cỡ bàn tay y hệt tay bạn.</i>", Color.white));
        PhoneNote("Đơn #0000 đã giao thành công", $"Người nhận đã ký: <b>chữ ký giống hệt chữ ký của bạn</b>.\nGiao lúc 22:47 - {tomorrow}.");
        yield return Shot(3.2f, null, null, Phone,
            () => Say("Bạn", "Mình... mình phải về nhà. Ngay bây giờ.", YouSpeak));
        ClosePhone();
        if (screenBlood != null) Destroy(screenBlood.gameObject);
        screenBlood = null;
        TeardownRide();
        if (snapRt != null) { snapRt.Release(); Destroy(snapRt); snapRt = null; }
        StoreProgress.AddXP(5);
        ProgressHUD.Toast("<color=#BFD7FF>Tay bạn vẫn còn run... +5 XP (gan dạ).</color>");
        horrorNews = $"<b>BẢN TIN SÁNG</b> Camera an ninh của siêu thị VietUrban ghi được hình chủ cửa hàng đứng ở quầy lúc 22:47 tối qua - đúng lúc người đó đang đi giao hàng ở {lastStreet.Replace("gần ", "")}.";
    }

    private static float GroundAtStore(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore)) return h.point.y;
        return p.y;
    }

    /// <summary>Chụp một tấm ảnh chiếc xe người chơi từ ngay phía sau (để gửi kèm tin nhắn).</summary>
    private Texture SnapBehind()
    {
        var go = new GameObject("SnapCam");
        var c = go.AddComponent<Camera>();
        Camera like = cam != null ? cam : Camera.main;
        if (like != null) c.CopyFrom(like);
        c.fieldOfView = 55f;
        Vector3 p = youPos - youDir * 4.5f + Vector3.up * 1.5f + Right(youDir) * 0.3f;
        go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(youPos + Vector3.up * 0.8f - p));
        var rt = new RenderTexture(480, 270, 16);
        c.targetTexture = rt;
        c.Render();
        c.targetTexture = null;
        Destroy(go);
        if (snapRt != null) { snapRt.Release(); Destroy(snapRt); }
        snapRt = rt;
        return rt;
    }

    private RenderTexture snapRt;
}
