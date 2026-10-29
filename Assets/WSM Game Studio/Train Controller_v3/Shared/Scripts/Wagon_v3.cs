using System.Collections.Generic;
using UnityEngine;

namespace WSMGameStudio.RailroadSystem
{
    public class Wagon_v3 : MonoBehaviour, IRailwayVehicle
    {
        #region VARIABLES
        private TrainController_v3 _locomotive;
        private HingeJoint _carJoint;

        private Rigidbody _rigidbody;
        private Transform _transform;
        private bool _isGrounded;
        private float _maxSpeedMps = 65f;
        [Range(-1f, 1f)] private float _acceleration = 0f;
        [Range(0f, 1f)] private float _brake = 0f;
        private float _speed_MPS;
        private float _accelerationRate_MPS = 5f;
        private float _brakingDecelerationRate_MPS = 4.0f;
        private float _inertiaDecelerationRate_MPS = 2.0f;
        //private bool _shouldBeStatic = false;
        private bool _reverseAcceleration = false;
        private bool _locomotiveEngineOn = false;

        //Movement
        private Vector3 _targetVelocity;
        private float _targetSpeed;
        private float _currentSpeed;
        private float _wagonAccel;
        private Vector3 _localVelocity;
        //SFX
        private TrainAudio _trainAudio;

        public AudioSource wheelsSFX;
        public AudioSource wagonConnectionSFX;
        [Range(0f, 3f)]
        public float minWheelsPitch = 0.6f;
        [Range(0f, 3f)]
        public float maxWheelsPitch = 1.5f;
        public List<TrainWheel_v3> wheelsScripts;
        public Sensors sensors;
        public List<Light> externalLights;
        public List<Light> internalLights;
        public Rigidbody backJoint; //Must be rigibody for hinge connection
        public Rigidbody frontJoint;
        public Rigidbody jointAnchor;
        public WagonCoupling coupling;
        public WagonDecouplingSettings decouplingSettings;
        public float recouplingTimeout = 1f;
        #endregion

        #region PROPERTIES
        // Locomotive that commands this wagon
        public TrainController_v3 Locomotive { get { return _locomotive; } set { _locomotive = value; } }

        public bool IsConected { get { return _locomotive != null; } }
        public bool IsGrounded { get { return _isGrounded; } }
        public float MaxSpeedMps { get { return _maxSpeedMps; } set { _maxSpeedMps = value; } }
        public float Acceleration { get { return _acceleration; } set { _acceleration = value; } }
        public float AccelerationRate_MPS { get { return _accelerationRate_MPS; } set { _accelerationRate_MPS = Mathf.Abs(value); } }
        public float Brake { get { return _brake; } set { _brake = value; } }
        /// <summary>
        /// Identify if wagon was connected backwards and needs to move the oposite direction
        /// </summary>
        public bool ReverseAcceleration { get { return _reverseAcceleration; } set { _reverseAcceleration = value; } }

        // Joint for wagon connection
        public HingeJoint CarJoint { get { return _carJoint; } }
        public Rigidbody JointAnchor { get { return jointAnchor; } set { jointAnchor = value; } }
        public Rigidbody FrontJoint { get { return frontJoint; } set { frontJoint = value; } }
        public Rigidbody BackJoint { get { return backJoint; } set { backJoint = value; } }
        public List<TrainWheel_v3> Wheels { get { return wheelsScripts; } set { wheelsScripts = value; } }
        public AudioSource WheelsSFX { get { return wheelsSFX; } set { wheelsSFX = value; } }
        public AudioSource WagonConnectionSFX { get { return wagonConnectionSFX; } set { wagonConnectionSFX = value; } }
        public AudioSource BrakesSFX { get { return null; } set { } }
        public Sensors Sensors { get { return sensors; } set { sensors = value; } }
        public List<Light> ExternalLights { get { return externalLights; } set { externalLights = value; } }
        public List<Light> InternalLights { get { return internalLights; } set { internalLights = value; } }

        public GameObject GetGameObject { get { return gameObject; } }

        public TrainType TrainType { get { return TrainType.PhysicsBased; } }

        /// <summary>
        /// Distance between front and back couplers
        /// </summary>
        public float CouplersDistance
        {
            get
            {
                if (frontJoint == null || backJoint == null)
                {
                    Debug.LogError(string.Format("Couplers not set on {0}. Please manually set the couplers references and try again", gameObject.name.ToUpper()));
                    return 0f;
                }

                return Mathf.Abs(frontJoint.transform.localPosition.z) + Mathf.Abs(backJoint.transform.localPosition.z); ;
            }
        }

        /// <summary>
        ///  1 - moving forward
        ///  0 - not moving
        /// -1 - moving backwards
        /// </summary>
        public int LocalDirection
        {
            get
            {
                float localVel = _transform.InverseTransformDirection(_rigidbody.velocity).z;
                return localVel > 0f ? 1 : (localVel < 0f ? -1 : 0);
            }
        }

        #endregion

        #region UNITY EVENTS
        /// <summary>
        /// Initialie wagon
        /// </summary>
        private void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _transform = GetComponent<Transform>();
            _carJoint = GetComponent<HingeJoint>();
            InitializeSFX();
        }

