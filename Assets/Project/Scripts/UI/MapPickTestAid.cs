using System.Collections;
using System.IO;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Test aids for the Classic BR map choice (client):
    ///   <c>-classicmap ID</c>  chooses the map as the player would on the mode screen (it is saved like a real choice);
    ///   <c>-mapshot DIR</c>    in the lobby, opens the mode screen on Classic BR and saves a screenshot of the map strip, then closes it.
    /// </summary>
    public class MapPickTestAid : MonoBehaviour
    {
        private string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] == "-classicmap")
                {
                    var map = Railbound.Maps.MapCatalog.Find(a[i + 1]);
                    if (map != null) { GameModeCatalog.SetClassicMap(map); Debug.Log("[MapPickTest] Classic BR map: " + map.Id + ", queue key " + GameModeCatalog.QueueKey); }
                }
                if (a[i] == "-mapshot" && FindFirstObjectByType<MapPickTestAid>() == null)
                {
                    var go = new GameObject("[Test] Map pick");
                    DontDestroyOnLoad(go);
                    go.AddComponent<MapPickTestAid>().dir = a[i + 1];
                }
            }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            LobbyGameModePanel panel = null;
            float waited = 0f;
            while (panel == null && waited < 180f) { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); panel = FindFirstObjectByType<LobbyGameModePanel>(FindObjectsInactive.Include); }
            if (panel == null) { Debug.LogError("[MapPickTest] no mode screen"); yield break; }
            yield return new WaitForSecondsRealtime(4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "lobby.png"));
            yield return new WaitForSecondsRealtime(1f);
            panel.Show();
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "mode_screen.png"));
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[MapPickTest] DONE, queue key " + GameModeCatalog.QueueKey);
        }
    }
}
