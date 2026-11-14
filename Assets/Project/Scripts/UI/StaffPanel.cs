using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Tab NHÂN VIÊN trên máy tính: thuê / cho nghỉ thu ngân, nhân viên xếp kệ, bảo vệ, nhân viên lau nhà. Esc / X để đóng.</summary>
public class StaffPanel : MonoBehaviour
{
    private static StaffPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject root;
    private TMP_Text headerInfo;
    private Button unlockCounter;
    private TMP_Text counterStatus;


    public static void Open()
    {
        if (instance == null) instance = new GameObject("StaffPanel").AddComponent<StaffPanel>();
        instance.root.SetActive(true);
        UIState.Open(instance);
        instance.Refresh();
    }

    void Awake()
    {
        Build();
        StaffManager.OnStaffChanged += Refresh;
        StoreProgress.OnChanged += Refresh;
        LockedCheckout.OnChanged += Refresh;
        EconomyManager.OnMoneyChanged += OnMoney;
    }

    private void OnMoney(float _) => Refresh();

    void OnDestroy()
    {
        StaffManager.OnStaffChanged -= Refresh;
        StoreProgress.OnChanged -= Refresh;
        LockedCheckout.OnChanged -= Refresh;
        EconomyManager.OnMoneyChanged -= OnMoney;
        UIState.Close(this);
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
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
        StaffManager sm = StaffManager.Instance;
        if (sm == null) return;
        headerInfo.text = $"Store level {StoreProgress.Level} · wages are paid at the end of each day";
        wagesText.text = $"Daily wages  <b><color=#FF9F8A><size=27>{EconomyManager.Format(sm.DailyWages)}</size></color></b>";

        bool canC = sm.CanHireCashier(out string rc);
        cards[0].Set(sm.CashierCount, sm.MaxCashiers, canC, rc);
        bool canR = sm.CanHireRestocker(out string rr);
        cards[1].Set(sm.RestockerCount, StaffManager.MaxRestockers, canR, rr);
        bool canG = sm.CanHireGuard(out string rg);
        cards[2].Set(sm.GuardCount, StaffManager.MaxGuards, canG, rg);
        bool canJ = sm.CanHireJanitor(out string rj);
        cards[3].Set(sm.JanitorCount, StaffManager.MaxJanitors, canJ, rj);

        int total = 1 + LockedCheckout.All.Count;
        int open = 1 + LockedCheckout.UnlockedCount;
        LockedCheckout next = LockedCheckout.Next();
        bool canU = LockedCheckout.CanUnlockNext(out string ru);
        counterTitle.text = next == null ? "CHECKOUT COUNTERS" : $"CHECKOUT COUNTER {open + 1}";
        counterStatus.text = next == null
            ? $"All {total} counters are open. Each open counter can have one cashier."
            : $"Another counter halves the queue at rush hour. It needs its own cashier.  <color=#AEB6C2>Open {open}/{total} · needs store Lv {next.requiredLevel}</color>"
              + (canU ? "" : $"  <color=#FF9F8A>{ru}</color>");
        unlockCounter.gameObject.SetActive(next != null);
        if (next != null) ShopPC.SetLabel(unlockCounter, $"UNLOCK · {EconomyManager.Format(next.price)}");
        unlockCounter.interactable = canU;
    }

    // ================= Dựng giao diện (bản thiết kế 03: đơn xin việc) =================
    private class StaffCard
    {
        public TMP_Text who, reason, waiting;
        public GameObject stamp;
        public Button hire, fire;

        public void Set(int count, int max, bool canHire, string why)
        {
            who.text = count > 0 ? $"Hired {count}/{max}" : "position open";
            stamp.SetActive(count > 0);
            waiting.gameObject.SetActive(count == 0);
            reason.text = canHire || count >= max ? "" : why;
            hire.gameObject.SetActive(count < max);
            hire.interactable = canHire;
            fire.gameObject.SetActive(count > 0);
        }
    }

    private readonly StaffCard[] cards = new StaffCard[4];
    private TMP_Text wagesText, counterTitle;

