using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Nakama;


namespace NakamaNetwork
{
    /// <summary>
    /// Login UI controller that integrates Nakama authentication with the login menu
    /// </summary>
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
        [SerializeField] private Button moreButton; // From LoginMenuController
        [SerializeField] private CanvasGroup accountLoginPopup; // From LoginMenuController
        [SerializeField] private TMPro.TMP_Text loginStatusText;
        
        [Header("Privacy Policy")]
        [SerializeField] private GameObject privacyPolicyPopup;
        [SerializeField] private Button acceptPrivacyPolicyButton;
        [SerializeField] private TMPro.TMP_Text privacyPolicyText;

        // CanvasGroup ref from LoginMenu (will find automatically)
        private CanvasGroup _loginMenuCanvasGroup;
        private bool _isLoggingIn = false;
        private bool _isConnecting = false;

        public event Action<GameObject> OnLoginSuccess;
        public event Action OnLogout;

        private void Awake()
        {
            AutoWireReferences();

            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginClicked);
            
            if (registerButton != null)
                registerButton.onClick.AddListener(OnRegisterClicked);
            
            if (guestButton != null)
                guestButton.onClick.AddListener(OnGuestClicked);
            
            if (moreButton != null)
                moreButton.onClick.AddListener(ShowAccountLoginPopup);
            
            // Find the login menu canvas group if not assigned
            if (accountLoginPopup == null && transform.parent != null)
            {
                _loginMenuCanvasGroup = transform.parent.Find("LoginMenuCanvasGroup")?.GetComponent<CanvasGroup>();
            }

