/// <summary>
/// Single source of truth for the default backend endpoints. Scene and prefab values still override these
/// where a component serializes its own address; scripts that used to hardcode an address read from here instead.
/// </summary>
public static class ServerConfig
{
    public const string NakamaScheme = "http";
    public const string NakamaHost = "161.118.255.172";
    public const int NakamaPort = 7350;
    public const string NakamaServerKey = "defaultkey";

    public const string GameServerAddress = "134.185.84.226";
}
