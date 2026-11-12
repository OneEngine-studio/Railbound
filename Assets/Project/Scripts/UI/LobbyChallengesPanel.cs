using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// "Daily challenges" card in the lobby's left column: a header with the time left today, three challenge rows
    /// (description, progress, XP reward) and a locked bonus row. The list is sample content until the challenge
    /// backend is connected; call <see cref="SetChallenges"/> to show real ones. Built at runtime under the HUD root.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyChallengesPanel : MonoBehaviour
    {
        public struct Challenge
        {
            public string text; public int progress, goal, xp;
            public Challenge(string text, int progress, int goal, int xp) { this.text = text; this.progress = progress; this.goal = goal; this.xp = xp; }
        }

        private static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.05f, 0.60f);
        private static readonly Color HeaderBg = new Color(0.16f, 0.17f, 0.18f, 0.82f);
        private static readonly Color RowBg = new Color(0.07f, 0.08f, 0.09f, 0.72f);
        private static readonly Color Gold = new Color(1f, 0.86f, 0.30f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.66f, 0.70f, 1f);

        private const float Width = 440f, HeaderH = 44f, RowH = 84f, BonusH = 44f, Gap = 4f;

        public static LobbyChallengesPanel Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Daily_Challenges_Panel", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(48f, -112f);
            var comp = go.AddComponent<LobbyChallengesPanel>();
            comp.font = font;
            comp.Rebuild(new[]
            {
                new Challenge("Get 1 Kill with a Throwable", 0, 1, 4500),
                new Challenge("Get 2 Headshots with Pistols", 0, 2, 4500),
                new Challenge("Get 3 Kills using Suppressed Weapons", 0, 3, 4500),
            });
            return comp;
        }

        private TMP_FontAsset font;
        private TextMeshProUGUI timeLeft;
        private float nextClock;

        public void SetChallenges(Challenge[] list) { Rebuild(list); }

        private void Rebuild(Challenge[] list)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            var root = (RectTransform)transform;

            float y = 0f;
            var header = Box("Header", root, Width, HeaderH, y, HeaderBg); y += HeaderH + Gap;
            Text("Title", header, "DAILY CHALLENGES", 20f, Color.white, TextAlignmentOptions.MidlineLeft, 14f, 0.62f);
            timeLeft = Text("TimeLeft", header, "", 18f, Muted, TextAlignmentOptions.MidlineRight, -14f, 0.62f, rightSide: true);

            foreach (var c in list)
            {
                var row = Box("Challenge", root, Width, RowH, y, RowBg); y += RowH + Gap;
                var medal = Box("Medal", row, 12f, 12f, 0f, new Color(1f, 1f, 1f, 0.45f)); // small marker, left edge
                var mr = medal.GetComponent<RectTransform>(); mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f); mr.anchoredPosition = new Vector2(22f, 0f); mr.localRotation = Quaternion.Euler(0f, 0f, 45f);

                var label = Text("Text", row, c.text, 19f, Color.white, TextAlignmentOptions.MidlineLeft, 46f, 0.66f);
                label.enableWordWrapping = true; label.fontStyle = FontStyles.Normal; label.characterSpacing = 0f; label.overflowMode = TextOverflowModes.Ellipsis;

                var prog = Text("Progress", row, c.progress.ToString(), 26f, Gold, TextAlignmentOptions.Center, 0f, 0f);
                var pr = prog.rectTransform; pr.anchorMin = new Vector2(1f, 0.5f); pr.anchorMax = new Vector2(1f, 0.5f); pr.sizeDelta = new Vector2(44f, 30f); pr.anchoredPosition = new Vector2(-98f, 10f);
                var goal = Text("Goal", row, "/" + c.goal, 16f, Muted, TextAlignmentOptions.Center, 0f, 0f);
                var gr = goal.rectTransform; gr.anchorMin = new Vector2(1f, 0.5f); gr.anchorMax = new Vector2(1f, 0.5f); gr.sizeDelta = new Vector2(44f, 22f); gr.anchoredPosition = new Vector2(-98f, -14f);

                var xp = Text("Xp", row, "XP", 15f, Color.white, TextAlignmentOptions.Center, 0f, 0f);
                var xr = xp.rectTransform; xr.anchorMin = new Vector2(1f, 0.5f); xr.anchorMax = new Vector2(1f, 0.5f); xr.sizeDelta = new Vector2(34f, 28f); xr.anchoredPosition = new Vector2(-34f, 16f);
                Frame(xr, 1.5f, new Color(1f, 1f, 1f, 0.9f));
                var reward = Text("XpValue", row, c.xp.ToString(), 17f, Color.white, TextAlignmentOptions.Center, 0f, 0f);
                var rr = reward.rectTransform; rr.anchorMin = new Vector2(1f, 0.5f); rr.anchorMax = new Vector2(1f, 0.5f); rr.sizeDelta = new Vector2(60f, 22f); rr.anchoredPosition = new Vector2(-34f, -16f);

                // thin progress line along the bottom of the row
                var track = Box("Progress_Track", row, Width - 8f, 3f, 0f, new Color(1f, 1f, 1f, 0.12f));
                var tr = track.GetComponent<RectTransform>(); tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(0f, 0f); tr.pivot = new Vector2(0f, 0f); tr.anchoredPosition = new Vector2(4f, 4f);
                float k = c.goal > 0 ? Mathf.Clamp01(c.progress / (float)c.goal) : 0f;
                var fill = Box("Fill", track, (Width - 8f) * k, 3f, 0f, Gold);
                var fr = fill.GetComponent<RectTransform>(); fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f); fr.pivot = new Vector2(0f, 0.5f); fr.anchoredPosition = Vector2.zero;
            }

            var bonus = Box("Bonus", root, Width, BonusH, y, RowBg); y += BonusH;
            Text("BonusText", bonus, "BONUS CHALLENGE", 17f, Muted, TextAlignmentOptions.MidlineLeft, 18f, 0.9f);

            root.sizeDelta = new Vector2(Width, y);
            nextClock = 0f;
        }

        private void Update()
        {
            if (timeLeft == null || Time.unscaledTime < nextClock) return;
            nextClock = Time.unscaledTime + 20f;
            var left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            timeLeft.text = $"{(int)left.TotalHours}h {left.Minutes:00}m";
        }

        // ------------------------------------------------------------------ tiny uGUI helpers
        /// <summary>A coloured box stacked from the top of its parent: width w, height h, top edge y px down.</summary>
        private static RectTransform Box(string name, Transform parent, float w, float h, float y, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(0f, -y);
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return rt;
        }

        /// <summary>A hollow rectangle drawn as four thin edges inside <paramref name="parent"/>.</summary>
        private static void Frame(RectTransform parent, float thickness, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Edge" + i, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                var r = (RectTransform)go.transform;
                bool horizontal = i < 2;
                bool first = i % 2 == 0;           // top / left
                r.anchorMin = new Vector2(horizontal ? 0f : (first ? 0f : 1f), horizontal ? (first ? 1f : 0f) : 0f);
                r.anchorMax = new Vector2(horizontal ? 1f : (first ? 0f : 1f), horizontal ? (first ? 1f : 0f) : 1f);
                r.pivot = new Vector2(horizontal ? 0.5f : (first ? 0f : 1f), horizontal ? (first ? 1f : 0f) : 0.5f);
                r.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
                r.anchoredPosition = Vector2.zero;
                var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            }
        }

        private TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, float padX, float widthFrac, bool rightSide = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (widthFrac > 0f)
            {
                // fills a horizontal band of the parent: [padX .. widthFrac] from the left, or mirrored when rightSide
                rt.anchorMin = new Vector2(rightSide ? 1f - widthFrac : 0f, 0f);
                rt.anchorMax = new Vector2(rightSide ? 1f : widthFrac, 1f);
                rt.offsetMin = new Vector2(rightSide ? 0f : padX, 0f);
                rt.offsetMax = new Vector2(rightSide ? padX : 0f, 0f);
            }
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color; t.alignment = align;
            t.characterSpacing = 2f; t.enableWordWrapping = false; t.raycastTarget = false;
            return t;
        }
    }
}
