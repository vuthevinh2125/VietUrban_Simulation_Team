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
    public int Money
    {
        get
        {
            return playerData.money;
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
    }

    public void AddItem(string itemId, float amount)
    {
        playerData.AddItem(itemId, amount);
    }
}
