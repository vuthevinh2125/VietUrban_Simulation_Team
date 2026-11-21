using UnityEngine;
using System;
using System.Collections.Generic;
public enum BatchState
{
    Staging, Cooking, Done
}
[System.Serializable]
public class CookingBatch
{
    [SerializeField] private Order order;
    private List<CookingResult> results;
    [SerializeField] private BatchState state;
    [SerializeField] private float elapsed;
    [SerializeField] private float cookTime;
    public Order Order
    {
        get { 
        return order;}
    }
    public IReadOnlyList<CookingResult> Results
    {
        get
        {
            return results;
        }
    }
    public BatchState State
    {
        get
        {
            return state;
        }
    }
    public int StagedCount
    {
        get
        {
            return results.Count;
        }
    }
    public bool IsDone
    {
        get
        {
            return (state == BatchState.Done);
        }
    }
    public float Progress
    {
        get
        {
            return Mathf.Clamp01(elapsed / cookTime);
        }
    }

    public CookingBatch(Order order)
    {
        
        this.order = order;
        if (order == null || order.Recipe == null)
        {
            cookTime = 1;
            Debug.Log("need order");
        }
        else
        {
            cookTime = order.Recipe.CookTime;
        }
        cookTime = Math.Max(0.01f, cookTime);
        results = new List<CookingResult>();
        state = BatchState.Staging;
        elapsed = 0f;
        
    }

    public bool Stage(CookingResult result) {
        if (state != BatchState.Staging || result == null)
        {
            return false;
        }
        results.Add(result);
        return true;
    }

    public bool StartCooking()
    {
        if (state != BatchState.Staging || StagedCount == 0)
        {
            return false;
        }state = BatchState.Cooking;
        return true;
    }
    public bool Tick(float delTime)
    {
        if (state != BatchState.Cooking)
        {
            return false;        }
        elapsed += delTime;
        if (elapsed >= cookTime)
        {
            elapsed = cookTime;
            state = BatchState.Done;
            return true;
        }
        return false;
    }
}
