using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Drives the login screen UI with real Nakama auth.
/// - CONTINUE WITH GOOGLE -> opens browser for Google OAuth, receives id_token via local HTTP callback
/// - PLAY AS GUEST (registerButton) -> device auth via Nakama
/// - AccountLoginPopup LOGIN -> email/password via Nakama AuthenticateEmailAsync
/// On success AuthenticationService auto-loads GameLobby; this script also listens and loads as fallback.
/// </summary>
public class LoginUI : MonoBehaviour
{
    [Header("Branding")]
    public TMP_Text gameTitle;
    public TMP_Text subtitle;

    [Header("Google Login")]
    public Button googleLoginButton;
    public TMP_Text googleLoginButtonText;
    public Image googleLoginIcon;
    public Button moreButton;
    public Button closeAccountLoginButton;

    [Header("Divider")]
    public TMP_Text dividerText;

    [Header("Username / Password")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public TMP_Text loginButtonText;
    public Button forgotPasswordButton;
    public TMP_Text forgotPasswordButtonText;

    [Header("Register / Guest")]
    public Button registerButton; // in your scene this shows "PLAY AS GUEST"
    public TMP_Text registerText;

    [Header("Terms & Age - must be checked before login")]
    public Button ageCheckboxButton;
    public Image ageCheckboxImage;
    public Button termsCheckboxButton;
    public Image termsCheckboxImage;
    [SerializeField] private string termsNotAcceptedError = "Please confirm your age and accept the Terms of Service to continue.";
    private bool ageConfirmed = false;
    private bool termsAccepted = false;
    private GameObject ageCheckmark;
    private GameObject termsCheckmark;
    // Warzone Yellow HUD palette: dark steel boxes, tactical yellow when checked
    private static readonly Color uncheckedColor = new Color(0.095f, 0.133f, 0.176f, 0.9f);
    private static readonly Color checkedColor = new Color(1.0f, 0.816f, 0.0f, 1f); // #FFD000
    private static readonly Color checkTickColor = new Color(0.05f, 0.06f, 0.08f, 1f);

    [Header("Error Popup")]
    public CanvasGroup errorPopupCanvasGroup;
    public TMP_Text errorPopupTitle;
    public TMP_Text errorPopupMessage;
    public Button errorPopupOKButton;
    public Button errorPopupCancelButton;
    [SerializeField] private string errorColor = "#FF5A5A";
    [SerializeField] private string warningColor = "#FFAA33";
    [SerializeField] private string infoColor = "#33CCFF";
    private System.Action _onRetryAction;

    [Header("Audio")]
    [SerializeField] private AudioClip errorSoundClip;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip errorCloseSoundClip;
    [SerializeField] private AudioClip tickSoundClip;
    [SerializeField, Range(0f, 1f)] private float errorSoundVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float bgMusicVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float errorCloseVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float tickVolume = 0.5f;
    private AudioSource _errorAudioSource;
    private AudioSource _bgMusicSource;

    [Header("Create Account")]
    public CanvasGroup createAccountPopup;
    public TMP_InputField createUsernameInput;
    public TMP_InputField createPasswordInput;
    public TMP_InputField confirmPasswordInput;
    public Button createButton;
    public Button closeCreateAccountButton;
    public Button openCreateAccountButton; // inside AccountLoginPopup RegisterButton

    [Header("Feedback")]
    public TMP_Text errorText;
    public CanvasGroup loadingCanvasGroup;
    public TMP_Text loadingText;
    public CanvasGroup accountLoginPopup;

    [Header("Behavior")]
    [SerializeField] private bool hidePasswordByDefault = true;
    [SerializeField] private string defaultGameTitle = "RAILBOUND";
    [SerializeField] private string defaultSubtitle = "ENTER THE IRONLINE";
    [SerializeField] private string defaultErrorText = "Please enter a username and password.";
    [SerializeField] private string lobbySceneName = "GameLobby";
    [SerializeField] private bool autoGoToLobby = true;

    private void Start()
    {
        if (gameTitle) gameTitle.text = defaultGameTitle;
        if (subtitle) subtitle.text = defaultSubtitle;

        AutoWireTermsCheckboxes();
        EnsureCheckmarkLabels();
        AutoWireErrorPopup();
        AutoWireCreateAccountPopup();
        InitAudio();

        // wire buttons (avoid double listeners if scene reloads)
        if (loginButton) { loginButton.onClick.RemoveListener(OnLoginClicked); loginButton.onClick.AddListener(OnLoginClicked); }
        if (googleLoginButton) { googleLoginButton.onClick.RemoveListener(OnGoogleLoginClicked); googleLoginButton.onClick.AddListener(OnGoogleLoginClicked); }
        if (moreButton) { moreButton.onClick.RemoveListener(OnMoreLogin); moreButton.onClick.AddListener(OnMoreLogin); }
        if (closeAccountLoginButton) { closeAccountLoginButton.onClick.RemoveListener(OnCloseAccountLogin); closeAccountLoginButton.onClick.AddListener(OnCloseAccountLogin); }
        if (registerButton) { registerButton.onClick.RemoveListener(OnRegisterClicked); registerButton.onClick.AddListener(OnRegisterClicked); }
        if (forgotPasswordButton) { forgotPasswordButton.onClick.RemoveListener(OnForgotPasswordClicked); forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked); }
        if (ageCheckboxButton) { ageCheckboxButton.onClick.RemoveListener(ToggleAge); ageCheckboxButton.onClick.AddListener(ToggleAge); }
        if (termsCheckboxButton) { termsCheckboxButton.onClick.RemoveListener(ToggleTerms); termsCheckboxButton.onClick.AddListener(ToggleTerms); }
        if (createButton) { createButton.onClick.RemoveListener(OnCreateAccountClicked); createButton.onClick.AddListener(OnCreateAccountClicked); }
        if (closeCreateAccountButton) { closeCreateAccountButton.onClick.RemoveListener(OnCloseCreateAccount); closeCreateAccountButton.onClick.AddListener(OnCloseCreateAccount); }
        if (openCreateAccountButton) { openCreateAccountButton.onClick.RemoveListener(OnOpenCreateAccount); openCreateAccountButton.onClick.AddListener(OnOpenCreateAccount); }

        if (passwordInput) passwordInput.contentType = hidePasswordByDefault ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        if (passwordInput) passwordInput.ForceLabelUpdate();
        if (createPasswordInput) { createPasswordInput.contentType = hidePasswordByDefault ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard; createPasswordInput.ForceLabelUpdate(); }
        if (confirmPasswordInput) { confirmPasswordInput.contentType = hidePasswordByDefault ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard; confirmPasswordInput.ForceLabelUpdate(); }

        UpdateCheckboxesVisual();
        SetError(null);
        SetLoading(false);
        SetAccountLoginPopup(false);
        SetCreateAccountPopup(false);

        // also listen to service success for fallback nav
        if (AuthenticationService.Instance != null)
        {
            AuthenticationService.Instance.OnLoginSuccess -= OnAuthSuccessGoLobby;
            AuthenticationService.Instance.OnLoginSuccess += OnAuthSuccessGoLobby;
            // Check server connectivity
            _ = CheckServerConnection();
        }
    }

