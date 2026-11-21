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
    [SerializeField] private List<NpcController> npcPrefabs = new List<NpcController>();
    [SerializeField] private Transform player;
    [SerializeField, Min(1)] private int populationSize = 8;
    [SerializeField] private bool spawnAcrossWholeNavMesh = true;
    [SerializeField, Min(0f)] private float spawnRadius = 40f;
    [SerializeField, Min(0.1f)] private float sampleRadius = 4f;
    [SerializeField, Min(0f)] private float minimumSpawnSeparation = 5f;
    [SerializeField, Min(0f)] private float maxSpawnHeightDifference = 1.5f;
    [SerializeField, Min(0f)] private float maxRoamingHeightDifference = 1.5f;
    [SerializeField, Min(0f)] private float playerStopPadding = 0.8f;
    [SerializeField, Range(0f, 1f)] private float activityVisitChance = 0.2f;
    [SerializeField, Min(1f)] private float roamingRadius = 35f;
    [SerializeField, Min(0f)] private float minTimeRoaming = 2f;
    [SerializeField, Min(0f)] private float maxTimeRoaming = 8f;
    [SerializeField] private Transform[] houseEntrances;
    [SerializeField] private Transform[] shopSpots;
    [SerializeField] private List<NpcResidentData> residents = new List<NpcResidentData>();

    private readonly List<NpcController> activeResidents = new List<NpcController>();
    private readonly List<Vector3> spawnPositions = new List<Vector3>();

    public IReadOnlyList<NpcController> ActiveResidents { get { return activeResidents; } }

    private void Start()
    {
        EnsurePlayerObstacle();
        SpawnPopulation();
    }

    private void EnsurePlayerObstacle()
    {
        if (player == null)
        {
            StarterAssets.FirstPersonController playerController = FindAnyObjectByType<StarterAssets.FirstPersonController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }

        if (player == null)
        {
            Debug.LogWarning("npc spawner could not find the player; NPC avoidance is disabled", this);
            return;
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogWarning("player needs a CharacterController for NPC avoidance", player);
            return;
        }

        NavMeshObstacle obstacle = player.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = player.gameObject.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = characterController.center;
        obstacle.radius = characterController.radius + playerStopPadding;
        obstacle.height = characterController.height;
        obstacle.carving = false;
        obstacle.enabled = true;
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
        if (GetAvailableNpcPrefabCount() == 0 || residents.Count == 0 || houseEntrances == null || houseEntrances.Length < 2)
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

            NpcController selectedPrefab = GetRandomNpcPrefab();
            NpcController resident = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity, transform);
            resident.Configure(residentData.behaviorType, residentData.residentName, residentData.deliveryAddress,
                residentData.homeEntrance, houseEntrances, shopSpots, activityVisitChance,
                roamingRadius, minTimeRoaming, maxTimeRoaming, player,
                maxRoamingHeightDifference, playerStopPadding);
            activeResidents.Add(resident);
            spawnPositions.Add(spawnPosition);
        }
    }

    private int GetAvailableNpcPrefabCount()
    {
        int count = 0;
        foreach (NpcController prefab in npcPrefabs)
        {
            if (prefab != null)
            {
                count++;
            }
        }

        if (count == 0 && npcPrefab != null)
        {
            count = 1;
        }
        return count;
    }

    private NpcController GetRandomNpcPrefab()
    {
        int count = GetAvailableNpcPrefabCount();
        if (count == 1 && npcPrefabs.Count == 0)
        {
            return npcPrefab;
        }

        int selectedIndex = UnityEngine.Random.Range(0, count);
        foreach (NpcController prefab in npcPrefabs)
        {
            if (prefab == null)
            {
                continue;
            }
            if (selectedIndex == 0)
            {
                return prefab;
            }
            selectedIndex--;
        }

        return npcPrefab;
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

            float groundHeight = player != null ? player.position.y : transform.position.y;
            if (foundPosition && Mathf.Abs(candidate.y - groundHeight) <= maxSpawnHeightDifference &&
                IsFarEnoughFromOtherSpawnPoints(candidate))
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
