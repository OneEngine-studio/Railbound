using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.Gameplay
{
    /// <summary>
    /// Keeps the DRAWN terrain close to the terrain everything stands on. MAP-2's terrains are saved with a heightmap pixel
    /// error of 20: Unity then simplifies the rendered ground until it is up to 20 screen pixels away from the real surface,
    /// which is about 1 m at 50 m and 2 m at 100 m on a 1080p screen. Bots, players and loot stand on the real surface (the
    /// TerrainCollider), so at a distance they looked like they were floating above the ground or sinking into it.
    /// Unity's default of 5 keeps that gap to a few centimetres at combat range. Clients only: a headless server draws nothing.
    /// </summary>
    public static class TerrainDisplayAccuracy
    {
        public const float MaxPixelError = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Apply(scene); }

        public static int Apply(Scene scene)
        {
            int changed = 0;
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                if (terrain.gameObject.scene != scene || terrain.heightmapPixelError <= MaxPixelError) continue;
                terrain.heightmapPixelError = MaxPixelError;
                changed++;
            }
            if (changed > 0) Debug.Log($"[TerrainDisplayAccuracy] heightmap pixel error set to {MaxPixelError} on {changed} terrain(s) of '{scene.name}'.");
            return changed;
        }
    }
}
