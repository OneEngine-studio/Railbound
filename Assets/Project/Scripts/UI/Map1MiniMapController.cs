using System.Collections;
using System.Collections.Generic;
using Arikan;
using JUTPS;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Tactical Sci-Fi Minimap Controller for MAP-1 with Stealth Fog-of-War.
    /// Enemies are HIDDEN by default and only become visible when detected by
    /// Sonar Scan (or firing weapons), then fade back into fog of war.
    /// </summary>
    public class Map1MiniMapController : MonoBehaviour
    {
        public static Map1MiniMapController Instance { get; private set; }

        [Header("MiniMap Core")]
        public MiniMapView miniMap;
        public MiniMapBounds bounds;

        [Header("Sprites & Prefabs")]
        public Sprite playerArrowSprite;
        public Sprite enemyDotSprite;
        public Sprite enemyPingedSprite;

        [Header("Colors")]
        public Color playerColor = new Color(0.0f, 0.95f, 1.0f, 1.0f); // Sci-Fi Cyan
        public Color enemyIdleColor = new Color(1.0f, 0.25f, 0.25f, 0.8f);
        public Color enemyPingedColor = new Color(1.0f, 0.12f, 0.22f, 1.0f); // Tactical Neon Red

        private Transform _playerTransform;
        private Image _playerIconImage;
        private readonly Dictionary<Transform, EnemyMarker> _trackedEnemies = new Dictionary<Transform, EnemyMarker>();

        private class EnemyMarker
        {
            public Image Icon;
            public CanvasGroup CanvasGroup;
            public JUHealth Health;
            public float PingEndTime;
            public bool IsPinged;
        }

        private void Awake()
        {
            Instance = this;
            if (miniMap == null) miniMap = GetComponent<MiniMapView>();
        }

        private void Start()
        {
            InitPlayer();
            StartCoroutine(ScanEnemiesRoutine());
        }

        private void InitPlayer()
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _playerTransform = playerGO.transform;
                if (miniMap != null)
                {
                    _playerIconImage = miniMap.FollowCentered(_playerTransform, playerArrowSprite);
                    if (_playerIconImage != null)
                    {
                        _playerIconImage.color = playerColor;
                        _playerIconImage.rectTransform.sizeDelta = new Vector2(28f, 28f);
                    }
                }
            }
        }

        private IEnumerator ScanEnemiesRoutine()
        {
            var wait = new WaitForSeconds(1.5f);
            while (true)
            {
                RefreshEnemies();
                yield return wait;
            }
        }

        private void RefreshEnemies()
        {
            if (miniMap == null) return;

            // Find all active enemies in scene
            GameObject[] enemyGOs = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var go in enemyGOs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                Transform t = go.transform;
                if (!_trackedEnemies.ContainsKey(t))
                {
                    JUHealth health = go.GetComponent<JUHealth>();
                    if (health != null && health.IsDead) continue;

                    Image icon = miniMap.Follow(t, enemyPingedSprite != null ? enemyPingedSprite : enemyDotSprite, rotateWithTarget: false);
                    CanvasGroup cg = null;
                    if (icon != null)
                    {
                        cg = icon.gameObject.GetComponent<CanvasGroup>();
                        if (cg == null) cg = icon.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = 0f; // Hidden by default (Fog of War)
                        icon.gameObject.SetActive(false); // Inactive until sonar ping
                        icon.color = enemyPingedColor;
                        icon.rectTransform.sizeDelta = new Vector2(22f, 22f);
                    }

                    _trackedEnemies.Add(t, new EnemyMarker
                    {
                        Icon = icon,
                        CanvasGroup = cg,
                        Health = health,
                        PingEndTime = 0f,
                        IsPinged = false
                    });
                }
            }

            // Cleanup dead or destroyed enemies
            var toRemove = new List<Transform>();
            foreach (var kvp in _trackedEnemies)
            {
                if (kvp.Key == null || (kvp.Value.Health != null && kvp.Value.Health.IsDead))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var dead in toRemove)
            {
                if (_trackedEnemies.TryGetValue(dead, out var marker) && marker.Icon != null)
                {
                    miniMap.UnfollowTarget(dead);
                }
                _trackedEnemies.Remove(dead);
            }
        }

        /// <summary>
        /// Reveals an enemy on the Minimap with a pulsating neon red alert icon for the duration.
        /// When duration expires, the marker fades out and returns to fog of war.
        /// </summary>
        public void PingEnemy(Transform enemyTransform, float duration = 12f)
        {
            if (enemyTransform == null || miniMap == null) return;

            if (!_trackedEnemies.TryGetValue(enemyTransform, out var marker))
            {
                // Register if not already in dictionary
                Image icon = miniMap.Follow(enemyTransform, enemyPingedSprite != null ? enemyPingedSprite : enemyDotSprite, false);
                CanvasGroup cg = null;
                if (icon != null)
                {
                    cg = icon.gameObject.GetComponent<CanvasGroup>();
                    if (cg == null) cg = icon.gameObject.AddComponent<CanvasGroup>();
                    icon.color = enemyPingedColor;
                    icon.rectTransform.sizeDelta = new Vector2(22f, 22f);
                    icon.gameObject.SetActive(true);
                    cg.alpha = 1f;
                }

                marker = new EnemyMarker
                {
                    Icon = icon,
                    CanvasGroup = cg,
                    Health = enemyTransform.GetComponent<JUHealth>(),
                    PingEndTime = Time.time + duration,
                    IsPinged = true
                };
                _trackedEnemies[enemyTransform] = marker;
            }
            else
            {
                marker.PingEndTime = Mathf.Max(marker.PingEndTime, Time.time + duration);
                marker.IsPinged = true;
                if (marker.Icon != null)
                {
                    marker.Icon.gameObject.SetActive(true);
                    if (marker.CanvasGroup != null) marker.CanvasGroup.alpha = 1f;
                }
            }
        }

        private void Update()
        {
            float time = Time.time;
            foreach (var kvp in _trackedEnemies)
            {
                var marker = kvp.Value;
                if (marker.Icon == null) continue;

                if (marker.IsPinged)
                {
                    float remaining = marker.PingEndTime - time;
                    if (remaining > 0f)
                    {
                        // Pulsing red animation on radar
                        float pulse = 1f + 0.3f * Mathf.Sin(time * 10f);
                        marker.Icon.rectTransform.sizeDelta = new Vector2(22f * pulse, 22f * pulse);

                        // If less than 1.5s remaining, fade out smoothly
                        if (marker.CanvasGroup != null)
                        {
                            marker.CanvasGroup.alpha = remaining < 1.5f ? (remaining / 1.5f) : 1f;
                        }
                    }
                    else
                    {
                        // Expired: Hide marker back into Fog of War
                        marker.IsPinged = false;
                        if (marker.CanvasGroup != null) marker.CanvasGroup.alpha = 0f;
                        marker.Icon.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}
