using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class cyclemanager : MonoBehaviour
{
    [Header("⏱️ Time Progression")]
    [Tooltip("Current time of day in 24-hour format.")]
    [Range(0f, 24f)] public float currentTime = 10f;

    [Tooltip("Speed multiplier for time progression.")]
    public float timeSpeed = 0.2f;

    [Space]
    [Header("🕒 Current Time Display")]
    [Tooltip("Formatted time string for UI or display.")]
    public string currentTimeString;

    [Space]
    [Header("☀️ Sun Configuration")]
    public Light sunLight;

    [Tooltip("Sun latitude angle.")]
    [Range(0f, 90f)] public float sunLatitude = 20f;

    [Tooltip("Sun longitude angle.")]
    [Range(-180f, 180f)] public float sunLongitude = -90f;

    [Tooltip("Base intensity for sun.")]
    public float sunIntensity = 1.5f;

    public AnimationCurve sunIntensityMultiplier = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.2f),
        new Keyframe(0.5f, 1f),
        new Keyframe(0.75f, 0.2f),
        new Keyframe(1f, 0f));

    public AnimationCurve sunTemperatureCurve = new AnimationCurve(
        new Keyframe(0f, 0.25f),
        new Keyframe(0.25f, 0.35f),
        new Keyframe(0.5f, 0.65f),
        new Keyframe(0.75f, 0.35f),
        new Keyframe(1f, 0.25f));

    [Space]
    [Header("🌙 Moon Configuration")]
    public Light moonLight;

    [Tooltip("Moon latitude angle.")]
    [Range(0f, 90f)] public float moonLatitude = 40f;

    [Tooltip("Moon longitude angle.")]
    [Range(-180f, 180f)] public float moonLongitude = 90f;

    [Tooltip("Base intensity for moon.")]
    public float moonIntensity = 0.35f;

    public AnimationCurve moonIntensityMultiplier = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.25f, 0.1f),
        new Keyframe(0.5f, 0f),
        new Keyframe(0.75f, 0.1f),
        new Keyframe(1f, 1f));

    public AnimationCurve moonTemperatureCurve = new AnimationCurve(
        new Keyframe(0f, 0.85f),
        new Keyframe(0.5f, 0.65f),
        new Keyframe(1f, 0.85f));

    [Space]
    [Header("🌕 Lunar Cycle Settings")]
    [Range(1, 30)] public int lunarDay = 15;

    [Tooltip("Speed at which the lunar day changes.")]
    public float lunarCycleSpeed = 1f;

    [Tooltip("Reference to moon visual model.")]
    public Transform moonModel;

    [Space]
    [Header("🔁 Status Flags (Read-Only)")]
    public bool isDay = true;
    public bool sunActive = true;
    public bool moonActive = true;

    void Start()
    {
        NormalizeLegacyHdrpValues();
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

    void Update()
    {
        currentTime += Time.deltaTime * timeSpeed;

        if (currentTime >= 24f)
        {
            currentTime = 0f;
            lunarDay = (lunarDay % 30) + 1;
        }

        UpdateTimeText();
        UpdateLight();
        CheckShadowStatus();
        UpdateMoonPhase();
    }

    private void OnValidate()
    {
        NormalizeLegacyHdrpValues();
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

        RenderSettings.ambientIntensity = Mathf.Lerp(0.3f, 1.0f, sunCurve);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.25f, 1.0f, sunCurve);
    }

    void CheckShadowStatus()
    {
        float t = currentTime;
        isDay = t >= 6f && t <= 18f;

        if (sunLight != null)
        {
            sunLight.shadows = isDay ? LightShadows.Soft : LightShadows.None;
            sunActive = t >= 5.7f && t <= 18.3f;
            if (!sunLight.gameObject.activeSelf)
                sunLight.gameObject.SetActive(true);
        }

        if (moonLight != null)
        {
            moonLight.shadows = !isDay ? LightShadows.Soft : LightShadows.None;
            moonActive = !(t >= 6.3f && t <= 17.7f);
            if (Application.isPlaying)
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
