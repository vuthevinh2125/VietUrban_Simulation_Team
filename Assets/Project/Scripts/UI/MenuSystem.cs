using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu của game (tự tạo, không cần đặt vào scene):
///  - Menu chính khi mở game: Continue (có file lưu) / New Game / Settings / Food Delivery Mode / Quit.
///    Food Delivery Mode mở chế độ thứ hai: game nấu ăn - giao hàng của Nam (Assets/FoodDelivery).
///  - Menu tạm dừng (Esc khi đang chơi): Resume / Save / Settings / Main Menu / Quit.
///  - Settings: độ nhạy chuột, âm lượng, nhạc nền (chọn bài), âm lượng nhạc, chất lượng đồ hoạ, toàn màn hình, bóng đổ đèn trần.
///  - Game Over khi hết tiền.
/// Khi menu mở: thời gian dừng (Time.timeScale = 0), hiện chuột.
/// </summary>
public class MenuSystem : MonoBehaviour
{
    public static MenuSystem Instance { get; private set; }

    private GameObject mainMenu, pauseMenu, settingsMenu, confirmMenu, gameOverMenu;
    private Button continueButton;
    private TMP_Text calDay, calInfo, calSaved, pauseNote, confirmText;
    private GameObject continueTag;
    private TMP_Text sensValue, volValue, musicValue, musicVolValue, qualValue, fullValue, shadowValue;
    private Action confirmAction;
    private GameObject settingsReturnTo;

    public bool IsMainMenuOpen => mainMenu != null && mainMenu.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Create();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Create();

    private static void Create()
    {
        if (Instance == null && SupermarketScene.IsActive) new GameObject("MenuSystem").AddComponent<MenuSystem>();
    }

    void Awake()
    {
        Instance = this;
        GameSettings.Load();
        Build();
        ShowMain();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (confirmMenu.activeSelf) { confirmMenu.SetActive(false); return; }
        if (settingsMenu.activeSelf) { CloseSettings(); return; }
        if (mainMenu.activeSelf || gameOverMenu.activeSelf) return;
        if (pauseMenu.activeSelf) { Resume(); return; }

        // Esc vừa dùng để đóng bảng khác (máy tính, bảng giá...) thì không mở menu tạm dừng
        if (UIState.IsBlockingUIOpen || UIState.LastClosedFrame >= Time.frameCount - 1) return;
        OpenPause();
    }

    /// <summary>
    /// Còn giao diện nào mở (menu, máy tính, bảng giá...) thì luôn giữ chuột hiện + tự do,
    /// kể cả khi script khác hoặc cú click vào cửa sổ Game lỡ khoá chuột lại.
    /// </summary>
    void LateUpdate()
    {
        if (!UIState.IsBlockingUIOpen) return;
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }

    // ================= Menu chính =================
    private void ShowMain()
    {
        HideAll();
        mainMenu.SetActive(true);
        bool hasSave = File.Exists(SaveManager.SavePath);
        continueButton.interactable = hasSave;
        FillCalendar(hasSave);
        Freeze();
    }

    private void Continue()
    {
        if (TutorialManager.Instance != null) TutorialManager.Instance.Stop();
        HideAll();
        Unfreeze();
        if (SaveManager.Instance != null) SaveManager.Instance.Load();
    }

    private void NewGame()
    {
        if (File.Exists(SaveManager.SavePath))
        {
            Confirm("Start a new game?\n<size=75%>Your saved game will be deleted.</size>", () =>
            {
                SaveManager.DeleteSave();
                HideAll();
                Unfreeze();
                StartTutorial();
            });
            return;
        }
        HideAll();
        Unfreeze();
        StartTutorial();
    }

    /// <summary>New Game: chiếu cảnh mở đầu (cốt truyện, [Space] bỏ qua) rồi mới bắt đầu hướng dẫn.</summary>
    private static void StartTutorial()
    {
        IntroCutscene.Play(() =>
        {
            ProgressHUD.Toast("Welcome to your new supermarket!");
            if (TutorialManager.Instance != null) TutorialManager.Instance.Begin();
        });
    }

