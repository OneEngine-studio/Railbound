using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Tactical 3D holographic HUD ping marker (matching INab World Scan FX screenshot style).
    /// Displays clean distance readout (e.g. "11M") with a glowing downward chevron arrow
    /// pointing directly at the discovered enemy / loot through walls.
    /// </summary>
    public class SonarEnemyPingMarker : MonoBehaviour
    {
        private Transform _target;
        private Camera _mainCam;
        private Canvas _canvas;
        private TextMeshProUGUI _distanceText;
        private TextMeshProUGUI _chevronText;
        private TextMeshProUGUI _hostileLabel;

        private float _duration = 14f;
        private float _spawnTime;
        private Material _zTestAlwaysMat;
        private static readonly int ZTestProp = Shader.PropertyToID("unity_GUIZTestMode");
        private Vector3 _baseScale = Vector3.one * 0.008f;

        public static void Attach(GameObject enemy, float duration = 14f)
        {
            if (enemy == null) return;
            var existing = enemy.GetComponentInChildren<SonarEnemyPingMarker>();
            if (existing != null)
            {
                existing.RefreshTimer(duration);
                return;
            }

            var markerGO = new GameObject("SonarPingMarker_WorldScan");
            markerGO.transform.SetParent(enemy.transform, false);
            markerGO.transform.localPosition = new Vector3(0f, 2.3f, 0f);

            var marker = markerGO.AddComponent<SonarEnemyPingMarker>();
            marker._target = enemy.transform;
            marker._duration = duration;
        }

        public void RefreshTimer(float duration)
        {
            _spawnTime = Time.time;
            _duration = duration;
        }

        private void Awake()
        {
            _mainCam = Camera.main;
            _spawnTime = Time.time;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 600; // Always on top of scene

            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 100f);
            rt.localScale = _baseScale;

            // ZTest Always material for through-wall visibility
            Shader uiShader = Shader.Find("UI/Default");
            if (uiShader != null)
            {
                _zTestAlwaysMat = new Material(uiShader);
                _zTestAlwaysMat.SetInt(ZTestProp, (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            // 1. Distance Readout: e.g. "11M" (Top, bold, white, crisp dark outline)
            var distGO = new GameObject("DistanceText");
            distGO.transform.SetParent(transform, false);
            var distRt = distGO.AddComponent<RectTransform>();
            distRt.anchoredPosition = new Vector2(0f, 26f);
            distRt.sizeDelta = new Vector2(160f, 34f);
            _distanceText = distGO.AddComponent<TextMeshProUGUI>();
            _distanceText.text = "0M";
            _distanceText.fontSize = 24f;
            _distanceText.fontStyle = FontStyles.Bold;
            _distanceText.alignment = TextAlignmentOptions.Center;
            _distanceText.color = Color.white;
            _distanceText.outlineWidth = 0.35f;
            _distanceText.outlineColor = new Color32(15, 15, 20, 255);
            if (_distanceText.fontMaterial != null)
            {
                _distanceText.fontMaterial.SetInt(ZTestProp, (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            // 2. Downward Chevron Arrow: "v" (Below distance, coral neon glow pointing down)
            var chevronGO = new GameObject("ChevronArrow");
            chevronGO.transform.SetParent(transform, false);
            var chevronRt = chevronGO.AddComponent<RectTransform>();
            chevronRt.anchoredPosition = new Vector2(0f, 2f);
            chevronRt.sizeDelta = new Vector2(100f, 26f);
            _chevronText = chevronGO.AddComponent<TextMeshProUGUI>();
            _chevronText.text = "v";
            _chevronText.fontSize = 24f;
            _chevronText.fontStyle = FontStyles.Bold;
            _chevronText.alignment = TextAlignmentOptions.Center;
            _chevronText.color = new Color(1.0f, 0.48f, 0.32f, 1.0f); // Vibrant Coral Glow
            _chevronText.outlineWidth = 0.3f;
            _chevronText.outlineColor = new Color32(20, 10, 10, 255);
            if (_chevronText.fontMaterial != null)
            {
                _chevronText.fontMaterial.SetInt(ZTestProp, (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            // 3. Optional Tactical Subtitle: "[HOSTILE]"
            var labelGO = new GameObject("HostileLabel");
            labelGO.transform.SetParent(transform, false);
            var labelRt = labelGO.AddComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(0f, -20f);
            labelRt.sizeDelta = new Vector2(160f, 18f);
            _hostileLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _hostileLabel.text = "[HOSTILE]";
            _hostileLabel.fontSize = 11f;
            _hostileLabel.fontStyle = FontStyles.Bold;
            _hostileLabel.alignment = TextAlignmentOptions.Center;
            _hostileLabel.color = new Color(1.0f, 0.3f, 0.35f, 0.85f);
            if (_hostileLabel.fontMaterial != null)
            {
                _hostileLabel.fontMaterial.SetInt(ZTestProp, (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        private void Update()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            float elapsed = Time.time - _spawnTime;
            if (elapsed > _duration)
            {
                Destroy(gameObject);
                return;
            }

            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam != null)
            {
                // Face the camera cleanly
                transform.rotation = _mainCam.transform.rotation;

                float dist = Vector3.Distance(_mainCam.transform.position, _target.position);
                if (_distanceText != null)
                {
                    // Clean capital 'M' distance readout matching the screenshot: "11M"
                    _distanceText.text = $"{Mathf.RoundToInt(dist)}M";
                }

                // Scale compensation so marker remains legible across 10m - 150m
                float scaleFactor = Mathf.Clamp(dist * 0.05f, 0.7f, 2.2f);
                transform.localScale = _baseScale * scaleFactor;
            }

            // Gentle pulsating breathing animation on the chevron
            if (_chevronText != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
                _chevronText.transform.localScale = Vector3.one * pulse;
            }
        }

        private void OnDestroy()
        {
            if (_zTestAlwaysMat != null)
            {
                Destroy(_zTestAlwaysMat);
            }
        }
    }
}
