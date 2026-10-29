using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu của game (tự tạo, không cần đặt vào scene):
///  - Menu chính khi mở game: Continue (có file lưu) / New Game / Settings / Quit.
///  - Menu tạm dừng (Esc khi đang chơi): Resume / Save / Settings / Main Menu / Quit.
///  - Settings: độ nhạy chuột, âm lượng, chất lượng đồ hoạ, toàn màn hình, bóng đổ đèn trần.
///  - Game Over khi hết tiền.
/// Khi menu mở: thời gian dừng (Time.timeScale = 0), hiện chuột.
/// </summary>
public class MenuSystem : MonoBehaviour
{
    public static MenuSystem Instance { get; private set; }

    private GameObject mainMenu, pauseMenu, settingsMenu, confirmMenu, gameOverMenu;
    private Button continueButton;
    private TMP_Text saveInfo, confirmText;
    private TMP_Text sensValue, volValue, qualValue, fullValue, shadowValue;
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
        if (Instance == null) new GameObject("MenuSystem").AddComponent<MenuSystem>();
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
        saveInfo.text = hasSave ? SaveSummary() : "No saved game";
        Freeze();
    }

    private void Continue()
    {
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
                ProgressHUD.Toast("Welcome to your new supermarket!\nOpen the computer to order products.");
            });
            return;
        }
        HideAll();
        Unfreeze();
        ProgressHUD.Toast("Welcome to your new supermarket!\nOpen the computer to order products.");
    }

    private static string SaveSummary()
    {
        try
        {
            var d = JsonUtility.FromJson<SaveManager.SaveData>(File.ReadAllText(SavePathSafe()));
            return d == null ? "" : $"Day {d.day}  ·  {EconomyManager.Format(d.money)}  ·  Level {d.level}\n<size=80%>Saved {d.savedAt}</size>";
        }
        catch { return "Saved game found"; }
    }

    private static string SavePathSafe() => SaveManager.SavePath;

    // ================= Tạm dừng =================
    private void OpenPause()
    {
        HideAll();
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

        // --- Menu chính ---
        mainMenu = UIKit.FullScreen("MainMenu", canvas, new Color(0.04f, 0.06f, 0.1f, 0.93f));
        UIKit.Text(mainMenu.transform, "VIETURBAN", 92, new Vector2(1200, 120), new Vector2(0, 300), TextAlignmentOptions.Center, true, new Color(0.3f, 0.75f, 1f));
        UIKit.Text(mainMenu.transform, "SUPERMARKET SIMULATOR", 46, new Vector2(1200, 70), new Vector2(0, 210), TextAlignmentOptions.Center, true);
        continueButton = UIKit.Btn(mainMenu.transform, "Continue", new Vector2(380, 74), new Vector2(0, 80), Continue, UIKit.Green, 34);
        saveInfo = UIKit.Text(mainMenu.transform, "", 22, new Vector2(800, 60), new Vector2(0, 15), TextAlignmentOptions.Center, false, new Color(0.8f, 0.85f, 0.9f));
        UIKit.Btn(mainMenu.transform, "New Game", new Vector2(380, 74), new Vector2(0, -60), NewGame, null, 34);
        UIKit.Btn(mainMenu.transform, "Settings", new Vector2(380, 74), new Vector2(0, -150), () => OpenSettings(mainMenu), null, 34);
        UIKit.Btn(mainMenu.transform, "Quit", new Vector2(380, 74), new Vector2(0, -240), Quit, UIKit.Red, 34);
        UIKit.Text(mainMenu.transform, "WASD move · Mouse look · E / LMB interact · Esc pause", 22, new Vector2(1200, 40), new Vector2(0, -400), TextAlignmentOptions.Center, false, new Color(0.7f, 0.75f, 0.8f));

        // --- Tạm dừng ---
        pauseMenu = UIKit.FullScreen("PauseMenu", canvas, new Color(0f, 0f, 0f, 0.6f));
        GameObject pw = UIKit.Panel("Window", pauseMenu.transform, new Vector2(460, 560), Vector2.zero, UIKit.Window);
        GameObject ph = UIKit.Panel("Header", pw.transform, new Vector2(460, 70), new Vector2(0, 245), UIKit.Header);
        UIKit.Text(ph.transform, "PAUSED", 36, new Vector2(440, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        UIKit.Btn(pw.transform, "Resume", new Vector2(340, 66), new Vector2(0, 140), Resume, UIKit.Green);
        UIKit.Btn(pw.transform, "Save Game", new Vector2(340, 66), new Vector2(0, 60), SaveFromPause);
        UIKit.Btn(pw.transform, "Settings", new Vector2(340, 66), new Vector2(0, -20), () => OpenSettings(pauseMenu));
        UIKit.Btn(pw.transform, "Main Menu", new Vector2(340, 66), new Vector2(0, -100), ToMainMenu);
        UIKit.Btn(pw.transform, "Quit", new Vector2(340, 66), new Vector2(0, -180), Quit, UIKit.Red);

        // --- Settings ---
        settingsMenu = UIKit.FullScreen("Settings", canvas, new Color(0f, 0f, 0f, 0.7f));
        GameObject sw = UIKit.Panel("Window", settingsMenu.transform, new Vector2(760, 600), Vector2.zero, UIKit.Window);
        GameObject sh = UIKit.Panel("Header", sw.transform, new Vector2(760, 70), new Vector2(0, 265), UIKit.Header);
        UIKit.Text(sh.transform, "SETTINGS", 36, new Vector2(740, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        sensValue = SettingRow(sw.transform, "Mouse sensitivity", 160, () => { GameSettings.SetSensitivity(GameSettings.MouseSensitivity - 0.2f); RefreshSettings(); }, () => { GameSettings.SetSensitivity(GameSettings.MouseSensitivity + 0.2f); RefreshSettings(); });
        volValue = SettingRow(sw.transform, "Volume", 80, () => { GameSettings.SetVolume(GameSettings.Volume - 0.1f); RefreshSettings(); }, () => { GameSettings.SetVolume(GameSettings.Volume + 0.1f); RefreshSettings(); });
        qualValue = SettingRow(sw.transform, "Graphics quality", 0, () => { GameSettings.SetQuality(GameSettings.Quality - 1); RefreshSettings(); }, () => { GameSettings.SetQuality(GameSettings.Quality + 1); RefreshSettings(); });
        fullValue = SettingRow(sw.transform, "Fullscreen", -80, () => { GameSettings.SetFullscreen(!GameSettings.Fullscreen); RefreshSettings(); }, () => { GameSettings.SetFullscreen(!GameSettings.Fullscreen); RefreshSettings(); });
        shadowValue = SettingRow(sw.transform, "Lamp shadows (slow)", -160, () => { GameSettings.SetLampShadows(!GameSettings.LampShadows); RefreshSettings(); }, () => { GameSettings.SetLampShadows(!GameSettings.LampShadows); RefreshSettings(); });
        UIKit.Btn(sw.transform, "Back", new Vector2(240, 62), new Vector2(0, -250), CloseSettings, UIKit.Green);

        // --- Game Over ---
        gameOverMenu = UIKit.FullScreen("GameOver", canvas, new Color(0.15f, 0f, 0f, 0.85f));
        UIKit.Text(gameOverMenu.transform, "GAME OVER", 90, new Vector2(1200, 120), new Vector2(0, 160), TextAlignmentOptions.Center, true, new Color(1f, 0.4f, 0.35f));
        UIKit.Text(gameOverMenu.transform, "Your supermarket ran out of money.", 34, new Vector2(1200, 60), new Vector2(0, 70), TextAlignmentOptions.Center);
        UIKit.Btn(gameOverMenu.transform, "Main Menu", new Vector2(360, 72), new Vector2(0, -50), GameSession.ReloadScene, UIKit.Green, 32);
        UIKit.Btn(gameOverMenu.transform, "Quit", new Vector2(360, 72), new Vector2(0, -140), Quit, UIKit.Red, 32);

        // --- Hộp xác nhận (luôn nằm trên cùng) ---
        confirmMenu = UIKit.FullScreen("Confirm", canvas, new Color(0f, 0f, 0f, 0.6f));
        GameObject cw = UIKit.Panel("Window", confirmMenu.transform, new Vector2(640, 300), Vector2.zero, UIKit.Window);
        confirmText = UIKit.Text(cw.transform, "", 32, new Vector2(600, 150), new Vector2(0, 50), TextAlignmentOptions.Center, true);
        UIKit.Btn(cw.transform, "Yes", new Vector2(200, 64), new Vector2(-120, -90), () => { confirmMenu.SetActive(false); confirmAction?.Invoke(); }, UIKit.Red);
        UIKit.Btn(cw.transform, "No", new Vector2(200, 64), new Vector2(120, -90), () => confirmMenu.SetActive(false));

        HideAll();
    }

    private static TMP_Text SettingRow(Transform parent, string label, float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        UIKit.Text(parent, label, 28, new Vector2(340, 56), new Vector2(-170, y), TextAlignmentOptions.Left);
        UIKit.Btn(parent, "<", new Vector2(60, 56), new Vector2(60, y), minus);
        TMP_Text value = UIKit.Text(parent, "", 28, new Vector2(180, 56), new Vector2(180, y), TextAlignmentOptions.Center, true);
        UIKit.Btn(parent, ">", new Vector2(60, 56), new Vector2(300, y), plus);
        return value;
    }
}
