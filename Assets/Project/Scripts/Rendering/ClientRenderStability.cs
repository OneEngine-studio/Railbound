using UnityEngine;

/// <summary>
/// Client-side render settings that do not depend on how each map was authored:
///  - frame pacing (see GraphicsManager.ApplyFramePacing) is applied before the first scene, not only once the lobby loads.
///
/// Dedicated servers skip all of it.
/// </summary>
public static class ClientRenderStability
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (DedicatedServerRuntime.IsDedicatedServer) return;
        Michsky.UI.Reach.GraphicsManager.ApplyFramePacing();
    }
}
