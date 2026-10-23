using System.Collections.Generic;
using UnityEngine;
using WSMGameStudio.Splines;

namespace WSMGameStudio.RailroadSystem
{
    public class TrainController_v3 : MonoBehaviour, ILocomotive, IRailwayVehicle
    {
        #region VARIABLES
        private Rigidbody _rigidbody;
        private Transform _transform;
        private bool _isGrounded = false;
        private bool _onRails = false;
        private float _speed_MPS;
        private float _speed_KPH;
        private float _speed_MPH;
        //Movement
        private float _currentMaxSpeed = 0f;
        private float _targetSpeed;
        private float _currentSpeed;
        private Vector3 _targetVelocity;
        private Vector3 _localVelocity;
        //SFX & VFX
        private TrainAudio _trainAudio;
        private TrainParticles _trainParticles;
        //Doors
        private ITrainDoorsController _doorController;

        //Input
        [SerializeField] private bool enginesOn = false;
        [SerializeField] private float maxSpeed = 70f;
        [SerializeField] private SpeedUnits speedUnit;
        [Range(-1f, 1f)] [SerializeField] private float acceleration = 0f;
        [SerializeField] private bool automaticBrakes = true;
        [Range(0f, 1f)] [SerializeField] private float brake = 0f;
        [SerializeField] private bool emergencyBrakes = false;
        [SerializeField] private float accelerationRate = 5f;
        [SerializeField] private float brakingDecelerationRate = 5f;
        [SerializeField] private float inertiaDecelerationRate = 3f;

        [SerializeField] private List<TrainWheel_v3> wheelsScripts;
        [SerializeField] private Sensors sensors;
        [SerializeField] private List<Wagon_v3> wagons;
        [SerializeField] private List<Light> externalLights;
        [SerializeField] private List<Light> internalLights;
        [SerializeField] private AudioSource hornSFX;
        [SerializeField] private AudioSource bellSFX;
        [SerializeField] private AudioSource engineSFX;
        [SerializeField] private AudioSource wheelsSFX;
        [SerializeField] private AudioSource brakesSFX;
        [Range(0f, 3f)] [SerializeField] private float idleEnginePitch = 0.7f;
        [Range(0f, 3f)] [SerializeField] private float maxEnginePitch = 1f;
        [Range(0f, 3f)] [SerializeField] private float minWheelsPitch = 0.6f;
        [Range(0f, 3f)] [SerializeField] private float maxWheelsPitch = 1.5f;
        [SerializeField] private bool enableSmoke = true;
        [SerializeField] private bool enableBrakingSparks = true;
        [SerializeField] private float minSmokeEmission = 2f;
        [SerializeField] private float maxSmokeEmission = 60f;
        [SerializeField] private ParticleSystem smokeParticles;
        [SerializeField] private ParticleSystem[] brakingSparksParticles;
        [SerializeField] private Rigidbody frontJoint;
        [SerializeField] private Rigidbody backJoint;
        [SerializeField] private Animator bell;

        #endregion

        #region PROPERTIES

        public float Speed_MPS { get { return _speed_MPS; } }
        public float Speed_KPH { get { return _speed_KPH; } }
        public float Speed_MPH { get { return _speed_MPH; } }

        public bool IsGrounded { get { return _isGrounded; } set { _isGrounded = value; } }
        public bool OnRails { get { return _onRails; } }

        public ITrainDoorsController DoorsController
        {
            get
            {
                if (_doorController == null)
                    _doorController = GetComponent<ITrainDoorsController>();

                return _doorController;
            }
        }

        public bool BellOn { get { return _trainAudio.BellOn; } }

        public Rigidbody JointAnchor { get { return null; } set { } }
        public Rigidbody FrontJoint { get { return frontJoint; } set { frontJoint = value; } }
        public Rigidbody BackJoint { get { return backJoint; } set { backJoint = value; } }
        public List<TrainWheel_v3> Wheels { get { return wheelsScripts; } set { wheelsScripts = value; } }
        public AudioSource WheelsSFX { get { return wheelsSFX; } set { wheelsSFX = value; } }
        public AudioSource EngineSFX { get { return engineSFX; } set { engineSFX = value; } }
        public AudioSource HornSFX { get { return hornSFX; } set { hornSFX = value; } }
        public AudioSource BellSFX { get { return bellSFX; } set { bellSFX = value; } }
        public AudioSource BrakesSFX { get { return brakesSFX; } set { brakesSFX = value; } }
        public AudioSource WagonConnectionSFX { get { return null; } set { } }
        public Sensors Sensors { get { return sensors; } set { sensors = value; } }
        public List<Light> ExternalLights { get { return externalLights; } set { externalLights = value; } }
        public List<Light> InternalLights { get { return internalLights; } set { internalLights = value; } }

