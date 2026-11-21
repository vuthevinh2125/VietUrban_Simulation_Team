using UnityEngine;
using System.Collections.Generic;
//holding batch list, each frame call Tick for each batch, when a batch is completed, call order.Addcookedresults to create shipbox then delete that batch from the list
//and also send 2 events: progress per bacth for UI and the completed batch
public class CookingOven : InteractableObject
{
    [SerializeField] private Transform ovenVisualRoot;
    [SerializeField] private Vector3 ovenVisualOffset;
    [SerializeField] private float ovenVisualSpacing = 0.25f;
    private List<CookingBatch> cookingBatches = new List<CookingBatch>();
    private Dictionary<CookingBatch, List<GameObject>> stagedVisuals = new Dictionary<CookingBatch, List<GameObject>>();
    public event System.Action<CookingBatch> OnBatchProgress; //update progress on UI
    public event System.Action<CookingBatch> OnBatchDone;
    private CookingSession cookingSession;

    public void SetCookingSession(CookingSession session)
    {
        cookingSession = session;
    }

    public override string GetInteractionPrompt()
    {
        if (cookingSession != null && cookingSession.HasStagedDish)
        {
            return "press k to cook";
        }
        return "oven";
    }

    public override bool HandleInteractionKey(KeyCode key)
    {
        if (key == KeyCode.K && cookingSession != null)
        {
            return cookingSession.StartCookingBatch();
        }
        return false;
    }
    

    void Update()
    {
    for (int i = cookingBatches.Count - 1; i >= 0; i--)
        {
            CookingBatch batch = cookingBatches[i];
            bool done = batch.Tick(Time.deltaTime);
            if (OnBatchProgress != null)
            {
                OnBatchProgress(batch);
            }
            if (done == true)
            {
                bool result = batch.Order.AddCookedResults(batch.Results);
                if (result == false)
                {
                    Debug.Log("addcookedresults fail");
                }
                cookingBatches.RemoveAt(i);
                if (OnBatchDone != null)
                {
                    OnBatchDone(batch);
                    
                }
                ClearBatchVisuals(batch);
                ShowCookedDishVisuals(batch);
            }
        }  
        
        
    }
    public bool StartBatch(CookingBatch batch)
    {
        if (batch == null)
        {
            return false;
        }
        if (batch.State != BatchState.Staging)
        { return false; }
        if (batch.StagedCount <= 0)
        {
            return false;
        }
        if (batch.Order == null)
        {
            return false;
        }
        if (batch.Order.OrderStatus != Order.OrderState.Making)
        {
            return false;
        }
        batch.StartCooking();
        batch.Order.MarkCooking();
        cookingBatches.Add(batch);
        return true;
    }

    public void StageDishVisual(CookingBatch batch, GameObject visual)
    {
        if (batch == null || visual == null)
        {
            return;
        }

        Transform visualRoot = ovenVisualRoot;
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        List<GameObject> batchVisuals;
        if (stagedVisuals.TryGetValue(batch, out batchVisuals) == false)
        {
            batchVisuals = new List<GameObject>();
            stagedVisuals.Add(batch, batchVisuals);
        }

        int visualIndex = GetStagedVisualCount();
        visual.transform.SetParent(visualRoot, true);
        visual.transform.localPosition = ovenVisualOffset + Vector3.right * ovenVisualSpacing * visualIndex;
        visual.transform.localRotation = Quaternion.identity;
        batchVisuals.Add(visual);
    }

    private int GetStagedVisualCount()
    {
        int count = 0;
        foreach (KeyValuePair<CookingBatch, List<GameObject>> pair in stagedVisuals)
        {
            count += pair.Value.Count;
        }
        return count;
    }

    private void ShowCookedDishVisuals(CookingBatch batch)
    {
        if (batch == null || batch.Order == null || batch.Order.Recipe == null)
        {
            return;
        }

        Transform visualRoot = ovenVisualRoot != null ? ovenVisualRoot : transform;
        foreach (CookingResult result in batch.Results)
        {
            if (result == null)
            {
                continue;
            }

            GameObject prefab = result.isCorrect
                ? batch.Order.Recipe.CompletedDish
                : batch.Order.Recipe.IncompletedDish;
            if (prefab == null)
            {
                continue;
            }

            int index = visualRoot.childCount;
            GameObject cookedDish = Instantiate(prefab, visualRoot);
            cookedDish.transform.localPosition = ovenVisualOffset + Vector3.right * ovenVisualSpacing * index;
            cookedDish.transform.localRotation = Quaternion.identity;
        }
    }

    private void ClearBatchVisuals(CookingBatch batch)
    {
        List<GameObject> batchVisuals;
        if (stagedVisuals.TryGetValue(batch, out batchVisuals) == false)
        {
            return;
        }

        foreach (GameObject visual in batchVisuals)
        {
            if (visual != null)
            {
                Destroy(visual);
            }
        }
        stagedVisuals.Remove(batch);
    }

}
