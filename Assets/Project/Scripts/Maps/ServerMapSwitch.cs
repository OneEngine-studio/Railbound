using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.Maps
{
    /// <summary>
    /// A fleet server boots with MAP-2 before it knows its match. When Nakama allocates it for a match on another map (the player chose a
    /// map for Classic BR), the server reloads the match scene with that map before anybody joins. Joins wait while it reloads
    /// (<see cref="Busy"/>). Test aid: <c>-switchmap ID</c> on a server switches 15 s after it starts listening, like an allocation.
    /// </summary>
    public static class ServerMapSwitch
    {
        /// <summary>True from the switch until the new map is built and the match scene is running again.</summary>
        public static bool Busy { get; private set; }

        private const float MaxSeconds = 90f;
        private static float startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Busy = false; }

        /// <summary>Rebuilds the match on <paramref name="mapId"/> if this server built another map. False when nothing had to be done.</summary>
        public static bool SwitchTo(string mapId)
        {
            var def = MapCatalog.Find(mapId);
            var nm = NetworkManager.Singleton;
            if (def == null || nm == null || !nm.IsServer || nm.SceneManager == null) return false;
            var built = MapLoader.Active ?? MapCatalog.Map2;
            if (built.Id == def.Id) { Debug.Log("[Map] Allocated for " + def.Id + ": already built."); return false; }
            if (nm.ConnectedClientsIds.Count > (nm.IsHost ? 1 : 0)) { Debug.LogWarning("[Map] Not switching to " + def.Id + ": players are already connected."); return false; }

            Debug.Log($"[Map] Allocated for {def.Id}: rebuilding the match scene (was {built.Id}).");
            MapSelection.Set(def);
            Busy = true;
            startedAt = Time.realtimeSinceStartup;
            nm.SceneManager.OnLoadEventCompleted -= OnLoaded;
            nm.SceneManager.OnLoadEventCompleted += OnLoaded;
            var status = nm.SceneManager.LoadScene(MapLoader.MatchSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError("[Map] Could not reload the match scene: " + status);
                nm.SceneManager.OnLoadEventCompleted -= OnLoaded;
                Busy = false;
                return false;
            }
            return true;
        }

        private static void OnLoaded(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> done, System.Collections.Generic.List<ulong> timedOut)
        {
            if (sceneName != MapLoader.MatchSceneName) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SceneManager != null) nm.SceneManager.OnLoadEventCompleted -= OnLoaded;
            Runner.Instance.StartCoroutine(Finish());
        }

        // one more frame, so the scene's Start methods (match setup, spawns, bots) have run before players come in
        private static IEnumerator Finish()
        {
            yield return null;
            yield return null;
            Busy = false;
            var active = MapLoader.Active ?? MapCatalog.Map2;
            Debug.Log($"[Map] The match scene runs on {active.Id} ({Time.realtimeSinceStartup - startedAt:0.0} s).");
        }

        /// <summary>Joins wait for the switch; after <see cref="MaxSeconds"/> they stop waiting (the switch failed).</summary>
        public static bool StillBusy => Busy && Time.realtimeSinceStartup - startedAt < MaxSeconds;

        // ---------------------------------------------------------------- helper object and test aid
        private sealed class Runner : MonoBehaviour
        {
            private static Runner instance;
            public static Runner Instance
            {
                get
                {
                    if (instance != null) return instance;
                    var go = new GameObject("[Map] Server switch");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                    return instance;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void TestAid()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], "-switchmap", StringComparison.OrdinalIgnoreCase)) { Runner.Instance.StartCoroutine(SwitchLater(a[i + 1])); return; }
        }

        private static IEnumerator SwitchLater(string mapId)
        {
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !NetworkManager.Singleton.IsListening) yield return null;
            yield return new WaitForSecondsRealtime(15f);
            Debug.Log("[MapSwitchTest] switching to " + mapId);
            SwitchTo(mapId);
        }
    }
}
