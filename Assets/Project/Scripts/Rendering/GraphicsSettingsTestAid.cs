using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Michsky.UI.Reach;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Test aid, client only: <c>-gfxsettings DIR</c>. In a live match it drives every graphics setting of the settings menu
/// (<see cref="GraphicsManager"/>, the same calls the menu makes) to two different values and records what the main camera really renders
/// with each time: its anti-aliasing, upscaler, resolution scale and field of view, the quality settings, the frame pacing and the camera's
/// own blended volume stack. A setting whose two values leave everything the same does nothing: FAIL. Screenshots of both values are saved.
/// The player's own graphics choices are saved first and put back at the end.
/// </summary>
public class GraphicsSettingsTestAid : MonoBehaviour
{
    private string dir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
        {
            if (a[i] != "-gfxsettings" || Application.isBatchMode) continue;
            var go = new GameObject("[Test] Graphics settings");
            DontDestroyOnLoad(go);
            go.AddComponent<GraphicsSettingsTestAid>().dir = a[i + 1];
            return;
        }
    }

    private static readonly string[] Keys =
    {
        "Graphics_VSync", "Graphics_FrameRate", "Graphics_TextureQuality", "Graphics_AnisotropicFiltering", "Graphics_ExposureMode", "Graphics_ExposureComp",
        "Graphics_ExposureAdapt", "Graphics_DLSS", "Graphics_DLSSQuality", "Graphics_VolumetricFog", "Graphics_Fog", "Graphics_Brightness", "Graphics_AntiAliasing",
        "Graphics_RenderScaleIndex", "Graphics_FSR", "Graphics_FSRSharpness", "Graphics_MeshQuality", "Graphics_SSAO", "Graphics_ShadowQuality",
        "Graphics_ShadowDistanceIndex", "Graphics_MotionBlur", "Graphics_Bloom", "Graphics_DepthOfField", "Graphics_FilmGrain", "Graphics_FOV",
    };

    private IEnumerator Start()
    {
        Directory.CreateDirectory(dir);
        float waited = 0f;
        while (waited < 240f && (JUTPS.JUGameManager.PlayerController == null || Railbound.BR.BRMatchCoordinator.Instance == null || !Railbound.BR.BRMatchCoordinator.Instance.IsMatchLive))
        { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
        yield return new WaitForSecondsRealtime(6f);

        var gm = FindFirstObjectByType<GraphicsManager>(FindObjectsInactive.Include);
        var report = new StringBuilder();
        if (gm == null) { report.AppendLine("NO GraphicsManager in the match scene"); File.WriteAllText(Path.Combine(dir, "settings.txt"), report.ToString()); Debug.Log("[GfxSettings] DONE (no manager)"); yield break; }

        // the player's choices, to put back afterwards
        var saved = new Dictionary<string, (float f, int i, bool has)>();
        foreach (var k in Keys) saved[k] = (PlayerPrefs.GetFloat(k, float.NaN), PlayerPrefs.GetInt(k, int.MinValue), PlayerPrefs.HasKey(k));

        var tests = new List<(string name, Action a, Action b)>
        {
            ("Anti-aliasing (Off / TAA)", () => gm.SetAntiAliasing(0), () => gm.SetAntiAliasing(3)),
            // DLSS picks its own resolution, so render scale and FSR are checked with DLSS off (as the menu greys them out with it on)
            ("Render scale, DLSS off (50 % / 100 %)", () => { gm.SetDLSS(false); gm.SetRenderScale(0); }, () => gm.SetRenderScale(2)),
            ("DLSS (off / on)", () => gm.SetDLSS(false), () => gm.SetDLSS(true)),
            ("DLSS quality (0 / 3)", () => gm.SetDLSSQuality(0), () => gm.SetDLSSQuality(3)),
            ("FSR at 50 %, DLSS off (off / on)", () => { gm.SetDLSS(false); gm.SetRenderScale(0); gm.SetFSR(false); }, () => gm.SetFSR(true)),
            ("Field of view (60 / 90)", () => gm.SetFieldOfView(60f), () => gm.SetFieldOfView(90f)),
            ("Texture quality (full / quarter)", () => gm.SetTextureQuality(0), () => gm.SetTextureQuality(2)),
            ("Anisotropic (off / forced)", () => gm.SetAnisotropicFiltering(0), () => gm.SetAnisotropicFiltering(2)),
            ("Mesh quality (low / ultra)", () => gm.SetMeshQuality(0), () => gm.SetMeshQuality(3)),
            ("Shadow quality (low / ultra)", () => gm.SetShadowQuality(0), () => gm.SetShadowQuality(3)),
            ("Shadow distance (near / ultra)", () => gm.SetShadowDistance(0), () => gm.SetShadowDistance(3)),
            ("Ambient occlusion (off / on)", () => gm.SetAmbientOcclusion(false), () => gm.SetAmbientOcclusion(true)),
            ("Motion blur (off / on)", () => gm.SetMotionBlur(false), () => gm.SetMotionBlur(true)),
            ("Bloom (off / on)", () => gm.SetBloom(false), () => gm.SetBloom(true)),
            ("Depth of field (off / on)", () => gm.SetDepthOfField(false), () => gm.SetDepthOfField(true)),
            ("Film grain (0 / 100 %)", () => gm.SetFilmGrain(0f), () => gm.SetFilmGrain(1f)),
            ("Fog (off / on)", () => gm.SetFog(false), () => gm.SetFog(true)),
            ("Volumetric fog (off / on)", () => gm.SetVolumetricFog(false), () => gm.SetVolumetricFog(true)),
            ("Brightness (30 % / 80 %)", () => gm.SetBrightness(0.3f), () => gm.SetBrightness(0.8f)),
            ("Exposure mode (auto / fixed)", () => gm.SetExposureAutomatic(), () => gm.SetExposureFixed()),
            ("Exposure compensation (20 % / 80 %)", () => gm.SetExposureCompensation(0.2f), () => gm.SetExposureCompensation(0.8f)),
            ("Exposure adaptation (0 / 2)", () => gm.SetExposureAdaptation(0), () => gm.SetExposureAdaptation(2)),
            ("VSync, no frame limit (off / on)", () => { gm.SetFrameRate(-1); gm.SetVSync(false); }, () => gm.SetVSync(true)),
            ("Frame rate limit (30 / none)", () => gm.SetFrameRate(30), () => gm.SetFrameRate(-1)),
        };

        int pass = 0, fail = 0, n = 0;
        foreach (var (name, a, b) in tests)
        {
            string fa = null, fb = null;
            string err = null;
            try { a(); } catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
            yield return new WaitForSecondsRealtime(1.2f);
            fa = Fingerprint();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"s{n:00}_a.png"));
            yield return null;
            try { b(); } catch (Exception e) { err = (err != null ? err + " | " : "") + e.GetType().Name + ": " + e.Message; }
            yield return new WaitForSecondsRealtime(1.2f);
            fb = Fingerprint();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"s{n:00}_b.png"));
            yield return null;
            string diff = Diff(fa, fb);
            bool ok = diff.Length > 0 && err == null;
            if (ok) pass++; else fail++;
            string line = $"{(ok ? "PASS" : "FAIL")}  s{n:00}  {name}: {(diff.Length > 0 ? diff : "no effect")}{(err != null ? "  ERROR " + err : "")}";
            report.AppendLine(line);
            Debug.Log("[GfxSettings] " + line);
            n++;
        }

        // the player's own choices back, applied the way the game applies them at start
        foreach (var kv in saved)
        {
            if (!kv.Value.has) PlayerPrefs.DeleteKey(kv.Key);
            else if (!float.IsNaN(kv.Value.f)) PlayerPrefs.SetFloat(kv.Key, kv.Value.f);
            else if (kv.Value.i != int.MinValue) PlayerPrefs.SetInt(kv.Key, kv.Value.i);
        }
        PlayerPrefs.Save();
        try { gm.LoadAndApplyAllSettings(); } catch (Exception e) { report.AppendLine("restore: " + e.Message); }
        report.Insert(0, $"{pass} passed, {fail} failed (camera {Camera.main?.name}, manager on {gm.gameObject.name})\n");
        File.WriteAllText(Path.Combine(dir, "settings.txt"), report.ToString());
        Debug.Log($"[GfxSettings] DONE {pass} passed, {fail} failed");
    }

    private static string Fingerprint()
    {
        var d = new SortedDictionary<string, string>();
        var cam = Camera.main;
        if (cam != null)
        {
            d["cam.fov"] = cam.fieldOfView.ToString("0.0");
            if (cam.TryGetComponent(out HDAdditionalCameraData hd))
            {
                d["cam.aa"] = hd.antialiasing.ToString();
                d["cam.dlss"] = hd.allowDeepLearningSuperSampling.ToString();
                d["cam.dlssQ"] = hd.deepLearningSuperSamplingQuality.ToString();
                d["cam.dynRes"] = hd.allowDynamicResolution.ToString();
            }
            var stack = HDCamera.GetOrCreate(cam).volumeStack;
            var ex = stack.GetComponent<Exposure>();
            d["exposure"] = $"{ex.mode.value} comp {ex.compensation.value:0.00} adapt {ex.adaptationSpeedDarkToLight.value:0.0}/{ex.adaptationSpeedLightToDark.value:0.0}";
            d["post"] = stack.GetComponent<ColorAdjustments>().postExposure.value.ToString("0.00");
            var fog = stack.GetComponent<Fog>(); d["fog"] = fog.enabled.value + " vol " + fog.enableVolumetricFog.value;
            d["ssao"] = stack.GetComponent<ScreenSpaceAmbientOcclusion>().intensity.value.ToString("0.00");
            d["motionBlur"] = stack.GetComponent<MotionBlur>().intensity.value.ToString("0.00");
            d["bloom"] = stack.GetComponent<Bloom>().intensity.value.ToString("0.00");
            d["dof"] = stack.GetComponent<DepthOfField>().focusMode.value.ToString();
            d["grain"] = stack.GetComponent<FilmGrain>().intensity.value.ToString("0.00");
            d["shadowDist"] = stack.GetComponent<HDShadowSettings>().maxShadowDistance.value.ToString("0");
        }
        // the handler between frames is the default one: select the game camera's own (what HDRP uses when it renders it)
        if (cam != null)
        {
            DynamicResolutionHandler.UpdateAndUseCamera(cam);
            var drs = DynamicResolutionHandler.instance;
            d["res.scale"] = drs.GetCurrentScale().ToString("0.00");
            d["res.filter"] = drs.filter.ToString();
            var hc = HDCamera.GetOrCreate(cam);
            d["res.rendered"] = hc.actualWidth + "x" + hc.actualHeight;
            DynamicResolutionHandler.ClearSelectedCamera();
        }
        d["q.mip"] = QualitySettings.globalTextureMipmapLimit.ToString();
        d["q.aniso"] = QualitySettings.anisotropicFiltering.ToString();
        d["q.lod"] = QualitySettings.lodBias.ToString("0.0");
        d["q.vsync"] = QualitySettings.vSyncCount.ToString();
        d["fps.target"] = Application.targetFrameRate.ToString();
        var sun = RenderSettings.sun;
        if (sun != null && sun.TryGetComponent(out HDAdditionalLightData sd)) d["sun.shadowRes"] = sd.shadowResolution.level + "/" + sd.shadowResolution.useOverride + "/" + sd.shadowResolution.@override;
        var sb = new StringBuilder();
        foreach (var kv in d) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
        return sb.ToString();
    }

    private static string Diff(string a, string b)
    {
        var da = new Dictionary<string, string>(); var db = new Dictionary<string, string>();
        foreach (var p in a.Split(';')) { int i = p.IndexOf('='); if (i > 0) da[p.Substring(0, i)] = p.Substring(i + 1); }
        foreach (var p in b.Split(';')) { int i = p.IndexOf('='); if (i > 0) db[p.Substring(0, i)] = p.Substring(i + 1); }
        var sb = new StringBuilder();
        foreach (var kv in db) if (!da.TryGetValue(kv.Key, out var v) || v != kv.Value) sb.Append(kv.Key).Append(' ').Append(da.TryGetValue(kv.Key, out var old) ? old : "?").Append(" -> ").Append(kv.Value).Append("  ");
        return sb.ToString().Trim();
    }
}
