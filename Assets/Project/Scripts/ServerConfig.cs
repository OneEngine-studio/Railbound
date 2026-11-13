/// <summary>
/// Single source of truth for the default backend endpoints. Scene and prefab values still override these
/// where a component serializes its own address; scripts that used to hardcode an address read from here instead.
/// </summary>
public static class ServerConfig
{
    // Nakama API through the Cloudflare tunnel (restapinakama.kushanherath.me -> nakama:7350 in the k3s cluster).
    // The realtime socket (matchmaker) uses WebSocket over the same address.
    public const string NakamaScheme = "https";
    public const string NakamaHost = "restapinakama.kushanherath.me";
    public const int NakamaPort = 443;
    public const string NakamaServerKey = "defaultkey";

    // Fallback address of a game server when no server was allocated (direct join). Normally Nakama's "railbound_allocate"
    // returns the real host and UDP port (Agones range 7000-8000). This is the AWS k3s master's Elastic IP (infra-railbound, eip.tf),
    // which no longer changes when the cluster is stopped and started.
    public const string GameServerAddress = "13.204.24.15";
    public const ushort GameServerPort = 7777;

    // The allocation RPC returns the host and UDP port of the node the game server runs on (the fleet spreads servers over
    // all nodes, each with its own public address), so the client uses that host. GameServerAddress only replaces it when
    // Nakama sends nothing or a private address (10.x / 172.16-31.x / 192.168.x) that a player cannot reach.
    // Turn this on only when ALL game servers are reachable through the single GameServerAddress (one public relay / elastic IP).
    public static readonly bool UseConfiguredGameServerHost = false;
}
