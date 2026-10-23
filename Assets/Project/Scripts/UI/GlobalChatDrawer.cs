using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Slide-out global / squad communication drawer.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GlobalChatDrawer : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private UIScreenTransition transition;

        [Header("Controls")]
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform messageContainer;

        public event Action<string> OnMessageSent;

        private void Awake()
        {
            if (transition == null) transition = GetComponent<UIScreenTransition>();

            if (sendButton != null) sendButton.onClick.AddListener(SendMessage);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (messageInput != null) messageInput.onSubmit.AddListener(_ => SendMessage());
        }

        public void Open()
        {
            if (transition != null) transition.FadeIn();
            else gameObject.SetActive(true);

            if (messageInput != null) messageInput.ActivateInputField();
        }

        public void Close()
        {
            if (transition != null) transition.FadeOut();
            else gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (gameObject.activeSelf) Close();
            else Open();
        }

        public void SendMessage()
        {
            if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text)) return;
            string msg = messageInput.text.Trim();
            messageInput.text = string.Empty;
            OnMessageSent?.Invoke(msg);
            messageInput.ActivateInputField();
        }
    }
}
