using System.Collections;
using TMPro;
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
            yield return Fade(1f, 0f);
            op.allowSceneActivation = true;
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