    private void OnDestroy()
    {
        if (AuthenticationService.Instance != null)
            AuthenticationService.Instance.OnLoginSuccess -= OnAuthSuccessGoLobby;
        if (ageCheckboxButton) ageCheckboxButton.onClick.RemoveListener(ToggleAge);
        if (termsCheckboxButton) termsCheckboxButton.onClick.RemoveListener(ToggleTerms);
        if (errorPopupOKButton) errorPopupOKButton.onClick.RemoveListener(OnPopupOKClicked);
        if (errorPopupCancelButton) errorPopupCancelButton.onClick.RemoveListener(OnPopupCancelClicked);
        if (createButton) createButton.onClick.RemoveListener(OnCreateAccountClicked);
        if (closeCreateAccountButton) closeCreateAccountButton.onClick.RemoveListener(OnCloseCreateAccount);
        if (openCreateAccountButton) openCreateAccountButton.onClick.RemoveListener(OnOpenCreateAccount);
        // Stop bg music
        if (_bgMusicSource) { _bgMusicSource.Stop(); Destroy(_bgMusicSource.gameObject); }
    }

    // ------------------------------------------------------------------ Audio
    private void InitAudio()
    {
        // Auto-load clips from Resources if not assigned in Inspector
        if (errorSoundClip == null)
            errorSoundClip = Resources.Load<AudioClip>("SoundClips/errorMeeageSound");
        if (backgroundMusicClip == null)
            backgroundMusicClip = Resources.Load<AudioClip>("SoundClips/gameMusicLoop");
        if (errorCloseSoundClip == null)
            errorCloseSoundClip = Resources.Load<AudioClip>("SoundClips/errorMessgeClose");
        if (tickSoundClip == null)
            tickSoundClip = Resources.Load<AudioClip>("SoundClips/tickSound");

        // --- Error sound AudioSource (one-shot) ---
        _errorAudioSource = gameObject.AddComponent<AudioSource>();
        _errorAudioSource.playOnAwake = false;
        _errorAudioSource.clip = errorSoundClip;
        _errorAudioSource.volume = errorSoundVolume;
        _errorAudioSource.loop = false;
        _errorAudioSource.priority = 0;

        // --- Background music AudioSource (loop) ---
        if (backgroundMusicClip != null)
        {
            _bgMusicSource = gameObject.AddComponent<AudioSource>();
            _bgMusicSource.playOnAwake = false;
            _bgMusicSource.clip = backgroundMusicClip;
            _bgMusicSource.volume = bgMusicVolume;
            _bgMusicSource.loop = true;
            _bgMusicSource.priority = 128;
            _bgMusicSource.Play();
            Debug.Log("[LoginUI] Background music started");
        }
        else
        {
            Debug.LogWarning("[LoginUI] Background music clip not found");
        }
    }

