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

    public const string GameServerAddress = "134.185.84.226";
}
