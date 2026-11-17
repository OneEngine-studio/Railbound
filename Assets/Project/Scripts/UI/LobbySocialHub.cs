using System;
using System.Collections.Generic;
using Railbound.Social;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The lobby's social layer, on its own overlay canvas above the lobby HUD (built at runtime, 1920x1080 reference):
    ///   - a strip under the header: LEADERBOARDS and INBOX (with the unread count), plus footer key chips (L, N, ENTER) cloned from F1 SOCIAL
    ///   - the chat dock (bottom right): GLOBAL / LFG / HELP rooms, history, system lines, @mentions, report, the server's refusals inline
    ///   - the inbox drawer (from the right): typed notification cards, gift CLAIM, filters, dismiss / clear
    ///   - live toasts for notifications that arrive while the lobby is open
    /// It hides itself while a full-screen menu, the customizer or a panel owns the lobby (the HUD tells it through <c>lobbyIdle</c>).
    /// </summary>
    public sealed class LobbySocialHub : MonoBehaviour
    {
        // ---- palette (same as the lobby menus) ----
        private static readonly Color Panel = new Color(0.045f, 0.05f, 0.06f, 0.94f);
        private static readonly Color PanelHi = new Color(0.12f, 0.13f, 0.15f, 1f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.32f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color Bad = new Color(1f, 0.36f, 0.33f, 1f);
        private static readonly Color Good = new Color(0.45f, 0.95f, 0.6f, 1f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color Cyan = new Color(0.45f, 0.85f, 1f, 1f);
        private static readonly Color Mention = new Color(1f, 0.78f, 0.25f, 0.16f);

        private TMP_FontAsset font;
        private Sprite px, gradient;
        private RectTransform root, toastRoot;
        private CanvasGroup rootGroup;
        private Action openLeaderboards, openBattlePass;
        private Func<bool> lobbyIdle;
        private SocialService svc;

        public static LobbySocialHub Create(Transform hudRoot, TMP_FontAsset font, Action openLeaderboards, Action openBattlePass, Func<bool> lobbyIdle)
        {
            var go = new GameObject("[Lobby] Social", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 56;   // above the lobby HUD (50), under the full-screen menus (60)
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var hub = go.AddComponent<LobbySocialHub>();
            hub.rootGroup = go.AddComponent<CanvasGroup>();
            hub.font = font != null ? font : TMP_Settings.defaultFontAsset;
            hub.openLeaderboards = openLeaderboards; hub.openBattlePass = openBattlePass; hub.lobbyIdle = lobbyIdle;
            hub.root = (RectTransform)go.transform;
            hub.Build(hudRoot);
            return hub;
        }

        // ================================================================= keyboard ownership
        private int lastTypingFrame = -10, inboxClosedFrame = -10;
        public bool Typing => chatInput != null && chatInput.isFocused;
        /// <summary>True while the player types in the chat or the inbox is open (and on the frame either ended), so lobby hotkeys stay quiet.</summary>
        public bool OwnsKeyboard => Typing || Time.frameCount - lastTypingFrame <= 1 || inboxOpen || Time.frameCount - inboxClosedFrame <= 1;

        // ================================================================= build
        private void Build(Transform hudRoot)
        {
            px = MakePixel(); gradient = MakeGradient();
            svc = SocialService.Instance;
            BuildStrip();
            BuildChat();
            BuildInbox();
            BuildToasts();
            BuildFooterChips(hudRoot);
            svc.InboxChanged += OnInboxChanged;
            svc.LiveNotification += OnLive;
            svc.ChatChanged += OnChatChanged;
            svc.ChatStateChanged += OnChatState;
        }

        private void OnDestroy()
        {
            if (svc == null) return;
            svc.InboxChanged -= OnInboxChanged;
            svc.LiveNotification -= OnLive;
            svc.ChatChanged -= OnChatChanged;
            svc.ChatStateChanged -= OnChatState;
            if (toastRoot != null) Destroy(toastRoot.root.gameObject);
        }

        private bool started;
        private float nextStartTry;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Typing) lastTypingFrame = Time.frameCount;

            // connect once the player is signed in (the lobby can appear a moment before the session is ready)
            if (!started && Time.unscaledTime >= nextStartTry)
            {
                nextStartTry = Time.unscaledTime + 1f;
                if (svc.Online) { started = true; svc.StartChat(); _ = svc.RefreshInboxAsync(); }
                OnChatState();
            }

            bool idle = lobbyIdle == null || lobbyIdle();
            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, idle ? 1f : 0f, dt * 6f);
            rootGroup.blocksRaycasts = idle && rootGroup.alpha > 0.5f;
            rootGroup.interactable = idle;
            if (!idle && Typing) chatInput.DeactivateInputField();
            if (!idle && inboxOpen) SetInbox(false);

            TickChat(dt);
            TickInbox(dt);
            TickToasts(dt);
            TickStrip(dt);
        }

        // ================================================================= strip under the header
        private TextMeshProUGUI badgeText;
        private Image badgeBg;
        private RectTransform badge;
        private float badgePulse;

        private void BuildStrip()
        {
            var strip = NewRect("Strip", root, new Vector2(1f, 1f), new Vector2(360f, 28f), new Vector2(-36f, -84f));
            strip.pivot = new Vector2(1f, 1f);
            stripGroup = strip.gameObject.AddComponent<CanvasGroup>();
            var inbox = StripButton(strip, "INBOX", "UI/Lobby/Icon_Mail", new Vector2(-70f, -14f), 140f, () => ToggleInbox());
            var lb = StripButton(strip, "LEADERBOARDS", "UI/Lobby/Icon_TabLeaderboard", new Vector2(-236f, -14f), 182f, () => { UISfx.Play(UISfx.Kind.Open); openLeaderboards?.Invoke(); });
            badgeBg = Img("Badge", inbox, Bad);
            badge = badgeBg.rectTransform;
            Place(badge, new Vector2(56f, 9f), new Vector2(24f, 18f));
            badgeText = Txt("Count", badge, "0", 12, Color.white, TextAlignmentOptions.Center);
            badgeText.fontStyle = FontStyles.Bold; Stretch(badgeText.rectTransform);
            badge.gameObject.SetActive(false);
        }

        private RectTransform StripButton(RectTransform parent, string label, string icon, Vector2 pos, float w, Action click)
        {
            var bg = Img(label, parent, new Color(0f, 0f, 0f, 0.45f));
            var r = bg.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = new Vector2(w, 28f);
            bg.raycastTarget = true;
            var line = Img("Line", r, new Color(1f, 1f, 1f, 0.25f));
            var lr = line.rectTransform; lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f); lr.sizeDelta = new Vector2(0f, 1.5f); lr.anchoredPosition = Vector2.zero;
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => click());
            UIFeedback.Attach(bg.gameObject, 1.03f).silentClick = true;
            Icon(r, icon, new Vector2(-w * 0.5f + 18f, 0f), new Vector2(18f, 18f), Color.white);
            var t = Txt("Label", r, label, 14, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Left);
            t.fontStyle = FontStyles.Bold; t.characterSpacing = 4f;
            Place(t.rectTransform, new Vector2(14f, 0f), new Vector2(w - 44f, 24f));
            OnHover(bg.gameObject, on => { if (bg != null) { bg.color = on ? new Color(1f, 1f, 1f, 0.14f) : new Color(0f, 0f, 0f, 0.45f); line.color = on ? Color.white : new Color(1f, 1f, 1f, 0.25f); } });
            return r;
        }

        private CanvasGroup stripGroup;
        private int shownUnread = -1;
        private void TickStrip(float dt)
        {
            stripGroup.alpha = Mathf.MoveTowards(stripGroup.alpha, inboxOpen ? 0f : 1f, dt * 8f);
            stripGroup.blocksRaycasts = !inboxOpen;
            int n = svc.UnreadCount;
            if (n != shownUnread)
            {
                if (n > shownUnread && shownUnread >= 0) badgePulse = 1f;
                shownUnread = n;
                badge.gameObject.SetActive(n > 0);
                badgeText.text = n > 99 ? "99+" : n.ToString();
                badge.sizeDelta = new Vector2(n > 9 ? 32f : 22f, 18f);
            }
            if (badgePulse > 0f)
            {
                badgePulse = Mathf.MoveTowards(badgePulse, 0f, dt * 2.5f);
                badge.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(badgePulse * Mathf.PI));
            }
        }

        // ================================================================= footer key chips (cloned from F1 SOCIAL so they look the same)
        private readonly List<(KeyCode key, Action act)> chipActions = new List<(KeyCode, Action)>();

        private void BuildFooterChips(Transform hudRoot)
        {
            var src = hudRoot != null ? hudRoot.Find("Bottom_Footer/Keybinds/KB_Social") as RectTransform : null;
            var deploy = hudRoot != null ? hudRoot.Find("Bottom_Footer/Keybinds/KB_Deploy") as RectTransform : null;
            if (src == null) return;
            float x = deploy != null ? deploy.anchoredPosition.x + deploy.sizeDelta.x : src.anchoredPosition.x + 580f;
            x = AddChip(src, "L", "LEADERBOARDS", x, () => { UISfx.Play(UISfx.Kind.Open); openLeaderboards?.Invoke(); });
            x = AddChip(src, "N", "INBOX", x, () => ToggleInbox());
            AddChip(src, "ENTER", "CHAT", x, () => FocusChat());
        }

        private float AddChip(RectTransform src, string key, string action, float x, Action click)
        {
            var chip = (RectTransform)Instantiate(src.gameObject, src.parent).transform;
            chip.name = "KB_" + action;
            TextMeshProUGUI actionText = null, keyText = null;
            foreach (var t in chip.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.transform.name == "Action") actionText = t; else if (keyText == null) keyText = t;
            }
            if (actionText != null) actionText.text = action;
            float grow = 0f;
            if (keyText != null)
            {
                keyText.text = key;
                var box = keyText.transform.parent as RectTransform;
                if (box != null && box != chip)
                {
                    float want = keyText.GetPreferredValues(key).x + 14f;
                    grow = Mathf.Max(0f, want - box.sizeDelta.x);
                    box.sizeDelta = new Vector2(box.sizeDelta.x + grow, box.sizeDelta.y);
                    box.anchoredPosition += new Vector2(grow * box.pivot.x, 0f);
                }
            }
            if (actionText != null) actionText.rectTransform.anchoredPosition += new Vector2(grow, 0f);
            float width = actionText != null ? actionText.rectTransform.anchoredPosition.x + actionText.GetPreferredValues(action).x + 34f : src.sizeDelta.x;
            chip.anchoredPosition = new Vector2(x, src.anchoredPosition.y);
            chip.sizeDelta = new Vector2(Mathf.Max(width, 90f), src.sizeDelta.y);
            var hit = chip.GetComponent<Image>();
            if (hit == null) { hit = chip.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); }
            hit.raycastTarget = true;
            var btn = chip.GetComponent<Button>();
            if (btn == null) btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit; btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => click());
            return x + chip.sizeDelta.x;
        }

        // ================================================================= chat dock
        private const float ChatW = 470f, ChatCollapsedH = 230f, ChatExpandedH = 470f;
        private RectTransform chatRoot, chatContent, chatView, newPill;
        private CanvasGroup chatGroup;
        private ScrollRect chatScroll;
        private TMP_InputField chatInput;
        private TextMeshProUGUI chatCounter, chatError, chatStatus, chatPlaceholder;
        private Image chatStatusDot, chatAccent;
        private readonly Image[] roomTabLine = new Image[3];
        private readonly TextMeshProUGUI[] roomTabLabel = new TextMeshProUGUI[3];
        private readonly Image[] roomTabDot = new Image[3];
        private int room;
        private float chatErrorUntil, chatIdleFor, chatH = ChatCollapsedH;
        private bool chatHovered;
        private readonly List<RectTransform> chatRows = new List<RectTransform>();
        private bool sending;

        private void BuildChat()
        {
            chatRoot = NewRect("Chat", root, new Vector2(1f, 0f), new Vector2(ChatW, ChatCollapsedH), new Vector2(-36f, 54f));
            chatRoot.pivot = new Vector2(1f, 0f);
            chatGroup = chatRoot.gameObject.AddComponent<CanvasGroup>();
            var bg = Img("Bg", chatRoot, Panel); Stretch(bg.rectTransform); bg.raycastTarget = true;
            OnHover(bg.gameObject, on => chatHovered = on);
            chatAccent = Img("Accent", chatRoot, new Color(1f, 1f, 1f, 0.4f));
            var ar = chatAccent.rectTransform; ar.anchorMin = new Vector2(0f, 1f); ar.anchorMax = new Vector2(1f, 1f); ar.pivot = new Vector2(0.5f, 1f); ar.sizeDelta = new Vector2(0f, 2f); ar.anchoredPosition = Vector2.zero;

            // header: rooms and the connection state
            string[] names = { "GLOBAL", "LFG", "HELP" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var tab = Img("Room " + names[i], chatRoot, new Color(0f, 0f, 0f, 0f));
                var tr = tab.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 1f);
                tr.anchoredPosition = new Vector2(12f + i * 92f, -4f); tr.sizeDelta = new Vector2(86f, 34f);
                tab.raycastTarget = true;
                var b = tab.gameObject.AddComponent<Button>(); b.targetGraphic = tab; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetRoom(index));
                UIFeedback.Attach(tab.gameObject, 1f, UISfx.Kind.Click);
                roomTabLabel[i] = Txt("Label", tr, names[i], 14, Dim, TextAlignmentOptions.Center);
                roomTabLabel[i].fontStyle = FontStyles.Bold; roomTabLabel[i].characterSpacing = 4f; Stretch(roomTabLabel[i].rectTransform);
                roomTabLine[i] = Img("Line", tr, Color.white);
                var lr = roomTabLine[i].rectTransform; lr.anchorMin = new Vector2(0.15f, 0f); lr.anchorMax = new Vector2(0.85f, 0f); lr.pivot = new Vector2(0.5f, 0f); lr.sizeDelta = new Vector2(0f, 2f); lr.anchoredPosition = Vector2.zero;
                roomTabDot[i] = Img("Unread", tr, Gold);
                Place(roomTabDot[i].rectTransform, new Vector2(38f, 8f), new Vector2(6f, 6f));
                roomTabDot[i].rectTransform.anchorMin = roomTabDot[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }
            chatStatusDot = Img("StatusDot", chatRoot, Faint);
            var sd = chatStatusDot.rectTransform; sd.anchorMin = sd.anchorMax = new Vector2(1f, 1f); sd.pivot = new Vector2(0.5f, 0.5f); sd.anchoredPosition = new Vector2(-150f, -21f); sd.sizeDelta = new Vector2(7f, 7f);
            chatStatus = Txt("Status", chatRoot, "", 11, Faint, TextAlignmentOptions.Left);
            chatStatus.fontStyle = FontStyles.Bold; chatStatus.characterSpacing = 3f;
            var st = chatStatus.rectTransform; st.anchorMin = st.anchorMax = new Vector2(1f, 1f); st.pivot = new Vector2(0f, 0.5f); st.anchoredPosition = new Vector2(-140f, -21f); st.sizeDelta = new Vector2(132f, 20f);
            var hl = Img("HeadLine", chatRoot, Line);
            var hlr = hl.rectTransform; hlr.anchorMin = new Vector2(0f, 1f); hlr.anchorMax = new Vector2(1f, 1f); hlr.pivot = new Vector2(0.5f, 1f); hlr.sizeDelta = new Vector2(-20f, 1f); hlr.anchoredPosition = new Vector2(0f, -40f);

            // the messages
            chatView = NewRect("Messages", chatRoot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            chatView.anchorMin = new Vector2(0f, 0f); chatView.anchorMax = new Vector2(1f, 1f); chatView.offsetMin = new Vector2(4f, 74f); chatView.offsetMax = new Vector2(-4f, -42f);
            var hit = Img("Hit", chatView, new Color(0f, 0f, 0f, 0f)); Stretch(hit.rectTransform); hit.raycastTarget = true;
            OnHover(hit.gameObject, on => chatHovered = on || chatHovered);
            chatView.gameObject.AddComponent<RectMask2D>();
            chatContent = NewRect("Content", chatView, new Vector2(0.5f, 1f), new Vector2(0f, 0f), Vector2.zero);
            chatContent.anchorMin = new Vector2(0f, 1f); chatContent.anchorMax = new Vector2(1f, 1f); chatContent.pivot = new Vector2(0.5f, 1f); chatContent.sizeDelta = new Vector2(0f, 0f);
            var vlg = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6); vlg.spacing = 1f; vlg.childControlHeight = true; vlg.childControlWidth = true; vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            chatContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            chatScroll = chatView.gameObject.AddComponent<ScrollRect>();
            chatScroll.viewport = chatView; chatScroll.content = chatContent; chatScroll.horizontal = false; chatScroll.vertical = true;
            chatScroll.movementType = ScrollRect.MovementType.Clamped; chatScroll.scrollSensitivity = 24f;

            // "new messages" pill when the player scrolled up
            var pill = Img("NewMessages", chatRoot, Color.white);
            newPill = pill.rectTransform; newPill.anchorMin = newPill.anchorMax = new Vector2(0.5f, 0f); newPill.pivot = new Vector2(0.5f, 0f);
            newPill.anchoredPosition = new Vector2(0f, 80f); newPill.sizeDelta = new Vector2(150f, 22f);
            pill.raycastTarget = true;
            var pb = pill.gameObject.AddComponent<Button>(); pb.targetGraphic = pill; pb.transition = Selectable.Transition.None;
            pb.onClick.AddListener(() => { chatScroll.verticalNormalizedPosition = 0f; newPill.gameObject.SetActive(false); });
            var pt = Txt("Label", newPill, "NEW MESSAGES", 11, Color.black, TextAlignmentOptions.Center); pt.fontStyle = FontStyles.Bold; pt.characterSpacing = 3f; Stretch(pt.rectTransform);
            newPill.gameObject.SetActive(false);

            // the error line and the input
            chatError = Txt("Error", chatRoot, "", 12, Bad, TextAlignmentOptions.Left);
            chatError.fontStyle = FontStyles.Bold; chatError.characterSpacing = 2f;
            var er = chatError.rectTransform; er.anchorMin = new Vector2(0f, 0f); er.anchorMax = new Vector2(1f, 0f); er.pivot = new Vector2(0.5f, 0f); er.anchoredPosition = new Vector2(0f, 54f); er.sizeDelta = new Vector2(-24f, 18f);
            BuildChatInput();
            SetRoom(0, false);
        }

        private void BuildChatInput()
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            go.transform.SetParent(chatRoot, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(-34f, 10f); r.sizeDelta = new Vector2(-88f, 40f);
            var img = go.GetComponent<Image>(); img.sprite = px; img.color = new Color(1f, 1f, 1f, 0.07f);
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(r, false);
            var ar = (RectTransform)area.transform; ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = new Vector2(12f, 4f); ar.offsetMax = new Vector2(-54f, -4f);
            chatPlaceholder = Txt("Placeholder", ar, "PRESS ENTER TO CHAT", 14, Faint, TextAlignmentOptions.MidlineLeft);
            chatPlaceholder.fontStyle = FontStyles.Bold; chatPlaceholder.characterSpacing = 3f; Stretch(chatPlaceholder.rectTransform);
            var text = Txt("Text", ar, "", 16, Color.white, TextAlignmentOptions.MidlineLeft); Stretch(text.rectTransform);
            text.richText = false;
            chatInput = go.AddComponent<TMP_InputField>();
            chatInput.textViewport = ar; chatInput.textComponent = text; chatInput.placeholder = chatPlaceholder;
            chatInput.fontAsset = font; chatInput.pointSize = 16f; chatInput.characterLimit = SocialService.ChatMaxLength;
            chatInput.lineType = TMP_InputField.LineType.SingleLine; chatInput.richText = false;
            chatInput.caretColor = Color.white; chatInput.customCaretColor = true; chatInput.caretWidth = 2; chatInput.selectionColor = new Color(1f, 1f, 1f, 0.25f);
            chatInput.targetGraphic = img;
            chatInput.onSubmit.AddListener(OnChatSubmit);
            chatInput.onValueChanged.AddListener(_ => UpdateCounter());
            chatInput.onSelect.AddListener(_ => { chatPlaceholder.text = "TYPE A MESSAGE   @NAME TO PING"; svc.MarkRead(SocialService.Rooms[room]); });
            chatInput.onDeselect.AddListener(_ => chatPlaceholder.text = "PRESS ENTER TO CHAT");
            chatCounter = Txt("Counter", r, "", 11, Faint, TextAlignmentOptions.Right);
            var cr = chatCounter.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f); cr.pivot = new Vector2(1f, 0.5f); cr.anchoredPosition = new Vector2(-8f, 0f); cr.sizeDelta = new Vector2(52f, 20f);
            go.SetActive(true);

            var send = Img("Send", chatRoot, Color.white);
            var sr = send.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(1f, 0f); sr.anchoredPosition = new Vector2(-12f, 10f); sr.sizeDelta = new Vector2(64f, 40f);
            send.raycastTarget = true;
            var sb = send.gameObject.AddComponent<Button>(); sb.targetGraphic = send;
            var c = sb.colors; c.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); c.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f); sb.colors = c;
            sb.onClick.AddListener(() => OnChatSubmit(chatInput.text));
            UIFeedback.Attach(send.gameObject, 1.04f).silentClick = true;
            var st = Txt("Label", sr, "SEND", 13, Color.black, TextAlignmentOptions.Center); st.fontStyle = FontStyles.Bold; st.characterSpacing = 3f; Stretch(st.rectTransform);
            UpdateCounter();
        }

        private void UpdateCounter()
        {
            int n = chatInput.text.Length;
            chatCounter.text = n > 0 ? n + "/" + SocialService.ChatMaxLength : "";
            chatCounter.color = n >= SocialService.ChatMaxLength - 20 ? Gold : Faint;
        }

        /// <summary>Puts the cursor in the chat (Enter in the lobby).</summary>
        public void FocusChat()
        {
            if (chatInput == null) return;
            chatIdleFor = 0f;
            EventSystem.current?.SetSelectedGameObject(chatInput.gameObject);
            chatInput.ActivateInputField();
        }

        private void SetRoom(int r, bool sound = true)
        {
            r = Mathf.Clamp(r, 0, 2);
            if (sound && r != room) UISfx.Play(UISfx.Kind.Click);
            room = r;
            for (int i = 0; i < 3; i++)
            {
                roomTabLabel[i].color = i == room ? Color.white : Dim;
                roomTabLine[i].gameObject.SetActive(i == room);
            }
            svc.MarkRead(SocialService.Rooms[room]);
            RebuildChat();
        }

        private async void OnChatSubmit(string text)
        {
            if (sending) return;
            text = (text ?? "").Trim();
            if (text.Length == 0) { chatInput.DeactivateInputField(); EventSystem.current?.SetSelectedGameObject(null); return; }
            sending = true;
            string error = await svc.SendChatAsync(SocialService.Rooms[room], text);
            sending = false;
            if (this == null) return;
            if (error == null)
            {
                chatInput.text = "";
                UISfx.Play(UISfx.Kind.Click);
                chatScroll.verticalNormalizedPosition = 0f;
            }
            else if (error.Length > 0) ShowChatError(error);
            chatInput.ActivateInputField();   // keep typing
        }

        private void ShowChatError(string error)
        {
            chatError.text = error;
            chatErrorUntil = Time.unscaledTime + 3.5f;
            UISfx.Play(UISfx.Kind.Error);
        }

        private void OnChatChanged(string changedRoom)
        {
            string current = SocialService.Rooms[room];
            if (changedRoom == current)
            {
                bool atBottom = chatScroll.verticalNormalizedPosition <= 0.02f || chatContent.rect.height <= chatView.rect.height;
                var lines = svc.Lines(current);
                // append just the new ones when possible
                if (lines.Count == chatRows.Count + 1 && chatRows.Count > 0) chatRows.Add(BuildChatRow(lines[lines.Count - 1]));
                else RebuildChat();
                if (atBottom) StickToBottom(); else newPill.gameObject.SetActive(true);
                if (Typing || chatHovered) svc.MarkRead(current);
                var last = lines.Count > 0 ? lines[lines.Count - 1] : null;
                if (last != null && last.mentionsMe) { UISfx.Play(UISfx.Kind.Confirm); chatIdleFor = 0f; }
            }
            RefreshRoomDots();
        }

        private int stickFrames;
        private void StickToBottom() { stickFrames = 2; }

        private void RebuildChat()
        {
            for (int i = chatContent.childCount - 1; i >= 0; i--) Destroy(chatContent.GetChild(i).gameObject);
            chatRows.Clear();
            foreach (var l in svc.Lines(SocialService.Rooms[room])) chatRows.Add(BuildChatRow(l));
            if (chatRows.Count == 0)
            {
                var empty = Txt("Empty", chatContent, svc.Online ? (svc.ChatJoining ? "JOINING THE ROOM..." : "NO MESSAGES YET. SAY HELLO!") : "SIGN IN TO CHAT", 13, Faint, TextAlignmentOptions.Center);
                empty.fontStyle = FontStyles.Bold; empty.characterSpacing = 3f;
                empty.gameObject.AddComponent<LayoutElement>().minHeight = 60f;
            }
            StickToBottom();
            newPill.gameObject.SetActive(false);
            RefreshRoomDots();
        }

        private RectTransform BuildChatRow(ChatLine l)
        {
            var row = new GameObject("Line", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            row.transform.SetParent(chatContent, false);
            var bg = row.GetComponent<Image>(); bg.sprite = px; bg.raycastTarget = true;
            bg.color = l.mentionsMe ? Mention : l.system && l.kind == "victory" ? new Color(Gold.r, Gold.g, Gold.b, 0.07f) : l.system ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.07f) : new Color(0f, 0f, 0f, 0f);
            var v = row.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(8, 64, 3, 3); v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
            var t = Txt("Text", (RectTransform)row.transform, Format(l), 15, Color.white, TextAlignmentOptions.TopLeft);
            t.enableWordWrapping = true; t.richText = true;
            if (l.mentionsMe)
            {
                var bar = Img("Bar", (RectTransform)row.transform, Gold);
                bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var br = bar.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 0.5f); br.sizeDelta = new Vector2(3f, 0f); br.anchoredPosition = Vector2.zero;
            }
            // REPORT on hover (not for your own lines or the server's)
            if (!l.isMe && !l.system && !string.IsNullOrEmpty(l.id))
            {
                var rep = Img("Report", (RectTransform)row.transform, new Color(1f, 1f, 1f, 0.1f));
                rep.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var rr = rep.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(1f, 1f); rr.anchoredPosition = new Vector2(-6f, -3f); rr.sizeDelta = new Vector2(58f, 18f);
                rep.raycastTarget = true;
                var rb = rep.gameObject.AddComponent<Button>(); rb.targetGraphic = rep; rb.transition = Selectable.Transition.None;
                var rt = Txt("Label", rr, l.reported ? "REPORTED" : "REPORT", 10, l.reported ? Faint : Bad, TextAlignmentOptions.Center);
                rt.fontStyle = FontStyles.Bold; rt.characterSpacing = 2f; Stretch(rt.rectTransform);
                rb.onClick.AddListener(() => Report(l, rt));
                UIFeedback.Attach(rep.gameObject, 1.05f).silentClick = true;
                rep.gameObject.SetActive(l.reported);
                OnHover(row, on => { if (rep != null) rep.gameObject.SetActive(on || l.reported); });
            }
            return (RectTransform)row.transform;
        }

        private string Format(ChatLine l)
        {
            string time = "<color=#FFFFFF55>" + l.time.ToLocalTime().ToString("HH:mm") + "</color>  ";
            string text = Escape(l.text);
            if (l.system)
            {
                string tag = l.kind == "victory" ? "<color=#FFC740>VICTORY</color>" : "<color=#73D9FF>RAILBOUND</color>";
                return time + "<b>" + tag + "</b>  <color=#FFFFFFDD>" + text + "</color>";
            }
            string me = svc.MyUsername;
            if (me.Length > 0) text = ReplaceIgnoreCase(text, "@" + Escape(me), "<b><color=#FFC740>@" + Escape(me) + "</color></b>");
            string name = "<b><color=" + (l.isMe ? "#FFFFFF" : NameColor(l.senderId)) + ">" + Escape(l.username) + "</color></b>";
            return time + name + "  <color=#FFFFFFE0>" + text + "</color>" + (l.filtered ? "  <color=#FFFFFF44><size=11>FILTERED</size></color>" : "");
        }

        private static string ReplaceIgnoreCase(string s, string find, string with)
        {
            int at = s.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            return at < 0 ? s : s.Substring(0, at) + with + s.Substring(at + find.Length);
        }

        private static string Escape(string s) { return (s ?? "").Replace("<", "‹").Replace(">", "›"); }

        // a stable, readable colour per player
        private static readonly string[] NameColors = { "#9FD3FF", "#B7F5A8", "#FFB38A", "#D9B8FF", "#FFE08A", "#8AF0E0", "#FF9EC4", "#C8D0DA" };
        private static string NameColor(string id)
        {
            int h = 0; foreach (char ch in id ?? "") h = h * 31 + ch;
            return NameColors[(h & 0x7fffffff) % NameColors.Length];
        }

        private async void Report(ChatLine l, TextMeshProUGUI label)
        {
            if (l.reported) return;
            label.text = "...";
            string status = await svc.ReportAsync(l, "abuse");
            if (label == null) return;
            switch (status)
            {
                case "ok": case "already_reported": label.text = "REPORTED"; label.color = Faint; ShowChatInfo("THANKS - THE MESSAGE WAS REPORTED"); break;
                case "not_found": label.text = "REPORT"; ShowChatError("THAT MESSAGE IS TOO OLD TO REPORT"); break;
                default: label.text = "REPORT"; ShowChatError("COULD NOT REPORT IT - TRY AGAIN"); break;
            }
        }

        private void ShowChatInfo(string text)
        {
            chatError.text = "<color=#73F29A>" + text + "</color>";
            chatErrorUntil = Time.unscaledTime + 3f;
            UISfx.Play(UISfx.Kind.Confirm);
        }

        private void RefreshRoomDots()
        {
            for (int i = 0; i < 3; i++) roomTabDot[i].gameObject.SetActive(i != room && svc.Unread(SocialService.Rooms[i]) > 0);
        }

        private void OnChatState()
        {
            if (chatStatus == null) return;
            if (!svc.Online) { chatStatus.text = "OFFLINE"; chatStatusDot.color = Bad; }
            else if (svc.Muted) { chatStatus.text = "MUTED  " + svc.MutedUntil.ToLocalTime().ToString("HH:mm"); chatStatusDot.color = Bad; }
            else if (svc.ChatConnected) { chatStatus.text = "ONLINE"; chatStatusDot.color = Good; }
            else { chatStatus.text = "CONNECTING"; chatStatusDot.color = Gold; }
            if (chatRows.Count == 0) RebuildChat();
        }

        private float nextStatus;
        private void TickChat(float dt)
        {
            if (stickFrames > 0 && --stickFrames == 0) { Canvas.ForceUpdateCanvases(); chatScroll.verticalNormalizedPosition = 0f; }
            if (chatScroll.verticalNormalizedPosition <= 0.02f) newPill.gameObject.SetActive(false);
            if (chatErrorUntil > 0f && Time.unscaledTime > chatErrorUntil) { chatError.text = ""; chatErrorUntil = 0f; }
            if (Time.unscaledTime > nextStatus) { nextStatus = Time.unscaledTime + 1f; OnChatState(); }

            // expanded while used, dimmed and short while idle
            bool active = Typing || chatHovered || chatErrorUntil > 0f;
            chatIdleFor = active ? 0f : chatIdleFor + dt;
            bool expand = Typing || chatHovered;
            float targetH = expand ? ChatExpandedH : ChatCollapsedH;
            chatH = Mathf.Lerp(chatH, targetH, 1f - Mathf.Exp(-dt * 12f));
            chatRoot.sizeDelta = new Vector2(ChatW, chatH);
            float targetA = chatIdleFor > 6f ? 0.62f : 1f;
            chatGroup.alpha = Mathf.MoveTowards(chatGroup.alpha, targetA, dt * 2.5f);
            chatAccent.color = new Color(1f, 1f, 1f, Typing ? 0.9f : 0.35f);
            chatHovered = chatHovered && RectTransformUtility.RectangleContainsScreenPoint(chatRoot, Input.mousePosition, null);
        }

        // ================================================================= inbox drawer
        private const float InboxW = 560f;
        private RectTransform inboxPanel, inboxContent, inboxStateBox;
        private Image inboxBackdrop;
        private ScrollRect inboxScroll;
        private TextMeshProUGUI inboxCount, inboxState;
        private RectTransform inboxSpinner;
        private readonly Image[] filterBg = new Image[4];
        private readonly TextMeshProUGUI[] filterLabel = new TextMeshProUGUI[4];
        private static readonly string[] FilterNames = { "ALL", "REWARDS", "MATCHES", "SOCIAL" };
        private int filter;
        private bool inboxOpen;
        private float inboxT;

        private void BuildInbox()
        {
            inboxBackdrop = Img("InboxBackdrop", root, new Color(0f, 0f, 0f, 0f));
            Stretch(inboxBackdrop.rectTransform); inboxBackdrop.raycastTarget = true;
            var bb = inboxBackdrop.gameObject.AddComponent<Button>(); bb.transition = Selectable.Transition.None; bb.onClick.AddListener(() => SetInbox(false));
            inboxBackdrop.gameObject.SetActive(false);

            var panel = Img("Inbox", root, new Color(0.035f, 0.04f, 0.05f, 0.985f));
            inboxPanel = panel.rectTransform;
            inboxPanel.anchorMin = new Vector2(1f, 0f); inboxPanel.anchorMax = new Vector2(1f, 1f); inboxPanel.pivot = new Vector2(1f, 0.5f);
            inboxPanel.offsetMin = new Vector2(-InboxW, 40f); inboxPanel.offsetMax = new Vector2(0f, -80f);
            panel.raycastTarget = true;
            var edge = Img("Edge", inboxPanel, new Color(1f, 1f, 1f, 0.5f));
            var er = edge.rectTransform; er.anchorMin = new Vector2(0f, 0f); er.anchorMax = new Vector2(0f, 1f); er.pivot = new Vector2(0f, 0.5f); er.sizeDelta = new Vector2(2f, 0f); er.anchoredPosition = Vector2.zero;

            var title = Txt("Title", inboxPanel, "INBOX", 34, Color.white, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold; title.characterSpacing = 10f;
            Top(title.rectTransform, 28f, -24f, 300f, 44f);
            inboxCount = Txt("Count", inboxPanel, "", 13, Dim, TextAlignmentOptions.Left);
            inboxCount.fontStyle = FontStyles.Bold; inboxCount.characterSpacing = 4f;
            Top(inboxCount.rectTransform, 30f, -64f, 400f, 20f);
            TopButton("CLOSE  ESC", new Vector2(-28f, -30f), 130f, () => SetInbox(false));
            TopButton("CLEAR READ", new Vector2(-168f, -30f), 130f, ClearRead);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var f = Img("Filter " + FilterNames[i], inboxPanel, PanelHi);
                var fr = f.rectTransform; fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 1f);
                fr.anchoredPosition = new Vector2(28f + i * 126f, -96f); fr.sizeDelta = new Vector2(118f, 34f);
                f.raycastTarget = true;
                var b = f.gameObject.AddComponent<Button>(); b.targetGraphic = f; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetFilter(index));
                UIFeedback.Attach(f.gameObject, 1.02f, UISfx.Kind.Click);
                filterLabel[i] = Txt("Label", fr, FilterNames[i], 13, Dim, TextAlignmentOptions.Center);
                filterLabel[i].fontStyle = FontStyles.Bold; filterLabel[i].characterSpacing = 3f; Stretch(filterLabel[i].rectTransform);
                filterBg[i] = f;
            }

            var view = NewRect("List", inboxPanel, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            view.anchorMin = new Vector2(0f, 0f); view.anchorMax = new Vector2(1f, 1f); view.offsetMin = new Vector2(20f, 20f); view.offsetMax = new Vector2(-20f, -146f);
            var hit = Img("Hit", view, new Color(0f, 0f, 0f, 0f)); Stretch(hit.rectTransform); hit.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            inboxContent = NewRect("Content", view, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            inboxContent.anchorMin = new Vector2(0f, 1f); inboxContent.anchorMax = new Vector2(1f, 1f); inboxContent.pivot = new Vector2(0.5f, 1f);
            inboxScroll = view.gameObject.AddComponent<ScrollRect>();
            inboxScroll.viewport = view; inboxScroll.content = inboxContent; inboxScroll.horizontal = false; inboxScroll.movementType = ScrollRect.MovementType.Clamped; inboxScroll.scrollSensitivity = 30f;

            inboxStateBox = NewRect("State", inboxPanel, new Vector2(0.5f, 0.5f), new Vector2(480f, 200f), new Vector2(0f, -20f));
            inboxSpinner = Spinner(inboxStateBox, new Vector2(0f, 40f), 40f);
            inboxState = Txt("Text", inboxStateBox, "", 15, Dim, TextAlignmentOptions.Center);
            inboxState.fontStyle = FontStyles.Bold; inboxState.characterSpacing = 3f; inboxState.enableWordWrapping = true;
            Place(inboxState.rectTransform, new Vector2(0f, -30f), new Vector2(460f, 80f));

            inboxPanel.anchoredPosition = new Vector2(InboxW + 40f, inboxPanel.anchoredPosition.y);
            inboxPanel.gameObject.SetActive(false);
            SetFilter(0, false);
        }

        private void Top(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        }

        private void TopButton(string label, Vector2 pos, float w, Action click)
        {
            var b = Img(label, inboxPanel, new Color(1f, 1f, 1f, 0.12f));
            var r = b.rectTransform; r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(w, 36f);
            b.raycastTarget = true;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            var c = btn.colors; c.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f); c.pressedColor = new Color(2.2f, 2.2f, 2.2f, 1f); btn.colors = c;
            btn.onClick.AddListener(() => click());
            UIFeedback.Attach(b.gameObject, 1.03f);
            var t = Txt("Label", r, label, 13, Color.white, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold; t.characterSpacing = 4f; Stretch(t.rectTransform);
        }

        public bool InboxOpen => inboxOpen;
        public void ToggleInbox() { SetInbox(!inboxOpen); }

        public void SetInbox(bool open)
        {
            if (open == inboxOpen) return;
            inboxOpen = open;
            UISfx.Play(open ? UISfx.Kind.Open : UISfx.Kind.Back);
            if (open)
            {
                if (Typing) chatInput.DeactivateInputField();
                inboxPanel.gameObject.SetActive(true);
                inboxBackdrop.gameObject.SetActive(true);
                RebuildInbox();
                _ = svc.RefreshInboxAsync();
            }
            else
            {
                inboxClosedFrame = Time.frameCount;
                svc.MarkInboxSeen();
            }
        }

        private void SetFilter(int f, bool sound = true)
        {
            if (sound && f != filter) UISfx.Play(UISfx.Kind.Click);
            filter = f;
            for (int i = 0; i < 4; i++)
            {
                filterBg[i].color = i == filter ? Color.white : PanelHi;
                filterLabel[i].color = i == filter ? Color.black : Dim;
            }
            if (inboxOpen) RebuildInbox();
        }

        private static int Category(int code)
        {
            if (code == SocialService.NoteGift || code == SocialService.NoteLeaderboardReward || code == SocialService.NoteTierReached) return 1;
            if (code == SocialService.NoteMatchResult || code == SocialService.NoteLevelUp || code == SocialService.NoteWeaponLevelUp) return 2;
            return 3;
        }

        private void OnInboxChanged() { if (inboxOpen) RebuildInbox(); }

        private readonly Dictionary<string, float> cardIn = new Dictionary<string, float>();

        private void RebuildInbox()
        {
            for (int i = inboxContent.childCount - 1; i >= 0; i--) Destroy(inboxContent.GetChild(i).gameObject);
            var items = svc.Inbox.FindAll(i => filter == 0 || Category(i.code) == filter);
            int gifts = 0; foreach (var i in svc.Inbox) if (i.IsGift && !i.claimed && !i.expired) gifts++;
            inboxCount.text = svc.Inbox.Count + " NOTIFICATION" + (svc.Inbox.Count == 1 ? "" : "S") + (gifts > 0 ? "   |   " + gifts + " GIFT" + (gifts == 1 ? "" : "S") + " TO CLAIM" : "");

            bool loadingNow = svc.InboxLoading && !svc.InboxLoaded;
            if (!svc.Online) SetInboxState("OFFLINE  -  SIGN IN TO SEE YOUR NOTIFICATIONS", false);
            else if (loadingNow) SetInboxState("LOADING", true);
            else if (items.Count == 0) SetInboxState(filter == 0 ? "YOU ARE ALL CAUGHT UP" : "NOTHING HERE", false);
            else SetInboxState("", false);

            float y = 0f;
            long seen = 0;
            long.TryParse(PlayerPrefs.GetString("social.inbox.seen." + svc.MyUserId, "0"), out seen);
            DateTime lastDay = DateTime.MinValue;
            foreach (var item in items)
            {
                var day = item.createTime.ToLocalTime().Date;
                if (day != lastDay)
                {
                    lastDay = day;
                    var h = Txt("Day", inboxContent, day == DateTime.Now.Date ? "TODAY" : day == DateTime.Now.Date.AddDays(-1) ? "YESTERDAY" : day.ToString("dd MMM").ToUpperInvariant(), 12, Faint, TextAlignmentOptions.Left);
                    h.fontStyle = FontStyles.Bold; h.characterSpacing = 5f;
                    var hr = h.rectTransform; hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f); hr.anchoredPosition = new Vector2(8f, -y - 6f); hr.sizeDelta = new Vector2(-16f, 22f);
                    y += 30f;
                }
                float hgt = item.IsGift || item.code == SocialService.NoteTierReached || item.code == SocialService.NoteLeaderboardReward ? 118f : 96f;
                BuildCard(item, y, hgt, item.createTime.Ticks > seen);
                y += hgt + 8f;
            }
            inboxContent.sizeDelta = new Vector2(0f, y);
        }

        private void SetInboxState(string text, bool spin)
        {
            inboxStateBox.gameObject.SetActive(!string.IsNullOrEmpty(text));
            inboxState.text = text;
            inboxSpinner.gameObject.SetActive(spin);
        }

        private void BuildCard(InboxItem item, float y, float h, bool isNew)
        {
            var card = Img("Card", inboxContent, isNew ? PanelHi : new Color(0.08f, 0.085f, 0.1f, 1f));
            var cr = card.rectTransform; cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = new Vector2(0f, -y); cr.sizeDelta = new Vector2(0f, h);
            card.raycastTarget = true;
            Color accent = AccentOf(item.code);
            var acc = Img("Accent", cr, isNew ? accent : new Color(accent.r, accent.g, accent.b, 0.35f));
            var ar = acc.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f); ar.sizeDelta = new Vector2(4f, 0f); ar.anchoredPosition = Vector2.zero;

            var tile = Img("Tile", cr, new Color(accent.r, accent.g, accent.b, 0.13f));
            var tr = tile.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 1f); tr.anchoredPosition = new Vector2(18f, -16f); tr.sizeDelta = new Vector2(56f, 56f);
            Icon(tr, IconOf(item.code), Vector2.zero, new Vector2(32f, 32f), accent);

            var subject = Txt("Subject", cr, item.subject, 17, Color.white, TextAlignmentOptions.TopLeft);
            subject.fontStyle = FontStyles.Bold; subject.characterSpacing = 2f; subject.enableWordWrapping = false; subject.overflowMode = TextOverflowModes.Ellipsis;
            subject.enableAutoSizing = true; subject.fontSizeMin = 13f; subject.fontSizeMax = 17f;
            Top(subject.rectTransform, 90f, -14f, 330f, 26f);
            var when = Txt("When", cr, Ago(item.createTime), 11, Faint, TextAlignmentOptions.TopRight);
            when.fontStyle = FontStyles.Bold; when.characterSpacing = 3f;
            var wr = when.rectTransform; wr.anchorMin = wr.anchorMax = new Vector2(1f, 1f); wr.pivot = new Vector2(1f, 1f); wr.anchoredPosition = new Vector2(-44f, -17f); wr.sizeDelta = new Vector2(90f, 18f);
            var detail = Txt("Detail", cr, Detail(item), 14, Dim, TextAlignmentOptions.TopLeft);
            detail.enableWordWrapping = true; detail.characterSpacing = 1f;
            Top(detail.rectTransform, 90f, -42f, 420f, 44f);

            // actions
            if (item.IsGift)
            {
                int coins = item.Int("coins");
                var amount = Txt("Coins", cr, "+" + coins.ToString("N0"), 18, Gold, TextAlignmentOptions.Left);
                amount.fontStyle = FontStyles.Bold;
                var amr = amount.rectTransform; amr.anchorMin = amr.anchorMax = new Vector2(0f, 0f); amr.pivot = new Vector2(0f, 0f); amr.anchoredPosition = new Vector2(116f, 12f); amr.sizeDelta = new Vector2(120f, 26f);
                var coinIcon = Icon(cr, "UI/Lobby/Icon_GameMoney_G", Vector2.zero, new Vector2(20f, 20f), Gold).rectTransform;
                coinIcon.anchorMin = coinIcon.anchorMax = new Vector2(0f, 0f); coinIcon.pivot = new Vector2(0f, 0f); coinIcon.anchoredPosition = new Vector2(90f, 15f);
                if (item.claimed) ActionChip(cr, "CLAIMED", false, Good, null);
                else if (item.expired) ActionChip(cr, "EXPIRED", false, Faint, null);
                else ActionChip(cr, "CLAIM", true, Color.black, chip => ClaimGift(item, chip));
            }
            else if (item.code == SocialService.NoteTierReached && item.content["claimableTiers"] is Newtonsoft.Json.Linq.JArray tiers && tiers.Count > 0)
                ActionChip(cr, "OPEN BATTLE PASS", true, Color.black, _ => { SetInbox(false); openBattlePass?.Invoke(); });
            else if (item.code == SocialService.NoteLeaderboardReward)
                ActionChip(cr, "VIEW LEADERBOARD", true, Color.black, _ => { SetInbox(false); openLeaderboards?.Invoke(); });

            // dismiss (a gift that can still be claimed stays)
            bool canDismiss = !(item.IsGift && !item.claimed && !item.expired);
            if (canDismiss)
            {
                var x = Img("Dismiss", cr, new Color(1f, 1f, 1f, 0f));
                var xr = x.rectTransform; xr.anchorMin = xr.anchorMax = new Vector2(1f, 1f); xr.pivot = new Vector2(1f, 1f); xr.anchoredPosition = new Vector2(-10f, -10f); xr.sizeDelta = new Vector2(28f, 28f);
                x.raycastTarget = true;
                var xb = x.gameObject.AddComponent<Button>(); xb.targetGraphic = x; xb.transition = Selectable.Transition.None;
                var xi = Icon(xr, "HUD/Icons/Close_Circle", Vector2.zero, new Vector2(18f, 18f), Faint);
                OnHover(x.gameObject, on => { if (xi != null) xi.color = on ? Color.white : Faint; });
                xb.onClick.AddListener(() => Dismiss(item));
                UIFeedback.Attach(x.gameObject, 1.1f, UISfx.Kind.Back);
            }
            // a new card slides in
            if (!cardIn.ContainsKey(item.id)) { cardIn[item.id] = Time.unscaledTime; var g = card.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; }
        }

        private void ActionChip(RectTransform card, string label, bool primary, Color textColor, Action<TextMeshProUGUI> click)
        {
            var b = Img(label, card, primary ? Color.white : new Color(1f, 1f, 1f, 0.06f));
            var r = b.rectTransform; r.anchorMin = r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(1f, 0f); r.anchoredPosition = new Vector2(-14f, 12f); r.sizeDelta = new Vector2(label.Length > 8 ? 170f : 112f, 32f);
            var t = Txt("Label", r, label, 13, textColor, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold; t.characterSpacing = 3f; Stretch(t.rectTransform);
            if (click == null) return;
            b.raycastTarget = true;
            var btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = b;
            var c = btn.colors; c.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); c.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f); btn.colors = c;
            btn.onClick.AddListener(() => click(t));
            UIFeedback.Attach(b.gameObject, 1.04f).silentClick = true;
            if (primary)
            {
                var glow = Img("Glow", r, new Color(1f, 1f, 1f, 0.15f)); glow.transform.SetAsFirstSibling();
                var gr = glow.rectTransform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = new Vector2(-4f, -4f); gr.offsetMax = new Vector2(4f, 4f);
                glows.Add(glow);
            }
        }

        private readonly List<Image> glows = new List<Image>();

        private async void ClaimGift(InboxItem item, TextMeshProUGUI label)
        {
            label.text = "CLAIMING";
            string status = await svc.ClaimGiftAsync(item);
            if (this == null) return;
            switch (status)
            {
                case "ok": UISfx.Play(UISfx.Kind.Reward); ShowToast(new InboxItem { code = SocialService.NoteGift, subject = "+" + item.Int("coins").ToString("N0") + " COINS CLAIMED", content = item.content, createTime = DateTime.UtcNow }, false); break;
                case "already_claimed": UISfx.Play(UISfx.Kind.Error); break;
                case "expired": UISfx.Play(UISfx.Kind.Error); break;
                default: UISfx.Play(UISfx.Kind.Error); if (label != null) label.text = "TRY AGAIN"; break;
            }
            RebuildInbox();
        }

        private async void Dismiss(InboxItem item)
        {
            await svc.DismissAsync(new[] { item });
        }

        private async void ClearRead()
        {
            var list = svc.Inbox.FindAll(i => !(i.IsGift && !i.claimed && !i.expired) && (filter == 0 || Category(i.code) == filter));
            if (list.Count == 0) { UISfx.Play(UISfx.Kind.Error); return; }
            UISfx.Play(UISfx.Kind.Back);
            await svc.DismissAsync(list);
        }

        private void TickInbox(float dt)
        {
            if (inboxPanel == null) return;
            inboxT = Mathf.MoveTowards(inboxT, inboxOpen ? 1f : 0f, dt * 5f);
            float e = 1f - Mathf.Pow(1f - inboxT, 3f);
            inboxPanel.anchoredPosition = new Vector2((1f - e) * (InboxW + 40f), inboxPanel.anchoredPosition.y);
            inboxBackdrop.color = new Color(0f, 0f, 0f, 0.5f * e);
            if (!inboxOpen && inboxT <= 0f && inboxPanel.gameObject.activeSelf) { inboxPanel.gameObject.SetActive(false); inboxBackdrop.gameObject.SetActive(false); }
            if (inboxOpen && Input.GetKeyDown(KeyCode.Escape)) SetInbox(false);
            if (inboxOpen && Input.GetKeyDown(KeyCode.N) && !Typing && Time.frameCount - lastTypingFrame > 1) SetInbox(false);

            float pulse = 0.12f + 0.12f * Mathf.Sin(Time.unscaledTime * 3.2f);
            for (int i = glows.Count - 1; i >= 0; i--) { if (glows[i] == null) glows.RemoveAt(i); else glows[i].color = new Color(1f, 1f, 1f, pulse); }
            // cards fade in one after another
            int k = 0;
            for (int i = 0; i < inboxContent.childCount; i++)
            {
                var g = inboxContent.GetChild(i).GetComponent<CanvasGroup>();
                if (g == null || g.alpha >= 1f) continue;
                float t = (Time.unscaledTime - 0.04f * k++);
                g.alpha = Mathf.MoveTowards(g.alpha, 1f, dt * 5f * Mathf.Clamp01(t));
            }
        }

        // ================================================================= live toasts
        private sealed class Toast { public RectTransform rect; public CanvasGroup group; public Image timer; public float born, life; public InboxItem item; public bool closing; }
        private readonly List<Toast> toasts = new List<Toast>();
        private const float ToastTop = -172f;   // under the header strip and the menus' BACK button

        private void BuildToasts()
        {
            var go = new GameObject("[Lobby] Social Toasts", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;   // above the full-screen menus: a gift that arrives in the Store is still seen
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            toastRoot = (RectTransform)go.transform;
        }

        private void OnLive(InboxItem item)
        {
            // a mention while the chat is in front of the player is enough as the highlighted line
            if (item.code == SocialService.NoteChatMention && (Typing || chatHovered)) return;
            ShowToast(item, true);
        }

        private void ShowToast(InboxItem item, bool sound)
        {
            if (toastRoot == null) return;
            while (toasts.Count >= 3) { CloseToast(toasts[0]); toasts.RemoveAt(0); }
            Color accent = AccentOf(item.code);
            var bg = Img("Toast", toastRoot, new Color(0.04f, 0.045f, 0.055f, 0.97f));
            var r = bg.rectTransform; r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f); r.sizeDelta = new Vector2(440f, 86f);
            r.anchoredPosition = new Vector2(480f, ToastTop);
            bg.raycastTarget = true;
            var acc = Img("Accent", r, accent);
            var ar = acc.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f); ar.sizeDelta = new Vector2(5f, 0f); ar.anchoredPosition = Vector2.zero;
            var tile = Img("Tile", r, new Color(accent.r, accent.g, accent.b, 0.14f));
            Place(tile.rectTransform, new Vector2(-180f, 0f), new Vector2(52f, 52f));
            Icon(tile.rectTransform, IconOf(item.code), Vector2.zero, new Vector2(30f, 30f), accent);
            var kind = Txt("Kind", r, KindOf(item.code), 11, accent, TextAlignmentOptions.TopLeft); kind.fontStyle = FontStyles.Bold; kind.characterSpacing = 4f;
            Place(kind.rectTransform, new Vector2(42f, 26f), new Vector2(300f, 16f));
            var s = Txt("Subject", r, item.subject, 16, Color.white, TextAlignmentOptions.TopLeft); s.fontStyle = FontStyles.Bold; s.characterSpacing = 1.5f; s.overflowMode = TextOverflowModes.Ellipsis;
            Place(s.rectTransform, new Vector2(42f, 6f), new Vector2(300f, 22f));
            var d = Txt("Detail", r, Detail(item), 13, Dim, TextAlignmentOptions.TopLeft); d.overflowMode = TextOverflowModes.Ellipsis;
            Place(d.rectTransform, new Vector2(42f, -18f), new Vector2(300f, 20f));
            var timer = Img("Timer", r, new Color(accent.r, accent.g, accent.b, 0.7f));
            var tr = timer.rectTransform; tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0f, 0f); tr.sizeDelta = new Vector2(0f, 2f); tr.anchoredPosition = Vector2.zero;
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
            var toast = new Toast { rect = r, group = bg.gameObject.AddComponent<CanvasGroup>(), timer = timer, born = Time.unscaledTime, life = 6f, item = item };
            b.onClick.AddListener(() =>
            {
                toast.closing = true;
                if (item.code == SocialService.NoteChatMention) FocusChat();
                else if (lobbyIdle == null || lobbyIdle()) SetInbox(true);
            });
            UIFeedback.Attach(bg.gameObject, 1.02f).silentClick = true;
            toasts.Add(toast);
            if (sound)
            {
                bool reward = item.code == SocialService.NoteGift || item.code == SocialService.NoteLeaderboardReward;
                UISfx.Play(reward ? UISfx.Kind.Reward : item.code == SocialService.NoteChatMention ? UISfx.Kind.Confirm : item.code == SocialService.NoteChatMuted ? UISfx.Kind.Error : UISfx.Kind.Open);
            }
        }

        private void CloseToast(Toast t) { if (t.rect != null) Destroy(t.rect.gameObject); }

        private void TickToasts(float dt)
        {
            float y = ToastTop;
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                if (t.rect == null) { toasts.RemoveAt(i--); continue; }
                float age = Time.unscaledTime - t.born;
                bool hovered = RectTransformUtility.RectangleContainsScreenPoint(t.rect, Input.mousePosition, null);
                if (hovered) t.born += dt;   // a toast the pointer is on stays
                if (age > t.life) t.closing = true;
                float targetX = t.closing ? 480f : -36f;
                var p = t.rect.anchoredPosition;
                p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-dt * 10f));
                p.y = Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-dt * 12f));
                t.rect.anchoredPosition = p;
                t.group.alpha = t.closing ? Mathf.MoveTowards(t.group.alpha, 0f, dt * 4f) : Mathf.MoveTowards(t.group.alpha, 1f, dt * 6f);
                t.timer.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(1f - age / t.life), 0f);
                t.timer.rectTransform.sizeDelta = new Vector2(0f, 2f);
                if (t.closing && t.group.alpha <= 0.01f) { CloseToast(t); toasts.RemoveAt(i--); continue; }
                if (!t.closing) y -= 94f;
            }
        }

        // ================================================================= text and icons of a notification
        private static Color AccentOf(int code)
        {
            switch (code)
            {
                case SocialService.NoteGift: case SocialService.NoteLeaderboardReward: return Gold;
                case SocialService.NoteLevelUp: case SocialService.NoteTierReached: case SocialService.NoteWeaponLevelUp: return Good;
                case SocialService.NoteAnnouncement: return Cyan;
                case SocialService.NoteChatMuted: return Bad;
                case SocialService.NoteChatMention: return Gold;
                case SocialService.NoteFriendRequest: case SocialService.NoteFriendAccepted: return Cyan;
                default: return Color.white;
            }
        }

        private static string IconOf(int code)
        {
            switch (code)
            {
                case SocialService.NoteMatchResult: return "UI/Lobby/Icon_DiamondCrosshair";
                case SocialService.NoteLevelUp: return "UI/Lobby/Icon_TabRanked";
                case SocialService.NoteWeaponLevelUp: return "UI/Lobby/Icon_TabLoadout";
                case SocialService.NoteTierReached: return "UI/Icons/Icon_Emblem";
                case SocialService.NoteLeaderboardReward: return "UI/Lobby/Icon_TabLeaderboard";
                case SocialService.NoteGift: return "UI/Lobby/Icon_EventGift";
                case SocialService.NoteAnnouncement: return "UI/Lobby/Icon_Globe";
                case SocialService.NoteChatMuted: return "UI/Lobby/Icon_Warning";
                case SocialService.NoteChatMention: return "UI/Lobby/Icon_ChatShield";
                case SocialService.NoteFriendRequest: case SocialService.NoteFriendAccepted: return "UI/Lobby/Icon_FriendAdd";
                default: return "UI/Lobby/Icon_Mail";
            }
        }

        private static string KindOf(int code)
        {
            switch (code)
            {
                case SocialService.NoteMatchResult: return "MATCH RESULT";
                case SocialService.NoteLevelUp: return "LEVEL UP";
                case SocialService.NoteWeaponLevelUp: return "WEAPON MASTERY";
                case SocialService.NoteTierReached: return "BATTLE PASS";
                case SocialService.NoteLeaderboardReward: return "LEADERBOARD REWARD";
                case SocialService.NoteGift: return "GIFT";
                case SocialService.NoteAnnouncement: return "ANNOUNCEMENT";
                case SocialService.NoteChatMuted: return "CHAT";
                case SocialService.NoteChatMention: return "MENTION";
                case SocialService.NoteFriendRequest: return "FRIEND REQUEST";
                case SocialService.NoteFriendAccepted: return "FRIENDS";
                default: return "NOTIFICATION";
            }
        }

        /// <summary>The second line of a notification, from its content.</summary>
        public static string Detail(InboxItem i)
        {
            switch (i.code)
            {
                case SocialService.NoteMatchResult:
                    return "#" + i.Int("placement") + " of " + i.Int("totalPlayers") + "   |   " + i.Int("kills") + " kills   |   +" + i.Int("xp").ToString("N0") + " XP   |   +" + i.Int("coins").ToString("N0") + " coins";
                case SocialService.NoteLevelUp:
                    return "Level " + i.Int("oldLevel") + " to " + i.Int("newLevel") + (i.Int("bonusCoins") > 0 ? "   |   +" + i.Int("bonusCoins").ToString("N0") + " bonus coins" : "");
                case SocialService.NoteWeaponLevelUp:
                {
                    var parts = new List<string>();
                    if (i.content["weapons"] is Newtonsoft.Json.Linq.JArray ws)
                        foreach (var w in ws) parts.Add(Railbound.Gameplay.WeaponIds.Display((string)w["weapon"]) + " LV " + (int?)w["level"]);
                    return string.Join("   |   ", parts);
                }
                case SocialService.NoteTierReached:
                {
                    int claim = i.content["claimableTiers"] is Newtonsoft.Json.Linq.JArray t ? t.Count : 0;
                    return "Tier " + i.Int("oldTier") + " to " + i.Int("newTier") + (claim > 0 ? "   |   " + claim + " coin reward" + (claim == 1 ? "" : "s") + " to claim" : "");
                }
                case SocialService.NoteLeaderboardReward:
                    return i.Str("title") + "   |   rank #" + i.Int("rank") + "   |   score " + i.Int("score").ToString("N0") + "   |   +" + i.Int("coins").ToString("N0") + " coins paid";
                case SocialService.NoteGift:
                {
                    string msg = i.Str("message");
                    long exp = i.content["expiresAt"] != null ? (long)i.content["expiresAt"] : 0;
                    string left = exp > 0 ? Left(DateTimeOffset.FromUnixTimeMilliseconds(exp).UtcDateTime) : "";
                    return (msg.Length > 0 ? msg + "   |   " : "") + (i.claimed ? "claimed" : i.expired ? "expired" : left.Length > 0 ? "expires in " + left : "");
                }
                case SocialService.NoteAnnouncement: return i.Str("message");
                case SocialService.NoteChatMuted:
                {
                    long until = i.content["until"] != null ? (long)i.content["until"] : 0;
                    return (i.Str("reason").Length > 0 ? i.Str("reason") + "   |   " : "") + (until > 0 ? "until " + DateTimeOffset.FromUnixTimeMilliseconds(until).ToLocalTime().ToString("HH:mm") : "");
                }
                case SocialService.NoteChatMention: return i.Str("from") + ": " + i.Str("message");
                case SocialService.NoteFriendRequest: return "Wants to add you as a friend";
                case SocialService.NoteFriendAccepted: return "You are now friends: you can message each other and compare scores";
                default: return "";
            }
        }

        private static string Left(DateTime utc)
        {
            var d = utc - DateTime.UtcNow;
            if (d.TotalSeconds <= 0) return "";
            return d.TotalDays >= 1 ? (int)d.TotalDays + "d " + d.Hours + "h" : d.TotalHours >= 1 ? (int)d.TotalHours + "h " + d.Minutes + "m" : Math.Max(1, d.Minutes) + "m";
        }

        private static string Ago(DateTime utc)
        {
            var d = DateTime.UtcNow - utc;
            if (d.TotalMinutes < 1) return "NOW";
            if (d.TotalHours < 1) return (int)d.TotalMinutes + "M AGO";
            if (d.TotalDays < 1) return (int)d.TotalHours + "H AGO";
            return (int)d.TotalDays + "D AGO";
        }

        // ================================================================= test hooks (LobbyShotAid -socialshot)
        public void TestFilter(int f) { SetFilter(f); }
        public InboxItem TestFirstGift() { return svc.Inbox.Find(i => i.IsGift && !i.claimed && !i.expired); }
        public void TestClaim(InboxItem i) { if (i != null) ClaimGift(i, Txt("x", root, "", 1, Color.clear, TextAlignmentOptions.Center)); }
        public void TestSend(string text) { FocusChat(); chatInput.text = text; OnChatSubmit(text); }
        public ChatLine TestFirstOtherLine() { var l = svc.Lines(SocialService.Rooms[room]); for (int i = l.Count - 1; i >= 0; i--) if (!l[i].isMe && !l[i].system) return l[i]; return null; }
        public void TestReport(ChatLine l) { if (l != null) Report(l, Txt("x", root, "", 1, Color.clear, TextAlignmentOptions.Center)); }
        public void TestHoverChat(bool on) { chatHovered = on; }
        public string TestError => chatError != null ? chatError.text : "";

        // ================================================================= tiny UI kit
        private Image Img(string name, RectTransform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        private TextMeshProUGUI Txt(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private Image Icon(RectTransform parent, string path, Vector2 pos, Vector2 size, Color color)
        {
            var i = Img("Icon", parent, color);
            if (!sprites.TryGetValue(path ?? "", out var s) || s == null) { s = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path); sprites[path ?? ""] = s; }
            i.sprite = s; i.preserveAspect = true; i.enabled = s != null;
            Place(i.rectTransform, pos, size);
            return i;
        }

        private RectTransform Spinner(RectTransform parent, Vector2 pos, float size)
        {
            var i = Icon(parent, "UI/Icon_Spinner", pos, new Vector2(size, size), Color.white);
            i.gameObject.AddComponent<SpinUI>();
            return i.rectTransform;
        }

        private sealed class SpinUI : MonoBehaviour { private void Update() { transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime); } }

        private static void OnHover(GameObject go, Action<bool> changed)
        {
            var h = go.GetComponent<HoverRelay>(); if (h == null) h = go.AddComponent<HoverRelay>();
            h.changed = changed;
        }

        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<bool> changed;
            public void OnPointerEnter(PointerEventData e) { changed?.Invoke(true); }
            public void OnPointerExit(PointerEventData e) { changed?.Invoke(false); }
        }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGradient()
        {
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[2 * h];
            for (int y = 0; y < h; y++) { float a = Mathf.Pow(y / (float)(h - 1), 1.4f); for (int x = 0; x < 2; x++) cols[y * 2 + x] = new Color(1f, 1f, 1f, a); }
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
