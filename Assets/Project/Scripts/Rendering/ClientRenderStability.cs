using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

/// <summary>
/// Client-side fixes for smearing and flicker that do not depend on how each map was authored:
///  - volumetric fog that uses Reprojection denoising is switched to Gaussian: HDRP documents Reprojection as causing
///    "severe ghosting artifacts for highly dynamic lighting", which shows as trails behind the player and flickering fog;
///  - frame pacing (see GraphicsManager.ApplyFramePacing) is applied before the first scene, not only once the lobby loads.
///
/// Volumes created after a scene loads (map streaming) are picked up by a light once-a-second sweep. Dedicated servers
/// skip all of it.
/// </summary>
public static class ClientRenderStability
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (DedicatedServerRuntime.IsDedicatedServer) return;
        Michsky.UI.Reach.GraphicsManager.ApplyFramePacing();

        var go = new GameObject("[ClientRenderStability]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Sweeper>();
    }

    private sealed class Sweeper : MonoBehaviour
    {
        private readonly HashSet<int> doneVolumes = new HashSet<int>();
        private float next;

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            doneVolumes.Clear();
            next = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;

            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v == null || v.sharedProfile == null || !doneVolumes.Add(v.GetInstanceID())) continue;
                if (!v.sharedProfile.TryGet<Fog>(out var authored) || authored.denoisingMode.value == FogDenoisingMode.Gaussian) continue;
                var live = SafeProfile(v);            // a runtime copy; the saved asset is never touched
                if (live != null && live.TryGet<Fog>(out var fog)) fog.denoisingMode.Override(FogDenoisingMode.Gaussian);
            }
        }

        /// <summary>Volume.profile clones the shared profile and throws if one of its components was destroyed.</summary>
        private static VolumeProfile SafeProfile(Volume v)
        {
            try { return v.profile; }
            catch (System.Exception) { return null; }
        }
    }
}
