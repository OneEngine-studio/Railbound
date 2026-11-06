using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Railbound.DebugTools
{
    /// <summary>
    /// Play-mode diagnostic for the "white trees" artifact in MAP-2. Each key flips one rendering feature so you can
    /// see which one makes the white go away. Works on runtime copies of the volume profiles (nothing is saved).
    ///   F1 volumetric fog   F2 all fog   F3 bloom   F4 anti-aliasing (TAA / SMAA)
    ///   F5 exposure (auto / fixed)   F6 far-tree billboards   F9 hide / show this panel
    /// Editor and development builds only.
    /// </summary>
    [DisallowMultipleComponent]
    public class Map2RenderDebug : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly List<Fog> fogs = new List<Fog>();
        private readonly List<Bloom> blooms = new List<Bloom>();
        private readonly List<Exposure> exposures = new List<Exposure>();

        private bool volumetricOn = true, fogOn = true, bloomOn = true, smaa, fixedExposure, noBillboards, showPanel = true;
        private HDAdditionalCameraData camData;
        private HDAdditionalCameraData.AntialiasingMode originalAA;
        private readonly Dictionary<Terrain, float> billboardDistance = new Dictionary<Terrain, float>();
        private readonly List<ExposureMode> originalExposureMode = new List<ExposureMode>();
        private GUIStyle style;

        private void Start()
        {
            foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (v.sharedProfile == null) continue;
                var profile = v.profile;                       // runtime clone, the asset stays untouched
                if (profile.TryGet(out Fog f)) fogs.Add(f);
                if (profile.TryGet(out Bloom b)) blooms.Add(b);
                if (profile.TryGet(out Exposure e)) { exposures.Add(e); originalExposureMode.Add(e.mode.value); }
            }
            foreach (var t in FindObjectsByType<Terrain>(FindObjectsSortMode.None)) billboardDistance[t] = t.treeBillboardDistance;
        }

        private HDAdditionalCameraData Cam()
        {
            if (camData == null && Camera.main != null)
            {
                camData = Camera.main.GetComponent<HDAdditionalCameraData>();
                if (camData != null) originalAA = camData.antialiasing;
            }
            return camData;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) ToggleVolumetricFog();
            if (Input.GetKeyDown(KeyCode.F2)) ToggleFog();
            if (Input.GetKeyDown(KeyCode.F3)) ToggleBloom();
            if (Input.GetKeyDown(KeyCode.F4)) ToggleAntiAliasing();
            if (Input.GetKeyDown(KeyCode.F5)) ToggleExposure();
            if (Input.GetKeyDown(KeyCode.F6)) ToggleBillboards();
            if (Input.GetKeyDown(KeyCode.F9)) showPanel = !showPanel;
        }

        public void ToggleVolumetricFog()
        {
            volumetricOn = !volumetricOn;
            foreach (var f in fogs) f.enableVolumetricFog.Override(volumetricOn);
        }

        public void ToggleFog()
        {
            fogOn = !fogOn;
            foreach (var f in fogs) f.enabled.Override(fogOn);
        }

        public void ToggleBloom()
        {
            bloomOn = !bloomOn;
            foreach (var b in blooms) b.active = bloomOn;
        }

        public void ToggleAntiAliasing()
        {
            var cd = Cam();
            if (cd == null) return;
            smaa = !smaa;
            cd.antialiasing = smaa ? HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing : originalAA;
        }

        public void ToggleExposure()
        {
            fixedExposure = !fixedExposure;
            for (int i = 0; i < exposures.Count; i++)
            {
                if (fixedExposure) exposures[i].mode.Override(ExposureMode.Fixed);
                else exposures[i].mode.Override(originalExposureMode[i]);
            }
        }

        public void ToggleBillboards()
        {
            noBillboards = !noBillboards;
            foreach (var kv in billboardDistance)
                if (kv.Key != null) kv.Key.treeBillboardDistance = noBillboards ? 5000f : kv.Value;
        }

        private static string S(bool on) => on ? "ON " : "OFF";

        private void OnGUI()
        {
            if (!showPanel) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true };
                style.normal.textColor = Color.white;
            }
            var cd = Cam();
            string aa = cd != null ? cd.antialiasing.ToString() : "-";
            string text =
                "<b>MAP-2 render debug</b>   (F9 hides)\n" +
                "F1  volumetric fog   " + S(volumetricOn) + "\n" +
                "F2  all fog              " + S(fogOn) + "\n" +
                "F3  bloom                " + S(bloomOn) + "\n" +
                "F4  anti-aliasing      " + aa + "\n" +
                "F5  exposure           " + (fixedExposure ? "FIXED" : "AUTO") + "\n" +
                "F6  far-tree billboards " + S(!noBillboards);
            GUI.Box(new Rect(12, 12, 330, 150), text, style);
        }
#endif
    }
}
