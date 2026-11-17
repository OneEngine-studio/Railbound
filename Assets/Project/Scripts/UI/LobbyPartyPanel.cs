using System;
using System.Collections.Generic;
using System.Linq;
using Railbound.Networking;
using Railbound.Social;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// SOCIAL (F1 / the SOCIAL chip): the party and the friends side by side.
    ///   YOUR PARTY - four slots (you, the leader, each member's level and readiness; empty slots point at the friends list), the squad
    ///                the party deploys as, LEAVE PARTY, and the party code with COPY / JOIN WITH CODE as a fallback.
    ///   FRIENDS    - ONLINE / ALL / REQUESTS, add by username, INVITE an online friend with one click (they get a toast and an inbox
    ///                card with JOIN), accept / decline requests, remove on hover.
    /// The party is a Nakama realtime party (<see cref="NakamaPartyService"/>); invites go through the server (railbound_party_invite,
    /// friends only). Built at runtime in the lobby's style, with motion and sounds.
    /// </summary>
    public class LobbyPartyPanel : MonoBehaviour
    {
        private static readonly Color Panel = new Color(0.04f, 0.045f, 0.055f, 0.985f);
        private static readonly Color Card = new Color(0.08f, 0.085f, 0.1f, 1f);
        private static readonly Color CardHi = new Color(0.12f, 0.13f, 0.15f, 1f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color Online = new Color(0.45f, 0.95f, 0.55f, 1f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color Squad = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color Bad = new Color(1f, 0.38f, 0.35f, 1f);

        public bool IsOpen => root != null && root.gameObject.activeSelf;
        /// <summary>True while the player types in a field (the lobby's keyboard shortcuts must not fire then).</summary>
        public bool IsTyping => IsOpen && ((codeInput != null && codeInput.isFocused) || (friendInput != null && friendInput.isFocused));

        private RectTransform root, window, slotsRoot, friendsList;
        private CanvasGroup windowGroup;
        private Image dim;
        private TMP_FontAsset font;
        private Sprite px;
        private NakamaPartyService service;
        private TextMeshProUGUI squadLine, statusLine, codeText, countLabel, friendsHeader;
        private TMP_InputField codeInput, friendInput;
        private Button leaveButton, copyButton;
        private readonly Image[] tabBg = new Image[3];
        private readonly TextMeshProUGUI[] tabLabel = new TextMeshProUGUI[3];
        private int friendTab;   // 0 online, 1 all, 2 requests
        private string notice = "";
        private float noticeUntil, openT, rebuildAt;
        private readonly Dictionary<string, float> invitedAt = new Dictionary<string, float>();
        private readonly List<(CanvasGroup g, float delay)> reveals = new List<(CanvasGroup, float)>();
        private float revealClock;

        public static LobbyPartyPanel Create(Transform parent, TMP_FontAsset fontAsset)
        {
            var go = new GameObject("[Lobby] Social Panel");
            var panel = go.AddComponent<LobbyPartyPanel>();
            panel.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            panel.Build();
            return panel;
        }

        // ================================================================= open / close
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        public void Show()
        {
            if (root == null) return;
            if (!IsOpen) { openT = 0f; revealClock = Time.unscaledTime; }
            root.gameObject.SetActive(true);
            UISfx.Play(UISfx.Kind.Open);

            service = NakamaPartyService.Instance;
            if (service != null)
            {
                service.Changed -= OnPartyChanged; service.Changed += OnPartyChanged;
                service.Notice -= SetNotice; service.Notice += SetNotice;
            }
            bool signedIn = AuthenticationService.Instance != null && AuthenticationService.Instance.IsAuthenticated;
            if (!signedIn) SetNotice("SIGN IN TO PARTY UP WITH FRIENDS");
            else if (service != null && service.Unsupported) SetNotice("PARTIES ARE NOT AVAILABLE ON THIS SERVER YET");
            else if (service != null && !service.InParty) _ = service.CreateAsync();   // your own party, ready for invites

            EnsureEventSystem();
            FriendsService.Changed -= OnFriendsChanged; FriendsService.Changed += OnFriendsChanged;
            _ = FriendsService.RefreshAsync();
            RebuildAll(true);
        }

        public void Hide()
        {
            if (root == null || !IsOpen) return;
            if (service != null) { service.Changed -= OnPartyChanged; service.Notice -= SetNotice; }
            FriendsService.Changed -= OnFriendsChanged;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            UISfx.Play(UISfx.Kind.Back);
            root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (service != null) { service.Changed -= OnPartyChanged; service.Notice -= SetNotice; }
            FriendsService.Changed -= OnFriendsChanged;
        }

        private void OnPartyChanged() { rebuildAt = Time.unscaledTime; }
        private void OnFriendsChanged() { rebuildAt = Time.unscaledTime; }

        private void SetNotice(string line)
        {
            notice = line ?? "";
            noticeUntil = Time.unscaledTime + 5f;
            if (statusLine != null) statusLine.text = notice;
        }

        private void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            openT = Mathf.MoveTowards(openT, 1f, dt * 5f);
            float e = 1f - Mathf.Pow(1f - openT, 3f);
            windowGroup.alpha = e;
            window.localScale = Vector3.one * (0.965f + 0.035f * e);
            dim.color = new Color(0f, 0f, 0f, 0.72f * e);
            if (notice.Length > 0 && Time.unscaledTime > noticeUntil) { notice = ""; statusLine.text = ""; }
            if (rebuildAt > 0f && Time.unscaledTime >= rebuildAt) { rebuildAt = 0f; RebuildAll(false); }
            // staggered reveals of the rows
            for (int i = reveals.Count - 1; i >= 0; i--)
            {
                var (g, delay) = reveals[i];
                if (g == null) { reveals.RemoveAt(i); continue; }
                g.alpha = Mathf.Clamp01((Time.unscaledTime - revealClock - delay) / 0.22f);
                if (g.alpha >= 1f) reveals.RemoveAt(i);
            }
            if (Input.GetKeyDown(KeyCode.Tab) && !IsTyping) SetFriendTab((friendTab + 1) % 3);
        }

        // ================================================================= build
        private void Build()
        {
            px = MakePixel();
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 880;
            var sc = canvasGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGo.transform;

            dim = Img("Dim", root, new Color(0f, 0f, 0f, 0.72f)); Stretch(dim.rectTransform); dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>(); dimBtn.transition = Selectable.Transition.None; dimBtn.onClick.AddListener(Hide);

            window = NewRect("Window", root, new Vector2(1320f, 760f), Vector2.zero);
            windowGroup = window.gameObject.AddComponent<CanvasGroup>();
            var bg = Img("Bg", window, Panel); Stretch(bg.rectTransform); bg.raycastTarget = true;
            var edge = Img("Edge", window, new Color(1f, 1f, 1f, 0.45f)); TopBar(edge.rectTransform, 3f);

            // header
            var title = Txt(window, "SOCIAL", 40, Color.white, TextAlignmentOptions.Left, new Vector2(-640f + 40f + 200f, 330f), new Vector2(400f, 52f));
            title.fontStyle = FontStyles.Bold; title.characterSpacing = 12f;
            Txt(window, "PARTY UP WITH FRIENDS AND DEPLOY TOGETHER AS ONE SQUAD", 14, Dim, TextAlignmentOptions.Left, new Vector2(-640f + 40f + 300f, 292f), new Vector2(600f, 22f)).characterSpacing = 4f;
            MakeButton(window, "CLOSE   ESC", new Vector2(640f - 40f - 85f, 326f), new Vector2(170f, 44f), false, Hide, 14f, UISfx.Kind.Back);
            var rule = Img("Rule", window, Line); Place(rule.rectTransform, new Vector2(0f, 262f), new Vector2(1240f, 1f));

            BuildPartyColumn();
            BuildFriendsColumn();

            statusLine = Txt(window, "", 15, Gold, TextAlignmentOptions.Center, new Vector2(0f, -352f), new Vector2(1200f, 24f));
            statusLine.fontStyle = FontStyles.Bold; statusLine.characterSpacing = 3f;
            root.gameObject.SetActive(false);
        }

        // ---------------- party column ----------------
        private void BuildPartyColumn()
        {
            var col = NewRect("Party", window, new Vector2(560f, 600f), new Vector2(-330f, -30f));
            var head = Txt(col, "YOUR PARTY", 20, Color.white, TextAlignmentOptions.Left, new Vector2(-280f + 150f, 270f), new Vector2(300f, 30f));
            head.fontStyle = FontStyles.Bold; head.characterSpacing = 6f;
            countLabel = Txt(col, "", 14, Dim, TextAlignmentOptions.Right, new Vector2(280f - 100f, 270f), new Vector2(200f, 24f));
            countLabel.fontStyle = FontStyles.Bold; countLabel.characterSpacing = 4f;
            squadLine = Txt(col, "", 14, Squad, TextAlignmentOptions.Left, new Vector2(-280f + 260f, 240f), new Vector2(520f, 22f));
            squadLine.fontStyle = FontStyles.Bold; squadLine.characterSpacing = 3f;
            slotsRoot = NewRect("Slots", col, new Vector2(560f, 400f), new Vector2(0f, 20f));

            // the code (fallback for players who are not friends yet)
            var codeBox = Img("CodeBox", col, Card); Place(codeBox.rectTransform, new Vector2(0f, -218f), new Vector2(560f, 64f));
            var cb = codeBox.rectTransform;
            Txt(cb, "PARTY CODE", 11, Faint, TextAlignmentOptions.Left, new Vector2(-280f + 18f + 60f, 14f), new Vector2(120f, 16f)).characterSpacing = 4f;
            codeText = Txt(cb, "", 14, Color.white, TextAlignmentOptions.Left, new Vector2(-280f + 18f + 170f, -9f), new Vector2(340f, 22f));
            codeText.overflowMode = TextOverflowModes.Ellipsis;
            copyButton = MakeButton(cb, "COPY", new Vector2(280f - 12f - 50f, 0f), new Vector2(100f, 40f), false, OnCopy, 13f);
            codeInput = BuildInput(col, "OR PASTE A FRIEND'S PARTY CODE", new Vector2(-60f, -282f), new Vector2(440f, 46f), OnJoinCode);
            MakeButton(col, "JOIN", new Vector2(280f - 55f, -282f), new Vector2(110f, 46f), false, OnJoinCode, 14f);
            leaveButton = MakeButton(col, "LEAVE PARTY", new Vector2(0f, -335f + 0f), new Vector2(560f, 40f), false, OnLeave, 13f, UISfx.Kind.Back);
            leaveButton.GetComponent<Image>().color = new Color(Bad.r, Bad.g, Bad.b, 0.18f);
        }

        // ---------------- friends column ----------------
        private void BuildFriendsColumn()
        {
            var col = NewRect("Friends", window, new Vector2(620f, 600f), new Vector2(310f, -30f));
            var head = Txt(col, "FRIENDS", 20, Color.white, TextAlignmentOptions.Left, new Vector2(-310f + 150f, 270f), new Vector2(300f, 30f));
            head.fontStyle = FontStyles.Bold; head.characterSpacing = 6f;
            friendsHeader = Txt(col, "", 14, Dim, TextAlignmentOptions.Right, new Vector2(310f - 150f, 270f), new Vector2(300f, 24f));
            friendsHeader.fontStyle = FontStyles.Bold; friendsHeader.characterSpacing = 4f;

            string[] names = { "ONLINE", "ALL", "REQUESTS" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var t = Img("Tab " + names[i], col, CardHi);
                Place(t.rectTransform, new Vector2(-310f + 100f + i * 206f, 226f), new Vector2(198f, 40f));
                t.raycastTarget = true;
                var b = t.gameObject.AddComponent<Button>(); b.targetGraphic = t; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SetFriendTab(index));
                UIFeedback.Attach(t.gameObject, 1.02f, UISfx.Kind.Click);
                tabLabel[i] = Txt(t.rectTransform, names[i], 14, Dim, TextAlignmentOptions.Center, Vector2.zero, new Vector2(190f, 30f));
                tabLabel[i].fontStyle = FontStyles.Bold; tabLabel[i].characterSpacing = 4f;
                tabBg[i] = t;
            }

            friendInput = BuildInput(col, "ADD A FRIEND BY USERNAME", new Vector2(-310f + 240f, 172f), new Vector2(480f, 46f), OnAddFriend);
            friendInput.characterLimit = 24;
            MakeButton(col, "ADD", new Vector2(310f - 60f, 172f), new Vector2(120f, 46f), true, OnAddFriend, 14f);

            var view = NewRect("List", col, new Vector2(620f, 410f), new Vector2(0f, -66f));
            var hit = Img("Hit", view, new Color(0f, 0f, 0f, 0f)); Stretch(hit.rectTransform); hit.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            friendsList = NewRect("Content", view, new Vector2(620f, 0f), Vector2.zero);
            friendsList.anchorMin = new Vector2(0f, 1f); friendsList.anchorMax = new Vector2(1f, 1f); friendsList.pivot = new Vector2(0.5f, 1f);
            friendsList.anchoredPosition = Vector2.zero; friendsList.sizeDelta = new Vector2(0f, 0f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = friendsList; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        }

        private void SetFriendTab(int t)
        {
            if (t != friendTab) UISfx.Play(UISfx.Kind.Click);
            friendTab = t;
            revealClock = Time.unscaledTime;
            RebuildFriends();
        }

        // ================================================================= refresh
        private void RebuildAll(bool reveal)
        {
            if (!IsOpen) return;
            if (reveal) revealClock = Time.unscaledTime;
            RebuildParty();
            RebuildFriends();
        }

        private string MyUserId => AuthenticationService.Instance != null && AuthenticationService.Instance.Session != null ? AuthenticationService.Instance.Session.UserId : "";

        private void RebuildParty()
        {
            for (int i = slotsRoot.childCount - 1; i >= 0; i--) Destroy(slotsRoot.GetChild(i).gameObject);
            var lobby = LobbyParty.Instance;
            var members = lobby != null ? lobby.Members.ToList() : new List<PartyMember>();
            bool inParty = service != null && service.InParty;
            int n = inParty ? Mathf.Max(1, service.MemberCount) : 1;

            countLabel.text = n + " / " + LobbyParty.MaxMembers;
            squadLine.text = n <= 1 ? "SOLO   |   INVITE FRIENDS TO SQUAD UP" : "DEPLOYING AS A SQUAD OF " + n + (service.IsLeader ? "   |   YOU START THE MATCH" : "   |   THE LEADER STARTS THE MATCH");
            squadLine.color = n <= 1 ? Dim : Squad;
            codeText.text = inParty ? service.PartyId : (service != null && service.Busy ? "CREATING YOUR PARTY..." : "NO PARTY");
            copyButton.interactable = inParty;
            leaveButton.gameObject.SetActive(inParty && n > 1);

            const float slotH = 92f, gap = 8f;
            for (int i = 0; i < LobbyParty.MaxMembers; i++)
            {
                float y = 200f - slotH * 0.5f - i * (slotH + gap);
                var m = i < members.Count ? members[i] : null;
                var slot = NewRect("Slot " + i, slotsRoot, new Vector2(560f, slotH), new Vector2(0f, y));
                var g = slot.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; reveals.Add((g, 0.04f + i * 0.05f));
                if (m == null) { BuildEmptySlot(slot); continue; }
                var bg = Img("Bg", slot, m.isLocal ? CardHi : Card); Stretch(bg.rectTransform);
                var acc = Img("Accent", slot, m.isLeader ? Gold : (n > 1 ? Squad : Faint));
                var ar = acc.rectTransform; ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f); ar.pivot = new Vector2(0f, 0.5f); ar.sizeDelta = new Vector2(4f, 0f); ar.anchoredPosition = Vector2.zero;
                var tile = Img("Avatar", slot, new Color(1f, 1f, 1f, 0.06f)); Place(tile.rectTransform, new Vector2(-280f + 16f + 32f, 0f), new Vector2(64f, 64f));
                Icon(tile.rectTransform, "UI/Lobby/Avatar_Operator", Vector2.zero, new Vector2(56f, 56f), Color.white);
                var name = Txt(slot, (m.displayName ?? "PLAYER").ToUpperInvariant(), 20, Color.white, TextAlignmentOptions.Left, new Vector2(-280f + 100f + 140f, 14f), new Vector2(280f, 28f));
                name.fontStyle = FontStyles.Bold; name.characterSpacing = 3f; name.overflowMode = TextOverflowModes.Ellipsis;
                Txt(slot, string.IsNullOrEmpty(m.levelText) ? "" : m.levelText.ToUpperInvariant(), 13, Dim, TextAlignmentOptions.Left, new Vector2(-280f + 100f + 140f, -14f), new Vector2(280f, 20f)).characterSpacing = 3f;
                float chipX = 280f - 16f;
                if (m.isLocal) chipX = Chip(slot, "YOU", Color.white, Color.black, chipX);
                if (m.isLeader) chipX = Chip(slot, "LEADER", Gold, Color.black, chipX);
                Chip(slot, m.ready ? "READY" : "NOT READY", m.ready ? new Color(Online.r, Online.g, Online.b, 0.18f) : new Color(1f, 1f, 1f, 0.08f), m.ready ? Online : Dim, chipX);
            }
        }

        private void BuildEmptySlot(RectTransform slot)
        {
            var bg = Img("Empty", slot, new Color(1f, 1f, 1f, 0.025f)); Stretch(bg.rectTransform); bg.raycastTarget = true;
            Outline(slot, new Color(1f, 1f, 1f, 0.12f));
            var plus = Txt(slot, "+", 34, Faint, TextAlignmentOptions.Center, new Vector2(-280f + 48f, 2f), new Vector2(60f, 50f));
            var label = Txt(slot, "INVITE A FRIEND", 16, Faint, TextAlignmentOptions.Left, new Vector2(-280f + 100f + 140f, 8f), new Vector2(280f, 24f));
            label.fontStyle = FontStyles.Bold; label.characterSpacing = 4f;
            Txt(slot, "ONLINE FRIENDS ARE LISTED ON THE RIGHT", 12, new Color(1f, 1f, 1f, 0.22f), TextAlignmentOptions.Left, new Vector2(-280f + 100f + 140f, -16f), new Vector2(280f, 18f)).characterSpacing = 2f;
            var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SetFriendTab(0));
            UIFeedback.Attach(bg.gameObject, 1f).silentClick = true;
            var hover = bg.gameObject.AddComponent<Hover>();
            hover.changed = on => { if (bg != null) { bg.color = on ? new Color(1f, 1f, 1f, 0.06f) : new Color(1f, 1f, 1f, 0.025f); plus.color = on ? Color.white : Faint; label.color = on ? Color.white : Faint; } };
        }

        private float Chip(RectTransform slot, string text, Color bg, Color fg, float right)
        {
            float w = Mathf.Max(56f, text.Length * 9.5f + 18f);
            var c = Img("Chip", slot, bg);
            var r = c.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(1f, 0.5f); r.anchoredPosition = new Vector2(right, 0f); r.sizeDelta = new Vector2(w, 24f);
            var t = Txt(r, text, 11, fg, TextAlignmentOptions.Center, Vector2.zero, new Vector2(w, 22f)); t.fontStyle = FontStyles.Bold; t.characterSpacing = 3f;
            return right - w - 6f;
        }

        private void RebuildFriends()
        {
            if (friendsList == null) return;
            for (int i = friendsList.childCount - 1; i >= 0; i--) Destroy(friendsList.GetChild(i).gameObject);
            var all = FriendsService.All.Where(f => f.relation != FriendsService.Relation.Blocked).ToList();
            int online = FriendsService.OnlineCount, requests = FriendsService.InvitesReceived;
            tabLabel[0].text = "ONLINE  " + online;
            tabLabel[1].text = "ALL  " + all.Count(f => f.relation == FriendsService.Relation.Friend);
            tabLabel[2].text = requests > 0 ? "REQUESTS  " + requests : "REQUESTS";
            for (int i = 0; i < 3; i++) { tabBg[i].color = i == friendTab ? Color.white : CardHi; tabLabel[i].color = i == friendTab ? Color.black : (i == 2 && requests > 0 ? Gold : Dim); }
            friendsHeader.text = FriendsService.Busy && all.Count == 0 ? "LOADING" : online + " ONLINE";

            IEnumerable<FriendsService.Friend> shown;
            if (friendTab == 0) shown = all.Where(f => f.relation == FriendsService.Relation.Friend && f.online);
            else if (friendTab == 1) shown = all.Where(f => f.relation == FriendsService.Relation.Friend).OrderByDescending(f => f.online).ThenBy(f => f.Name);
            else shown = all.Where(f => f.relation == FriendsService.Relation.InviteReceived || f.relation == FriendsService.Relation.InviteSent).OrderBy(f => f.relation);
            var list = shown.ToList();

            if (!string.IsNullOrEmpty(FriendsService.Notice) && notice.Length == 0) SetNotice(FriendsService.Notice);
            if (list.Count == 0)
            {
                string empty = AuthenticationService.Instance == null || !AuthenticationService.Instance.IsAuthenticated ? "SIGN IN TO SEE YOUR FRIENDS"
                    : friendTab == 0 ? (all.Count == 0 ? "NO FRIENDS YET\nADD ONE WITH THEIR USERNAME ABOVE" : "NONE OF YOUR FRIENDS IS ONLINE")
                    : friendTab == 1 ? "NO FRIENDS YET\nADD ONE WITH THEIR USERNAME ABOVE" : "NO FRIEND REQUESTS";
                var t = Txt(friendsList, empty, 15, Faint, TextAlignmentOptions.Center, new Vector2(0f, -70f), new Vector2(560f, 60f));
                t.fontStyle = FontStyles.Bold; t.characterSpacing = 3f; t.enableWordWrapping = true;
                var tr = t.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
                friendsList.sizeDelta = new Vector2(0f, 140f);
                return;
            }
            const float rowH = 64f, gap = 6f;
            var partyIds = service != null && service.InParty ? new HashSet<string>(service.MemberUserIds) : new HashSet<string>();
            bool partyFull = service != null && service.InParty && service.MemberCount >= LobbyParty.MaxMembers;
            for (int i = 0; i < list.Count; i++)
                BuildFriendRow(list[i], -rowH * 0.5f - i * (rowH + gap), rowH, i, partyIds.Contains(list[i].userId), partyFull);
            friendsList.sizeDelta = new Vector2(0f, list.Count * (rowH + gap));
        }

        private void BuildFriendRow(FriendsService.Friend f, float y, float h, int index, bool inMyParty, bool partyFull)
        {
            var row = NewRect("Friend " + f.Name, friendsList, new Vector2(620f, h), new Vector2(0f, y));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            var g = row.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; reveals.Add((g, 0.03f + Mathf.Min(index, 10) * 0.03f));
            var bg = Img("Bg", row, Card); Stretch(bg.rectTransform); bg.raycastTarget = true;
            bool isFriend = f.relation == FriendsService.Relation.Friend;
            bool on = isFriend && f.online;
            var dot = Img("Dot", row, on ? Online : new Color(1f, 1f, 1f, 0.22f)); Place(dot.rectTransform, new Vector2(-310f + 22f, 0f), new Vector2(10f, 10f));
            var nm = Txt(row, f.Name.ToUpperInvariant(), 18, on || !isFriend ? Color.white : Dim, TextAlignmentOptions.Left, new Vector2(-310f + 44f + 130f, 10f), new Vector2(260f, 26f));
            nm.fontStyle = FontStyles.Bold; nm.characterSpacing = 3f; nm.overflowMode = TextOverflowModes.Ellipsis;
            string state = inMyParty ? "IN YOUR PARTY" : isFriend ? (on ? "ONLINE  |  IN THE LOBBY" : "OFFLINE")
                         : f.relation == FriendsService.Relation.InviteReceived ? "WANTS TO BE YOUR FRIEND" : "REQUEST SENT";
            var st = Txt(row, state, 12, inMyParty ? Squad : on ? Online : Faint, TextAlignmentOptions.Left, new Vector2(-310f + 44f + 130f, -14f), new Vector2(260f, 18f));
            st.fontStyle = FontStyles.Bold; st.characterSpacing = 3f;

            string id = f.userId;
            float right = 310f - 12f;
            if (f.relation == FriendsService.Relation.InviteReceived)
            {
                MakeButton(row, "DECLINE", new Vector2(right - 50f, 0f), new Vector2(100f, 36f), false, () => { _ = FriendsService.RemoveAsync(id); }, 12f, UISfx.Kind.Back);
                MakeButton(row, "ACCEPT", new Vector2(right - 160f, 0f), new Vector2(110f, 36f), true, () => { _ = FriendsService.AcceptAsync(id); }, 12f, UISfx.Kind.Confirm);
                return;
            }
            if (f.relation == FriendsService.Relation.InviteSent)
            {
                MakeButton(row, "CANCEL", new Vector2(right - 50f, 0f), new Vector2(100f, 36f), false, () => { _ = FriendsService.RemoveAsync(id); }, 12f, UISfx.Kind.Back);
                return;
            }
            // a friend: INVITE (online, not in the party yet, room left) and REMOVE on hover
            bool invited = invitedAt.TryGetValue(id, out float at) && Time.unscaledTime - at < 30f;
            if (on && !inMyParty)
            {
                bool can = !partyFull && !invited && service != null && !service.Unsupported;
                var inv = MakeButton(row, invited ? "INVITED" : partyFull ? "PARTY FULL" : "INVITE", new Vector2(right - 60f, 0f), new Vector2(120f, 36f), can, () => Invite(id, f.Name), 12f, UISfx.Kind.Confirm);
                inv.interactable = can;
                if (!can) { inv.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f); inv.GetComponentInChildren<TextMeshProUGUI>().color = invited ? Squad : Dim; }
                right -= 128f;
            }
            var remove = MakeButton(row, "REMOVE", new Vector2(right - 45f, 0f), new Vector2(90f, 30f), false, () => { _ = FriendsService.RemoveAsync(id); }, 11f, UISfx.Kind.Back);
            remove.gameObject.SetActive(false);
            var hover = bg.gameObject.AddComponent<Hover>();
            hover.changed = over => { if (bg != null) { bg.color = over ? CardHi : Card; if (remove != null) remove.gameObject.SetActive(over); } };
            remove.gameObject.AddComponent<Hover>().changed = over => { if (over && remove != null) remove.gameObject.SetActive(true); };
        }

        // ================================================================= actions
        private async void Invite(string userId, string name)
        {
            if (service == null) return;
            if (!service.InParty) { await service.CreateAsync(); if (this == null) return; }
            if (!service.InParty) { SetNotice("COULD NOT CREATE YOUR PARTY - TRY AGAIN"); UISfx.Play(UISfx.Kind.Error); return; }
            invitedAt[userId] = Time.unscaledTime;
            RebuildFriends();
            string status = await SocialService.Instance.InviteToPartyAsync(userId, service.PartyId);
            if (this == null) return;
            switch (status)
            {
                case "ok": SetNotice("INVITE SENT TO " + name.ToUpperInvariant()); break;
                case "rate_limited": invitedAt.Remove(userId); SetNotice("TOO MANY INVITES - WAIT A MOMENT"); UISfx.Play(UISfx.Kind.Error); break;
                case "not_friends": invitedAt.Remove(userId); SetNotice("ONLY FRIENDS CAN BE INVITED"); UISfx.Play(UISfx.Kind.Error); break;
                default: invitedAt.Remove(userId); SetNotice("THE INVITE WAS NOT SENT - TRY AGAIN"); UISfx.Play(UISfx.Kind.Error); break;
            }
            RebuildFriends();
        }

        private void OnCopy() { if (service != null) { service.CopyCode(); SetNotice("PARTY CODE COPIED"); } }

        private void OnJoinCode()
        {
            if (service == null || codeInput == null) return;
            string code = codeInput.text.Trim();
            codeInput.text = "";
            if (code.Length == 0) { UISfx.Play(UISfx.Kind.Error); SetNotice("PASTE A PARTY CODE FIRST"); return; }
            _ = service.JoinAsync(code);
        }

        private void OnLeave() { if (service != null) _ = service.LeaveAsync(); }

        private void OnAddFriend()
        {
            if (friendInput == null) return;
            string name = friendInput.text.Trim();
            friendInput.text = "";
            if (name.Length == 0) { UISfx.Play(UISfx.Kind.Error); SetNotice("TYPE A USERNAME FIRST"); return; }
            _ = FriendsService.AddAsync(name);
            SetNotice("FRIEND REQUEST SENT TO " + name.ToUpperInvariant());
        }

        /// <summary>Test aid: invites the first online friend who is not in the party.</summary>
        public void TestInviteFirstOnline()
        {
            var ids = service != null && service.InParty ? new HashSet<string>(service.MemberUserIds) : new HashSet<string>();
            var f = FriendsService.All.FirstOrDefault(x => x.relation == FriendsService.Relation.Friend && x.online && !ids.Contains(x.userId));
            if (!string.IsNullOrEmpty(f.userId)) Invite(f.userId, f.Name);
            else Debug.Log("[SocialPanel] No online friend to invite.");
        }

        // ================================================================= kit
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("[Lobby] EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private TMP_InputField BuildInput(RectTransform parent, string placeholder, Vector2 pos, Vector2 size, Action submit)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; Place(r, pos, size);
            var bg = go.GetComponent<Image>(); bg.sprite = px; bg.color = new Color(1f, 1f, 1f, 0.07f);
            var area = NewRect("Text Area", r, Vector2.zero, Vector2.zero);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(16, 6); area.offsetMax = new Vector2(-16, -6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Txt(area, placeholder, 13, Faint, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero); Stretch(ph.rectTransform);
            ph.fontStyle = FontStyles.Bold; ph.characterSpacing = 3f;
            var text = Txt(area, "", 16, Color.white, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero); Stretch(text.rectTransform);
            text.richText = false;
            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = ph;
            input.fontAsset = font; input.pointSize = 16; input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 64; input.richText = false;
            input.caretColor = Color.white; input.customCaretColor = true; input.selectionColor = new Color(1f, 1f, 1f, 0.3f);
            input.targetGraphic = bg;
            input.onSubmit.AddListener(_ => submit());
            input.onSelect.AddListener(_ => bg.color = new Color(1f, 1f, 1f, 0.12f));
            input.onDeselect.AddListener(_ => bg.color = new Color(1f, 1f, 1f, 0.07f));
            go.SetActive(true);
            return input;
        }

        private Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, bool primary, Action onClick, float fontSize, UISfx.Kind sound = UISfx.Kind.Click)
        {
            var bg = Img(label + " Button", parent, primary ? Color.white : new Color(1f, 1f, 1f, 0.12f));
            Place(bg.rectTransform, pos, size);
            bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = primary ? new Color(0.84f, 0.84f, 0.84f, 1f) : new Color(1.7f, 1.7f, 1.7f, 1f);
            c.pressedColor = primary ? new Color(0.62f, 0.62f, 0.62f, 1f) : new Color(2.3f, 2.3f, 2.3f, 1f);
            c.disabledColor = Color.white; c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(() => onClick());
            UIFeedback.Attach(bg.gameObject, 1.03f, sound);
            var t = Txt(bg.rectTransform, label, fontSize, primary ? Color.black : Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            t.fontStyle = FontStyles.Bold; t.characterSpacing = 4f; Stretch(t.rectTransform);
            return btn;
        }

        private Image Img(string name, RectTransform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        private TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.enableWordWrapping = false;
            Place(t.rectTransform, pos, box);
            return t;
        }

        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private Image Icon(RectTransform parent, string path, Vector2 pos, Vector2 size, Color color)
        {
            var i = Img("Icon", parent, color);
            if (!sprites.TryGetValue(path, out var s) || s == null) { s = Resources.Load<Sprite>(path); sprites[path] = s; }
            i.sprite = s; i.preserveAspect = true; i.enabled = s != null;
            Place(i.rectTransform, pos, size);
            return i;
        }

        private void Outline(RectTransform r, Color c)
        {
            for (int k = 0; k < 4; k++)
            {
                var o = Img("Outline", r, c).rectTransform;
                bool horiz = k < 2;
                o.anchorMin = horiz ? new Vector2(0f, k == 0 ? 1f : 0f) : new Vector2(k == 2 ? 0f : 1f, 0f);
                o.anchorMax = horiz ? new Vector2(1f, k == 0 ? 1f : 0f) : new Vector2(k == 2 ? 0f : 1f, 1f);
                o.sizeDelta = horiz ? new Vector2(0f, 1f) : new Vector2(1f, 0f); o.anchoredPosition = Vector2.zero;
            }
        }

        private static void TopBar(RectTransform r, float h) { r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(0f, h); }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; Place(r, pos, size);
            return r;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        private sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<bool> changed;
            public void OnPointerEnter(PointerEventData e) { changed?.Invoke(true); }
            public void OnPointerExit(PointerEventData e) { changed?.Invoke(false); }
        }
    }
}
