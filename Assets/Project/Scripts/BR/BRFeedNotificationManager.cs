using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.Reach;

namespace Railbound.BR
{
    public enum BRNotificationPriority
    {
        Info,       // Cyan / White - Safe zone updates, entering safe zone
        Warning,    // Yellow (#FFD000) - Zone closing soon, storm moving
        Critical    // Red (#FF3333) - Outside safe zone, taking storm damage
    }

    /// <summary>
    /// Manages Reach UI Feed Notifications for Battle Royale zone events, storm alerts, and tactical messages.
    /// Spawns Reach UI Feed Notification prefabs, colorizes them according to priority (Yellow for Warning,
    /// Red for Critical, Cyan for Info), and plays the designated tactical notification sound.
    /// </summary>
    [DisallowMultipleComponent]
    public class BRFeedNotificationManager : MonoBehaviour
    {
        private static BRFeedNotificationManager _instance;
        public static BRFeedNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BRFeedNotificationManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Prefabs & Resources")]
        [SerializeField] private GameObject feedNotificationPrefab;
        [SerializeField] private AudioClip notificationSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private RectTransform notificationContainer;

        [Header("Tactical Icons")]
        [SerializeField] private Sprite warningIcon;
        [SerializeField] private Sprite criticalIcon;
        [SerializeField] private Sprite infoIcon;

        [Header("Theme Colors")]
        public static readonly Color WarningYellow = new Color(1.0f, 0.816f, 0.0f, 1.0f);     // #FFD000
        public static readonly Color WarningGlow   = new Color(1.0f, 0.816f, 0.0f, 0.25f);
        public static readonly Color WarningText   = new Color(1.0f, 0.88f, 0.20f, 1.0f);

        public static readonly Color CriticalRed  = new Color(1.0f, 0.20f, 0.20f, 1.0f);       // #FF3333
        public static readonly Color CriticalGlow = new Color(1.0f, 0.20f, 0.20f, 0.35f);
        public static readonly Color CriticalText = new Color(1.0f, 0.32f, 0.32f, 1.0f);

        public static readonly Color InfoCyan     = new Color(0.0f, 0.85f, 1.0f, 1.0f);        // #00D9FF
        public static readonly Color InfoGlow     = new Color(0.0f, 0.85f, 1.0f, 0.20f);
        public static readonly Color InfoText     = new Color(0.35f, 0.90f, 1.0f, 1.0f);

        private FeedNotification currentActiveNotification;
        private BRNotificationPriority currentPriority = BRNotificationPriority.Info;
        private Coroutine activeDismissCoroutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            ResolveDependencies();
        }

        public void ResolveDependencies()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D UI Audio
                audioSource.bypassEffects = true;
                audioSource.bypassListenerEffects = true;
                audioSource.ignoreListenerPause = true;
                audioSource.volume = 1f;
            }

            if (notificationSound == null)
            {
                notificationSound = LoadNotificationAudio();
            }

            if (feedNotificationPrefab == null)
            {
                feedNotificationPrefab = LoadFeedPrefab();
            }

            if (warningIcon == null)
            {
                warningIcon = LoadWarningIcon();
            }

            if (criticalIcon == null)
            {
                criticalIcon = LoadCriticalIcon();
            }

            if (infoIcon == null)
            {
                infoIcon = LoadInfoIcon();
            }

            if (notificationContainer == null)
            {
                SetupNotificationContainer();
            }
        }

        public void SetupNotificationContainer()
        {
            // Find existing Canvas in the scene
            Canvas targetCanvas = null;
            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name.Contains("JUTPS User Interface"))
                {
                    targetCanvas = c;
                    break;
                }
            }

            if (targetCanvas == null)
            {
                foreach (var c in canvases)
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }

            if (targetCanvas != null)
            {
                var existing = targetCanvas.transform.Find("BR_FeedNotification_Container") as RectTransform;
                if (existing != null)
                {
                    notificationContainer = existing;
                }
                else
                {
                    var containerObj = new GameObject("BR_FeedNotification_Container", typeof(RectTransform));
                    containerObj.transform.SetParent(targetCanvas.transform, false);
                    notificationContainer = containerObj.GetComponent<RectTransform>();

                    // Anchor Top-Center below compass/top UI
                    notificationContainer.anchorMin = new Vector2(0.5f, 1f);
                    notificationContainer.anchorMax = new Vector2(0.5f, 1f);
                    notificationContainer.pivot = new Vector2(0.5f, 1f);
                    notificationContainer.anchoredPosition = new Vector2(0f, -110f);
                    notificationContainer.sizeDelta = new Vector2(500f, 100f);
                    containerObj.transform.SetAsLastSibling();
                }
            }
        }

        private AudioClip LoadNotificationAudio()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/SoundClips/notification.mp3");
#else
            return Resources.Load<AudioClip>("Audio/notification");
#endif
        }

        private GameObject LoadFeedPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reach - Complete Sci-Fi UI/Prefabs/HUD/Feed Notification.prefab");
#else
            return Resources.Load<GameObject>("HUD/Feed Notification");
#endif
        }

        private Sprite LoadWarningIcon()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Resources/UI/Lobby/Icon_Warning.png")
                   ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Icons/Navigation/Close Circle (64x).png");
