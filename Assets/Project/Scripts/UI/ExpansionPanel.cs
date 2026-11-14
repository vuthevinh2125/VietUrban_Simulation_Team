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

    private GameObject root;
    private Transform canvasT;
    private TMP_Text rentText, rentNote, roomsText;
    private Image roomsBar;
    private class Room
    {
        public StoreExpansion e;
        public GameObject solid, dashedYes, dashedNo, glow;
        public TMP_Text name, desc, state;
        public Button buy;
        public CanvasGroup group;
    }
    private readonly List<Room> rows = new List<Room>();
    private int builtCount = -1;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("ExpansionPanel").AddComponent<ExpansionPanel>();
        instance.Show();
    }

    void Awake()
    {
        canvasT = UIKit.CreateCanvas(transform, 550).transform;
        Build();
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
        EconomyManager em = EconomyManager.Instance;
        rentText.text = EconomyManager.Format(em != null ? em.CurrentRent : 0f);
        rentNote.text = em != null ? $"+ {EconomyManager.Format(em.rentPerExpansion)} for each new room" : "";
        int built = 0;
        foreach (Room r in rows)
        {
            if (r.e == null) continue;
            bool owned = r.e.IsUnlocked;
            bool can = r.e.CanBuy(out string reason);
            if (owned) built++;
            r.solid.SetActive(owned);
            r.dashedYes.SetActive(!owned && can);
            r.glow.SetActive(!owned && can);
            r.dashedNo.SetActive(!owned && !can);
            r.name.color = owned ? Color.white : can ? ShopPC.Yellow : ShopPC.A(Color.white, 0.75f);
            r.group.alpha = owned || can ? 1f : 0.8f;
            r.state.text = owned ? "built · owned"
                : can ? $"{EconomyManager.Format(r.e.price)} · adds to the daily rent"
                : StoreProgress.Level < r.e.requiredLevel ? $"needs store Lv {r.e.requiredLevel} · {EconomyManager.Format(r.e.price)}" : reason;
            r.buy.gameObject.SetActive(!owned && can);
        }
        roomsText.text = $"{built} / {rows.Count}";
        roomsBar.fillAmount = rows.Count > 0 ? (float)built / rows.Count : 0f;
    }

    // ================= Dựng giao diện (bản thiết kế 07: bản vẽ mặt bằng) =================
    private void Build()
    {
        if (root != null) { root.SetActive(false); Destroy(root); }
        rows.Clear();
        builtCount = StoreExpansion.All.Count;
        RectTransform c = ShopPC.Computer(canvasT, "EXPAND", Close, out root);

        // Bản vẽ xanh có lưới
        const float planW = 1368f;
        float planH = ShopPC.ContentSize.y;
        RectTransform plan = ShopPC.Sheet("FloorPlan", c, 0f, 0f, planW, planH, ShopUI.Enamel);
        plan.gameObject.AddComponent<RectMask2D>();
        Color grid = ShopPC.A(Color.white, 0.14f);
        for (float gx = 36f; gx < planW; gx += 36f) ShopPC.Box("GridV", plan, gx, 0f, 1.5f, planH, grid);
        for (float gy = 36f; gy < planH; gy += 36f) ShopPC.Box("GridH", plan, 0f, gy, planW, 1.5f, grid);
        ShopPC.Text(plan, "MẶT BẰNG · FLOOR PLAN 1:100", ShopUI.Print, 33, 33f, 21f, 900f, 46f, Color.white, TextAlignmentOptions.Left, 4.5f);

        // Gian chính
        RectTransform hall = ShopPC.Group("MainHall", plan, 90f, 105f, 630f, 450f);
        ShopUI.Frame(hall, hall.sizeDelta, Color.white, 4.5f);
        ShopPC.Text(hall, "MAIN HALL", ShopUI.Print, 30, 18f, 18f, 500f, 40f, Color.white, TextAlignmentOptions.Left, 3f);
        ShopPC.Text(hall, "Shelves and checkout · owned", ShopUI.Body, 18, 18f, 58f, 560f, 26f, Color.white, TextAlignmentOptions.Left);
        for (int i = 0; i < 9; i++) ShopPC.Box("Shelf", hall, 30f + i * 64f, 120f, 30f, 225f, ShopPC.A(Color.white, 0.28f));
        ShopPC.Box("Door", plan, 300f, 93f, 135f, 24f, ShopUI.Enamel);
        ShopPC.Box("DoorL", plan, 300f, 93f, 4.5f, 24f, Color.white);
        ShopPC.Box("DoorR", plan, 430.5f, 93f, 4.5f, 24f, Color.white);
        ShopPC.Text(plan, "ENTRANCE · HAI BÀ TRƯNG", ShopUI.Body, 16, 294f, 60f, 400f, 24f, Color.white, TextAlignmentOptions.Left, 0f, true);

        // Các phòng mở rộng
        float[,] slots = { { 720f, 105f, 375f, 450f }, { 90f, 555f, 450f, 285f }, { 540f, 555f, 555f, 285f }, { 1095f, 105f, 240f, 450f }, { 1095f, 555f, 240f, 285f } };
        int k = 0;
        if (builtCount == 0)
            ShopPC.Text(plan, "No expansions yet.\nRun Tools > Store Setup > 15 / 16 in the editor.", ShopUI.Body, 22, 720f, 200f, 600f, 90f, Color.white, TextAlignmentOptions.Left);
        foreach (StoreExpansion e in StoreExpansion.All)
        {
            if (e == null) continue;
            int si = Mathf.Min(k, slots.GetLength(0) - 1);
            float x = slots[si, 0], y = slots[si, 1], w = slots[si, 2], h = slots[si, 3];
            var r = new Room { e = e };
            RectTransform room = ShopPC.Group("Room_" + e.id, plan, x, y, w, h);
            r.group = room.gameObject.AddComponent<CanvasGroup>();
            RectTransform glow = ShopPC.Box("Glow", room, 0f, 0f, w, h, ShopPC.A(ShopPC.Yellow, 0.14f));
            glow.gameObject.AddComponent<AlphaPulse>();
            r.glow = glow.gameObject;
            RectTransform solid = ShopUI.Group("Solid", room, room.sizeDelta, Vector2.zero);
            ShopUI.Frame(solid, room.sizeDelta, Color.white, 4.5f);
            RectTransform yes = ShopUI.Group("DashYes", room, room.sizeDelta, Vector2.zero);
            ShopPC.DashedFrame(yes, room.sizeDelta, ShopPC.Yellow, 4.5f);
            RectTransform no = ShopUI.Group("DashNo", room, room.sizeDelta, Vector2.zero);
            ShopPC.DashedFrame(no, room.sizeDelta, ShopPC.A(Color.white, 0.55f), 4.5f);
            r.solid = solid.gameObject; r.dashedYes = yes.gameObject; r.dashedNo = no.gameObject;
            r.name = ShopPC.Text(room, e.displayName.ToUpperInvariant(), ShopUI.Print, 30, 18f, 18f, w - 36f, 40f, Color.white, TextAlignmentOptions.Left, 3f);
            r.desc = ShopPC.Text(room, e.description, ShopUI.Body, 17, 18f, 60f, w - 36f, h - 60f - 100f, ShopPC.A(Color.white, 0.85f), TextAlignmentOptions.TopLeft);
            r.state = ShopPC.Text(room, "", ShopUI.Body, 16, 18f, h - 96f, w - 36f, 26f, Color.white, TextAlignmentOptions.Left, 0f, true);
            StoreExpansion captured = e;
            r.buy = ShopPC.Btn(room, "BUILD", 18f, h - 66f, 150f, 50f, () => captured.TryBuy(), ShopPC.Kind.Red, 21f);
            rows.Add(r);
            k++;
        }
        ShopPC.Text(plan, "Yellow dashed rooms can be built now. Every new room adds to the rent paid each night.", ShopUI.Body, 19, 720f, planH - 70f, 620f, 60f,
            ShopPC.A(Color.white, 0.85f), TextAlignmentOptions.TopLeft);

        // Cột phải
        const float rx = planW + 30f, rw = 450f;
        RectTransform rent = ShopPC.Sheet("Rent", c, rx, 0f, rw, 165f, ShopPC.Paper, 0f, 24f);
        ShopPC.Text(rent, "RENT · PAID EVERY NIGHT", ShopUI.Body, 15, 24f, 21f, rw - 48f, 22f, ShopPC.Red, TextAlignmentOptions.Left, 3f, true);
        rentText = ShopPC.Text(rent, "", ShopUI.Print, 51, 24f, 45f, rw - 48f, 66f, ShopPC.Ink, TextAlignmentOptions.Left);
        rentNote = ShopPC.Text(rent, "", ShopUI.Hand, 30, 24f, 110f, rw - 48f, 40f, ShopPC.Pen, TextAlignmentOptions.Left);

        RectTransform rooms = ShopPC.Sheet("Rooms", c, rx, 195f, rw, 150f, ShopPC.Paper, 0f, 24f);
        ShopPC.Text(rooms, "ROOMS BUILT", ShopUI.Body, 15, 24f, 21f, rw - 48f, 22f, ShopPC.Red, TextAlignmentOptions.Left, 3f, true);
        roomsText = ShopPC.Text(rooms, "", ShopUI.Print, 51, 24f, 45f, rw - 48f, 66f, ShopPC.Ink, TextAlignmentOptions.Left);
        roomsBar = ShopPC.HBar(rooms, ShopPC.At(rooms.sizeDelta, 24f, 120f, 0f, 12f), rw - 48f, 12f, ShopPC.A(ShopPC.Ink, 0.12f), ShopPC.Red);

        ShopPC.StickyNote(c, "Do not build before the shelves are full. Empty rooms still cost rent. - bà Lan", rx, 384f, rw, 170f, -1.5f, 30f);
        root.SetActive(false);
    }
}
