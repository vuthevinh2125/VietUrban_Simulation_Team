using System;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý ngày/giờ và game over. Tiền KHÔNG còn nằm ở đây nữa mà ở EconomyManager.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Gọi khi sang ngày mới (tham số = số ngày mới).</summary>
    public static event Action<int> OnNewDay;

    [Header("Game Progression")]
    public int currentDay = 1;
    [Tooltip("Một ngày trong game dài bao nhiêu giây ngoài đời")]
    public float realSecondsPerDay = 60f;
    [Tooltip("Giờ mở cửa (0-23)")]
    public int openingHour = 8;
    [Tooltip("Giờ đóng cửa (0-23). Hết giờ là sang ngày mới.")]
    public int closingHour = 18;

    [Header("UI References")]
    public TextMeshProUGUI dayTextUI;
    public TextMeshProUGUI moneyTextUI;
    public TextMeshProUGUI timeTextUI;
    public GameObject gameOverPanel;
    public TextMeshProUGUI interactTextUI;

    [Header("Debug")]
    [Tooltip("Bấm phím này để qua ngày ngay (chỉ để test)")]
    public bool allowSkipDayKey = true;
    public KeyCode skipDayKey = KeyCode.T;

    private static readonly DateTime StartDate = new DateTime(2026, 2, 9);

    private bool isGameOver = false;
    private float dayStartTime;

    public bool IsGameOver => isGameOver;
    public float DayProgress01 => realSecondsPerDay <= 0f ? 0f : Mathf.Clamp01((Time.time - dayStartTime) / realSecondsPerDay);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameManager] Có 2 GameManager trong scene, bỏ bớt 1 cái.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable() => EconomyManager.OnMoneyChanged += RefreshMoney;
    private void OnDisable() => EconomyManager.OnMoneyChanged -= RefreshMoney;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Tự tìm ô hiển thị giờ nếu chưa kéo thả
        if (timeTextUI == null)
        {
            GameObject foundText = GameObject.Find("Text_DateTime");
            if (foundText != null) timeTextUI = foundText.GetComponent<TextMeshProUGUI>();
        }

        dayStartTime = Time.time;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        RefreshMoney(EconomyManager.Instance.Money);
        UpdateUI();
        HideInteractText();
    }

    private void Update()
    {
        if (isGameOver) return;

        bool skipPressed = allowSkipDayKey && !UIState.IsBlockingUIOpen && Input.GetKeyDown(skipDayKey);
        if (Time.time - dayStartTime >= realSecondsPerDay || skipPressed)
        {
            EndDay();
        }

        UpdateClockText();
    }

    // ================= Ngày =================
    private void EndDay()
    {
        EconomyManager economy = EconomyManager.Instance;
        DailySummary summary = economy.CloseDay();

        Debug.Log($"[Day {summary.day}] Income {EconomyManager.Format(summary.income)} | " +
                  $"Expenses {EconomyManager.Format(summary.expenses)} | Profit {EconomyManager.Format(summary.Profit)} | " +
                  $"Balance {EconomyManager.Format(summary.closingBalance)}");

        if (economy.Money < 0f)
        {
            TriggerGameOver();
            return;
        }

        currentDay++;
        dayStartTime = Time.time;
        UpdateUI();
        OnNewDay?.Invoke(currentDay);
    }

    private void TriggerGameOver()
    {
        isGameOver = true;
        Time.timeScale = 0f;
        UIState.Open(this); // hiện chuột để bấm Restart
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Debug.Log("[GameManager] GAME OVER - hết tiền.");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UIState.Close(this);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ================= UI =================
    public void UpdateUI()
    {
        if (dayTextUI != null) dayTextUI.text = "Day: " + currentDay;
        UpdateClockText();
    }

    private void RefreshMoney(float money)
    {
        if (moneyTextUI != null) moneyTextUI.text = "Money: " + EconomyManager.Format(money);
    }

    private void UpdateClockText()
    {
        if (timeTextUI == null) return;

        float openMinutes = openingHour * 60f;
        float workMinutes = Mathf.Max(1, closingHour - openingHour) * 60f;
        int totalMins = Mathf.FloorToInt(openMinutes + DayProgress01 * workMinutes) % 1440;

        int hour24 = totalMins / 60;
        int minute = totalMins % 60;
        string ampm = hour24 >= 12 ? "PM" : "AM";
        int hour12 = hour24 % 12;
        if (hour12 == 0) hour12 = 12;

        string dateString = StartDate.AddDays(currentDay - 1).ToString("yyyy.MM.dd");
        timeTextUI.text = $"{hour12:00}:{minute:00} {ampm}\n{dateString}";
    }

    public void ShowInteractText(string message)
    {
        if (interactTextUI == null) return;
        interactTextUI.text = message;
        if (!interactTextUI.gameObject.activeSelf) interactTextUI.gameObject.SetActive(true);
    }

    public void HideInteractText()
    {
        if (interactTextUI != null && interactTextUI.gameObject.activeSelf)
            interactTextUI.gameObject.SetActive(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnNewDay = null;
    }
}
