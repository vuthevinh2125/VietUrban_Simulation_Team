using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class CartEntry
{
    public ItemData item;
    public float amount; 

    public float LineTotal
    {
        get
        {
            return item.basePriceBuy * amount;
        }
    }
}


public class CartManager : MonoBehaviour
{
    [Header("Bag Capacity")]
    [SerializeField] private float bagCapacity = 100f;
    [SerializeField] private Slider bagFillSlider;
    [SerializeField] private TMP_Text bagPercentText;

    [Header("Cart List")]
    [SerializeField] private Transform cartContentParent;
    [SerializeField] private CartItemRowUI cartRowPrefab;

    [Header("Summary")]
    [SerializeField] private TMP_Text playerMoneyText;
    [SerializeField] private TMP_Text totalCostText;
    [SerializeField] private Button checkoutButton;
    [SerializeField] private PlayerWallet playerWallet;
    private readonly List<CartEntry> _cart = new();
    private readonly List<CartItemRowUI> _spawnedRows = new();
    private float _currentLoad;

    public event Action<CartEntry> OnItemAdded;
    public event Action OnCheckout;

    private void Start()
    {
        checkoutButton.onClick.AddListener(HandleCheckout);
        RefreshUI();
        playerWallet.MoneyChange += HandleMoneyChanged;
    }   
    private void OnDestroy()
    {
        if (playerWallet != null)
            playerWallet.MoneyChange -= HandleMoneyChanged;
    }
    public bool TryAddItem(ItemData item, float amount)
    {
        if (_currentLoad + amount > bagCapacity)
        {
            Debug.Log("Bag is full, cannot add more");
            return false;
        }
        CartEntry existingItem = null;
        foreach (CartEntry entry in _cart)
        {
            if (entry.item == item)
            {
                existingItem = entry;
                break;
            }
        }
        if (existingItem != null)
        {
            existingItem.amount += amount;
        }
        else
        {
            CartEntry newItem = new CartEntry();
            newItem.item = item;
            newItem.amount = amount;
            _cart.Add(newItem);
            existingItem = newItem;
        }
        _currentLoad += amount;
        RefreshUI();
        if (OnItemAdded != null)
        {
            OnItemAdded(existingItem);
        }
        return true;
    }
    public void RemoveItem(CartEntry entry)
    {
        _currentLoad -= entry.amount;
        _cart.Remove(entry);
        RefreshUI();
    }

    private void HandleCheckout()
    { 
        if (_cart.Count == 0)
        {
            return;
        }
        int total = GetCartTotal();
        bool canPay = playerWallet.TrySpend(total);
        if (canPay == false)
        {
            return;
        }
        foreach (CartEntry entry in _cart)
        {
            playerWallet.AddItem(entry.item.itemId, entry.amount);
        }
        Debug.Log("checkout saving");
        SaveSystem.SaveGame(GameSession.Current);
        _cart.Clear();
        _currentLoad = 0;
        RefreshUI();
        if (OnCheckout != null)
        {
            OnCheckout();
        }
    }

    private int GetCartTotal()
    {
        float total = 0f;
        foreach (CartEntry entry in _cart)
            total += entry.LineTotal;
        return Mathf.RoundToInt(total);
    }

    private void RefreshUI()
    {
        float fillRatio = _currentLoad / bagCapacity;
        if (fillRatio > 1)
        {
            fillRatio = 1;
        }
        if (fillRatio < 0)
        {
            fillRatio = 0;
        }
        bagFillSlider.value = fillRatio;
        float percent = fillRatio * 100;
        bagPercentText.text = percent.ToString("0") + "%";

        foreach (CartItemRowUI row in _spawnedRows)
        {
            Destroy(row.gameObject);
        }
        _spawnedRows.Clear();
        int total = GetCartTotal();
        foreach (CartEntry entry in _cart)
        {
            CartItemRowUI row = Instantiate(cartRowPrefab, cartContentParent);

            row.Setup(entry, () => RemoveItem(entry));

            _spawnedRows.Add(row);
        }
        playerMoneyText.text = "Current cash: " + playerWallet.Money.ToString("N0") + "đ";
        totalCostText.text = "Total payment: " + total.ToString("N0") + "đ";
        if (_cart.Count > 0 && total <= playerWallet.Money)
        {
            checkoutButton.interactable = true;
        }
        else
        {
            checkoutButton.interactable = false;
        }
    }
    private void HandleMoneyChanged(int newMoney)
    {
        RefreshUI();
    }
}