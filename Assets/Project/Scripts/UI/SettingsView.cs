using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modal overlay for Audio, Graphics, and Controls settings with full persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsView : MonoBehaviour
    {
        [Header("Screen Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Tab Navigation")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button graphicsTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private GameObject audioTabContent;
        [SerializeField] private GameObject graphicsTabContent;
        [SerializeField] private GameObject controlsTabContent;

        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider voiceVolumeSlider;

        [Header("Graphics Settings")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown fpsLimitDropdown;

        [Header("Controls Settings")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Toggle invertYToggle;

        [Header("Bottom Actions")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetDefaultsButton;
        [SerializeField] private Button closeButton;

        public event Action OnApply;
        public event Action OnResetDefaults;
        public event Action OnClose;

        private void Awake()
        {
            if (transition == null) transition = GetComponent<UIScreenTransition>();

            if (audioTabButton != null) audioTabButton.onClick.AddListener(() => SwitchTab(0));
            if (graphicsTabButton != null) graphicsTabButton.onClick.AddListener(() => SwitchTab(1));
            if (controlsTabButton != null) controlsTabButton.onClick.AddListener(() => SwitchTab(2));

            if (applyButton != null) applyButton.onClick.AddListener(ApplySettings);
            if (resetDefaultsButton != null) resetDefaultsButton.onClick.AddListener(ResetDefaults);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            LoadSettings();
            SwitchTab(0);
        }

        public void Show()
        {
            LoadSettings();
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            OnClose?.Invoke();
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }

        public void SwitchTab(int tabIndex)
        {
            if (audioTabContent != null) audioTabContent.SetActive(tabIndex == 0);
            if (graphicsTabContent != null) graphicsTabContent.SetActive(tabIndex == 1);
            if (controlsTabContent != null) controlsTabContent.SetActive(tabIndex == 2);
        }

        public void LoadSettings()
        {
            float master = PlayerPrefs.GetFloat("Settings_MasterVol", 1.0f);
            float sfx = PlayerPrefs.GetFloat("Settings_SfxVol", 0.85f);
            float music = PlayerPrefs.GetFloat("Settings_MusicVol", 0.70f);
            float voice = PlayerPrefs.GetFloat("Settings_VoiceVol", 0.80f);

            if (masterVolumeSlider != null) masterVolumeSlider.value = master;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfx;
            if (musicVolumeSlider != null) musicVolumeSlider.value = music;
            if (voiceVolumeSlider != null) voiceVolumeSlider.value = voice;

            int quality = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
            if (qualityDropdown != null) qualityDropdown.value = Mathf.Clamp(quality, 0, qualityDropdown.options.Count - 1);

            bool fullscreen = PlayerPrefs.GetInt("Settings_Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            if (fullscreenToggle != null) fullscreenToggle.isOn = fullscreen;

            int fpsLimit = PlayerPrefs.GetInt("Settings_FpsLimit", 0);
            if (fpsLimitDropdown != null) fpsLimitDropdown.value = fpsLimit;

            float sensitivity = PlayerPrefs.GetFloat("Settings_Sensitivity", 1.0f);
            if (sensitivitySlider != null) sensitivitySlider.value = sensitivity;

            bool invertY = PlayerPrefs.GetInt("Settings_InvertY", 0) == 1;
            if (invertYToggle != null) invertYToggle.isOn = invertY;
        }

        public void ApplySettings()
        {
            if (masterVolumeSlider != null)
            {
                AudioListener.volume = masterVolumeSlider.value;
                PlayerPrefs.SetFloat("Settings_MasterVol", masterVolumeSlider.value);
            }
            if (sfxVolumeSlider != null) PlayerPrefs.SetFloat("Settings_SfxVol", sfxVolumeSlider.value);
            if (musicVolumeSlider != null) PlayerPrefs.SetFloat("Settings_MusicVol", musicVolumeSlider.value);
            if (voiceVolumeSlider != null) PlayerPrefs.SetFloat("Settings_VoiceVol", voiceVolumeSlider.value);

            if (qualityDropdown != null)
            {
                QualitySettings.SetQualityLevel(qualityDropdown.value);
                PlayerPrefs.SetInt("Settings_Quality", qualityDropdown.value);
            }

            if (fullscreenToggle != null)
            {
                Screen.fullScreen = fullscreenToggle.isOn;
                PlayerPrefs.SetInt("Settings_Fullscreen", fullscreenToggle.isOn ? 1 : 0);
            }

            if (fpsLimitDropdown != null)
            {
                PlayerPrefs.SetInt("Settings_FpsLimit", fpsLimitDropdown.value);
                switch (fpsLimitDropdown.value)
                {
                    case 0: Application.targetFrameRate = 60; break;
                    case 1: Application.targetFrameRate = 120; break;
                    case 2: Application.targetFrameRate = 144; break;
                    case 3: Application.targetFrameRate = -1; break;
                }
            }

            if (sensitivitySlider != null) PlayerPrefs.SetFloat("Settings_Sensitivity", sensitivitySlider.value);
            if (invertYToggle != null) PlayerPrefs.SetInt("Settings_InvertY", invertYToggle.isOn ? 1 : 0);

            PlayerPrefs.Save();
            OnApply?.Invoke();
            Debug.Log("[SettingsView] Settings successfully applied and saved.");
        }

        public void ResetDefaults()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.value = 1.0f;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 0.85f;
            if (musicVolumeSlider != null) musicVolumeSlider.value = 0.70f;
            if (voiceVolumeSlider != null) voiceVolumeSlider.value = 0.80f;

            if (qualityDropdown != null) qualityDropdown.value = 2; // High
            if (fullscreenToggle != null) fullscreenToggle.isOn = true;
            if (fpsLimitDropdown != null) fpsLimitDropdown.value = 0; // 60 FPS

            if (sensitivitySlider != null) sensitivitySlider.value = 1.0f;
            if (invertYToggle != null) invertYToggle.isOn = false;

            ApplySettings();
            OnResetDefaults?.Invoke();
            Debug.Log("[SettingsView] Settings reset to tactical defaults.");
        }
    }
}
