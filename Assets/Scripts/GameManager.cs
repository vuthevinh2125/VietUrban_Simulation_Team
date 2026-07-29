using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Progression")]
    public int currentDay = 1;
    public float dayDuration = 10f;
    private float currentTime;

    [Header("Tài chính")]
    public int currentMoney = 10000;
    public int dailyCost = 1500;

    [Header("Giao diện UI")]
    public TextMeshProUGUI dayTextUI;
    public TextMeshProUGUI moneyTextUI;
    public TextMeshProUGUI timeTextUI;
    public GameObject gameOverPanel;

    // ĐÂY LÀ BIẾN MỚI ĐỂ CHỨA DÒNG CHỮ "PRESS E"
    public TextMeshProUGUI interactTextUI;

    private bool isGameOver = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentTime = dayDuration;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateUI();

        // Tắt dòng chữ Press E lúc mới vào game
        HideInteractText();
    }

    private void Update()
    {
        if (isGameOver) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0 || Input.GetKeyDown(KeyCode.T))
        {
            NextDay();
        }

        UpdateUI();
    }

    private void NextDay()
    {
        currentDay++;
        currentMoney -= dailyCost;
        currentTime = dayDuration;

        if (currentMoney < 0)
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        isGameOver = true;
        Time.timeScale = 0f;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void UpdateUI()
    {
        if (dayTextUI != null) dayTextUI.text = "Day: " + currentDay + ".";
        if (moneyTextUI != null) moneyTextUI.text = "Money: " + currentMoney + "VND";

        if (timeTextUI != null)
        {
            timeTextUI.text = "Time: " + Mathf.Ceil(currentTime).ToString() + "s";
        }
    }

    // --- 2 HÀM MỚI ĐỂ BẬT/TẮT CHỮ UI ---
    public void ShowInteractText(string message)
    {
        if (interactTextUI != null)
        {
            interactTextUI.text = message;
            interactTextUI.gameObject.SetActive(true);
        }
    }

    public void HideInteractText()
    {
        if (interactTextUI != null)
        {
            interactTextUI.gameObject.SetActive(false);
        }
    }
}