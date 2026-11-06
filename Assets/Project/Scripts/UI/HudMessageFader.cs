using UnityEngine;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// On the JUTPS "MenssagesPanel": the trigger scripts only set its text and active state. This keeps the
    /// panel invisible while it has no text and fades it in/out when a message arrives, so no empty box or
    /// placeholder text ever shows.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class HudMessageFader : MonoBehaviour
    {
        [SerializeField] private Text messageText;
        [SerializeField] private float fadeSpeed = 6f;
        private CanvasGroup _group;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (messageText == null) messageText = GetComponentInChildren<Text>(true);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        void OnEnable()
        {
            if (_group != null) _group.alpha = 0f;
        }

        void Update()
        {
            bool has = messageText != null && !string.IsNullOrWhiteSpace(messageText.text);
            _group.alpha = Mathf.MoveTowards(_group.alpha, has ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        }
    }
}
