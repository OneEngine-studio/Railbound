using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modal overlay for Audio, Graphics, and Controls settings.
    /// </summary>
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
            if (audioTabButton != null) audioTabButton.onClick.AddListener(() => SwitchTab(0));
            if (graphicsTabButton != null) graphicsTabButton.onClick.AddListener(() => SwitchTab(1));
            if (controlsTabButton != null) controlsTabButton.onClick.AddListener(() => SwitchTab(2));

            if (applyButton != null) applyButton.onClick.AddListener(() => OnApply?.Invoke());
            if (resetDefaultsButton != null) resetDefaultsButton.onClick.AddListener(() => OnResetDefaults?.Invoke());
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            SwitchTab(0);
        }

        public void Show()
        {
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
    }
}
