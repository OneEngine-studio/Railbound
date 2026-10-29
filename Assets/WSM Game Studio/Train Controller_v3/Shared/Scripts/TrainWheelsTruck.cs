using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainWheelsTruck : MonoBehaviour
    {
        [SerializeField] private Transform _targetSteeringJoint;

        private Transform _transform;
        private Vector3 _targetRotation;
        private Vector3 _currentRotation;

        private float _speed = 1f;

        /// <summary>
        /// Start is called before the first frame update
        /// </summary>
        void Start()
        {
            _transform = GetComponent<Transform>();
            _targetRotation = Vector3.zero;
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        void Update()
        {
            UpdateWheelsTruckRotation();
        }

        /// <summary>
        /// Updates rotation to follow tracks
        /// </summary>
        private void UpdateWheelsTruckRotation()
        {
            if (_targetSteeringJoint != null)
            {
                //calculate new rotation
                _transform.rotation = Quaternion.RotateTowards(_transform.rotation, _targetSteeringJoint.rotation, _speed);

                //Lock x and z axis, only y axis needs to be updated
                _targetRotation = _transform.localEulerAngles;
                _targetRotation.x = 0f;
                _targetRotation.z = 0f;
                _transform.localEulerAngles = _targetRotation;
            }
        }
    }
}
