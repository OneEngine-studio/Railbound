using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Test aid, client only: <c>-gfxprobe DIR</c>. Once the match is live it writes what the main camera really renders with (the resolved
/// volume stack: tonemapping, exposure, colour, film grain, bloom, fog, ambient occlusion, contact shadows; the camera's anti-aliasing,
/// dynamic resolution and upscaler; the frame rate) to DIR/gfx.txt and saves screenshots from a few fixed directions, so look changes can
/// be compared with numbers and pictures.
/// </summary>
public class GraphicsProbeTestAid : MonoBehaviour
{
    private string dir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
        {
            if (a[i] != "-gfxprobe" || Application.isBatchMode) continue;
            var go = new GameObject("[Test] Graphics probe");
            DontDestroyOnLoad(go);
            go.AddComponent<GraphicsProbeTestAid>().dir = a[i + 1];
            return;
        }
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(dir);
        float waited = 0f;
        while (waited < 240f && (JUTPS.JUGameManager.PlayerController == null || Railbound.BR.BRMatchCoordinator.Instance == null || !Railbound.BR.BRMatchCoordinator.Instance.IsMatchLive))
        { waited += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
        yield return new WaitForSecondsRealtime(8f);   // exposure adapts

        var sb = new StringBuilder();
        var cam = Camera.main;
        var stack = VolumeManager.instance.stack;
        void Line(string s) { sb.AppendLine(s); }
        if (cam != null && cam.TryGetComponent(out HDAdditionalCameraData hd))
            Line($"camera: aa={hd.antialiasing} taaQuality={hd.TAAQuality} sharpen={hd.taaSharpenStrength:0.00} dynRes={hd.allowDynamicResolution} dlss={hd.allowDeepLearningSuperSampling} scale={DynamicResolutionHandler.instance.GetCurrentScale():0.00} hdr={cam.allowHDR} fov={cam.fieldOfView:0}");
        Line($"screen {Screen.width}x{Screen.height} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} fps~{1f / Mathf.Max(0.001f, Time.smoothDeltaTime):0}");
        var tm = stack.GetComponent<Tonemapping>(); Line($"tonemapping: mode={tm.mode.value} active={tm.IsActive()}");
        var ex = stack.GetComponent<Exposure>(); Line($"exposure: mode={ex.mode.value} comp={ex.compensation.value:0.00} limits={ex.limitMin.value:0.0}..{ex.limitMax.value:0.0} fixed={ex.fixedExposure.value:0.0}");
        var ca = stack.GetComponent<ColorAdjustments>(); Line($"colorAdjustments: post={ca.postExposure.value:0.00} contrast={ca.contrast.value:0} saturation={ca.saturation.value:0} filter={ca.colorFilter.value} hue={ca.hueShift.value:0}");
        var wb = stack.GetComponent<WhiteBalance>(); Line($"whiteBalance: temp={wb.temperature.value:0} tint={wb.tint.value:0}");
        var fg = stack.GetComponent<FilmGrain>(); Line($"filmGrain: type={fg.type.value} intensity={fg.intensity.value:0.00} response={fg.response.value:0.00} active={fg.IsActive()}");
        var bl = stack.GetComponent<Bloom>(); Line($"bloom: intensity={bl.intensity.value:0.00} threshold={bl.threshold.value:0.00} scatter={bl.scatter.value:0.00}");
        var vg = stack.GetComponent<Vignette>(); Line($"vignette: intensity={vg.intensity.value:0.00}");
        var ao = stack.GetComponent<ScreenSpaceAmbientOcclusion>(); Line($"ssao: intensity={ao.intensity.value:0.00} radius={ao.radius.value:0.00}");
        var cs = stack.GetComponent<ContactShadows>(); Line($"contactShadows: enable={cs.enable.value} length={cs.length.value:0.00}");
        var fog = stack.GetComponent<Fog>(); Line($"fog: enabled={fog.enabled.value} meanFreePath={fog.meanFreePath.value:0} albedo={fog.albedo.value} volumetric={fog.enableVolumetricFog.value} colorMode={fog.colorMode.value}");
        var sky = stack.GetComponent<VisualEnvironment>(); Line($"sky: type={sky.skyType.value}");
        var mb = stack.GetComponent<MotionBlur>(); Line($"motionBlur: intensity={mb.intensity.value:0.00}");
        var dof = stack.GetComponent<DepthOfField>(); Line($"dof: mode={dof.focusMode.value}");
        var ilc = stack.GetComponent<IndirectLightingController>(); Line($"indirect: diffuse={ilc.indirectDiffuseLightingMultiplier.value:0.00} reflection={ilc.reflectionLightingMultiplier.value:0.00}");
        var smh = stack.GetComponent<ShadowsMidtonesHighlights>(); Line($"smh: shadows={smh.shadows.value} highlights={smh.highlights.value}");
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            Line($"volume {v.name}: enabled={v.isActiveAndEnabled} global={v.isGlobal} priority={v.priority} weight={v.weight:0.00} profile={(v.sharedProfile != null ? v.sharedProfile.name : "none")}");
        var sun = RenderSettings.sun; if (sun != null && sun.TryGetComponent(out HDAdditionalLightData sd)) Line($"sun: intensity={sd.intensity:0} lux, rot={sun.transform.eulerAngles}");
        File.WriteAllText(Path.Combine(dir, "gfx.txt"), sb.ToString());
        Debug.Log("[GfxProbe] " + sb.ToString().Replace("\n", " | "));

        // pictures from the player's camera in four directions
        var rig = FindFirstObjectByType<JUTPS.CameraSystems.JUCameraController>();
        if (rig != null && cam != null)
        {
            rig.enabled = false;
            var me = JUTPS.JUGameManager.PlayerController.transform;
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, me.eulerAngles.y + i * 90f, 0f);
                cam.transform.position = me.position + yaw * new Vector3(0.6f, 1.8f, -3.2f);
                cam.transform.rotation = yaw * Quaternion.Euler(6f, 0f, 0f);
                yield return new WaitForSecondsRealtime(1.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"view{i}.png"));
                yield return new WaitForSecondsRealtime(0.4f);
            }
            rig.enabled = true;
        }
        Debug.Log("[GfxProbe] DONE");
    }
}