    /// <summary>Tờ lịch bloc ở menu chính = bản lưu: số ngày, tiền, cấp cửa hàng, giờ lưu.</summary>
    private void FillCalendar(bool hasSave)
    {
        continueTag.SetActive(hasSave);
        calDay.text = "?"; calInfo.text = "No saved game"; calSaved.text = "";
        if (!hasSave) return;
        try
        {
            var d = JsonUtility.FromJson<SaveManager.SaveData>(File.ReadAllText(SavePathSafe()));
            if (d == null) { calInfo.text = "Saved game found"; return; }
            calDay.text = d.day.ToString();
            calInfo.text = $"{EconomyManager.Format(d.money)} · Store Lv {d.level}";
            string at = d.savedAt ?? "";
            calSaved.text = "SAVED " + (at.Length >= 16 ? at.Substring(0, 16) : at);
        }
        catch { calInfo.text = "Saved game found"; }
    }

    /// <summary>Tờ giấy nhớ ở menu tạm dừng: ngày, giờ, tiền quỹ lúc dừng.</summary>
    private static string PauseSummary()
    {
        GameManager gm = GameManager.Instance;
        EconomyManager eco = EconomyManager.Instance;
        string t = gm != null ? $"Day {gm.currentDay}, {gm.ClockString}" : "";
        if (gm != null) t += gm.IsStoreOpen ? " - open" : " - closed";
        if (eco != null) t += $"\nCash box: {EconomyManager.Format(eco.Money)}";
        t += $"\nStore level {StoreProgress.Level}";
        return t;
    }

    private static string SavePathSafe() => SaveManager.SavePath;

    // ================= Tạm dừng =================
    private void OpenPause()
    {
        HideAll();
        pauseNote.text = PauseSummary();
        pauseMenu.SetActive(true);
        Freeze();
    }

    private void Resume()
    {
        HideAll();
        Unfreeze();
    }

    private void SaveFromPause()
    {
        if (SaveManager.Instance == null) return;
        if (SaveManager.Instance.TryManualSave()) Resume(); // không lưu được (còn khách) thì thông báo hiện lên, vẫn ở menu
    }

    /// <summary>Chế độ thứ hai: mở menu chính của game nấu ăn - giao hàng (scene MainMenu của Nam).</summary>
    private void OpenFoodDelivery()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void ToMainMenu() =>
        Confirm("Return to main menu?\n<size=75%>Progress since your last save will be lost.</size>", GameSession.ReloadScene);

