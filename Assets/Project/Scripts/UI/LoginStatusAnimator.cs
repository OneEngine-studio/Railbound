using TMPro;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// Login screen status: the "login to online account" text breathes and its dots cycle while the account service signs in,
    /// with a thin line sweeping beneath. The sign-in options stay hidden unless there is no saved session to restore.
    /// </summary>
    public class LoginStatusAnimator : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string baseText = "LOGIN TO ONLINE ACCOUNT";
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform sweep;

        [Header("Sign-in options")]
        [Tooltip("Reveal the options when no saved session could be restored (otherwise nobody could sign in).")]
        [SerializeField] private bool showOptionsIfNoSession = true;
        [SerializeField] private float minAnimationTime = 2.2f;
        [SerializeField] private float fadeTime = 0.5f;
        [SerializeField] private CanvasGroup[] options;

        private float startTime;
        private int lastDots = -1;
        private bool revealed;
        private float revealT;

        private void Awake()
        {
            startTime = Time.unscaledTime;
            SetOptionsAlpha(0f);
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (!revealed)
            {
                int dots = Mathf.FloorToInt(now * 2.5f) % 4;
                if (dots != lastDots && label != null)
                {
                    lastDots = dots;
                    // hidden dots keep the text centred while it grows
                    label.text = baseText + new string('.', dots) + "<alpha=#00>" + new string('.', 3 - dots);
                }
                if (label != null) label.alpha = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(now * 3f));

                if (track != null && sweep != null)
                {
                    float phase = (now * 0.75f) % 1f;
                    float eased = phase * phase * (3f - 2f * phase);
                    float w = sweep.rect.width;
                    sweep.anchoredPosition = new Vector2(Mathf.Lerp(-w, track.rect.width, eased), 0f);
                }

                if (showOptionsIfNoSession && now - startTime >= minAnimationTime && NoSessionToRestore())
                {
                    revealed = true;
                    if (label != null) { label.text = baseText; label.alpha = 1f; }
                    if (sweep != null) sweep.gameObject.SetActive(false);
                    if (track != null) track.gameObject.SetActive(false);
                }
            }
            else if (revealT < 1f)
            {
                revealT = Mathf.Clamp01(revealT + Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeTime));
                SetOptionsAlpha(revealT, true);
            }
        }

        private static bool NoSessionToRestore()
        {
            var auth = AuthenticationService.Instance;
            return auth == null || (auth.IsAuthenticationReady && !auth.IsAuthenticated);
        }

        private void SetOptionsAlpha(float a, bool interactable = false)
        {
            if (options == null) return;
            foreach (var g in options)
            {
                if (g == null) continue;
                g.alpha = a;
                g.interactable = interactable && a > 0.99f;
                g.blocksRaycasts = interactable && a > 0.99f;
            }
        }
    }
}
