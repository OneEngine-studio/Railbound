using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CycleManager : MonoBehaviour
{
    public enum DayCycleMode
    {
        OnlyDay,
        FullDayNight
    }

    [Header("☀️ Day / Night Mode Option")]
    [Tooltip("Choose whether the environment is locked to Day only or runs the full 24-hour cycle.")]
    public DayCycleMode cycleMode = DayCycleMode.OnlyDay;

    [Tooltip("Hour of the day when locked to Day (e.g. 11.5 = high sun / golden noon).")]
    [Range(6f, 18f)] public float dayLockedTime = 11.5f;

    [Space]
    [Header("⏱️ Time Progression")]
    [Tooltip("Current time of day in 24-hour format.")]
    [Range(0f, 24f)] public float currentTime = 11.5f;

    [Tooltip("Speed multiplier for time progression (used when FullDayNight is selected).")]
    public float timeSpeed = 0.2f;

    [Tooltip("Speed of lighting transitions.")]
    public float transitionSpeed = 2f;

    [Space]
    [Header("🕒 Current Time Display")]
    [Tooltip("Formatted time string for UI or display.")]
    public string currentTimeString;

    [Space]
    [Header("☀️ Sun Configuration")]
    public Light sunLight;
    [Range(0f, 90f)] public float sunLatitude = 20f;
    [Range(-180f, 180f)] public float sunLongitude = -90f;
    public float sunIntensity = 1.5f;
    public AnimationCurve sunIntensityMultiplier;
    public AnimationCurve sunTemperatureCurve;

    [Space]
    [Header("🌙 Moon Configuration")]
    public Light moonLight;
    [Range(0f, 90f)] public float moonLatitude = 40f;
    [Range(-180f, 180f)] public float moonLongitude = 90f;
    public float moonIntensity = 0.35f;
    public AnimationCurve moonIntensityMultiplier;
    public AnimationCurve moonTemperatureCurve;

    [Space]
    [Header("🌕 Lunar Cycle Settings")]
    [Range(1, 30)] public int lunarDay = 15;
    public Transform moonModel;

    [Space]
    [Header("🔁 Status Flags (Read-Only)")]
    public bool isDay = true;
    public bool sunActive = true;
    public bool moonActive = false;

    void Start()
    {
        NormalizeLegacyHdrpValues();
        SyncDayNightState();
        UpdateTimeText();
        UpdateLight();
        CheckShadowStatus();
        UpdateMoonPhase();
    }

    void NormalizeLegacyHdrpValues()
    {
        if (sunIntensity > 10f) sunIntensity = 1.5f;
        if (moonIntensity > 5f) moonIntensity = 0.35f;
    }

    void SyncDayNightState()
    {
        if (cycleMode == DayCycleMode.OnlyDay)
        {
            currentTime = dayLockedTime;
        }
    }

    void Update()
    {
        if (cycleMode == DayCycleMode.FullDayNight)
        {
            currentTime += Time.deltaTime * timeSpeed;

            if (currentTime >= 24f)
            {
                currentTime = 0f;
                lunarDay = (lunarDay % 30) + 1;
            }
        }

        UpdateTimeText();
        UpdateLight();
        CheckShadowStatus();
        UpdateMoonPhase();
    }

    private void OnValidate()
    {
        NormalizeLegacyHdrpValues();
        SyncDayNightState();
        UpdateTimeText();
        UpdateLight();
        CheckShadowStatus();
        UpdateMoonPhase();
    }

    void UpdateTimeText()
    {
        currentTimeString = Mathf.Floor(currentTime).ToString("00") + ":" + ((currentTime % 1) * 60).ToString("00");
    }

    void UpdateLight()
    {
        float sunRotation = currentTime / 24f * 360f;
        float moonRotation = (currentTime + 12f) / 24f * 360f;

        if (sunLight != null)
        {
            sunLight.transform.localRotation = Quaternion.Euler(sunLatitude - 90f, sunLongitude, 0f) * Quaternion.Euler(0f, sunRotation, 0f);
        }

        if (moonLight != null)
        {
            moonLight.transform.localRotation = Quaternion.Euler(moonLatitude - 90f, moonLongitude, 0f) * Quaternion.Euler(0f, moonRotation, 0f);
        }

        float normalizedTime = Mathf.Clamp01(currentTime / 24f);
        float sunCurve = (sunIntensityMultiplier != null && sunIntensityMultiplier.length > 0)
            ? sunIntensityMultiplier.Evaluate(normalizedTime)
            : Mathf.Clamp01(Mathf.Sin((normalizedTime - 0.25f) * Mathf.PI * 2f));

        float moonCurve = (moonIntensityMultiplier != null && moonIntensityMultiplier.length > 0)
            ? moonIntensityMultiplier.Evaluate(normalizedTime)
            : Mathf.Clamp01(-Mathf.Sin((normalizedTime - 0.25f) * Mathf.PI * 2f));

        if (sunLight != null)
        {
            sunLight.useColorTemperature = true;
            sunLight.intensity = sunCurve * sunIntensity;
            if (sunTemperatureCurve != null && sunTemperatureCurve.length > 0)
            {
                sunLight.colorTemperature = Mathf.Clamp(sunTemperatureCurve.Evaluate(normalizedTime) * 10000f, 1500f, 20000f);
            }
        }

        if (moonLight != null)
        {
            moonLight.useColorTemperature = true;
            float phaseMultiplier = Mathf.Max(Mathf.Sin((float)lunarDay / 30f * Mathf.PI), 0.15f);
            moonLight.intensity = moonCurve * moonIntensity * phaseMultiplier;
            if (moonTemperatureCurve != null && moonTemperatureCurve.length > 0)
            {
                moonLight.colorTemperature = Mathf.Clamp(moonTemperatureCurve.Evaluate(normalizedTime) * 10000f, 1500f, 20000f);
            }
        }

        float targetAmbient = Mathf.Lerp(0.3f, 1.0f, sunCurve);
        float targetReflection = Mathf.Lerp(0.25f, 1.0f, sunCurve);

        RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, targetAmbient, Time.deltaTime * transitionSpeed);
        RenderSettings.reflectionIntensity = Mathf.Lerp(RenderSettings.reflectionIntensity, targetReflection, Time.deltaTime * transitionSpeed);
    }

    void CheckShadowStatus()
    {
        float t = currentTime;
        if (cycleMode == DayCycleMode.OnlyDay)
        {
            isDay = true;
            sunActive = true;
            moonActive = false;
        }
        else
        {
            isDay = t >= 6f && t <= 18f;
            sunActive = t >= 5.7f && t <= 18.3f;
            moonActive = !(t >= 6.3f && t <= 17.7f);
        }

        if (sunLight != null)
        {
            sunLight.shadows = isDay ? LightShadows.Soft : LightShadows.None;
            if (!sunLight.gameObject.activeSelf)
                sunLight.gameObject.SetActive(true);
        }

        if (moonLight != null)
        {
            moonLight.shadows = !isDay ? LightShadows.Soft : LightShadows.None;
            moonLight.gameObject.SetActive(moonActive);
        }
    }

    void UpdateMoonPhase()
    {
        float phase = (lunarDay - 1) / 29f;
        float scale = Mathf.Sin(phase * Mathf.PI);
        scale = Mathf.Max(scale, 0.1f);

        if (moonModel != null)
            moonModel.localScale = Vector3.one * scale;
    }
}
