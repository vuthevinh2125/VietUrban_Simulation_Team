using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class NpcResidentData
{
    public string residentName;
    public string deliveryAddress;
    public Transform homeEntrance;
    public NpcBehaviorType behaviorType;
}

public class NpcPopulationSpawner : MonoBehaviour
{
    [SerializeField] private NpcController npcPrefab;
    [SerializeField] private Transform player;
    [SerializeField, Min(1)] private int populationSize = 8;
    [SerializeField] private bool spawnAcrossWholeNavMesh = true;
    [SerializeField, Min(0f)] private float spawnRadius = 40f;
    [SerializeField, Min(0.1f)] private float sampleRadius = 4f;
    [SerializeField, Min(0f)] private float minimumSpawnSeparation = 5f;
    [SerializeField, Range(0f, 1f)] private float activityVisitChance = 0.2f;
    [SerializeField, Min(1f)] private float roamingRadius = 35f;
    [SerializeField, Min(0f)] private float minTimeRoaming = 2f;
    [SerializeField, Min(0f)] private float maxTimeRoaming = 8f;
    [SerializeField] private Transform[] houseEntrances;
    [SerializeField] private Transform[] shopSpots;
    [SerializeField] private Transform[] parkSpots;
    [SerializeField] private List<NpcResidentData> residents = new List<NpcResidentData>();

    private readonly List<NpcController> activeResidents = new List<NpcController>();
    private readonly List<Vector3> spawnPositions = new List<Vector3>();

    public IReadOnlyList<NpcController> ActiveResidents { get { return activeResidents; } }

    private void Start()
    {
        SpawnPopulation();
    }

    public NpcController GetRandomResident()
    {
        List<NpcController> availableResidents = new List<NpcController>();
        foreach (NpcController resident in activeResidents)
        {
            if (resident != null && resident.isActiveAndEnabled && !resident.HasPendingDelivery)
            {
                availableResidents.Add(resident);
            }
        }

        if (availableResidents.Count == 0)
        {
            return null;
        }
        return availableResidents[UnityEngine.Random.Range(0, availableResidents.Count)];
    }

    private void SpawnPopulation()
    {
        if (npcPrefab == null || residents.Count == 0 || houseEntrances == null || houseEntrances.Length < 2)
        {
            Debug.Log("npc population needs a prefab, residents and house entrances", this);
            return;
        }

        List<NpcResidentData> spawnList = new List<NpcResidentData>();
        foreach (NpcResidentData resident in residents)
        {
            if (resident != null && resident.homeEntrance != null)
            {
                spawnList.Add(resident);
            }
        }

        if (spawnList.Count == 0)
        {
            Debug.Log("npc population has no residents with a home entrance", this);
            return;
        }

        int count = Mathf.Min(Mathf.Max(1, populationSize), spawnList.Count);
        for (int index = 0; index < count; index++)
        {
            int selectedIndex = UnityEngine.Random.Range(0, spawnList.Count);
            NpcResidentData residentData = spawnList[selectedIndex];
            spawnList.RemoveAt(selectedIndex);

            if (!TryGetSpawnPosition(out Vector3 spawnPosition))
            {
                Debug.Log("npc population could not find a spawn position on the nav mesh", this);
                continue;
            }

            NpcController resident = Instantiate(npcPrefab, spawnPosition, Quaternion.identity, transform);
            resident.Configure(residentData.behaviorType, residentData.residentName, residentData.deliveryAddress,
                residentData.homeEntrance, houseEntrances, shopSpots, parkSpots, activityVisitChance,
                roamingRadius, minTimeRoaming, maxTimeRoaming);
            activeResidents.Add(resident);
            spawnPositions.Add(spawnPosition);
        }
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPosition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector3 candidate;
            bool foundPosition = false;
            if (spawnAcrossWholeNavMesh)
            {
                foundPosition = NpcNavMeshUtility.TryGetRandomPosition(out candidate);
            }
            else
            {
                Vector3 center = transform.position;
                if (player != null)
                {
                    center = player.position;
                }
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
                Vector3 nearbyCandidate = center + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(nearbyCandidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                {
                    candidate = hit.position;
                    foundPosition = true;
                }
                else
                {
                    candidate = Vector3.zero;
                }
            }

            if (foundPosition && IsFarEnoughFromOtherSpawnPoints(candidate))
            {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private bool IsFarEnoughFromOtherSpawnPoints(Vector3 candidate)
    {
        foreach (Vector3 existingPosition in spawnPositions)
        {
            if (Vector3.Distance(candidate, existingPosition) < minimumSpawnSeparation)
            {
                return false;
            }
        }
        return true;
    }
}
