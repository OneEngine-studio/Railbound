using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class BattleRoyaleLoadingScreen : MonoBehaviour
{
    [Header("UI References")]
    public Slider progressBar;
    public TextMeshProUGUI progressPercent;
    public TextMeshProUGUI statusText;
    public Canvas loadingCanvas;
    public Button retryButton;

    [Header("Micro-Interactions & Tactical Polish")]
    public CanvasGroup canvasGroup;
    public Graphic progressGlow;
    public TextMeshProUGUI tipTitleText;
    public TextMeshProUGUI tipDescriptionText;
    public float fadeInDuration = 0.4f;
    public float fadeOutDuration = 0.35f;

    [Header("Network")]
    public NetworkManager networkManager;

    [Header("Battle Royale Settings")]
    public string gameScene = "GameLobby";
    public float minLoadTime = 3f;
    public float connectionCheckTimeout = 10f;

    private static readonly (string title, string tip)[] TacticalTips = new[]
    {
        ("// MISSION INTEL // CONVOY DEFENSE", "Railbound convoys transport critical munitions across disputed sectors. Hold perimeter high ground during transit."),
        ("// TACTICAL DOCTRINE // RECON SQUAD", "Coordinate squad loadouts with thermal optics and suppression fire before breaching contested depot sectors."),
        ("// LOGISTICS PROTOCOL // SUPPLY CRATES", "Rare weapon attachments and tactical gear drop at high-tier extraction checkpoints throughout Iron Valley."),
        ("// SECTOR DIRECTIVE // EXTRACTION", "Extraction relays broadcast localized signals for 30 seconds. Prepare defensive perimeters before activating beacon.")
    };

    private float loadTimer;
    private AsyncOperation loadOp;
    private Coroutine glowPulseRoutine;

    void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    void Start()
    {
        if (loadingCanvas != null)
            loadingCanvas.GetComponent<RectTransform>().localScale = Vector3.one;

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        if (progressBar != null)
            progressBar.gameObject.SetActive(true);

        if (progressPercent != null)
            progressPercent.gameObject.SetActive(true);

        // Display random tactical tip
        int tipIdx = Random.Range(0, TacticalTips.Length);
        if (tipTitleText != null) tipTitleText.text = TacticalTips[tipIdx].title;
        if (tipDescriptionText != null) tipDescriptionText.text = TacticalTips[tipIdx].tip;

        // Start glow pulsation
        if (progressGlow != null)
            glowPulseRoutine = StartCoroutine(GlowPulseRoutine());

        // Fade in
        StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration));

        StartCoroutine(CheckConnectionAndLoad());
    }

    IEnumerator GlowPulseRoutine()
    {
        float timer = 0f;
        Color baseCol = progressGlow.color;
        while (true)
        {
            timer += Time.unscaledDeltaTime * 3f;
            float alpha = Mathf.Lerp(0.35f, 0.95f, (Mathf.Sin(timer) + 1f) * 0.5f);
            progressGlow.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
            yield return null;
        }
    }

    IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;
        float elapsed = 0f;
        canvasGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    IEnumerator CheckConnectionAndLoad()
    {
        if (statusText != null)
            statusText.text = "INITIALIZING TACTICAL RELAY...";

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            OnConnectionFailed("No internet connection");
            yield break;
        }

        if (networkManager != null && NetworkManager.Singleton != null)
        {
            float timeout = connectionCheckTimeout;
            bool connected = false;

            while (timeout > 0f)
            {
                if (NetworkManager.Singleton.IsConnectedClient ||
                    NetworkManager.Singleton.IsServer)
                {
                    connected = true;
                    break;
                }

                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!connected)
            {
                OnConnectionFailed("Could not connect to server");
                yield break;
            }
        }

        OnConnectionSuccess();
    }

    void OnConnectionSuccess()
    {
        if (loadingCanvas != null)
            loadingCanvas.GetComponent<RectTransform>().localScale = Vector3.one;

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        if (statusText != null)
            statusText.text = "SYNCHRONIZING OPERATIONAL ASSETS...";

        StartCoroutine(LoadingRoutine());
    }

    void OnConnectionFailed(string error)
    {
        if (statusText != null)
            statusText.text = error.ToUpper() + ". CHECK NETWORK LINK.";

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(true);
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RetryConnection);
        }

        Debug.LogWarning("Connection check failed: " + error);
    }

    public void RetryConnection()
    {
        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        StartCoroutine(CheckConnectionAndLoad());
    }

    IEnumerator LoadingRoutine()
    {
        loadTimer = 0f;

        string targetScene = gameScene;
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            if (Application.CanStreamedLevelBeLoaded("GameLobby")) targetScene = "GameLobby";
            else if (Application.CanStreamedLevelBeLoaded("MAP-1")) targetScene = "MAP-1";
        }

        loadOp = SceneManager.LoadSceneAsync(targetScene);
        loadOp.allowSceneActivation = false;

        while (!loadOp.isDone)
        {
            loadTimer += Time.deltaTime;

            float raw = Mathf.Clamp01(loadOp.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = raw;

            if (progressPercent != null)
                progressPercent.text = Mathf.RoundToInt(raw * 100) + "%";

            if (raw < 0.25f && statusText != null)
                statusText.text = "ESTABLISHING SECURE PROTOCOL...";
            else if (raw < 0.5f && statusText != null)
                statusText.text = "SYNCHRONIZING TACTICAL ASSETS...";
            else if (raw < 0.75f && statusText != null)
                statusText.text = "STREAMING HIGH-RES SECTOR MESH...";
            else if (raw < 0.95f && statusText != null)
                statusText.text = "AUTHENTICATING SQUAD ROSTER...";
            else if (statusText != null)
                statusText.text = "SECTOR VERIFIED // READY FOR DEPLOYMENT";

            if (loadOp.progress >= 0.9f && loadTimer >= minLoadTime)
            {
                if (progressBar != null)
                    progressBar.value = 1f;
                if (progressPercent != null)
                    progressPercent.text = "100%";
                if (statusText != null)
                    statusText.text = "MISSION LINK ACTIVE // INITIALIZING...";

                yield return new WaitForSeconds(0.2f);
                yield return StartCoroutine(FadeCanvasGroup(1f, 0f, fadeOutDuration));
                loadOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
