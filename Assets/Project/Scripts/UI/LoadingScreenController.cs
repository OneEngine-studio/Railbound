using System.Collections;
using Railbound.BR;
using Railbound.Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Railbound.UI
{
    /// <summary>
    /// Cinematic loading screen: looping background video, smoothed progress bar, rotating tips and a fade in / out.
    /// Load any scene through it with <see cref="Load"/>; opening the LoadingScreen scene directly loads <see cref="defaultScene"/>.
    /// </summary>
    public class LoadingScreenController : MonoBehaviour
    {
        public const string LoadingSceneName = "LoadingScreen";

        /// <summary>The scene the loading screen is about to load. Set by <see cref="Load"/>.</summary>
        public static string NextScene { get; private set; }

        /// <summary>Show the loading screen and load <paramref name="sceneName"/> behind it.</summary>
        public static void Load(string sceneName)
        {
            NextScene = sceneName;
            if (Application.CanStreamedLevelBeLoaded(LoadingSceneName)) SceneManager.LoadScene(LoadingSceneName);
            else SceneManager.LoadSceneAsync(sceneName);   // loading scene not in the build: still load the target
        }

        [Header("Background")]
        [SerializeField] private VideoPlayer video;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private bool muteVideo = true;

        [Header("Layout")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform progressGlow;
        [SerializeField] private RectTransform spinner;
        [SerializeField] private TextMeshProUGUI percentText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI mapNameText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private TextMeshProUGUI tipCounterText;
        [SerializeField] private CanvasGroup tipGroup;
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("Loading")]
        [SerializeField] private string defaultScene = "MAP-2";
        [SerializeField] private float minLoadTime = 4f;
        [SerializeField] private float fadeTime = 0.6f;
        [SerializeField] private float tipInterval = 6f;

        [Header("Game server")]
        [Tooltip("When the lobby got a game server from Nakama, keep this screen up after the map is loaded until the Netcode client is connected and the match is synchronised.")]
        [SerializeField] private bool waitForServer = true;
        [Tooltip("Seconds the client may take to reach the allocated game server before it is sent back to the lobby.")]
        [SerializeField] private float connectTimeoutSeconds = 25f;
        [SerializeField] private string lobbySceneName = "GameLobby";

        [TextArea(1, 3)]
        [SerializeField]
        private string[] tips =
        {
            "The safe zone keeps shrinking. Stay inside it or it will hurt.",
            "Check the minimap often: it shows the zone edge and where you are.",
            "AI combatants are hunting too. Use cover and move carefully.",
            "Pick up health and ammo as you go, you will need both.",
            "Tune exposure, DLSS and shadows in the pause menu under Visuals.",
            "Squad up in the lobby before you deploy for a safer start.",
        };

        private static readonly string[] Stages =
        {
            "INITIALIZING", "LOADING TERRAIN", "STREAMING ASSETS", "BUILDING NAVIGATION", "SPAWNING COMBATANTS", "SYNCHRONIZING", "READY",
        };

        private RenderTexture videoTexture;
        private float shown;      // smoothed progress that is drawn
        private float target;     // real progress from the async load

        private void Awake()
        {
            Time.timeScale = 1f;
            if (root != null) root.alpha = 0f;
            if (versionText != null) versionText.text = "BUILD " + Application.version;
            if (mapNameText != null) mapNameText.text = DisplayName(string.IsNullOrEmpty(NextScene) ? defaultScene : NextScene);
            SetProgress(0f);
            SetupVideo();
        }

        private IEnumerator Start()
        {
            string scene = string.IsNullOrEmpty(NextScene) ? defaultScene : NextScene;
            StartCoroutine(TipLoop());
            StartCoroutine(Fade(0f, 1f));

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("[LoadingScreen] Scene '" + scene + "' is not in the build settings.");
                if (statusText != null) statusText.text = "SCENE NOT FOUND";
                yield break;
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            float elapsed = 0f;

            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                target = Mathf.Clamp01(op.progress / 0.9f);
                // never show 100% before the minimum time has passed, so the screen does not flash by
                float cap = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, minLoadTime));
                shown = Mathf.MoveTowards(shown, Mathf.Min(target, cap), Time.unscaledDeltaTime * 0.45f);
                SetProgress(shown);

                if (op.progress >= 0.9f && shown >= 0.999f) break;
                yield return null;
            }

            if (statusText != null) statusText.text = Stages[Stages.Length - 1];
            yield return new WaitForSecondsRealtime(0.35f);

            if (!waitForServer || !GameServerConnection.Instance.HasEndpoint)
            {
                // offline / direct play: nothing to wait for, show the map as soon as it is loaded
                yield return Fade(1f, 0f);
                op.allowSceneActivation = true;
                yield break;
            }

            // The Netcode client only exists inside the map scene, so this screen has to survive the scene change and keep
            // reporting what the connection is doing until the player is really in the match.
            KeepAliveAcrossSceneLoad();
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return ConnectionPhase();
        }

        private readonly System.Collections.Generic.List<GameObject> keptRoots = new System.Collections.Generic.List<GameObject>();

        private void KeepAliveAcrossSceneLoad()
        {
            AddKeptRoot(transform.root.gameObject);
            if (root != null) AddKeptRoot(root.transform.root.gameObject);
            if (video != null) AddKeptRoot(video.transform.root.gameObject);
            foreach (var go in keptRoots)
            {
                DontDestroyOnLoad(go);
                foreach (var canvas in go.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas) canvas.sortingOrder = 30000;   // above the map's own UI
            }
        }

        private void AddKeptRoot(GameObject go)
        {
            if (go != null && !keptRoots.Contains(go)) keptRoots.Add(go);
        }

        private void DismissOverlay()
        {
            foreach (var go in keptRoots)
                if (go != null) Destroy(go);
            keptRoots.Clear();
        }

        /// <summary>Shows the real connection stages of the Netcode client until the match is synchronised, or sends the player back.</summary>
        private IEnumerator ConnectionPhase()
        {
            var conn = GameServerConnection.Instance;
            string server = conn.Endpoint;   // for the log only: players never see addresses or raw errors
            // the singleton retries on its own; never give up before it has
            float connectLimit = Mathf.Max(connectTimeoutSeconds, conn.ConnectTimeoutSeconds + 10f);
            SetProgress(1f);
            if (percentText != null) percentText.text = "";

            var offlineChar = GameObject.Find("TPS Character");
            if (offlineChar != null && offlineChar.GetComponent<NetworkObject>() == null)
            {
                offlineChar.SetActive(false);
            }

            float elapsed = 0f;
            string failure = null;

            // the singleton owns the connection (it started in the lobby or starts now) and keeps going across the scene change
            if (conn.State == GameServerState.Allocated && !conn.Connect()) failure = conn.LastError;

            while (failure == null)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.25f);   // long frames (scene activation) must not eat the timeout
                var nm = NetworkManager.Singleton;
                string stage;
                bool done = false;

                if (conn.State == GameServerState.Failed)
                {
                    failure = string.IsNullOrEmpty(conn.LastError) ? "CONNECTION TO THE GAME SERVER FAILED" : conn.LastError;
                    stage = "RETURNING TO LOBBY";
                }
                else if (nm == null)
                {
                    stage = "PREPARING NETWORK";
                    if (elapsed > connectLimit) failure = "NETWORK UNAVAILABLE";
                }
                else
                {
                    if (!nm.IsConnectedClient)
                    {
                        // first try: plain connecting; later tries read as a calm "still working on it", not as an error
                        stage = conn.Attempt <= 1 ? "CONNECTING TO MATCH" : "WARMING UP MATCH SERVER";
                        if (elapsed > connectLimit) failure = "COULD NOT REACH THE GAME SERVER";
                    }
                    else if (nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
                    {
                        stage = "JOINING MATCH";
                        if (elapsed > connectLimit + 15f) failure = "THE SERVER DID NOT SPAWN YOUR PLAYER";
                    }
                    else
                    {
                        var match = BRMatchCoordinator.Instance;
                        if (match == null || !match.IsSpawned)
                        {
                            stage = "SYNCHRONIZING MATCH";
                            if (elapsed > connectLimit + 15f) done = true;   // no coordinator in this scene: nothing more to wait for
                        }
                        else
                        {
                            stage = "MATCH READY  " + match.TotalCount + " COMBATANTS";
                            done = true;
                        }
                    }
                }

                if (statusText != null) statusText.text = stage;
                if (done) break;
                yield return null;
            }

            if (failure != null)
            {
                Debug.LogWarning("[LoadingScreen] " + failure + " (" + server + ")");
                if (statusText != null) statusText.text = "RETURNING TO LOBBY";   // the technical reason is in the log only
                yield return new WaitForSecondsRealtime(1.5f);
                conn.Clear();   // leaves the server and forgets the address
                LobbyNotice.Set("COULD NOT JOIN THE MATCH - PLEASE TRY AGAIN", LobbyNotice.Kind.Problem);   // the lobby tells the player why they are back
                if (Application.CanStreamedLevelBeLoaded(lobbySceneName)) SceneManager.LoadScene(lobbySceneName);
                DismissOverlay();
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.4f);
            yield return Fade(1f, 0f);
            DismissOverlay();
        }

        private void Update()
        {
            if (spinner != null) spinner.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
        }

        private void SetProgress(float p)
        {
            if (progressFill != null) progressFill.fillAmount = p;
            if (percentText != null) percentText.text = Mathf.RoundToInt(p * 100f) + "%";
            if (statusText != null && p < 0.999f)
                statusText.text = Stages[Mathf.Min(Stages.Length - 2, Mathf.FloorToInt(p * (Stages.Length - 1)))];
            if (progressGlow != null && progressFill != null)
            {
                var bar = (RectTransform)progressFill.transform;
                progressGlow.anchoredPosition = new Vector2(bar.rect.width * p, 0f);
            }
        }

        private static string DisplayName(string scene)
        {
            return string.IsNullOrEmpty(scene) ? "" : scene.Replace('_', ' ').ToUpperInvariant();
        }

        // ---------------- video ----------------
        private void SetupVideo()
        {
            if (video == null || videoImage == null) return;
            videoTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "LoadingVideoRT" };
            videoTexture.Create();
            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = videoTexture;
            video.isLooping = true;
            video.playOnAwake = false;
            video.audioOutputMode = muteVideo ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
            videoImage.texture = videoTexture;
            videoImage.color = Color.clear;                 // stay dark until the first frame is ready
            video.prepareCompleted += OnVideoPrepared;
            video.Prepare();
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            vp.Play();
            if (videoImage != null) videoImage.color = Color.white;
        }

        private void OnDestroy()
        {
            if (video != null) video.prepareCompleted -= OnVideoPrepared;
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
        }

        // ---------------- tips ----------------
        private IEnumerator TipLoop()
        {
            if (tips == null || tips.Length == 0 || tipText == null) yield break;
            int i = Random.Range(0, tips.Length);
            while (true)
            {
                tipText.text = tips[i];
                if (tipCounterText != null) tipCounterText.text = "TIP  " + (i + 1) + " / " + tips.Length;
                yield return FadeGroup(tipGroup, 0f, 1f, 0.5f);
                yield return new WaitForSecondsRealtime(tipInterval);
                yield return FadeGroup(tipGroup, 1f, 0f, 0.5f);
                i = (i + 1) % tips.Length;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            yield return FadeGroup(root, from, to, fadeTime);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float time)
        {
            if (group == null) yield break;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
