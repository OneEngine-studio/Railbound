using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Tactical callsig/operator name prompt before entering as guest.
    /// Validates 3-16 alphanumeric characters, saves to PlayerPrefs, and transitions to GameLobby.
    /// </summary>
    public class GuestNameModalController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_InputField callsignInput;
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private UIScreenTransition transition;

        [Header("Configuration")]
        [SerializeField] private string lobbySceneName = "GameLobby";
        [SerializeField] private int minLength = 3;
        [SerializeField] private int maxLength = 16;
        [SerializeField] private string defaultCallsign = "Callsign_01";

        private static readonly Regex CallsignRegex = new Regex(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

        private void Awake()
        {
            if (transition == null) transition = GetComponent<UIScreenTransition>();

            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);

            if (warningText != null) warningText.gameObject.SetActive(false);
            if (callsignInput != null)
            {
                callsignInput.characterLimit = maxLength;
                callsignInput.onSubmit.AddListener(_ => OnConfirmClicked());
            }
        }

        public void Show()
        {
            if (warningText != null) warningText.gameObject.SetActive(false);

            if (callsignInput != null)
            {
                string savedName = PlayerPrefs.GetString("PlayerName", defaultCallsign);
                callsignInput.text = string.IsNullOrWhiteSpace(savedName) ? defaultCallsign : savedName;
                callsignInput.Select();
                callsignInput.ActivateInputField();
            }

            if (transition != null)
            {
                transition.FadeIn();
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (transition != null)
            {
                transition.FadeOut();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void OnConfirmClicked()
        {
            string callsign = callsignInput != null ? callsignInput.text.Trim() : string.Empty;

            if (string.IsNullOrEmpty(callsign) || callsign.Length < minLength || callsign.Length > maxLength || !CallsignRegex.IsMatch(callsign))
            {
                if (warningText != null)
                {
                    warningText.text = $"Callsign must be {minLength}-{maxLength} alphanumeric characters.";
                    warningText.gameObject.SetActive(true);
                }
                return;
            }

            if (warningText != null) warningText.gameObject.SetActive(false);

            // Save to PlayerPrefs
            PlayerPrefs.SetString("PlayerName", callsign);
            PlayerPrefs.SetString("GuestCallsign", callsign);
            PlayerPrefs.Save();
            Debug.Log($"[GuestLogin] Deployed as: {callsign}");

            // Initiate guest auth session in background if service is present
            if (AuthenticationService.Instance != null && !AuthenticationService.Instance.IsAuthenticated)
            {
                _ = AuthenticationService.Instance.LoginAsGuestAsync();
            }

            // Transition to GameLobby
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Debug.Log($"[GuestNameModalController] Validated in Editor test mode for scene: {lobbySceneName}");
                return;
            }
#endif
            if (UIManager.Instance != null)
            {
                if (transition != null)
                    transition.FadeOut(() => UIManager.Instance.LoadSceneAsync(lobbySceneName));
                else
                    UIManager.Instance.LoadSceneAsync(lobbySceneName);
            }
            else
            {
                if (transition != null)
                    transition.FadeOut(() => SceneManager.LoadSceneAsync(lobbySceneName));
                else
                    SceneManager.LoadSceneAsync(lobbySceneName);
            }
        }
    }
}