#else
            return null;
#endif
        }

        private Sprite LoadCriticalIcon()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Icons/Navigation/Close Circle (64x).png")
                   ?? LoadWarningIcon();
#else
            return null;
#endif
        }

        private Sprite LoadInfoIcon()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Icons/HUD/Shield (64x).png")
                   ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Reach - Complete Sci-Fi UI/Textures/Icons/Navigation/Checkmark Circle (64x).png");
#else
            return null;
#endif
        }

        public void ShowWarning(string message, float duration = 4.0f)
        {
            ShowNotification(message, BRNotificationPriority.Warning, duration);
        }

        public void ShowCritical(string message, float duration = 4.5f)
        {
            ShowNotification(message, BRNotificationPriority.Critical, duration);
        }

        public void ShowInfo(string message, float duration = 3.5f)
        {
            ShowNotification(message, BRNotificationPriority.Info, duration);
        }

        public void ShowNotification(string message, BRNotificationPriority priority = BRNotificationPriority.Warning, float duration = 4.0f)
        {
            if (feedNotificationPrefab == null)
            {
                ResolveDependencies();
                if (feedNotificationPrefab == null)
                {
                    Debug.LogWarning("[BRFeedNotificationManager] Feed notification prefab missing.");
                    return;
                }
            }

            if (notificationContainer == null)
            {
                SetupNotificationContainer();
                if (notificationContainer == null)
                {
                    Debug.LogWarning("[BRFeedNotificationManager] Notification container missing.");
                    return;
                }
            }

            // Always bring container to top sibling
            notificationContainer.SetAsLastSibling();

            // Play sound
            if (audioSource != null && notificationSound != null)
            {
                audioSource.PlayOneShot(notificationSound);
            }

            // If an existing notification is active, clean it up immediately
            if (currentActiveNotification != null)
            {
                if (activeDismissCoroutine != null) StopCoroutine(activeDismissCoroutine);
                Destroy(currentActiveNotification.gameObject);
                currentActiveNotification = null;
            }

            // Instantiate prefab
            GameObject notifObj = Instantiate(feedNotificationPrefab, notificationContainer, false);
            notifObj.name = "BR_ActiveNotification";

            var rt = notifObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }

            var feed = notifObj.GetComponent<FeedNotification>();
            if (feed == null) return;

            currentActiveNotification = feed;
            currentPriority = priority;

            // CRUCIAL: FeedNotification's Start() deactivates the object if defaultState == Minimized!
            // We set it to Expanded so Start() keeps it active and runs correctly.
            feed.defaultState = FeedNotification.DefaultState.Expanded;
            feed.useLocalization = false;
            feed.notificationText = message;
            feed.minimizeAfter = duration;
            feed.afterMinimize = FeedNotification.AfterMinimize.Destroy;

            // Pick icon and colors based on priority
            Color accentColor = WarningYellow;
            Color glowColor   = WarningGlow;
            Color textColor   = WarningText;
            Sprite iconSprite = warningIcon;

            switch (priority)
            {
                case BRNotificationPriority.Warning:
                    accentColor = WarningYellow;
                    glowColor   = WarningGlow;
                    textColor   = WarningText;
                    iconSprite  = warningIcon ?? infoIcon;
                    break;
                case BRNotificationPriority.Critical:
                    accentColor = CriticalRed;
                    glowColor   = CriticalGlow;
                    textColor   = CriticalText;
                    iconSprite  = criticalIcon ?? warningIcon;
                    break;
                case BRNotificationPriority.Info:
                    accentColor = InfoCyan;
                    glowColor   = InfoGlow;
                    textColor   = InfoText;
                    iconSprite  = infoIcon ?? warningIcon;
                    break;
            }

            feed.icon = iconSprite;
            feed.UpdateUI();

            // Apply colors directly to child UI elements & bypass Reach theme override
            ApplyThemedColors(notifObj, accentColor, glowColor, textColor, iconSprite);

            // Trigger animation
            feed.ExpandNotification();

            // Self-dismiss tracking
            activeDismissCoroutine = StartCoroutine(DismissRoutine(duration + 1.0f));
        }

        private void ApplyThemedColors(GameObject notifObj, Color accent, Color glow, Color textC, Sprite iconSpr)
        {
            // 1. Text
            var tmps = notifObj.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                var ut = t.GetComponent<UIManagerText>();
                if (ut != null) ut.useCustomColor = true;
                t.color = textC;
            }

            // 2. Images
            var images = notifObj.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                var uiImg = img.GetComponent<UIManagerImage>();
                if (uiImg != null) uiImg.useCustomColor = true;

                string n = img.gameObject.name;
                if (n == "Glow")
                {
                    img.color = glow;
                }
                else if (n == "Left" || n == "Right" || n == "Parent" || n == "Filled")
                {
                    img.color = accent;
                }
                else if (n == "Image") // The Icon inside Parent
                {
                    if (iconSpr != null) img.sprite = iconSpr;
                    img.color = new Color(0.098f, 0.137f, 0.176f, 1f); // Dark contrast icon on bright accent box
                }
            }
        }

        private IEnumerator DismissRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (currentActiveNotification != null)
            {
                currentActiveNotification = null;
            }
        }
    }
}
