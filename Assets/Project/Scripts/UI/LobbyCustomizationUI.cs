using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Michsky.UI.Reach;

namespace Railbound.UI
{
    /// <summary>
    /// Customization UI for Operator selection, Skills preview, and Emotes triggering in GameLobby.
    /// Uses Reach UI visual styling, interactive border glow, and tactile button animations.
    /// </summary>
    public class LobbyCustomizationUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static LobbyCustomizationUI Instance { get; private set; }

        [Header("Operator Selection")]
        [SerializeField] private Button prevOperatorButton;
        [SerializeField] private Button nextOperatorButton;
        [SerializeField] private TextMeshProUGUI operatorNameText;
        [SerializeField] private TextMeshProUGUI operatorRoleText;

        [Header("Skills / Abilities Card")]
        [SerializeField] private GameObject skillsCardPanel;
        [SerializeField] private TextMeshProUGUI activeSkillNameText;
        [SerializeField] private TextMeshProUGUI activeSkillDescText;
        [SerializeField] private TextMeshProUGUI passiveSkillNameText;
        [SerializeField] private TextMeshProUGUI passiveSkillDescText;
        [SerializeField] private TextMeshProUGUI ultimateSkillNameText;
        [SerializeField] private TextMeshProUGUI ultimateSkillDescText;
        [SerializeField] private Button toggleSkillsButton;

        [Header("Emotes")]
        [SerializeField] private Button emoteSaluteButton;
        [SerializeField] private Button emoteWaveButton;
        [SerializeField] private Button emoteFlexButton;
        [SerializeField] private Button emoteVictoryButton;

        [Header("Character Drag Area")]
        [Tooltip("RawImage or Viewport RectTransform where the 3D character is rendered.")]
        [SerializeField] private RawImage characterViewportRawImage;

