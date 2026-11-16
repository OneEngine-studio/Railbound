using System.Collections.Generic;
using JUTPS;
using Railbound.BR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// Modern-military style match feedback for the local player, built at runtime (no scene changes):
    ///
    ///  * Kill confirmation, right of the crosshair: "+75 XP" (the XP Nakama really pays per kill) with tag lines that stack:
    ///    ELIMINATED NAME, HEADSHOT KILL (the killing hit struck the head on this machine), 2ND KILL ... plus a red kill X at the crosshair.
    ///  * Event banner, upper centre: a large title, a coloured tag strip with segment bars and a subtitle. Only real match events:
    ///    battle started, safe zone collapsing (phase n of N), final showdown, top 10 / top 5 while alive, kill streaks of 3 / 5 / 7.
    ///    Banners queue; a critical one goes first.
    ///  * Sounds: kill confirm (click + low thump), brighter click for a headshot, XP tick, banner whoosh + ping (lower for critical,
    ///    higher per streak tier). Clips live in Resources/Audio/HUD so the runtime-built HUD can load them in a build.
    ///
    /// Clients only (a headless server has no screen). Fonts: Rajdhani, like the rest of the match HUD; 1920x1080 reference canvas.
    /// </summary>
    public sealed class MatchEventHUD : MonoBehaviour
    {
        public const int XpPerKill = 75;   // the per-kill part of the server's match reward (railbound_report_match)

        private static readonly Color White = new Color(1f, 1f, 1f, 0.96f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.78f);
        private static readonly Color Gold = new Color(1f, 0.835f, 0.31f, 1f);       // #FFD54F
        private static readonly Color Amber = new Color(1f, 0.816f, 0f, 1f);         // #FFD000, as the zone warnings
        private static readonly Color Red = new Color(1f, 0.36f, 0.3f, 1f);
        private static readonly Color Green = new Color(0.49f, 1f, 0.61f, 1f);
        private static readonly Color Plate = new Color(0.03f, 0.035f, 0.04f, 0.62f);

        public enum Tone { Info, Warning, Critical, Success, Streak }

        public static MatchEventHUD Instance { get; private set; }
        public static int BannersShown, KillPopupsShown;   // test counters (BRNetDebug)

        // ---- ui
        private RectTransform root;
        private TMP_FontAsset fontBold, fontSemi;
        private Sprite px;

        private RectTransform bannerRoot, bannerStrip, bannerSegments;
        private CanvasGroup bannerGroup;
        private TextMeshProUGUI bannerTitle, bannerTag, bannerSub;
        private Image bannerTagFill;
        private readonly List<Image> segments = new List<Image>();

        private RectTransform popupRoot;
        private CanvasGroup popupGroup;
        private TextMeshProUGUI popupXp;
        private readonly List<TextMeshProUGUI> popupLines = new List<TextMeshProUGUI>();
        private RectTransform killX;
        private CanvasGroup killXGroup;

        // ---- audio
        private AudioSource sfx, sfxTick;
        private AudioClip clipConfirm, clipThump, clipTick, clipWhoosh, clipPing;

        // ---- state
        private sealed class Banner { public string Title, Tag, Sub; public Tone Tone; public int Filled, Segments; public float NotBefore; }
        private readonly List<Banner> queue = new List<Banner>();
        private Banner current;
        private float bannerT = -1f;
        private const float BannerIn = 0.32f, BannerHold = 3.1f, BannerOut = 0.4f;
        // below the ALIVE / KILLS counters (they end ~180 px from the top at 1080p)
        private const float BannerY = -300f, BannerStripY = -6f, TitleY = 42f;

        private float popupBorn = -99f, popupXpShown;
        private int popupXpTotal;
        private float killXBorn = -99f;
        private const float PopupLife = 2.6f, ComboWindow = 3.2f;

        private int localKills, lastZonePhase = -1;
        private bool wasShrinking, saidTop10, saidTop5, battleAnnounced, localDead;
        private float nextZonePoll;

        private void Awake()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { enabled = false; return; }
            Instance = this;
            fontBold = FindFont("Rajdhani-Bold") ?? TMP_Settings.defaultFontAsset;
            fontSemi = FindFont("Rajdhani-SemiBold") ?? fontBold;
            px = MakePixel();
            Build();
            LoadAudio();
        }

        private void OnEnable()
        {
            BRMatchCoordinator.KillOccurred += OnKill;
            BRMatchCoordinator.LocalEliminated += OnLocalEliminated;
            BRMatchCoordinator.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            BRMatchCoordinator.KillOccurred -= OnKill;
            BRMatchCoordinator.LocalEliminated -= OnLocalEliminated;
            BRMatchCoordinator.StateChanged -= OnStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root != null) Destroy(root.gameObject);
        }

        // ================================================================= events
        private void OnKill(BRKillInfo k)
        {
            if (k.KillerIsLocal && !k.VictimIsLocal)
            {
                localKills++;
                bool head = WasHeadshot(k.Victim);
                ShowKillPopup(k.Victim, head, localKills);
                if (localKills == 3) Enqueue("KILLSTREAK", "3 KILLS", "Three operators down. Keep moving.", Tone.Streak, 3, 7);
                else if (localKills == 5) Enqueue("UNSTOPPABLE", "5 KILLS", "Five eliminations this match.", Tone.Streak, 5, 7);
                else if (localKills == 7) Enqueue("APEX PREDATOR", "7 KILLS", "Seven eliminations. Everyone left is hunting you.", Tone.Streak, 7, 7);
            }
            if (!localDead) CheckPlacementMilestones(k.AliveAfter);
        }

        private void OnLocalEliminated(BRKillInfo k) { localDead = true; queue.Clear(); }

        private void OnStateChanged(BRMatchState s)
        {
            var co = BRMatchCoordinator.Instance;
            int alive = co != null ? co.AliveCount : 0;
            if (s == BRMatchState.ActiveBattle && !battleAnnounced)
            {
                battleAnnounced = true;
                Enqueue("BATTLE STARTED", "DEPLOYED", alive + " operators in the zone. Last one standing wins.", Tone.Info, 0, 0, false, 1.6f);   // after the countdown card of the match HUD has faded
            }
            else if (s == BRMatchState.FinalShowdown && !localDead)
            {
                Enqueue("FINAL SHOWDOWN", alive + " LEFT", "The circle closes for the last time.", Tone.Critical, 0, 0, true);
            }
        }

        private void CheckPlacementMilestones(int alive)
        {
            if (!saidTop5 && alive <= 5 && alive > 1) { saidTop5 = saidTop10 = true; Enqueue("TOP 5", alive + " REMAIN", "Only five operators are still standing.", Tone.Success, 0, 0); }
            else if (!saidTop10 && alive <= 10 && alive > 5) { saidTop10 = true; Enqueue("TOP 10", alive + " REMAIN", "You are in the top ten.", Tone.Success, 0, 0); }
        }

        private void PollZone()
        {
            if (Time.unscaledTime < nextZonePoll) return;
            nextZonePoll = Time.unscaledTime + 0.4f;
            var z = BRZoneManager.Instance;
            if (z == null || z.PhaseCount <= 0 || localDead) return;
            int phase = z.CurrentPhaseIndex;
            bool shrinking = z.IsShrinking;
            if (shrinking && (!wasShrinking || phase != lastZonePhase) && phase >= 0)
            {
                int secs = Mathf.CeilToInt(z.TimeRemaining);
                Enqueue("SAFE ZONE COLLAPSING", "PHASE " + (phase + 1), "The storm closes in " + secs / 60 + ":" + (secs % 60).ToString("00") + ". Get inside the circle.",
                        phase + 1 >= z.PhaseCount ? Tone.Critical : Tone.Warning, phase + 1, z.PhaseCount);
            }
            wasShrinking = shrinking;
            lastZonePhase = phase;
        }

        // the killing hit struck the head (recorded by DamageableBodyPart on this machine within the last moment)
        // The last hit THIS player landed (bullets set the attacker; on a client only the local player's bullets deal damage locally),
        // shortly before the kill, struck the head. Matching the victim by name does not work on a client: its roster names differ
        // from the server's.
        private static bool WasHeadshot(string victimName)
        {
            var me = Railbound.Gameplay.LocalPlayer.GameObject;
            if (me == null || JUHealth.LastHitAttacker != me) return false;
            return JUHealth.LastHitAnyWasHead && Time.time - JUHealth.LastHitAnyTime < 1.5f;
        }

        // ================================================================= kill popup
        private void ShowKillPopup(string victim, bool head, int killNumber)
        {
            KillPopupsShown++;
            Debug.Log("[MatchEventHUD] kill " + killNumber + ": " + victim + (head ? " (HEADSHOT)" : ""));
            bool combo = Time.unscaledTime - popupBorn < ComboWindow;
            popupXpTotal = (combo ? popupXpTotal : 0) + XpPerKill;
            if (!combo) popupXpShown = 0f;
            popupBorn = Time.unscaledTime;
            killXBorn = Time.unscaledTime;

            var lines = new List<KeyValuePair<string, Color>>();
            if (head) lines.Add(new KeyValuePair<string, Color>("HEADSHOT KILL", Gold));
            lines.Add(new KeyValuePair<string, Color>("ELIMINATED  " + (string.IsNullOrEmpty(victim) ? "OPERATOR" : victim.ToUpperInvariant()), White));
            if (killNumber >= 2) lines.Add(new KeyValuePair<string, Color>(Ordinal(killNumber) + " KILL", Red));
            for (int i = 0; i < popupLines.Count; i++)
            {
                bool on = i < lines.Count;
                popupLines[i].gameObject.SetActive(on);
                if (on) { popupLines[i].text = lines[i].Key; popupLines[i].color = lines[i].Value; }
            }

            Play(sfx, clipConfirm, head ? 1.18f : 1f, 0.95f);
            Play(sfx, clipThump, 0.82f, 0.32f);
            Play(sfxTick, clipTick, 1.05f + Mathf.Min(killNumber, 6) * 0.03f, 0.55f);
        }

        private static string Ordinal(int n)
        {
            int t = n % 100;
            string s = (t >= 11 && t <= 13) ? "TH" : (n % 10 == 1 ? "ST" : n % 10 == 2 ? "ND" : n % 10 == 3 ? "RD" : "TH");
            return n + s;
        }

        // ================================================================= banner
        public void Enqueue(string title, string tag, string sub, Tone tone, int filled, int segmentCount, bool urgent = false, float delay = 0f)
        {
            var b = new Banner { Title = title, Tag = tag, Sub = sub, Tone = tone, Filled = filled, Segments = segmentCount, NotBefore = Time.unscaledTime + delay };
            if (urgent || tone == Tone.Critical) queue.Insert(0, b); else queue.Add(b);
            while (queue.Count > 3) queue.RemoveAt(queue.Count - 1);
        }

        private void StartBanner(Banner b)
        {
            current = b;
            bannerT = 0f;
            BannersShown++;
            Color accent = AccentOf(b.Tone);
            bannerTitle.text = b.Title;
            bannerTitle.color = b.Tone == Tone.Critical ? Red : White;
            bannerTag.text = b.Tag;
            bannerTagFill.color = accent;
            bannerSub.text = b.Sub;
            bannerTag.ForceMeshUpdate();
            float tagW = Mathf.Max(120f, bannerTag.preferredWidth + 28f);
            ((RectTransform)bannerTagFill.transform).sizeDelta = new Vector2(tagW, 30f);
            bannerSegments.anchoredPosition = new Vector2(tagW + 8f, 0f);
            int count = Mathf.Clamp(b.Segments, 0, segments.Count);
            // the tag and its segment bars are centred as one group under the title
            float stripW = tagW + (count > 0 ? 8f + count * 34f - 6f : 0f);
            bannerStrip.anchoredPosition = new Vector2(-stripW * 0.5f, BannerStripY);
            for (int i = 0; i < segments.Count; i++)
            {
                bool on = i < count;
                segments[i].gameObject.SetActive(on);
                if (on) segments[i].color = i < b.Filled ? accent : new Color(accent.r, accent.g, accent.b, 0.22f);
            }
            float pitch = b.Tone == Tone.Critical ? 0.88f : b.Tone == Tone.Streak ? 1f + Mathf.Clamp(b.Filled, 0, 7) * 0.03f : 1f;
            Play(sfx, clipWhoosh, pitch, 0.55f);
            Play(sfxTick, clipPing, pitch, 0.7f);
        }

        private static Color AccentOf(Tone t)
        {
            switch (t)
            {
                case Tone.Warning: return Amber;
                case Tone.Critical: return Red;
                case Tone.Success: return Green;
                case Tone.Streak: return Gold;
                default: return new Color(1f, 1f, 1f, 0.9f);
            }
        }

        // ================================================================= frame
        private void Update()
        {
            PollZone();

            // banner
            if (current == null && queue.Count > 0 && Time.unscaledTime >= queue[0].NotBefore) { var b = queue[0]; queue.RemoveAt(0); StartBanner(b); }
            if (current != null)
            {
                bannerT += Time.unscaledDeltaTime;
                float a, slide, strip;
                if (bannerT < BannerIn) { float k = Ease(bannerT / BannerIn); a = k; slide = (1f - k) * -36f; strip = k; }
                else if (bannerT < BannerIn + BannerHold) { a = 1f; slide = 0f; strip = 1f; }
                else { float k = Mathf.Clamp01((bannerT - BannerIn - BannerHold) / BannerOut); a = 1f - k; slide = 0f; strip = 1f; bannerRoot.anchoredPosition = new Vector2(0f, BannerY + k * 14f); }
                bannerGroup.alpha = a;
                bannerTitle.rectTransform.anchoredPosition = new Vector2(slide, TitleY);
                bannerStrip.localScale = new Vector3(Mathf.Max(0.001f, strip), 1f, 1f);
                if (bannerT >= BannerIn + BannerHold + BannerOut)
                {
                    current = null;
                    bannerGroup.alpha = 0f;
                    bannerRoot.anchoredPosition = new Vector2(0f, BannerY);
                }
            }

            // kill popup
            float age = Time.unscaledTime - popupBorn;
            if (age < PopupLife)
            {
                float pop = age < 0.16f ? Mathf.Lerp(1.28f, 1f, Ease(age / 0.16f)) : 1f;
                popupRoot.localScale = new Vector3(pop, pop, 1f);
                popupGroup.alpha = age < PopupLife - 0.45f ? 1f : Mathf.Clamp01((PopupLife - age) / 0.45f);
                popupXpShown = Mathf.MoveTowards(popupXpShown, popupXpTotal, Time.unscaledDeltaTime * 600f);   // the number counts up
                popupXp.text = "<size=42>+" + Mathf.RoundToInt(popupXpShown) + "</size><size=22>  XP</size>";
            }
            else popupGroup.alpha = 0f;

            // kill X at the crosshair
            float xAge = Time.unscaledTime - killXBorn;
            if (xAge < 0.42f)
            {
                float s = xAge < 0.08f ? Mathf.Lerp(1.6f, 1f, xAge / 0.08f) : 1f;
                killX.localScale = new Vector3(s, s, 1f);
                killXGroup.alpha = xAge < 0.25f ? 1f : 1f - (xAge - 0.25f) / 0.17f;
            }
            else killXGroup.alpha = 0f;
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        // ================================================================= build
        private void Build()
        {
            var cgo = new GameObject("[HUD] Match Events", typeof(Canvas), typeof(CanvasScaler));
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;   // just above the match HUD (40), below its end screen (205)
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cgo.transform;

            // ---- banner (upper centre, under the compass and the ALIVE / KILLS counters), centred on a soft dark band
            bannerRoot = Rect("Banner", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, BannerY), new Vector2(1200f, 170f));
            bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f; bannerGroup.blocksRaycasts = false; bannerGroup.interactable = false;
            var band = NewImage("Band", bannerRoot, new Color(0f, 0f, 0f, 0.5f));
            band.sprite = MakeBand();
            Place((RectTransform)band.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(1200f, 168f));
            bannerTitle = Text("Title", bannerRoot, "", fontBold, 54f, White, 4f, TextAlignmentOptions.Center);
            Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, TitleY), new Vector2(1100f, 62f));
            Shadow(bannerTitle);

            bannerStrip = Rect("Strip", bannerRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, BannerStripY), new Vector2(600f, 30f));
            bannerStrip.pivot = new Vector2(0f, 0.5f);
            bannerTagFill = NewImage("TagFill", bannerStrip, Amber);
            var tf = (RectTransform)bannerTagFill.transform;
            tf.anchorMin = tf.anchorMax = tf.pivot = new Vector2(0f, 0.5f); tf.anchoredPosition = Vector2.zero; tf.sizeDelta = new Vector2(160f, 30f);
            bannerTag = Text("Tag", tf, "", fontBold, 20f, new Color(0.04f, 0.04f, 0.05f, 1f), 2f, TextAlignmentOptions.Center);
            Stretch(bannerTag.rectTransform);
            bannerSegments = Rect("Segments", bannerStrip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(168f, 0f), new Vector2(240f, 30f));
            bannerSegments.pivot = new Vector2(0f, 0.5f);
            for (int i = 0; i < 7; i++)
            {
                var seg = NewImage("Seg" + i, bannerSegments, Amber);
                var sr = (RectTransform)seg.transform;
                sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
                sr.anchoredPosition = new Vector2(i * 34f, 0f);
                sr.sizeDelta = new Vector2(28f, 30f);
                segments.Add(seg);
            }
            bannerSub = Text("Sub", bannerRoot, "", fontSemi, 23f, new Color(1f, 1f, 1f, 0.9f), 1f, TextAlignmentOptions.Center);
            Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(900f, 34f));
            Shadow(bannerSub);

            // ---- kill popup (right of the crosshair)
            popupRoot = Rect("KillPopup", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(84f, 64f), new Vector2(420f, 140f));
            popupRoot.pivot = new Vector2(0f, 1f);
            popupGroup = popupRoot.gameObject.AddComponent<CanvasGroup>();
            popupGroup.alpha = 0f; popupGroup.blocksRaycasts = false; popupGroup.interactable = false;
            var backing = NewImage("Backing", popupRoot, new Color(0f, 0f, 0f, 0.58f));
            backing.sprite = MakeFade();
            Place((RectTransform)backing.transform, new Vector2(0f, 1f), new Vector2(-14f, 8f), new Vector2(430f, 128f));
            popupXp = Text("Xp", popupRoot, "", fontBold, 36f, Gold, 1f, TextAlignmentOptions.TopLeft);
            Place(popupXp.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 42f));
            Shadow(popupXp);
            for (int i = 0; i < 3; i++)
            {
                var line = Text("Line" + i, popupRoot, "", fontBold, 19f, White, 2.5f, TextAlignmentOptions.TopLeft);
                Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(2f, -46f - i * 25f), new Vector2(420f, 24f));
                Shadow(line);
                popupLines.Add(line);
            }

            // ---- kill X at the crosshair
            killX = Rect("KillX", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            killXGroup = killX.gameObject.AddComponent<CanvasGroup>();
            killXGroup.alpha = 0f; killXGroup.blocksRaycasts = false;
            for (int i = 0; i < 2; i++)
            {
                var bar = NewImage("Bar" + i, killX, Red);
                var br = (RectTransform)bar.transform;
                br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
                br.sizeDelta = new Vector2(3f, 30f);
                br.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
        }

        private void LoadAudio()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfxTick = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { sfx, sfxTick }) { s.playOnAwake = false; s.spatialBlend = 0f; s.ignoreListenerPause = false; }
            clipConfirm = Resources.Load<AudioClip>("Audio/HUD/kill_confirm");
            clipThump = Resources.Load<AudioClip>("Audio/HUD/kill_thump");
            clipTick = Resources.Load<AudioClip>("Audio/HUD/xp_tick");
            clipWhoosh = Resources.Load<AudioClip>("Audio/HUD/banner_whoosh");
            clipPing = Resources.Load<AudioClip>("Audio/HUD/banner_ping");
            if (clipConfirm == null || clipWhoosh == null) Debug.LogWarning("[MatchEventHUD] HUD sounds missing from Resources/Audio/HUD.");
        }

        private static void Play(AudioSource src, AudioClip clip, float pitch, float volume)
        {
            if (src == null || clip == null) return;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        // ---- helpers
        private static RectTransform Rect(string name, Transform parent, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = amin; r.anchorMax = amax; r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private static void Place(RectTransform r, Vector2 anchorPivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchorPivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        private Image NewImage(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>();
            i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, TMP_FontAsset font, float size, Color c, float spacing, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.color = c; t.characterSpacing = spacing; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.richText = true;
            return t;
        }

        // a soft dark underlay so white text reads over a bright sky (instanced material, only for these texts)
        private static void Shadow(TextMeshProUGUI t)
        {
            var m = t.fontMaterial;
            if (m == null || !m.HasProperty(ShaderUtilities.ID_UnderlayColor)) return;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
        }

        private static TMP_FontAsset FindFont(string namePart)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name.Contains(namePart)) return f;
            return null;
        }

        // horizontal band: transparent at both ends, solid in the middle (behind the centred banner)
        private static Sprite MakeBand()
        {
            var tex = new Texture2D(128, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[128 * 8];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 127f, edge = Mathf.Min(u, 1f - u) / 0.32f;
                    float v = y / 7f, vert = Mathf.Min(v, 1f - v) / 0.5f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge)) * Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(vert)) * 255f);
                    cols[y * 128 + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 128, 8), new Vector2(0.5f, 0.5f), 100f);
        }

        // left to right fade: solid on the left, transparent on the right (behind the kill popup)
        private static Sprite MakeFade()
        {
            var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[64 * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 64; x++)
                    cols[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.SmoothStep(1f, 0f, x / 63f) * 255f));
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
