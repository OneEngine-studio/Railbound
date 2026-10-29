using UnityEngine;
using UnityEngine.Serialization;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainWheel_v3 : MonoBehaviour
    {
        [Tooltip("Visual wheels don't use physics and applied animated rotation instead")]
        [FormerlySerializedAs("optimized")]
        public bool visualWheels = false;
        [Tooltip("Wheel radius")]
        public float radius = 0.5f;
        
        private float _speed = 0f;
        [Range(0f, 1f)] private float _brake = 0f;
        private float _brakingDecelerationRate = 2f;
        private Rigidbody _rigidbody;
        private Transform _transform;

        private float _angularSpeed;
        private const float _radDegressConversion = 57.2958f;

        public float Brake
        {
            get { return _brake; }
            set { _brake = value; }
        }

        public float Speed
        {
            get { return _speed; }
            set { _speed = value; }
        }

        public float BrakingDecelerationRate
        {
            get { return _brakingDecelerationRate; }
            set { _brakingDecelerationRate = value; }
        }

        /// <summary>
        /// Initialize wheel
        /// </summary>
        void Start()
        {
            _transform = GetComponent<Transform>();
            _rigidbody = GetComponent<Rigidbody>();

            if (!visualWheels)
            {
                _rigidbody.maxAngularVelocity = GeneralSettings.WheelsMaxAngularVelocity;
                _rigidbody.angularDrag = GeneralSettings.IdleDrag;
            }
        }

        /// <summary>
        /// Fixed update
        /// </summary>
        void FixedUpdate()
        {
            if (!visualWheels)
            {
                TrainPhysics.ApplyBrakes_PhysicsBasedWheels(_rigidbody, _brake, _brakingDecelerationRate, 0f);
            }
        }

        /// <summary>
        /// Update
        /// </summary>
        private void Update()
        {
            if (visualWheels)
            {
                _angularSpeed = (radius != 0f) ? (_speed / radius) * _radDegressConversion * Time.deltaTime : _speed;
                _transform.Rotate(_angularSpeed, 0f, 0f, Space.Self);
            }
        }
    }
}
