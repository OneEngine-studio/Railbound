using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Scene Loading")]
    public Slider loadingSlider;
    public TextMeshProUGUI loadingText;
    public string sceneToLoad = "NextScene";

    private bool isSceneLoading;
    private string activeSceneLoadName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("GameManager.LoadSceneAsync called with an empty scene name.");
            return;
        }

        if (isSceneLoading)
        {
            Debug.LogWarning($"Scene load already in progress for '{activeSceneLoadName}'. Ignoring request to load '{sceneName}'.");
            return;
        }

        if (!SceneExistsInBuildSettings(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' was not found in the build settings. Verify the scene name or add it to Build Settings.");
            return;
        }

        sceneToLoad = sceneName;
        activeSceneLoadName = sceneName;
        isSceneLoading = true;

        if (loadingSlider != null)
        {
            loadingSlider.minValue = 0f;
            loadingSlider.maxValue = 1f;
            loadingSlider.value = 0f;
        }

        if (loadingText != null)
        {
            loadingText.text = "0%";
        }

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private bool SceneExistsInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string buildSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        if (asyncLoad == null)
        {
            Debug.LogError($"Failed to start scene load for '{sceneName}'.");
            FinishSceneLoad();
            yield break;
        }

        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            UpdateLoadingUI(progress);

            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        UpdateLoadingUI(1f);
        FinishSceneLoad();
    }

    private void UpdateLoadingUI(float progress)
    {
        if (loadingSlider != null)
        {
            loadingSlider.value = progress;
        }

        if (loadingText != null)
        {
            loadingText.text = Mathf.RoundToInt(progress * 100f) + "%";
        }
    }

    private void FinishSceneLoad()
    {
        isSceneLoading = false;
        activeSceneLoadName = null;
    }
}
