using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Nakama;

namespace NakamaNetwork
{
    public class LoginManager : MonoBehaviour
    {
        [Header("Nakama Connection")]
        [SerializeField] private NakamaConnection nakamaConnection;
        
        [Header("Login UI Elements")]
        [SerializeField] private TMPro.TMP_InputField usernameInput;
        [SerializeField] private TMPro.TMP_InputField passwordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private Button moreButton;
        [SerializeField] private CanvasGroup accountLoginPopup;
        [SerializeField] private TMPro.TMP_Text loginStatusText;
        
        [Header("Privacy Policy")]
        [SerializeField] private GameObject privacyPolicyPopup;
        [SerializeField] private Button acceptPrivacyPolicyButton;
        [SerializeField] private TMPro.TMP_Text privacyPolicyText;

        private CanvasGroup _loginMenuCanvasGroup;
        private bool _isLoggingIn = false;

        public event Action<GameObject> OnLoginSuccess;
        public event Action OnLogout;

        private void Awake()
        {
            AutoWireReferences();

            if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
            if (guestButton != null) guestButton.onClick.AddListener(OnGuestClicked);
            if (moreButton != null) moreButton.onClick.AddListener(ShowAccountLoginPopup);
            
            if (accountLoginPopup == null && transform.parent != null)
            {
                _loginMenuCanvasGroup = transform.parent.Find("LoginMenuCanvasGroup")?.GetComponent<CanvasGroup>();
            }
        }

        private async void Start()
        {
            bool policyAccepted = await CheckPrivacyPolicyAsync();
            CheckPrivacyPolicyResult(policyAccepted);

            if (nakamaConnection != null)
            {
                try
                {
                    if (UserSessionExists() && nakamaConnection.IsSessionValid())
                    {
                        await nakamaConnection.ConnectAsync();
                        if (nakamaConnection.ConnectionState == ConnectionState.Connected)
                        {
                            OnLoginSuccess?.Invoke(gameObject);
                        }
                    }
                    else
                    {
                        await AutoConnectAsGuest();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LoginManager] Auto-connect failed: {ex.Message}");
                    UpdateLoginStatus("Connection error, please try again.");
                }
            }
        }

