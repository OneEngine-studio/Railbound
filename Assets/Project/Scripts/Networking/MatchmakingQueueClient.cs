using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Railbound.Networking
{
    /// <summary>
    /// Client side of the server-authoritative matchmaking queue (Nakama module: RPC railbound_queue_join + authoritative match
    /// "railbound_queue"). The client never decides anything: it asks for a session, joins it, SHOWS the server's countdown and
    /// player count, and goes to the game server the session assigns it (its own join ticket included).
    ///
    /// Why: the old flow let every client run its own 30 s timer and derive the match id from the player list. The same players (or a
    /// party) produced the same id again and were sent to the match that was already running; players whose timer ended at a
    /// different moment than the Nakama matchmaker's lobby were split or mixed between matches.
    /// </summary>
    public static class MatchmakingQueueClient
    {
        public const string RpcName = "railbound_queue_join";
        private const long OpState = 1, OpAssigned = 2, OpFailed = 3;

        public enum Outcome { Assigned, Failed, Cancelled, Unavailable }

        public sealed class Result
        {
            public Outcome Outcome;
            public string SessionId, MatchId, Host, ServerName, Token, Reason;
            public int Port, Players;
        }

        /// <summary>What the server last said about the session (for the HUD).</summary>
        public sealed class Progress { public string Phase; public int Countdown, Players, Max; }

        [Serializable] private class JoinRequest { public string mode; public string partyId; public bool newMatch; }
        [Serializable] private class JoinReply { public string status; public string sessionId; public bool locked; public string matchId; public string host; public int port; public string name; public string token; public int players; }
        [Serializable] private class StateMsg { public string sessionId; public string phase; public int countdown; public int players; public int max; }
        [Serializable] private class AssignedMsg { public string sessionId; public string matchId; public string host; public int port; public string name; public string token; public int players; }
        [Serializable] private class FailedMsg { public string sessionId; public string matchId; public string reason; }

        /// <summary>
        /// Queues for <paramref name="mode"/> and waits for the server's assignment. <paramref name="progress"/> is called on the main
        /// thread whenever the server reports the session state (once a second while counting down).
        /// </summary>
        public static async Task<Result> QueueAsync(IClient client, ISession session, ISocket socket, string mode, string partyId,
            Func<bool> cancelled, Action<Progress> progress, float timeoutSeconds = 180f)
        {
            var inbox = new ConcurrentQueue<IMatchState>();
            Action<IMatchState> onState = s => inbox.Enqueue(s);   // socket callbacks may come from a background thread
            socket.ReceivedMatchState += onState;
            string sessionId = null;
            try
            {
                for (int attempt = 1; attempt <= 4; attempt++)
                {
                    if (cancelled()) return new Result { Outcome = Outcome.Cancelled };
                    JoinReply reply;
                    try
                    {
                        var rpc = await client.RpcAsync(session, RpcName, JsonUtility.ToJson(new JoinRequest { mode = mode, partyId = partyId ?? "" }));
                        reply = JsonUtility.FromJson<JoinReply>(rpc.Payload);
                    }
                    catch (ApiResponseException ex) when (ex.StatusCode == 404 || (ex.Message ?? "").IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.LogWarning("[Queue] The server has no matchmaking queue (old Nakama module).");
                        return new Result { Outcome = Outcome.Unavailable, Reason = "no_queue_rpc" };
                    }

                    if (reply != null && reply.status == "assigned")
                    {
                        Debug.Log($"[Queue] Already locked into match {reply.matchId}: going back to it (reconnect).");
                        return Assigned(reply.sessionId, reply.matchId, reply.host, reply.port, reply.name, reply.token, reply.players);
                    }
                    if (reply == null || reply.status != "queued" || string.IsNullOrEmpty(reply.sessionId))
                    {
                        Debug.LogWarning("[Queue] Unexpected queue reply: " + (reply != null ? reply.status : "null"));
                        continue;
                    }

                    try
                    {
                        await socket.JoinMatchAsync(reply.sessionId);
                        sessionId = reply.sessionId;
                        Debug.Log($"[Queue] Joined matchmaking session {sessionId} (mode {mode}{(reply.locked ? ", already locked" : "")}).");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // the session locked (or filled) between the reply and the join: ask again, the server opens a new one
                        Debug.Log($"[Queue] Session {reply.sessionId} refused the join ({ex.Message}); asking for another session (attempt {attempt}).");
                        await Task.Delay(300);
                    }
                }
                if (sessionId == null) return new Result { Outcome = Outcome.Failed, Reason = "no_session" };

                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    if (cancelled())
                    {
                        Debug.Log($"[Queue] Cancelled; leaving session {sessionId}.");
                        return new Result { Outcome = Outcome.Cancelled, SessionId = sessionId };
                    }
                    while (inbox.TryDequeue(out var s))
                    {
                        if (s.MatchId != sessionId) continue;   // another match this socket is in
                        string json = Encoding.UTF8.GetString(s.State ?? Array.Empty<byte>());
                        if (s.OpCode == OpState)
                        {
                            var st = JsonUtility.FromJson<StateMsg>(json);
                            if (st != null) progress?.Invoke(new Progress { Phase = st.phase, Countdown = st.countdown, Players = st.players, Max = st.max });
                        }
                        else if (s.OpCode == OpAssigned)
                        {
                            var a = JsonUtility.FromJson<AssignedMsg>(json);
                            Debug.Log($"[Queue] Session {sessionId} assigned match {a.matchId}: {a.host}:{a.port} ({a.name}), {a.players} player(s).");
                            return Assigned(sessionId, a.matchId, a.host, a.port, a.name, a.token, a.players);
                        }
                        else if (s.OpCode == OpFailed)
                        {
                            var f = JsonUtility.FromJson<FailedMsg>(json);
                            Debug.LogWarning($"[Queue] Session {sessionId} failed: {f?.reason}.");
                            return new Result { Outcome = Outcome.Failed, SessionId = sessionId, Reason = f != null ? f.reason : "failed" };
                        }
                    }
                    await Task.Delay(100);
                }
                Debug.LogWarning($"[Queue] No assignment from session {sessionId} within {timeoutSeconds:0}s.");
                return new Result { Outcome = Outcome.Failed, SessionId = sessionId, Reason = "timeout" };
            }
            finally
            {
                socket.ReceivedMatchState -= onState;
                if (sessionId != null)
                {
                    // leaving before the lock frees the seat; after the lock the server keeps it (reconnect) whatever we do
                    try { await socket.LeaveMatchAsync(sessionId); } catch (Exception) { }
                }
            }
        }

        private static Result Assigned(string sessionId, string matchId, string host, int port, string name, string token, int players)
        {
            return new Result { Outcome = Outcome.Assigned, SessionId = sessionId, MatchId = matchId, Host = host, Port = port, ServerName = name, Token = token, Players = Mathf.Max(1, players) };
        }
    }
}
