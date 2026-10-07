using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI.Anim
{
    /// <summary>Static entry points for toasts/notifications.</summary>
    public static class Notifications
    {
        public static void Show(Canvas canvas, string title, string body, float seconds = 4f, bool important = false, bool fromBottom = false)
        {
            var presenter = NotificationPresenter.Ensure(canvas);
            if (presenter != null) presenter.Show(title, body, seconds, important, fromBottom);
        }

        /// <summary>Countdown toast: icon pulses, seconds tick down, progress bar depletes, auto-dismiss.</summary>
        public static void ShowCountdown(Canvas canvas, string title, int seconds, bool important = true)
        {
            var presenter = NotificationPresenter.Ensure(canvas);
            if (presenter != null) presenter.ShowCountdown(title, seconds, important);
        }
    }

    /// <summary>
    /// Stack of notification toasts (top-right by default):
    /// slide in, icon pop (Back Out) + pulse, progress bar, auto fade-out.
    /// </summary>
    public sealed class NotificationPresenter : MonoBehaviour
    {
        private const float ToastWidth = 440f;
        private const float ToastHeight = 96f;
        private const float ToastGap = 12f;

        private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.09f, 0.97f);
        private static readonly Color Gold = new Color(1f, 0.75f, 0.2f, 1f);
        private static readonly Color AlertRed = new Color(1f, 0.10f, 0.15f, 1f);
        private static readonly Color Muted = new Color(0.6f, 0.65f, 0.7f, 1f);

        private readonly List<Toast> _active = new List<Toast>();

        public static NotificationPresenter Ensure(Canvas canvas)
        {
            if (canvas == null) return null;
            var existing = canvas.transform.Find("Notifications");
            if (existing != null) return existing.GetComponent<NotificationPresenter>();

            var go = new GameObject("Notifications", typeof(RectTransform), typeof(NotificationPresenter));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -150f);
            rect.sizeDelta = Vector2.zero;
            go.transform.SetAsLastSibling();
            return go.GetComponent<NotificationPresenter>();
        }

        public void Show(string title, string body, float seconds, bool important = false, bool fromBottom = false)
        {
            StartCoroutine(ShowRoutine(title, body, seconds, important, fromBottom, countdown: false));
        }

        public void ShowCountdown(string title, int seconds, bool important = true)
        {
            StartCoroutine(ShowRoutine(title, $"{seconds} seconds remaining", seconds, important, false, countdown: true));
        }

        private IEnumerator ShowRoutine(
            string title, string body, float duration, bool important, bool fromBottom, bool countdown)
        {
            var toast = CreateToast(title, body, important);
            _active.Insert(0, toast);

            // re-stack: everything below the new toast shifts down
            for (var i = 1; i < _active.Count; i++)
                MoveToSlot(_active[i], i, UIAnim.NotificationSlide * 0.8f);

            // slide from top (+60 -> slot) / bottom (-60 -> slot) + fade in
            var slot = SlotPosition(0);
            var start = slot + new Vector2(0f, fromBottom ? -60f : 60f);
            toast.Rect.anchoredPosition = start;
            toast.Group.alpha = 0f;
            UITween.MoveAnchoredTo(toast.Rect, slot, UIAnim.NotificationSlide, UIAnim.HoverEase, null, toast.Token);
            UITween.FadeTo(toast.Group, 1f, UIAnim.NotificationSlide, UIAnim.HoverEase, null, toast.Token);

            // icon pop (Back Out) + pulse while visible
            if (toast.Icon != null)
            {
                toast.Icon.localScale = Vector3.zero;
                UITween.ScaleTo(toast.Icon, Vector3.one, UIAnim.Popup, UIAnim.PopEase,
                    null, toast.IconToken);
                IconAnimator.Pulse(toast.Icon, important ? 1.15f : 1.10f, 0.6f, toast.PulseToken);
            }

            // progress + optional countdown
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var remaining = Mathf.Max(0f, duration - elapsed);
                if (toast.Fill != null)
                    toast.Fill.anchorMax = new Vector2(Mathf.Clamp01(remaining / Mathf.Max(0.01f, duration)), 1f);
                if (countdown && toast.Body != null)
                    toast.Body.text = $"{Mathf.CeilToInt(remaining)} seconds remaining";
                yield return null;
            }

            Dismiss(toast);
        }

        private void Dismiss(Toast toast)
        {
            if (toast == null) return;
            _active.Remove(toast);
            for (var i = 0; i < _active.Count; i++)
                MoveToSlot(_active[i], i, UIAnim.NotificationSlide * 0.8f);

            UITween.Kill(toast.Token);
            UITween.Kill(toast.IconToken);
            UITween.Kill(toast.PulseToken);
            UITween.Kill(toast.SlideToken);
            if (toast.Group != null)
                UITween.FadeTo(toast.Group, 0f, 0.25f, UIAnim.TransitionEase, () => { if (toast.Root != null) Destroy(toast.Root); }, toast.Token);
            if (toast.Rect != null)
                UITween.MoveAnchoredTo(toast.Rect, toast.Rect.anchoredPosition + new Vector2(0f, 40f), 0.25f, UIAnim.TransitionEase, null, toast.SlideToken);
        }

        private void MoveToSlot(Toast toast, int index, float duration)
        {
            if (toast == null || toast.Rect == null) return;
            UITween.MoveAnchoredTo(toast.Rect, SlotPosition(index), duration, UIAnim.TransitionEase, null, toast.Token);
        }

        private static Vector2 SlotPosition(int index) => new Vector2(0f, -index * (ToastHeight + ToastGap));

        // ------------------------------------------------------------------ build

        private Toast CreateToast(string title, string body, bool important)
        {
            var chamfer = Resources.Load<Sprite>("UI/Onboarding_Chamfer");

            var root = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ToastWidth, ToastHeight);
            rect.anchoredPosition = SlotPosition(0);
            var panel = root.GetComponent<Image>();
            panel.sprite = chamfer;
            panel.type = chamfer != null ? Image.Type.Sliced : Image.Type.Simple;
            panel.color = PanelColor;
            panel.raycastTarget = false;
            var group = root.GetComponent<CanvasGroup>();

            // accent rail (red for alerts, gold otherwise)
            var rail = NewImage("Accent", root.transform, important ? AlertRed : Gold);
            var railRect = (RectTransform)rail.transform;
            railRect.anchorMin = new Vector2(0f, 0f);
            railRect.anchorMax = new Vector2(0f, 1f);
            railRect.offsetMin = Vector2.zero;
            railRect.offsetMax = new Vector2(4f, 0f);

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(root.transform, false);
            var titleText = titleObject.GetComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 15f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.TopLeft;
            titleText.enableWordWrapping = true;
            titleText.raycastTarget = false;
            var titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(18f, -44f);
            titleRect.offsetMax = new Vector2(-66f, -14f);

            var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObject.transform.SetParent(root.transform, false);
            var bodyText = bodyObject.GetComponent<TextMeshProUGUI>();
            bodyText.text = body;
            bodyText.fontSize = 13f;
            bodyText.color = Muted;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableWordWrapping = true;
            bodyText.raycastTarget = false;
            var bodyRect = (RectTransform)bodyObject.transform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(18f, -74f);
            bodyRect.offsetMax = new Vector2(-66f, -46f);

            // warning icon: pops in and pulses while the toast is visible
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(TextMeshProUGUI));
            icon.transform.SetParent(root.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(1f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(1f, 1f);
            iconRect.anchoredPosition = new Vector2(-14f, -14f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = chamfer;
            iconImage.type = chamfer != null ? Image.Type.Sliced : Image.Type.Simple;
            iconImage.color = important ? new Color(0.9f, 0.15f, 0.18f, 1f) : new Color(0.12f, 0.10f, 0.03f, 1f);
            iconImage.raycastTarget = false;
            var bang = icon.GetComponent<TextMeshProUGUI>();
            bang.text = important ? "!" : "i";
            bang.fontSize = 22f;
            bang.fontStyle = FontStyles.Bold;
            bang.color = important ? Color.white : Gold;
            bang.alignment = TextAlignmentOptions.Center;
            bang.raycastTarget = false;

            // progress bar
            var track = NewImage("ProgressTrack", root.transform, new Color(0.16f, 0.16f, 0.20f, 1f));
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.offsetMin = new Vector2(4f, 0f);
            trackRect.offsetMax = new Vector2(0f, 5f);
            var fill = NewImage("ProgressFill", track.transform, Gold);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.sprite = null;

            return new Toast
            {
                Root = root,
                Rect = rect,
                Group = group,
                Icon = iconRect,
                Fill = fillRect,
                Body = bodyText,
                Token = new object(),
                IconToken = new object(),
                PulseToken = new object(),
                SlideToken = new object()
            };
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private sealed class Toast
        {
            public GameObject Root;
            public RectTransform Rect;
            public CanvasGroup Group;
            public RectTransform Icon;
            public RectTransform Fill;
            public TMP_Text Body;
            public object Token;
            public object IconToken;
            public object PulseToken;
            public object SlideToken;
        }
    }
}
