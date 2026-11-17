using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The interface sounds of the whole game, in one place: hover, click, confirm, back, error, open, reward and upgrade (clips in
    /// Resources/Audio/UI). One 2D source that survives scene loads; the same sound asked for twice within a few frames plays once, so a
    /// button that has the global hook AND calls a sound from its own handler is not heard twice.
    /// </summary>
    public static class UISfx
    {
        public enum Kind { Hover, Click, Confirm, Back, Error, Open, Reward, Upgrade }

        private static readonly string[] ClipNames = { "ui_hover", "ui_click", "ui_confirm", "ui_back", "ui_error", "ui_open", "ui_reward", "ui_upgrade" };
        private static readonly float[] Volumes = { 0.32f, 0.6f, 0.7f, 0.6f, 0.65f, 0.55f, 0.8f, 0.75f };
        private static readonly float[] MinGap = { 0.05f, 0.07f, 0.07f, 0.07f, 0.15f, 0.08f, 0.05f, 0.1f };
        private static readonly AudioClip[] clips = new AudioClip[ClipNames.Length];
        private static readonly float[] lastPlayed = new float[ClipNames.Length];
        private static float lastAnyClick = -1f;
        private static AudioSource source;
        private static bool loaded;

        /// <summary>Master volume of the interface sounds (0..1).</summary>
        public static float Volume = 1f;

        public static void Play(Kind kind, AudioClip overrideClip = null)
        {
            int i = (int)kind;
            float now = Time.unscaledTime;
            if (now - lastPlayed[i] < MinGap[i] && lastPlayed[i] > 0f) return;
            // a confirm / back / error / reward replaces the plain click of the same press
            if (kind == Kind.Click && now - lastAnyClick < 0.07f) return;
            var clip = overrideClip != null ? overrideClip : Clip(kind);
            if (clip == null || !EnsureSource()) return;
            lastPlayed[i] = now;
            if (kind != Kind.Hover) lastAnyClick = now;
            source.PlayOneShot(clip, Volumes[i] * Mathf.Clamp01(Volume));
        }

        public static AudioClip Clip(Kind kind)
        {
            if (!loaded)
            {
                loaded = true;
                for (int i = 0; i < ClipNames.Length; i++) clips[i] = Resources.Load<AudioClip>("Audio/UI/" + ClipNames[i]);
            }
            return clips[(int)kind];
        }

        private static bool EnsureSource()
        {
            if (source != null) return true;
            var go = new GameObject("[UI] Sound");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f; source.ignoreListenerPause = true; source.priority = 32;
            return true;
        }
    }

    /// <summary>
    /// Hover / press feedback of one control: a sound on hover and on click (or an error sound if it is disabled), and an optional small
    /// scale animation (grows on hover, dips on press, eased, in unscaled time so it also works while the game is paused).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISelectHandler
    {
        public float hoverScale = 1f;   // 1 = no scale animation (sound only)
        public float pressScale = 0.97f;
        public UISfx.Kind clickKind = UISfx.Kind.Click;
        public bool silentClick;        // the control plays its own, more specific sound (confirm, reward...)

        private Selectable selectable;
        private Vector3 baseScale = Vector3.one;
        private bool hovered, pressed;
        private float current = 1f;

        public static UIFeedback Attach(GameObject go, float hoverScale = 1f, UISfx.Kind click = UISfx.Kind.Click)
        {
            var f = go.GetComponent<UIFeedback>();
            if (f == null) f = go.AddComponent<UIFeedback>();
            f.hoverScale = hoverScale; f.clickKind = click;
            return f;
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
        }

        private bool Interactable => selectable == null || (selectable.interactable && selectable.IsInteractable());

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (Interactable) UISfx.Play(UISfx.Kind.Hover);
        }

        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (!Interactable) { if (selectable != null) UISfx.Play(UISfx.Kind.Error); return; }
            if (!silentClick) UISfx.Play(clickKind);
        }

        public void OnSelect(BaseEventData e)
        {
            // keyboard / gamepad navigation moves the selection: same tick as a mouse hover
            if (!(e is PointerEventData) && Interactable) UISfx.Play(UISfx.Kind.Hover);
        }

        private void OnDisable() { hovered = pressed = false; current = 1f; if (hoverScale != 1f) transform.localScale = baseScale; }

        private void Update()
        {
            if (hoverScale == 1f) return;   // sound only
            float target = !Interactable ? 1f : (pressed ? pressScale : (hovered ? hoverScale : 1f));
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            if (Mathf.Abs(current - target) < 0.0005f) current = target;
            transform.localScale = baseScale * current;
        }
    }

    /// <summary>
    /// Gives every uGUI control in the game the hover and click sounds, without touching scenes or prefabs: while the cursor is visible it
    /// looks for new Selectables a few times a second and adds a sound-only <see cref="UIFeedback"/> to the ones that have none.
    /// </summary>
    public sealed class UIFeedbackInstaller : MonoBehaviour
    {
        private float next;
        private readonly HashSet<int> seen = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode) return;   // dedicated server
            var go = new GameObject("[UI] Feedback Installer");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<UIFeedbackInstaller>();
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            if (!Cursor.visible && Cursor.lockState == CursorLockMode.Locked) return;   // in gameplay: nothing to hover
            var all = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var s in all)
            {
                if (s == null || !seen.Add(s.GetInstanceID())) continue;
                if (s.GetComponent<UIFeedback>() != null) continue;
                if (s is Scrollbar || s is InputField || s is TMPro.TMP_InputField) continue;   // typing and dragging stay silent
                var f = s.gameObject.AddComponent<UIFeedback>();
                f.hoverScale = 1f;
                if (s is Toggle || s is Slider || s is Dropdown || s is TMPro.TMP_Dropdown) f.clickKind = UISfx.Kind.Click;
            }
            if (seen.Count > 20000) seen.Clear();
        }
    }
}
