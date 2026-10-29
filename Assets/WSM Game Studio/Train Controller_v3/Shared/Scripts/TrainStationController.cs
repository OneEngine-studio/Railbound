using UnityEngine;
using UnityEngine.Events;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainStationController : MonoBehaviour
    {
        public float leaveWarningTime = 3f;
        public float onAfterStoppingEventTime = 1f;
        public float onBeforeLeavingEventTime = 1f;

        private ILocomotive _locomotive;
        private StationBehaviour _stationBehaviour;
        private bool _turnOffEngines = false;
        private bool _stopping = false;
        private float _stopTimeout;
        private float _lastDirection = 0f;

        public UnityEvent onBrakesActivation; // Execute custom event stack when activating brakes to stop at station
        public UnityEvent onStop; // Executes when the train stops moving
        public UnityEvent onAfterStopping; // Executes after the train stopped
        public UnityEvent onLeaveWarning; // Excutes warning before leaving
        public UnityEvent onBeforeLeaving; // Excutes before leaving (after warning)
        public UnityEvent onLeave; //Execute when leaving the station (Only after stop has been executed)

        // Use this for initialization
        void Start()
        {
            _locomotive = GetComponent<ILocomotive>();
        }

        // Update is called once per frame
        void Update()
        {
            if (_stopping && Mathf.Abs(_locomotive.Speed_MPS) <= 0.1f)
            {
                onStop.Invoke();
                _stopping = false;

                if (_turnOffEngines)
                    _locomotive.EnginesOn = false;

                if (_stationBehaviour == StationBehaviour.LeaveAfterTime && _stopTimeout >= 0f)
                {
                    Invoke("AfterStopping", onAfterStoppingEventTime);
                    Invoke("LeaveWarning", _stopTimeout);
                }
            }
        }

        /// <summary>
        /// Set stop parameters and activate brakes
        /// </summary>
        /// <param name="stationBehaviour">Leave after or stop forever</param>
        /// <param name="stopTimeout">Optional</param>
        public void StopAtStation(StationBehaviour stationBehaviour, float stopTimeout, bool turnOffEngines)
        {
            _turnOffEngines = turnOffEngines;
            _stopTimeout = Mathf.Abs(stopTimeout);
            _stationBehaviour = stationBehaviour;
            Stop();
        }

        /// <summary>
        /// Activate brakes to stop at station
        /// </summary>
        private void Stop()
        {
            _lastDirection = _locomotive.Acceleration;
            _locomotive.Acceleration = 0f;
            _locomotive.Brake = 1f;

            _stopping = true;

            onBrakesActivation.Invoke();
        }

        /// <summary>
        /// Execute before leaving custom events stack
        /// </summary>
        private void AfterStopping()
        {
            onAfterStopping.Invoke();
        }

        /// <summary>
        /// Warning before leaving station
        /// </summary>
        private void LeaveWarning()
        {
            onLeaveWarning.Invoke();

            Invoke("BeforeLeaving", leaveWarningTime);
        }

        /// <summary>
        /// Execute before leaving custom events stack
        /// </summary>
        private void BeforeLeaving()
        {
            onBeforeLeaving.Invoke();

            Invoke("Leave", onBeforeLeavingEventTime);
        }

        /// <summary>
        /// Leave station
        /// </summary>
        private void Leave()
        {
            _stopping = false;

            _locomotive.EnginesOn = true;
            _locomotive.Acceleration = _lastDirection;
            _locomotive.Brake = 0f;

            onLeave.Invoke();
        }
    }
}
