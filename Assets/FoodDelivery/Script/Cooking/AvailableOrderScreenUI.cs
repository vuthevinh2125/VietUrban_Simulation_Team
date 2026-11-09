using System.Collections.Generic;
using UnityEngine;

public class AvailableOrderScreenUI : MonoBehaviour
{
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private OrderCardView orderCardPrefab;
    [SerializeField] private Transform orderCardContainer;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

    private readonly List<int> displayedOrderIds = new List<int>();
    private readonly List<OrderCardView> cardPool = new List<OrderCardView>();
    private float refreshTimer;

    private void OnEnable()
    {
        RefreshOrders();
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
        {
            return;
        }

        refreshTimer = 0f;
        RefreshOrders();
    }

    public void RefreshOrders()
    {
        if (orderManager == null || orderCardPrefab == null || orderCardContainer == null)
        {
            return;
        }

        List<Order> availableOrders = new List<Order>();
        foreach (Order order in orderManager.Orders)
        {
            if (order != null && order.OrderStatus == Order.OrderState.Available)
            {
                availableOrders.Add(order);
            }
        }

        if (DisplayedOrdersMatch(availableOrders))
        {
            return;
        }

        displayedOrderIds.Clear();

        for (int index = 0; index < availableOrders.Count; index++)
        {
            Order order = availableOrders[index];
            OrderCardView card = GetOrCreateCard(index);
            card.gameObject.SetActive(true);
            int orderId = order.OrderId;
            card.SetOrder(order, delegate { AcceptOrder(orderId); });
            displayedOrderIds.Add(orderId);
        }

        for (int index = availableOrders.Count; index < cardPool.Count; index++)
        {
            cardPool[index].gameObject.SetActive(false);
        }
    }

    private bool DisplayedOrdersMatch(List<Order> availableOrders)
    {
        if (displayedOrderIds.Count != availableOrders.Count)
        {
            return false;
        }

        for (int index = 0; index < availableOrders.Count; index++)
        {
            if (displayedOrderIds[index] != availableOrders[index].OrderId)
            {
                return false;
            }
        }

        return true;
    }

    private OrderCardView GetOrCreateCard(int index)
    {
        if (index < cardPool.Count)
        {
            return cardPool[index];
        }

        OrderCardView card = Instantiate(orderCardPrefab, orderCardContainer);
        cardPool.Add(card);
        return card;
    }

    private void AcceptOrder(int orderId)
    {
        if (orderManager.AcceptOrder(orderId))
        {
            RefreshOrders();
        }
    }
}
