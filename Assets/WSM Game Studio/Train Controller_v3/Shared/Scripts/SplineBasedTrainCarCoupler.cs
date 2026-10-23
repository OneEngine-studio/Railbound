using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class SplineBasedTrainCarCoupler : MonoBehaviour, ITrainCarCoupler
    {
        public CarCouplerType couplerType;

        private SplineBasedLocomotive _locomotive;
        private SplineBasedWagon _wagon;

        private bool _isLocomotive = false;
        private bool _isWagon = false;

        public SplineBasedLocomotive Locomotive { get { return _locomotive; } }
        public SplineBasedWagon Wagon { get { return _wagon; } }

        public bool IsBackJoint { get { return (couplerType == CarCouplerType.BackCoupler); } }
        public bool IsLocomotive { get { return _isLocomotive; } }
        public bool IsWagon { get { return _isWagon; } }

        private void OnEnable()
        {
            // Parent must be always wagon or locomotive
            _locomotive = this.transform.parent.GetComponent<SplineBasedLocomotive>();
            _wagon = this.transform.parent.GetComponent<SplineBasedWagon>();

            _isLocomotive = (_locomotive != null);
            _isWagon = (_wagon != null);
        }

        private void OnTriggerStay(Collider other)
        {
            ConnectOnCollision(other, false);
        }

        private void OnTriggerEnter(Collider other)
        {
            ConnectOnCollision(other, true);
        }

        /// <summary>
        /// Connect wagons on couplers collision
        /// </summary>
        /// <param name="other"></param>
        private void ConnectOnCollision(Collider other, bool playSFX)
        {
            // Only wagons connect
            if (!_isWagon) return;
            // Ignore already connected wagons 
            if (_wagon.IsConected) return;

            SplineBasedTrainCarCoupler otherCarCoupler = other.GetComponent<SplineBasedTrainCarCoupler>();

            if (otherCarCoupler != null)
            {
                _wagon.Connect(this, otherCarCoupler, playSFX);
            }
        }
    } 
}
