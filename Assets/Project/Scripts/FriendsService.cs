using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// The player's friends, straight from Nakama's friends system: the list (friends, invitations sent and received), adding by username,
/// accepting, removing, and who is online right now (the friends are followed on the realtime socket). Used by the FRIENDS tab of the
/// lobby's social window.
/// </summary>
public static class FriendsService
{
    public enum Relation { Friend = 0, InviteSent = 1, InviteReceived = 2, Blocked = 3 }

    public struct Friend
    {
        public string userId, username, displayName;
        public Relation relation;
        public bool online;
        public string Name => string.IsNullOrEmpty(displayName) ? username : displayName;
    }

    private static readonly List<Friend> list = new List<Friend>();
    private static readonly HashSet<string> online = new HashSet<string>();
    private static ISocket followingOn;
    private static bool busy;

    public static IReadOnlyList<Friend> All => list;
    public static bool Busy => busy;
    public static int OnlineCount => list.Count(f => f.relation == Relation.Friend && f.online);
    public static int InvitesReceived => list.Count(f => f.relation == Relation.InviteReceived);

    /// <summary>The last problem or result, for the panel to show ("USER NOT FOUND", "INVITATION SENT").</summary>
    public static string Notice { get; private set; } = "";
    public static event Action Changed;

    private static bool SignedIn { get { var a = AuthenticationService.Instance; return a != null && a.IsAuthenticated; } }
    private static void Raise() { if (Changed != null) Changed(); }

    // ---------------- reading ----------------
    public static async Task RefreshAsync()
    {
        if (!SignedIn || busy) return;
        busy = true; Raise();
        var auth = AuthenticationService.Instance;
        try
        {
            var result = await auth.Client.ListFriendsAsync(auth.Session, null, 100);
            list.Clear();
            foreach (var f in result.Friends)
            {
                if (f.User == null) continue;
                bool isOn = f.User.Online || online.Contains(f.User.Id);
                list.Add(new Friend { userId = f.User.Id, username = f.User.Username, displayName = f.User.DisplayName, relation = (Relation)Mathf.Clamp(f.State, 0, 3), online = isOn });
            }
            list.Sort((a, b) => a.relation != b.relation ? a.relation.CompareTo(b.relation) : (a.online != b.online ? b.online.CompareTo(a.online) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)));
            Notice = "";
            await FollowAsync();
        }
        catch (Exception ex) { Notice = "COULD NOT LOAD FRIENDS"; Debug.LogWarning("[Friends] " + ex.Message); }
        finally { busy = false; Raise(); }
    }

    // who is online: follow the friends on the socket and track their status changes
    private static async Task FollowAsync()
    {
        try
        {
            var socket = await AuthenticationService.Instance.EnsureSocketAsync();
            if (socket == null || !socket.IsConnected) return;
            if (followingOn != socket)
            {
                if (followingOn != null) followingOn.ReceivedStatusPresence -= OnStatusPresence;
                socket.ReceivedStatusPresence += OnStatusPresence;
                followingOn = socket;
            }
            var ids = list.Where(f => f.relation == Relation.Friend || f.relation == Relation.InviteSent).Select(f => f.userId).ToArray();
            if (ids.Length == 0) return;
            var status = await socket.FollowUsersAsync(ids);
            foreach (var p in status.Presences) online.Add(p.UserId);
            SetOnlineFlags();
        }
        catch (Exception ex) { Debug.LogWarning("[Friends] Presence: " + ex.Message); }
    }

    private static void OnStatusPresence(IStatusPresenceEvent e)
    {
        foreach (var p in e.Joins) online.Add(p.UserId);
        foreach (var p in e.Leaves) online.Remove(p.UserId);
        SetOnlineFlags();
        Raise();
    }

    private static void SetOnlineFlags()
    {
        for (int i = 0; i < list.Count; i++) { var f = list[i]; f.online = online.Contains(f.userId); list[i] = f; }
    }

    // ---------------- changing ----------------
    /// <summary>Sends a friend request by username (or accepts one that person already sent).</summary>
    public static async Task AddAsync(string username)
    {
        username = (username ?? "").Trim();
        if (!SignedIn) { Notice = "SIGN IN TO USE FRIENDS"; Raise(); return; }
        if (username.Length < 3) { Notice = "TYPE THEIR USERNAME"; Raise(); return; }
        var auth = AuthenticationService.Instance;
        if (string.Equals(username, auth.Session.Username, StringComparison.OrdinalIgnoreCase)) { Notice = "THAT IS YOU"; Raise(); return; }
        try
        {
            await auth.Client.AddFriendsAsync(auth.Session, null, new[] { username });
            Notice = "REQUEST SENT TO " + username.ToUpperInvariant();
        }
        catch (ApiResponseException ex) { Notice = ex.StatusCode == 404 ? "NO PLAYER NAMED " + username.ToUpperInvariant() : "COULD NOT ADD THAT PLAYER"; Debug.LogWarning("[Friends] Add: " + ex.Message); }
        catch (Exception ex) { Notice = "COULD NOT ADD THAT PLAYER"; Debug.LogWarning("[Friends] Add: " + ex.Message); }
        await RefreshAsync();
    }

    public static async Task AcceptAsync(string userId)
    {
        if (!SignedIn) return;
        var auth = AuthenticationService.Instance;
        try { await auth.Client.AddFriendsAsync(auth.Session, new[] { userId }); Notice = "FRIEND ADDED"; }
        catch (Exception ex) { Notice = "COULD NOT ACCEPT"; Debug.LogWarning("[Friends] Accept: " + ex.Message); }
        await RefreshAsync();
    }

    /// <summary>Removes a friend, declines an invitation, or cancels one that was sent.</summary>
    public static async Task RemoveAsync(string userId)
    {
        if (!SignedIn) return;
        var auth = AuthenticationService.Instance;
        try { await auth.Client.DeleteFriendsAsync(auth.Session, new[] { userId }); Notice = ""; }
        catch (Exception ex) { Notice = "COULD NOT REMOVE"; Debug.LogWarning("[Friends] Remove: " + ex.Message); }
        await RefreshAsync();
    }
}
