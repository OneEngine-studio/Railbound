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

        void Start()
        {
            RelabelForMatch();
        }

        /// <summary>
        /// In a match the pause menu's EXIT leaves the match (back to the lobby, out of the match for good), so it says LEAVE, and its
        /// confirmation says so too. The map scene is not in the repository, so the labels are changed here at runtime.
        /// </summary>
        private void RelabelForMatch()
        {
            if (Railbound.BR.BRMatchCoordinator.Instance == null && FindFirstObjectByType<Railbound.BR.BRMatchSetup>() == null) return;
            foreach (var b in FindObjectsByType<ButtonManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b == null || !string.Equals((b.buttonText ?? "").Trim(), "Exit", System.StringComparison.OrdinalIgnoreCase)) continue;
                b.useLocalization = false;   // a language refresh would put "Exit" back
                b.SetText("Leave");
            }
            foreach (var m in FindObjectsByType<ModalWindowManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (m == null || (m.titleText ?? "").IndexOf("exit", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                m.useLocalization = false;
                m.titleText = "Leave match";
                m.descriptionText = "Leave this match and go back to the lobby? You are out of the match and cannot rejoin it.";
                m.UpdateUI();
                if (m.windowTitle != null) m.windowTitle.text = m.titleText;
                if (m.windowDescription != null) m.windowDescription.text = m.descriptionText;
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
            RelabelForMatch();
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

            // In a match, EXIT means "leave the match": disconnect from the game server and go back to the lobby. Loading the
            // login scene directly kept the Netcode client connected and destroyed the match's network objects on a client
            // ("Destroy a spawned NetworkObject on a non-host client is not valid"), and signed-in players landed on the login screen.
            var match = Railbound.BR.BRMatchCoordinator.Instance;
            if (match != null)
            {
                match.LeaveMatch();
                return;
            }

            // outside a match: still close any network session before the scene change
            if (Railbound.Networking.GameServerConnection.Instance != null) Railbound.Networking.GameServerConnection.Instance.Clear();
            var net = Unity.Netcode.NetworkManager.Singleton;
            if (net != null && net.IsListening) net.Shutdown();

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
