using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkHotkeyManager : MonoBehaviour
{
    private NetworkManager networkManager;
    private bool isHeadless;

    private void Awake()
    {
        // Cache the headless state once
        isHeadless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }

    private void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null) return;

        ProcessCommandLineArguments();
    }

    private void ProcessCommandLineArguments()
    {
        string[] args = Environment.GetCommandLineArgs();
        
        // Using a loop is generally faster and more memory efficient than LINQ Contains for small arrays
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("-host", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[Network] Starting Host via command-line argument...");
                networkManager.StartHost();
                return;
            }
            
            if (args[i].Equals("-server", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[Network] Starting Dedicated Server via command-line argument...");
                networkManager.StartServer();
                return;
            }
        }
    }

    private void Update()
    {
        // Early exit for headless servers or if already connected
        if (isHeadless || networkManager == null || networkManager.IsClient || networkManager.IsServer) return;

        HandleInput();
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.hKey.wasPressedThisFrame)
        {
            networkManager.StartHost();
        }
        else if (keyboard.cKey.wasPressedThisFrame)
        {
            networkManager.StartClient();
        }
    }
}
