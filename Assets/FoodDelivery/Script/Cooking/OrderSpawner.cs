using System;
using System.Collections.Generic;
using UnityEngine;

public class OrderSpawner : MonoBehaviour
{
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private TimeController timeController;
    [SerializeField] private NpcPopulationSpawner populationSpawner;
    [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();
    [SerializeField] private int firstOrderId = 1;
    [SerializeField] private int minRequiredCount = 1;
    [SerializeField] private int maxRequiredCount = 3;
    [SerializeField] private int unitPrice = 10000;
    [SerializeField] private float firstOrderDelayGameMinutes;
    [SerializeField] private float minSpawnIntervalGameMinutes = 30f;
    [SerializeField] private float maxSpawnIntervalGameMinutes = 60f;
    [SerializeField] private float expectedDeliveryGameMinutes = 120f;

    private DateTime nextSpawnTime;
    private int nextOrderId;
    private bool scheduleInitialized;

    private void Awake()
    {
        nextOrderId = Mathf.Max(1, firstOrderId);
    }

    private void Update()
    {
        if (orderManager == null || timeController == null || populationSpawner == null)
        {
            return;
        }

        DateTime now = timeController.CurrentTime;
        if (now == DateTime.MinValue)
        {
            return;
        }

        if (!scheduleInitialized)
        {
            nextSpawnTime = now.AddMinutes(Mathf.Max(0f, firstOrderDelayGameMinutes));
            scheduleInitialized = true;
        }
        if (now < nextSpawnTime)
        {
            return;
        }

        TrySpawnOrder(now);
        float minInterval = Mathf.Max(0f, Mathf.Min(minSpawnIntervalGameMinutes, maxSpawnIntervalGameMinutes));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(minSpawnIntervalGameMinutes, maxSpawnIntervalGameMinutes));
        nextSpawnTime = now.AddMinutes(UnityEngine.Random.Range(minInterval, maxInterval));
    }

    private bool TrySpawnOrder(DateTime placedAt)
    {
        NpcController customer = populationSpawner.GetRandomResident();
        List<RecipeData> validRecipes = new List<RecipeData>();
        foreach (RecipeData recipe in recipes)
        {
            if (recipe != null)
            {
                validRecipes.Add(recipe);
            }
        }

        if (customer == null || customer.DeliveryPoint == null || validRecipes.Count == 0)
        {
            Debug.Log("order spawner needs a spawned customer and at least one recipe", this);
            return false;
        }

        RecipeData selectedRecipe = validRecipes[UnityEngine.Random.Range(0, validRecipes.Count)];
        int minCount = Mathf.Max(1, Mathf.Min(minRequiredCount, maxRequiredCount));
        int maxCount = Mathf.Max(minCount, Mathf.Max(minRequiredCount, maxRequiredCount));
        int requiredCount = UnityEngine.Random.Range(minCount, maxCount + 1);
        DateTime expectedDeliveryAt = placedAt.AddMinutes(Mathf.Max(0f, expectedDeliveryGameMinutes));
        Order order = new Order(selectedRecipe, requiredCount, nextOrderId++, Mathf.Max(0, unitPrice),
            customer.DeliveryPoint, customer.ResidentName, customer.DeliveryAddress, placedAt,
            expectedDeliveryAt, customer);

        customer.SetHasPendingDelivery(true);
        orderManager.AddOrder(order);
        return true;
    }
}
