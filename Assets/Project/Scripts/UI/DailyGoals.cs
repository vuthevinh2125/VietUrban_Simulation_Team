using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giấy nhớ "VIỆC HÔM NAY · TO DO" trên HUD (bản thiết kế HUD): 2-3 việc trong ngày lấy từ số liệu thật,
/// tick khi xong (chữ gạch ngang). Chỉ là mục tiêu để người chơi có hướng, không cộng thưởng nên không đổi cân bằng game.
///  - Phục vụ N khách (N = 110% số khách hôm qua, tối thiểu 10)
///  - Giữ điểm đánh giá hôm nay từ 4.0 trở lên (khi đã có đánh giá)
///  - Giao 2 đơn hàng (khi đã mở khoá giao hàng)
/// </summary>
public class DailyGoals : MonoBehaviour
{
    private static DailyGoals instance;
    private GameObject root;
    private RectTransform note;
    private readonly TMP_Text[] rows = new TMP_Text[3];
    private readonly Image[] boxes = new Image[3];
    private readonly GameObject[] rowGo = new GameObject[3];
    private int day = -1, customerGoal = 10;
    private float nextRefresh;
    private const int DeliveryGoal = 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance == null) instance = new GameObject("DailyGoals").AddComponent<DailyGoals>();
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Transform canvas = UIKit.CreateCanvas(transform, 49).transform;
        RectTransform anchor = ShopUI.Group("GoalsAnchor", canvas, new Vector2(330f, 170f), Vector2.zero);
        anchor.anchorMin = anchor.anchorMax = new Vector2(0f, 1f);
        anchor.pivot = new Vector2(0f, 1f);
        anchor.anchoredPosition = new Vector2(14f, -205f);
        root = anchor.gameObject;
        note = ShopUI.Sheet("TodoNote", anchor, new Vector2(320f, 160f), new Vector2(0f, 0f), ShopPC.Note, -1.2f);
        ShopUI.Tape(note, -80f, -5f);
        ShopUI.Text(note, "VIỆC HÔM NAY · TO DO", ShopUI.Print, 21, new Vector2(290f, 30f), new Vector2(0f, 56f), ShopPC.Red, TextAlignmentOptions.Left, 0f, 3f)
            .textWrappingMode = TextWrappingModes.NoWrap;
        for (int i = 0; i < 3; i++)
        {
            float y = 22f - i * 36f;
            RectTransform row = ShopUI.Group("Row" + i, note, new Vector2(290f, 34f), new Vector2(0f, y));
            RectTransform box = ShopUI.Box("Check", row, new Vector2(18f, 18f), new Vector2(-136f, 0f), new Color(0f, 0f, 0f, 0f));
            ShopUI.Frame(box, box.sizeDelta, ShopPC.Pen, 2f);
            RectTransform fill = ShopUI.Box("Fill", box, new Vector2(10f, 10f), Vector2.zero, ShopPC.Red);
            boxes[i] = fill.GetComponent<Image>();
            rows[i] = ShopUI.Text(row, "", ShopUI.Hand, 24, new Vector2(262f, 34f), new Vector2(15f, 0f), ShopPC.Pen, TextAlignmentOptions.Left);
            rows[i].textWrappingMode = TextWrappingModes.NoWrap;
            rows[i].enableAutoSizing = true; rows[i].fontSizeMin = 16; rows[i].fontSizeMax = 24;
            rowGo[i] = row.gameObject;
        }
        UIMotion m = note.gameObject.AddComponent<UIMotion>();
        m.swayDegrees = 0.5f; m.swaySpeed = 0.9f;
        root.SetActive(false);
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        EconomyManager em = EconomyManager.Instance;
        bool show = gm != null && em != null && em.Today != null && !UIState.IsBlockingUIOpen;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;

        if (gm.currentDay != day)
        {
            day = gm.currentDay;
            var days = BusinessStats.Days;
            int yesterday = days.Count > 0 ? days[days.Count - 1].customers : 0;
            customerGoal = Mathf.Max(10, Mathf.CeilToInt(yesterday * 1.1f));
        }

        int served = em.Today.customersServed;
        int i = 0;
        SetRow(i++, $"Serve {customerGoal} customers - {Mathf.Min(served, customerGoal)}", served >= customerGoal);

        ReviewManager.ForDay(day, out float rating, out int count);
        string r = count > 0 ? rating.ToString("0.0", CultureInfo.InvariantCulture) : "no reviews yet";
        SetRow(i++, $"Rating 4.0 or more ({r})", count > 0 && rating >= 4f);

        DeliveryManager dm = DeliveryManager.Instance;
        if (dm != null && StoreProgress.Level >= dm.requiredLevel)
        {
            int done = BusinessStats.TodayDeliveries;
            SetRow(i++, $"{DeliveryGoal} deliveries - {Mathf.Min(done, DeliveryGoal)} done", done >= DeliveryGoal);
        }
        for (int k = i; k < 3; k++) rowGo[k].SetActive(false);
    }

    private void SetRow(int i, string text, bool done)
    {
        rowGo[i].SetActive(true);
        rows[i].text = done ? $"<s>{text}</s>" : text;
        rows[i].color = done ? ShopPC.A(ShopPC.Pen, 0.55f) : ShopPC.Pen;
        boxes[i].enabled = done;
    }
}