        private Coroutine cardAnimCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Ensure root full-screen container never blocks clicks on underlying Reach UI panels (Play, Game Mode, Squad)
            var rootImg = GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.raycastTarget = false;
            }
        }

        private void Start()
        {
            // Bind operator navigation buttons
            if (prevOperatorButton != null)
            {
                prevOperatorButton.onClick.RemoveAllListeners();
                prevOperatorButton.onClick.AddListener(() =>
                {
                    LobbyCharacterPreviewManager.Instance?.PreviousOperator();
                    PulseOperatorHeader();
                });
                AttachTacticalButtonAnim(prevOperatorButton);
            }

            if (nextOperatorButton != null)
            {
                nextOperatorButton.onClick.RemoveAllListeners();
                nextOperatorButton.onClick.AddListener(() =>
                {
                    LobbyCharacterPreviewManager.Instance?.NextOperator();
                    PulseOperatorHeader();
                });
                AttachTacticalButtonAnim(nextOperatorButton);
            }

            // Bind emote buttons
            if (emoteSaluteButton != null)
            {
                emoteSaluteButton.onClick.RemoveAllListeners();
                emoteSaluteButton.onClick.AddListener(() => TriggerEmote(1, emoteSaluteButton));
                AttachTacticalButtonAnim(emoteSaluteButton);
            }

            if (emoteWaveButton != null)
            {
                emoteWaveButton.onClick.RemoveAllListeners();
                emoteWaveButton.onClick.AddListener(() => TriggerEmote(2, emoteWaveButton));
                AttachTacticalButtonAnim(emoteWaveButton);
            }

            if (emoteFlexButton != null)
            {
                emoteFlexButton.onClick.RemoveAllListeners();
                emoteFlexButton.onClick.AddListener(() => TriggerEmote(3, emoteFlexButton));
                AttachTacticalButtonAnim(emoteFlexButton);
            }

            if (emoteVictoryButton != null)
            {
                emoteVictoryButton.onClick.RemoveAllListeners();
                emoteVictoryButton.onClick.AddListener(() => TriggerEmote(4, emoteVictoryButton));
                AttachTacticalButtonAnim(emoteVictoryButton);
            }

            // Bind skills card toggle
            if (toggleSkillsButton != null && skillsCardPanel != null)
            {
                toggleSkillsButton.onClick.RemoveAllListeners();
                toggleSkillsButton.onClick.AddListener(ToggleSkillsCard);
                AttachTacticalButtonAnim(toggleSkillsButton);
                UpdateSkillsButtonLabel(skillsCardPanel.activeSelf);
            }

            // Forward drag events from characterViewportRawImage so center character rotation works without blocking side UI
            if (characterViewportRawImage != null)
            {
                characterViewportRawImage.raycastTarget = true;
                var trigger = characterViewportRawImage.gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = characterViewportRawImage.gameObject.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                AddDragTrigger(trigger, EventTriggerType.BeginDrag, data => OnBeginDrag((PointerEventData)data));
                AddDragTrigger(trigger, EventTriggerType.Drag, data => OnDrag((PointerEventData)data));
                AddDragTrigger(trigger, EventTriggerType.EndDrag, data => OnEndDrag((PointerEventData)data));
            }

            // Hook operator change event
            if (LobbyCharacterPreviewManager.Instance != null)
            {
                LobbyCharacterPreviewManager.Instance.OnOperatorChanged -= UpdateOperatorUI;
                LobbyCharacterPreviewManager.Instance.OnOperatorChanged += UpdateOperatorUI;
                if (LobbyCharacterPreviewManager.Instance.CurrentOperator != null)
                {
                    UpdateOperatorUI(LobbyCharacterPreviewManager.Instance.CurrentOperator);
                }
            }
        }

        private void AddDragTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        private void AttachTacticalButtonAnim(Button btn)
        {
            if (btn == null) return;
            if (btn.gameObject.GetComponent<TacticalReachButtonAnimator>() == null)
            {
                btn.gameObject.AddComponent<TacticalReachButtonAnimator>();
            }
        }

        private void ToggleSkillsCard()
        {
            if (skillsCardPanel == null) return;
            bool targetState = !skillsCardPanel.activeSelf;
            UpdateSkillsButtonLabel(targetState);

            if (cardAnimCoroutine != null) StopCoroutine(cardAnimCoroutine);
            cardAnimCoroutine = StartCoroutine(AnimateSkillsCard(targetState));
        }

        private void UpdateSkillsButtonLabel(bool isOpen)
        {
            if (toggleSkillsButton == null) return;
            var label = toggleSkillsButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = isOpen ? "[ HIDE LOADOUT ]" : "[ SKILLS & LOADOUT ]";
            }
        }

        private IEnumerator AnimateSkillsCard(bool show)
        {
            var cg = skillsCardPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = skillsCardPanel.AddComponent<CanvasGroup>();
            var rt = skillsCardPanel.GetComponent<RectTransform>();

            if (show)
            {
                skillsCardPanel.SetActive(true);
                if (LobbyCharacterPreviewManager.Instance?.CurrentOperator != null)
                {
                    UpdateOperatorUI(LobbyCharacterPreviewManager.Instance.CurrentOperator);
                }
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime * 7f;
                    float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                    cg.alpha = ease;
                    if (rt != null) rt.localScale = Vector3.Lerp(new Vector3(0.95f, 0.95f, 1f), Vector3.one, ease);
                    yield return null;
                }
                cg.alpha = 1f;
                if (rt != null) rt.localScale = Vector3.one;
            }
            else
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime * 8f;
                    float ease = Mathf.Clamp01(t);
                    cg.alpha = 1f - ease;
                    if (rt != null) rt.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.95f, 0.95f, 1f), ease);
                    yield return null;
                }
                skillsCardPanel.SetActive(false);
            }
        }

        private void PulseOperatorHeader()
        {
            if (operatorNameText != null)
            {
                StartCoroutine(PulseTransform(operatorNameText.rectTransform));
            }
        }

        private IEnumerator PulseTransform(RectTransform rt)
        {
            if (rt == null) yield break;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 6f;
                float s = 1f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.08f;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            if (LobbyCharacterPreviewManager.Instance != null)
            {
                LobbyCharacterPreviewManager.Instance.OnOperatorChanged -= UpdateOperatorUI;
            }
        }

        public void UpdateOperatorUI(OperatorDefinition op)
        {
            if (op == null) return;

            if (operatorNameText != null)
                operatorNameText.text = op.displayName;

            if (operatorRoleText != null)
                operatorRoleText.text = $"// ROLE: {op.roleCategory.ToUpper()}";

            if (activeSkillNameText != null)
                activeSkillNameText.text = $"[Q] ACTIVE // {op.activeSkillName}";

            if (activeSkillDescText != null)
                activeSkillDescText.text = op.activeSkillDesc;

            if (passiveSkillNameText != null)
                passiveSkillNameText.text = $"[P] PASSIVE // {op.passiveSkillName}";

            if (passiveSkillDescText != null)
                passiveSkillDescText.text = op.passiveSkillDesc;

            if (ultimateSkillNameText != null)
                ultimateSkillNameText.text = $"[Z] ULTIMATE // {op.ultimateSkillName}";

            if (ultimateSkillDescText != null)
                ultimateSkillDescText.text = op.ultimateSkillDesc;
        }

        private void TriggerEmote(int emoteId, Button sourceBtn = null)
        {
            if (LobbyCharacterPreviewManager.Instance != null)
            {
                LobbyCharacterPreviewManager.Instance.PlayEmote(emoteId);
            }
            if (sourceBtn != null)
            {
                var anim = sourceBtn.GetComponent<TacticalReachButtonAnimator>();
                if (anim != null) anim.FlashActivePulse();
            }
        }

        // Drag to rotate character
        public void OnBeginDrag(PointerEventData eventData)
        {
            LobbyCharacterPreviewManager.Instance?.OnBeginDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            LobbyCharacterPreviewManager.Instance?.OnDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            LobbyCharacterPreviewManager.Instance?.OnEndDrag();
        }
    }

    /// <summary>
    /// Provides Reach UI sci-fi hover, press, border glow, and scale feedback for tactical lobby buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticalReachButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rectTransform;
        private Image bgImage;
        private Image frameImage;
        private Image accentImage;
        private TextMeshProUGUI labelText;

        private bool isHovered;
        private bool isPressed;
        private float activePulseTimer;

        private static readonly Color NormalBg = new Color(0.06f, 0.11f, 0.16f, 0.90f);
        private static readonly Color HoverBg = new Color(0.00f, 0.52f, 0.72f, 0.92f);
        private static readonly Color PressBg = new Color(0.00f, 0.78f, 1.00f, 0.98f);

        private static readonly Color NormalFrame = new Color(0.00f, 0.784f, 1.00f, 0.72f);
        private static readonly Color HoverFrame = new Color(0.35f, 0.92f, 1.00f, 1.00f);

        private static readonly Color NormalText = new Color(0.88f, 0.96f, 1.00f, 1.00f);
        private static readonly Color HoverText = new Color(1.00f, 1.00f, 1.00f, 1.00f);

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            bgImage = GetComponent<Image>();
            var frameTr = transform.Find("BorderFrame");
            if (frameTr != null) frameImage = frameTr.GetComponent<Image>();
            var accentTr = transform.Find("AccentBar");
            if (accentTr != null) accentImage = accentTr.GetComponent<Image>();
            var labelTr = transform.Find("Label");
            if (labelTr != null) labelText = labelTr.GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (rectTransform == null) return;

            if (activePulseTimer > 0f)
            {
                activePulseTimer -= Time.unscaledDeltaTime;
            }

            Vector3 targetScale = isPressed
                ? new Vector3(0.95f, 0.95f, 1f)
                : (isHovered ? new Vector3(1.045f, 1.045f, 1f) : Vector3.one);

            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * 16f);

            bool highlight = isHovered || isPressed || activePulseTimer > 0f;
            if (bgImage != null)
            {
                Color targetBg = isPressed ? PressBg : (highlight ? HoverBg : NormalBg);
                bgImage.color = Color.Lerp(bgImage.color, targetBg, Time.unscaledDeltaTime * 14f);
            }

            if (frameImage != null)
            {
                Color targetFrame = highlight ? HoverFrame : NormalFrame;
                frameImage.color = Color.Lerp(frameImage.color, targetFrame, Time.unscaledDeltaTime * 14f);
            }

            if (accentImage != null)
            {
                Color targetAccent = highlight
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(0f, 0.784f, 1f, 0.9f);
                accentImage.color = Color.Lerp(accentImage.color, targetAccent, Time.unscaledDeltaTime * 14f);
            }

            if (labelText != null)
            {
                labelText.color = Color.Lerp(labelText.color, highlight ? HoverText : NormalText, Time.unscaledDeltaTime * 14f);
            }
        }

        public void FlashActivePulse()
        {
            activePulseTimer = 0.45f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
        }
    }
}
