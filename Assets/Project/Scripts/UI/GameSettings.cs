using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Railbound.UI
{
    /// <summary>
    /// The player's audio and video settings: kept in PlayerPrefs and applied when the game starts (so every scene gets them) and whenever the
    /// SETTINGS screen of the lobby changes one. Not used by a dedicated server.
    /// </summary>
    public static class GameSettings
    {
        private const string KMaster = "Railbound.Settings.MasterVolume";
        private const string KMode = "Railbound.Settings.DisplayMode";
        private const string KResolution = "Railbound.Settings.Resolution";   // "WIDTHxHEIGHT"
        private const string KVsync = "Railbound.Settings.VSync";
        private const string KFps = "Railbound.Settings.FpsCap";
        private const string KQuality = "Railbound.Settings.Quality";

        public static readonly string[] DisplayModes = { "FULLSCREEN", "WINDOWED" };
        public static readonly int[] FpsCaps = { 0, 30, 60, 120, 144, 240 };   // 0 = unlimited

        public static float MasterVolume { get => Mathf.Clamp01(PlayerPrefs.GetFloat(KMaster, 1f)); set { PlayerPrefs.SetFloat(KMaster, Mathf.Clamp01(value)); ApplyAudio(); } }
        public static int DisplayMode { get => Mathf.Clamp(PlayerPrefs.GetInt(KMode, Screen.fullScreen ? 0 : 1), 0, 1); set { PlayerPrefs.SetInt(KMode, value); ApplyScreen(); } }
        public static bool VSync { get => PlayerPrefs.GetInt(KVsync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1; set { PlayerPrefs.SetInt(KVsync, value ? 1 : 0); ApplyFrameRate(); } }
        public static int FpsCapIndex { get => Mathf.Clamp(PlayerPrefs.GetInt(KFps, 0), 0, FpsCaps.Length - 1); set { PlayerPrefs.SetInt(KFps, value); ApplyFrameRate(); } }
        public static int QualityLevel { get => Mathf.Clamp(PlayerPrefs.GetInt(KQuality, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1)); set { PlayerPrefs.SetInt(KQuality, value); ApplyQuality(); } }

        /// <summary>The screen sizes the monitor offers (one entry per width and height).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct().OrderBy(v => v.x).ThenBy(v => v.y).ToList();
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            return list;
        }

        public static int ResolutionIndex
        {
            get
            {
                var list = Resolutions();
                string saved = PlayerPrefs.GetString(KResolution, "");
                int i = list.FindIndex(v => v.x + "x" + v.y == saved);
                if (i >= 0) return i;
                i = list.FindIndex(v => v.x == Screen.width && v.y == Screen.height);
                return i >= 0 ? i : list.Count - 1;
            }
            set
            {
                var list = Resolutions();
                var v = list[Mathf.Clamp(value, 0, list.Count - 1)];
                PlayerPrefs.SetString(KResolution, v.x + "x" + v.y);
                ApplyScreen();
            }
        }

        /// <summary>Forgets every saved setting and goes back to what the game started with.</summary>
        public static void ResetAll()
        {
            foreach (var k in new[] { KMaster, KMode, KResolution, KVsync, KFps, KQuality }) PlayerPrefs.DeleteKey(k);
            AudioListener.volume = 1f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Save();
        }

        public static void Save() { try { PlayerPrefs.Save(); } catch (System.Exception) { /* kept for this session only */ } }

        // ---------------- apply ----------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStart()
        {
            if (Application.isBatchMode) return;   // a dedicated server has its own limits (DedicatedServerRuntime)
            ApplyAudio();
            if (PlayerPrefs.HasKey(KQuality)) ApplyQuality();
            if (PlayerPrefs.HasKey(KMode) || PlayerPrefs.HasKey(KResolution)) ApplyScreen();
            if (PlayerPrefs.HasKey(KVsync) || PlayerPrefs.HasKey(KFps)) ApplyFrameRate();
        }

        public static void ApplyAudio() { AudioListener.volume = MasterVolume; }

        public static void ApplyQuality() { if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(QualityLevel, true); }

        public static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            int cap = FpsCaps[FpsCapIndex];
            Application.targetFrameRate = cap == 0 ? -1 : cap;
        }

        public static void ApplyScreen()
        {
            var list = Resolutions();
            var res = list[Mathf.Clamp(ResolutionIndex, 0, list.Count - 1)];
            var mode = DisplayMode == 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(res.x, res.y, mode);
        }
    }
}
