using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; // Added for New Input System support

public class NetworkHotkeyManager : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton == null) return;

        string[] args = Environment.GetCommandLineArgs();

        // Auto-start Host if container passes -host flag
        if (args.Contains("-host"))
        {
            Debug.Log("[Network] Starting Host via command-line argument...");
            NetworkManager.Singleton.StartHost();
        }
        // Auto-start Server if container passes -server flag
        else if (args.Contains("-server"))
        {
            Debug.Log("[Network] Starting Dedicated Server via command-line argument...");
            NetworkManager.Singleton.StartServer();
        }
    }

    private void Update()
    {
        // Don't listen for keyboard inputs on headless Linux server instances
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;

        // New Input System syntax (fixes InvalidOperationException)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) NetworkManager.Singleton.StartHost();
            if (Keyboard.current.cKey.wasPressedThisFrame) NetworkManager.Singleton.StartClient();
        }
    }
}