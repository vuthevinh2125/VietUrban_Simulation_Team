using UnityEngine;
using System;
public class PlayerWallet : MonoBehaviour
{
    
    private PlayerData playerData
    {
        get
        {
            return GameSession.Current;
        }
    }
    public event Action<int> MoneyChange;
    public event Action<int> MoneyTransaction;
    public event Action<int> ReputationChanged;
    public int Money
    {
        get
        {
            return playerData.money;
        }
    }
    public int Reputation
    {
        get
        {
            return playerData.reputation;
        }
    }
    
    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }
        if (playerData.money < amount)
        {
            return false;
        }
        playerData.money -= amount;
        if (MoneyChange != null)
        {
            MoneyChange(playerData.money);
        }
        if (MoneyTransaction != null)
        {
            MoneyTransaction(-amount);
        }
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        playerData.money += amount;
        if (MoneyChange != null)
        {
            MoneyChange(playerData.money);
        }
        if (MoneyTransaction != null)
        {
            MoneyTransaction(amount);
        }
    }

    public void SetReputation(int reputation)
    {
        if (reputation > 50)
        {
            reputation = 50;
        }
        playerData.reputation = reputation;
        if (ReputationChanged != null)
        {
            ReputationChanged(playerData.reputation);
        }
    }

    public void AddItem(string itemId, float amount)
    {
        playerData.AddItem(itemId, amount);
    }
}
