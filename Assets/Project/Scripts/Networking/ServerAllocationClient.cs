using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Asks Nakama (RPC "railbound_allocate") for the dedicated game server of a match. Nakama allocates one through Agones;
/// every player who sends the same match id gets the same server back.
/// </summary>
public static class ServerAllocationClient
{
    public const string RpcName = "railbound_allocate";

    [Serializable]
    private class Request { public string matchId; public int players; }

    [Serializable]
    private class Reply { public string status; public string matchId; public string host; public int port; public string name; }

    public readonly struct Allocation
    {
        public readonly string Host;
        public readonly int Port;
        public readonly string ServerName;
        public Allocation(string host, int port, string serverName) { Host = host; Port = port; ServerName = serverName; }
    }

    /// <summary>
    /// Polls the RPC until a server is ready (another player may be allocating it for the same match), the timeout
    /// passes or <paramref name="cancelled"/> returns true. Returns null when no server could be obtained.
    /// </summary>
    public static async Task<Allocation?> AllocateAsync(string matchId, int players, float timeoutSeconds, Func<bool> cancelled, Action<string> status)
    {
        var auth = AuthenticationService.Instance;
        if (auth == null || !auth.IsAuthenticated || auth.Client == null || auth.Session == null)
        {
            Debug.Log("[ServerAllocation] Not signed in to Nakama: no allocation possible.");
            return null;
        }

        string payload = JsonUtility.ToJson(new Request { matchId = matchId, players = Mathf.Max(1, players) });
        float start = Time.realtimeSinceStartup;
        int attempt = 0;

        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (cancelled != null && cancelled()) return null;
            attempt++;

            try
            {
                var rpc = await auth.Client.RpcAsync(auth.Session, RpcName, payload);
                var reply = JsonUtility.FromJson<Reply>(rpc.Payload);
                switch (reply != null ? reply.status : null)
                {
                    case "ready":
                        Debug.Log($"[ServerAllocation] Server ready for match {matchId}: {reply.host}:{reply.port} ({reply.name})");
                        return new Allocation(reply.host, reply.port, reply.name);

                    case "pending":
                        status?.Invoke("PREPARING YOUR MATCH SERVER...");
                        break;

                    case "unavailable":
                        status?.Invoke("ALL SERVERS ARE BUSY, RETRYING...");
                        await Task.Delay(2500);
                        break;

                    default:
                        Debug.LogWarning($"[ServerAllocation] Unexpected reply: {rpc.Payload}");
                        break;
                }
            }
            catch (Exception ex)
            {
                // the RPC is missing (Nakama without the module), the network dropped, ...
                Debug.LogWarning($"[ServerAllocation] Attempt {attempt} failed: {ex.Message}");
                if (attempt >= 3) return null;
                await Task.Delay(1500);
            }

            await Task.Delay(1000);
        }

        Debug.LogWarning($"[ServerAllocation] Timed out after {timeoutSeconds:0}s waiting for a server.");
        return null;
    }
}
