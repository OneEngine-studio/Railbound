using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// The match's image, applied in code on top of MAP-2's GlobalVolume (scene assets are not edited by hand): a global volume of its own,
/// above the map's, that only overrides what made the picture look cheap. Measured against the map's volume:
///  * Colour cast: white balance -13 tinted the whole image blue (the far mountains purple); -2 is natural daylight.
///  * Light: indirect diffuse 2.5 and reflections 1.5 flattened and washed out every surface; 1.4 / 1.1 keep shade and shape readable,
///    and a slight post-exposure lift and contrast keep the overall brightness.
///  * Motion blur: 0.4 with camera blur smeared every turn; 0.2 when the player's Motion Blur setting is on, none when it is off.
///  * Vignette 0.45 darkened a third of the screen; 0.3.
///  * Film grain: the player's setting at the menu's new scale (100 % = HDRP 0.3, fine grain); at 100 % HDRP's own scale it covered the
///    whole picture in noise. Anti-aliasing reaches the match camera too now (see ClientRenderStability).
/// The map keeps its ACES tonemapping, ambient occlusion, contact shadows, fog and sky. Clients only; <c>-classiclook</c> switches it
/// off (to compare), and the probe test aid flips it at runtime.
/// </summary>
public static class MatchLook
{
    private const float Priority = 40f;   // above the map's volumes (0-10), below short full-screen effects (death screen, low health)
    private static Volume volume;

    public static bool Enabled => volume != null && volume.isActiveAndEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private static bool Off()
    {
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return true;
        foreach (var a in System.Environment.GetCommandLineArgs()) if (string.Equals(a, "-classiclook", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name != "MAP-2" || Off()) return;
        Set(true);
        // it belongs to the match scene and goes with it (the lobby has its own look)
        if (volume != null && volume.gameObject.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(volume.gameObject, scene);
    }

    /// <summary>Turns the look on or off (it is built the first time).</summary>
    public static void Set(bool on)
    {
        if (on && volume == null) volume = Build();
        if (volume != null) volume.enabled = on;
    }

    private static Volume Build()
    {
        var go = new GameObject("[Look] Match");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = Priority;
        v.weight = 1f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        p.name = "MatchLook";

        var wb = p.Add<WhiteBalance>(); wb.temperature.Override(-2f); wb.tint.Override(0f);
        var il = p.Add<IndirectLightingController>(); il.indirectDiffuseLightingMultiplier.Override(1.4f); il.reflectionLightingMultiplier.Override(1.1f);
        var ca = p.Add<ColorAdjustments>(); ca.postExposure.Override(0.15f); ca.contrast.Override(5f); ca.saturation.Override(2f);
        // the player's Motion Blur setting (the settings menu only reaches the lobby's volumes)
        var mb = p.Add<MotionBlur>(); mb.intensity.Override(PlayerPrefs.GetInt("Graphics_MotionBlur", 1) == 1 ? 0.2f : 0f);
        var vg = p.Add<Vignette>(); vg.intensity.Override(0.3f);
        // the player's Film Grain setting, scaled like the settings menu now does (100 % used to be HDRP's maximum, which buried the
        // picture in noise); 0 % means none, which also covers MAP-2's own grain
        float grain01 = Mathf.Clamp01(PlayerPrefs.GetFloat("Graphics_FilmGrain", 0f));
        var fg = p.Add<FilmGrain>(); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(Michsky.UI.Reach.GraphicsManager.FilmGrainIntensity(grain01));
        v.sharedProfile = p;
        return v;
    }
}
