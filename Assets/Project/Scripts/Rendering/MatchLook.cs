using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// The match's image, applied in code on top of MAP-2's own day volume (scene assets are not edited by hand): a global volume of its own,
/// above the map's, that only overrides what made the picture look cheap.
///  * Light: the map's indirect light multiplier (2.4) flattened and washed out every surface; 1.35 keeps the shade readable.
///  * Colour: contrast and saturation pushed the grass to neon and the far mountains to purple; a neutral filmic grade (ACES) with a
///    touch less saturation, a neutral fog colour and a little less haze keep colours natural at every distance.
///  * Grounding: ambient occlusion and contact shadows so characters, rocks and props sit on the ground instead of floating.
///  * Noise: film grain only as much as the player's Film Grain setting asks for (0 unless they chose some); SSAO and bloom also follow
///    the player's settings.
/// Clients only. <c>-classiclook</c> switches it off (to compare); the probe test aid flips it at runtime.
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

        var tm = p.Add<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
        var ca = p.Add<ColorAdjustments>(); ca.contrast.Override(6f); ca.saturation.Override(-4f);
        var wb = p.Add<WhiteBalance>(); wb.temperature.Override(3f); wb.tint.Override(0f);
        var il = p.Add<IndirectLightingController>(); il.indirectDiffuseLightingMultiplier.Override(1.35f); il.reflectionLightingMultiplier.Override(1f);
        // the player's own settings decide grain, ambient occlusion and bloom (the settings menu changes every volume, this one too)
        var fg = p.Add<FilmGrain>(); fg.intensity.Override(Mathf.Clamp01(PlayerPrefs.GetFloat("Graphics_FilmGrain", 0f)));
        var ao = p.Add<ScreenSpaceAmbientOcclusion>(); ao.intensity.Override(0.75f); ao.radius.Override(1.6f); ao.directLightingStrength.Override(0.25f);
        ao.active = PlayerPrefs.GetInt("Graphics_SSAO", 1) == 1;
        var cs = p.Add<ContactShadows>(); cs.enable.Override(true); cs.length.Override(0.18f); cs.opacity.Override(0.85f);
        var fog = p.Add<Fog>(); fog.albedo.Override(new Color(0.96f, 0.96f, 0.95f)); fog.meanFreePath.Override(1100f);
        var bl = p.Add<Bloom>(); bl.intensity.Override(0.14f); bl.scatter.Override(0.6f);
        bl.active = PlayerPrefs.GetInt("Graphics_Bloom", 1) == 1;
        v.sharedProfile = p;
        return v;
    }
}