    private void Quit() =>
        Confirm("Quit the game?\n<size=75%>Progress since your last save will be lost.</size>", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

    /// <summary>GameManager gọi khi hết tiền.</summary>
    public void ShowGameOver()
    {
        HideAll();
        gameOverMenu.SetActive(true);
        Freeze();
    }

    // ================= Settings =================
    private void OpenSettings(GameObject from)
    {
        settingsReturnTo = from;
        if (from != null) from.SetActive(false);
        settingsMenu.SetActive(true);
        RefreshSettings();
    }

    private void CloseSettings()
    {
        settingsMenu.SetActive(false);
        if (settingsReturnTo != null) settingsReturnTo.SetActive(true);
    }

    private void RefreshSettings()
    {
        sensValue.text = GameSettings.MouseSensitivity.ToString("0.0");
        volValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        musicValue.text = GameSettings.MusicName;
        musicVolValue.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
        qualValue.text = GameSettings.QualityName;
        fullValue.text = GameSettings.Fullscreen ? "On" : "Off";
        shadowValue.text = GameSettings.LampShadows ? "On" : "Off";
    }

    // ================= Tiện ích =================
    private void Confirm(string message, Action onYes)
    {
        confirmText.text = message;
        confirmAction = onYes;
        confirmMenu.SetActive(true);
        confirmMenu.transform.SetAsLastSibling();
    }

    private void HideAll()
    {
        mainMenu.SetActive(false);
        pauseMenu.SetActive(false);
        settingsMenu.SetActive(false);
        confirmMenu.SetActive(false);
        gameOverMenu.SetActive(false);
    }

    private void Freeze()
    {
        UIState.Open(this);
        Time.timeScale = 0f;
    }

    private void Unfreeze()
    {
        UIState.Close(this);
        bool over = GameManager.Instance != null && GameManager.Instance.IsGameOver;
        if (!over) Time.timeScale = 1f;
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 900).transform;
        const TextAlignmentOptions Left = TextAlignmentOptions.Left, Center = TextAlignmentOptions.Center;

        // --- Menu chính: bức tường vôi vàng trước tiệm, đồ vật treo / dán trên tường ---
        mainMenu = ShopUI.Full("MainMenu", canvas, ShopUI.Wall).gameObject;
        Transform mm = mainMenu.transform;
        ShopUI.GlowLight(mm, new Vector2(1700, 1300), new Vector2(160, 230), new Color(1f, 0.9f, 0.62f, 0.75f)).gameObject.AddComponent<UIMotion>().flicker = 0.35f;
        ShopUI.Text(mm, "KHOAN CẮT BÊ TÔNG\n<size=125%>09xx.xxx.xxx</size>", ShopUI.Print, 44, new Vector2(620, 150), new Vector2(560, -330), new Color(0.09f, 0.12f, 0.24f, 0.5f), Left, -4f, 4f);
        ShopUI.Text(mm, "CẤM ĐỔ RÁC", ShopUI.Print, 34, new Vector2(400, 50), new Vector2(-130, 420), new Color(0.6f, 0.08f, 0.08f, 0.5f), Center, 3f, 6f);
        // cửa sổ chớp xanh, một cánh mở thấy đèn trong nhà
        RectTransform win = ShopUI.Sheet("Window", mm, new Vector2(400, 520), new Vector2(640, 70), new Color(0.23f, 0.165f, 0.08f));
        RectTransform shutter = ShopUI.Box("Shutter", win, new Vector2(180, 496), new Vector2(-98, 0), new Color(0.18f, 0.42f, 0.31f));
        for (int i = 0; i < 20; i++) ShopUI.Box("Slat", shutter, new Vector2(180, 5), new Vector2(0, 230 - i * 24), new Color(0.13f, 0.31f, 0.23f));
        RectTransform pane = ShopUI.Box("Pane", win, new Vector2(180, 496), new Vector2(98, 0), new Color(1f, 0.83f, 0.5f));
        ShopUI.Box("Figure", pane, new Vector2(130, 260), new Vector2(0, -118), new Color(0.16f, 0.125f, 0.09f, 0.8f));
        ShopUI.GlowLight(win, new Vector2(560, 680), new Vector2(98, 0), new Color(1f, 0.84f, 0.5f, 0.35f));
        // bóng đèn tròn, thỉnh thoảng chập chờn
        ShopUI.Box("Wire", mm, new Vector2(4, 150), new Vector2(130, 465), ShopUI.Ink);
        ShopUI.Box("Bulb", mm, new Vector2(44, 56), new Vector2(130, 372), new Color(1f, 0.95f, 0.77f));
        ShopUI.GlowLight(mm, new Vector2(440, 440), new Vector2(130, 372), new Color(1f, 0.93f, 0.66f, 0.9f)).gameObject.AddComponent<UIMotion>().flicker = 0.5f;
        ShopUI.Full("Vignette", mm, new Color(0.16f, 0.09f, 0.02f, 0.8f), false).GetComponent<Image>().sprite = ShopUI.Vignette;
        // biển hiệu + biển tên phố
        RectTransform logo = ShopUI.SignBoard(mm, new Vector2(600, 170), new Vector2(-590, 395), -1.5f);
        ShopUI.Text(logo, "VIETURBAN", ShopUI.Sign, 88, new Vector2(580, 110), new Vector2(0, 18), ShopUI.SignGold);
        ShopUI.Text(logo, "TẠP HOÁ BÀ LAN · SIÊU THỊ MINI", ShopUI.Body, 17, new Vector2(580, 30), new Vector2(0, -58), new Color(1f, 0.88f, 0.54f), Center, 0f, 8f);
        RectTransform plate = ShopUI.Sheet("StreetPlate", mm, new Vector2(330, 84), new Vector2(-705, 255), Color.white, -1.5f);
        ShopUI.Box("Blue", plate, new Vector2(320, 74), Vector2.zero, ShopUI.Enamel);
        ShopUI.Text(plate, "QUẬN HOÀN KIẾM", ShopUI.Body, 14, new Vector2(310, 22), new Vector2(0, 22), Color.white, Center, 0f, 8f);
        ShopUI.Text(plate, "PHỐ HAI BÀ TRƯNG", ShopUI.Print, 32, new Vector2(310, 44), new Vector2(0, -10), Color.white, Center, 0f, 4f);
        // lịch bloc = Continue (số ngày của bản lưu)
        RectTransform cal = ShopUI.Group("Calendar", mm, new Vector2(360, 500), new Vector2(-640, 140), -2f);
        cal.pivot = new Vector2(0.5f, 1f);
        UIMotion calSway = cal.gameObject.AddComponent<UIMotion>(); calSway.swayDegrees = 0.9f; calSway.swaySpeed = 1.2f;
        for (int i = 3; i >= 1; i--) ShopUI.Box("Edge", cal, new Vector2(360, 450), new Vector2(0, -1 - i * 6), Color.Lerp(ShopUI.Paper, ShopUI.Muted, 0.12f * i));
        RectTransform page = ShopUI.Sheet("Page", cal, new Vector2(360, 450), new Vector2(0, -1), ShopUI.Paper, 0f, true);
        ShopUI.Box("Binding", cal, new Vector2(360, 26), new Vector2(0, 237), new Color(0.5f, 0.52f, 0.56f));
        continueButton = page.gameObject.AddComponent<Button>();
        continueButton.transition = Selectable.Transition.None;
        continueButton.onClick.AddListener(Continue);
        page.gameObject.AddComponent<UIMotion>().hoverScale = 1.04f;
        RectTransform calHead = ShopUI.Box("Head", page, new Vector2(360, 46), new Vector2(0, 202), ShopUI.SignRed);
        calSaved = ShopUI.Text(calHead, "", ShopUI.Body, 15, new Vector2(350, 40), Vector2.zero, new Color(1f, 0.95f, 0.84f), Center, 0f, 6f);
        ShopUI.Text(page, "NGÀY · DAY", ShopUI.Body, 15, new Vector2(340, 26), new Vector2(0, 160), ShopUI.Muted, Center, 0f, 10f);
        calDay = ShopUI.Text(page, "?", ShopUI.Print, 220, new Vector2(340, 230), new Vector2(0, 30), ShopUI.SignRed);
        calDay.enableAutoSizing = true; calDay.fontSizeMin = 80; calDay.fontSizeMax = 220;
        ShopUI.Box("Rule", page, new Vector2(300, 2), new Vector2(0, -105), ShopUI.A(ShopUI.Muted, 0.5f));
        calInfo = ShopUI.Text(page, "", ShopUI.Hand, 34, new Vector2(350, 50), new Vector2(0, -145), ShopUI.Pen);
        RectTransform tag = ShopUI.Box("ContinueTag", mm, new Vector2(250, 56), new Vector2(-640, -420), ShopUI.Ink);
        ShopUI.Text(tag, "CONTINUE", ShopUI.Print, 30, new Vector2(250, 56), Vector2.zero, ShopUI.SignGold, Center, 0f, 8f);
        tag.gameObject.AddComponent<UIMotion>().pulse = 0.03f;
        continueTag = tag.gameObject;
        // các mục menu: bìa các-tông viết bút dạ
        ShopUI.CardButton(mm, "New Game", "open the shop from day 1", new Vector2(440, 112), new Vector2(-90, 205), -2f, NewGame);
        ShopUI.CardButton(mm, "Food Delivery", "mode 2: cook and deliver", new Vector2(420, 112), new Vector2(-40, 65), 1.5f, OpenFoodDelivery, ShopUI.PaperOld, ShopUI.SignRed);
        ShopUI.CardButton(mm, "Settings", "sound, mouse, graphics", new Vector2(430, 112), new Vector2(-80, -75), -1f, () => OpenSettings(mainMenu));
        ShopUI.CardButton(mm, "Quit", "close up for tonight", new Vector2(340, 112), new Vector2(-20, -215), 2f, Quit, ShopUI.CardboardDark);
        RectTransform foot = ShopUI.Box("Footer", mm, new Vector2(0, 64), Vector2.zero, ShopUI.Night);
        foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(0.5f, 0f);
        foot.sizeDelta = new Vector2(0f, 64f); foot.anchoredPosition = Vector2.zero;
        ShopUI.Text(foot, "WASD move · Mouse look · E / LMB interact · Esc pause", ShopUI.Body, 20, new Vector2(1400, 40), Vector2.zero, new Color(0.85f, 0.8f, 0.7f));

        // --- Tạm dừng: biển treo "TẠM NGHỈ" + bìa các-tông + giấy nhớ ---
        pauseMenu = ShopUI.Full("PauseMenu", canvas, new Color(0.03f, 0.035f, 0.05f, 0.8f)).gameObject;
        Transform pm = pauseMenu.transform;
        RectTransform hang = ShopUI.Group("Hanging", pm, new Vector2(620, 330), Vector2.zero);
        hang.anchorMin = hang.anchorMax = new Vector2(0.5f, 1f); hang.pivot = new Vector2(0.5f, 1f); hang.anchoredPosition = Vector2.zero;
        UIMotion swing = hang.gameObject.AddComponent<UIMotion>(); swing.swayDegrees = 1.5f; swing.swaySpeed = 1.3f;
        ShopUI.Box("StringL", hang, new Vector2(3, 110), new Vector2(-190, 110), ShopUI.PaperOld);
        ShopUI.Box("StringR", hang, new Vector2(3, 110), new Vector2(190, 110), ShopUI.PaperOld);
        RectTransform closed = ShopUI.Sheet("ClosedSign", hang, new Vector2(620, 220), new Vector2(0, -55), Color.white);
        ShopUI.Box("Red", closed, new Vector2(600, 200), Vector2.zero, ShopUI.SignRed);
        ShopUI.Text(closed, "TẠM NGHỈ", ShopUI.Print, 108, new Vector2(600, 130), new Vector2(0, 26), Color.white, Center, 0f, 8f);
        ShopUI.Text(closed, "PAUSED · BACK IN 5 MINUTES", ShopUI.Print, 30, new Vector2(600, 40), new Vector2(0, -66), Color.white, Center, 0f, 12f);
        ShopUI.CardButton(pm, "Resume", null, new Vector2(400, 96), new Vector2(-430, 120), -2f, Resume, ShopUI.PaperOld, ShopUI.SignRed);
        ShopUI.CardButton(pm, "Save game", null, new Vector2(400, 96), new Vector2(-400, 5), 1.5f, SaveFromPause);
        ShopUI.CardButton(pm, "Settings", null, new Vector2(400, 96), new Vector2(-425, -110), -1f, () => OpenSettings(pauseMenu));
        ShopUI.CardButton(pm, "Main menu", null, new Vector2(400, 96), new Vector2(-395, -225), 2f, ToMainMenu);
        ShopUI.CardButton(pm, "Quit", null, new Vector2(300, 96), new Vector2(-460, -340), -1.5f, Quit, ShopUI.CardboardDark);
        RectTransform note = ShopUI.Sheet("Note", pm, new Vector2(540, 300), new Vector2(400, 40), ShopUI.Note, 1.5f);
        ShopUI.Tape(note);
        ShopUI.Text(note, "HÔM NAY · TODAY SO FAR", ShopUI.Print, 26, new Vector2(490, 40), new Vector2(0, 110), ShopUI.SignRed, Left, 0f, 6f);
        pauseNote = ShopUI.Text(note, "", ShopUI.Hand, 38, new Vector2(490, 200), new Vector2(0, -25), ShopUI.Pen, TextAlignmentOptions.TopLeft);
        RectTransform tip = ShopUI.Sheet("Tip", pm, new Vector2(540, 140), new Vector2(400, -215), ShopUI.Paper, -1f);
        ShopUI.Text(tip, "MẸO · TIP", ShopUI.Body, 15, new Vector2(490, 24), new Vector2(0, 45), ShopUI.SignRed, Left, 0f, 10f);
        ShopUI.Text(tip, "Fresh food spoils. Look at a shelf and press R to throw out bad items before the health inspector finds them.", ShopUI.Body, 20, new Vector2(490, 84), new Vector2(0, -14), ShopUI.Ink, TextAlignmentOptions.TopLeft);

        // --- Settings: tờ phiếu điền bút bi ---
        settingsMenu = ShopUI.Full("Settings", canvas, new Color(0.03f, 0.035f, 0.05f, 0.85f)).gameObject;
        RectTransform sw = ShopUI.Sheet("Form", settingsMenu.transform, new Vector2(900, 800), Vector2.zero, ShopUI.Paper, 0.5f, true);
        ShopUI.Text(sw, "PHIẾU CÀI ĐẶT · SETTINGS", ShopUI.Print, 44, new Vector2(840, 60), new Vector2(0, 340), ShopUI.SignRed, Left, 0f, 6f);
        ShopUI.Box("Rule", sw, new Vector2(840, 3), new Vector2(0, 300), ShopUI.SignRed);
        sensValue = SettingRow(sw, "Mouse sensitivity", 240, () => { GameSettings.SetSensitivity(GameSettings.MouseSensitivity - 0.2f); RefreshSettings(); }, () => { GameSettings.SetSensitivity(GameSettings.MouseSensitivity + 0.2f); RefreshSettings(); });
        volValue = SettingRow(sw, "Volume", 170, () => { GameSettings.SetVolume(GameSettings.Volume - 0.1f); RefreshSettings(); }, () => { GameSettings.SetVolume(GameSettings.Volume + 0.1f); RefreshSettings(); });
        musicValue = SettingRow(sw, "Music", 100, () => { GameSettings.SetMusic(GameSettings.Music - 1); RefreshSettings(); }, () => { GameSettings.SetMusic(GameSettings.Music + 1); RefreshSettings(); });
        musicVolValue = SettingRow(sw, "Music volume", 30, () => { GameSettings.SetMusicVolume(GameSettings.MusicVolume - 0.1f); RefreshSettings(); }, () => { GameSettings.SetMusicVolume(GameSettings.MusicVolume + 0.1f); RefreshSettings(); });
        qualValue = SettingRow(sw, "Graphics quality", -40, () => { GameSettings.SetQuality(GameSettings.Quality - 1); RefreshSettings(); }, () => { GameSettings.SetQuality(GameSettings.Quality + 1); RefreshSettings(); });
        fullValue = SettingRow(sw, "Fullscreen", -110, () => { GameSettings.SetFullscreen(!GameSettings.Fullscreen); RefreshSettings(); }, () => { GameSettings.SetFullscreen(!GameSettings.Fullscreen); RefreshSettings(); });
        shadowValue = SettingRow(sw, "Lamp shadows (slow)", -180, () => { GameSettings.SetLampShadows(!GameSettings.LampShadows); RefreshSettings(); }, () => { GameSettings.SetLampShadows(!GameSettings.LampShadows); RefreshSettings(); });
        ShopUI.PrintButton(sw, "DONE", new Vector2(260, 66), new Vector2(0, -320), CloseSettings);

        // --- Game Over: cửa cuốn kéo xuống, dán giấy "SANG NHƯỢNG CỬA HÀNG" ---
        gameOverMenu = ShopUI.Full("GameOver", canvas, new Color(0.5f, 0.52f, 0.55f)).gameObject;
        Transform gov = gameOverMenu.transform;
        for (int i = 0; i < 60; i++) ShopUI.Box("Slat", gov, new Vector2(5000, 6), new Vector2(0, 760 - i * 26), new Color(0.37f, 0.39f, 0.42f));
        ShopUI.Full("Vignette", gov, new Color(0f, 0f, 0f, 0.85f), false).GetComponent<Image>().sprite = ShopUI.Vignette;
        RectTransform sale = ShopUI.Sheet("TransferSign", gov, new Vector2(920, 520), new Vector2(0, 110), ShopUI.Paper, -2f);
        ShopUI.Tape(sale, -320f, -8f); ShopUI.Tape(sale, 320f, 6f);
        sale.gameObject.AddComponent<UIMotion>().swayDegrees = 0.6f;
        ShopUI.Text(sale, "SANG NHƯỢNG", ShopUI.Print, 150, new Vector2(900, 170), new Vector2(0, 150), ShopUI.SignRed, Center, 0f, 4f);
        ShopUI.Text(sale, "CỬA HÀNG", ShopUI.Print, 96, new Vector2(900, 110), new Vector2(0, 30), ShopUI.SignRed, Center, 0f, 20f);
        ShopUI.Text(sale, "SHOP FOR TRANSFER · GAME OVER", ShopUI.Print, 30, new Vector2(900, 40), new Vector2(0, -50), ShopUI.Ink, Center, 0f, 12f);
        ShopUI.Box("Rule", sale, new Vector2(820, 2), new Vector2(0, -85), ShopUI.A(ShopUI.Muted, 0.6f));
        ShopUI.Text(sale, "Your supermarket ran out of money.", ShopUI.Hand, 40, new Vector2(880, 50), new Vector2(0, -125), ShopUI.Pen);
        ShopUI.Text(sale, "Your saved game has been deleted - start a New Game to try again.", ShopUI.Body, 20, new Vector2(880, 30), new Vector2(0, -195), ShopUI.Muted);
        ShopUI.PrintButton(gov, "MAIN MENU", new Vector2(340, 72), new Vector2(-170, -300), GameSession.ReloadScene);
        ShopUI.PrintButton(gov, "QUIT", new Vector2(240, 72), new Vector2(150, -300), Quit, 1);

        // --- Hộp xác nhận (luôn nằm trên cùng): tờ giấy + hai nút ---
        confirmMenu = ShopUI.Full("Confirm", canvas, new Color(0f, 0f, 0f, 0.65f)).gameObject;
        RectTransform cw = ShopUI.Sheet("Window", confirmMenu.transform, new Vector2(700, 340), Vector2.zero, ShopUI.Paper, -1f, true);
        ShopUI.Tape(cw);
        confirmText = ShopUI.Text(cw, "", ShopUI.Body, 30, new Vector2(640, 170), new Vector2(0, 55), ShopUI.Ink);
        confirmText.fontStyle = FontStyles.Bold;
        ShopUI.PrintButton(cw, "YES", new Vector2(200, 64), new Vector2(-120, -105), () => { confirmMenu.SetActive(false); confirmAction?.Invoke(); });
        ShopUI.PrintButton(cw, "NO", new Vector2(200, 64), new Vector2(120, -105), () => confirmMenu.SetActive(false), 2);

        HideAll();
    }

    private static TMP_Text SettingRow(Transform parent, string label, float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        ShopUI.Text(parent, label, ShopUI.Body, 26, new Vector2(400, 56), new Vector2(-220, y), ShopUI.Ink, TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
        ShopUI.PrintButton(parent, "<", new Vector2(56, 52), new Vector2(90, y), minus, 2);
        TMP_Text value = ShopUI.Text(parent, "", ShopUI.Hand, 40, new Vector2(240, 56), new Vector2(240, y), ShopUI.Pen);
        ShopUI.PrintButton(parent, ">", new Vector2(56, 52), new Vector2(390, y), plus, 2);
        ShopUI.Box("Dots", parent, new Vector2(840, 1), new Vector2(0, y - 35), ShopUI.A(ShopUI.Muted, 0.4f));
        return value;
    }
}
