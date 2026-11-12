using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// A small nameplate floating above each squad member's head on the 3D stage (name, level, ready state).
    /// The plate of the spotlighted member is full size and bright, the others are smaller and dimmer;
    /// clicking a plate puts that member in the spotlight. Built at runtime under the lobby HUD root, behind the other panels.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbySquadNameplates : MonoBehaviour
    {
        private static readonly Color PlateBg = new Color(0.03f, 0.04f, 0.05f, 0.66f);
        private static readonly Color Ready = new Color(0.55f, 0.93f, 0.55f, 1f);
        private static readonly Color NotReady = new Color(1f, 0.42f, 0.38f, 1f);
        private static readonly Color Dim = new Color(0.72f, 0.75f, 0.78f, 1f);

        public static LobbySquadNameplates Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Squad_Nameplates", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();
            var comp = go.AddComponent<LobbySquadNameplates>();
            comp.font = font;
            comp.Build();
            return comp;
        }

        private class Plate
        {
            public RectTransform rt; public CanvasGroup group; public Image bg, accent;
            public TextMeshProUGUI name, level, state, pointer;
            public float focus;               // 0 = in the background, 1 = in the spotlight
            public Vector2 pos; public bool placed;
        }

        private TMP_FontAsset font;
        private RectTransform area;
        private readonly Plate[] plates = new Plate[LobbyParty.MaxMembers];
        private LobbyStageDirector director;

        private void Build()
        {
            area = (RectTransform)transform;
            for (int i = 0; i < plates.Length; i++)
            {
                int index = i;
                var p = new Plate();
                p.rt = NewRect("Plate_" + i, area, new Vector2(270f, 86f));
                p.rt.pivot = new Vector2(0.5f, 0f);
                p.group = p.rt.gameObject.AddComponent<CanvasGroup>();

                p.pointer = NewText("Pointer", p.rt, "▼", 16f, Color.white, TextAlignmentOptions.Center);
                Place(p.pointer.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 0f), new Vector2(0f, 18f));

                var bgRt = NewRect("Bg", p.rt, new Vector2(270f, 46f));
                Place(bgRt, 0f, 0f, 1f, 0f, new Vector2(0f, 18f), new Vector2(0f, 64f));
                p.bg = bgRt.gameObject.AddComponent<Image>(); p.bg.color = PlateBg;
                var btn = bgRt.gameObject.AddComponent<Button>(); btn.targetGraphic = p.bg; btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => LobbyParty.Instance.Select(index));

                var accentRt = NewRect("Accent", bgRt, new Vector2(0f, 3f));
                Place(accentRt, 0f, 0f, 1f, 0f, Vector2.zero, new Vector2(0f, 3f));
                p.accent = accentRt.gameObject.AddComponent<Image>(); p.accent.raycastTarget = false;

                p.name = NewText("Name", bgRt, "", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
                Place(p.name.rectTransform, 0f, 0f, 0.68f, 1f, new Vector2(12f, 3f), new Vector2(0f, 0f));
                p.level = NewText("Level", bgRt, "", 16f, Dim, TextAlignmentOptions.MidlineRight);
                Place(p.level.rectTransform, 0.62f, 0f, 1f, 1f, new Vector2(0f, 3f), new Vector2(-12f, 0f));

                p.state = NewText("State", p.rt, "", 15f, Ready, TextAlignmentOptions.Center);
                Place(p.state.rectTransform, 0f, 0f, 1f, 0f, new Vector2(0f, 66f), new Vector2(0f, 86f));

                plates[i] = p;
            }
            var party = LobbyParty.Instance;
            party.Changed += Refresh;
            party.SelectionChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            var party = LobbyParty.InstanceIfExists;
            if (party != null) { party.Changed -= Refresh; party.SelectionChanged -= Refresh; }
        }

        private void Refresh()
        {
            var party = LobbyParty.Instance;
            for (int i = 0; i < plates.Length; i++)
            {
                var p = plates[i];
                bool has = i < party.Count;
                p.rt.gameObject.SetActive(has);
                if (!has) { p.placed = false; continue; }
                var m = party.Members[i];
                p.name.text = m.displayName;
                p.level.text = m.levelText;
                p.state.text = m.ready ? "READY" : "NOT READY";
                p.state.color = m.ready ? Ready : NotReady;
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (director == null) director = FindObjectOfType<LobbyStageDirector>();
            if (director == null) return;

            var party = LobbyParty.Instance;
            float dt = Time.unscaledDeltaTime;
            float follow = 1f - Mathf.Exp(-18f * dt);
            float focusStep = 1f - Mathf.Exp(-9f * dt);

            for (int i = 0; i < plates.Length && i < party.Count; i++)
            {
                var p = plates[i];
                bool onStage = director.TryGetHeadPoint(i, out var head);
                Vector3 sp = onStage ? cam.WorldToScreenPoint(head) : Vector3.zero;
                bool visible = onStage && sp.z > 0.1f;
                p.group.alpha = Mathf.MoveTowards(p.group.alpha, visible ? Mathf.Lerp(0.62f, 1f, p.focus) : 0f, dt * 6f);
                p.group.blocksRaycasts = visible;
                if (!visible) continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, sp, null, out var local);
                p.pos = p.placed ? Vector2.Lerp(p.pos, local, follow) : local;
                p.placed = true;
                p.rt.anchoredPosition = p.pos;   // the local point is relative to the area's centre pivot, same as the plate's anchors

                bool selected = i == party.SelectedIndex;
                p.focus = Mathf.Lerp(p.focus, selected ? 1f : 0f, focusStep);
                float scale = Mathf.Lerp(0.8f, 1f, p.focus);
                p.rt.localScale = new Vector3(scale, scale, 1f);
                p.accent.color = Color.Lerp(new Color(1f, 1f, 1f, 0.18f), Color.white, p.focus);
                p.name.color = Color.Lerp(Dim, Color.white, p.focus);
                p.pointer.color = new Color(1f, 1f, 1f, p.focus);
            }
        }

        // ------------------------------------------------------------------ tiny uGUI helpers
        private static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Anchors a rect to a fraction of its parent plus pixel offsets (min / max corner).</summary>
        private static void Place(RectTransform r, float ax0, float ay0, float ax1, float ay1, Vector2 offMin, Vector2 offMax)
        {
            r.anchorMin = new Vector2(ax0, ay0); r.anchorMax = new Vector2(ax1, ay1);
            r.offsetMin = offMin; r.offsetMax = offMax;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color; t.alignment = align;
            t.characterSpacing = 2f; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
            return t;
        }
    }
}
