using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hướng dẫn ngày đầu (chỉ khi bấm New Game): hiện từng bước ở góc phải màn hình,
/// làm xong bước nào thì tự chuyển bước tiếp theo. [H] ẩn / hiện, có thể bỏ qua.
///  1. Mở máy tính, đặt mua hàng  2. Nhặt thùng  3. Xếp lên kệ  4. Đặt giá
///  5. Lật biển OPEN  6. Quét hàng + nhận tiền khách đầu tiên
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private struct Step
    {
        public string title, text;
        public Func<bool> done;
    }

    private Step[] steps;
    private int index = -1;
    private bool hidden;
    private GameObject panel;
    private TMP_Text titleText, bodyText, progressText;

    // Mốc ban đầu để biết người chơi đã làm bước đó chưa
    private int startBoxes, startShelfItems, startCustomers;
    private bool priceChanged;
    private ItemPickup hands;

    public bool IsRunning => index >= 0 && steps != null && index < steps.Length;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Create();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Create();
    private static void Create() { if (Instance == null) new GameObject("TutorialManager").AddComponent<TutorialManager>(); }

    void Awake()
    {
        Instance = this;
        Build();
        panel.SetActive(false);
        PriceManager.OnPriceChanged += OnPrice;
    }

    void OnDestroy()
    {
        PriceManager.OnPriceChanged -= OnPrice;
        if (Instance == this) Instance = null;
    }

    private void OnPrice(ItemData item, float price) => priceChanged = true;

    /// <summary>Bắt đầu hướng dẫn (MenuSystem gọi khi New Game).</summary>
    public void Begin()
    {
        hands = null;
        foreach (ItemPickup ip in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
            if (ip.playerCamera != null) { hands = ip; break; }

        startBoxes = CountBoxes();
        startShelfItems = CountShelfItems();
        startCustomers = EconomyManager.Instance != null ? EconomyManager.Instance.Today.customersServed : 0;
        priceChanged = false;

        steps = new[]
        {
            new Step { title = "Order products", text = "Walk to the <b>computer</b> at the counter and press <b>E</b>.\nAdd a product to the cart and press <b>Buy</b>.",
                       done = () => CountBoxes() > startBoxes },
            new Step { title = "Pick up the box", text = "Your boxes are delivered <b>outside the store</b>.\nLook at a box and press <b>E</b> to pick it up.",
                       done = () => hands != null && hands.HeldBox != null },
            new Step { title = "Stock a shelf", text = "Carry the box to an empty shelf.\nHold <b>Left Mouse</b> to place products. Press <b>E</b> to drop the box.",
                       done = () => CountShelfItems() > startShelfItems },
            new Step { title = "Set the price", text = "With empty hands, look at the shelf and click <b>Left Mouse</b>.\nSet a price close to the <b>market price</b> and press <b>Confirm</b>.",
                       done = () => priceChanged },
            new Step { title = "Open the store", text = "Find the <b>OPEN / CLOSED</b> sign near the entrance and click it.\nCustomers will start coming in.",
                       done = () => GameManager.Instance != null && GameManager.Instance.IsStoreOpen },
            new Step { title = "Serve a customer", text = "When a customer is at the counter, click each item to <b>scan</b> it.\nThen click their <b>cash or card</b> and finish the payment.",
                       done = () => EconomyManager.Instance != null && EconomyManager.Instance.Today.customersServed > startCustomers },
        };
        index = 0;
        hidden = false;
        Show();
    }

    public void Stop()
    {
        index = -1;
        panel.SetActive(false);
    }

    void Update()
    {
        if (!IsRunning) return;
        if (Input.GetKeyDown(KeyCode.H) && !UIState.IsBlockingUIOpen) { hidden = !hidden; Show(); }

        if (steps[index].done())
        {
            AudioManager.Play("level_up", 0.5f);
            index++;
            if (index >= steps.Length)
            {
                ProgressHUD.Toast("Tutorial complete! Keep your shelves stocked\nand grow your supermarket.");
                Stop();
                return;
            }
            hidden = false;
            Show();
        }
        // Ẩn khi đang mở menu chính để không đè lên
        panel.SetActive(!hidden && (MenuSystem.Instance == null || !MenuSystem.Instance.IsMainMenuOpen));
    }

    private void Show()
    {
        if (!IsRunning) return;
        titleText.text = steps[index].title;
        bodyText.text = steps[index].text;
        progressText.text = $"TUTORIAL  {index + 1}/{steps.Length}      <size=80%>[H] hide</size>";
        panel.SetActive(!hidden);
    }

    // ================= Đếm =================
    private static int CountBoxes()
    {
        int n = 0;
        foreach (Interactable b in StockManager.Boxes) if (b != null && !b.IsEmpty) n++;
        return n;
    }

    private static int CountShelfItems()
    {
        int n = 0;
        foreach (Shelf s in StockManager.Shelves) if (s != null) n += s.Count;
        return n;
    }

    // ================= Giao diện =================
    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 60).transform;
        panel = UIKit.Panel("Tutorial", canvas, new Vector2(520, 210), Vector2.zero, new Color(0.04f, 0.2f, 0.35f, 0.88f));
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-20f, 120f);
        progressText = UIKit.Text(panel.transform, "", 20, new Vector2(490, 30), new Vector2(0, 80), TextAlignmentOptions.Left, true, new Color(0.6f, 0.85f, 1f));
        titleText = UIKit.Text(panel.transform, "", 32, new Vector2(490, 44), new Vector2(0, 45), TextAlignmentOptions.Left, true);
        bodyText = UIKit.Text(panel.transform, "", 23, new Vector2(490, 110), new Vector2(0, -35), TextAlignmentOptions.TopLeft);
        // Cho chữ không bị cắt
        bodyText.textWrappingMode = TextWrappingModes.Normal;
    }
}
