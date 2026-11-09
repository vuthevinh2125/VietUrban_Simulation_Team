using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderDeliveryUI : MonoBehaviour
{
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private DeliveryInventory deliveryInventory;
    [SerializeField] private TimeController timeController;
    [SerializeField] private KeyCode phoneKey = KeyCode.O;
    [SerializeField] private KeyCode deliveryInventoryKey = KeyCode.Tab;

    private Canvas uiCanvas;
    private GameObject mainPanel;
    private TextMeshProUGUI panelTitle;
    private Transform listContent;
    private Button phoneButton;
    private int activeTab;
    private bool isOpen;
    private float refreshTimer;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private const int AvailableTab = 0;
    private const int AcceptedTab = 1;
    private const int DeliveryTab = 2;

    private void Start()
    {
        CreateInterface();
        mainPanel.SetActive(false);
        SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(phoneKey))
        {
            if (isOpen)
            {
                SetOpen(false);
            }
            else
            {
                activeTab = AvailableTab;
                SetOpen(true);
            }
        }

        if (Input.GetKeyDown(deliveryInventoryKey))
        {
            if (isOpen && activeTab == DeliveryTab)
            {
                SetOpen(false);
            }
            else
            {
                activeTab = DeliveryTab;
                SetOpen(true);
            }
        }

        if (!isOpen)
        {
            return;
        }

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= 0.5f)
        {
            refreshTimer = 0f;
            RefreshInterface();
        }
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void CreateInterface()
    {
        GameObject canvasObject = new GameObject("order and delivery ui");
        canvasObject.transform.SetParent(transform, false);
        uiCanvas = canvasObject.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        phoneButton = CreateButton(canvasObject.transform, "order", TogglePhone);
        RectTransform phoneButtonRect = phoneButton.GetComponent<RectTransform>();
        SetAnchoredRect(phoneButtonRect, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(190f, 48f));

        mainPanel = CreatePanel(canvasObject.transform, new Color(0.08f, 0.1f, 0.13f, 0.96f));
        RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
        SetAnchoredRect(panelRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(500f, 690f));

        panelTitle = CreateText(mainPanel.transform, "đơn hàng", 30, TextAlignmentOptions.Center);
        SetAnchoredRect(panelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-28f, 48f));

        GameObject tabs = new GameObject("tabs");
        tabs.transform.SetParent(mainPanel.transform, false);
        RectTransform tabsRect = tabs.AddComponent<RectTransform>();
        SetAnchoredRect(tabsRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(-28f, 46f));
        HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 8f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        CreateButton(tabs.transform, "available", delegate { SelectTab(AvailableTab); });
        CreateButton(tabs.transform, "đã nhận", delegate { SelectTab(AcceptedTab); });
        CreateButton(tabs.transform, "túi giao hàng", delegate { SelectTab(DeliveryTab); });

        GameObject scrollObject = new GameObject("order list");
        scrollObject.transform.SetParent(mainPanel.transform, false);
        RectTransform scrollRectTransform = scrollObject.AddComponent<RectTransform>();
        SetAnchoredRect(scrollRectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(0f, -42f), new Vector2(-28f, -142f));
        Image scrollImage = scrollObject.AddComponent<Image>();
        scrollImage.color = new Color(0.04f, 0.05f, 0.07f, 0.6f);
        Mask scrollMask = scrollObject.AddComponent<Mask>();
        scrollMask.showMaskGraphic = false;
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject contentObject = new GameObject("content");
        contentObject.transform.SetParent(scrollObject.transform, false);
        RectTransform contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup listLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(10, 10, 10, 10);
        listLayout.spacing = 10f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRect;
        scroll.viewport = scrollRectTransform;
        listContent = contentObject.transform;

        TextMeshProUGUI footer = CreateText(mainPanel.transform,
            "nhấn o để mở điện thoại, tab để xem túi giao hàng", 16, TextAlignmentOptions.Center);
        SetAnchoredRect(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-28f, 28f));
    }

    private void TogglePhone()
    {
        if (isOpen)
        {
            SetOpen(false);
        }
        else
        {
            activeTab = AvailableTab;
            SetOpen(true);
        }
    }

    private void SelectTab(int tab)
    {
        activeTab = tab;
        RefreshInterface();
    }

    private void SetOpen(bool open)
    {
        if (isOpen == open)
        {
            return;
        }

        if (open)
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }

        isOpen = open;
        mainPanel.SetActive(open);
        if (open)
        {
            RefreshInterface();
        }
    }

    private void RefreshInterface()
    {
        if (orderManager == null || deliveryInventory == null || listContent == null)
        {
            return;
        }

        string title = "đơn hàng mới";
        if (activeTab == AcceptedTab)
        {
            title = "đơn đã nhận";
        }
        else if (activeTab == DeliveryTab)
        {
            title = "túi giao hàng";
        }
        panelTitle.text = title;
        ClearList();

        if (activeTab == DeliveryTab)
        {
            BuildDeliveryList();
        }
        else
        {
            BuildOrderList();
        }
    }

    private void BuildOrderList()
    {
        bool foundOrder = false;
        foreach (Order order in orderManager.Orders)
        {
            if (activeTab == AvailableTab && order.OrderStatus != Order.OrderState.Available)
            {
                continue;
            }
            if (activeTab == AcceptedTab && order.OrderStatus == Order.OrderState.Available)
            {
                continue;
            }
            if (order.OrderStatus == Order.OrderState.Delivered)
            {
                continue;
            }

            foundOrder = true;
            BuildOrderCard(order);
        }

        if (!foundOrder)
        {
            CreateText(listContent, "chưa có đơn hàng trong mục này", 20, TextAlignmentOptions.Center);
        }
    }

    private void BuildOrderCard(Order order)
    {
        GameObject card = CreatePanel(listContent, new Color(0.16f, 0.19f, 0.23f, 1f));
        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.minHeight = 154f;
        cardLayout.preferredHeight = 154f;
        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        string recipeName = "món chưa xác định";
        if (order.Recipe != null)
        {
            recipeName = order.Recipe.RecipeName;
        }
        string details = "đơn #" + order.OrderId + " · " + recipeName + " · " +
            order.ProducedCount + "/" + order.RequiredCount + " · " + GetStatusText(order);
        CreateText(card.transform, details, 19, TextAlignmentOptions.Left);
        CreateText(card.transform, order.CustomerName + " · " + order.DeliveryAddress, 16, TextAlignmentOptions.Left);
        CreateText(card.transform, GetDeadlineText(order), 15, TextAlignmentOptions.Left);

        if (activeTab == AvailableTab)
        {
            int orderId = order.OrderId;
            CreateButton(card.transform, "nhận đơn", delegate { orderManager.AcceptOrder(orderId); });
            return;
        }

        if (order.OrderStatus == Order.OrderState.Accepted)
        {
            int orderId = order.OrderId;
            CreateButton(card.transform, "bắt đầu làm", delegate { orderManager.StartMakingOrder(orderId); });
        }
        else if (order.OrderStatus == Order.OrderState.WaitingConfirm)
        {
            int orderId = order.OrderId;
            if (order.IsComplete)
            {
                CreateButton(card.transform, "xác nhận và tạo hộp", delegate { ConfirmAndCreateBox(orderId); });
            }
            else
            {
                CreateButton(card.transform, "làm bù", delegate { StartMakeUp(orderId); });
                CreateButton(card.transform, "xác nhận và tạo hộp", delegate { ConfirmAndCreateBox(orderId); });
            }
        }
        else if (order.OrderStatus == Order.OrderState.Closed)
        {
            if (!order.IsCreated)
            {
                int orderId = order.OrderId;
                CreateButton(card.transform, "tạo hộp giao hàng", delegate { orderManager.CreateShippingBox(orderId); });
            }
            else
            {
                CreateText(card.transform, "đang chờ lấy hộp ở kệ", 15, TextAlignmentOptions.Left);
            }
        }
    }

    private void ConfirmAndCreateBox(int orderId)
    {
        if (!orderManager.SelectOrder(orderId))
        {
            return;
        }
        if (orderManager.ConfirmSelectedOrder())
        {
            orderManager.CreateShippingBox(orderId);
        }
    }

    private void StartMakeUp(int orderId)
    {
        if (orderManager.SelectOrder(orderId))
        {
            orderManager.MakeUpSelectedOrder();
        }
    }

    private void BuildDeliveryList()
    {
        if (deliveryInventory.Packages.Count == 0)
        {
            CreateText(listContent, "chưa có hộp trong túi giao hàng", 20, TextAlignmentOptions.Center);
            return;
        }

        foreach (Order order in deliveryInventory.Packages)
        {
            GameObject card = CreatePanel(listContent, new Color(0.16f, 0.19f, 0.23f, 1f));
            LayoutElement cardLayout = card.AddComponent<LayoutElement>();
            cardLayout.minHeight = 160f;
            cardLayout.preferredHeight = 160f;
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            int orderId = order.OrderId;
            bool selected = deliveryInventory.SelectedPackage == order;
            string recipeName = "món chưa xác định";
            if (order.Recipe != null)
            {
                recipeName = order.Recipe.RecipeName;
            }
            CreateText(card.transform, "đơn #" + orderId + " · " + recipeName + " · " +
                order.ProducedCount + " món", 19, TextAlignmentOptions.Left);
            CreateText(card.transform, order.CustomerName + " · " + order.DeliveryAddress, 16, TextAlignmentOptions.Left);
            CreateButton(card.transform, "chọn hộp này", delegate { deliveryInventory.SelectPackage(orderId); });

            if (selected)
            {
                float distance = deliveryInventory.DistanceToSelectedCustomer;
                CreateText(card.transform, "cách khách " + distance.ToString("0.0") + " m", 15, TextAlignmentOptions.Left);
                Button deliverButton = CreateButton(card.transform, "giao hàng", delegate
                {
                    deliveryInventory.TryDeliverSelectedPackage();
                });
                deliverButton.interactable = deliveryInventory.CanDeliverSelectedPackage;
            }
        }
    }

    private string GetStatusText(Order order)
    {
        if (order.OrderStatus == Order.OrderState.Available)
        {
            return "đang chờ nhận";
        }
        if (order.OrderStatus == Order.OrderState.Accepted)
        {
            return "đã nhận";
        }
        if (order.OrderStatus == Order.OrderState.Making || order.OrderStatus == Order.OrderState.Cooking)
        {
            return "đang làm";
        }
        if (order.OrderStatus == Order.OrderState.WaitingConfirm)
        {
            return "chờ xác nhận";
        }
        if (order.OrderStatus == Order.OrderState.Closed)
        {
            return "chờ giao";
        }
        return "đã giao";
    }

    private string GetDeadlineText(Order order)
    {
        if (!order.HasDeliveryDeadline)
        {
            return "chưa có hạn giao";
        }
        if (timeController == null || timeController.CurrentTime == DateTime.MinValue)
        {
            return "hạn giao " + order.ExpectedDeliveryAt.ToString("HH:mm");
        }

        TimeSpan remaining = order.ExpectedDeliveryAt - timeController.CurrentTime;
        if (remaining.TotalSeconds <= 0d)
        {
            return "đã quá hạn";
        }
        return "còn " + Mathf.CeilToInt((float)remaining.TotalMinutes) + " phút";
    }

    private void ClearList()
    {
        for (int index = listContent.childCount - 1; index >= 0; index--)
        {
            Destroy(listContent.GetChild(index).gameObject);
        }
    }

    private GameObject CreatePanel(Transform parent, Color color)
    {
        GameObject panel = new GameObject("panel");
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 0f);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string value, int size, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject("text");
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.minHeight = 30f;
        layout.preferredHeight = 42f;
        return text;
    }

    private Button CreateButton(Transform parent, string label, Action action)
    {
        GameObject buttonObject = new GameObject("button");
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 38f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.38f, 0.55f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(delegate { action(); });

        TextMeshProUGUI text = CreateText(buttonObject.transform, label, 17, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 2f);
        textRect.offsetMax = new Vector2(-6f, -2f);
        LayoutElement textLayout = text.GetComponent<LayoutElement>();
        Destroy(textLayout);

        LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
        buttonLayout.minHeight = 38f;
        buttonLayout.preferredHeight = 38f;
        return button;
    }

    private void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
