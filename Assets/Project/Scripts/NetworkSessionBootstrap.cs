using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Bridges an authenticated Nakama session to the Netcode transport.
/// Authentication remains owned by AuthenticationService; this component only
/// starts the configured Netcode client or dedicated server.
/// </summary>
public sealed class NetworkSessionBootstrap : MonoBehaviour
{
    [Header("Transport")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort serverPort = 7777;
    [SerializeField] private string listenAddress = "0.0.0.0";

    [Header("Client")]
    [SerializeField] private bool startClientAfterAuthentication = true;
    [SerializeField] private bool persistNetworkManager = true;

    private NetworkManager _networkManager;
    private bool _subscribed;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("[NetworkSessionBootstrap] NetworkManager component is missing.");
            enabled = false;
            return;
        }

        if (persistNetworkManager)
            DontDestroyOnLoad(gameObject);

        ConfigureTransport();
        SubscribeToAuthentication();
    }

    private void Start()
    {
        SubscribeToAuthentication();
        StartDedicatedModeFromCommandLine();
    }

    private void OnDestroy()
    {
        if (_subscribed && AuthenticationService.Instance != null)
            AuthenticationService.Instance.OnLoginSuccess -= HandleLoginSuccess;
    }

    private void SubscribeToAuthentication()
    {
        if (_subscribed || AuthenticationService.Instance == null)
            return;

        AuthenticationService.Instance.OnLoginSuccess += HandleLoginSuccess;
        _subscribed = true;
    }

    private void HandleLoginSuccess(AuthResult _)
    {
        if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsAuthenticated)
        {
            Debug.LogError("[NetworkSessionBootstrap] Refusing Netcode startup without authenticated Nakama session.");
            return;
        }

        if (!startClientAfterAuthentication || _networkManager == null)
            return;

        if (_networkManager.IsClient || _networkManager.IsServer)
        {
            Debug.Log("[NetworkSessionBootstrap] Netcode is already running.");
            return;
        }

        if (!_networkManager.StartClient())
            Debug.LogError("[NetworkSessionBootstrap] Netcode client failed to start.");
        else
            Debug.Log($"[NetworkSessionBootstrap] Netcode client started for {serverAddress}:{serverPort}.");
    }

    private void ConfigureTransport()
    {
        var transport = GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[NetworkSessionBootstrap] UnityTransport component is missing.");
            enabled = false;
            return;
        }

        transport.SetConnectionData(serverAddress, serverPort, listenAddress);
    }

    private void StartDedicatedModeFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool isServer = HasArgument(args, "-server");
        bool isHost = HasArgument(args, "-host");

        if (!isServer && !isHost)
            return;

        if (_networkManager.IsClient || _networkManager.IsServer)
            return;

        bool started = isServer
            ? _networkManager.StartServer()
            : _networkManager.StartHost();

        if (!started)
            Debug.LogError($"[NetworkSessionBootstrap] Failed to start {(isServer ? "server" : "host")} mode.");
        else
            Debug.Log($"[NetworkSessionBootstrap] Started {(isServer ? "dedicated server" : "host")} mode.");
    }

    private static bool HasArgument(string[] args, string expected)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
