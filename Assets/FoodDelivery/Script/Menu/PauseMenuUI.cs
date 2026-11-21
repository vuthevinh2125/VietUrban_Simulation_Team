using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveAndQuitButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private AccountMenuController accountMenuController;
    [SerializeField] private string sceneMainMenu;
    [SerializeField] private MonoBehaviour[] disableWhileOpen;
    public static bool IsPaused;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool isOpen;
    public bool IsOpen
    {
        get
        {
            return isOpen;
        }
        private set
        {
            isOpen = value;
        }
    }
    private void Awake()
    {
        IsPaused = false;
    }
    void Start()
    {
        if (accountMenuController == null)
        {
            accountMenuController = GetComponent<AccountMenuController>();
        }
        resumeButton.onClick.AddListener(Close);
        saveAndQuitButton.onClick.AddListener(SaveAndQuit);
        mainMenuButton.onClick.AddListener(BackMainMenu);
        if (accountMenuController != null)
        {
            accountMenuController.CloseProfile();
        }
    }
    void Update()
    {
        if (IsOpen)
        {
            if (EscapePressed())
            {
                Close();
            }
            return;
        }if (EscapePressed())
        {
            if (ShopOpener.AnyOpen || ShopOpener.LastCloseFrame == Time.frameCount)
            {
                return;
            }
            Open();
        }
        
    }
    private void BackMainMenu()
    {
        Time.timeScale = 1f;
        IsOpen = false;
        IsPaused = false;
        SaveSystem.SaveGame(GameSession.Current);
        SceneManager.LoadScene(sceneMainMenu);
    }
    private void SaveAndQuit()
    {
        Time.timeScale = 1f;
        SaveSystem.SaveGame(GameSession.Current);
        Application.Quit();
        Debug.Log("Quit");
    }
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        if (accountMenuController != null)
        {
            accountMenuController.CloseProfile();
        }
        IsOpen = true;
        IsPaused = true;
        previousCursorVisible = Cursor.visible;
        previousLockState = Cursor.lockState;
        pauseMenuRoot.SetActive(true);
        SetControlEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

    }
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        if (accountMenuController != null)
        {
            accountMenuController.CloseProfile();
        }
        IsOpen = false;
        IsPaused = false;
        pauseMenuRoot.SetActive(false);
        SetControlEnabled(true);
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        Time.timeScale = 1f;
        }
    public void SetControlEnabled(bool enabled)
    {
        foreach (MonoBehaviour behaviour in disableWhileOpen)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }
    
    private static bool EscapePressed()
    {
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }
        return false;
    }
}
