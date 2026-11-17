using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Nakama;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Railbound.Social
{
    // ---------------------------------------------------------------- data the UI shows
    public sealed class LeaderboardEntry
    {
        public int rank;
        public string userId = "", username = "";
        public long score;
        public int level;
        public bool isMe;
    }

    public sealed class LeaderboardBoard
    {
        public string board = "", title = "";
        public bool weekly;
        public List<int> payouts = new List<int>();
    }

    public sealed class LeaderboardPage
    {
        public string status = "", board = "", title = "", scope = "";
        public List<LeaderboardEntry> records = new List<LeaderboardEntry>();
        public LeaderboardEntry me;
        public long resetsAt;               // unix seconds, 0 = never
        public List<int> payouts = new List<int>();
    }

    public sealed class InboxItem
    {
        public string id = "", subject = "", senderId = "";
        public int code;
        public JObject content = new JObject();
        public DateTime createTime;          // UTC
        public bool claimed, expired;        // gifts only
        public bool persistent = true;

        public bool IsGift => code == SocialService.NoteGift;
        public string GiftId => (string)content["giftId"] ?? "";
        public int Int(string key, int fallback = 0) { var t = content[key]; return t != null && t.Type != JTokenType.Null ? (int)t : fallback; }
        public string Str(string key) { var t = content[key]; return t != null && t.Type != JTokenType.Null ? t.ToString() : ""; }
    }

    public sealed class ChatLine
    {
        public string id = "", room = "", channelId = "", senderId = "", username = "", text = "", kind = "";
        public DateTime time;                // UTC
        public bool system, filtered, isMe, mentionsMe, reported;
    }

    /// <summary>
    /// The game's side of the Nakama social features (module section "Social platform"): leaderboards, the notification inbox with
    /// gifts, live notifications, and the chat rooms. Everything the server decides stays on the server: this class asks, shows and
    /// reports what the server answered. Socket events arrive on a background thread and are handed to the main thread here, so every
    /// event of this class is raised on the main thread.
    /// </summary>
    public sealed class SocialService : MonoBehaviour
    {
        // notification codes of the module (Nakama's own are negative: -2 friend request ...)
        public const int NoteMatchResult = 100, NoteLevelUp = 101, NoteWeaponLevelUp = 102, NoteTierReached = 103,
                         NoteLeaderboardReward = 110, NoteGift = 120, NoteAnnouncement = 130, NoteChatMuted = 140, NoteChatMention = 141,
                         NoteFriendRequest = -2, NoteFriendAccepted = -3, NotePartyInvite = 150;
        public static readonly string[] Rooms = { "global", "lfg", "help" };
        public const int ChatMaxLength = 200;
        private const int MaxLinesPerRoom = 120;

        private static SocialService instance;
        public static SocialService Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[Social] Service");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SocialService>();
                }
                return instance;
            }
        }
        public static SocialService InstanceIfExists => instance;

        // ---------------------------------------------------------------- events (main thread)
        public event Action InboxChanged;
        public event Action<InboxItem> LiveNotification;
        public event Action<string> ChatChanged;          // room
        public event Action ChatStateChanged;             // connected / muted / joined

        // ---------------------------------------------------------------- state
        public bool Online => Auth != null && Auth.IsAuthenticated && Auth.Client != null && Auth.Session != null;
        public string MyUserId => Online ? Auth.Session.UserId : "";
        public string MyUsername => Online ? Auth.Session.Username ?? "" : "";

        public readonly List<InboxItem> Inbox = new List<InboxItem>();
        public bool InboxLoaded { get; private set; }
        public bool InboxLoading { get; private set; }
        public int UnclaimedGifts { get; private set; }

        public bool ChatConnected => boundSocket != null && boundSocket.IsConnected && channels.Count > 0;
        public bool ChatJoining { get; private set; }
        public DateTime MutedUntil { get; private set; }   // UTC; MinValue = not muted
        public bool Muted => MutedUntil > DateTime.UtcNow;

        private static AuthenticationService Auth => AuthenticationService.Instance;
        private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        private ISocket boundSocket;
        private readonly Dictionary<string, IChannel> channels = new Dictionary<string, IChannel>();        // room -> channel
        private readonly Dictionary<string, string> roomOfChannel = new Dictionary<string, string>();       // channel id -> room
        private readonly Dictionary<string, List<ChatLine>> lines = new Dictionary<string, List<ChatLine>>();
        private readonly Dictionary<string, int> unread = new Dictionary<string, int>();
        private float nextSocketCheck;
        private bool wantChat;

        // ================================================================= helpers
        private void Update()
        {
            while (mainThread.TryDequeue(out var a))
            {
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
            if (Time.unscaledTime >= nextSocketCheck)
            {
                nextSocketCheck = Time.unscaledTime + 1f;
                // the auth service makes a new socket after a reconnect: bind to it and join the rooms again
                var s = Online ? Auth.Socket : null;
                if (s != boundSocket) Bind(s);
                if (wantChat && boundSocket != null && boundSocket.IsConnected && channels.Count == 0 && !ChatJoining) _ = JoinRoomsAsync();
            }
        }

        private void OnDestroy()
        {
            Bind(null);
            if (instance == this) instance = null;
        }

        private void Bind(ISocket socket)
        {
            if (boundSocket != null)
            {
                boundSocket.ReceivedNotification -= OnSocketNotification;
                boundSocket.ReceivedChannelMessage -= OnSocketChannelMessage;
            }
            boundSocket = socket;
            channels.Clear(); roomOfChannel.Clear();
            if (boundSocket != null)
            {
                boundSocket.ReceivedNotification += OnSocketNotification;
                boundSocket.ReceivedChannelMessage += OnSocketChannelMessage;
            }
            mainThread.Enqueue(() => ChatStateChanged?.Invoke());
        }

        private async Task<JObject> RpcAsync(string id, object payload)
        {
            if (!Online) return null;
            try
            {
                var r = await Auth.Client.RpcAsync(Auth.Session, id, JsonConvert.SerializeObject(payload ?? new { }));
                return string.IsNullOrEmpty(r.Payload) ? new JObject() : JObject.Parse(r.Payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Social] " + id + " failed: " + e.Message);
                return null;
            }
        }

        private static DateTime ParseTime(string s)
        {
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : DateTime.UtcNow;
        }

        private static JObject ParseContent(string json)
        {
            if (string.IsNullOrEmpty(json)) return new JObject();
            try { return JObject.Parse(json); } catch (Exception) { return new JObject(); }
        }

        // ================================================================= leaderboards
        private List<LeaderboardBoard> catalog;

        public async Task<List<LeaderboardBoard>> GetBoardsAsync()
        {
            if (catalog != null) return catalog;
            var r = await RpcAsync("railbound_leaderboard", new { });
            if (r == null || (string)r["status"] != "ok") return null;
            catalog = r["boards"]?.ToObject<List<LeaderboardBoard>>() ?? new List<LeaderboardBoard>();
            return catalog;
        }

        /// <summary>One view of a board: scope "top", "around_me" or "friends". Null when offline.</summary>
        public async Task<LeaderboardPage> GetLeaderboardAsync(string board, string scope, int limit)
        {
            var r = await RpcAsync("railbound_leaderboard", new { board, scope, limit });
            return r?.ToObject<LeaderboardPage>();
        }

        // ================================================================= inbox
        public async Task RefreshInboxAsync()
        {
            if (!Online || InboxLoading) return;
            InboxLoading = true;
            InboxChanged?.Invoke();
            var r = await RpcAsync("railbound_inbox", new { limit = 100 });
            InboxLoading = false;
            if (r != null && (string)r["status"] == "ok")
            {
                Inbox.Clear();
                foreach (var n in r["notifications"] ?? new JArray())
                {
                    Inbox.Add(new InboxItem
                    {
                        id = (string)n["id"] ?? "", code = (int?)n["code"] ?? 0, subject = (string)n["subject"] ?? "", senderId = (string)n["senderId"] ?? "",
                        content = n["content"] as JObject ?? new JObject(), createTime = ParseTime((string)n["createTime"]),
                        claimed = (bool?)n["claimed"] ?? false, expired = (bool?)n["expired"] ?? false
                    });
                }
                Inbox.Sort((a, b) => b.createTime.CompareTo(a.createTime));
                UnclaimedGifts = (int?)r["unclaimedGifts"] ?? 0;
                InboxLoaded = true;
            }
            InboxChanged?.Invoke();
        }

        private string SeenKey => "social.inbox.seen." + MyUserId;

        /// <summary>Notifications newer than the last time the inbox was opened, plus gifts still waiting.</summary>
        public int UnreadCount
        {
            get
            {
                if (!InboxLoaded) return 0;
                long seen = 0;
                long.TryParse(PlayerPrefs.GetString(SeenKey, "0"), out seen);
                int n = 0;
                foreach (var i in Inbox)
                {
                    bool waitingGift = i.IsGift && !i.claimed && !i.expired;
                    if (waitingGift || i.createTime.Ticks > seen) n++;
                }
                return n;
            }
        }

        public void MarkInboxSeen()
        {
            if (!Online) return;
            long newest = 0;
            foreach (var i in Inbox) newest = Math.Max(newest, i.createTime.Ticks);
            PlayerPrefs.SetString(SeenKey, newest.ToString());
            InboxChanged?.Invoke();
        }

        /// <summary>Claims a gift on the server. Returns the server's status ("ok", "already_claimed", "expired", "not_eligible", "offline").</summary>
        public async Task<string> ClaimGiftAsync(InboxItem item)
        {
            if (item == null || !item.IsGift) return "unknown_gift";
            var r = await RpcAsync("railbound_claim_gift", new { giftId = item.GiftId });
            if (r == null) return "offline";
            PlayerProgression.ApplyReply(r);   // coins and the gift list come back with the profile
            string status = (string)r["status"] ?? "error";
            if (status == "ok" || status == "already_claimed") item.claimed = true;
            if (status == "expired") item.expired = true;
            UnclaimedGifts = 0;
            foreach (var i in Inbox) if (i.IsGift && !i.claimed && !i.expired) UnclaimedGifts++;
            InboxChanged?.Invoke();
            return status;
        }

        /// <summary>Removes notifications from the server inbox. A gift that is still claimable is never removed by "clear all".</summary>
        public async Task<bool> DismissAsync(IList<InboxItem> items)
        {
            if (!Online || items == null || items.Count == 0) return false;
            var ids = new List<string>();
            foreach (var i in items) if (!string.IsNullOrEmpty(i.id)) ids.Add(i.id);
            if (ids.Count == 0) return false;
            try
            {
                await Auth.Client.DeleteNotificationsAsync(Auth.Session, ids);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Social] Dismiss failed: " + e.Message);
                return false;
            }
            Inbox.RemoveAll(i => ids.Contains(i.id));
            InboxChanged?.Invoke();
            return true;
        }

        private void OnSocketNotification(IApiNotification n)
        {
            // background thread
            var item = new InboxItem
            {
                id = n.Id ?? "", code = n.Code, subject = n.Subject ?? "", senderId = n.SenderId ?? "", content = ParseContent(n.Content),
                createTime = ParseTime(n.CreateTime), persistent = n.Persistent
            };
            mainThread.Enqueue(() => OnLiveNotification(item));
        }

        private void OnLiveNotification(InboxItem item)
        {
            if (item.persistent && !Inbox.Exists(i => i.id == item.id))
            {
                Inbox.Insert(0, item);
                if (item.IsGift) UnclaimedGifts++;
            }
            if (item.code == NoteChatMuted)
            {
                long until = item.content["until"] != null ? (long)item.content["until"] : 0;
                if (until > 0) { MutedUntil = DateTimeOffset.FromUnixTimeMilliseconds(until).UtcDateTime; ChatStateChanged?.Invoke(); }
            }
            // the server already paid these: show the new coins and level
            if (item.code == NoteLeaderboardReward || item.code == NoteLevelUp) _ = PlayerProgression.LoadAsync();
            InboxChanged?.Invoke();
            LiveNotification?.Invoke(item);
        }

        // ================================================================= party invites
        /// <summary>Invites a friend to the party (the server checks the friendship). Returns "ok", "not_friends", "rate_limited", "offline".</summary>
        public async Task<string> InviteToPartyAsync(string userId, string partyId)
        {
            var r = await RpcAsync("railbound_party_invite", new { userId, partyId });
            return r != null ? (string)r["status"] ?? "error" : "offline";
        }

        /// <summary>An invite that can still be accepted (the party id and who sent it), or null.</summary>
        public static bool InviteUsable(InboxItem i)
        {
            if (i == null || i.code != NotePartyInvite || string.IsNullOrEmpty(i.Str("partyId"))) return false;
            long exp = i.content["expiresAt"] != null ? (long)i.content["expiresAt"] : 0;
            return exp <= 0 || DateTimeOffset.FromUnixTimeMilliseconds(exp).UtcDateTime > DateTime.UtcNow;
        }

        // ================================================================= chat
        public IReadOnlyList<ChatLine> Lines(string room)
        {
            if (!lines.TryGetValue(room, out var l)) { l = new List<ChatLine>(); lines[room] = l; }
            return l;
        }

        public int Unread(string room) { return unread.TryGetValue(room, out int n) ? n : 0; }
        public void MarkRead(string room) { if (Unread(room) > 0) { unread[room] = 0; ChatChanged?.Invoke(room); } }

        /// <summary>Joins the chat rooms (and keeps them joined across socket reconnects) and loads their recent history.</summary>
        public void StartChat()
        {
            wantChat = true;
            if (!ChatJoining && channels.Count == 0) _ = JoinRoomsAsync();
        }

        private async Task JoinRoomsAsync()
        {
            if (!Online) return;
            ChatJoining = true;
            ChatStateChanged?.Invoke();
            try
            {
                var socket = await Auth.EnsureSocketAsync();
                if (socket == null) return;
                if (socket != boundSocket) Bind(socket);
                foreach (var room in Rooms)
                {
                    try
                    {
                        var ch = await socket.JoinChatAsync(room, ChannelType.Room, persistence: true, hidden: false);
                        channels[room] = ch; roomOfChannel[ch.Id] = room;
                        await LoadHistoryAsync(room, ch.Id);
                    }
                    catch (Exception e) { Debug.LogWarning("[Social] Join " + room + ": " + e.Message); }
                }
            }
            finally
            {
                ChatJoining = false;
                ChatStateChanged?.Invoke();
            }
        }

        private async Task LoadHistoryAsync(string room, string channelId)
        {
            try
            {
                var history = await Auth.Client.ListChannelMessagesAsync(Auth.Session, channelId, 40, forward: false);
                var list = new List<ChatLine>();
                if (history?.Messages != null) foreach (var m in history.Messages) list.Add(ToLine(m, room));
                list.Sort((a, b) => a.time.CompareTo(b.time));
                var l = (List<ChatLine>)Lines(room);
                l.Clear(); l.AddRange(list);
                ChatChanged?.Invoke(room);
            }
            catch (Exception e) { Debug.LogWarning("[Social] History " + room + ": " + e.Message); }
        }

        private ChatLine ToLine(IApiChannelMessage m, string room)
        {
            var c = ParseContent(m.Content);
            var line = new ChatLine
            {
                id = m.MessageId ?? "", room = room, channelId = m.ChannelId ?? "", senderId = m.SenderId ?? "", username = m.Username ?? "",
                text = (string)c["message"] ?? "", kind = (string)c["kind"] ?? "", system = (bool?)c["system"] ?? false,
                filtered = (bool?)c["filtered"] ?? false, time = ParseTime(m.CreateTime)
            };
            line.isMe = !string.IsNullOrEmpty(MyUserId) && line.senderId == MyUserId;
            string me = MyUsername;
            line.mentionsMe = !line.isMe && me.Length > 0 && line.text.IndexOf("@" + me, StringComparison.OrdinalIgnoreCase) >= 0;
            return line;
        }

        private void OnSocketChannelMessage(IApiChannelMessage m)
        {
            // background thread
            mainThread.Enqueue(() =>
            {
                if (!roomOfChannel.TryGetValue(m.ChannelId ?? "", out var room)) room = string.IsNullOrEmpty(m.RoomName) ? "global" : m.RoomName;
                var line = ToLine(m, room);
                var l = (List<ChatLine>)Lines(room);
                if (l.Exists(x => x.id == line.id)) return;
                l.Add(line);
                while (l.Count > MaxLinesPerRoom) l.RemoveAt(0);
                if (!line.isMe) unread[room] = Unread(room) + 1;
                ChatChanged?.Invoke(room);
            });
        }

        /// <summary>Sends a chat message. Returns null when the server took it, otherwise the reason for the player.</summary>
        public async Task<string> SendChatAsync(string room, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return "";
            if (text.Length > ChatMaxLength) return "MESSAGES ARE AT MOST " + ChatMaxLength + " CHARACTERS";
            if (Muted) return "YOU ARE MUTED UNTIL " + MutedUntil.ToLocalTime().ToString("HH:mm");
            if (!channels.TryGetValue(room, out var ch) || boundSocket == null || !boundSocket.IsConnected) return "CHAT IS NOT CONNECTED";
            try
            {
                await boundSocket.WriteChatMessageAsync(ch.Id, JsonConvert.SerializeObject(new { message = text }));
                return null;
            }
            catch (Exception e)
            {
                return FriendlyChatError(e.Message);
            }
        }

        // the module's refusals, in the player's words
        private string FriendlyChatError(string message)
        {
            string m = (message ?? "").ToLowerInvariant();
            if (m.Contains("muted until"))
            {
                int at = m.IndexOf("muted until", StringComparison.Ordinal) + "muted until".Length;
                if (DateTime.TryParse(message.Substring(at).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var until))
                {
                    MutedUntil = until;
                    ChatStateChanged?.Invoke();
                    return "YOU ARE MUTED UNTIL " + until.ToLocalTime().ToString("HH:mm");
                }
                return "YOU ARE MUTED";
            }
            if (m.Contains("slow down")) return "SLOW DOWN - AT MOST 5 MESSAGES EVERY 10 SECONDS";
            if (m.Contains("same message")) return "YOU JUST SENT THAT";
            if (m.Contains("longer than")) return "MESSAGES ARE AT MOST " + ChatMaxLength + " CHARACTERS";
            if (m.Contains("empty")) return "";
            return "MESSAGE NOT SENT";
        }

        /// <summary>Reports a chat message. Returns "ok", "already_reported", "not_found", "not_reportable" or "offline".</summary>
        public async Task<string> ReportAsync(ChatLine line, string reason)
        {
            if (line == null) return "not_found";
            var r = await RpcAsync("railbound_chat_report", new { channelId = line.channelId, messageId = line.id, reason });
            string status = r != null ? (string)r["status"] ?? "error" : "offline";
            if (status == "ok" || status == "already_reported") { line.reported = true; ChatChanged?.Invoke(line.room); }
            return status;
        }
    }
}