        public bool EnginesOn { get { return enginesOn; } set { enginesOn = value; } }
        public float Acceleration { get { return acceleration; } set { acceleration = value; } }
        public bool AutomaticBrakes { get { return automaticBrakes; } set { automaticBrakes = value; } }
        public float Brake { get { return brake; } set { brake = value; } }
        public bool EmergencyBrakes { get { return emergencyBrakes; } set { emergencyBrakes = value; } }
        public float MaxSpeed { get { return maxSpeed; } set { maxSpeed = EnforceSpeedLimit(value); } }
        public SpeedUnits SpeedUnit { get { return speedUnit; } set { speedUnit = value; } }
        public float AccelerationRate { get { return accelerationRate; } set { accelerationRate = Mathf.Abs(value); } }
        public float BrakingDecelerationRate { get { return brakingDecelerationRate; } set { brakingDecelerationRate = Mathf.Abs(value); } }
        public float InertiaDecelerationRate { get { return inertiaDecelerationRate; } set { inertiaDecelerationRate = Mathf.Abs(value); } }

        /// <summary>
        /// Max speed at meters per second
        /// </summary>
        public float MaxSpeed_MPS { get { return speedUnit == SpeedUnits.kph ? Speed.Convert_KPH_To_MPS(_currentMaxSpeed) : Speed.Convert_MPH_To_MPS(_currentMaxSpeed); } }
        public float AccelerationRate_MPS { get { return speedUnit == SpeedUnits.kph ? Speed.Convert_KPH_To_MPS(accelerationRate) : Speed.Convert_MPH_To_MPS(accelerationRate); } }
        public float BrakingDecelerationRate_MPS { get { return speedUnit == SpeedUnits.kph ? Speed.Convert_KPH_To_MPS(brakingDecelerationRate) : Speed.Convert_MPH_To_MPS(brakingDecelerationRate); } }
        public float InertiaDecelerationRate_MPS { get { return speedUnit == SpeedUnits.kph ? Speed.Convert_KPH_To_MPS(inertiaDecelerationRate) : Speed.Convert_MPH_To_MPS(inertiaDecelerationRate); } }

        public ParticleSystem SmokeParticles { get { return smokeParticles; } set { smokeParticles = value; } }
        public ParticleSystem[] BrakingSparksParticles { get { return brakingSparksParticles; } set { brakingSparksParticles = value; } }
        public Animator BellAnimator { get { return bell; } set { bell = value; } }

        public GameObject GetGameObject { get { return gameObject; } }

        public TrainType TrainType { get { return TrainType.PhysicsBased; } }

        public List<Wagon_v3> WagonsScripts { get { return wagons; } set { wagons = value; } }

        public List<GameObject> ConnectedWagons
        {
            get
            {
                List<GameObject> temp = new List<GameObject>();
                foreach (var item in wagons)
                {
                    temp.Add(item.gameObject);
                }

                return temp;
            }
        }

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
        /// Initialize train
        /// </summary>
        void Start()
        {
            _currentMaxSpeed = maxSpeed;

            _rigidbody = GetComponent<Rigidbody>();
            _transform = GetComponent<Transform>();
            _doorController = GetComponent<ITrainDoorsController>();
            ConnectWagons();
            InitializeSFX();
            InitializeVFX();
        }