            // Check privacy policy on start
            CheckPrivacyPolicyAsync();
        }

        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(OnLoginClicked);
            
            if (registerButton != null)
                registerButton.onClick.RemoveListener(OnRegisterClicked);
            
            if (guestButton != null)
                guestButton.onClick.RemoveListener(OnGuestClicked);
            
            if (moreButton != null)
                moreButton.onClick.RemoveListener(ShowAccountLoginPopup);
        }

        private void AutoWireReferences()
        {
            // Find NakamaConnection in scene
            if (nakamaConnection == null)
            {
                var found = GameObject.FindGameObjectWithTag("NakamaConnection");
                if (found != null)
                    nakamaConnection = found.GetComponent<NakamaConnection>();
            }

            // Find LoginMenu UI elements
            if (usernameInput == null)
                usernameInput = transform.Find("UsernameInputField")?.GetComponent<TMPro.TMP_InputField>();

            if (passwordInput == null)
                passwordInput = transform.Find("PasswordInputField")?.GetComponent<TMPro.TMP_InputField>();

            if (loginButton == null)
                loginButton = transform.Find("LoginButton")?.GetComponent<Button>();

            if (registerButton == null)
                registerButton = transform.Find("RegisterButton")?.GetComponent<Button>();

            if (guestButton == null)
                guestButton = transform.Find("GuestButton")?.GetComponent<Button>();

            if (loginStatusText == null)
                loginStatusText = transform.Find("LoginStatusText")?.GetComponent<TMPro.TMP_Text>();

            if (accountLoginPopup == null && transform.parent != null)
            {
                accountLoginPopup = transform.parent.Find("AccountLoginPopup")?.GetComponent<CanvasGroup>();
            }

            if (privacyPolicyPopup == null)
            {
                var canvas = FindObjectOfType<UnityEngine.Canvas>();
                if (canvas != null)
                {
                    privacyPolicyPopup = canvas.transform.Find("PrivacyPolicyPopup")?.gameObject;
                }
            }

            if (acceptPrivacyPolicyButton == null && privacyPolicyPopup != null)
            {
                acceptPrivacyPolicyButton = privacyPolicyPopup.transform.Find("AcceptButton")?.GetComponent<Button>();
            }

            if (privacyPolicyText == null && privacyPolicyPopup != null)
            {
                privacyPolicyText = privacyPolicyPopup.transform.Find("PolicyText")?.GetComponent<TMPro.TMP_Text>();
            }

            if (acceptPrivacyPolicyButton != null)
            {
                acceptPrivacyPolicyButton.onClick.AddListener(OnAcceptPrivacyPolicy);
            }

            EnableLoginButtons(true);
        }

        private async void Start()
        {
            // Check privacy policy
            CheckPrivacyPolicyResult(await CheckPrivacyPolicyAsync());

            // Initialize Nakama connection
            if (nakamaConnection != null)
            {
                try
                {
                    // Auto-connect on start if session exists
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
                        // Try auto-connect as guest
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

        #region UI Callbacks

        private void OnLoginClicked()
        {
            if (_isLoggingIn) return;
            
            string username = usernameInput?.text?.Trim();
            string password = passwordInput?.text?.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                UpdateLoginStatus("Please enter username and password");
                return;
            }

            _isLoggingIn = true;
            EnableLoginButtons(false);
            UpdateLoginStatus("Logging in...");

            // Authenticate with username/password
            AuthenticateUsernamePasswordAsync(username, password, true, OnAuthComplete);
        }

        private void OnRegisterClicked()
        {
            if (_isLoggingIn) return;
            
            string username = usernameInput?.text?.Trim();
            string password = passwordInput?.text?.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                UpdateLoginStatus("Please enter username and password");
                return;
            }

            _isLoggingIn = true;
            EnableLoginButtons(false);
            UpdateLoginStatus("Creating account...");

            // Try to authenticate - if createAccount is true and account doesn't exist, it will be created
            AuthenticateUsernamePasswordAsync(username, password, true, OnAuthComplete);
        }

        private void OnGuestClicked()
        {
            if (_isLoggingIn) return;
            
            _isLoggingIn = true;
            EnableLoginButtons(false);
            UpdateLoginStatus("Connecting as guest...");

            // Authenticate as guest
            AuthenticateDeviceAsync(OnAuthComplete);
        }

        private void OnAcceptPrivacyPolicy()
        {
            if (nakamaConnection != null)
            {
                nakamaConnection.AcceptPrivacyPolicy();
                ClosePrivacyPolicyPopup();
                
                // Send analytics event for policy acceptance
                Debug.Log("[LoginManager] Privacy policy accepted");
            }
        }

        #endregion

        #region Authentication Methods

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
            if (nakamaConnection != null)
            {
                try
                {
                    await nakamaConnection.ConnectAsync();
                    return nakamaConnection.ConnectionState == ConnectionState.Connected;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LoginManager] Auto-connect as guest failed: {ex.Message}");
                }
            }
            return false;
        }

        private async void AuthenticateUsernamePasswordAsync(string username, string password, bool createAccount, Action<AuthResult, ISession, string> onComplete)
        {
            if (nakamaConnection == null)
            {
                onComplete?.Invoke(AuthResult.Failed, null, "Nakama connection not configured");
                return;
            }

            try
            {
                await nakamaConnection.AuthenticateUsernamePasswordAsync(username, password, createAccount, (result, session, error) => {
                    onComplete?.Invoke(result, session, error);
                });
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(AuthResult.NetworkError, null, ex.Message);
            }
        }

        private async void AuthenticateDeviceAsync(Action<AuthResult, ISession, string> onComplete)
        {
            if (nakamaConnection == null)
            {
                onComplete?.Invoke(AuthResult.Failed, null, "Nakama connection not configured");
                return;
            }

            try
            {
                await nakamaConnection.AuthenticateDeviceAsync((result, session, error) => {
                    onComplete?.Invoke(result, session, error);
                });
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(AuthResult.NetworkError, null, ex.Message);
            }
        }

        private void OnAuthComplete(AuthResult result, ISession session, string errorMessage)
        {
            _isLoggingIn = false;
            EnableLoginButtons(true);

            switch (result)
            {
                case AuthResult.Success:
                    Debug.Log($"[LoginManager] Authentication successful: {session?.UserId}");
                    UpdateLoginStatus("Success!");
                    // Hide popup and trigger navigation
                    HideAccountLoginPopup();
                    OnLoginSuccess?.Invoke(gameObject);
                    break;

                case AuthResult.InvalidCredentials:
                    Debug.LogWarning("[LoginManager] Invalid credentials");
                    UpdateLoginStatus("Invalid username or password");
                    break;

                case AuthResult.Failed:
                    Debug.LogWarning($"[LoginManager] Authentication failed: {errorMessage}");
                    UpdateLoginStatus($"Login failed: {errorMessage}");
                    break;

                case AuthResult.NetworkError:
                    Debug.LogError($"[LoginManager] Network error: {errorMessage}");
                    UpdateLoginStatus($"Network error: {errorMessage}");
                    break;

                case AuthResult.Cancelled:
                    UpdateLoginStatus("Cancelled");
                    break;
            }
        }

        private bool UserSessionExists()
        {
            return !string.IsNullOrEmpty(nakamaConnection?.Session?.AuthToken);
        }

        #endregion

        #region Privacy Policy Methods

        private void OnPrivacyPolicyChecked(bool accepted)
        {
            if (privacyPolicyPopup != null)
            {
                if (accepted)
                {
                    ClosePrivacyPolicyPopup();
                }
                else
                {
                    // Show the privacy policy popup
                    privacyPolicyPopup.SetActive(true);
                }
            }
        }

        public void ShowPrivacyPolicyPopup()
        {
            if (privacyPolicyPopup != null)
            {
                privacyPolicyPopup.SetActive(true);
            }
            else
            {
                // Fallback - create the popup
                CreatePrivacyPolicyPopup();
            }
        }

        private void ClosePrivacyPolicyPopup()
        {
            if (privacyPolicyPopup != null)
            {
                privacyPolicyPopup.SetActive(false);
            }
        }

        private void CreatePrivacyPolicyPopup()
        {
            var canvas = FindObjectOfType<UnityEngine.Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[LoginManager] No Canvas found in scene");
                return;
            }

            // Create popup GameObject
            GameObject popup = new GameObject("PrivacyPolicyPopup");
            popup.transform.SetParent(canvas.transform, false);
            
            // Add CanvasGroup for fading
            CanvasGroup canvasGroup = popup.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 0f;

            // Create popup background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(popup.transform, false);
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(1, 1);
            bgRect.sizeDelta = new Vector2(0, 0);
            var bgSprite = background.AddComponent<UnityEngine.UI.Image>();
            bgSprite.color = new Color(0, 0, 0, 0.8f);

            // Create popup container
            GameObject container = new GameObject("Container");
            container.transform.SetParent(popup.transform, false);
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.1f, 0.25f);
            containerRect.anchorMax = new Vector2(0.9f, 0.75f);
            containerRect.sizeDelta = new Vector2(800, 600);
            var containerImage = container.AddComponent<UnityEngine.UI.Image>();
            containerImage.color = new Color(0.9f, 0.9f, 0.95f);
            containerImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Sprite/DefaultBackgroundDiscard.png");

            // Create title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(container.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -300);
            titleRect.sizeDelta = new Vector2(600, 60);
            var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "Privacy Policy";
            titleText.fontSize = 32;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.color = Color.black;

            // Create content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(container.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = new Vector2(0, 20);
            contentRect.sizeDelta = new Vector2(550, 280);
            
            var contentText = contentObj.AddComponent<TMPro.TextMeshProUGUI>();
            contentText.text = @"
                <size=24><b>Privacy Policy</b></size>\n\n
                Last Updated: September 2024\n\n
                <indent=20>We respect your privacy and are committed to protecting your personal data. This policy explains how we collect, use, and protect your information.\n\n
                <b>1. Information We Collect</b>\n
                - Device identifier for device-based authentication\n
                - Username and password (stored on server, not locally)\n
                - Session information for matchmaking and multiplayer\n\n
                <b>2. How We Use Your Information</b>\n
                - Provide game services and multiplayer functionality\n
                - Authenticate and secure user accounts\n
                - Matchmaking and leaderboards\n\n
                <b>3. Data Storage</b>\n
                - All data is stored on our secure Nakama server\n
                - Personal data is encrypted and complies with privacy regulations\n\n
                <b>4. Your Rights</b>\n
                - You can delete your account at any time\n
                - You can request data deletion\n
                - You can revoke device authentication\n\n
                <b>5. Cookie Policy</b>\n
                We use local storage for session tokens. To clear your data, please reset your device or game data.";
            contentText.fontSize = 18;
            contentText.alignment = TMPro.TextAlignmentOptions.TopLeft;
            contentText.color = Color.black;
            contentText.lineSpacing = 1.5f;

            // Create accept button
            GameObject acceptBtnObj = new GameObject("AcceptButton");
            acceptBtnObj.transform.SetParent(container.transform, false);
            RectTransform acceptBtnRect = acceptBtnObj.AddComponent<RectTransform>();
            acceptBtnRect.anchorMin = new Vector2(0.5f, 0f);
            acceptBtnRect.anchorMax = new Vector2(0.5f, 0f);
            acceptBtnRect.anchoredPosition = new Vector2(0, 280);
            acceptBtnRect.sizeDelta = new Vector2(200, 50);
            
            var acceptBtn = acceptBtnObj.AddComponent<Button>();
            acceptBtn.interactable = true;
            var acceptButtonImage = acceptBtnObj.AddComponent<UnityEngine.UI.Image>();
            acceptButtonImage.color = new Color(0.2f, 0.7f, 0.4f);
            
            var acceptBtnText = acceptBtnObj.AddComponent<TMPro.TextMeshProUGUI>();
            acceptBtnText.text = "Accept";
            acceptBtnText.fontSize = 20;
            acceptBtnText.alignment = TMPro.TextAlignmentOptions.Center;
            acceptBtnText.color = Color.white;

            // Create quit button
            GameObject quitBtnObj = new GameObject("QuitButton");
            quitBtnObj.transform.SetParent(container.transform, false);
            RectTransform quitBtnRect = quitBtnObj.AddComponent<RectTransform>();
            quitBtnRect.anchorMin = new Vector2(0.5f, 0f);
            quitBtnRect.anchorMax = new Vector2(0.5f, 0f);
            quitBtnRect.anchoredPosition = new Vector2(0, 200);
            quitBtnRect.sizeDelta = new Vector2(200, 50);
            
            var quitBtn = quitBtnObj.AddComponent<Button>();
            quitBtn.interactable = true;
            var quitButtonImage = quitBtnObj.AddComponent<UnityEngine.UI.Image>();
            quitButtonImage.color = Color.gray;
            
            var quitBtnText = quitBtnObj.AddComponent<TMPro.TextMeshProUGUI>();
            quitBtnText.text = "Quit Game";
            quitBtnText.fontSize = 20;
            quitBtnText.alignment = TMPro.TextAlignmentOptions.Center;
            quitBtnText.color = Color.white;

            // Add button listeners
            acceptBtn.onClick.AddListener(() =>
            {
                nakamaConnection.AcceptPrivacyPolicy();
                ClosePrivacyPolicyPopup();
                // Hide account login popup
                HideAccountLoginPopup();
            });

            // Add Fade In animation
            StartCoroutine(AnimatePopupIn(canvasGroup));
        }

        private IEnumerator AnimatePopupIn(CanvasGroup canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            
            for (float t = 0; t < 0.3f; t += Time.deltaTime)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.3f);
                canvasGroup.blocksRaycasts = t > 0.1f;
                yield return null;
            }
        }

        #endregion

        #region Helper Methods

        private void ShowAccountLoginPopup()
        {
            if (accountLoginPopup != null)
            {
                accountLoginPopup.gameObject.SetActive(true);
                accountLoginPopup.alpha = 0f;
                accountLoginPopup.interactable = true;
                accountLoginPopup.blocksRaycasts = true;
                
                StartCoroutine(AnimatePopupIn(accountLoginPopup));
            }
        }

        private void HideAccountLoginPopup()
        {
            if (accountLoginPopup != null)
            {
                accountLoginPopup.alpha = 0f;
                accountLoginPopup.interactable = false;
                accountLoginPopup.blocksRaycasts = false;
                
                StartCoroutine(AnimatePopupOut(accountLoginPopup));
                
                // Also close privacy policy if open
                ClosePrivacyPolicyPopup();
            }
        }

        private IEnumerator AnimatePopupOut(CanvasGroup canvasGroup)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.gameObject.SetActive(false);
        }

        private void UpdateLoginStatus(string message)
        {
            if (loginStatusText != null)
            {
                loginStatusText.text = message;
            }
            else
            {
                Debug.Log($"[LoginManager] Status: {message}");
            }
        }

        private void EnableLoginButtons(bool enable)
        {
            if (loginButton != null)
                loginButton.interactable = enable;
            
            if (registerButton != null)
                registerButton.interactable = enable;
            
            if (guestButton != null)
                guestButton.interactable = enable;
        }

        private void CheckPrivacyPolicyResult(bool accepted)
        {
            if (accepted)
            {
                ClosePrivacyPolicyPopup();
            }
        }

        /// <summary>
        /// Public method to trigger manual logout
        /// </summary>
        public void Logout()
        {
            if (nakamaConnection != null)
            {
                nakamaConnection.Logout();
            }
            
            // Reset UI
            if (usernameInput != null) usernameInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            
            OnLogout?.Invoke();
        }

        #endregion
    }
}
