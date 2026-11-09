using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class NpcNavMeshUtility
{
    private struct NavMeshTriangle
    {
        public Vector3 first;
        public Vector3 second;
        public Vector3 third;
        public float cumulativeArea;
    }

    private static List<NavMeshTriangle> triangles = new List<NavMeshTriangle>();
    private static float totalArea;
    private static bool hasBuiltTriangles;

    public static bool TryGetRandomPosition(out Vector3 position)
    {
        BuildTrianglesIfNeeded();
        if (triangles.Count == 0 || totalArea <= 0f)
        {
            position = Vector3.zero;
            return false;
        }

        float selectedArea = Random.Range(0f, totalArea);
        NavMeshTriangle selectedTriangle = triangles[triangles.Count - 1];
        foreach (NavMeshTriangle triangle in triangles)
        {
            if (selectedArea <= triangle.cumulativeArea)
            {
                selectedTriangle = triangle;
                break;
            }
        }

        float firstWeight = Random.value;
        float secondWeight = Random.value;
        if (firstWeight + secondWeight > 1f)
        {
            firstWeight = 1f - firstWeight;
            secondWeight = 1f - secondWeight;
        }

        Vector3 candidate = selectedTriangle.first;
        candidate += (selectedTriangle.second - selectedTriangle.first) * firstWeight;
        candidate += (selectedTriangle.third - selectedTriangle.first) * secondWeight;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            position = hit.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    public static bool TryGetReachablePosition(NavMeshAgent agent, float roamingRadius, out Vector3 position)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            position = Vector3.zero;
            return false;
        }

        float baseRadius = Mathf.Max(1f, roamingRadius);
        for (int radiusStep = 0; radiusStep < 4; radiusStep++)
        {
            float currentRadius = baseRadius / Mathf.Pow(2f, radiusStep);
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * currentRadius;
                Vector3 candidate = agent.nextPosition + new Vector3(offset.x, 0f, offset.y);
                float sampleDistance = Mathf.Max(2f, currentRadius * 0.2f);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, agent.areaMask))
                {
                    continue;
                }

                float minimumDistance = Mathf.Min(4f, currentRadius * 0.5f);
                if (Vector3.Distance(agent.nextPosition, hit.position) < minimumDistance)
                {
                    continue;
                }

                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    position = hit.position;
                    return true;
                }
            }
        }

        position = Vector3.zero;
        return false;
    }

    private static void BuildTrianglesIfNeeded()
    {
        if (hasBuiltTriangles)
        {
            return;
        }

        hasBuiltTriangles = true;
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        for (int index = 0; index + 2 < triangulation.indices.Length; index += 3)
        {
            Vector3 first = triangulation.vertices[triangulation.indices[index]];
            Vector3 second = triangulation.vertices[triangulation.indices[index + 1]];
            Vector3 third = triangulation.vertices[triangulation.indices[index + 2]];
            float area = Vector3.Cross(second - first, third - first).magnitude * 0.5f;
            if (area <= 0.001f)
            {
                continue;
            }

            totalArea += area;
            NavMeshTriangle triangle = new NavMeshTriangle();
            triangle.first = first;
            triangle.second = second;
            triangle.third = third;
            triangle.cumulativeArea = totalArea;
            triangles.Add(triangle);
        }
    }
}
