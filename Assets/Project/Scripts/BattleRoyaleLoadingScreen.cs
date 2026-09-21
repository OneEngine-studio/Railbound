using System.Collections;
using System.Collections.Generic;
using System.Data;
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

    [Header("Network")]
    public NetworkManager networkManager;

    [Header("Battle Royale Settings")]
    public string gameScene = "Lobby";
    public float minLoadTime = 4f;
    public float connectionCheckTimeout = 10f;

    private float loadTimer;
    private AsyncOperation loadOp;

    void Start()
    {
        if (loadingCanvas != null)
            loadingCanvas.GetComponent<RectTransform>().localScale = Vector3.one;

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        if (progressPercent != null)
            progressPercent.gameObject.SetActive(false);

        StartCoroutine(CheckConnectionAndLoad());
    }

    IEnumerator CheckConnectionAndLoad()
    {
        statusText.text = "Checking internet connection...";

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

        statusText.text = "Connected! Starting load...";
        StartCoroutine(LoadingRoutine());
    }

    void OnConnectionFailed(string error)
    {
        statusText.text = error + ". Please check your connection.";

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

        loadOp = SceneManager.LoadSceneAsync(gameScene);
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
                statusText.text = "Connecting to servers.";
            else if (raw < 0.5f && statusText != null)
                statusText.text = "Connecting to servers..";
            else if (raw < 0.75f && statusText != null)
                statusText.text = "Connecting to servers...";
            else if (raw < 0.95f && statusText != null)
                statusText.text = "Connecting to servers....";
            else if (statusText != null)
                statusText.text = "Connecting to servers ok";

            if (loadOp.progress >= 0.9f && loadTimer >= minLoadTime)
            {
                if (progressBar != null)
                    progressBar.value = 1f;
                if (progressPercent != null)
                    progressPercent.text = "100%";
                if (statusText != null)
                    statusText.text = "Connecting to servers ok";

                yield return new WaitForSeconds(0.5f);
                loadOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
