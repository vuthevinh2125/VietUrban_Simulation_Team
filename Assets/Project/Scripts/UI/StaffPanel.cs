using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Tab NHÂN VIÊN trên máy tính: thuê / cho nghỉ thu ngân, nhân viên xếp kệ, bảo vệ. Esc / X để đóng.</summary>
public class StaffPanel : MonoBehaviour
{
    private static StaffPanel instance;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    private GameObject root;
    private TMP_Text headerInfo, cashierStatus, restockerStatus, guardStatus;
    private Button hireCashier, fireCashier, hireRestocker, fireRestocker, hireGuard, fireGuard, unlockCounter;
    private TMP_Text counterStatus;

    private static readonly Color WindowColor = new Color(0.13f, 0.16f, 0.21f, 0.98f);
    private static readonly Color HeaderColor = new Color(0.0f, 0.6f, 0.95f, 1f);
    private static readonly Color RowColor = new Color(0.2f, 0.24f, 0.3f, 1f);

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
        headerInfo.text = $"Store level {StoreProgress.Level}   |   Daily wages {EconomyManager.Format(sm.DailyWages)}";

        bool canC = sm.CanHireCashier(out string rc);
        cashierStatus.text = $"Hired {sm.CashierCount}/{sm.MaxCashiers}" + (canC ? "" : $"\n<size=80%><color=#FF8A7A>{rc}</color></size>");
        hireCashier.interactable = canC;
        fireCashier.interactable = sm.CashierCount > 0;

        bool canR = sm.CanHireRestocker(out string rr);
        restockerStatus.text = $"Hired {sm.RestockerCount}/{StaffManager.MaxRestockers}" + (canR ? "" : $"\n<size=80%><color=#FF8A7A>{rr}</color></size>");
        hireRestocker.interactable = canR;
        fireRestocker.interactable = sm.RestockerCount > 0;

        bool canG = sm.CanHireGuard(out string rg);
        guardStatus.text = $"Hired {sm.GuardCount}/{StaffManager.MaxGuards}" + (canG ? "" : $"\n<size=80%><color=#FF8A7A>{rg}</color></size>");
        hireGuard.interactable = canG;
        fireGuard.interactable = sm.GuardCount > 0;

