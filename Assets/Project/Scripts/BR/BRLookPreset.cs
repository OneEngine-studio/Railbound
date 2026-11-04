using UnityEngine;
using UnityEngine.Rendering;

namespace Railbound.BR
{
    /// <summary>
    /// Switches MAP-1 between a bright Day look and the golden-hour Sunset look.
    /// It locks the day/night cycle to the preset's hour and enables the matching post-processing / fog volume.
    /// Change Active in the inspector (it applies in the editor and at match start).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Railbound/BR/Look Preset (Day / Sunset)")]
    public class BRLookPreset : MonoBehaviour
    {
        public enum Preset
        {
            Day = 0,
            Sunset = 1
        }

        [Header("Active preset")]
        public Preset Active = Preset.Day;

        [Header("Scene references")]
        public CycleManager Cycle;
        public Volume DayVolume;
        public Volume SunsetVolume;

        [Header("Time of day (24h clock)")]
        [Range(6f, 18f)] public float DayHour = 14f;
        [Range(6f, 18f)] public float SunsetHour = 16.8f;

        private void Start()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        [ContextMenu("Apply Preset")]
        public void Apply()
        {
            if (Cycle != null)
            {
                float hour = Active == Preset.Day ? DayHour : SunsetHour;
                Cycle.cycleMode = CycleManager.DayCycleMode.OnlyDay;
                Cycle.dayLockedTime = hour;
                Cycle.currentTime = hour;
            }

            if (DayVolume != null) DayVolume.enabled = Active == Preset.Day;
            if (SunsetVolume != null) SunsetVolume.enabled = Active == Preset.Sunset;
        }
    }
}
