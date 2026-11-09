using System;
using TMPro;
using UnityEngine;

public class TimeController : MonoBehaviour
{
    [SerializeField]
    private float timeMultiplier;
    [SerializeField]
    private float startHour;
    [SerializeField]
    private TextMeshProUGUI timeText;
    [SerializeField]
    private Light sunLight;
    [SerializeField]
    private float sunriseHour;
    [SerializeField]
    private float sunsetHour;
    [SerializeField]
    private Color dayAmbientLight;
    [SerializeField]
    private Color nightAmbientLight;
    [SerializeField]
    private AnimationCurve lightChangeCurve;
    [SerializeField]
    private float maxSunLightIntensity;
    [SerializeField]
    private Light moonLight;
    [SerializeField]
    private float maxMoonLightIntensity;

    [Header("Skybox")]
    [SerializeField]
    private float dayExposure = 1f;
    [SerializeField]
    private float nightExposure = 0.1f;
    private Material skyboxMaterial;

    private DateTime currentTime;
    private TimeSpan sunriseTime;
    private TimeSpan sunsetTime;

    public DateTime CurrentTime
    {
        get
        {
            return currentTime;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentTime = DateTime.Now.Date + TimeSpan.FromHours(startHour);
        sunriseTime = TimeSpan.FromHours(sunriseHour);
        sunsetTime = TimeSpan.FromHours(sunsetHour);
        if (RenderSettings.skybox != null)
        {
            skyboxMaterial = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxMaterial;
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateTimeOfDay();
        RotateSun();
        UpdateLightSetting();
    }

    private void UpdateTimeOfDay()
    {
        currentTime = currentTime.AddSeconds(Time.deltaTime * timeMultiplier);
        if (timeText != null)
        {
            timeText.text = currentTime.ToString("HH:mm");
        }
    }

    private void RotateSun()
    {
        float rotation = CalculateCelestialRotation();
        sunLight.transform.rotation = Quaternion.AngleAxis(rotation, Vector3.right);
        moonLight.transform.rotation = Quaternion.AngleAxis(rotation + 180f, Vector3.right);
    }

    private float CalculateCelestialRotation()
    {
        float rotation;
        if (currentTime.TimeOfDay > sunriseTime && currentTime.TimeOfDay < sunsetTime)
        {
            TimeSpan sunriseToSunsetDuration = CalculateTimeDifference(sunriseTime, sunsetTime);
            TimeSpan timeSinceSunrise = CalculateTimeDifference(sunriseTime, currentTime.TimeOfDay);
            double percentage = timeSinceSunrise.TotalMinutes / sunriseToSunsetDuration.TotalMinutes;
            rotation = Mathf.Lerp(0, 180, (float)percentage);
        }
        else
        {
            TimeSpan sunsetToSunriseDuration = CalculateTimeDifference(sunsetTime, sunriseTime);
            TimeSpan timeSinceSunset = CalculateTimeDifference(sunsetTime, currentTime.TimeOfDay);
            double percentage = timeSinceSunset.TotalMinutes / sunsetToSunriseDuration.TotalMinutes;
            rotation = Mathf.Lerp(180, 360, (float)percentage);
        }
        return rotation;
    }

    private TimeSpan CalculateTimeDifference(TimeSpan fromTime, TimeSpan toTime)
    {
        TimeSpan difference = toTime - fromTime;
        if (difference.TotalSeconds < 0)
        {
            difference += TimeSpan.FromHours(24);
        }
        return difference;
    }

    private void UpdateLightSetting()
    {
        float dotProduct = Vector3.Dot(sunLight.transform.forward, Vector3.down);
        float t = lightChangeCurve.Evaluate(dotProduct);

        sunLight.intensity = Mathf.Lerp(0, maxSunLightIntensity, t);
        moonLight.intensity = Mathf.Lerp(maxMoonLightIntensity, 0, t);
        RenderSettings.ambientLight = Color.Lerp(nightAmbientLight, dayAmbientLight, t);

        if (skyboxMaterial != null && skyboxMaterial.HasProperty("_Exposure"))
        {
            skyboxMaterial.SetFloat("_Exposure", Mathf.Lerp(nightExposure, dayExposure, t));
        }
    }
}
