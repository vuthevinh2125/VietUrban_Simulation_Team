using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tab MỞ RỘNG trên máy tính: mua phòng mở rộng siêu thị, nhà kho... Esc / X để đóng.</summary>
public class ExpansionPanel : MonoBehaviour
{
    private static ExpansionPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject root, window;
    private TMP_Text info;
    private readonly List<(StoreExpansion e, TMP_Text status, Button buy)> rows = new List<(StoreExpansion, TMP_Text, Button)>();
    private int builtCount = -1;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("ExpansionPanel").AddComponent<ExpansionPanel>();
        instance.Show();
    }

    void Awake()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        root = UIKit.FullScreen("Root", canvas, new Color(0f, 0f, 0f, 0.45f));
        root.SetActive(false);
        EconomyManager.OnMoneyChanged += OnMoney;
        StoreProgress.OnChanged += Refresh;
        StoreExpansion.OnChanged += Refresh;
    }

    void OnDestroy()
    {
        EconomyManager.OnMoneyChanged -= OnMoney;
        StoreProgress.OnChanged -= Refresh;
        StoreExpansion.OnChanged -= Refresh;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnMoney(float _) => Refresh();

    private void Show()
    {
        if (builtCount != StoreExpansion.All.Count) Build();
        root.SetActive(true);
        UIState.Open(this);
        Refresh();
    }

    private void Close()
    {
        ClosedFrame = Time.frameCount;
        root.SetActive(false);
        UIState.Close(this);
    }

    private void Refresh()
    {
        if (root == null || !root.activeSelf) return;
        info.text = $"Store level {StoreProgress.Level}   |   Money {EconomyManager.Format(EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0f)}";
        foreach (var r in rows)
        {
            if (r.e == null) continue;
            bool owned = r.e.IsUnlocked;
            bool can = r.e.CanBuy(out string reason);
            r.status.text = owned ? "<color=#6BE37B>OWNED</color>" : (can ? EconomyManager.Format(r.e.price) : $"<color=#FF8A7A>{reason}</color>");
            r.buy.gameObject.SetActive(!owned);
            r.buy.interactable = can;
        }
    }

    private void Build()
    {
        if (window != null) Destroy(window);
        rows.Clear();
        builtCount = StoreExpansion.All.Count;
        int n = Mathf.Max(1, builtCount);
        float rowH = 90f, gap = 10f, h = 200 + n * (rowH + gap);
        window = UIKit.Panel("Window", root.transform, new Vector2(960, h), Vector2.zero, UIKit.Window);
        GameObject header = UIKit.Panel("Header", window.transform, new Vector2(960, 70), new Vector2(0, h / 2 - 35), UIKit.Header);
        UIKit.Text(header.transform, "EXPAND YOUR STORE", 34, new Vector2(900, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        UIKit.Btn(header.transform, "X", new Vector2(50, 50), new Vector2(445, 0), Close, new Color(0.9f, 0.2f, 0.2f));
        info = UIKit.Text(window.transform, "", 24, new Vector2(900, 36), new Vector2(0, h / 2 - 95), TextAlignmentOptions.Center);

        float y = h / 2 - 155;
        if (builtCount == 0)
            UIKit.Text(window.transform, "No expansions yet.\nRun Tools > Store Setup > 15 / 16 in the editor.", 26, new Vector2(900, 80), new Vector2(0, y - 10));
        foreach (StoreExpansion e in StoreExpansion.All)
        {
            if (e == null) continue;
            GameObject row = UIKit.Panel("Row", window.transform, new Vector2(900, rowH), new Vector2(0, y), new Color(0.2f, 0.24f, 0.3f));
            UIKit.Text(row.transform, $"<b>{e.displayName}</b>", 28, new Vector2(520, 40), new Vector2(-170, 18), TextAlignmentOptions.Left);
            UIKit.Text(row.transform, $"<size=75%>{e.description}  ·  Lv {e.requiredLevel}</size>", 26, new Vector2(520, 40), new Vector2(-170, -20), TextAlignmentOptions.Left);
            TMP_Text status = UIKit.Text(row.transform, "", 24, new Vector2(200, rowH), new Vector2(210, 0), TextAlignmentOptions.Center, true);
            StoreExpansion captured = e;
            Button buy = UIKit.Btn(row.transform, "Buy", new Vector2(110, 50), new Vector2(375, 0), () => captured.TryBuy(), UIKit.Green);
            rows.Add((e, status, buy));
            y -= rowH + gap;
        }
    }
}
