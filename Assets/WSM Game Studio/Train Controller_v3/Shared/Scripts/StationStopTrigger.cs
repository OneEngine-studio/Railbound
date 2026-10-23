using UnityEngine;
using UnityEngine.Serialization;

namespace WSMGameStudio.RailroadSystem
{
    public class StationStopTrigger : MonoBehaviour
    {
        [FormerlySerializedAs("stopMode")]
        [SerializeField] private StopMode _stopMode;
        [FormerlySerializedAs("stationDoorDirection")]
        [SerializeField] private StationDoorDirection _stationDoorDirection;
        [FormerlySerializedAs("stationBehaviour")]
        [SerializeField] private StationBehaviour _stationBehaviour;
        [FormerlySerializedAs("stopTimeout")]
        [SerializeField] private float _stopTimeout = 10f;
        [Range(0, 100)]
        [FormerlySerializedAs("randomStopProbability")]
        [SerializeField] private int _randomStopProbability = 50;
        [FormerlySerializedAs("turnOffEngines")]
        [SerializeField] private bool _turnOffEngines = false;
        [SerializeField] private bool _reverseTrainDirection = false;
        [SerializeField] private ReverseDirectionMode _reverseDirectionMode;

        private bool _alreadyStopped = false;

        public StopMode StopMode { get { return _stopMode; } set { _stopMode = value; } }
        public StationDoorDirection StationDoorDirection { get { return _stationDoorDirection; } set { _stationDoorDirection = value; } }
        public StationBehaviour StationBehaviour { get { return _stationBehaviour; } set { _stationBehaviour = value; } }
        public float StopTimeout { get { return _stopTimeout; } set { _stopTimeout = value; } }
        public int RandomStopProbability { get { return _randomStopProbability; } set { _randomStopProbability = value; } }
        public bool TurnOffEngines { get { return _turnOffEngines; } set { _turnOffEngines = value; } }
        public bool ReverseTrainDirection { get { return _reverseTrainDirection; } set { _reverseTrainDirection = value; } }
        public ReverseDirectionMode ReverseDirectionMode { get { return _reverseDirectionMode; } set { _reverseDirectionMode = value; } }

        private void OnTriggerEnter(Collider other)
        {
            ILocomotive locomotive = other.GetComponent<ILocomotive>();

            float trainDirection = locomotive != null ? locomotive.Acceleration : 0f;

            if (_reverseTrainDirection)
            {
                if (locomotive != null)
                {
                    switch (_reverseDirectionMode)
                    {
                        case ReverseDirectionMode.Always:
                            locomotive.Acceleration *= -1;
                            break;
                        case ReverseDirectionMode.OnlyIfMovingForward:
                            if (locomotive.Acceleration > 0f) locomotive.Acceleration *= -1;
                            break;
                        case ReverseDirectionMode.OnlyIfMovingBackwards:
                            if (locomotive.Acceleration < 0f) locomotive.Acceleration *= -1;
                            break;
                    }
                }
            }

            TrainStationController trainStationController = other.GetComponent<TrainStationController>();

            if (trainStationController != null)
            {
                switch (_stopMode)
                {
                    case StopMode.Always:
                        trainStationController.StopAtStation(_stationBehaviour, _stopTimeout, _turnOffEngines);
                        break;
                    case StopMode.Once:
                        if (!_alreadyStopped)
                        {
                            trainStationController.StopAtStation(_stationBehaviour, _stopTimeout, _turnOffEngines);
                            _alreadyStopped = true;
                        }
                        break;
                    case StopMode.Random:
                        if (Probability.RandomEvent(_randomStopProbability))
                            trainStationController.StopAtStation(_stationBehaviour, _stopTimeout, _turnOffEngines);
                        break;
                    case StopMode.OnlyIfMovingForward:
                        if (trainDirection > 0f)
                            trainStationController.StopAtStation(_stationBehaviour, _stopTimeout, _turnOffEngines);
                        break;
                    case StopMode.OnlyIfMovingBackwards:
                        if (trainDirection < 0f)
                            trainStationController.StopAtStation(_stationBehaviour, _stopTimeout, _turnOffEngines);
                        break;
                }

                ITrainDoorsController trainDoorsController = other.GetComponent<ITrainDoorsController>();

                if (trainDoorsController != null)
                    trainDoorsController.StationDoorDirection = _stationDoorDirection;
            }
        }
    }
}