        private void OnDestroy()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginClicked);
            if (registerButton != null) registerButton.onClick.RemoveListener(OnRegisterClicked);
            if (guestButton != null) guestButton.onClick.RemoveListener(OnGuestClicked);
            if (moreButton != null) moreButton.onClick.RemoveListener(ShowAccountLoginPopup);
        }

        private void AutoWireReferences()
        {
            if (nakamaConnection == null)
            {
                nakamaConnection = FindFirstObjectByType<NakamaConnection>();
            }

            if (usernameInput == null) usernameInput = transform.Find("UsernameInputField")?.GetComponent<TMPro.TMP_InputField>();
            if (passwordInput == null) passwordInput = transform.Find("PasswordInputField")?.GetComponent<TMPro.TMP_InputField>();
            if (loginButton == null) loginButton = transform.Find("LoginButton")?.GetComponent<Button>();
            if (registerButton == null) registerButton = transform.Find("RegisterButton")?.GetComponent<Button>();
            if (guestButton == null) guestButton = transform.Find("GuestButton")?.GetComponent<Button>();
            if (loginStatusText == null) loginStatusText = transform.Find("LoginStatusText")?.GetComponent<TMPro.TMP_Text>();

            if (accountLoginPopup == null && transform.parent != null)
            {
                accountLoginPopup = transform.parent.Find("AccountLoginPopup")?.GetComponent<CanvasGroup>();
            }

            if (privacyPolicyPopup == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) privacyPolicyPopup = canvas.transform.Find("PrivacyPolicyPopup")?.gameObject;
            }

            if (privacyPolicyPopup != null)
            {
                if (acceptPrivacyPolicyButton == null) acceptPrivacyPolicyButton = privacyPolicyPopup.transform.Find("AcceptButton")?.GetComponent<Button>();
                if (privacyPolicyText == null) privacyPolicyText = privacyPolicyPopup.transform.Find("PolicyText")?.GetComponent<TMPro.TMP_Text>();
            }

            if (acceptPrivacyPolicyButton != null)
            {
                acceptPrivacyPolicyButton.onClick.AddListener(OnAcceptPrivacyPolicy);
            }

            EnableLoginButtons(true);
        }

        private async void OnLoginClicked() => await HandleAuthAsync(true, false);
        private async void OnRegisterClicked() => await HandleAuthAsync(true, true);
        private async void OnGuestClicked() => await HandleAuthAsync(false, false);

        private async Task HandleAuthAsync(bool isUserPass, bool createAccount)
        {
            if (_isLoggingIn) return;
            
            string username = usernameInput?.text?.Trim();
            string password = passwordInput?.text?.Trim();

            if (isUserPass && (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)))
            {
                UpdateLoginStatus("Please enter username and password");
                return;
            }

            _isLoggingIn = true;
            EnableLoginButtons(false);
            UpdateLoginStatus(isUserPass ? (createAccount ? "Creating account..." : "Logging in...") : "Connecting as guest...");

            try
            {
                if (isUserPass)
                    await AuthenticateUsernamePasswordAsync(username, password, createAccount, OnAuthComplete);
                else
                    await AuthenticateDeviceAsync(OnAuthComplete);
            }
            finally
            {
                _isLoggingIn = false;
                EnableLoginButtons(true);
            }
        }

        private void OnAcceptPrivacyPolicy()
        {
            if (nakamaConnection != null)
            {
                nakamaConnection.AcceptPrivacyPolicy();
                ClosePrivacyPolicyPopup();
                Debug.Log("[LoginManager] Privacy policy accepted");
            }
        }

        private Task<bool> CheckPrivacyPolicyAsync()
        {
            if (nakamaConnection != null)
            {
                nakamaConnection.CheckPrivacyPolicy((accepted, reason) => OnPrivacyPolicyChecked(accepted));
            }
            return Task.FromResult(nakamaConnection != null && nakamaConnection.HasAcceptedPrivacyPolicy());
        }

        private async Task<bool> AutoConnectAsGuest()
        {
            if (nakamaConnection == null) return false;
            try
            {
                await nakamaConnection.ConnectAsync();
                return nakamaConnection.ConnectionState == ConnectionState.Connected;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoginManager] Auto-connect as guest failed: {ex.Message}");
                return false;
            }
        }

        private async Task AuthenticateUsernamePasswordAsync(string username, string password, bool createAccount, Action<AuthResult, ISession, string> onComplete)
        {
            if (nakamaConnection == null) return;
            try
            {
                await nakamaConnection.AuthenticateUsernamePasswordAsync(username, password, createAccount, (result, session, error) => onComplete?.Invoke(result, session, error));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(AuthResult.NetworkError, null, ex.Message);
            }
        }

        private async Task AuthenticateDeviceAsync(Action<AuthResult, ISession, string> onComplete)
        {
            if (nakamaConnection == null) return;
            try
            {
                await nakamaConnection.AuthenticateDeviceAsync((result, session, error) => onComplete?.Invoke(result, session, error));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(AuthResult.NetworkError, null, ex.Message);
            }
        }

        private void OnAuthComplete(AuthResult result, ISession session, string errorMessage)
        {
            switch (result)
            {
                case AuthResult.Success:
                    UpdateLoginStatus("Success!");
                    HideAccountLoginPopup();
                    OnLoginSuccess?.Invoke(gameObject);
                    break;
                case AuthResult.InvalidCredentials:
                    UpdateLoginStatus("Invalid username or password");
                    break;
                default:
                    UpdateLoginStatus($"Error: {errorMessage}");
                    break;
            }
        }

        private bool UserSessionExists() => !string.IsNullOrEmpty(nakamaConnection?.Session?.AuthToken);

        private void OnPrivacyPolicyChecked(bool accepted)
        {
            if (privacyPolicyPopup != null) privacyPolicyPopup.SetActive(!accepted);
        }

        private void ClosePrivacyPolicyPopup()
        {
            if (privacyPolicyPopup != null) privacyPolicyPopup.SetActive(false);
        }

        private void ShowAccountLoginPopup()
        {
            if (accountLoginPopup != null)
            {
                accountLoginPopup.gameObject.SetActive(true);
                StartCoroutine(AnimateCanvasGroup(accountLoginPopup, 0f, 1f, 0.3f));
            }
        }

        private void HideAccountLoginPopup()
        {
            if (accountLoginPopup != null)
            {
                StartCoroutine(AnimateCanvasGroup(accountLoginPopup, 1f, 0f, 0.2f, () => accountLoginPopup.gameObject.SetActive(false)));
                ClosePrivacyPolicyPopup();
            }
        }

        private IEnumerator AnimateCanvasGroup(CanvasGroup cg, float start, float end, float duration, Action onComplete = null)
        {
            cg.alpha = start;
            cg.blocksRaycasts = end > 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            cg.alpha = end;
            cg.blocksRaycasts = end > 0.5f;
            onComplete?.Invoke();
        }

        private void UpdateLoginStatus(string message)
        {
            if (loginStatusText != null) loginStatusText.text = message;
        }

        private void EnableLoginButtons(bool enable)
        {
            if (loginButton != null) loginButton.interactable = enable;
            if (registerButton != null) registerButton.interactable = enable;
            if (guestButton != null) guestButton.interactable = enable;
        }

        private void CheckPrivacyPolicyResult(bool accepted)
        {
            if (accepted) ClosePrivacyPolicyPopup();
        }

        public void Logout()
        {
            nakamaConnection?.Logout();
            if (usernameInput != null) usernameInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            OnLogout?.Invoke();
        }
    }
}
