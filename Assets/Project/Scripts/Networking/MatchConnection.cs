/// <summary>
/// Where the game server of the current match is. The lobby fills it in after the Nakama allocation RPC
/// (see <see cref="ServerAllocationClient"/>); the gameplay scene's network overlay reads it when it connects.
/// Empty means "use the default server address".
/// </summary>
public static class MatchConnection
{
    public static string MatchId { get; private set; } = "";
    public static string Host { get; private set; } = "";
    public static ushort Port { get; private set; }
    public static string ServerName { get; private set; } = "";

    public static bool HasAllocation => !string.IsNullOrEmpty(Host) && Port != 0;

    public static void Set(string matchId, string host, int port, string serverName)
    {
        MatchId = matchId ?? "";
        Host = host ?? "";
        Port = (ushort)port;
        ServerName = serverName ?? "";
    }

    public static void Clear()
    {
        MatchId = Host = ServerName = "";
        Port = 0;
    }
}
