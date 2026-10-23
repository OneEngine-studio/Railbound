using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class SwitchTrigger : MonoBehaviour
    {
        [FormerlySerializedAs("switchMode")]
        [SerializeField] private SwitchMode _switchMode;
        [FormerlySerializedAs("randomSwitchProbability")]
        [Range(0, 100)] [SerializeField] private int _randomSwitchProbability = 50;
        [FormerlySerializedAs("railroadSwitches")]
        [SerializeField] private List<RailroadSwitch_v3> _railroadSwitches;
        [SerializeField] private int _leftRouteIndex = 0;
        [SerializeField] private int _rightRouteIndex = 1;

        private bool _alreadySwitched = false;

        public SwitchMode SwitchMode { get { return _switchMode; } set { _switchMode = value; } }
        public int RandomSwitchProbability { get { return _randomSwitchProbability; } set { _randomSwitchProbability = value; } }
        public List<RailroadSwitch_v3> RailroadSwitches { get { return _railroadSwitches; } set { _railroadSwitches = value; } }
        public int LeftRouteIndex { get { return _leftRouteIndex; } set { _leftRouteIndex = Mathf.Abs(value); } }
        public int RightRouteIndex { get { return _rightRouteIndex; } set { _rightRouteIndex = Mathf.Abs(value); } }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            if (locomotive != null)
            {
                if (_railroadSwitches == null || _railroadSwitches.Count == 0)
                {
                    Debug.LogWarning("Railroad Switch not set on Switch Trigger: " + gameObject.name);
                    return;
                }

                switch (_switchMode)
                {
                    case SwitchMode.Always:
                        SwitchRails(locomotive, locomotive.GetType() == typeof(SplineBasedLocomotive)); break;
                    case SwitchMode.Once:
                        SwitchOnce(locomotive, locomotive.GetType() == typeof(SplineBasedLocomotive)); break;
                    case SwitchMode.Random:
                        RandomSwitch(locomotive, locomotive.GetType() == typeof(SplineBasedLocomotive)); break;
                    case SwitchMode.IfActivated:
                        SwitchActivatedRails(locomotive, locomotive.GetType() == typeof(SplineBasedLocomotive)); break;
                    case SwitchMode.IfDeactivated:
                        SwitchDeactivatedRails(locomotive, locomotive.GetType() == typeof(SplineBasedLocomotive)); break;
                }
            }
        }

        /// <summary>
        /// Switch rails
        /// </summary>
        private void SwitchRails(ILocomotive locomotive, bool splineBased)
        {
            foreach (var railSwitch in _railroadSwitches)
            {
                if (splineBased)
                    railSwitch.SplineBasedSwitchRails(locomotive, _leftRouteIndex, _rightRouteIndex);
                else
                    railSwitch.SwitchRails();
            }
        }

        /// <summary>
        /// Random rails switching
        /// </summary>
        private void RandomSwitch(ILocomotive locomotive, bool splineBased)
        {
            if (Probability.RandomEvent(_randomSwitchProbability))
                SwitchRails(locomotive, splineBased);
        }

        /// <summary>
        /// Switch rails only once
        /// </summary>
        private void SwitchOnce(ILocomotive locomotive, bool splineBased)
        {
            if (!_alreadySwitched)
            {
                SwitchRails(locomotive, splineBased);
                _alreadySwitched = true;
            }
        }

        /// <summary>
        /// Switch rails if activated
        /// </summary>
        private void SwitchActivatedRails(ILocomotive locomotive, bool splineBased)
        {
            foreach (var railSwitch in _railroadSwitches)
            {
                if (railSwitch.Activated)
                {
                    if (splineBased)
                        railSwitch.SplineBasedSwitchRails(locomotive, _leftRouteIndex, _rightRouteIndex);
                    else
                        railSwitch.SwitchRails();
                }
            }
        }

        /// <summary>
        /// Switch rails if deactivated
        /// </summary>
        private void SwitchDeactivatedRails(ILocomotive locomotive, bool splineBased)
        {
            foreach (var railSwitch in _railroadSwitches)
            {
                if (!railSwitch.Activated)
                {
                    if (splineBased)
                        railSwitch.SplineBasedSwitchRails(locomotive, _leftRouteIndex, _rightRouteIndex);
                    else
                        railSwitch.SwitchRails();
                }
            }
        }
    }
}
