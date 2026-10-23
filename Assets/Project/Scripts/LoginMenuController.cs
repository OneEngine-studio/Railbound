using UnityEngine;
using UnityEngine.UI;
using Railbound.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Controls the LoginMenu interactions:
/// - Play as Guest: opens GuestNameModalController popup.
/// - Settings: displays SettingsView modal.
/// - Language: toggles/displays locale status.
/// - Quit: exits application.
/// - More: opens the username/password AccountLoginPopup.
/// - Ambient VFX: pauses VFX during modals to save GPU/CPU cycles.
/// Auto-wires buttons and views if unassigned.
/// </summary>
public class LoginMenuController : MonoBehaviour
{
    [Header("Primary Action Buttons")]
    [SerializeField] private Button guestButton;
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private Button moreButton;

    [Header("Bottom Bar Utility Buttons")]
    [SerializeField] private Button languageButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Popups & Modals")]
    [SerializeField] private GuestNameModalController guestNamePopup;
    [SerializeField] private SettingsView settingsView;
    [SerializeField] private CanvasGroup accountLoginPopup;
    [SerializeField] private Button closeAccountLoginButton;

    [Header("Settings Prefab (Fallback if not in scene)")]
    [SerializeField] private GameObject settingsViewPrefab;

    [Header("Ambient VFX")]
    [SerializeField] private AmbientVFXController ambientVFX;

    private int _currentLanguageIndex = 0;
    private static readonly string[] SupportedLanguages = { "English (US)", "Español", "Deutsch", "Français", "日本語" };

    private void Awake()
    {
        AutoWire();
        WireButtons();

        // Ensure account login popup starts hidden
        HideAccountLogin();
    }

    private void AutoWire()
    {
        var canvas = transform.root.GetComponentInChildren<Canvas>();
        Transform canvasT = canvas != null ? canvas.transform : transform.parent;

        if (guestButton == null)
            guestButton = transform.Find("GuestButton")?.GetComponent<Button>() ??
                          canvasT?.Find("LoginPanel/GuestButton")?.GetComponent<Button>();

        if (googleLoginButton == null)
            googleLoginButton = transform.Find("GoogleLoginButton")?.GetComponent<Button>() ??
                                canvasT?.Find("LoginPanel/GoogleLoginButton")?.GetComponent<Button>();

        if (moreButton == null)
            moreButton = transform.Find("MoreButton")?.GetComponent<Button>() ??
                         canvasT?.Find("LoginPanel/MoreButton")?.GetComponent<Button>();

        if (languageButton == null && canvasT != null)
            languageButton = canvasT.Find("LanguageButton")?.GetComponent<Button>() ??
                             transform.Find("LanguageButton")?.GetComponent<Button>();

        if (settingsButton == null && canvasT != null)
            settingsButton = canvasT.Find("SettingsButton")?.GetComponent<Button>() ??
                             transform.Find("SettingsButton")?.GetComponent<Button>();

        if (quitButton == null && canvasT != null)
            quitButton = canvasT.Find("QuitButton")?.GetComponent<Button>() ??
                         transform.Find("QuitButton")?.GetComponent<Button>();

        if (guestNamePopup == null && canvasT != null)
            guestNamePopup = canvasT.GetComponentInChildren<GuestNameModalController>(true);

        if (settingsView == null && canvasT != null)
            settingsView = canvasT.GetComponentInChildren<SettingsView>(true);

        if (accountLoginPopup == null && canvasT != null)
            accountLoginPopup = canvasT.Find("AccountLoginPopup")?.GetComponent<CanvasGroup>();

        if (closeAccountLoginButton == null && accountLoginPopup != null)
            closeAccountLoginButton = accountLoginPopup.transform
                .Find("AccountLoginCard/CloseButton")?.GetComponent<Button>();

        if (ambientVFX == null && canvasT != null)
            ambientVFX = canvasT.GetComponentInChildren<AmbientVFXController>(true);
    }

