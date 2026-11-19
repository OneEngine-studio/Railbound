using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Railbound.Gameplay
{
    /// <summary>
    /// What the player feels when he is shot: the camera jolts (a short shake that grows with the damage and dies away in a
    /// quarter of a second, smaller while aiming down the sights), a red arc round the crosshair points to where the shot came
    /// from, and the screen edges flash red. The server tells the owner who hit him and from where (<see cref="Report"/>, sent by
    /// BRNetHealth); offline the local player's own damage events drive it.
    /// The shake is added after every camera has been placed and taken off again before the next frame, so it never builds up
    /// and never moves the aim.
    /// </summary>
    [DefaultExecutionOrder(31500)]
    public class HitFeedback : MonoBehaviour
    {
        public static HitFeedback Instance { get; private set; }
        public static int HitsShown;   // test counter

        [Header("Shake")]
        public float MaxPitch = 2.4f, MaxYaw = 1.8f, MaxRoll = 3.4f;   // degrees at full trauma
        public float MaxOffset = 0.05f;                                 // metres at full trauma
        public float TraumaDecay = 2.6f;                                // per second
        public float AimingScale = 0.55f;

        [Header("Indicator")]
        public float IndicatorSeconds = 1.25f;
        public float IndicatorRadius = 150f;

        private float trauma;
        private Vector3 kick;   // a push away from the shot, decays with the trauma
        private Transform shakenCam;
        private Vector3 appliedPos;
        private Quaternion appliedRot = Quaternion.identity;

        private class Hit { public Vector3 From; public bool Known; public float At; public float Strength; public RectTransform Arc; public Image Img; }
        private readonly List<Hit> hits = new List<Hit>();
        private Canvas canvas;
        private Image vignette;
        private float vignetteAlpha;
        private static Sprite arcSprite, vignetteSprite;
        private JUHealth hookedOffline;

        // test aid: -hittest DIR logs every hit and saves a screenshot of the first ones, mid-shake, with the arc showing
        private static string testDir;
        private float shotAt = -1f;
        private int shotsTaken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isBatchMode || Instance != null) return;
            var go = new GameObject("[HUD] Hit Feedback");
            DontDestroyOnLoad(go);
            go.AddComponent<HitFeedback>();
        }

        private void Awake()
        {
            Instance = this;
            BuildUi();
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-hittest") { testDir = a[i + 1]; System.IO.Directory.CreateDirectory(testDir); }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>The local player was hit for <paramref name="damage"/>; <paramref name="from"/> is where the shot came from when <paramref name="known"/>.</summary>
        public static void Report(Vector3 from, bool known, float damage)
        {
            if (Instance == null || damage <= 0f) return;
            Instance.OnHit(from, known, damage);
        }

        private void OnHit(Vector3 from, bool known, float damage)
        {
            HitsShown++;
            if (testDir != null)
            {
                Debug.Log($"[HitTest] hit {HitsShown}: {damage:0.#} damage, shooter {(known ? "known at " + from : "unknown")}");
                if (shotsTaken < 4 && shotAt < 0f) shotAt = Time.unscaledTime + 0.06f;
            }
            float strength = Mathf.Clamp(damage / 40f, 0.3f, 1f);
            trauma = Mathf.Clamp01(trauma + strength * 0.75f);
            vignetteAlpha = Mathf.Max(vignetteAlpha, 0.22f + strength * 0.25f);

            var cam = Camera.main;
            if (known && cam != null)
            {
                // the camera snaps a little away from the shot
                Vector3 away = cam.transform.InverseTransformDirection((cam.transform.position - from).normalized);
                kick = new Vector3(-Mathf.Abs(away.z) * 1.2f - 0.6f, away.x * 1.6f, -away.x * 2.2f) * strength;

                // reuse an arc already pointing about the same way (several hits from one shooter: one arc)
                Hit h = null;
                foreach (var e in hits) if (Vector3.Distance(e.From, from) < 4f) { h = e; break; }
                if (h == null) { h = new Hit(); MakeArc(h); hits.Add(h); }
                h.From = from; h.Known = true; h.At = Time.unscaledTime; h.Strength = Mathf.Max(h.Strength * 0.5f, strength);
            }
            else kick = new Vector3(-0.8f, Random.Range(-0.8f, 0.8f), Random.Range(-1.5f, 1.5f)) * strength;
        }

        // ------------------------------------------------------------------ the shake
        private void Update()
        {
            // take last frame's shake off before anybody places the camera again
            if (shakenCam != null)
            {
                shakenCam.localPosition -= appliedPos;
                shakenCam.localRotation = shakenCam.localRotation * Quaternion.Inverse(appliedRot);
                appliedPos = Vector3.zero;
                appliedRot = Quaternion.identity;
                shakenCam = null;
            }
            HookOffline();
        }

        private void LateUpdate()
        {
            trauma = Mathf.Max(0f, trauma - TraumaDecay * Time.unscaledDeltaTime);
            var cam = Camera.main;
            if (cam != null && trauma > 0.001f)
            {
                var pc = JUGameManager.PlayerController;
                float scale = pc != null && pc.IsAiming ? AimingScale : 1f;
                float s = trauma * trauma * scale;
                float t = Time.unscaledTime * 26f;
                float n1 = Mathf.PerlinNoise(t, 0.1f) * 2f - 1f, n2 = Mathf.PerlinNoise(0.7f, t) * 2f - 1f, n3 = Mathf.PerlinNoise(t, 5.3f) * 2f - 1f;
                Vector3 euler = new Vector3(n1 * MaxPitch, n2 * MaxYaw, n3 * MaxRoll) * s + kick * trauma * scale;
                Vector3 offset = new Vector3(n2, n1, 0f) * MaxOffset * s;
                appliedRot = Quaternion.Euler(euler);
                appliedPos = offset;
                shakenCam = cam.transform;
                shakenCam.localRotation = shakenCam.localRotation * appliedRot;
                shakenCam.localPosition += appliedPos;
            }
            UpdateUi(cam);
            if (shotAt > 0f && Time.unscaledTime >= shotAt)
            {
                shotAt = -1f;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(testDir, $"hit_{shotsTaken++}.png"));
            }
        }

        /// <summary>Without a network session the local player's own damage drives the feedback.</summary>
        private void HookOffline()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            bool networked = nm != null && nm.IsListening;
            var pc = JUGameManager.PlayerController;
            var h = pc != null ? pc.GetComponent<JUHealth>() : null;
            if (networked || h == hookedOffline) { if (networked && hookedOffline != null) { hookedOffline.OnTakeDamage -= OfflineDamage; hookedOffline = null; } return; }
            if (hookedOffline != null) hookedOffline.OnTakeDamage -= OfflineDamage;
            hookedOffline = h;
            if (h != null) h.OnTakeDamage += OfflineDamage;
        }

        private void OfflineDamage(float damage, Vector3 at)
        {
            var h = hookedOffline;
            bool known = h != null && h.LastAttacker != null && Time.time - h.LastAttackTime < 0.1f;
            Report(known ? h.LastAttacker.transform.position + Vector3.up * 1.4f : at, known, damage);
        }

        // ------------------------------------------------------------------ the HUD part
        private void BuildUi()
        {
            var cgo = new GameObject("Hit Feedback Canvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 44;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var vgo = new GameObject("Edge flash", typeof(RectTransform), typeof(Image));
            vgo.transform.SetParent(cgo.transform, false);
            var vr = vgo.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            vignette = vgo.GetComponent<Image>();
            vignette.sprite = VignetteSprite();
            vignette.color = new Color(0.85f, 0.05f, 0.04f, 0f);
            vignette.raycastTarget = false;
        }

        private void MakeArc(Hit h)
        {
            var go = new GameObject("Hit arc", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            h.Arc = go.GetComponent<RectTransform>();
            h.Arc.sizeDelta = new Vector2(IndicatorRadius * 2f + 60f, IndicatorRadius * 2f + 60f);
            h.Img = go.GetComponent<Image>();
            h.Img.sprite = ArcSprite();
            h.Img.raycastTarget = false;
        }

        private void UpdateUi(Camera cam)
        {
            vignetteAlpha = Mathf.Max(0f, vignetteAlpha - Time.unscaledDeltaTime * 1.1f);
            if (vignette != null) vignette.color = new Color(0.85f, 0.05f, 0.04f, vignetteAlpha);

            for (int i = hits.Count - 1; i >= 0; i--)
            {
                var h = hits[i];
                float age = Time.unscaledTime - h.At;
                if (age > IndicatorSeconds || cam == null)
                {
                    if (h.Arc != null) Destroy(h.Arc.gameObject);
                    hits.RemoveAt(i);
                    continue;
                }
                // which way the shot came from, seen from above, relative to where the camera looks (0 = ahead, at the top)
                Vector3 fwd = cam.transform.forward; fwd.y = 0f;
                Vector3 to = h.From - cam.transform.position; to.y = 0f;
                float angle = fwd.sqrMagnitude > 0.001f && to.sqrMagnitude > 0.001f ? Vector3.SignedAngle(fwd, to, Vector3.up) : 0f;
                h.Arc.localRotation = Quaternion.Euler(0f, 0f, -angle);
                float fadeIn = Mathf.Clamp01(age / 0.06f), fadeOut = 1f - Mathf.Clamp01((age - IndicatorSeconds * 0.45f) / (IndicatorSeconds * 0.55f));
                float pulse = 1f + 0.12f * Mathf.Exp(-age * 9f);
                h.Arc.localScale = Vector3.one * pulse;
                h.Img.color = new Color(1f, 0.16f, 0.12f, (0.55f + 0.4f * h.Strength) * fadeIn * fadeOut);
            }
        }

        // a soft arc at the top of a square texture: rotated round the centre it points in any direction
        private Sprite ArcSprite()
        {
            if (arcSprite != null) return arcSprite;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HitArc" };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            float radius = N * 0.5f * (IndicatorRadius / (IndicatorRadius + 30f));
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);   // 0 at the top
                    float band = 1f - Mathf.Clamp01(Mathf.Abs(r - radius) / 7f);
                    float span = 1f - Mathf.Clamp01((ang - 26f) / 10f);
                    float inner = Mathf.Clamp01((r - radius + 7f) / 7f);          // sharper outside, softer inside
                    float a = band * span * Mathf.Lerp(0.55f, 1f, inner);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            arcSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return arcSprite;
        }

        private static Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int W = 256, H = 144;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HitVignette" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.85f + v * v);
                    float a = Mathf.Clamp01((d - 0.62f) / 0.55f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            vignetteSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return vignetteSprite;
        }
    }
}
