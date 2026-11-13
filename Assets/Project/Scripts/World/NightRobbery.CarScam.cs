using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sự kiện "va chạm giả để ăn vạ" khi lái Ô TÔ đi giao hàng (dùng chung hệ thống đoạn phim với vụ cướp xe máy).
///  - Kích hoạt: đang lái ô tô chở hàng, còn 40-150 m tới điểm giao; mỗi đơn xét 1 lần, 50%, tối đa 1 lần / ngày; ngày hay đêm đều được.
///    Phím thử (Editor / Development): Shift + F4 khi đang lái ô tô và đã lấy gói hàng.
///  - Một xe máy hai người vượt lên, tạt đầu, phanh gấp rồi TỰ ĐỔ ngay trước mũi xe. "Nạn nhân" ngồi ôm chân giữa đường,
///    đồng bọn ra đập cửa kính đòi đền tiền, thêm hai "người nhà" xúm lại.
///  - Lựa chọn (8 s, vẫn hình chậm): đưa tiền cho xong / khoá cửa gọi 113 / mở camera hành trình đối chất / lùi xe vòng qua bỏ đi.
///    Gọi 113 -> chúng đập kính dữ hơn -> lựa chọn 2: ngồi yên chờ công an / hạ kính nói chuyện.
///    Camera hành trình -> 75% "nạn nhân" đứng bật dậy, cả nhóm bỏ chạy; 25% chúng định giật camera -> lựa chọn 2: lái đi ngay / gọi 113.
///  - Kết cục: bị chặt tiền, bị giật điện thoại / ví, bóc mẽ được (thưởng), công an tới bắt (thưởng), hoặc bỏ đi nhưng bị quay clip
///    "tài xế tông người bỏ chạy" (một đánh giá 1 sao). Sáng hôm sau có bản tin.
/// </summary>
public partial class NightRobbery
{
    private enum ScamEnd { None, Paid, Snatched, Exposed, Arrested, Viral, Escaped }

    [Range(0f, 1f)] public float scamChance = 0.5f;
    private int lastScamDay = -1;
    private ScamEnd lastScam = ScamEnd.None;
    private bool carMode;

    // Diễn viên đứng dưới đường (tĩnh, tạo tư thế sẵn)
    private readonly List<GameObject> scamPeople = new List<GameObject>();
    private GameObject scamRider, scamPass, victimDown, victimUp, accomplice;

    private static readonly Color ScamCol = new Color(1f, 0.75f, 0.35f);

    /// <summary>Ô tô người chơi đang lái (không phải xe máy).</summary>
    private static PlayerCarController DrivingCar()
    {
        foreach (PlayerCarController c in FindObjectsByType<PlayerCarController>())
            if (c != null && c.isDriving && c.GetComponentInChildren<MotorcycleInteraction>(true) == null
                && c.GetComponentInParent<MotorcycleInteraction>() == null) return c;
        return null;
    }

    /// <summary>Gọi từ Update khi người chơi không đi xe máy: xét điều kiện rồi chiếu vụ ăn vạ.</summary>
    private bool TryCarScam(Vector3 dest, int serial, bool force)
    {
        PlayerCarController c = DrivingCar();
        if (c == null) return false;
        if (!force)
        {
            if (serial == decidedSerial) return true;
            Vector3 d = dest - c.transform.position; d.y = 0f;
            if (d.magnitude > 150f || d.magnitude < 40f) return true;
            decidedSerial = serial;
            int day = GameManager.Instance != null ? GameManager.Instance.currentDay : 0;
            if (day == lastScamDay || Random.value > scamChance) return true;
            lastScamDay = day;
        }
        if (!Busy()) StartCoroutine(RunScam(c));
        return true;
    }