    private void Build()
    {
        Transform canvas = UIKit.CreateCanvas(transform, 550).transform;
        RectTransform c = ShopPC.Computer(canvas, "STAFF", Close, out root);
        headerInfo = ShopPC.Title(c, "STAFF", "", out wagesText);

        var roles = new (string role, string desc, float wage, int level, UnityEngine.Events.UnityAction hire, UnityEngine.Events.UnityAction fire)[]
        {
            ("Cashier", "Scans items and takes payment at a counter.", StaffManager.CashierWage, StaffManager.CashierLevel,
                () => StaffManager.Instance.HireCashier(), () => StaffManager.Instance.FireCashier()),
            ("Restocker", "Carries boxes to shelves that sell that product and refills them.", StaffManager.RestockerWage, StaffManager.RestockerLevel,
                () => StaffManager.Instance.HireRestocker(), () => StaffManager.Instance.FireRestocker()),
            ("Guard", "Stands outside the entrance and chases down shoplifters for you.", StaffManager.GuardWage, StaffManager.GuardLevel,
                () => StaffManager.Instance.HireGuard(), () => StaffManager.Instance.FireGuard()),
            ("Janitor", "Finds dirty spots on the floor and sweeps them, so customers stay happy.", StaffManager.JanitorWage, StaffManager.JanitorLevel,
                () => StaffManager.Instance.HireJanitor(), () => StaffManager.Instance.FireJanitor()),
        };
        float[] rots = { -1f, 0.8f, -0.6f, 1f };
        const float gap = 27f, h = 420f;
        float w = (ShopPC.ContentSize.x - gap * 3f) / 4f;
        for (int i = 0; i < 4; i++)
            cards[i] = Card(c, i * (w + gap), 84f, w, h, rots[i], roles[i].role, roles[i].desc, roles[i].wage, roles[i].level, roles[i].hire, roles[i].fire, 0.04f + i * 0.04f);

        // Quầy thu ngân
        RectTransform bar = ShopPC.Box("Counter", c, 0f, ShopPC.ContentSize.y - 90f, ShopPC.ContentSize.x, 90f, ShopPC.Card);
        ShopPC.Box("Gold", bar, 0f, 0f, 6f, 90f, ShopPC.Gold);
        counterTitle = ShopPC.Text(bar, "", ShopUI.Print, 30, 30f, 0f, 420f, 90f, ShopPC.Gold, TextAlignmentOptions.Left, 3f);
        counterStatus = ShopPC.Text(bar, "", ShopUI.Body, 19, 450f, 0f, 1060f, 90f, ShopPC.Hex(0xD3D8E0), TextAlignmentOptions.Left);
        unlockCounter = ShopPC.Btn(bar, "UNLOCK", ShopPC.ContentSize.x - 27f - 300f, 15f, 300f, 60f, () => LockedCheckout.TryUnlockNext(), ShopPC.Kind.Red, 24f);
        root.SetActive(false);
    }

    private StaffCard Card(Transform parent, float x, float y, float w, float h, float rot, string role, string desc, float wage, int level,
                           UnityEngine.Events.UnityAction onHire, UnityEngine.Events.UnityAction onFire, float delay)
    {
        var sc = new StaffCard();
        RectTransform card = ShopPC.Sheet("Card_" + role, parent, x, y, w, h, ShopPC.Paper, rot, 30f);
        ShopPC.Text(card, "ĐƠN XIN VIỆC · JOB APPLICATION", ShopUI.Body, 15, 0f, 18f, w, 24f, ShopPC.Red, TextAlignmentOptions.Center, 3f, true);

        // Ảnh thẻ: bóng người trên nền xanh
        RectTransform photo = ShopPC.Box("Photo", card, 24f, 54f, 111f, 144f, ShopPC.Hex(0x8FB2D6));
        photo.gameObject.AddComponent<RectMask2D>();
        Color sil = ShopPC.Hex(0x2A2018);
        ShopPC.Dot(photo, new Vector2(84f, 84f), ShopPC.At(photo.sizeDelta, 13.5f, 75f, 84f, 84f), sil);
        ShopPC.Box("Body", photo, 13.5f, 117f, 84f, 40f, sil);
        ShopPC.Dot(photo, new Vector2(42f, 45f), ShopPC.At(photo.sizeDelta, 34.5f, 24f, 42f, 45f), sil);
        ShopUI.Frame(photo, photo.sizeDelta, Color.white, 3f);

        ShopPC.Text(card, role.ToUpperInvariant(), ShopUI.Print, 39, 153f, 50f, w - 170f, 48f, ShopPC.Ink, TextAlignmentOptions.Left, 1.5f);
        sc.who = ShopPC.Text(card, "", ShopUI.Hand, 31, 153f, 96f, w - 170f, 38f, ShopPC.Pen, TextAlignmentOptions.Left);
        ShopPC.Text(card, "Wage", ShopUI.Body, 18, 153f, 144f, 60f, 32f, ShopPC.Ink, TextAlignmentOptions.Left, 0f, true);
        ShopPC.Text(card, $"{EconomyManager.Format(wage)} / day", ShopUI.Hand, 33, 208f, 136f, w - 225f, 42f, ShopPC.Pen, TextAlignmentOptions.Left);
        ShopPC.Text(card, $"Needs store Lv {level}", ShopUI.Body, 16, 153f, 178f, w - 170f, 26f, ShopPC.Muted, TextAlignmentOptions.Left);
        ShopPC.Text(card, desc, ShopUI.Body, 18, 24f, 216f, w - 48f, 80f, ShopPC.Body, TextAlignmentOptions.TopLeft);
        sc.reason = ShopPC.Text(card, "", ShopUI.Body, 15, 24f, 298f, w - 48f, 26f, ShopPC.Red, TextAlignmentOptions.Left, 0f, true);

        float by = h - 24f - 57f;
        sc.stamp = ShopPC.Stamp(card, "ĐÃ NHẬN · HIRED", ShopPC.At(card.sizeDelta, 24f, by + 4f, 200f, 50f), 24f, -6f).gameObject;
        sc.waiting = ShopPC.Text(card, "Not hired yet", ShopUI.Body, 16, 24f, by, 200f, 57f, ShopPC.Muted, TextAlignmentOptions.Left);
        sc.hire = ShopPC.Btn(card, "HIRE", w - 24f - 120f, by, 120f, 57f, onHire, ShopPC.Kind.Red, 24f);
        sc.fire = ShopPC.Btn(card, "FIRE", w - 24f - 120f - 12f - 120f, by, 120f, 57f, onFire, ShopPC.Kind.Dark, 24f);
        return sc;
    }
}
