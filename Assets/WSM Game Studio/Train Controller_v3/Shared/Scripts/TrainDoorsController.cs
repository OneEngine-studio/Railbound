using System.Collections.Generic;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainDoorsController : MonoBehaviour, ITrainDoorsController
    {
        private bool _cabinLeftDoorOpen = false;
        private bool _cabinRightDoorOpen = false;
        private bool _passengerLeftDoorOpen = false;
        private bool _passengerRightDoorOpen = false;

        private StationDoorDirection _stationDoorDirection;
        private ILocomotive _locomotive;
        private List<TrainDoorsController> _wagonsDoorsControllers;

        public AudioSource openCabinDoorSFX;
        public AudioSource closeCabinDoorSFX;
        public AudioSource openPassengerDoorSFX;
        public AudioSource closePassengerDoorSFX;
        public AudioSource closeDoorsWarningSFX;

        public TrainDoor cabinDoorLeft;
        public TrainDoor cabinDoorRight;
        public List<TrainDoor> passengerDoorsLeft;
        public List<TrainDoor> passengerDoorsRight;

        public StationDoorDirection StationDoorDirection
        {
            get { return _stationDoorDirection; }
            set
            {
                _stationDoorDirection = value;
                UpdateWagonDoorsDirection();
            }
        }

        public bool CabinLeftDoorOpen { get { return _cabinLeftDoorOpen; } }
        public bool CabinRightDoorOpen { get { return _cabinRightDoorOpen; } }
        public bool PassengerLeftDoorOpen { get { return _passengerLeftDoorOpen; } }
        public bool PassengerRightDoorOpen { get { return _passengerRightDoorOpen; } }

        private void Start()
        {
            _locomotive = GetComponent<ILocomotive>();
            UpdateWagonsDoorsControllers();
        }

        #region Public Methods

        /// <summary>
        /// Open left cabin door
        /// </summary>
        public void OpenCabinDoorLeft()
        {
            if (!_cabinLeftDoorOpen)
                _cabinLeftDoorOpen = OpenDoor(cabinDoorLeft, openCabinDoorSFX);
        }

        /// <summary>
        /// Open right cabin door
        /// </summary>
        public void OpenCabinDoorRight()
        {
            if (!_cabinRightDoorOpen)
                _cabinRightDoorOpen = OpenDoor(cabinDoorRight, openCabinDoorSFX);
        }

        /// <summary>
        /// Close left cabin door
        /// </summary>
        public void CloseCabinDoorLeft()
        {
            if (_cabinLeftDoorOpen)
                _cabinLeftDoorOpen = !CloseDoor(cabinDoorLeft, closeCabinDoorSFX);
        }

        /// <summary>
        /// Close right cabin door
        /// </summary>
        public void CloseCabinDoorRight()
        {
            if (_cabinRightDoorOpen)
                _cabinRightDoorOpen = !CloseDoor(cabinDoorRight, closeCabinDoorSFX);
        }

        public void OpenPassengersDoors(StationDoorDirection doorsDiretion)
        {
            _stationDoorDirection = doorsDiretion;
            OpenPassengersDoors();
        }

        /// <summary>
        /// Open passengers doors taking in consideration the station door direction
        /// </summary>
        public void OpenPassengersDoors()
        {
            switch (_stationDoorDirection)
            {
                case StationDoorDirection.BothSides:
                    if (!_passengerLeftDoorOpen) _passengerLeftDoorOpen = OpenDoor(passengerDoorsLeft, openPassengerDoorSFX);
                    if (!_passengerRightDoorOpen) _passengerRightDoorOpen = OpenDoor(passengerDoorsRight, openPassengerDoorSFX);
                    break;
                case StationDoorDirection.Left:
                    if (!_passengerLeftDoorOpen) _passengerLeftDoorOpen = OpenDoor(passengerDoorsLeft, openPassengerDoorSFX);
                    break;
                case StationDoorDirection.Right:
                    if (!_passengerRightDoorOpen) _passengerRightDoorOpen = OpenDoor(passengerDoorsRight, openPassengerDoorSFX);
                    break;
            }

            if (_wagonsDoorsControllers == null)
                return;

            foreach (var item in _wagonsDoorsControllers)
            {
                item.OpenPassengersDoors();
                _passengerLeftDoorOpen = item.PassengerLeftDoorOpen;
                _passengerRightDoorOpen = item.PassengerRightDoorOpen;
            }
        }

        /// <summary>
        /// Close passengers doors on BOTH sides
        /// </summary>
        public void ClosePassengersDoors()
        {
            if (_passengerLeftDoorOpen) _passengerLeftDoorOpen = !CloseDoor(passengerDoorsLeft, closePassengerDoorSFX);
            if (_passengerRightDoorOpen) _passengerRightDoorOpen = !CloseDoor(passengerDoorsRight, closePassengerDoorSFX);

            if (_wagonsDoorsControllers == null)
                return;

            foreach (var item in _wagonsDoorsControllers)
            {
                item.ClosePassengersDoors();
                _passengerLeftDoorOpen = item.PassengerLeftDoorOpen;
                _passengerRightDoorOpen = item.PassengerRightDoorOpen;
            }
        }

        /// <summary>
        /// Close left passengers doors
        /// </summary>
        public void ClosePassengersLeftDoors()
        {
            if (_passengerLeftDoorOpen) _passengerLeftDoorOpen = !CloseDoor(passengerDoorsLeft, closePassengerDoorSFX);

            if (_wagonsDoorsControllers == null)
                return;

            foreach (var item in _wagonsDoorsControllers)
            {
                item.ClosePassengersLeftDoors();
                _passengerLeftDoorOpen = item.PassengerLeftDoorOpen;
            }
        }

        /// <summary>
        /// Close right passengers doors
        /// </summary>
        public void ClosePassengersRightDoors()
        {
            if (_passengerRightDoorOpen) _passengerRightDoorOpen = !CloseDoor(passengerDoorsRight, closePassengerDoorSFX);

            if (_wagonsDoorsControllers == null)
                return;

            foreach (var item in _wagonsDoorsControllers)
            {
                item.ClosePassengersRightDoors();
                _passengerRightDoorOpen = item.PassengerRightDoorOpen;
            }
        }

        /// <summary>
        /// Update wagons doors controllers list, if the script is attached to a locomotive
        /// </summary>
        public void UpdateWagonsDoorsControllers()
        {
            //If null wagon script is attached to wagon
            if (_locomotive == null)
                return;

            _wagonsDoorsControllers = new List<TrainDoorsController>();

            if (_locomotive.ConnectedWagons == null)
                return;

            foreach (var wagon in _locomotive.ConnectedWagons)
            {
                TrainDoorsController doorController = wagon.GetComponent<TrainDoorsController>();

                if (doorController != null)
                    _wagonsDoorsControllers.Add(doorController);
            }
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Updates wagons station doors direction
        /// </summary>
        private void UpdateWagonDoorsDirection()
        {
            if (_wagonsDoorsControllers == null)
                return;

            foreach (var item in _wagonsDoorsControllers)
            {
                item.StationDoorDirection = _stationDoorDirection;
            }
        }

        /// <summary>
        /// Open door and play SFX
        /// </summary>
        /// <param name="door"></param>
        /// <param name="openSFX"></param>
        /// <returns></returns>
        private bool OpenDoor(TrainDoor door, AudioSource openSFX)
        {
            if (door == null)
                return false;

            bool opened = door.Open();

            if (opened)
                PlayDoorSFX(openSFX);

            return opened;
        }

        /// <summary>
        /// Open door
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        private bool OpenDoor(TrainDoor door)
        {
            if (door == null)
                return false;

            return door.Open();
        }

        /// <summary>
        /// Open doors
        /// </summary>
        /// <param name="doors"></param>
        private bool OpenDoor(List<TrainDoor> doors, AudioSource openSFX)
        {
            if (doors == null)
                return false;

            bool opened = false;
            foreach (var door in doors)
                opened = OpenDoor(door);

            if (opened)
                PlayDoorSFX(openSFX);

            return opened;
        }

        /// <summary>
        /// Close door and play SFX
        /// </summary>
        /// <param name="door"></param>
        /// <param name="closeSFX"></param>
        /// <returns></returns>
        private bool CloseDoor(TrainDoor door, AudioSource closeSFX)
        {
            if (door == null)
                return false;

            bool closed = door.Close();

            if (closed)
                PlayDoorSFX(closeSFX);

            return closed;
        }

        /// <summary>
        /// Close door
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        private bool CloseDoor(TrainDoor door)
        {
            if (door == null)
                return false;

            return door.Close();
        }

        /// <summary>
        /// Close doors
        /// </summary>
        /// <param name="doors"></param>
        private bool CloseDoor(List<TrainDoor> doors, AudioSource closeSFX)
        {
            if (doors == null)
                return false;

            bool closed = false;
            foreach (var door in doors)
                closed = CloseDoor(door);

            if (closed)
                PlayDoorSFX(closeSFX);

            return closed;
        }

        /// <summary>
        /// Close door warning
        /// </summary>
        public void CloseDoorWarning()
        {
            PlayDoorSFX(closeDoorsWarningSFX);

            if (_wagonsDoorsControllers == null)
                return;

            foreach (var item in _wagonsDoorsControllers)
            {
                item.CloseDoorWarning();
            }
        }

        /// <summary>
        /// Play door SFX
        /// </summary>
        private void PlayDoorSFX(AudioSource sfx)
        {
            if (sfx != null)
                sfx.Play();
        }

        #endregion
    }
}
