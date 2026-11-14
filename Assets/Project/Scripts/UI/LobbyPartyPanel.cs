using System.Linq;
using Railbound.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Railbound.UI
{
    /// <summary>
    /// The party window of the lobby (opened by the INVITE button / F1): it shows the party code with a COPY button, a field to join
    /// a friend's party with their code, a LEAVE PARTY button and the state of the party. It talks to <see cref="NakamaPartyService"/>
    /// and is built at runtime in the black-and-white style of the lobby.
    /// </summary>
    public class LobbyPartyPanel : MonoBehaviour
    {
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.3f);

        public bool IsOpen => root != null && root.gameObject.activeSelf;
        /// <summary>True while the player types in the code field (the lobby's keyboard shortcuts must not fire then).</summary>
        public bool IsTyping => IsOpen && ((codeInput != null && codeInput.isFocused) || (friendInput != null && friendInput.isFocused));

        private RectTransform root;
        private TMP_FontAsset font;
        private Sprite px;
        private TextMeshProUGUI statusText, codeText, membersText;
        private TMP_InputField codeInput;
        private Button copyButton, joinButton, leaveButton, closeButton;
        private NakamaPartyService service;
        private RectTransform partyView, friendsView, friendsList;
        private TMP_InputField friendInput;
        private TextMeshProUGUI friendsStatus;
        private Image tabPartyBg, tabFriendsBg;
        private TextMeshProUGUI tabPartyLabel, tabFriendsLabel;
        private int tab;
        private string notice = "";
        private float noticeUntil;

        public static LobbyPartyPanel Create(Transform parent, TMP_FontAsset fontAsset)
        {
            // a root of its own: as a nested canvas its dim only covered 100x100 units, so a click outside did not close the window
            var go = new GameObject("[Lobby] Party Panel");
            var panel = go.AddComponent<LobbyPartyPanel>();
            panel.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            panel.Build();
            return panel;
        }

        // ---------------- open / close ----------------
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        public void Show()
        {
            if (root == null) return;
            root.gameObject.SetActive(true);

            service = NakamaPartyService.Instance;
            if (service != null)
            {
                service.Changed -= Refresh; service.Changed += Refresh;
                service.Notice -= OnNotice; service.Notice += OnNotice;
            }

            bool signedIn = AuthenticationService.Instance != null && AuthenticationService.Instance.IsAuthenticated;
            if (!signedIn) SetNotice("SIGN IN TO USE PARTIES");
            else if (service != null && service.Unsupported) SetNotice("PARTIES ARE NOT AVAILABLE ON THIS SERVER YET");
            else if (service != null && !service.InParty) _ = service.CreateAsync();   // opening the window creates your party, ready to share

            EnsureEventSystem();
            FriendsService.Changed -= RefreshFriends; FriendsService.Changed += RefreshFriends;
            _ = FriendsService.RefreshAsync();
            SetTab(0);
            Refresh();
        }

        public void Hide()
        {
            if (root == null) return;
            if (service != null) { service.Changed -= Refresh; service.Notice -= OnNotice; }
            FriendsService.Changed -= RefreshFriends;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (service != null) { service.Changed -= Refresh; service.Notice -= OnNotice; }
            FriendsService.Changed -= RefreshFriends;
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (notice.Length > 0 && Time.unscaledTime > noticeUntil) { notice = ""; Refresh(); }
        }

        // ---------------- state ----------------
        private void OnNotice(string line) { SetNotice(line); }

        private void SetNotice(string line)
        {
            notice = line ?? "";
            noticeUntil = Time.unscaledTime + 5f;
            Refresh();
        }

        private void Refresh()
        {
            if (!IsOpen || service == null) return;

            bool inParty = service.InParty;
            bool alone = service.MemberCount <= 1;
            bool unsupported = service.Unsupported;

            codeText.text = inParty ? service.PartyId : (unsupported ? "NOT AVAILABLE YET" : (service.Busy ? "WORKING..." : "NO PARTY"));
            copyButton.interactable = inParty;
            leaveButton.gameObject.SetActive(inParty);
            joinButton.interactable = !service.Busy && !unsupported;

            // CLOSE sits in the middle while LEAVE PARTY is hidden
            if (closeButton != null) closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(inParty && tab == 0 ? 190f : 0f, -208f);

            if (inParty)
                membersText.text = service.MemberCount + " / " + LobbyParty.MaxMembers + " PLAYERS   |   " + (service.IsLeader ? "YOU ARE THE PARTY LEADER" : "THE LEADER STARTS THE MATCH");
            else
                membersText.text = unsupported ? "THE SERVER MUST BE UPDATED BEFORE FRIENDS CAN PARTY UP" : "NOT IN A PARTY";

            if (notice.Length > 0) statusText.text = notice;
            else statusText.text = inParty && alone ? "SEND YOUR CODE TO A FRIEND, OR PASTE THEIR CODE BELOW" : (inParty ? "YOUR SQUAD IS SHOWN IN THE LOBBY" : "");
        }

        // ---------------- buttons ----------------
        private void OnCopy() { if (service != null) service.CopyCode(); }

        private void OnJoin()
        {
            if (service == null || codeInput == null) return;
            string code = codeInput.text;
            codeInput.text = "";
            _ = service.JoinAsync(code);
        }

        private void OnLeave() { if (service != null) _ = service.LeaveAsync(); }

        // ---------------- build ----------------
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("[Lobby] EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private void Build()
        {
            px = MakePixel();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 880;
            var sc = canvasGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGo.transform;

            // dim: a click outside the window closes it
            var dim = NewImage("Dim", root, new Color(0f, 0f, 0f, 0.7f)); Stretch(dim.rectTransform); dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>(); dimBtn.transition = Selectable.Transition.None; dimBtn.onClick.AddListener(Hide);

            var panel = NewRect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(820, 560), Vector2.zero);
            var bg = NewImage("Bg", panel, new Color(0.04f, 0.045f, 0.055f, 0.97f)); Stretch(bg.rectTransform); bg.raycastTarget = true;   // clicks on the window do not close it
            var edge = NewImage("Edge", panel, new Color(1f, 1f, 1f, 0.35f)); var er = edge.rectTransform;
            er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f); er.anchoredPosition = Vector2.zero; er.sizeDelta = new Vector2(0f, 3f);

            partyView = NewRect("PartyView", panel, new Vector2(0.5f, 0.5f), new Vector2(820, 560), Vector2.zero);
            friendsView = NewRect("FriendsView", panel, new Vector2(0.5f, 0.5f), new Vector2(820, 560), Vector2.zero);
            BuildTabs(panel);
            BuildFriends(friendsView);
            statusText = Label(partyView, "", 18, false, Dim, new Vector2(0, 170), new Vector2(740, 28));

            // left-aligned labels: the box starts at the panel's inner left edge (-370) and extends to the right
            Label(partyView, "YOUR PARTY CODE", 14, false, Dim, new Vector2(-220, 118), new Vector2(300, 22), TextAlignmentOptions.Left);
            var codeBox = NewImage("CodeBox", partyView, new Color(0f, 0f, 0f, 0.55f)); var cbr = codeBox.rectTransform;
            Place(cbr, new Vector2(-90, 66), new Vector2(560, 56));
            codeText = Label(cbr, "", 17, false, Color.white, Vector2.zero, new Vector2(540, 40));
            copyButton = MakeButton(partyView, "COPY", new Vector2(290, 66), new Vector2(200, 56), true, OnCopy);

            membersText = Label(partyView, "", 16, false, Dim, new Vector2(0, 10), new Vector2(760, 26));
            var rule = NewImage("Rule", partyView, Faint); Place(rule.rectTransform, new Vector2(0, -30), new Vector2(740, 2));

            Label(partyView, "JOIN A FRIEND'S PARTY", 14, false, Dim, new Vector2(-220, -66), new Vector2(300, 22), TextAlignmentOptions.Left);
            codeInput = BuildInput(partyView, "PASTE THE PARTY CODE", new Vector2(-90, -118), new Vector2(560, 56));
            joinButton = MakeButton(partyView, "JOIN", new Vector2(290, -118), new Vector2(200, 56), false, OnJoin);

            leaveButton = MakeButton(partyView, "LEAVE PARTY", new Vector2(-190, -208), new Vector2(260, 52), false, OnLeave);
            closeButton = MakeButton(panel, "CLOSE", new Vector2(190, -208), new Vector2(260, 52), false, Hide);
            Label(panel, "ESC   CLOSE", 14, false, Faint, new Vector2(0, -262), new Vector2(400, 22));

            root.gameObject.SetActive(false);
        }

        private TMP_InputField BuildInput(RectTransform parent, string placeholder, Vector2 pos, Vector2 size)
        {
            var bg = NewImage("CodeInput", parent, new Color(1f, 1f, 1f, 0.1f));
            Place(bg.rectTransform, pos, size);
            bg.raycastTarget = true;

            var area = NewRect("Text Area", bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(16, 6); area.offsetMax = new Vector2(-16, -6);
            area.gameObject.AddComponent<RectMask2D>();

            var text = NewText("Text", area, "", 17, Color.white, TextAlignmentOptions.Left); Stretch(text.rectTransform);
            var ph = NewText("Placeholder", area, placeholder, 17, Faint, TextAlignmentOptions.Left); Stretch(ph.rectTransform);
            ph.fontStyle = FontStyles.Italic;

            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.fontAsset = font;
            input.pointSize = 17;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 64;
            input.caretColor = Color.white;
            input.selectionColor = new Color(1f, 1f, 1f, 0.3f);
            input.targetGraphic = bg;
            input.onSubmit.AddListener(_ => OnJoin());
            return input;
        }

        private Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size, bool primary, UnityEngine.Events.UnityAction onClick, float fontSize = 0f)
        {
            var bg = NewImage(label + " Button", parent, primary ? Color.white : new Color(1f, 1f, 1f, 0.14f));
            Place(bg.rectTransform, pos, size);
            bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = primary ? new Color(0.82f, 0.82f, 0.82f, 1f) : new Color(1.6f, 1.6f, 1.6f, 1f);
            c.pressedColor = primary ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(2.2f, 2.2f, 2.2f, 1f);
            c.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            var t = NewText("Label", bg.rectTransform, label, fontSize > 0f ? fontSize : (size.y > 54 ? 22 : 18), primary ? Color.black : Color.white, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold; t.characterSpacing = fontSize > 0f ? 4f : 8f; Stretch(t.rectTransform);
            return btn;
        }

        // ---------------- tabs: PARTY | FRIENDS ----------------
        private void BuildTabs(RectTransform panel)
        {
            tabPartyBg = TabButton(panel, "PARTY", new Vector2(-165f, 222f), () => SetTab(0), out tabPartyLabel);
            tabFriendsBg = TabButton(panel, "FRIENDS", new Vector2(165f, 222f), () => SetTab(1), out tabFriendsLabel);
        }

        private Image TabButton(RectTransform panel, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI text)
        {
            var bg = NewImage(label + " Tab", panel, new Color(1f, 1f, 1f, 0.14f));
            Place(bg.rectTransform, pos, new Vector2(320f, 60f));
            bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            text = Label(bg.rectTransform, label, 30, true, Color.white, Vector2.zero, new Vector2(300f, 44f));
            return bg;
        }

        private void SetTab(int t)
        {
            tab = t;
            if (partyView != null) partyView.gameObject.SetActive(t == 0);
            if (friendsView != null) friendsView.gameObject.SetActive(t == 1);
            if (tabPartyBg != null) { tabPartyBg.color = t == 0 ? Color.white : new Color(1f, 1f, 1f, 0.14f); tabPartyLabel.color = t == 0 ? Color.black : Color.white; }
            if (tabFriendsBg != null) { tabFriendsBg.color = t == 1 ? Color.white : new Color(1f, 1f, 1f, 0.14f); tabFriendsLabel.color = t == 1 ? Color.black : Color.white; }
            if (closeButton != null) closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(t == 0 && service != null && service.InParty ? 190f : 0f, -208f);
            if (t == 1) { _ = FriendsService.RefreshAsync(); RefreshFriends(); }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ---------------- friends view ----------------
        private void BuildFriends(RectTransform view)
        {
            friendsStatus = Label(view, "", 18, false, Dim, new Vector2(0, 168), new Vector2(740, 28));
            friendInput = BuildInput(view, "ADD A FRIEND BY USERNAME", new Vector2(-90, 114), new Vector2(560, 56));
            friendInput.characterLimit = 20;
            friendInput.onSubmit.RemoveAllListeners();
            friendInput.onSubmit.AddListener(_ => OnAddFriend());
            MakeButton(view, "ADD", new Vector2(290, 114), new Vector2(200, 56), true, OnAddFriend);
            friendsList = NewRect("List", view, new Vector2(0.5f, 0.5f), new Vector2(740, 270), new Vector2(0, -50));
        }

        private void OnAddFriend()
        {
            if (friendInput == null) return;
            string name = friendInput.text;
            friendInput.text = "";
            _ = FriendsService.AddAsync(name);
        }

        private void RefreshFriends()
        {
            if (friendsList == null || !IsOpen) return;
            for (int i = friendsList.childCount - 1; i >= 0; i--) Destroy(friendsList.GetChild(i).gameObject);

            var all = FriendsService.All.Where(f => f.relation != FriendsService.Relation.Blocked).ToList();
            int online = FriendsService.OnlineCount, invites = FriendsService.InvitesReceived;
            string n = FriendsService.Notice;
            if (!string.IsNullOrEmpty(n)) friendsStatus.text = n;
            else if (FriendsService.Busy && all.Count == 0) friendsStatus.text = "LOADING...";
            else if (all.Count == 0) friendsStatus.text = "NO FRIENDS YET - ADD ONE WITH THEIR USERNAME";
            else friendsStatus.text = online + " ONLINE" + (invites > 0 ? "   |   " + invites + " NEW REQUEST" + (invites > 1 ? "S" : "") : "");

            const int maxRows = 5; const float rowH = 46f, gap = 6f;
            for (int i = 0; i < all.Count && i < maxRows; i++)
            {
                var f = all[i];
                float y = 135f - rowH * 0.5f - i * (rowH + gap);
                var row = NewRect("Row", friendsList, new Vector2(0.5f, 0.5f), new Vector2(740f, rowH), new Vector2(0f, y));
                var bg = NewImage("Bg", row, new Color(1f, 1f, 1f, 0.07f)); Stretch(bg.rectTransform);

                bool isOnline = f.relation == FriendsService.Relation.Friend && f.online;
                var dot = NewImage("Dot", row, isOnline ? new Color(0.45f, 0.95f, 0.5f, 1f) : new Color(1f, 1f, 1f, 0.25f));
                Place(dot.rectTransform, new Vector2(-345f, 0f), new Vector2(12f, 12f));
                var nm = Label(row, f.Name.ToUpperInvariant(), 18, true, f.relation == FriendsService.Relation.Friend ? Color.white : Dim, new Vector2(-320f + 150f, 0f), new Vector2(300f, 30f), TextAlignmentOptions.Left);
                nm.characterSpacing = 4f;

                string state = f.relation == FriendsService.Relation.Friend ? (isOnline ? "ONLINE" : "OFFLINE")
                             : (f.relation == FriendsService.Relation.InviteReceived ? "WANTS TO BE FRIENDS" : "REQUEST SENT");
                Label(row, state, 13, false, isOnline ? new Color(0.62f, 0.9f, 0.58f, 1f) : Faint, new Vector2(-30f, 0f), new Vector2(240f, 22f), TextAlignmentOptions.Right);

                string id = f.userId;
                if (f.relation == FriendsService.Relation.InviteReceived)
                {
                    MakeButton(row, "ACCEPT", new Vector2(190f, 0f), new Vector2(110f, 34f), true, () => { _ = FriendsService.AcceptAsync(id); }, 14f);
                    MakeButton(row, "DECLINE", new Vector2(310f, 0f), new Vector2(110f, 34f), false, () => { _ = FriendsService.RemoveAsync(id); }, 14f);
                }
                else
                {
                    MakeButton(row, f.relation == FriendsService.Relation.Friend ? "REMOVE" : "CANCEL", new Vector2(310f, 0f), new Vector2(110f, 34f), false, () => { _ = FriendsService.RemoveAsync(id); }, 14f);
                }
            }
            if (all.Count > maxRows) Label(friendsList, "+ " + (all.Count - maxRows) + " MORE", 13, false, Faint, new Vector2(0f, -128f), new Vector2(300f, 20f));
        }

        // ---------------- helpers ----------------
        private TextMeshProUGUI Label(RectTransform parent, string text, float size, bool bold, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = NewText("Label", parent, text, size, color, align);
            if (bold) t.fontStyle = FontStyles.Bold;
            t.characterSpacing = bold ? 12f : 4f;
            Place(t.rectTransform, pos, box);
            return t;
        }

        private TextMeshProUGUI NewText(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private Image NewImage(string name, RectTransform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = px; i.color = c; i.raycastTarget = false;
            return i;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            Place(r, pos, size);
            r.anchorMin = r.anchorMax = anchor;
            return r;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        private static Sprite MakePixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[16]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
