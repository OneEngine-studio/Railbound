using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using Railbound.BR;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Makes the dedicated server a well-behaved Agones game server. It only runs inside an Agones pod (the sidecar sets
/// AGONES_SDK_HTTP_PORT); everywhere else (editor, host, local server) it does nothing.
///
///   1. when Netcode is listening          -> POST /ready   (the fleet may now hand this server out)
///   2. every 2 seconds                    -> POST /health  (otherwise Agones restarts the pod)
///   3. when Nakama allocates the server   -> reads the match id and the number of expected players from the
///                                            GameServer annotations and tells the match coordinator
///   4. when the match is over, nobody joined, or everybody left -> POST /shutdown and quit, so the fleet starts a fresh server
/// </summary>
public sealed class AgonesServerLifecycle : MonoBehaviour
{
    private const float HealthSeconds = 2f;
    private const float PollSeconds = 2f;
    private const float JoinGraceSeconds = 120f;     // allocated, but nobody ever connected
    private const float EmptyGraceSeconds = 30f;     // everybody left after the match had players
    private const float AfterMatchSeconds = 8f;      // extra time after the results screen before the server goes away

    private string baseUrl;
    private bool shuttingDown;

    public static string MatchId { get; private set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStart()
    {
        string port = Environment.GetEnvironmentVariable("AGONES_SDK_HTTP_PORT");
        if (string.IsNullOrEmpty(port)) return;                     // not inside an Agones pod
        if (FindFirstObjectByType<AgonesServerLifecycle>() != null) return;

        var go = new GameObject("[Agones] Lifecycle");
        DontDestroyOnLoad(go);
        go.AddComponent<AgonesServerLifecycle>().baseUrl = "http://localhost:" + port;
        Debug.Log("[Agones] SDK sidecar found on port " + port + ".");
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(baseUrl)) { enabled = false; return; }
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // 1. ready once the Netcode server is listening
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !NetworkManager.Singleton.IsListening)
            yield return null;
        yield return Post("/ready", "ready");
        StartCoroutine(HealthLoop());

        // 2. watch the allocation and the match
        bool allocated = false, everJoined = false;
        float allocatedAt = 0f, lastHumanAt = 0f, endedAt = 0f;

        while (!shuttingDown)
        {
            yield return new WaitForSecondsRealtime(PollSeconds);

            if (!allocated)
            {
                string state = null; JObject gs = null;
                yield return Get("/gameserver", j => gs = j);
                if (gs != null) state = (string)gs["status"]?["state"];
                if (state == "Allocated")
                {
                    allocated = true;
                    allocatedAt = Time.unscaledTime;
                    MatchId = (string)gs["object_meta"]?["annotations"]?["railbound.io/match-id"] ?? "";
                    int expected = 0;
                    int.TryParse((string)gs["object_meta"]?["annotations"]?["railbound.io/expected-players"], out expected);
                    BRMatchCoordinator.ExpectedHumans = expected;
                    Debug.Log($"[Agones] Allocated: match '{MatchId}', expecting {expected} player(s).");
                }
                continue;
            }

            int humans = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;
            if (humans > 0) { everJoined = true; lastHumanAt = Time.unscaledTime; }

            if (!everJoined && Time.unscaledTime - allocatedAt > JoinGraceSeconds) { yield return Shutdown("allocated but nobody joined"); yield break; }
            if (everJoined && humans == 0 && Time.unscaledTime - lastHumanAt > EmptyGraceSeconds) { yield return Shutdown("everybody left"); yield break; }

            var coordinator = BRMatchCoordinator.Instance;
            if (coordinator != null && coordinator.State == BRMatchState.MatchEnded)
            {
                if (endedAt <= 0f) endedAt = Time.unscaledTime;
                if (Time.unscaledTime - endedAt > coordinator.ReturnToLobbyDelay + AfterMatchSeconds) { yield return Shutdown("match over"); yield break; }
            }
        }
    }

    private IEnumerator HealthLoop()
    {
        var wait = new WaitForSecondsRealtime(HealthSeconds);
        while (!shuttingDown)
        {
            yield return Post("/health", null);
            yield return wait;
        }
    }

    private IEnumerator Shutdown(string reason)
    {
        shuttingDown = true;
        Debug.Log("[Agones] Shutting down: " + reason);
        yield return Post("/shutdown", "shutdown");
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit(0);
    }

    // ------------------------------------------------------------------ REST SDK

    private IEnumerator Post(string path, string logName)
    {
        using (var req = new UnityWebRequest(baseUrl + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[Agones] POST {path} failed: {req.error} {req.downloadHandler?.text}");
            else if (logName != null)
                Debug.Log($"[Agones] {logName} acknowledged.");
        }
    }

    private IEnumerator Get(string path, Action<JObject> onResult)
    {
        using (var req = UnityWebRequest.Get(baseUrl + path))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Agones] GET {path} failed: {req.error}");
                yield break;
            }
            try { onResult(JObject.Parse(req.downloadHandler.text)); }
            catch (Exception ex) { Debug.LogWarning("[Agones] Could not parse the GameServer reply: " + ex.Message); }
        }
    }
}
