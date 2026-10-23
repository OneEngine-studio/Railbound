using System.Collections.Generic;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class PassengerTags : MonoBehaviour
    {
        /// <summary>
        /// Set passengers to kinematic while moving
        /// </summary>
        public bool kinematicWhileMoving = false;

        /// <summary>
        /// Object tags for attaching passengers
        /// </summary>
        public List<string> passengerTags;

        private ILocomotive _locomotive;

        // Use this for initialization
        void Start()
        {
            _locomotive = GetComponent<ILocomotive>();
            UpdateWagonsPassengerTags();
        }

        /// <summary>
        /// Updade wagons passenger tags
        /// </summary>
        private void UpdateWagonsPassengerTags()
        {
            SetPassengerTags(this.gameObject);

            //If null wagon script is attached to wagon
            if (_locomotive == null)
                return;

            if (_locomotive.ConnectedWagons == null)
                return;

            foreach (var wagon in _locomotive.ConnectedWagons)
            {
                SetPassengerTags(wagon.gameObject);
            }
        }

        /// <summary>
        /// Set passenger tags
        /// </summary>
        /// <param name="wagon"></param>
        private void SetPassengerTags(GameObject wagon)
        {
            TrainAttachPassenger trainAttachPassenger = wagon.GetComponentInChildren<TrainAttachPassenger>();

            if (trainAttachPassenger != null)
            {
                trainAttachPassenger.PassengerTags = passengerTags;
                trainAttachPassenger.KinematicWhileMoving = kinematicWhileMoving;
            }
        }
    } 
}
