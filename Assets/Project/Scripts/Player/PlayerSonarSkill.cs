using System.Collections;
using System.Collections.Generic;
using JUTPS;
using Railbound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Advanced Full-Map Sonar Enemy Scanning Skill with See-Through-Walls X-Ray Vision.
    /// Triggered by pressing the 'I' key on the keyboard.
    /// Emits a full-battlefield sonar shockwave, interacts with SimpleSonarShader,
    /// reveals enemy skeletons and body silhouettes through walls, and pings them on the Minimap.
    /// </summary>
    public class PlayerSonarSkill : MonoBehaviour
    {
        [Header("World Scan Skill Settings (INab FX Style)")]
        public KeyCode triggerKey = KeyCode.I;
        public float scanRadius = 140f;   // Limited tactical battlefield sector (140m)
        public float scanDuration = 3.8f; // Multi-second smooth growing wavefront
        public float waveSpeed = 40f;     // Expansion speed
        public float cooldown = 10f;
        public float revealDuration = 14f; // Enemies visible through walls for 14 seconds
        public Color scanWaveColor = new Color(2.8f, 0.72f, 0.48f, 1.0f); // Neon Coral / Sunset Glow

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip sonarLaunchSfx;
        public AudioClip enemyAcquiredSfx;

        [Header("UI Elements (Optional / Auto-Created)")]
        public TextMeshProUGUI hudStatusText;
        public Image cooldownFillImage;
        public TextMeshProUGUI cooldownText;

        private float _lastUsedTime = -100f;
        private CanvasGroup _hudStatusCanvasGroup;
        private Coroutine _hideMessageCoroutine;
        private Coroutine _scanWaveRoutine;

        public bool IsReady => Time.time >= _lastUsedTime + cooldown;
        public float CooldownRemaining => Mathf.Max(0f, (_lastUsedTime + cooldown) - Time.time);

        private void Start()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D player audio
                }
            }

            if (sonarLaunchSfx == null)
            {
                sonarLaunchSfx = GenerateSubBassPingClip();
            }

            if (enemyAcquiredSfx == null)
            {
                enemyAcquiredSfx = GenerateAcquiredChirpClip();
            }

            CreateOrFindHUD();
        }

        private void Update()
        {
            bool keyPressed = Input.GetKeyDown(triggerKey);

#if ENABLE_INPUT_SYSTEM
            if (!keyPressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                keyPressed = UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame;
            }
#endif

            if (keyPressed)
            {
                if (IsReady)
                {
                    ActivateSonar();
                }
                else
                {
                    ShowHUDMessage($"<color=#FF5555>SONAR SCAN RECHARGING ({CooldownRemaining:0.0}s)</color>", 1.5f);
                }
            }

            UpdateUI();
        }

        public void ActivateSonar()
        {
            _lastUsedTime = Time.time;
            Vector3 origin = transform.position;

            // 1. Initial Sonar Activation Sound
            if (audioSource != null && sonarLaunchSfx != null)
            {
                audioSource.PlayOneShot(sonarLaunchSfx, 0.95f);
            }

            // 2. Full-Scene Volumetric World Scan Wavefront (INab FX Style)
            SonarPulseWaveEffect.Create(origin, scanRadius, scanDuration, scanWaveColor);

            // 3. SimpleSonarShader trigger (passes world pos & range into materials)
            SimpleSonarShader_Object.TriggerSonar(origin, scanRadius);
            if (SimpleSonarShader_Parent.Instance != null)
            {
                SimpleSonarShader_Parent.Instance.StartSonarRing(origin, scanRadius);
            }

            // 4. Begin Expanding Wavefront Scan Coroutine
            if (_scanWaveRoutine != null) StopCoroutine(_scanWaveRoutine);
            _scanWaveRoutine = StartCoroutine(FullMapScanRoutine(origin));
        }

        private IEnumerator FullMapScanRoutine(Vector3 origin)
        {
            ShowHUDMessage($"<color=#FFAA77>WORLD SCAN IN PROGRESS...</color> SCANNING {scanRadius:0}M SECTOR", scanDuration);

            // Collect all enemies in the level
            GameObject[] enemyGOs = GameObject.FindGameObjectsWithTag("Enemy");
            var pendingEnemies = new List<EnemyScanEntry>();

            foreach (var go in enemyGOs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                JUHealth health = go.GetComponent<JUHealth>();
                if (health != null && health.IsDead) continue;

                float dist = Vector3.Distance(origin, go.transform.position);
                if (dist <= scanRadius)
                {
                    pendingEnemies.Add(new EnemyScanEntry
                    {
                        GameObject = go,
                        Distance = dist,
                        Delay = (dist / Mathf.Max(scanRadius, 1f)) * scanDuration
                    });
                }
            }

            // Sort enemies by distance so wavefront reveals them in order
            pendingEnemies.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            float startTime = Time.time;
            int revealedCount = 0;
            int totalCount = pendingEnemies.Count;

            for (int i = 0; i < pendingEnemies.Count; i++)
            {
                var entry = pendingEnemies[i];
                float elapsed = Time.time - startTime;
                if (elapsed < entry.Delay)
                {
                    yield return new WaitForSeconds(entry.Delay - elapsed);
                }

                if (entry.GameObject != null && entry.GameObject.activeInHierarchy)
                {
                    JUHealth health = entry.GameObject.GetComponent<JUHealth>();
                    if (health != null && health.IsDead) continue;

                    revealedCount++;

                    // A) Through-Wall X-Ray Silhouette (Visible through walls & obstacles)
                    EnemyThroughWallSilhouette.RevealEnemy(entry.GameObject, revealDuration);

                    // B) 3D Tactical Ping Marker (Visible through walls)
                    SonarEnemyPingMarker.Attach(entry.GameObject, revealDuration);

                    // C) Top-Left Minimap Radar Pulse
                    if (Map1MiniMapController.Instance != null)
                    {
                        Map1MiniMapController.Instance.PingEnemy(entry.GameObject.transform, revealDuration);
                    }

                    // Tactical Target Acquisition Chirp
                    if (audioSource != null && enemyAcquiredSfx != null)
                    {
                        audioSource.PlayOneShot(enemyAcquiredSfx, 0.65f);
                    }

                    ShowHUDMessage($"<color=#FFAA77>TARGET ACQUIRED:</color> <color=#FFFFFF>[{revealedCount}/{totalCount}] HOSTILES IDENTIFIED THROUGH WALLS</color>", 2.5f);
                }
            }

            // Final Scan Summary
            if (revealedCount > 0)
            {
                ShowHUDMessage($"<color=#FFAA77>SCAN COMPLETE:</color> <color=#FFFFFF>{revealedCount} HOSTILE(S) PINNED WITHIN {scanRadius:0}M</color>", 4.0f);
            }
            else
            {
                ShowHUDMessage($"<color=#FFAA77>SCAN COMPLETE:</color> <color=#66FF88>NO HOSTILES DETECTED WITHIN {scanRadius:0}M</color>", 3.0f);
            }

            _scanWaveRoutine = null;
        }

        private struct EnemyScanEntry
        {
            public GameObject GameObject;
            public float Distance;
            public float Delay;
        }

        private void ShowHUDMessage(string msg, float duration)
        {
            if (hudStatusText == null) return;
            hudStatusText.text = msg;
            if (_hudStatusCanvasGroup != null) _hudStatusCanvasGroup.alpha = 1f;

            if (_hideMessageCoroutine != null) StopCoroutine(_hideMessageCoroutine);
            _hideMessageCoroutine = StartCoroutine(FadeMessageRoutine(duration));
        }

        private IEnumerator FadeMessageRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            float fadeTime = 0.8f;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                if (_hudStatusCanvasGroup != null)
                {
                    _hudStatusCanvasGroup.alpha = 1f - (t / fadeTime);
                }
                yield return null;
            }
            if (_hudStatusCanvasGroup != null) _hudStatusCanvasGroup.alpha = 0f;
        }

        private void UpdateUI()
        {
            if (cooldownText != null)
            {
                if (IsReady)
                {
                    cooldownText.text = "<color=#00F0FF>[I]</color> FULL SCAN: <color=#66FF66>READY (1600M)</color>";
                    if (cooldownFillImage != null) cooldownFillImage.fillAmount = 1f;
                }
                else
                {
                    float remaining = CooldownRemaining;
                    cooldownText.text = $"<color=#00F0FF>[I]</color> RECHARGING: <color=#FFAA33>{remaining:0.0}s</color>";
                    if (cooldownFillImage != null)
                    {
                        cooldownFillImage.fillAmount = 1f - (remaining / cooldown);
                    }
                }
            }
        }

        private void CreateOrFindHUD()
        {
            if (hudStatusText != null && cooldownText != null) return;

            GameObject uiCanvas = GameObject.Find("User Interface/UI");
            if (uiCanvas == null) uiCanvas = GameObject.Find("User Interface");
            if (uiCanvas == null) return;

            Transform existingWidget = uiCanvas.transform.Find("SonarSkillWidget");
            if (existingWidget != null)
            {
                cooldownText = existingWidget.Find("Text")?.GetComponent<TextMeshProUGUI>();
                cooldownFillImage = existingWidget.Find("FillBar")?.GetComponent<Image>();
                hudStatusText = existingWidget.Find("StatusMessage/Text")?.GetComponent<TextMeshProUGUI>();
                _hudStatusCanvasGroup = existingWidget.Find("StatusMessage")?.GetComponent<CanvasGroup>();
                return;
            }

            var widgetGO = new GameObject("SonarSkillWidget", typeof(RectTransform));
            widgetGO.transform.SetParent(uiCanvas.transform, false);

            var rt = widgetGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -270f);
            rt.sizeDelta = new Vector2(240f, 38f);

            // Dark Backing
            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(widgetGO.transform, false);
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.04f, 0.06f, 0.9f);

            // Cyan Accent Top Border
            var borderGO = new GameObject("TopBorder", typeof(RectTransform));
            borderGO.transform.SetParent(widgetGO.transform, false);
            var borderRt = borderGO.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0f, 1f);
            borderRt.anchorMax = new Vector2(1f, 1f);
            borderRt.pivot = new Vector2(0f, 1f);
            borderRt.sizeDelta = new Vector2(0f, 2f);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0.0f, 0.95f, 1.0f, 0.7f);

            // Fill Bar
            var fillGO = new GameObject("FillBar", typeof(RectTransform));
            fillGO.transform.SetParent(widgetGO.transform, false);
            var fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 0f);
            fillRt.pivot = new Vector2(0f, 0f);
            fillRt.sizeDelta = new Vector2(0f, 4f);
            cooldownFillImage = fillGO.AddComponent<Image>();
            cooldownFillImage.color = new Color(0.0f, 0.95f, 1.0f, 1f);
            cooldownFillImage.type = Image.Type.Filled;
            cooldownFillImage.fillMethod = Image.FillMethod.Horizontal;
            cooldownFillImage.fillAmount = 1f;

            // Cooldown label
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(widgetGO.transform, false);
            var txtRt = txtGO.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = new Vector2(-16f, 0f);
            txtRt.anchoredPosition = new Vector2(8f, 0f);
            cooldownText = txtGO.AddComponent<TextMeshProUGUI>();
            cooldownText.fontSize = 12f;
            cooldownText.fontStyle = FontStyles.Bold;
            cooldownText.alignment = TextAlignmentOptions.MidlineLeft;
            cooldownText.text = "<color=#00F0FF>[I]</color> FULL SCAN: <color=#66FF66>READY (1600M)</color>";

            // Status message
            var msgGO = new GameObject("StatusMessage", typeof(RectTransform));
            msgGO.transform.SetParent(widgetGO.transform, false);
            var msgRt = msgGO.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0f, 0f);
            msgRt.anchorMax = new Vector2(1f, 0f);
            msgRt.pivot = new Vector2(0f, 1f);
            msgRt.anchoredPosition = new Vector2(0f, -6f);
            msgRt.sizeDelta = new Vector2(360f, 32f);
            _hudStatusCanvasGroup = msgGO.AddComponent<CanvasGroup>();
            _hudStatusCanvasGroup.alpha = 0f;

            var msgBg = msgGO.AddComponent<Image>();
            msgBg.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

            var msgTxtGO = new GameObject("Text", typeof(RectTransform));
            msgTxtGO.transform.SetParent(msgGO.transform, false);
            var msgTxtRt = msgTxtGO.GetComponent<RectTransform>();
            msgTxtRt.anchorMin = Vector2.zero;
            msgTxtRt.anchorMax = Vector2.one;
            msgTxtRt.sizeDelta = new Vector2(-12f, 0f);
            msgTxtRt.anchoredPosition = new Vector2(6f, 0f);
            hudStatusText = msgTxtGO.AddComponent<TextMeshProUGUI>();
            hudStatusText.fontSize = 12f;
            hudStatusText.fontStyle = FontStyles.Bold;
            hudStatusText.alignment = TextAlignmentOptions.MidlineLeft;
            hudStatusText.text = "";
        }

        private AudioClip GenerateSubBassPingClip()
        {
            int sampleRate = 44100;
            float duration = 0.95f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Deep tactical sonar pulse: 900 Hz dropping to 220 Hz with heavy sub-bass tail
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(940f, 240f, Mathf.Pow(progress, 0.7f));
                float envelope = Mathf.Exp(-t * 4.2f) * Mathf.Sin(Mathf.Clamp01(t * 90f) * Mathf.PI * 0.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float sub = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.4f;
                samples[i] = (wave + sub) * envelope * 0.55f;
            }

            AudioClip clip = AudioClip.Create("SubBassSonarPulse", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateAcquiredChirpClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Tactical electronic target-lock double-pip
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.1f ? 1650f : 2100f;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.28f;
            }

            AudioClip clip = AudioClip.Create("TargetAcquiredChirp", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
