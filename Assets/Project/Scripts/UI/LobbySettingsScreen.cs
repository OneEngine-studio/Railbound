using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The SETTINGS menu (the gear in the lobby header): AUDIO (master volume), VIDEO (display mode, resolution, quality, V-Sync, frame
    /// rate limit) and ACCOUNT (who is playing, the server, and sign out). Every change is applied at once and saved; see
    /// <see cref="GameSettings"/>. It is not one of the top navigation tabs, so none of them is highlighted.
    /// </summary>
    public class LobbySettingsScreen : LobbyMenuScreen
    {
        protected override string Title => "SETTINGS";
        protected override string Subtitle => "AUDIO, VIDEO AND ACCOUNT";
        protected override int NavIndex => -1;
        protected override string Hints => "A D  CHANGE TAB      ESC  BACK";

        /// <summary>Raised by the SIGN OUT button (the lobby takes the player back to the login screen).</summary>
        public Action SignOutRequested;

        private static readonly string[] TabNames = { "AUDIO", "VIDEO", "ACCOUNT" };

        private readonly List<Image> tabBg = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabel = new List<TextMeshProUGUI>();
        private RectTransform listRoot;
        private int tab;
        private float fade;
        private float rowY;

        protected override void BuildContent(RectTransform c)
        {
            BuildTabs(c, TabNames, -880f, 360f, 220f, SetTab, tabBg, tabLabel);
            listRoot = NewRect("List", c, new Vector2(0.5f, 0.5f), new Vector2(1180f, 700f), new Vector2(-880f + 590f, -48f));

            var side = Img("Side", c, Panel); Place(side.rectTransform, new Vector2(610f, -8f), new Vector2(540f, 784f));
            var edge = Img("Edge", side.rectTransform, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);
            LabelL(side.rectTransform, "SAVED AUTOMATICALLY", 15, true, Color.white, -240f, 330f, 480f);
            var body = LabelL(side.rectTransform, "Every change applies at once and is kept for the next time you play.", 16, false, Dim, -240f, 250f, 480f, 90f);
            body.enableWordWrapping = true; body.alignment = TextAlignmentOptions.TopLeft; body.characterSpacing = 1f;
            var rule = Img("Rule", side.rectTransform, Line); Place(rule.rectTransform, new Vector2(0f, 180f), new Vector2(480f, 2f));
            MakeButton(side.rectTransform, "RESTORE DEFAULTS", new Vector2(0f, 120f), new Vector2(480f, 56f), false, () => { GameSettings.ResetAll(); Refresh(false); });
            var note = LabelL(side.rectTransform, "Resolution and display mode change in a built game; the editor's Game view keeps its own size.", 14, false, Faint, -240f, 20f, 480f, 80f);
            note.enableWordWrapping = true; note.alignment = TextAlignmentOptions.TopLeft; note.characterSpacing = 1f;
        }

        protected override void OnOpened() { tab = 0; Refresh(true); }

        protected override void OnClosing() { GameSettings.Save(); }

        protected override void OnKeys()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q)) SetTab(tab - 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.E)) SetTab(tab + 1);
        }

        protected override void OnTick()
        {
            if (fade >= 1f) return;
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 4f);
            float e = 1f - (1f - fade) * (1f - fade);
            for (int i = 0; i < listRoot.childCount; i++)
            {
                var row = (RectTransform)listRoot.GetChild(i);
                var g = row.GetComponent<CanvasGroup>();
                float local = Mathf.Clamp01((e - i * 0.08f) / 0.6f);
                if (g != null) g.alpha = local;
                row.anchoredPosition = new Vector2(Mathf.Lerp(-30f, 0f, local), row.anchoredPosition.y);
            }
        }

        private void SetTab(int t)
        {
            t = Mathf.Clamp(t, 0, TabNames.Length - 1);
            if (t == tab) return;
            tab = t;
            Refresh(true);
        }

        private void Refresh(bool animate)
        {
            MarkTabs(tabBg, tabLabel, tab);
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            rowY = 350f - 40f;

            switch (tab)
            {
                case 0:
                    SliderRow("MASTER VOLUME", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
                    break;
                case 1:
                    var resolutions = GameSettings.Resolutions();
                    OptionRow("DISPLAY MODE", GameSettings.DisplayModes[GameSettings.DisplayMode], d => GameSettings.DisplayMode = (GameSettings.DisplayMode + d + 2) % 2);
                    OptionRow("RESOLUTION", resolutions[GameSettings.ResolutionIndex].x + " X " + resolutions[GameSettings.ResolutionIndex].y,
                        d => GameSettings.ResolutionIndex = Mathf.Clamp(GameSettings.ResolutionIndex + d, 0, resolutions.Count - 1));
                    var names = QualitySettings.names;
                    OptionRow("QUALITY", names.Length > 0 ? names[GameSettings.QualityLevel].ToUpperInvariant() : "DEFAULT",
                        d => GameSettings.QualityLevel = Mathf.Clamp(GameSettings.QualityLevel + d, 0, Mathf.Max(0, names.Length - 1)));
                    OptionRow("V-SYNC", GameSettings.VSync ? "ON" : "OFF", d => GameSettings.VSync = !GameSettings.VSync);
                    int cap = GameSettings.FpsCaps[GameSettings.FpsCapIndex];
                    OptionRow("FRAME RATE LIMIT", GameSettings.VSync ? "SET BY V-SYNC" : (cap == 0 ? "UNLIMITED" : cap + " FPS"),
                        d => { if (!GameSettings.VSync) GameSettings.FpsCapIndex = Mathf.Clamp(GameSettings.FpsCapIndex + d, 0, GameSettings.FpsCaps.Length - 1); });
                    break;
                default:
                    var auth = AuthenticationService.Instance;
                    bool signedIn = auth != null && auth.IsAuthenticated;
                    string player = signedIn && auth.Session != null && !string.IsNullOrEmpty(auth.Session.Username) ? auth.Session.Username.ToUpperInvariant() : "NOT SIGNED IN";
                    InfoRow("PLAYER", player);
                    InfoRow("STATUS", signedIn ? "ONLINE" : "OFFLINE");
                    InfoRow("SERVER", ServerConfig.NakamaHost.ToUpperInvariant());
                    InfoRow("LEVEL", PlayerProgression.Current.level + "   |   " + PlayerProgression.Current.coins.ToString("N0") + " COINS");
                    ButtonRow("SIGN OUT", "SIGN OUT", () => { if (SignOutRequested != null) SignOutRequested(); });
                    break;
            }
            fade = animate ? 0f : 1f;
            if (!animate) for (int i = 0; i < listRoot.childCount; i++) { var g = listRoot.GetChild(i).GetComponent<CanvasGroup>(); if (g != null) g.alpha = 1f; }
        }

        // ---------------- rows ----------------
        private RectTransform NewRow(string label)
        {
            var row = NewRect("Row", listRoot, new Vector2(0.5f, 0.5f), new Vector2(1180f, 76f), new Vector2(0f, rowY));
            rowY -= 76f + 8f;
            row.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var bg = Img("Bg", row, Panel); Stretch(bg.rectTransform);
            var side = Img("Side", row, new Color(1f, 1f, 1f, 0.3f));
            var sr = side.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0f, 1f); sr.pivot = new Vector2(0f, 0.5f); sr.sizeDelta = new Vector2(4f, 0f); sr.anchoredPosition = Vector2.zero;
            LabelL(row, label, 20, true, Color.white, -560f, 0f, 420f);
            return row;
        }

        private void InfoRow(string label, string value)
        {
            var row = NewRow(label);
            LabelR(row, value, 22, true, Dim, 560f, 0f, 560f);
        }

        private void OptionRow(string label, string value, Action<int> step)
        {
            var row = NewRow(label);
            const float cx = 240f;
            var left = MakeButton(row, "<", new Vector2(cx - 230f, 0f), new Vector2(56f, 50f), false, () => { step(-1); Refresh(false); }, 22f);
            var right = MakeButton(row, ">", new Vector2(cx + 230f, 0f), new Vector2(56f, 50f), false, () => { step(1); Refresh(false); }, 22f);
            left.name = "Left"; right.name = "Right";
            LabelC(row, value, 22, true, Color.white, new Vector2(cx, 0f), new Vector2(360f, 36f));
        }

        private void SliderRow(string label, float value, Action<float> set)
        {
            var row = NewRow(label);
            const float cx = 200f, w = 520f;
            var valueText = LabelR(row, Mathf.RoundToInt(value * 100f) + "%", 22, true, Color.white, 560f, 0f, 110f);

            var trackGo = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            trackGo.transform.SetParent(row, false);
            var tr = (RectTransform)trackGo.transform; Place(tr, new Vector2(cx - 40f, 0f), new Vector2(w, 36f));
            var hit = trackGo.GetComponent<Image>(); hit.sprite = px; hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;

            var bar = Img("Bar", tr, new Color(1f, 1f, 1f, 0.16f)); Place(bar.rectTransform, Vector2.zero, new Vector2(w, 8f));
            var fillArea = NewRect("Fill Area", tr, new Vector2(0.5f, 0.5f), new Vector2(w, 8f), Vector2.zero);
            var fill = Img("Fill", fillArea, Color.white); fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f); fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = NewRect("Handle Slide Area", tr, new Vector2(0.5f, 0.5f), new Vector2(w, 36f), Vector2.zero);
            var handle = Img("Handle", handleArea, Color.white); handle.rectTransform.sizeDelta = new Vector2(16f, 34f);

            var slider = trackGo.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            slider.transition = Selectable.Transition.None;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => { set(v); valueText.text = Mathf.RoundToInt(v * 100f) + "%"; });
        }

        private void ButtonRow(string label, string button, Action click)
        {
            var row = NewRow(label);
            MakeButton(row, button, new Vector2(410f, 0f), new Vector2(320f, 50f), true, () => click());
        }
    }
}
