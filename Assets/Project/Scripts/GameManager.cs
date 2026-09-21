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

    private void Awake(){
        Instance = this;
    }
    
    

    public void LoadSceneAsync(string sceneName)
    {
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

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            if (loadingSlider != null)
            {
                loadingSlider.value = progress;
            }

            if (loadingText != null)
            {
                loadingText.text = Mathf.RoundToInt(progress * 100) + "%";
            }

            if (asyncLoad.progress >= 0.9f)
            {
                if (loadingSlider != null)
                {
                    loadingSlider.value = 1f;
                }

                if (loadingText != null)
                {
                    loadingText.text = "100%";
                }

                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
