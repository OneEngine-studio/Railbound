using System;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// In-game pause modal with tactical menu options (Resume, Settings, Restart, Leave).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenuModal : MonoBehaviour
    {
        [Header("Screen Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Menu Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button leaveMatchButton;

        public event Action OnResumeRequested;
        public event Action OnSettingsRequested;
        public event Action OnRestartRequested;
        public event Action OnLeaveMatchRequested;

        private void Awake()
        {
            if (transition == null) transition = GetComponent<UIScreenTransition>();

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => OnSettingsRequested?.Invoke());
            if (restartButton != null) restartButton.onClick.AddListener(() => OnRestartRequested?.Invoke());
            if (leaveMatchButton != null) leaveMatchButton.onClick.AddListener(() => OnLeaveMatchRequested?.Invoke());
        }

        public void Show()
        {
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }

        public void Resume()
        {
            OnResumeRequested?.Invoke();
            Hide();
        }
    }
}