    public void PlayErrorSound()
    {
        if (_errorAudioSource != null && errorSoundClip != null)
        {
            _errorAudioSource.PlayOneShot(errorSoundClip, errorSoundVolume);
        }
    }

    public void PlayTickSound()
    {
        if (_errorAudioSource != null && tickSoundClip != null)
        {
            _errorAudioSource.PlayOneShot(tickSoundClip, tickVolume);
        }
    }

    // ------------------------------------------------------------------ Error Popup
    private void AutoWireErrorPopup()
    {
        // Try to find the ErrorPopup hierarchy created in the scene
        if (errorPopupCanvasGroup == null)
        {
            var popup = GameObject.Find("Canvas/ErrorPopup")?.GetComponent<CanvasGroup>();
            if (popup != null) errorPopupCanvasGroup = popup;
        }
        if (errorPopupTitle == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/ErrorTitle");
            if (obj != null) errorPopupTitle = obj.GetComponent<TMP_Text>();
        }
        if (errorPopupMessage == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/ErrorMessage");
            if (obj != null) errorPopupMessage = obj.GetComponent<TMP_Text>();
        }
        if (errorPopupOKButton == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/OKButton");
            if (obj != null) errorPopupOKButton = obj.GetComponent<Button>();
        }
        if (errorPopupCancelButton == null)
        {
            var obj = GameObject.Find("Canvas/ErrorPopup/ErrorCard/CancelButton");
            if (obj != null) errorPopupCancelButton = obj.GetComponent<Button>();
        }
        if (errorPopupOKButton)
        {
            errorPopupOKButton.onClick.RemoveListener(OnPopupOKClicked);
            errorPopupOKButton.onClick.AddListener(OnPopupOKClicked);
        }
        if (errorPopupCancelButton)
        {
            errorPopupCancelButton.onClick.RemoveListener(OnPopupCancelClicked);
            errorPopupCancelButton.onClick.AddListener(OnPopupCancelClicked);
        }
        // Ensure hidden on start
        HideErrorPopup();
    }

