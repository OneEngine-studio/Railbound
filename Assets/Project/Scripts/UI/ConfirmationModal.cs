using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Reusable tactical confirmation prompt for quit, leave match, or overwrite actions.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ConfirmationModal : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Content")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        private void Awake()
        {
            if (transition == null) transition = GetComponent<UIScreenTransition>();

            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        }

        public void Show(string title, string message, Action onConfirm, Action onCancel = null)
        {
            if (titleText != null) titleText.text = title;
            if (messageText != null) messageText.text = message;

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);
        }

        private void OnConfirmClicked()
        {
            _onConfirm?.Invoke();
            Hide();
        }

        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
            Hide();
        }

        public void Hide()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }
    }
}
