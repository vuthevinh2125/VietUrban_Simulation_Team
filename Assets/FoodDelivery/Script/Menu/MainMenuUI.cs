using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button NewGameButton;
    [SerializeField] private Button ContinueButton;
    [SerializeField] private Button QuitButton;
    [SerializeField] private string GameScenceName;
    void Start()
    {
        NewGameButton.onClick.AddListener(OnNewGameClicked);
        ContinueButton.onClick.AddListener(OnContinueClicked);
        QuitButton.onClick.AddListener(OnQuitClicked);
        if (SaveSystem.HasSave() == false)
        {
            ContinueButton.gameObject.SetActive(false);
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    private void OnNewGameClicked()
    {
        GameSession.StartNewGame();
        SceneManager.LoadScene(GameScenceName);
    }
    private void OnContinueClicked()
    {
        SceneManager.LoadScene(GameScenceName);
    }
    private void OnQuitClicked()
    {
        Application.Quit();
        Debug.Log("quit");
    }
}