        /// <summary>
        /// Train physics
        /// </summary>
        void FixedUpdate()
        {
            _currentMaxSpeed = TrainPhysics.CalculateCurrentMaxSpeed(_currentMaxSpeed, maxSpeed, accelerationRate, brakingDecelerationRate, inertiaDecelerationRate);

            brake = automaticBrakes ? 1f - Mathf.Abs(acceleration) : brake;

            _isGrounded = sensors.leftSensor.Grounded || sensors.rightSensor.Grounded;
            _onRails = sensors.leftSensor.OnRails || sensors.rightSensor.OnRails;
            _speed_MPS = _rigidbody.velocity.magnitude;
            _speed_MPH = Speed.Convert_MPS_To_MPH(_speed_MPS);
            _speed_KPH = Speed.Convert_MPS_To_KPH(_speed_MPS);

            _trainAudio.UpdateSFX(_speed_KPH, brake, enginesOn, _isGrounded);
            _trainParticles.UpdateVFX(_speed_KPH, acceleration, brake, enginesOn, enableSmoke, enableBrakingSparks, LocalDirection);

            _localVelocity = _transform.InverseTransformDirection(_rigidbody.velocity);

            if (!enginesOn)
            {
                brake = automaticBrakes ? 1f : brake;
            }

            if (emergencyBrakes)
            {
                acceleration = 0f;
                brake = 1f;
            }

            TrainPhysics.UpdateWheels(wheelsScripts, brake, _localVelocity.z);

            TrainPhysics.SpeedControl_PhysicsBased(_rigidbody, enginesOn, _isGrounded, MaxSpeed_MPS, _speed_MPS, acceleration, brake, AccelerationRate_MPS, BrakingDecelerationRate_MPS, InertiaDecelerationRate_MPS, _targetVelocity, out _targetVelocity, _currentSpeed, out _currentSpeed, _targetSpeed, out _targetSpeed);
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Connect wagons hinges
        /// </summary>
        private void ConnectWagons()
        {
            for (int i = 0; i < wagons.Count; i++)
            {
                if (i == 0) // Connect wagon to locomotive
                    TrainPhysics.ConnectTrainCar(wagons[i].GetComponent<HingeJoint>(), backJoint);
                else // Connect wagon to wagon
                    TrainPhysics.ConnectTrainCar(wagons[i].GetComponent<HingeJoint>(), wagons[i - 1].backJoint);

                wagons[i].Locomotive = this;
            }
        }

        /// <summary>
        /// Initialize SFX
        /// </summary>
        private void InitializeSFX()
        {
            SFX sfx = new SFX();
            sfx.hornSFX = hornSFX;
            sfx.bellSFX = bellSFX;
            sfx.engineSFX = engineSFX;
            sfx.wheelsSFX = wheelsSFX;
            sfx.brakesSFX = brakesSFX;
            sfx.idleEnginePitch = idleEnginePitch;
            sfx.maxEnginePitch = maxEnginePitch;
            sfx.minWheelsPitch = minWheelsPitch;
            sfx.maxWheelsPitch = maxWheelsPitch;

            _trainAudio = new TrainAudio(sfx);
        }

        /// <summary>
        /// Initialize VFX
        /// </summary>
        private void InitializeVFX()
        {
            VFX vfx = new VFX();
            vfx.smokeParticles = smokeParticles;
            vfx.minSmokeEmission = Mathf.Abs(minSmokeEmission);
            vfx.maxSmokeEmission = Mathf.Abs(maxSmokeEmission);
            vfx.brakingSparksParticles = brakingSparksParticles;

            _trainParticles = new TrainParticles(vfx);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Automatically calculates wagons initial position on the train
        /// </summary>
        public void CalculateWagonsPositions()
        {
            if (backJoint == null)
            {
                Debug.LogWarning("Locomotive rear coupler cannot be null");
                return;
            }

            _transform = _transform == null ? GetComponent<Transform>() : _transform;
            List<IRailwayVehicle> targetwagons = new List<IRailwayVehicle>();

            foreach (var item in wagons)
            {
                targetwagons.Add(item);
            }

            TrainPhysics.CalculateWagonsPositions(_transform, targetwagons, backJoint.transform);
        }

        /// <summary>
        /// Move locomotive to start position and automatically calculates wagons initial positions
        /// </summary>
        /// <param name="targetRails"></param>
        public void CalculateWagonsPositions(List<Spline> targetRails)
        {
            CalculateWagonsPositions();
        }

        /// <summary>
        /// Update door controller wagons references
        /// </summary>
        public void UpdateDoorController()
        {
            if (_doorController != null)
                _doorController.UpdateWagonsDoorsControllers();
        }

        /// <summary>
        /// Disconnect last wagon
        /// </summary>
        public void DecoupleLastWagon()
        {
            if (wagons == null || wagons.Count == 0)
                return;

            DecoupleWagon(wagons.Count - 1);
        }

        /// <summary>
        /// Disconnect first wagons connected to the locomotive
        /// </summary>
        public void DecoupleFirstWagon()
        {
            DecoupleWagon(0);
        }

        /// <summary>
        /// Diconnect wagon by index
        /// </summary>
        /// <param name="index"></param>
        public void DecoupleWagon(int index)
        {
            if (wagons == null || index > wagons.Count - 1)
                return;

            for (int i = (wagons.Count - 1); i >= index; i--)
            {
                wagons[i].Disconnect((i == index));
                wagons.RemoveAt(i);
            }

            UpdateDoorController();
        }

        /// <summary>
        /// Turn external lights on/off
        /// </summary>
        public void ToggleLights()
        {
            if (externalLights != null)
            {
                foreach (Light light in externalLights)
                    light.enabled = !light.enabled;
            }

            if (wagons != null)
            {
                foreach (var wagon in wagons)
                    wagon.ToggleLights();
            }
        }

        /// <summary>
        /// Turn internal lights on/off
        /// </summary>
        public void ToggleInternalLights()
        {
            if (internalLights != null)
            {
                foreach (Light light in internalLights)
                    light.enabled = !light.enabled;
            }

            if (wagons != null)
            {
                foreach (var wagon in wagons)
                    wagon.ToggleInternalLights();
            }
        }

        /// <summary>
        ///  Toggle engine on/off
        /// </summary>
        public void ToggleEngine()
        {
            enginesOn = !enginesOn;
        }

        /// <summary>
        /// Toggle emergency brakes on/off
        /// </summary>
        public void ToggleEmergencyBrakes()
        {
            emergencyBrakes = !emergencyBrakes;
        }

        /// <summary>
        /// play the train horn
        /// </summary>
        public void Honk()
        {
            _trainAudio.Honk();
        }

        /// <summary>
        /// Toggle train security bell
        /// </summary>
        public void ToogleBell()
        {
            _trainAudio.ToogleBell();

            if (bell != null)
                bell.SetBool(AnimationParameters.BellPlaying, _trainAudio.BellOn);
        }

        /// <summary>
        /// Enforce max speed value based on selected speed unit
        /// </summary>
        /// <param name="speedLimit"></param>
        /// <returns></returns>
        public float EnforceSpeedLimit(float speedLimit)
        {
            speedLimit = Mathf.Abs(speedLimit);

            switch (speedUnit)
            {
                case SpeedUnits.kph:
                    speedLimit = speedLimit > GeneralSettings.MaxSpeedKph ? GeneralSettings.MaxSpeedKph : speedLimit;
                    break;
                case SpeedUnits.mph:
                    speedLimit = speedLimit > GeneralSettings.MaxSpeedMph ? GeneralSettings.MaxSpeedMph : speedLimit;
                    break;
            }

            return speedLimit;
        }

        /// <summary>
        /// Add new wagons to train
        /// </summary>
        /// <param name="newWagons"></param>
        public void AddWagons(List<GameObject> newWagons)
        {
            wagons = (wagons == null) ? new List<Wagon_v3>() : wagons;

            foreach (var item in newWagons)
            {
                Wagon_v3 wagonScript = item.GetComponent<Wagon_v3>();
                if (wagonScript == null) continue;
                wagons.Add(wagonScript);
            }
        }

        /// <summary>
        /// Remove all wagons references (don't disconnect them)
        /// </summary>
        public void RemoveAllWagons()
        {
            wagons = null;
        }

        /// <summary>
        /// Move train to target route
        /// </summary>
        /// <param name="newRoute"></param>
        public void AssignRoute(Route newRoute, float t = 0.5f)
        {
            if (newRoute.Splines == null || newRoute.Splines[0] == null)
                return;

            _transform = _transform == null ? GetComponent<Transform>() : _transform;

            Vector3 targetPosition = newRoute.Splines[0].GetPoint(t);
            Vector3 lookTarget = targetPosition + newRoute.Splines[0].GetDirection(t);

            this.gameObject.SetActive(false); //Deactivate before moving to avoid physics engine miscalculations
            _transform.position = targetPosition;
            _transform.LookAt(lookTarget);
            this.gameObject.SetActive(true); //Reactivate after moving

            CalculateWagonsPositions();
        }

        #endregion
    }
}