        int total = 1 + LockedCheckout.All.Count;
        int open = 1 + LockedCheckout.UnlockedCount;
        LockedCheckout next = LockedCheckout.Next();
        bool canU = LockedCheckout.CanUnlockNext(out string ru);
        counterStatus.text = $"Open {open}/{total}" + (next == null ? "" :
            $"\n<size=80%>Next: Lv {next.requiredLevel} · {EconomyManager.Format(next.price)}</size>" +
            (canU ? "" : $"\n<size=75%><color=#FF8A7A>{ru}</color></size>"));
        unlockCounter.interactable = canU;
    }

    // ================= Dựng giao diện =================
    private void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = NewRect("Root", canvasGo.transform);
        var rrt = (RectTransform)root.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        GameObject window = NewRect("Window", root.transform, new Vector2(960, 760));
        window.AddComponent<Image>().color = WindowColor;
        GameObject header = NewRect("Header", window.transform, new Vector2(960, 70), new Vector2(0, 345));
        header.AddComponent<Image>().color = HeaderColor;
        Text(header.transform, "STAFF", 34, new Vector2(900, 60), Vector2.zero, TextAlignmentOptions.Center, true);
        Btn(header.transform, "X", new Vector2(50, 50), new Vector2(445, 0), Close, new Color(0.9f, 0.2f, 0.2f));
        headerInfo = Text(window.transform, "", 24, new Vector2(900, 36), new Vector2(0, 285), TextAlignmentOptions.Center, false);

        Row(window.transform, 195, "Cashier",
            $"Scans items and takes payment at a counter.\n{EconomyManager.Format(StaffManager.CashierWage)}/day  ·  Lv {StaffManager.CashierLevel}",
            out cashierStatus, out hireCashier, out fireCashier,
            () => StaffManager.Instance.HireCashier(), () => StaffManager.Instance.FireCashier());
        Row(window.transform, 65, "Restocker",
            $"Carries boxes to shelves that sell that product and refills them.\n{EconomyManager.Format(StaffManager.RestockerWage)}/day  ·  Lv {StaffManager.RestockerLevel}",
            out restockerStatus, out hireRestocker, out fireRestocker,
            () => StaffManager.Instance.HireRestocker(), () => StaffManager.Instance.FireRestocker());
        Row(window.transform, -65, "Guard",
            $"Stands outside the entrance and chases down shoplifters for you.\n{EconomyManager.Format(StaffManager.GuardWage)}/day  ·  Lv {StaffManager.GuardLevel}",
            out guardStatus, out hireGuard, out fireGuard,
            () => StaffManager.Instance.HireGuard(), () => StaffManager.Instance.FireGuard());

        // Mở thêm quầy thu ngân (tối đa 4 quầy = 4 thu ngân)
        GameObject crow = NewRect("Row_Counter", window.transform, new Vector2(900, 120), new Vector2(0, -195));
        crow.AddComponent<Image>().color = RowColor;
        Text(crow.transform, "<b>Checkout counter</b>", 30, new Vector2(320, 50), new Vector2(-270, 25), TextAlignmentOptions.Left, false);
        Text(crow.transform, "<size=75%>Open another checkout so more customers can pay at once.\nEach open counter can have 1 cashier (max 4).</size>", 26, new Vector2(460, 70), new Vector2(-200, -22), TextAlignmentOptions.Left, false);
        counterStatus = Text(crow.transform, "", 24, new Vector2(220, 110), new Vector2(170, 0), TextAlignmentOptions.Center, true);
        unlockCounter = Btn(crow.transform, "Unlock", new Vector2(150, 46), new Vector2(360, 0), () => LockedCheckout.TryUnlockNext(), new Color(0.2f, 0.6f, 0.9f));

        Text(window.transform, "<size=80%>Wages are paid at the end of each day.</size>", 24, new Vector2(900, 36), new Vector2(0, -330), TextAlignmentOptions.Center, false);
        root.SetActive(false);
    }

    private void Row(Transform parent, float y, string title, string desc, out TMP_Text status, out Button hire, out Button fire,
                     UnityEngine.Events.UnityAction onHire, UnityEngine.Events.UnityAction onFire)
    {
        GameObject row = NewRect("Row_" + title, parent, new Vector2(900, 120), new Vector2(0, y));
        row.AddComponent<Image>().color = RowColor;
        Text(row.transform, $"<b>{title}</b>", 30, new Vector2(200, 50), new Vector2(-330, 25), TextAlignmentOptions.Left, false);
        Text(row.transform, $"<size=75%>{desc}</size>", 26, new Vector2(460, 70), new Vector2(-200, -22), TextAlignmentOptions.Left, false);
        status = Text(row.transform, "", 24, new Vector2(180, 100), new Vector2(155, 0), TextAlignmentOptions.Center, true);
        hire = Btn(row.transform, "Hire", new Vector2(100, 46), new Vector2(310, 0), onHire, new Color(0.2f, 0.7f, 0.3f));
        fire = Btn(row.transform, "Fire", new Vector2(80, 46), new Vector2(410, 0), onFire, new Color(0.75f, 0.3f, 0.25f));
    }

    private static GameObject NewRect(string name, Transform parent, Vector2 size = default, Vector2 pos = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return go;
    }

    private static TMP_Text Text(Transform parent, string text, float size, Vector2 box, Vector2 pos, TextAlignmentOptions align, bool bold)
    {
        GameObject go = NewRect("Text", parent, box, pos);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    private static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject go = NewRect("Button_" + label, parent, size, pos);
        go.AddComponent<Image>().color = color;
        Button b = go.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        Text(go.transform, label, 24, size, Vector2.zero, TextAlignmentOptions.Center, true);
        return b;
    }
}