    /// <summary>Show a styled Battle Royale error popup.
    /// type: "error" (red), "warning" (orange), "info" (cyan)
    /// mode: "ok" = single CONFIRM, "retry" = TRY AGAIN + CANCEL, "cancel_only" = CANCEL only
    /// onRetry: callback when TRY AGAIN is pressed
    /// </summary>
    public void ShowErrorPopup(string title, string message, string type = "error", string mode = "ok", System.Action onRetry = null)
    {
        if (errorPopupCanvasGroup == null)
        {
            SetError($"{title}: {message}");
            return;
        }

        _onRetryAction = onRetry;

        // Pick accent color based on type
        string hex = type == "error" ? errorColor : type == "warning" ? warningColor : infoColor;
        ColorUtility.TryParseHtmlString(hex, out Color accent);
        Color darkAccent = accent * 0.5f;
        darkAccent.a = accent.a;

        // Title
        if (errorPopupTitle)
        {
            errorPopupTitle.text = title;
            errorPopupTitle.color = accent;
        }

        // Message
        if (errorPopupMessage)
        {
            errorPopupMessage.text = message;
        }

        // Color all accent elements
        SetAccentColor("TopBar", accent);
        SetAccentColor("BottomBar", darkAccent);
        SetAccentColor("LeftStripe", accent);
        SetAccentColor("RightStripe", darkAccent);
        SetAccentColor("SepLine", darkAccent);

        // Icon
        var iconBg = GameObject.Find("Canvas/ErrorPopup/ErrorCard/IconBg")?.GetComponent<Image>();
        if (iconBg) { Color a = accent; a.a = 0.15f; iconBg.color = a; }
        var iconBorder = GameObject.Find("Canvas/ErrorPopup/ErrorCard/IconBg/IconBorder")?.GetComponent<Image>();
        if (iconBorder) { Color a = accent; a.a = 0.8f; iconBorder.color = a; }
        var iconText = GameObject.Find("Canvas/ErrorPopup/ErrorCard/IconBg/Icon!")?.GetComponent<TMP_Text>();
        if (iconText) iconText.color = accent;

        // OK / Try Again button
        var btnImg = GameObject.Find("Canvas/ErrorPopup/ErrorCard/OKButton")?.GetComponent<Image>();
        if (btnImg) btnImg.color = accent;
        var btnBorder = GameObject.Find("Canvas/ErrorPopup/ErrorCard/OKButton/BtnBorder")?.GetComponent<Image>();
        if (btnBorder) { Color a = accent; a.a = 0.6f; btnBorder.color = a; }

        // Configure buttons based on mode
        if (errorPopupOKButton)
        {
            var okLabel = errorPopupOKButton.transform.Find("Label")?.GetComponent<TMP_Text>();
            var okRT = errorPopupOKButton.GetComponent<RectTransform>();

            if (mode == "retry")
            {
                // Two buttons: TRY AGAIN (left) + CANCEL (right)
                okLabel.text = "TRY AGAIN";
                okRT.anchorMin = new Vector2(0.12f, 0.04f);
                okRT.anchorMax = new Vector2(0.47f, 0.18f);
                errorPopupOKButton.gameObject.SetActive(true);
                if (errorPopupCancelButton) errorPopupCancelButton.gameObject.SetActive(true);
            }
            else if (mode == "cancel_only")
            {
                errorPopupOKButton.gameObject.SetActive(false);
                if (errorPopupCancelButton)
                {
                    var cCancelRT = errorPopupCancelButton.GetComponent<RectTransform>();
                    cCancelRT.anchorMin = new Vector2(0.25f, 0.04f);
                    cCancelRT.anchorMax = new Vector2(0.75f, 0.18f);
                    var cLabel = errorPopupCancelButton.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (cLabel) cLabel.text = "CANCEL";
                    errorPopupCancelButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // Single CONFIRM button centered
                okLabel.text = "CONFIRM";
                okRT.anchorMin = new Vector2(0.25f, 0.04f);
                okRT.anchorMax = new Vector2(0.75f, 0.18f);
                errorPopupOKButton.gameObject.SetActive(true);
                if (errorPopupCancelButton) errorPopupCancelButton.gameObject.SetActive(false);
            }
        }

        // Corner decorations
        SetAccentColor("CornerTR", darkAccent);
        SetAccentColor("CornerBL", darkAccent);

        // Grid lines
        SetAccentColor("GridLine1", accent * 0.3f);
        SetAccentColor("GridLine2", accent * 0.3f);
        SetAccentColor("GridLine3", accent * 0.3f);

        // Show popup
        errorPopupCanvasGroup.alpha = 1f;
        errorPopupCanvasGroup.interactable = true;
        errorPopupCanvasGroup.blocksRaycasts = true;

        // Replay Reach UI scale-in animation on the card, if present
        var errorCard = GameObject.Find("Canvas/ErrorPopup/ErrorCard");
        var reachPopup = errorCard != null
            ? errorCard.GetComponent<Michsky.UI.Reach.UIPopup>()
            : null;
        if (reachPopup != null) reachPopup.PlayIn();

        // Play error sound
        PlayErrorSound();

        Debug.Log($"[LoginUI] ErrorPopup: [{type}] [{mode}] {title} - {message}");
    }

    private void SetAccentColor(string childName, Color col)
    {
        var obj = GameObject.Find($"Canvas/ErrorPopup/ErrorCard/{childName}");
        if (obj == null) return;
        var img = obj.GetComponent<Image>();
        if (img) img.color = col;
    }

    public void HideErrorPopup()
    {
        if (errorPopupCanvasGroup)
        {
            errorPopupCanvasGroup.alpha = 0f;
            errorPopupCanvasGroup.interactable = false;
            errorPopupCanvasGroup.blocksRaycasts = false;
        }
        // Play close sound
        if (_errorAudioSource != null && errorCloseSoundClip != null)
        {
            _errorAudioSource.PlayOneShot(errorCloseSoundClip, errorCloseVolume);
        }
    }

    private void OnPopupOKClicked()
    {
        HideErrorPopup();
        // If there's a retry action (e.g. server reconnect), invoke it
        if (_onRetryAction != null)
        {
            _onRetryAction.Invoke();
            _onRetryAction = null;
        }
    }

    private void OnPopupCancelClicked()
    {
        // Dismiss only — never quit from a dialog button.
        _onRetryAction = null;
        HideErrorPopup();
        Debug.Log("[LoginUI] Popup dismissed via Cancel/Close");
    }

    // ------------------------------------------------------------------ Terms validation
    private bool CheckTermsAccepted(string action = "continue")
    {
        if (!ageConfirmed || !termsAccepted)
        {
            string missing = !ageConfirmed && !termsAccepted ? "confirm your age and accept the Terms of Service"
                : !ageConfirmed ? "confirm your age" : "accept the Terms of Service";
            ShowErrorPopup("One More Step", $"Please {missing} below to {action}.", "warning");
            UpdateCheckboxesVisual();
            return false;
        }
        return true;
    }

    public void ToggleAge()
    {
        ageConfirmed = !ageConfirmed;
        UpdateCheckboxesVisual();
        if (ageConfirmed && termsAccepted) SetError(null);
        Debug.Log($"[LoginUI] Age checkbox: {ageConfirmed}");
    }

    public void ToggleTerms()
    {
        termsAccepted = !termsAccepted;
        UpdateCheckboxesVisual();
        if (ageConfirmed && termsAccepted) SetError(null);
        Debug.Log($"[LoginUI] Terms checkbox: {termsAccepted}");
    }

    private void UpdateCheckboxesVisual()
    {
        if (ageCheckboxImage) ageCheckboxImage.color = ageConfirmed ? checkedColor : uncheckedColor;
        if (termsCheckboxImage) termsCheckboxImage.color = termsAccepted ? checkedColor : uncheckedColor;
        if (ageCheckmark) ageCheckmark.SetActive(ageConfirmed);
        if (termsCheckmark) termsCheckmark.SetActive(termsAccepted);
    }

    private void AutoWireTermsCheckboxes()
    {
        // Try hierarchy: Canvas/Terms/Checkbox1 = age, Checkbox2 = terms (matches screenshot order)
        if (ageCheckboxButton == null)
        {
            var t = transform.root.Find("Canvas/Terms/Checkbox1") ?? GameObject.Find("Checkbox1")?.transform;
            if (t) ageCheckboxButton = t.GetComponent<Button>() ?? t.gameObject.AddComponent<Button>();
        }
        if (termsCheckboxButton == null)
        {
            var t = transform.root.Find("Canvas/Terms/Checkbox2") ?? GameObject.Find("Checkbox2")?.transform;
            if (t) termsCheckboxButton = t.GetComponent<Button>() ?? t.gameObject.AddComponent<Button>();
        }
        if (ageCheckboxImage == null && ageCheckboxButton) ageCheckboxImage = ageCheckboxButton.GetComponent<Image>();
        if (termsCheckboxImage == null && termsCheckboxButton) termsCheckboxImage = termsCheckboxButton.GetComponent<Image>();
        // fallback search by path inside same canvas
        if (ageCheckboxImage == null) ageCheckboxImage = GameObject.Find("Checkbox1")?.GetComponent<Image>();
        if (termsCheckboxImage == null) termsCheckboxImage = GameObject.Find("Checkbox2")?.GetComponent<Image>();
    }

    private void EnsureCheckmarkLabels()
    {
        ageCheckmark = GetOrCreateCheckmark(ageCheckboxImage, "AgeCheck");
        termsCheckmark = GetOrCreateCheckmark(termsCheckboxImage, "TermsCheck");
    }

    /// <summary>Builds a two-bar vector checkmark (no font glyph needed).</summary>
    private GameObject GetOrCreateCheckmark(Image parentImage, string name)
    {
        if (parentImage == null) return null;
        var existing = parentImage.transform.Find(name);
        if (existing != null)
        {
            // Drop legacy TMP-based checkmarks from older builds
            var legacy = existing.GetComponent<TMP_Text>();
            if (legacy != null) Destroy(legacy);
            if (existing.childCount == 0) Destroy(existing.gameObject);
            else return existing.gameObject;
        }
        var go = new GameObject(name);
        go.transform.SetParent(parentImage.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        // Short arm (bottom-left) + long arm, rotated into a tick
        AddTickBar(go.transform, new Vector2(-0.13f, -0.03f), 0.30f, 45f);
        AddTickBar(go.transform, new Vector2(0.12f, 0.07f), 0.58f, -45f);
        go.SetActive(false);
        return go;
    }

    private void AddTickBar(Transform parent, Vector2 centerFrac, float lenFrac, float angle)
    {
        var go = new GameObject("bar");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var box = ((RectTransform)parent).rect;
        float w = Mathf.Max(box.width, 18f);
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w * lenFrac, Mathf.Max(2.5f, w * 0.14f));
        rt.anchoredPosition = new Vector2(centerFrac.x * w, centerFrac.y * w);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<Image>();
        img.color = checkTickColor;
        img.raycastTarget = false;
    }

    private void OnAuthSuccessGoLobby(AuthResult _)
    {
        if (!autoGoToLobby) return;
        if (string.IsNullOrEmpty(lobbySceneName)) return;
        if (SceneManager.GetActiveScene().name == lobbySceneName) return;
        SceneManager.LoadSceneAsync(lobbySceneName);
    }

    // ------------------------------------------------------------------ Google (placeholder -> guest)
    public void OnGoogleLogin() => OnGoogleLoginClicked();

    private void OnGoogleLoginClicked()
    {
        if (!CheckTermsAccepted("continue with Google")) return;
        if (AuthenticationService.Instance == null) { ShowErrorPopup("Service Missing", "Authentication service is not available. Please restart the game.", "error"); return; }
        SetError(null);
        SetLoading(true);
        if (loadingText) loadingText.text = "Opening browser for Google login...";

        // Check if Google Client ID is configured
        if (string.IsNullOrEmpty(AuthenticationService.Instance.GoogleClientId)
            || AuthenticationService.Instance.GoogleClientId == "YOUR_GOOGLE_CLIENT_ID_HERE")
        {
            SetLoading(false);
            ShowErrorPopup("Google Not Configured", "Google login is not set up yet. Please use Guest or Email login instead.", "warning");
            Debug.LogWarning("[LoginUI] Google Client ID not set — cannot start OAuth.");
            return;
        }

        // Open browser for Google OAuth — token arrives via local HTTP callback
        AuthenticationService.Instance.StartGoogleOAuth();
        // Note: loading stays on while browser is open. The callback will trigger
        // LoginWithGoogleAsync(idToken) -> HandleNewSessionAsync -> OnLoginSuccess -> lobby load.
        // Add a timeout to reset if no callback arrives
        StartCoroutine(ResetLoadingAfterTimeout(60f));
    }

    private System.Collections.IEnumerator ResetLoadingAfterTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        // If still loading after timeout, something went wrong
        if (loadingCanvasGroup && loadingCanvasGroup.alpha > 0.5f)
        {
            SetLoading(false);
            ShowErrorPopup("Login Timed Out", "Google login took too long. Please try again or use a different login method.", "warning");
        }
    }

