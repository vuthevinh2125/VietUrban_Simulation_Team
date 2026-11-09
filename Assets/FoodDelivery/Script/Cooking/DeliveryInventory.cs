using System;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryInventory : MonoBehaviour
{
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private Transform playerTransform;
    [SerializeField, Min(0.5f)] private float deliveryDistance = 3f;

    private readonly List<Order> packages = new List<Order>();
    private Order selectedPackage;

    public event Action PackagesChanged;
    public event Action<Order> SelectedPackageChanged;

    public IReadOnlyList<Order> Packages
    {
        get { return packages; }
    }

    public Order SelectedPackage
    {
        get { return selectedPackage; }
    }

    public float DeliveryDistance
    {
        get { return deliveryDistance; }
    }

    public float DistanceToSelectedCustomer
    {
        get
        {
            if (selectedPackage == null || selectedPackage.CustomerNpc == null)
            {
                return float.PositiveInfinity;
            }
            return Vector3.Distance(GetPlayerPosition(), selectedPackage.CustomerNpc.position);
        }
    }

    public bool CanDeliverSelectedPackage
    {
        get
        {
            return selectedPackage != null && selectedPackage.CustomerNpc != null &&
                DistanceToSelectedCustomer <= deliveryDistance;
        }
    }

    public bool TryAddPackage(Order order)
    {
        if (order == null || order.OrderStatus != Order.OrderState.Closed || !order.IsCreated)
        {
            return false;
        }

        foreach (Order package in packages)
        {
            if (package.OrderId == order.OrderId)
            {
                return false;
            }
        }

        packages.Add(order);
        if (PackagesChanged != null)
        {
            PackagesChanged();
        }
        return true;
    }

    public bool SelectPackage(int orderId)
    {
        foreach (Order package in packages)
        {
            if (package.OrderId == orderId)
            {
                selectedPackage = package;
                if (SelectedPackageChanged != null)
                {
                    SelectedPackageChanged(selectedPackage);
                }
                return true;
            }
        }
        return false;
    }

    public void ClearSelectedPackage()
    {
        selectedPackage = null;
        if (SelectedPackageChanged != null)
        {
            SelectedPackageChanged(null);
        }
    }

    public bool TryDeliverSelectedPackage()
    {
        if (!CanDeliverSelectedPackage || orderManager == null || playerWallet == null)
        {
            return false;
        }

        if (!orderManager.CompleteDelivery(selectedPackage.OrderId, playerWallet))
        {
            return false;
        }

        packages.Remove(selectedPackage);
        selectedPackage = null;
        if (PackagesChanged != null)
        {
            PackagesChanged();
        }
        if (SelectedPackageChanged != null)
        {
            SelectedPackageChanged(null);
        }
        return true;
    }

    public bool ContainsPackage(int orderId)
    {
        foreach (Order package in packages)
        {
            if (package.OrderId == orderId)
            {
                return true;
            }
        }
        return false;
    }

    private Vector3 GetPlayerPosition()
    {
        if (playerTransform != null)
        {
            return playerTransform.position;
        }
        return transform.position;
    }
}
