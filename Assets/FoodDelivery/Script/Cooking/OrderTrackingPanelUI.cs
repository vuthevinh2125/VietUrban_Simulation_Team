using System.Collections.Generic;
using UnityEngine;
public class OrderTrackingPanelUI : MonoBehaviour
{
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private CookingOven cookingOven;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TrackingOrderCardView cardPrefab;
    [SerializeField] private Transform cardContainer;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
    private readonly List<int> trackedOrderIds = new List<int>();
    private readonly List<TrackingOrderCardView> cardPool = new List<TrackingOrderCardView>();
    private readonly Dictionary<int, float> progressByOrderId = new Dictionary<int, float>();
    private float refreshTimer;
    private bool panelOpen;
    private void OnEnable()
    {
        if (cookingOven != null)
        {
            cookingOven.OnBatchProgress += HandleBatchProgress;
            cookingOven.OnBatchDone += HandleBatchDone;
        }
    }
    private void OnDisable()
    {
        if (cookingOven != null)
        {
            cookingOven.OnBatchProgress -= HandleBatchProgress;
            cookingOven.OnBatchDone -= HandleBatchDone;
        }
    }
    private void Start()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
        {
            return;
        }
        refreshTimer = 0f;
        if (panelOpen)
        {
            RefreshOrders();
        }
    }
    public void SetPanelOpen(bool open)
    {
        panelOpen = open;
        if (panelRoot != null)
        {
            panelRoot.SetActive(open);
        }
        if (open)
        {
            RefreshOrders();
        }
    }
    public void TrackOrder(int orderId)
    {
        if (!trackedOrderIds.Contains(orderId))
        {
            trackedOrderIds.Add(orderId);
        }
        if (!progressByOrderId.ContainsKey(orderId))
        {
            progressByOrderId.Add(orderId, 0f);
        }
        RefreshOrders();
    }
    public void RefreshOrders()
    {
        if (orderManager == null || cardPrefab == null || cardContainer == null)
        {
            return;
        }
        int cardIndex = 0;
        for (int index = trackedOrderIds.Count - 1; index >= 0; index--)
        {
            Order order = orderManager.GetOrderById(trackedOrderIds[index]);
            if (order == null || order.OrderStatus == Order.OrderState.Delivered)
            {
                trackedOrderIds.RemoveAt(index);
                continue;
            }
            TrackingOrderCardView card = GetOrCreateCard(cardIndex);
            cardIndex++;
            card.gameObject.SetActive(true);
            int orderId = order.OrderId;
            float progress = GetProgress(orderId);
            card.SetOrder(order, progress, delegate { StartMakeUp(orderId); }, delegate { ConfirmAndCreateBox(orderId); });
        }
        for (int index = cardIndex; index < cardPool.Count; index++)
        {
            cardPool[index].gameObject.SetActive(false);
        }
    }
    private TrackingOrderCardView GetOrCreateCard(int index)
    {
        if (index < cardPool.Count)
        {
            return cardPool[index];
        }
        TrackingOrderCardView card = Instantiate(cardPrefab, cardContainer);
        cardPool.Add(card);
        return card;
    }
    private float GetProgress(int orderId)
    {
        float progress;
        if (progressByOrderId.TryGetValue(orderId, out progress))
        {
            return progress;
        }
        return 0f;
    }
    private void HandleBatchProgress(CookingBatch batch)
    {
        if (batch == null || batch.Order == null || !trackedOrderIds.Contains(batch.Order.OrderId))
        {
            return;
        }
        progressByOrderId[batch.Order.OrderId] = batch.Progress;
    }
    private void HandleBatchDone(CookingBatch batch)
    {
        if (batch == null || batch.Order == null || !trackedOrderIds.Contains(batch.Order.OrderId))
        {
            return;
        }
        progressByOrderId[batch.Order.OrderId] = 1f;
        RefreshOrders();
    }
    private void StartMakeUp(int orderId)
    {
        if (orderManager.SelectOrder(orderId) && orderManager.MakeUpSelectedOrder())
        {
            progressByOrderId[orderId] = 0f;
            RefreshOrders();
        }
    }
    private void ConfirmAndCreateBox(int orderId)
    {
        if (!orderManager.SelectOrder(orderId))
        {
            return;
        }
        Order order = orderManager.GetOrderById(orderId);
        if (order == null)
        {
            return;
        }
        if (order.OrderStatus == Order.OrderState.WaitingConfirm && !orderManager.ConfirmSelectedOrder())
        {
            return;
        }
        if (orderManager.CreateShippingBox(orderId))
        {
            trackedOrderIds.Remove(orderId);
            progressByOrderId.Remove(orderId);
            RefreshOrders();
        }
    }
}
