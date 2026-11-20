using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.UI
{
    /// <summary>
    /// Switches off the pieces of the original JUTPS / Reach HUD that the match HUD replaced but that still sit in the MAP-2 scene (the
    /// scene file is not edited by hand): the green frame counter under the network chip (HudNetworkChip shows the connection) and the
    /// Reach feed notifications (retired, see BRFeedNotificationManager). Clients only.
    /// </summary>
    public static class LegacyHudRetire
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isBatchMode || scene.name != "MAP-2") return;
            foreach (var fps in Object.FindObjectsByType<JUTPS.Utilities.FPSCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fps != null && fps.gameObject.scene == scene) fps.gameObject.SetActive(false);
            foreach (var feed in Object.FindObjectsByType<Railbound.BR.BRFeedNotificationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (feed != null) feed.enabled = false;
        }
    }
}