    // ------------------------------------------------------------------ Login (email/password)
    public async void OnLogin()
    {
        if (AuthenticationService.Instance == null) { ShowErrorPopup("Service Missing", "Authentication service is not available. Please restart the game.", "error"); EnableLoginControls(true); return; }
        SetError(null);
        SetLoading(true);
        var username = usernameInput ? usernameInput.text.Trim() : string.Empty;
        var password = passwordInput ? passwordInput.text : string.Empty;
        var result = await AuthenticationService.Instance.LoginAsync(username, password);
        HandleResult(result);
    }

    private void OnLoginClicked()
    {
        if (!CheckTermsAccepted("log in")) return;
        if (string.IsNullOrWhiteSpace(usernameInput?.text))
        {
            ShowErrorPopup("Username Missing", "Please enter your username to log in.", "warning");
            return;
        }
        if (string.IsNullOrEmpty(passwordInput?.text))
        {
            ShowErrorPopup("Password Missing", "Please enter your password to log in.", "warning");
            return;
        }
        OnLogin();
    }

    // ------------------------------------------------------------------ Register / Guest -> device auth
    public void OnRegister() => OnRegisterClicked();

    private async void OnRegisterClicked()
    {
        if (!CheckTermsAccepted("play as guest")) return;
        // This button is "PLAY AS GUEST" in your LoginMenu.unity -> do device auth.
        if (AuthenticationService.Instance == null) { ShowErrorPopup("Service Missing", "Authentication service is not available. Please restart the game.", "error"); return; }
        SetError(null);
        SetLoading(true);
        var result = await AuthenticationService.Instance.LoginAsGuestAsync();
        HandleResult(result);
    }

