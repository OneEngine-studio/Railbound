using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The BATTLE PASS menu: a season header with the tier progress and a scrollable track of tiers, each with a Free and a Premium reward.
    /// The pass is not live yet, so every reward here is a preview: nothing can be claimed and the premium button says COMING SOON.
    /// Scroll with the mouse wheel, by dragging, or with A / D.
    /// </summary>
    public class LobbyBattlePassScreen : LobbyMenuScreen
    {
        protected override string Title => "BATTLE PASS";
        protected override string Subtitle => "SEASON 01   |   PREVIEW";
        protected override int NavIndex => 3;
        protected override string Hints => "A D  SCROLL TIERS      MOUSE WHEEL OR DRAG  SCROLL      ESC  BACK";

        private const int Tiers = 40;
        private const float ColW = 168f, ViewW = 1650f, ViewH = 630f, ViewX = 105f, ViewY = -85f;

        // sample reward cycle (free, premium): kind, short mark, name, amount
        private struct Reward { public string mark, name, amount; public Reward(string m, string n, string a) { mark = m; name = n; amount = a; } }
        private static readonly Reward[] Free =
        {
            new Reward("C", "COINS", "x 500"), new Reward("XP", "XP BOOST", "+10%"), new Reward("EM", "EMBLEM", "COMMON"),
            new Reward("CC", "CALLING CARD", "COMMON"), new Reward("OP", "OPERATOR LOOK", "STANDARD"),
        };
        private static readonly Reward[] Premium =
        {
            new Reward("C", "COINS", "x 1,500"), new Reward("XP", "XP BOOST", "+25%"), new Reward("EM", "EMBLEM", "RARE"),
            new Reward("CC", "CALLING CARD", "RARE"), new Reward("OP", "OPERATOR LOOK", "ELITE"),
        };

        private ScrollRect scroll;
        private RectTransform content;
        private float keyTarget = float.NaN;
        private float keyUntil;

        protected override void BuildContent(RectTransform c)
        {
            // season strip: your tier, the XP bar, and the premium button
            var strip = Img("Strip", c, Panel); Place(strip.rectTransform, new Vector2(0f, 330f), new Vector2(1760f, 120f));
            var edge = Img("Edge", strip.rectTransform, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);
            var s = strip.rectTransform;
            LabelL(s, "YOUR TIER", 14, false, Dim, -850f, 34f, 200f);
            LabelL(s, "1", 64, true, Color.white, -850f, -14f, 120f, 80f);
            LabelL(s, "/ " + Tiers, 26, true, Dim, -770f, -22f, 120f);
            LabelL(s, "SEASON XP", 14, false, Dim, -560f, 34f, 300f);
            Bar(s, new Vector2(-560f + 280f, 0f), new Vector2(560f, 12f), 0f, Color.white);
            LabelL(s, "0 / 1,000 XP", 16, true, Dim, -560f, -30f, 300f);

            LabelR(s, "PREVIEW   |   REWARDS ARE NOT FINAL", 13, false, Dim, 850f, 38f, 520f);
            var buy = MakeButton(s, "UNLOCK PREMIUM   |   COMING SOON", new Vector2(590f, -6f), new Vector2(520f, 60f), false, () => { }, 17f);
            buy.interactable = false;
            buy.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            buy.GetComponentInChildren<TextMeshProUGUI>().color = Faint;

            // row names on the left, track on the right
            LabelL(c, "FREE", 20, true, Color.white, -870f, ViewY + 120f, 100f);
            LabelL(c, "PREMIUM", 20, true, Dim, -870f, ViewY - 120f, 120f);
            LabelL(c, "TIER", 14, false, Faint, -870f, ViewY + 270f, 100f);

            var view = NewRect("Viewport", c, new Vector2(0.5f, 0.5f), new Vector2(ViewW, ViewH), new Vector2(ViewX, ViewY));
            var hit = Img("Hit", view, new Color(0f, 0f, 0f, 0f)); Stretch(hit.rectTransform); hit.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();

            content = NewRect("Track", view, new Vector2(0f, 0.5f), new Vector2(ColW * Tiers + 16f, ViewH), Vector2.zero);
            content.pivot = new Vector2(0f, 0.5f); content.anchoredPosition = Vector2.zero;

            for (int i = 0; i < Tiers; i++) BuildTier(i);

            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = 0.08f;
            scroll.scrollSensitivity = 40f;
        }

        private void BuildTier(int i)
        {
            int tier = i + 1;
            // children of the track use centre anchors, so x is measured from the middle of the (very wide) track
            float cx = ColW * 0.5f + 8f + i * ColW - (ColW * Tiers + 16f) * 0.5f;
            bool milestone = tier % 5 == 0;
            var f = Free[i % Free.Length];
            var p = Premium[i % Premium.Length];

            // tier number chip
            var chip = Img("Tier " + tier, content, tier == 1 ? Color.white : (milestone ? new Color(1f, 1f, 1f, 0.22f) : new Color(1f, 1f, 1f, 0.1f)));
            Place(chip.rectTransform, new Vector2(cx, 270f), new Vector2(152f, 40f));
            LabelC(chip.rectTransform, tier.ToString("00"), 22, true, tier == 1 ? Color.black : Color.white, Vector2.zero, new Vector2(140f, 30f));

            BuildCard(cx, 120f, f, false, milestone, tier == 1);
            BuildCard(cx, -120f, p, true, milestone, false);

            // XP to reach the tier
            LabelC(content, "1,000 XP", 13, false, Faint, new Vector2(cx, -285f), new Vector2(152f, 20f));
        }

        private void BuildCard(float cx, float cy, Reward r, bool premium, bool milestone, bool current)
        {
            var card = Img(premium ? "Premium" : "Free", content, premium ? new Color(0.085f, 0.09f, 0.105f, 1f) : Panel);
            Place(card.rectTransform, new Vector2(cx, cy), new Vector2(152f, 200f));
            var top = Img("Top", card.rectTransform, premium ? new Color(1f, 1f, 1f, 0.28f) : (current ? Color.white : new Color(1f, 1f, 1f, milestone ? 0.7f : 0.3f)));
            var tr = top.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0f, milestone ? 5f : 3f);

            var band = Img("Band", card.rectTransform, new Color(1f, 1f, 1f, premium ? 0.07f : 0.12f)); band.sprite = gradient;
            Place(band.rectTransform, new Vector2(0f, 48f), new Vector2(146f, 90f));
            LabelC(card.rectTransform, r.mark, r.mark.Length > 1 ? 40 : 52, true, premium ? new Color(1f, 1f, 1f, 0.5f) : Color.white, new Vector2(0f, 48f), new Vector2(140f, 70f));

            var name = LabelC(card.rectTransform, r.name, 14, true, premium ? Dim : Color.white, new Vector2(0f, -20f), new Vector2(144f, 40f));
            name.enableWordWrapping = true; name.characterSpacing = 3f;
            LabelC(card.rectTransform, r.amount, 14, false, Dim, new Vector2(0f, -50f), new Vector2(144f, 22f));

            var foot = Img("Foot", card.rectTransform, premium ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.06f));
            Place(foot.rectTransform, new Vector2(0f, -82f), new Vector2(146f, 28f));
            LabelC(foot.rectTransform, premium ? "PREMIUM" : (current ? "NEXT" : "LOCKED"), 12, true, premium ? Faint : Dim, Vector2.zero, new Vector2(140f, 22f));
        }

        protected override void OnOpened()
        {
            content.anchoredPosition = Vector2.zero;
            keyTarget = float.NaN;
            scroll.velocity = Vector2.zero;
        }

        protected override void OnKeys()
        {
            float max = Mathf.Max(0f, content.rect.width - ViewW);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                float from = float.IsNaN(keyTarget) ? -content.anchoredPosition.x : keyTarget;
                keyTarget = Mathf.Clamp(from - ColW * 3f, 0f, max); keyUntil = Time.unscaledTime + 0.6f;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                float from = float.IsNaN(keyTarget) ? -content.anchoredPosition.x : keyTarget;
                keyTarget = Mathf.Clamp(from + ColW * 3f, 0f, max); keyUntil = Time.unscaledTime + 0.6f;
            }
        }

        protected override void OnTick()
        {
            if (float.IsNaN(keyTarget)) return;
            if (Time.unscaledTime > keyUntil) { keyTarget = float.NaN; return; }
            scroll.velocity = Vector2.zero;
            float x = Mathf.Lerp(content.anchoredPosition.x, -keyTarget, Time.unscaledDeltaTime * 10f);
            content.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