    private void ScamNews()
    {
        string where = $"{lastWhen} {lastStreet}";
        switch (lastScam)
        {
            case ScamEnd.Exposed:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> {where}, camera hành trình của một tài xế giao hàng đã vạch mặt nhóm \"ăn vạ\" - clip được chia sẻ khắp mạng.");
                break;
            case ScamEnd.Arrested:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> {where}, công an tạm giữ nhóm 4 người dàn cảnh va chạm giao thông để cưỡng đoạt tiền.");
                break;
            case ScamEnd.Paid:
            case ScamEnd.Snatched:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> {where}, lại thêm một tài xế bị nhóm \"ăn vạ\" dàn cảnh lấy tiền. Công an khuyên lắp camera hành trình và gọi 113.");
                break;
            case ScamEnd.Viral:
                ProgressHUD.Toast($"<b>BẢN TIN SÁNG</b> Clip \"tài xế tông người rồi bỏ chạy\" {lastStreet} lan truyền trên mạng. Cửa hàng bị đánh giá 1 sao.");
                break;
        }
        lastScam = ScamEnd.None;
    }

    // ================= Đoạn phim =================
    private IEnumerator RunScam(PlayerCarController drive)
    {
        VietDistrict city = FindAnyObjectByType<VietDistrict>();
        if (city == null || city.peopleModels == null || city.peopleModels.Length < 2) yield break;
        car = drive;
        Transform ct = car.transform;
        Vector3 heading = ct.forward; heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        rail = BuildRail(city, ct.position, heading.normalized, out lane);
        if (rail == null) yield break;
        Playing = true;
        carMode = true;
        ReadContext();
        lastWhen = When();
        lastStreet = DeliveryManager.TargetName(out string place) ? "gần " + place : "trong thành phố";

        Rigidbody rb = car.GetComponent<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
        car.enabled = false;
        yOffset = Mathf.Clamp(ct.position.y - Ground(ct.position, ct), 0f, 1.5f);
        sYou = rail.Project(ct.position);
        vYou = vYouWant = 9f;
        youLat = youLatWant = 0f;
        passPose.Clear(); youPose.Clear();
        robDown = false; robRoll = robRollWant = robSpin = robSpinWant = 0f; shake = flash = youLean = 0f; robRail = null; cityRef = city;
        standIn = null;

        // Xe máy "ăn vạ" xuất phát phía sau bên trái
        bike = SpawnScamBike(city);
        robRel = robRelWant = -18f; robLat = robLatWant = -2.2f; robFleeing = false; slow = 1f;
        Drive();
        BeginFilm();
        ScamEnd end = ScamEnd.None;

        MeasureCar();
        if (carMeasured)
        {
            // bánh xe chạm đúng mặt đường: đáy khung xe đặt lên mặt đường thật (raycast), không theo độ cao lúc xe đang nhún
            rail.Sample(sYou, youLat, out Vector3 railP, out _);
            yOffset = Mathf.Clamp(Ground(railP, car.transform) - railP.y - carBottom, -0.5f, 1.5f);
        }
        SpawnCarDriver(city);   // nhân vật người chơi ngồi ghế lái (NightRobbery.CarScamDriver.cs)
        StartCoroutine(PrepPolice());   // chuẩn bị CSGT + còi từ trước (khỏi khựng khi xe CSGT tới)
        // Máy quay đặt NGOÀI thân xe: trên nắp capo nhìn ra trước (kiểu camera hành trình)
        (Vector3, Vector3) Cabin(float lookAhead) => (youPos + youDir * (carFront * 0.75f) + Vector3.up * (carTop + 0.2f),
                                                       youPos + youDir * (carFront + lookAhead) + Vector3.up * 0.7f);
        // Sau vai tên đồng bọn, nhìn vào cửa kính lái
        (Vector3, Vector3) WindowCam() => (winPos + DoorOut * 1.0f - youDir * 1.0f + Vector3.up * 1.75f,
                                           driverRoot != null ? DriverHead : youPos + DoorOut * (carHalfW * 0.6f) + Vector3.up * (carTop * 0.75f));
        // Cạnh hông xe nhìn sang tên đồng bọn
        (Vector3, Vector3) AccompliceCam() => (youPos + DoorOut * (carHalfW + 0.4f) + youDir * (carFront * 0.8f) + Vector3.up * 1.35f,
                                               winPos + Vector3.up * 1.55f);

        // ===== HỒI 1: tạt đầu, phanh gấp, ô tô húc vào (góc thứ ba) =====
        // Máy quay bám sau xe, hơi cao (góc thứ ba)
        (Vector3, Vector3) Chase() => (youPos - youDir * 7.5f + Right(youDir) * 1.6f + Vector3.up * 3f, youPos + youDir * 6f + Vector3.up * 0.6f);
        yield return Shot(3f, Title(), Subtitle(),
            () => (youPos + youDir * 9f + Right(youDir) * 2.2f + Vector3.up * 1.6f, youPos + Vector3.up * 0.9f),
            () => Say("Bạn", Dark ? "Đường đêm thoáng thật, chắc kịp giờ giao." : "Đường thoáng, chắc kịp giờ giao.", YouCol));
        robRelWant = carFront + 5f; robLatWant = -1.8f; robFleeing = true;
        AudioManager.Play("horn_1", 0.5f);
        yield return Shot(2.4f, null, null, Chase,
            () => Say("", "<i>Một chiếc xe máy chở hai người rú ga vượt lên bên trái...</i>", Color.white));
        robLatWant = 0f; robFleeing = false;
        yield return Shot(1.6f, null, null, Chase,
            () => Say("", "<i>...tạt thẳng vào đầu xe bạn rồi phanh gấp!</i>", Color.white));
        // Phanh không kịp: đuôi xe máy lùi dần về sát cản trước rồi bị húc (hình chậm, góc thấp bên hông)
        float contact = carFront + 0.75f;                 // đuôi xe máy chạm cản trước
        robRelWant = contact; robFleeing = true; vYouWant = 0f;
        slow = 0.35f; Time.timeScale = 0.35f;
        Vector3 Hit() => youPos + youDir * (carFront + 0.3f);
        yield return Shot(1.6f, null, null,
            () => (Hit() - Right(youDir) * 5.5f + youDir * 1.5f + Vector3.up * 1.1f, Hit() + Vector3.up * 0.5f),
            () => Say("", "<i>Bạn đạp phanh hết cỡ - không kịp...</i>", Color.white),
            () => robRel <= contact + 0.05f);
        // Va chạm
        robRail = rail; sRob = sYou + robRel; vRob = vYou + 1.5f; vRobWant = 0f;  // xe máy bị đẩy trượt về trước rồi nằm lại
        robDown = true; robRoll = robLean; robRollWant = 84f; robSpinWant = 28f; shake = 0.5f; flash = 0.25f;
        AudioManager.Play("horn_2", 0.6f); AudioManager.Play("box_drop", 1f);
        yield return Shot(1.8f, null, null,
            () => (Hit() - Right(youDir) * 5.5f + youDir * 1.5f + Vector3.up * 1.1f, Hit() + youDir * 0.8f + Vector3.up * 0.4f),
            () => Say("", "<i>RẦM! Đầu xe bạn húc vào đuôi xe máy - chiếc xe đổ nghiêng, trượt đi một đoạn.</i>", Color.white));
        slow = 1f; Time.timeScale = 1f;
        SpawnDebris();
        yield return Shot(2.4f, null, null,
            () => (youPos - youDir * 6f - Right(youDir) * 2f + Vector3.up * 2.4f, robPos + Vector3.up * 0.4f),
            () => Say("Bạn", "Nó tự phanh gấp trước mũi xe mình mà! Mình chỉ chạm nhẹ thôi...", YouCol));

        // ===== HỒI 2: dàn cảnh =====
        if (scamRider != null) scamRider.SetActive(false);
        if (scamPass != null) scamPass.SetActive(false);
        SpawnScamCrowd(city);
        yield return Shot(2.6f, null, null,
            () => (youPos + youDir * 7f + Right(youDir) * 1.5f + Vector3.up * 1.3f, robPos + Vector3.up * 0.4f),
            () => Say("Nạn nhân", "Á á á! Cái chân tôi! Gãy chân rồi!", ScamCol));
        shake = 0.3f;
        yield return Shot(2.6f, null, null,
            () => AccompliceCam(),
            () => Say("Đồng bọn", "Đâm người ta rồi định chạy à?! Xuống xe! Đền 5 triệu ngay!", ScamCol));
        // Cận mặt tên đồng bọn (ống kính dài, đẩy máy)
        cam.fieldOfView = 34f;
        yield return Shot(2f, null, null,
            () => (winPos + youDir * 1.7f + Right(youDir) * 0.15f + Vector3.up * 1.62f, winPos + Vector3.up * 1.58f),
            () => Say("Đồng bọn", "Nhìn cái gì? Xuống đây nói chuyện!", ScamCol));
        cam.fieldOfView = 52f;
        yield return Shot(2.6f, null, null,
            () => (youPos + youDir * (carFront + 9f) + Vector3.up * 2.2f, youPos + youDir * (carFront + 2f) + Vector3.up * 0.9f),
            () => Say("", "<i>Từ đâu, hai \"người nhà\" chạy ào tới, vây kín đầu xe.</i>", Color.white));
        // Cận "nạn nhân"
        cam.fieldOfView = 42f;   // toàn thân, đủ xa để không cắt tay chân
        yield return Shot(1.8f, null, null,
            () => VictimCam(2.8f),
            () => Say("Nạn nhân", "Ối giời ơi... gãy thật rồi...", ScamCol));
        cam.fieldOfView = 52f;
        yield return Shot(2.6f, null, null,
            () => (youPos - youDir * 4f + Right(youDir) * 7f + Vector3.up * 5f, youPos + youDir * (carFront + 1f)),
            () => Say("", "<i>Người qua đường bắt đầu xúm lại. Phía sau, xe cộ ùn lại, còi inh ỏi.</i>", Color.white));

        int pick = -1;
        yield return Choose(8f, new[] { "Đưa tiền cho xong chuyện", "Khoá cửa, gọi 113", "Mở camera hành trình ra đối chất", "Lùi xe, vòng qua bỏ đi" },
            () => Cabin(5f), i => pick = i);

        switch (pick)
        {
            case 0:
                end = ScamEnd.Paid;
                yield return PayScene(false);   // hạ kính, nói qua lại, đưa tiền
                yield return ScamLeave("<i>Hắn đếm tiền, nhếch mép. \"Nạn nhân\" bỗng đi lại bình thường, dựng xe phóng mất.</i>");
                break;
            case 1:
            {
                shake = 0.5f; flash = 0f; banging = true;
                Show(youPose, "phone");
                yield return Shot(2.6f, null, null,
                    () => WindowCam(),
                    () => Say("", "<i>Cạch - bạn khoá cửa, bấm 113. Bên ngoài, chúng đập rầm rầm vào cửa kính.</i>", Color.white));
                yield return Say2("Đồng bọn", "Mở cửa ra! Có mở không thì bảo!", ScamCol);
                Show(youPose, "shock");
                int k = -1;
                yield return Choose(5f, new[] { "Ngồi yên chờ công an", "Hạ kính nói chuyện cho êm" },
                    () => WindowCam(), i => k = i);
                if (k == 1)
                {
                    end = ScamEnd.Snatched;
                    yield return SnatchScene();   // hạ kính nói chuyện -> bị thò tay giật đồ
                    yield return ScamLeave("<i>Cả nhóm dựng xe, rồ ga bỏ chạy.</i>");
                }
                else
                {
                    end = ScamEnd.Arrested;
                    Show(youPose, "phone");
                    yield return Shot(2.4f, null, null,
                        () => Cabin(5f),
                        () => Say("Bạn", k < 0 ? "<i>Tay bạn run, nhưng vẫn ngồi im, giữ chặt khoá cửa.</i>" : "Ngồi im... công an sắp tới rồi.", YouCol));
                    yield return PoliceArrive();
                }
                break;
            }
            case 2:
            {
                yield return Shot(2.6f, null, null,
                    () => WindowCam(),
                    () => { ShowRec(true); Say("Bạn", "Camera hành trình quay hết rồi đấy. Có muốn xem lại không?", YouCol); });
                if (Random.value < 0.75f)
                {
                    end = ScamEnd.Exposed;
                    StandUpVictim();
                    foreach (ScamActor a in actors) if (a.frames.Contains(victimDown)) a.frames.Clear();
                    if (victimDown != null) victimDown.SetActive(false);
                    if (victimUp != null) victimUp.SetActive(true);
                    banging = false;
                    yield return Shot(2.6f, null, null,
                        () => (youPos + youDir * 7f + Right(youDir) * 1.5f + Vector3.up * 1.3f, robPos + Vector3.up * 1f),
                        () => Say("", "<i>\"Nạn nhân\" bỗng đứng bật dậy, chân tay lành lặn như chưa có gì.</i>", Color.white));
                    yield return Say2("Đồng bọn", "Thôi... nhầm, nhầm! Đi thôi!", ScamCol);
                    yield return ScamLeave("<i>Cả nhóm cuống cuồng dựng xe, chuồn thẳng.</i>");
                }
                else
                {
                    shake = 0.4f;
                    yield return Shot(2.4f, null, null,
                        () => WindowCam(),
                        () => Say("Đồng bọn", "Camera à? Đưa đây! Đập nát nó luôn!", ScamCol));
                    int k = -1;
                    yield return Choose(4.5f, new[] { "Lái xe đi ngay", "Khoá cửa, gọi 113" },
                        () => Cabin(5f), i => k = i);
                    if (k == 1) { end = ScamEnd.Arrested; yield return PoliceArrive(); }
                    else { end = k == 0 ? ScamEnd.Escaped : ScamEnd.Snatched; yield return k == 0 ? DriveAround(false) : ScamLeave("<i>Bạn chần chừ - chúng giật được camera và ví rồi bỏ chạy.</i>"); }
                }
                break;
            }
            case 3:
                end = ScamEnd.Viral;
                yield return DriveAround(true);
                break;
            default:
                end = ScamEnd.Paid;
                yield return Say2("Đồng bọn", "Im lặng là đồng ý nhé. 7 triệu, không mặc cả!", ScamCol);
                yield return PayScene(true);
                yield return ScamLeave("<i>Chúng nhét tiền vào túi rồi đi như chưa có chuyện gì.</i>");
                break;
        }

        // Lời kết
        vYouWant = end == ScamEnd.Paid || end == ScamEnd.Snatched || end == ScamEnd.Arrested ? 0f : 7f;
        yield return Shot(2.8f, null, null,
            () => (youPos - youDir * 7.5f + Right(youDir) * 1.6f + Vector3.up * 3f, youPos + youDir * 6f + Vector3.up * 0.6f),
            () => Say("Bạn",
                end == ScamEnd.Exposed ? "Đúng là may có cái camera... Đi giao hàng tiếp thôi."
                : end == ScamEnd.Arrested ? "Hú vía. Lần sau cứ khoá cửa gọi 113 là chuẩn."
                : end == ScamEnd.Viral ? "Thoát rồi... nhưng chắc chúng nó quay clip mình rồi."
                : end == ScamEnd.Escaped ? "Thoát rồi. Clip camera vẫn còn, lát nữa nộp cho công an."
                : end == ScamEnd.Snatched ? "Mất cả điện thoại lẫn ví... Đáng lẽ không nên hạ kính."
                : "Mất tiền oan... Phải lắp camera hành trình mới được.", YouCol));

        banging = false; ShowRec(false);
        EndFilm();
        ClearScamActors();
        ClearCarDriver();
        if (bike != null) Destroy(bike.gameObject);
        bike = null;
        car.enabled = true;
        if (rb != null) { rb.isKinematic = wasKinematic; rb.linearVelocity = Vector3.zero; }
        ApplyScam(end, pick < 0);
        lastScam = end;
        rail = null; robRail = null;
        carMode = false;
        Playing = false;
    }

    /// <summary>Cả nhóm rời đi: người đứng biến mất, hai tên lên lại xe máy, dựng xe rồi phóng đi.</summary>
    private IEnumerator ScamLeave(string line)
    {
        RemoveScammers();
        if (scamRider != null) scamRider.SetActive(true);
        if (scamPass != null) scamPass.SetActive(true);
        robDown = false; robSpinWant = 0f; robLean = 0f;
        vRobWant = 11f;
        yield return Shot(2.6f, null, null,
            () => (youPos + Vector3.up * 1.25f - Right(youDir) * 0.35f, robPos + Vector3.up * 0.9f),
            () => Say("", line, Color.white));
    }

    /// <summary>Chuyển cảnh tối màn hình (công an tới), dọn cả nhóm và chiếc xe máy.</summary>
    private IEnumerator ScamCut(string caption, string line)
    {
        ClearScamActors();
        yield return Cut(caption, line); // Cut tự xoá chiếc xe máy
    }

    /// <summary>Lùi xe, đánh lái vòng qua chiếc xe máy nằm giữa đường rồi tăng tốc bỏ đi; viral = có người quay clip.</summary>
    private IEnumerator DriveAround(bool viral)
    {
        foreach (ScamActor a in actors) if (a.frames.Contains(accomplice)) { foreach (GameObject g in a.frames) if (g != null) g.SetActive(false); a.frames.Clear(); } // hắn nhảy lùi tránh xe
        vYouWant = -1.6f;
        ScatterMovers();
        yield return Shot(1.8f, null, null,
            () => (youPos + youDir * 8f + Right(youDir) * 2f + Vector3.up * 1.5f, youPos + Vector3.up * 0.9f),
            () => Say("", "<i>Bạn gài số lùi, lùi gấp vài mét...</i>", Color.white));
        vYouWant = 10f; youLatWant = -2.8f;
        foreach (ScamActor a in actors) if (!a.frames.Contains(victimDown)) { foreach (GameObject g in a.frames) if (g != null) g.SetActive(false); a.frames.Clear(); }
        yield return Shot(2.8f, null, null,
            () => (youPos - youDir * 6f + Right(youDir) * 3f + Vector3.up * 2f, youPos + youDir * 4f + Vector3.up * 0.8f),
            () => Say("", viral ? "<i>...rồi đánh lái vòng qua chiếc xe máy, tăng tốc. Phía sau, ai đó giơ điện thoại lên quay.</i>"
                                : "<i>...rồi đánh lái vòng qua chiếc xe máy, tăng tốc bỏ lại cả nhóm.</i>", Color.white));
        yield return Say2("Đồng bọn", viral ? "Tông người rồi bỏ chạy kìa! Quay lại biển số nó!" : "Đứng lại! Mày nhớ mặt tao đấy!", ScamCol);
        youLatWant = 0f;
    }

    // ================= Diễn viên =================
    /// <summary>Xe máy hai người (người thường, không mặt nạ / vũ khí); người lái / người ngồi sau là object riêng để ẩn khi xe đổ.</summary>
    private Transform SpawnScamBike(VietDistrict city)
    {
        int v = Random.Range(0, BikeModels.FirstElectric);
        BikeModels.Info info = BikeModels.Get(v);
        GameObject go = RealModels.Spawn(Single(info.lod0, info.lod1), transform, Vector3.zero, Quaternion.identity, "ScamBike", true, 0.04f, 0.004f);
        GameObject[] people = city.peopleModels, mats = city.peopleMaterials;
        string kind = info.scooter ? "scooter" : "wave";
        int a = Random.Range(0, people.Length), b = (a + 1 + Random.Range(0, Mathf.Max(1, people.Length - 1))) % people.Length;
        scamA = a; scamB = b;
        var helmet = Single(BikeModels.Helmet(new Color(0.75f, 0.1f, 0.1f), false), BikeModels.Helmet(new Color(0.75f, 0.1f, 0.1f), true));
        RealModels.Model rider = RealModels.Person(people[a], mats != null && a < mats.Length ? mats[a] : null, RealModels.Pose.Ride, MotorbikeTraffic.RiderIK(info), kind);
        if (rider != null && rider.ok)
        {
            scamRider = PoseObj(rider, go.transform, info.seat, "ScamRider");
            RealModels.AddTo(scamRider, helmet, Matrix4x4.TRS(rider.head + new Vector3(0f, 0.07f, 0.01f), Quaternion.identity, Vector3.one), true);
        }
        RealModels.Model pass = RealModels.Person(people[b], mats != null && b < mats.Length ? mats[b] : null, RealModels.Pose.RideBack, MotorbikeTraffic.PassengerIK(info), "p" + kind);
        if (pass != null && pass.ok)
        {
            scamPass = PoseObj(pass, go.transform, info.rearSeat, "ScamPassenger");
            RealModels.AddTo(scamPass, helmet, Matrix4x4.TRS(pass.head + new Vector3(0f, 0.07f, 0.01f), Quaternion.identity, Vector3.one), true);
        }
        return go.transform;
    }

    private int scamA, scamB;

    /// <summary>Một diễn viên đứng: vài tư thế đổi luân phiên (chỉ tay / đập kính...) + lắc lư nhẹ cho đỡ đứng im như tượng.</summary>
    private class ScamActor
    {
        public readonly List<GameObject> frames = new List<GameObject>();
        public float period, t, sway, phase;
        public int idx;
        public Vector3 pos; public Quaternion rot;
        public Vector3 swayAxis = Vector3.up;
    }
    private readonly List<ScamActor> actors = new List<ScamActor>();
    private bool banging;
    private float nextBang;
    private TMPro.TMP_Text recText;

    private float carHalfW = 0.9f, carTop = 1.45f, carFront = 2.2f;
    private Vector3 winPos;

    /// <summary>Kích thước thật của ô tô người chơi (bỏ qua camera / vật con không có mesh); vô lý thì dùng cỡ sedan.</summary>
    private void MeasureCar()
    {
        carHalfW = 0.9f; carTop = 1.45f; carFront = 2.2f; carBottom = 0f; carHeight = 1.45f; carMeasured = false;
        if (!ModelFit.MeshBounds(car.transform, car.transform, out Bounds b)) return;
        carMeasured = true;
        Vector3 ls = car.transform.lossyScale;
        float w = b.size.x * ls.x, top = b.max.y * ls.y, front = b.max.z * ls.z;
        if (w > 1.2f && w < 2.8f) carHalfW = w * 0.5f;
        if (top > 1f && top < 2.4f) carTop = top;
        if (front > 1.4f && front < 3.2f) carFront = front;
        carCenterZ = 0f; carHalfL = carFront;
        carBottom = b.min.y * ls.y; carHeight = Mathf.Clamp(b.size.y * ls.y, 1.1f, 2.4f);
        float hl = b.extents.z * ls.z;
        if (hl > 1.4f && hl < 3.2f) { carHalfL = hl; carCenterZ = b.center.z * ls.z; }
    }

    /// <summary>Hoạt cảnh mỗi frame (gọi từ Drive): đổi tư thế, lắc lư, tiếng đập kính.</summary>
    private void UpdateScamAnim(float dt)
    {
        if (!carMode && movers.Count == 0) return;
        UpdateCrowd(dt);
        foreach (ScamActor a in actors)
        {
            if (a.frames.Count == 0 || a.frames[0] == null) continue;
            a.t += dt;
            if (a.period > 0f && a.frames.Count > 1 && a.t >= a.period)
            {
                a.t = 0f; a.idx = (a.idx + 1) % a.frames.Count;
                for (int i = 0; i < a.frames.Count; i++) if (a.frames[i] != null) a.frames[i].SetActive(i == a.idx);
            }
            float ang = Mathf.Sin(Time.unscaledTime * 3.2f + a.phase) * a.sway;
            Quaternion q = a.rot * Quaternion.AngleAxis(ang, a.swayAxis);
            foreach (GameObject g in a.frames) if (g != null) g.transform.SetPositionAndRotation(a.pos, q);
        }
        if (banging && Time.unscaledTime >= nextBang)
        {
            nextBang = Time.unscaledTime + 0.44f;
            AudioManager.Play("bat_hit", 0.25f);
            shake = Mathf.Max(shake, 0.12f);
        }
        if (recText != null) recText.text = (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "<color=#FF3B30>●</color> REC  " : "   REC  ") + System.DateTime.Now.ToString("HH:mm:ss");
    }

    private void ShowRec(bool on)
    {
        if (!on) { if (recText != null) Destroy(recText.gameObject); recText = null; return; }
        if (recText != null || canvas == null) return;
        recText = UIKit.Text(canvas.transform, "", 30, new Vector2(400, 50), Vector2.zero, TMPro.TextAlignmentOptions.Left, true, Color.white);
        RectTransform rt = recText.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -110f);
    }

    /// <summary>
    /// Dàn cảnh sau khi xe đổ: "nạn nhân" ngồi bệt ôm chân cạnh xe (lắc lư; bản đứng dậy ẩn sẵn), đồng bọn đứng sát cửa lái
    /// luân phiên chỉ tay / đập kính, hai "người nhà" đứng chắn đầu xe luân phiên chỉ trỏ / chống nạnh. Tất cả quay mặt về ô tô.
    /// </summary>
    private void SpawnScamCrowd(VietDistrict city)
    {
        ClearScamActors();
        GameObject[] people = city.peopleModels, mats = city.peopleMaterials;
        // Đích IK khi đứng (tính từ hông): chân chạm đất, tay theo cử chỉ
        Vector3 fl = new Vector3(-0.11f, -0.9f, 0.02f), fr = new Vector3(0.11f, -0.9f, 0.02f);
        GameObject Frame(int idx, RealModels.Pose pose, bool ik, Vector3 hl, Vector3 hr, string key, string name)
        {
            idx = ((idx % people.Length) + people.Length) % people.Length;
            GameObject mat = mats != null && idx < mats.Length ? mats[idx] : null;
            RealModels.Model m = ik
                ? RealModels.Person(people[idx], mat, pose, new RealModels.IK { on = true, handL = hl, handR = hr, footL = fl, footR = fr }, "scam_" + key)
                : RealModels.Person(people[idx], mat, pose);
            if (m == null || !m.ok) return null;
            GameObject g = RealModels.Spawn(m, transform, Vector3.zero, Quaternion.identity, name, true, 0.04f, 0.004f);
            scamPeople.Add(g);
            return g;
        }
        ScamActor Actor(Vector3 at, Vector3 face, float period, float sway, params GameObject[] frames)
        {
            var a = new ScamActor { period = period, sway = sway, phase = Random.value * 6f };
            at.y = VietDistrict.RoadTop; face.y = 0f;
            a.pos = at; a.rot = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward);
            foreach (GameObject g in frames) if (g != null) a.frames.Add(g);
            for (int i = 0; i < a.frames.Count; i++) { a.frames[i].transform.SetPositionAndRotation(a.pos, a.rot); a.frames[i].SetActive(i == 0); }
            actors.Add(a);
            return a;
        }

        // chỗ đứng sát cửa lái, ngang ghế lái (ghế bên trái / phải tuỳ xe)
        winPos = youPos + DoorOut * (carHalfW + 0.36f) + youDir * (driverRoot != null ? seatLocal.z + 0.15f : 0.3f);
        Vector3 vPos = robPos + Right(youDir) * 0.9f;
        if (AnimatedCrowd) SpawnAnimatedScammers(vPos);   // người có hoạt ảnh Mixamo thật
        else
        {
            // "Nạn nhân": ngồi bệt ôm chân, lắc lư (nghiêng trước sau)
            victimDown = SitVictim(people, mats, out float hipsY);
            var va = Actor(vPos, youPos - vPos, 0f, 6f, victimDown);
            va.pos += Vector3.up * (0.14f - hipsY);   // hông chạm sát mặt đường
            va.swayAxis = Vector3.right;
            if (victimDown != null) victimDown.transform.position = va.pos;
            victimUp = Frame(scamA, RealModels.Pose.Stand, false, default, default, "", "ScamVictimUp");
            if (victimUp != null) { victimUp.transform.SetPositionAndRotation(new Vector3(vPos.x, VietDistrict.RoadTop, vPos.z), va.rot); victimUp.SetActive(false); }

            // Đồng bọn ở cửa lái: chỉ tay vào mặt <-> hai tay đập kính
            // vung: nắm đấm phải giơ cao ngang đầu, tay trái tì lên kính; đập: hai nắm tay chạm kính (cách người ~0.3 m), khuỷu gập
            GameObject point = Frame(scamB, RealModels.Pose.Stand, true, new Vector3(-0.18f, 0.5f, 0.3f), new Vector3(0.24f, 0.78f, 0.1f), "raise", "ScamAccomplice_raise");
            GameObject bang = Frame(scamB, RealModels.Pose.Stand, true, new Vector3(-0.18f, 0.5f, 0.3f), new Vector3(0.12f, 0.52f, 0.32f), "hit", "ScamAccomplice_hit");
            Actor(winPos, Right(youDir), 0.22f, 2f, point, bang);
            accomplice = point;
            accompliceFrames.Clear(); if (point != null) accompliceFrames.Add(point); if (bang != null) accompliceFrames.Add(bang);
        }

        // Hai "người nhà" chạy tới + người hiếu kỳ đi tới xem, kẹt xe phía sau (NightRobbery.CarScamLife.cs)
        SpawnCrowdMovers();
        SpawnJam();
    }

    private readonly List<GameObject> accompliceFrames = new List<GameObject>();

    /// <summary>"Nạn nhân" ngồi bệt giữa đường: tư thế ngồi, hai chân duỗi về trước sát mặt đất, hai tay ôm đầu gối phải.</summary>
    private GameObject SitVictim(GameObject[] people, GameObject[] mats, out float hipsY)
    {
        hipsY = 0.9f;
        int idx = ((scamA % people.Length) + people.Length) % people.Length;
        var ik = new RealModels.IK
        {
            on = true,
            footL = new Vector3(-0.16f, -0.08f, 0.82f), footR = new Vector3(0.12f, 0.02f, 0.55f),   // chân trái duỗi, chân phải hơi co
            handL = new Vector3(0.0f, 0.12f, 0.42f), handR = new Vector3(0.2f, 0.12f, 0.45f),      // ôm gối phải
        };
        RealModels.Model m = RealModels.Person(people[idx], mats != null && idx < mats.Length ? mats[idx] : null, RealModels.Pose.Sit, ik, "scam_sit");
        if (m == null || !m.ok) return null;
        hipsY = m.hips.y;
        GameObject g = RealModels.Spawn(m, transform, Vector3.zero, Quaternion.identity, "ScamVictim", true, 0.04f, 0.004f);
        scamPeople.Add(g);
        return g;
    }

    /// <summary>Dẹp bọn ăn vạ (đồng bọn, nạn nhân, người nhà) - người hiếu kỳ, xe kẹt, mảnh vỡ vẫn ở lại.</summary>
    private void RemoveScammers()
    {
        foreach (ScamActor a in actors) foreach (GameObject g in a.frames) if (g != null) Destroy(g);
        if (victimUp != null) Destroy(victimUp);
        actors.Clear(); accompliceFrames.Clear(); banging = false;
        victimDown = victimUp = accomplice = null;
        for (int i = movers.Count - 1; i >= 0; i--)
            if (movers[i].role != "onlooker") { if (movers[i].t != null) Destroy(movers[i].t.gameObject); movers.RemoveAt(i); }
        victimMover = accompliceMover = null;
    }

    private void ClearScamActors()
    {
        ClearCrowd();
        foreach (GameObject g in scamPeople) if (g != null) Destroy(g);
        scamPeople.Clear();
        actors.Clear(); accompliceFrames.Clear(); banging = false;
        victimDown = victimUp = accomplice = null;
    }

    /// <summary>Kết quả vào tiền / XP / đánh giá cửa hàng.</summary>
    private static void ApplyScam(ScamEnd e, bool froze)
    {
        EconomyManager eco = EconomyManager.Instance;
        switch (e)
        {
            case ScamEnd.Paid:
            {
                float cash = eco != null ? Mathf.Round(Mathf.Clamp(eco.Money * (froze ? 0.18f : 0.12f), 40f, froze ? 180f : 120f)) : 0f;
                if (eco != null && cash > 0f) eco.Charge(cash, MoneyCategory.Other, "Paid off a staged-accident scam");
                ProgressHUD.Toast($"<color=#FF8A7A>Bạn bị nhóm \"ăn vạ\" lấy {EconomyManager.Format(cash)}. Đơn hàng vẫn còn - đi giao tiếp nhé.</color>");
                break;
            }
            case ScamEnd.Snatched:
            {
                float cash = eco != null ? Mathf.Round(Mathf.Clamp(eco.Money * 0.08f, 30f, 100f)) : 0f;
                if (eco != null && cash > 0f) eco.Charge(cash, MoneyCategory.Other, "Phone and wallet snatched in a scam");
                ProgressHUD.Toast($"<color=#FF8A7A>Bị giật điện thoại và ví! Mất {EconomyManager.Format(cash)}. Đơn hàng vẫn còn trên xe.</color>");
                break;
            }
            case ScamEnd.Exposed:
                if (eco != null) eco.AddIncome(20f, MoneyCategory.Other, "Dashcam exposed a scam");
                StoreProgress.AddXP(8);
                ProgressHUD.Toast("<color=#6BE37B>Camera hành trình đã vạch mặt nhóm ăn vạ! +$20, +8 XP. Đi giao hàng tiếp nhé.</color>");
                break;
            case ScamEnd.Arrested:
                if (eco != null) eco.AddIncome(60f, MoneyCategory.Other, "Police reward (scam ring)");
                StoreProgress.AddXP(12);
                ProgressHUD.Toast("<color=#6BE37B>Công an đã bắt nhóm ăn vạ. Thưởng +$60, +12 XP. Đi giao hàng tiếp nhé.</color>");
                break;
            case ScamEnd.Escaped:
                ProgressHUD.Toast("<color=#6BE37B>Bạn lái đi kịp, không mất gì. Đi giao hàng tiếp nhé.</color>");
                break;
            case ScamEnd.Viral:
                ReviewManager.Submit(new ReviewManager.Visit { wanted = 3, got = 0, checkedOut = false, queueTolerance = 5f, customerType = "Viral clip", maxDirt = 20, sawSpoiled = 5 });
                ProgressHUD.Toast("<color=#FFB86B>Thoát được, không mất tiền - nhưng có người quay clip. Cửa hàng nhận một đánh giá xấu.</color>");
                break;
        }
    }
}