    // ------------------------------------------------------------------ Forgot
    private void OnForgotPasswordClicked()
    {
        ShowErrorPopup("Reset Not Available", "Password reset isn't available in this build yet. You can continue as guest, or create a new account if you lost access.", "info");
    }

    public void OnMoreLogin() { PlayTickSound(); SetError(null); SetAccountLoginPopup(true); }
    public void OnCloseAccountLogin() { PlayTickSound(); SetAccountLoginPopup(false); }

    // ------------------------------------------------------------------ Server connectivity check
    private async System.Threading.Tasks.Task CheckServerConnection()
    {
        if (AuthenticationService.Instance == null) return;
        try
        {
            // Show connecting state
            SetLoading(true);
            if (loadingText) loadingText.text = "Connecting to server...";

            using (var www = UnityEngine.Networking.UnityWebRequest.Get(AuthenticationService.Instance.ApiBaseUrl))
            {
                www.timeout = 5;
                var op = www.SendWebRequest();
                while (!op.isDone) await System.Threading.Tasks.Task.Yield();

                SetLoading(false);

                if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    ShowErrorPopup("SERVER UNREACHABLE",
                        "Cannot connect to the game server at " + AuthenticationService.Instance.ApiBaseUrl +
                        ". Please check your internet connection and try again.",
                        "error", "retry", () => _ = CheckServerConnection());
                    if (ServerStatusText) ServerStatusText.text = "Offline";
                    if (OnlineDot) OnlineDot.color = Color.red;
                }
                else
                {
                    Debug.Log("[LoginUI] Server connection OK");
                    if (ServerStatusText) ServerStatusText.text = "Online";
                    if (OnlineDot) OnlineDot.color = new Color(0.22f, 0.85f, 0.4f);
                }
            }
        }
        catch (System.Exception ex)
        {
            SetLoading(false);
            Debug.LogWarning($"[LoginUI] Server check failed: {ex.Message}");
            ShowErrorPopup("CONNECTION ERROR",
                "Unable to reach the game server. Please check your network connection.",
                "error", "retry", () => _ = CheckServerConnection());
        }
    }

    [Header("Server Status")]
    public TMP_Text ServerStatusText;
    public Image OnlineDot;

    // ------------------------------------------------------------------ Helpers
    private void HandleResult(AuthResult result)
    {
        SetLoading(false);
        EnableLoginControls(true);
        if (!result.IsSuccess)
        {
            string message = result.Error ?? "Unknown error. Please try again.";
            ShowErrorPopup("Login Failed", message, "error");
            // Mirror into the account card so the message stays visible after dismiss
            if (accountLoginPopup != null && accountLoginPopup.gameObject.activeSelf)
                SetError(message);
            return;
        }
        // success -> AuthenticationService already triggers lobby load; show feedback briefly
        if (loadingText) loadingText.text = "Success! Loading lobby...";
        // fallback load if service has autoLoad disabled
        if (autoGoToLobby && !string.IsNullOrEmpty(lobbySceneName) && SceneManager.GetActiveScene().name != lobbySceneName)
            SceneManager.LoadSceneAsync(lobbySceneName);
    }

    private void SetLoading(bool loading)
    {
        if (loadingCanvasGroup)
        {
            loadingCanvasGroup.alpha = loading ? 1f : 0f;
            loadingCanvasGroup.blocksRaycasts = loading;
            loadingCanvasGroup.interactable = loading;
        }
        if (loadingText) loadingText.text = loading ? "Logging in..." : string.Empty;
        if (loginButton) loginButton.interactable = !loading;
        if (googleLoginButton) googleLoginButton.interactable = !loading;
        if (moreButton) moreButton.interactable = !loading;
        if (registerButton) registerButton.interactable = !loading;
    }

    private void EnableLoginControls(bool enable)
    {
        if (loginButton) loginButton.interactable = enable;
        if (googleLoginButton) googleLoginButton.interactable = enable;
        if (registerButton) registerButton.interactable = enable;
    }

    private void SetError(string message)
    {
        if (errorText)
        {
            errorText.text = message ?? "";
            errorText.enabled = !string.IsNullOrEmpty(message);
        }
        if (!string.IsNullOrEmpty(message)) Debug.LogWarning($"[LoginUI] {message}");
    }

    private void SetAccountLoginPopup(bool visible)
    {
        if (!accountLoginPopup) return;
        accountLoginPopup.alpha = visible ? 1f : 0f;
        accountLoginPopup.interactable = visible;
        accountLoginPopup.blocksRaycasts = visible;
        if (visible && !accountLoginPopup.gameObject.activeSelf) accountLoginPopup.gameObject.SetActive(true);
        if (!visible) accountLoginPopup.gameObject.SetActive(false);
    }

    private void SetCreateAccountPopup(bool visible)
    {
        if (!createAccountPopup) return;
        createAccountPopup.alpha = visible ? 1f : 0f;
        createAccountPopup.interactable = visible;
        createAccountPopup.blocksRaycasts = visible;
        if (visible && !createAccountPopup.gameObject.activeSelf) createAccountPopup.gameObject.SetActive(true);
        if (!visible) createAccountPopup.gameObject.SetActive(false);
    }

    private void AutoWireCreateAccountPopup()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;
        var popupT = canvas.transform.Find("CreateAccountPopup");
        if (createAccountPopup == null && popupT != null) createAccountPopup = popupT.GetComponent<CanvasGroup>();
        var cardT = popupT != null ? popupT.Find("CreateAccountCard") : null;
        if (cardT != null)
        {
            if (createUsernameInput == null) createUsernameInput = cardT.Find("UsernameInput")?.GetComponent<TMP_InputField>();
            if (createPasswordInput == null) createPasswordInput = cardT.Find("PasswordInput")?.GetComponent<TMP_InputField>();
            if (confirmPasswordInput == null) confirmPasswordInput = cardT.Find("ConfirmPasswordInput")?.GetComponent<TMP_InputField>();
            if (createButton == null) createButton = cardT.Find("CreateButton")?.GetComponent<Button>();
            if (closeCreateAccountButton == null) closeCreateAccountButton = cardT.Find("CloseButton")?.GetComponent<Button>();
        }
        if (openCreateAccountButton == null)
        {
            var accCard = canvas.transform.Find("AccountLoginPopup/AccountLoginCard");
            if (accCard != null) openCreateAccountButton = accCard.Find("RegisterButton")?.GetComponent<Button>();
        }
        if (createButton)
        {
            createButton.onClick.RemoveListener(OnCreateAccountClicked);
            createButton.onClick.AddListener(OnCreateAccountClicked);
        }
        if (closeCreateAccountButton)
        {
            closeCreateAccountButton.onClick.RemoveListener(OnCloseCreateAccount);
            closeCreateAccountButton.onClick.AddListener(OnCloseCreateAccount);
        }
        if (openCreateAccountButton)
        {
            openCreateAccountButton.onClick.RemoveListener(OnOpenCreateAccount);
            openCreateAccountButton.onClick.AddListener(OnOpenCreateAccount);
        }
        SetCreateAccountPopup(false);
    }

    public void OnOpenCreateAccount()
    {
        PlayTickSound();
        SetAccountLoginPopup(false);
        SetCreateAccountPopup(true);
    }

    public void OnCloseCreateAccount()
    {
        PlayTickSound();
        SetCreateAccountPopup(false);
    }

    private async void OnCreateAccountClicked()
    {
        PlayTickSound();
        if (!CheckTermsAccepted("create an account")) return;
        if (createUsernameInput == null || createPasswordInput == null || confirmPasswordInput == null)
        {
            ShowErrorPopup("Setup Error", "Create account form not wired.", "error");
            return;
        }
        string user = createUsernameInput.text.Trim();
        string pass = createPasswordInput.text;
        string confirm = confirmPasswordInput.text;
        if (string.IsNullOrWhiteSpace(user))
        {
            ShowErrorPopup("Username Missing", "Please choose a username for your new account.", "warning");
            return;
        }
        if (string.IsNullOrEmpty(pass))
        {
            ShowErrorPopup("Password Missing", "Please choose a password for your new account.", "warning");
            return;
        }
        if (pass != confirm)
        {
            ShowErrorPopup("Passwords Don't Match", "The two passwords are different. Please re-enter them.", "warning");
            return;
        }
        if (AuthenticationService.Instance == null) { ShowErrorPopup("Service Missing", "Auth service unavailable.", "error"); return; }
        SetError(null);
        SetLoading(true);
        var result = await AuthenticationService.Instance.CreateAccountAsync(user, pass, confirm);
        HandleResult(result);
        if (result.IsSuccess) SetCreateAccountPopup(false);
    }
}
