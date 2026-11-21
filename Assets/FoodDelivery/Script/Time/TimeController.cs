using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeController : MonoBehaviour
{
    private static readonly string[] WeekdayNames =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    [SerializeField]
    private float timeMultiplier;
    [SerializeField]
    private float startHour;
    [SerializeField]
    private TextMeshProUGUI timeText;
    [SerializeField]
    private TextMeshProUGUI dateText;
    [SerializeField]
    private Image dayNightIcon;
    [SerializeField]
    private Sprite sunIcon;
    [SerializeField]
    private Sprite moonIcon;
    [SerializeField]
    private int startYear = 2026;
    [SerializeField]
    private int startMonth = 11;
    [SerializeField]
    private int startDay = 15;
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
    private DateTime firstGameDate;
    private TimeSpan sunriseTime;
    private TimeSpan sunsetTime;
    private bool daytimeIconVisible;
    private bool hasUpdatedDayNightIcon;

    public DateTime CurrentTime
    {
        get
        {
            return currentTime;
        }
    }
    private void Start()
    {
        firstGameDate = new DateTime(startYear, startMonth, startDay);
        currentTime = firstGameDate.AddHours(startHour);
        sunriseTime = TimeSpan.FromHours(sunriseHour);
        sunsetTime = TimeSpan.FromHours(sunsetHour);
        UpdateClockUI();
        if (RenderSettings.skybox != null)
        {
            skyboxMaterial = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxMaterial;
        }
    }

    private void Update()
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
        UpdateClockUI();
    }

    private void UpdateClockUI()
    {
        if (dateText != null)
        {
            int weekdayIndex = (int)((currentTime.Date - firstGameDate.Date).TotalDays % WeekdayNames.Length);
            dateText.text = WeekdayNames[weekdayIndex] + " | " + currentTime.ToString("yyyy.dd.MM", CultureInfo.InvariantCulture);
        }

        bool isDaytime = currentTime.Hour >= 5 && currentTime.Hour < 18;
        if (dayNightIcon != null && (!hasUpdatedDayNightIcon || daytimeIconVisible != isDaytime))
        {
            daytimeIconVisible = isDaytime;
            hasUpdatedDayNightIcon = true;
            if (isDaytime)
            {
                dayNightIcon.sprite = sunIcon;
            }
            else
            {
                dayNightIcon.sprite = moonIcon;
            }
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
