using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

/// <summary>
/// Client-side fixes for smearing and flicker that do not depend on how each map was authored:
///  - volumetric fog that uses Reprojection denoising is switched to Gaussian: HDRP documents Reprojection as causing
///    "severe ghosting artifacts for highly dynamic lighting", which shows as trails behind the player and flickering fog;
///  - cameras using TAA reject more history on moving pixels, so characters and vehicles no longer leave trails;
///  - the saved DLSS choice reaches the match's main camera (GraphicsManager only runs in the lobby);
///  - frame pacing (see GraphicsManager.ApplyFramePacing) is applied before the first scene, not only once the lobby loads.
///
/// Cameras and volumes created after a scene loads (the network player camera, map streaming) are picked up by a light
/// once-a-second sweep. Dedicated servers skip all of it.
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
        private readonly HashSet<int> doneCameras = new HashSet<int>();
        private readonly HashSet<int> doneVolumes = new HashSet<int>();
        private float next;

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            doneCameras.Clear();
            doneVolumes.Clear();
            next = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;

            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || !doneCameras.Add(cam.GetInstanceID())) continue;
                var hd = cam.GetComponent<HDAdditionalCameraData>();
                if (hd == null) continue;
                if (cam.CompareTag("MainCamera"))   // not minimap / UI cameras
                {
                    Michsky.UI.Reach.GraphicsManager.ApplySavedUpscaling(hd);
                    ApplySavedAntiAliasing(hd);
                }
                TuneTaa(hd);
            }

            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v == null || v.sharedProfile == null || !doneVolumes.Add(v.GetInstanceID())) continue;
                if (!v.sharedProfile.TryGet<Fog>(out var authored) || authored.denoisingMode.value == FogDenoisingMode.Gaussian) continue;
                var live = SafeProfile(v);            // a runtime copy; the saved asset is never touched
                if (live != null && live.TryGet<Fog>(out var fog)) fog.denoisingMode.Override(FogDenoisingMode.Gaussian);
            }
        }

        /// <summary>
        /// The player's anti-aliasing choice on the match camera. GraphicsManager applies it only to the lobby's camera, so a match ran with
        /// the camera prefab's "None": every blade of grass shimmered, which read as heavy noise over the whole picture. Nothing saved
        /// means TAA (the settings menu's default).
        /// </summary>
        private static void ApplySavedAntiAliasing(HDAdditionalCameraData hd)
        {
            int index = PlayerPrefs.GetInt("Graphics_AntiAliasing", 3);
            switch (index)
            {
                case 0: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None; break;
                case 1: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing; break;
                case 2:
                    hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    hd.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
                    break;
                default: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing; break;
            }
        }

        private static void TuneTaa(HDAdditionalCameraData hd)
        {
            if (hd.antialiasing != HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing) return;
            hd.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;   // High is needed for the history-rejection options
            hd.taaMotionVectorRejection = 0.6f;     // drop history where pixels move: removes trails behind characters and vehicles
            hd.taaAntiHistoryRinging = true;
            hd.taaBaseBlendFactor = 0.85f;          // slightly less history than the 0.875 default
        }

        /// <summary>Volume.profile clones the shared profile and throws if one of its components was destroyed.</summary>
        private static VolumeProfile SafeProfile(Volume v)
        {
            try { return v.profile; }
            catch (System.Exception) { return null; }
        }
    }
}