    private void WireButtons()
    {
        if (guestButton != null)
        {
            guestButton.onClick.RemoveListener(OnPlayAsGuestClicked);
            guestButton.onClick.AddListener(OnPlayAsGuestClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (languageButton != null)
        {
            languageButton.onClick.RemoveListener(OnLanguageClicked);
            languageButton.onClick.AddListener(OnLanguageClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        if (moreButton != null)
        {
            moreButton.onClick.RemoveListener(ShowAccountLogin);
            moreButton.onClick.AddListener(ShowAccountLogin);
        }

        if (closeAccountLoginButton != null)
        {
            closeAccountLoginButton.onClick.RemoveListener(HideAccountLogin);
            closeAccountLoginButton.onClick.AddListener(HideAccountLogin);
        }

        if (settingsView != null)
        {
            settingsView.OnClose += OnModalClosed;
        }
    }

    private void OnDestroy()
    {
        if (guestButton != null) guestButton.onClick.RemoveListener(OnPlayAsGuestClicked);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (languageButton != null) languageButton.onClick.RemoveListener(OnLanguageClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        if (moreButton != null) moreButton.onClick.RemoveListener(ShowAccountLogin);
        if (closeAccountLoginButton != null) closeAccountLoginButton.onClick.RemoveListener(HideAccountLogin);
        if (settingsView != null) settingsView.OnClose -= OnModalClosed;
    }

    // ------------------------------------------------------------------ Public Handlers

    public void OnPlayAsGuestClicked()
    {
        Debug.Log("[LoginMenu] Play as Guest clicked");
        if (guestNamePopup != null)
        {
            ambientVFX?.Pause();
            guestNamePopup.Show();
        }
        else
        {
            var mgr = UIManager.Instance ?? FindFirstObjectByType<Railbound.UI.UIManager>();
            if (mgr != null)
            {
                ambientVFX?.Pause();
                mgr.ShowGuestLogin();
            }
            else
            {
                Debug.LogWarning("[LoginMenu] GuestNameModalController & UIManager not found, loading GameLobby directly.");
#if UNITY_EDITOR
                if (Application.isPlaying)
                    UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("GameLobby");
#else
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("GameLobby");
#endif
            }
        }
    }

    public void OnSettingsClicked()
    {
        Debug.Log("[LoginMenu] Settings clicked");
        var mgr = UIManager.Instance ?? FindFirstObjectByType<Railbound.UI.UIManager>();
        if (mgr != null)
        {
            ambientVFX?.Pause();
            mgr.ShowSettings();
            return;
        }

        if (settingsView == null && settingsViewPrefab != null)
        {
            var canvas = transform.root.GetComponentInChildren<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform.parent;
            var obj = Instantiate(settingsViewPrefab, parent);
            settingsView = obj.GetComponent<SettingsView>();
            if (settingsView != null) settingsView.OnClose += OnModalClosed;
        }

        if (settingsView != null)
        {
            ambientVFX?.Pause();
            settingsView.Show();
        }
        else
        {
            Debug.LogWarning("[LoginMenu] SettingsView not found.");
        }
    }

    public void OnLanguageClicked()
    {
        _currentLanguageIndex = (_currentLanguageIndex + 1) % SupportedLanguages.Length;
        string lang = SupportedLanguages[_currentLanguageIndex];
        PlayerPrefs.SetString("GameLanguage", lang);
        PlayerPrefs.Save();
        Debug.Log($"[LoginMenu] Language changed to: {lang}");

        var label = languageButton != null ? languageButton.transform.Find("Label")?.GetComponent<Text>() : null;
        if (label != null) label.text = lang.Substring(0, 2).ToUpper();
    }

    public void OnQuitClicked()
    {
        Debug.Log("[LoginMenu] Quit application requested");
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirmation("CONFIRM EXIT", "Terminate session and exit Railbound?", () =>
            {
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
        else
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public void ShowAccountLogin()
    {
        if (accountLoginPopup == null) return;
        ambientVFX?.Pause();
        accountLoginPopup.gameObject.SetActive(true);
        accountLoginPopup.alpha = 1f;
        accountLoginPopup.interactable = true;
        accountLoginPopup.blocksRaycasts = true;
    }

    public void HideAccountLogin()
    {
        if (accountLoginPopup == null) return;
        accountLoginPopup.alpha = 0f;
        accountLoginPopup.interactable = false;
        accountLoginPopup.blocksRaycasts = false;
        ambientVFX?.Resume();
    }

    public void OnModalClosed()
    {
        ambientVFX?.Resume();
    }
}
