using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class AcceptedOrdersPanelUI : MonoBehaviour
{
    [Serializable] private class StatusColorEntry
    {
        public Order.OrderState status;
        public Color color = Color.white;
    }
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private TimeController timeController;
    [SerializeField] private Button orderButton;
    [SerializeField] private GameObject orderPanelRoot;
    [SerializeField] private AcceptedOrderCardView cardPrefab;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private OrderTrackingPanelUI trackingPanel;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    [SerializeField] private List<StatusColorEntry> statusColors = new List<StatusColorEntry> { new StatusColorEntry { status = Order.OrderState.Available, color = new Color(0.45f, 0.45f, 0.45f, 1f) }, new StatusColorEntry { status = Order.OrderState.Accepted, color = new Color(0.2f, 0.45f, 0.85f, 1f) }, new StatusColorEntry { status = Order.OrderState.Making, color = new Color(0.95f, 0.5f, 0.15f, 1f) }, new StatusColorEntry { status = Order.OrderState.Cooking, color = new Color(0.95f, 0.75f, 0.15f, 1f) }, new StatusColorEntry { status = Order.OrderState.WaitingConfirm, color = new Color(0.6f, 0.35f, 0.8f, 1f) }, new StatusColorEntry { status = Order.OrderState.Closed, color = new Color(0.2f, 0.65f, 0.35f, 1f) }, new StatusColorEntry { status = Order.OrderState.Delivered, color = new Color(0.35f, 0.55f, 0.55f, 1f) } };
    private readonly List<AcceptedOrderCardView> cardPool = new List<AcceptedOrderCardView>();
    private float refreshTimer;
    private bool isOpen;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private void Awake()
    {
        if (orderPanelRoot != null)
        {
            orderPanelRoot.SetActive(false);
        }
        if (trackingPanel != null)
        {
            trackingPanel.SetPanelOpen(false);
        }
        if (orderButton != null)
        {
            orderButton.onClick.AddListener(TogglePanel);
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
        {
            return;
        }
        refreshTimer = 0f;
        if (isOpen)
        {
            RefreshOrders();
        }
    }
    private void OnDestroy()
    {
        if (orderButton != null)
        {
            orderButton.onClick.RemoveListener(TogglePanel);
        }
        if (isOpen)
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }
    public void TogglePanel()
    {
        SetPanelOpen(!isOpen);
    }
    public void SetPanelOpen(bool open)
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
        if (orderPanelRoot != null)
        {
            orderPanelRoot.SetActive(open);
        }
        if (trackingPanel != null)
        {
            trackingPanel.SetPanelOpen(open);
        }
        if (open)
        {
            RefreshOrders();
        }
    }
    public void RefreshOrders()
    {
        if (orderManager == null || cardPrefab == null || cardContainer == null)
        {
            return;
        }
        int cardIndex = 0;
        foreach (Order order in orderManager.Orders)
        {
            if (order == null || order.OrderStatus == Order.OrderState.Available || order.OrderStatus == Order.OrderState.Delivered)
            {
                continue;
            }
            AcceptedOrderCardView card = GetOrCreateCard(cardIndex);
            cardIndex++;
            card.gameObject.SetActive(true);
            int orderId = order.OrderId;
            bool canCook = order.OrderStatus == Order.OrderState.Accepted;
            card.SetOrder(order, order.DeliveryAddress, GetDeadlineText(order), GetStatusLabel(order.OrderStatus), GetStatusColor(order.OrderStatus), canCook, delegate { StartCooking(orderId); });
        }
        for (int index = cardIndex; index < cardPool.Count; index++)
        {
            cardPool[index].gameObject.SetActive(false);
        }
    }
    private AcceptedOrderCardView GetOrCreateCard(int index)
    {
        if (index < cardPool.Count)
        {
            return cardPool[index];
        }
        AcceptedOrderCardView card = Instantiate(cardPrefab, cardContainer);
        cardPool.Add(card);
        return card;
    }
    private void StartCooking(int orderId)
    {
        if (orderManager.StartMakingOrder(orderId))
        {
            if (trackingPanel != null)
            {
                trackingPanel.TrackOrder(orderId);
            }
            RefreshOrders();
        }
    }
    private Color GetStatusColor(Order.OrderState status)
    {
        foreach (StatusColorEntry entry in statusColors)
        {
            if (entry.status == status)
            {
                return entry.color;
            }
        }
        return Color.gray;
    }
    private string GetStatusLabel(Order.OrderState status)
    {
        if (status == Order.OrderState.Available)
        {
            return "available";
        }
        if (status == Order.OrderState.Accepted)
        {
            return "accepted";
        }
        if (status == Order.OrderState.Making)
        {
            return "making";
        }
        if (status == Order.OrderState.Cooking)
        {
            return "cooking";
        }
        if (status == Order.OrderState.WaitingConfirm)
        {
            return "ready";
        }
        if (status == Order.OrderState.Closed)
        {
            return "boxed";
        }
        return "delivered";
    }
    private string GetDeadlineText(Order order)
    {
        if (!order.HasDeliveryDeadline)
        {
            return "no delivery deadline";
        }
        if (timeController == null || timeController.CurrentTime == DateTime.MinValue)
        {
            return "due " + order.ExpectedDeliveryAt.ToString("HH:mm").ToLowerInvariant();
        }
        TimeSpan remaining = order.ExpectedDeliveryAt - timeController.CurrentTime;
        if (remaining.TotalSeconds <= 0d)
        {
            return "overdue";
        }
        return "due in " + Mathf.CeilToInt((float)remaining.TotalMinutes) + " min";
    }
}
