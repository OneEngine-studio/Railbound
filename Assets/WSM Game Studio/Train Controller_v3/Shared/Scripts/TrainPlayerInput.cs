using UnityEngine;
using UnityEngine.Events;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainPlayerInput : MonoBehaviour
    {
        public bool enablePlayerInput = false;
        public TrainInputSettings inputSettings;

        public UnityEvent[] customEvents;

        private ILocomotive _locomotive;
        private IRailwayVehicle _railwayVehicle;
        private TrainDoorsController _doorController;

        // Use this for initialization
        void Start()
        {
            _locomotive = GetComponent<ILocomotive>();
            _railwayVehicle = GetComponent<IRailwayVehicle>();
            _doorController = GetComponent<TrainDoorsController>();
        }

        // Update is called once per frame
        void Update()
        {
            if (enablePlayerInput)
            {
                if (inputSettings == null)
                    return;

                if (_locomotive == null)
                    return;

                #region Movement Controls

                if (!_locomotive.EmergencyBrakes) //Ignores acceleration and brake inputs if emergency brakes are activated
                {
                    if (Input.GetKey(inputSettings.forward))
                        _locomotive.Acceleration = 1f;
                    else if (Input.GetKey(inputSettings.reverse))
                        _locomotive.Acceleration = -1f;
                    else
                        _locomotive.Acceleration = 0f;

                    if (!_locomotive.AutomaticBrakes)
                        _locomotive.Brake = Input.GetKey(inputSettings.brakes) ? 1f : 0f;
                }

                #endregion

                #region Max Speed Control
                inputSettings.speedIncreaseAmount = Mathf.Abs(inputSettings.speedIncreaseAmount);

                if (Input.GetKeyDown(inputSettings.increaseSpeed))
                {
                    if (_railwayVehicle.TrainType == TrainType.PhysicsBased)
                        _locomotive.MaxSpeed = (_locomotive.MaxSpeed < GeneralSettings.MaxSpeedKph) ? _locomotive.MaxSpeed + inputSettings.speedIncreaseAmount : GeneralSettings.MaxSpeedKph;
                    else if (_railwayVehicle.TrainType == TrainType.SplineBased)
                        _locomotive.MaxSpeed = _locomotive.MaxSpeed + inputSettings.speedIncreaseAmount;
                }
                else if (Input.GetKeyDown(inputSettings.decreaseSpeed))
                    _locomotive.MaxSpeed = (_locomotive.MaxSpeed > GeneralSettings.MinSpeed) ? _locomotive.MaxSpeed - inputSettings.speedIncreaseAmount : GeneralSettings.MinSpeed;
                #endregion

                #region Default Train Events
                if (Input.GetKeyDown(inputSettings.lights))
                    _locomotive.ToggleLights();

                if (Input.GetKeyDown(inputSettings.internalLights))
                    _locomotive.ToggleInternalLights();

                if (Input.GetKeyDown(inputSettings.honk))
                    _locomotive.Honk();

                if (Input.GetKeyDown(inputSettings.bell))
                    _locomotive.ToogleBell();

                if (Input.GetKeyDown(inputSettings.toggleEngine))
                    _locomotive.ToggleEngine();

                if (Input.GetKeyDown(inputSettings.toggleEmergencyBrakes))
                    _locomotive.ToggleEmergencyBrakes();

                if (_doorController != null)
                {
                    if (Input.GetKeyDown(inputSettings.cabinLeftDoor))
                    {
                        if (_doorController.CabinLeftDoorOpen)
                            _doorController.CloseCabinDoorLeft();
                        else
                            _doorController.OpenCabinDoorLeft();
                    }

                    if (Input.GetKeyDown(inputSettings.cabinRightDoor))
                    {
                        if (_doorController.CabinRightDoorOpen)
                            _doorController.CloseCabinDoorRight();
                        else
                            _doorController.OpenCabinDoorRight();
                    }

                    if (Input.GetKeyDown(inputSettings.passengerLeftDoor))
                    {
                        if (_doorController.PassengerLeftDoorOpen)
                            _doorController.ClosePassengersLeftDoors();
                        else
                        {
                            _doorController.OpenPassengersDoors(StationDoorDirection.Left);
                        }
                    }

                    if (Input.GetKeyDown(inputSettings.passengerRightDoor))
                    {
                        if (_doorController.PassengerRightDoorOpen)
                            _doorController.ClosePassengersRightDoors();
                        else
                        {
                            _doorController.OpenPassengersDoors(StationDoorDirection.Right);
                        }
                    }
                }

                #endregion

                #region Player Custom Events
                for (int i = 0; i < inputSettings.customEventTriggers.Length; i++)
                {
                    if (Input.GetKeyDown(inputSettings.customEventTriggers[i]))
                    {
                        if (customEvents.Length > i)
                            customEvents[i].Invoke();
                    }
                }
                #endregion
            }
        }
    }
}
