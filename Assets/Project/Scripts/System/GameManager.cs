using System;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Ngày làm việc kiểu Supermarket Simulator:
///  - Đầu ngày cửa hàng ĐÓNG, đồng hồ đứng ở giờ mở cửa. Người chơi chuẩn bị (đặt hàng, xếp kệ, đặt giá).
///  - Lật biển sang OPEN thì đồng hồ chạy và khách bắt đầu tới. Lật lại CLOSED thì ngừng đón khách, đồng hồ dừng.
///  - Tới giờ đóng cửa: tự đóng, không đón khách mới; khách cuối cùng ra về thì hiện báo cáo cuối ngày.
///  - Bấm "Next Day" trên báo cáo: trừ tiền thuê đã tính, sang ngày mới (cửa hàng lại đóng).
/// Tiền nằm ở EconomyManager.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Gọi khi sang ngày mới (tham số = số ngày mới).</summary>
    public static event Action<int> OnNewDay;
    /// <summary>Cửa hàng mở (true) / đóng (false).</summary>
    public static event Action<bool> OnStoreOpenChanged;

    [Header("Game Progression")]
    public int currentDay = 1;
    [Tooltip("Một ngày làm việc (từ giờ mở tới giờ đóng cửa) dài bao nhiêu PHÚT ngoài đời khi cửa hàng mở")]
    public float realMinutesPerDay = 10f;
    [HideInInspector] public float realSecondsPerDay = 60f; // không còn dùng, giữ để không mất dữ liệu cũ
    [Tooltip("Giờ mở cửa (0-23)")]
    public int openingHour = 8;
    [Tooltip("Giờ đóng cửa (0-23)")]
    public int closingHour = 22;

    [Header("UI References")]
    public TextMeshProUGUI dayTextUI;
    public TextMeshProUGUI moneyTextUI;
    public TextMeshProUGUI timeTextUI;
    public GameObject gameOverPanel;
    public TextMeshProUGUI interactTextUI;

    [Header("Debug")]
    [Tooltip("Bấm phím này để nhảy tới giờ đóng cửa (chỉ để test)")]
    public bool allowSkipDayKey = true;
    public KeyCode skipDayKey = KeyCode.T;
    [Tooltip("Khi scene không có biển Open/Closed: bấm phím này để mở / đóng cửa hàng")]
    public KeyCode toggleStoreKey = KeyCode.O;

    private static readonly DateTime StartDate = new DateTime(2026, 2, 9);

    private bool isGameOver;
    private bool storeOpen;
    private bool closingTimeReached;
    private bool dayEnded;
    private float clockMinutes;
    private float closingSince;

    [Tooltip("Hết giờ mà khách vẫn kẹt trong cửa hàng quá số giây này thì cho khách biến mất và chốt ngày")]
    public float maxWaitAfterClosing = 120f;

    public bool IsGameOver => isGameOver;
    public bool IsStoreOpen => storeOpen;
    /// <summary>Đã tới giờ đóng cửa, đang chờ khách cuối ra về.</summary>
    public bool IsClosingTime => closingTimeReached;
    private float OpenMinutes => openingHour * 60f;
    private float WorkMinutes => Mathf.Max(1, closingHour - openingHour) * 60f;
    public float DayProgress01 => Mathf.Clamp01((clockMinutes - OpenMinutes) / WorkMinutes);

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
        if (timeTextUI == null)
        {
            GameObject foundText = GameObject.Find("Text_DateTime");
            if (foundText != null) timeTextUI = foundText.GetComponent<TextMeshProUGUI>();
        }

        clockMinutes = OpenMinutes;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        RefreshMoney(EconomyManager.Instance.Money);
        UpdateUI();
        HideInteractText();
    }

    private void Update()
    {
        if (isGameOver || dayEnded) return;
        bool uiFree = !UIState.IsBlockingUIOpen;

        if (uiFree && Input.GetKeyDown(toggleStoreKey) && StoreSign.All.Count == 0) ToggleStore();

        if (uiFree && allowSkipDayKey && Input.GetKeyDown(skipDayKey) && !closingTimeReached)
        {
            clockMinutes = OpenMinutes + WorkMinutes;
            Debug.Log("[GameManager] (Test) Nhảy tới giờ đóng cửa.");
        }

        // Đồng hồ chỉ chạy khi cửa hàng đang mở
        if (storeOpen)
            clockMinutes += Time.deltaTime * WorkMinutes / Mathf.Max(1f, realMinutesPerDay * 60f);

        if (!closingTimeReached && clockMinutes >= OpenMinutes + WorkMinutes)
        {
            clockMinutes = OpenMinutes + WorkMinutes;
            closingTimeReached = true;
            closingSince = Time.time;
            SetStoreOpen(false, force: true);
            Debug.Log("[GameManager] Tới giờ đóng cửa. Phục vụ nốt khách trong cửa hàng.");
        }

        // Hết giờ và khách đã về hết -> báo cáo cuối ngày
        if (closingTimeReached && NPCShopper.Active.Count > 0 && Time.time - closingSince > maxWaitAfterClosing)
        {
            Debug.LogWarning($"[GameManager] {NPCShopper.Active.Count} khách kẹt quá lâu sau giờ đóng cửa, cho về luôn.");
            for (int i = NPCShopper.Active.Count - 1; i >= 0; i--)
                if (NPCShopper.Active[i] != null) Destroy(NPCShopper.Active[i].gameObject);
        }
        if (closingTimeReached && NPCShopper.Active.Count == 0 && !UIState.IsBlockingUIOpen) EndDay();

        UpdateClockText();
    }

    // ================= Mở / đóng cửa hàng =================
    public void ToggleStore() => SetStoreOpen(!storeOpen);

    public bool CanToggleStore => !closingTimeReached && !dayEnded && !isGameOver;

    public void SetStoreOpen(bool open, bool force = false)
    {
        if (!force && !CanToggleStore) return;
        if (storeOpen == open) return;
        storeOpen = open;
        Debug.Log(open ? "[GameManager] Cửa hàng MỞ CỬA." : "[GameManager] Cửa hàng ĐÓNG CỬA.");
        OnStoreOpenChanged?.Invoke(open);
        UpdateClockText();
    }

    /// <summary>Nạp ngày từ file lưu: bắt đầu ngày đó từ đầu (giờ mở cửa, cửa hàng đóng).</summary>
    public void LoadDay(int day)
    {
        currentDay = Mathf.Max(1, day);
        clockMinutes = OpenMinutes;
        closingTimeReached = false;
        dayEnded = false;
        SetStoreOpen(false, force: true);
        UpdateUI();
    }

    // ================= Ngày =================
    private void EndDay()
    {
        dayEnded = true;
        if (StaffManager.Instance != null) StaffManager.Instance.PayWages(); // lương nhân viên tính vào ngày hôm nay
        DailySummary summary = EconomyManager.Instance.CloseDay();
        Debug.Log($"[Day {summary.day}] Income {EconomyManager.Format(summary.income)} | " +
                  $"Expenses {EconomyManager.Format(summary.expenses)} | Profit {EconomyManager.Format(summary.Profit)} | " +
                  $"Balance {EconomyManager.Format(summary.closingBalance)}");
        DayReportPanel.Show(summary, StartNextDay);
    }

    private void StartNextDay()
    {
        if (EconomyManager.Instance.Money < 0f)
        {
            TriggerGameOver();
            return;
        }

        currentDay++;
        clockMinutes = OpenMinutes;
        closingTimeReached = false;
        dayEnded = false;
        SetStoreOpen(false, force: true);
        MarketPrices.RollNewDay(); // giá thị trường đổi mỗi sáng
        ProgressHUD.Toast("A new day! Market prices have changed.");
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

    public string ClockString
    {
        get
        {
            int totalMins = Mathf.FloorToInt(clockMinutes) % 1440;
            int hour24 = totalMins / 60, minute = totalMins % 60;
            string ampm = hour24 >= 12 ? "PM" : "AM";
            int hour12 = hour24 % 12;
            if (hour12 == 0) hour12 = 12;
            return $"{hour12:00}:{minute:00} {ampm}";
        }
    }

    private void UpdateClockText()
    {
        if (timeTextUI == null) return;
        string dateString = StartDate.AddDays(currentDay - 1).ToString("yyyy.MM.dd");
        string status = storeOpen ? "<color=#5BE36B>OPEN</color>" : (closingTimeReached ? "<color=#FFB347>CLOSING</color>" : "<color=#FF6B6B>CLOSED</color>");
        timeTextUI.text = $"{ClockString}  {status}\n{dateString}";
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
        OnStoreOpenChanged = null;
    }
}
