using System;
using UnityEngine;

/// <summary>
/// Runtime settings of a headless dedicated server. Without a frame-rate cap a server started with -batchmode -nographics
/// runs its main loop as fast as the CPU allows, so an idle server in the Kubernetes fleet burned about 1.2 CPU cores and
/// the nodes filled up with servers that did nothing. Netcode ticks at 30 Hz by default, so 30 frames per second loses nothing.
///
/// Only active on a headless build (no graphics device) or when started with -server / -dedicated; clients and the editor are untouched.
/// Override with the command line:  -fps 60
/// </summary>
public static class DedicatedServerRuntime
{
    public const int DefaultServerFrameRate = 30;

    public static bool IsDedicatedServer
    {
        get
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return true;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, "-server", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-dedicated", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        if (!IsDedicatedServer) return;

        int fps = DefaultServerFrameRate;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-fps", StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out int parsed) && parsed > 0)
                fps = Mathf.Clamp(parsed, 10, 120);
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
        Debug.Log($"[Server] Dedicated server runtime: frame rate capped at {fps} fps.");
    }
}
