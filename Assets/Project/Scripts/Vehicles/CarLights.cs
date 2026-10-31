using UnityEngine;

public class CarLights : MonoBehaviour
{
    [Header("References")]
    public Transform sun;
    public Light[] headLights; // Using Light components directly is faster than GameObjects

    [Header("Optimization Settings")]
    public float maxActivationDistance = 100f; // Only process lights if close to the player
    private Transform playerTransform;

    private bool lastNightState = false;
    private float checkInterval = 1f; // Optimization: Don't check every frame
    private float checkTimer = 0f;

    void Start()
    {
        // Automatically find the player by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if (sun == null)
        {
            // Fallback: Try to find the Directional Light automatically if unassigned
            GameObject sunObj = GameObject.Find("Directional Light");
            if (sunObj != null) sun = sunObj.transform; // Assigning to the 'sun' variable correctly
        }

        ValidateHeadlights();
    }

    /// <summary>
    /// Danh sách đèn pha có thể chứa tham chiếu hỏng (trong scene từng lưu GameObject thay vì Light ->
    /// Unity cảnh báo "Serialized reference type mismatch" và bật đèn thì lỗi MissingReferenceException).
    /// Gặp tham chiếu hỏng thì tự lấy lại các đèn spot / point nằm trong xe.
    /// </summary>
    private void ValidateHeadlights()
    {
        bool broken = headLights == null || headLights.Length == 0;
        if (!broken)
            foreach (Light l in headLights)
            {
                try { if (l == null) continue; var _ = l.type; }
                catch (System.Exception) { broken = true; break; }
            }
        if (!broken) return;

        var found = new System.Collections.Generic.List<Light>();
        foreach (Light l in GetComponentsInChildren<Light>(true))
            if (l.type == LightType.Spot || l.type == LightType.Point) found.Add(l);
        headLights = found.ToArray();
    }

    void Update()
    {
        if (sun == null) return;

        // OPTIMIZATION 1: Distance Culling
        // If the player is too far away, skip checking lights entirely to save CPU power.
        if (playerTransform != null)
        {
            float sqrDistanceToPlayer = (transform.position - playerTransform.position).sqrMagnitude;
            if (sqrDistanceToPlayer > (maxActivationDistance * maxActivationDistance))
            {
                return; // Too far, do nothing
            }
        }

        // OPTIMIZATION 2: Throttle the check timer
        // Checking the sun position 60 times a second is wasteful. Once every second is plenty.
        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        // Check if it is night time based on the sun's direction
        bool isNight = sun.forward.y > 0;

        // Only update lights if the night state actually changed (e.g., sunset/sunrise transition)
        if (isNight != lastNightState)
        {
            lastNightState = isNight;
            ToggleHeadlights(isNight);
        }
    }

    void ToggleHeadlights(bool turnOn)
    {
        foreach (Light lightComponent in headLights)
        {
            if (lightComponent == null) continue;
            try { lightComponent.enabled = turnOn; }
            catch (MissingReferenceException) { } // tham chiếu hỏng còn sót: bỏ qua thay vì báo lỗi mỗi lần đổi ngày / đêm
        }
    }   
}