using UnityEngine;
using UnityEngine.SceneManagement;
using JUTPS;

namespace Michsky.UI.Reach
{
    [AddComponentMenu("Reach UI/Custom/Reach Pause Bridge")]
    public class ReachPauseBridge : MonoBehaviour
    {
        [Header("References")]
        public PauseMenuManager pauseMenuManager;

        [Header("Scene Management")]
        public string mainMenuSceneName = "LoginMenu";

        void Awake()
        {
            if (pauseMenuManager == null)
            {
                pauseMenuManager = GetComponent<PauseMenuManager>();
            }
        }

        void OnEnable()
        {
            if (pauseMenuManager != null)
            {
                pauseMenuManager.onOpen.AddListener(OnPauseOpened);
                pauseMenuManager.onClose.AddListener(OnPauseClosed);
            }
        }

        void OnDisable()
        {
            if (pauseMenuManager != null)
            {
                pauseMenuManager.onOpen.RemoveListener(OnPauseOpened);
                pauseMenuManager.onClose.RemoveListener(OnPauseClosed);
            }
        }

        public void OnPauseOpened()
        {
            JUPauseGame.SetPaused(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void OnPauseClosed()
        {
            JUPauseGame.SetPaused(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            JUPauseGame.SetPaused(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
