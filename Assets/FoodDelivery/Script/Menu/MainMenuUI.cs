using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button NewGameButton;
    [SerializeField] private Button ContinueButton;
    [SerializeField] private Button QuitButton;
    [SerializeField] private TMP_Text statusText;
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
        if (statusText != null)
        {
            statusText.text = "";
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    private void OnNewGameClicked()
    {
        NewGameButton.interactable = false;
        ContinueButton.interactable = false;
        if (statusText != null)
        {
            statusText.text = "saving new game...";
        }
        GameSession.StartNewGame();
        SaveSystem.SaveLocalOnly(GameSession.Current);
        FirebaseCloudSaveService.SaveImmediately(GameSession.Current, delegate(bool saved)
        {
            if (saved)
            {
                SceneManager.LoadScene(GameScenceName);
                return;
            }
            Debug.Log("new game cloud save failed");
            if (statusText != null)
            {
                statusText.text = "cloud save failed. check your connection and retry.";
            }
            NewGameButton.interactable = true;
            ContinueButton.interactable = false;
        });
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