        /// <summary>
        /// Physics
        /// </summary>
        void FixedUpdate()
        {
            EnforceAnchorPosition();
            UpdateVelocity();

            _isGrounded = sensors.leftSensor.Grounded || sensors.rightSensor.Grounded;
            _speed_MPS = _rigidbody.velocity.magnitude;
            _wagonAccel = _reverseAcceleration ? (_acceleration * (-1)) : _acceleration;
            _localVelocity = _transform.InverseTransformDirection(_rigidbody.velocity);

            TrainPhysics.UpdateWheels(wheelsScripts, _brake, _localVelocity.z);

            TrainPhysics.SpeedControl_PhysicsBased(_rigidbody, _locomotiveEngineOn, _isGrounded, _maxSpeedMps, _speed_MPS, _wagonAccel, _brake, _accelerationRate_MPS, _brakingDecelerationRate_MPS, _inertiaDecelerationRate_MPS, _targetVelocity, out _targetVelocity, _currentSpeed, out _currentSpeed, _targetSpeed, out _targetSpeed);

            _trainAudio.UpdateSFX(Speed.Convert_MPS_To_KPH(_speed_MPS), _brake, false, _isGrounded);
        }
        #endregion

        #region METHODS

        /// <summary>
        /// Initialize SFX
        /// </summary>
        private void InitializeSFX()
        {
            SFX sfx = new SFX();
            sfx.wheelsSFX = wheelsSFX;
            sfx.wagonConnectionSFX = wagonConnectionSFX;
            sfx.minWheelsPitch = minWheelsPitch;
            sfx.maxWheelsPitch = maxWheelsPitch;

            _trainAudio = new TrainAudio(sfx);
        }

        /// <summary>
        /// Turn external lights on/off
        /// </summary>
        public void ToggleLights()
        {
            if (externalLights == null)
                return;

            foreach (Light light in externalLights)
                light.enabled = !light.enabled;
        }

        /// <summary>
        /// Turn internal lights on/off
        /// </summary>
        public void ToggleInternalLights()
        {
            if (internalLights == null)
                return;

            foreach (Light light in internalLights)
                light.enabled = !light.enabled;
        }

        /// <summary>
        /// Connect wagon to train
        /// </summary>
        /// <param name="carCoupler">Current wagon coupler (front or back)</param>
        /// <param name="otherCarCoupler">Other wagon coupler</param>
        public void Connect(TrainCarCoupler carCoupler, TrainCarCoupler otherCarCoupler, bool playSFX)
        {
            if (coupling == WagonCoupling.Enabled)
            {
                if (otherCarCoupler.IsLocomotive)
                {
                    _locomotive = otherCarCoupler.Locomotive as TrainController_v3;
                    _reverseAcceleration = (carCoupler.IsBackJoint == otherCarCoupler.IsBackJoint);
                }
                else if (otherCarCoupler.IsWagon)
                {
                    if (!otherCarCoupler.Wagon.IsConected)
                        return;

                    _locomotive = otherCarCoupler.Wagon.Locomotive;
                    _reverseAcceleration = (carCoupler.IsBackJoint != otherCarCoupler.IsBackJoint) ? otherCarCoupler.Wagon.ReverseAcceleration : !otherCarCoupler.Wagon.ReverseAcceleration;
                }

                TrainPhysics.ConnectTrainCar(_carJoint, otherCarCoupler.GetComponent<Rigidbody>());
                _locomotive.WagonsScripts.Add(this);
                _locomotive.UpdateDoorController();

                if (playSFX && _trainAudio != null)
                    _trainAudio.PlayConnectionSFX();
            }
        }

        /// <summary>
        /// Disconnect wagon from train
        /// </summary>
        public void Disconnect(bool disconnectJoint)
        {
            if (disconnectJoint)
                _carJoint.connectedBody = jointAnchor;
            _locomotive = null;

            _trainAudio.PlayConnectionSFX();

            coupling = WagonCoupling.Disabled;

            if (decouplingSettings == WagonDecouplingSettings.AllowRecoupling)
            {
                Invoke("ReenabledCoupling", Mathf.Abs(recouplingTimeout));
            }
        }

        /// <summary>
        /// Reenable wagon coupling
        /// </summary>
        private void ReenabledCoupling()
        {
            coupling = WagonCoupling.Enabled;
        }

        /// <summary>
        /// Updates wagon velocity
        /// </summary>
        private void UpdateVelocity()
        {
            if (_locomotive != null)
            {
                _locomotiveEngineOn = _locomotive.EnginesOn;
                _acceleration = _locomotive.IsGrounded ? _locomotive.Acceleration : 0;
                _maxSpeedMps = _locomotive.IsGrounded ? _locomotive.MaxSpeed_MPS : 0;
                _brake = _locomotive.IsGrounded ? _locomotive.Brake : 0;
                _accelerationRate_MPS = _locomotive.IsGrounded ? _locomotive.AccelerationRate_MPS : _accelerationRate_MPS;
                _brakingDecelerationRate_MPS = _locomotive.IsGrounded ? _locomotive.BrakingDecelerationRate_MPS : _brakingDecelerationRate_MPS;
                _inertiaDecelerationRate_MPS = _locomotive.IsGrounded ? _locomotive.InertiaDecelerationRate_MPS : _inertiaDecelerationRate_MPS;
            }
            else
            {
                _locomotiveEngineOn = false;
                _acceleration = Mathf.MoveTowards(_acceleration, 0f, _inertiaDecelerationRate_MPS * Time.deltaTime);
                _maxSpeedMps = Mathf.MoveTowards(_maxSpeedMps, 0f, _inertiaDecelerationRate_MPS * Time.deltaTime);
                _brake = 1f;
                _targetVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Always keeps anchor in the right place
        /// </summary>
        private void EnforceAnchorPosition()
        {
            if (jointAnchor != null)
            {
                jointAnchor.transform.localPosition = _carJoint.anchor;
                jointAnchor.transform.localRotation = Quaternion.identity;
            }
        }
        #endregion
    }
}
