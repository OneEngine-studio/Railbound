using WSMGameStudio.Splines;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

namespace WSMGameStudio.RailroadSystem
{
    public class SplineBasedLocomotive : SplineFollower, ILocomotive, IRailwayVehicle
    {
        #region VARIABLES

        //Input
        [SerializeField] private bool _enginesOn = false;
        [SerializeField] private float _maxSpeed = 70f;
        [SerializeField] private SpeedUnits _speedUnit;
        [Range(-1f, 1f)] [SerializeField] private float _acceleration = 0f;
        [SerializeField] private bool _automaticBrakes = true;
        [Range(0f, 1f)] [SerializeField] private float _brake = 0f;
        [SerializeField] private bool _emergencyBrakes = false;
        [SerializeField] private float _accelerationRate = 5f;
        [SerializeField] private float _brakingDecelerationRate = 5f;
        [SerializeField] private float _inertiaDecelerationRate = 3f;
        //Mechanical parts
        [SerializeField] private List<TrainWheel_v3> _wheelsScripts;
        //Lighs
        [SerializeField] private List<Light> _externalLights;
        [SerializeField] private List<Light> _internalLights;
        //SFX
        [SerializeField] private AudioSource _hornSFX;
        [SerializeField] private AudioSource _bellSFX;
        [SerializeField] private AudioSource _engineSFX;
        [SerializeField] private AudioSource _wheelsSFX;
        [SerializeField] private AudioSource _brakesSFX;
        [Range(0f, 3f)] [SerializeField] private float _idleEnginePitch = 0.7f;
        [Range(0f, 3f)] [SerializeField] private float _maxEnginePitch = 1f;
        [Range(0f, 3f)] [SerializeField] private float _minWheelsPitch = 0.6f;
        [Range(0f, 3f)] [SerializeField] private float _maxWheelsPitch = 1.5f;
        //Particles
        [SerializeField] private bool _enableSmoke = true;
        [SerializeField] private bool _enableBrakingSparks = true;
        [SerializeField] private float _minSmokeEmission = 2f;
        [SerializeField] private float _maxSmokeEmission = 60f;
        [SerializeField] private ParticleSystem _smokeParticles;
        [SerializeField] private ParticleSystem[] _brakingSparksParticles;
        [SerializeField] private Rigidbody _backJoint;
        [SerializeField] private Rigidbody _frontJoint;
        [SerializeField] private UnityEvent _onReachedEndOfRoute;
        //Debbuging
        [SerializeField] private bool _visualizeRouteOnEditor = false;
        //Bell
        [SerializeField] private Animator _bell;

        //Movement
        private float _currentMaxSpeed = 0f;
        private float _targetSpeed = 0f;
        private float _currentSpeed = 0f;
        //SFX & VFX
        private TrainAudio _trainAudio;
        private TrainParticles _trainParticles;
        //Doors
        private ITrainDoorsController _doorController;

        #endregion

        #region PROPERTIES

        public bool EnginesOn { get { return _enginesOn; } set { _enginesOn = value; } }
        public float Acceleration { get { return _acceleration; } set { _acceleration = value; } }
        public bool AutomaticBrakes { get { return _automaticBrakes; } set { _automaticBrakes = value; } }
        public float Brake { get { return _brake; } set { _brake = value; } }
        public bool EmergencyBrakes { get { return _emergencyBrakes; } set { _emergencyBrakes = value; } }
        public float MaxSpeed { get { return _maxSpeed; } set { _maxSpeed = Mathf.Abs(value); } }
        public SpeedUnits SpeedUnit { get { return _speedUnit; } set { _speedUnit = value; } }
        public float AccelerationRate { get { return _accelerationRate; } set { _accelerationRate = Mathf.Abs(value); } }
        public float BrakingDecelerationRate { get { return _brakingDecelerationRate; } set { _brakingDecelerationRate = Mathf.Abs(value); } }
        public float InertiaDecelerationRate { get { return _inertiaDecelerationRate; } set { _inertiaDecelerationRate = Mathf.Abs(value); } }

        public float Speed_MPS { get { return _currentSpeed; } }
        public float Speed_KPH { get { return Speed.Convert_MPS_To_KPH(base.GetConvertedSpeedUnit()); } }
        public float Speed_MPH { get { return Speed.Convert_MPS_To_MPH(base.GetConvertedSpeedUnit()); } }

        public bool BellOn { get { return _trainAudio.BellOn; } }

        public List<TrainWheel_v3> Wheels { get { return _wheelsScripts; } set { _wheelsScripts = value; } }
        public AudioSource WheelsSFX { get { return _wheelsSFX; } set { _wheelsSFX = value; } }
        public AudioSource EngineSFX { get { return _engineSFX; } set { _engineSFX = value; } }
        public AudioSource HornSFX { get { return _hornSFX; } set { _hornSFX = value; } }
        public AudioSource BellSFX { get { return _bellSFX; } set { _bellSFX = value; } }
        public AudioSource BrakesSFX { get { return _brakesSFX; } set { _brakesSFX = value; } }
        public AudioSource WagonConnectionSFX { get { return null; } set { } }
        public List<Light> ExternalLights { get { return _externalLights; } set { _externalLights = value; } }
        public List<Light> InternalLights { get { return _internalLights; } set { _internalLights = value; } }
        public Sensors Sensors { get { return null; } set { } }
        public Rigidbody JointAnchor { get { return null; } set { } }
        public Rigidbody FrontJoint { get { return _frontJoint; } set { _frontJoint = value; RecalculateOffset(); } }
        public Rigidbody BackJoint { get { return _backJoint; } set { _backJoint = value; RecalculateOffset(); } }

        public ParticleSystem SmokeParticles { get { return _smokeParticles; } set { _smokeParticles = value; } }
        public ParticleSystem[] BrakingSparksParticles { get { return _brakingSparksParticles; } set { _brakingSparksParticles = value; } }
        public Animator BellAnimator { get { return _bell; } set { _bell = value; } }

        public GameObject GetGameObject { get { return gameObject; } }

        public bool VisualizeRouteOnEditor { get { return _visualizeRouteOnEditor; } }

        public TrainType TrainType { get { return TrainType.SplineBased; } }

        public List<GameObject> ConnectedWagons
        {
            get
            {
                List<GameObject> temp = new List<GameObject>();
                foreach (var item in linkedFollowers)
                {
                    temp.Add(item.gameObject);
                }

                return temp;
            }
        }

        public ITrainDoorsController DoorsController
        {
            get
            {
                if (_doorController == null)
                    _doorController = GetComponent<ITrainDoorsController>();

                return _doorController;
            }
        }

        /// <summary>
        /// Distance between front and back couplers
        /// </summary>
        public float CouplersDistance
        {
            get
            {
                if (_frontJoint == null || _backJoint == null)
                {
                    return 0f;
                }

                return Mathf.Abs(_frontJoint.transform.localPosition.z) + Mathf.Abs(_backJoint.transform.localPosition.z); ;
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
                float convertedSpeed = base.GetConvertedSpeedUnit();
                return convertedSpeed > 0f ? 1 : (convertedSpeed < 0f ? -1 : 0);
            }
        }

        #endregion

        #region UNITY EVENTS

        /// <summary>
        /// Initialize train
        /// </summary>
        private new void Start()
        {
            _currentMaxSpeed = _maxSpeed;

            _currentSpeed = 0f;
            base.speed = _currentSpeed;
            base.speedUnit = GetTargetSpeedUnit();

            _doorController = GetComponent<ITrainDoorsController>();

            InitializeSFX();
            InitializeVFX();

            base.Start();
        }

        /// <summary>
        /// Spline based train following
        /// </summary>
        private new void Update()
        {
            _currentMaxSpeed = TrainPhysics.CalculateCurrentMaxSpeed(_currentMaxSpeed, _maxSpeed, _accelerationRate, _brakingDecelerationRate, _inertiaDecelerationRate);

            _brake = _automaticBrakes ? 1f - Mathf.Abs(_acceleration) : _brake;

            if (!_enginesOn)
            {
                _brake = _automaticBrakes ? 1f : _brake;
            }

            if(_emergencyBrakes)
            {
                _acceleration = 0f;
                _brake = 1f;
            }

            TrainPhysics.SpeedControl_SplineBased(_enginesOn, _currentMaxSpeed, _acceleration, _brake, _accelerationRate, _brakingDecelerationRate, _inertiaDecelerationRate, _currentSpeed, out _currentSpeed, _targetSpeed, out _targetSpeed);
            base.speed = _currentSpeed;
            base.Update();

            float convertedSpeed = base.GetConvertedSpeedUnit();

            //Dont simulate SFX, VFX and wheels if route is not applied
            if (base.splines == null || base.splines.Count == 0)
            {
                _currentSpeed = 0f;
                convertedSpeed = 0f;
            }

            _trainAudio.UpdateSFX(_currentSpeed, _brake, _enginesOn, true);
            _trainParticles.UpdateVFX(convertedSpeed, _acceleration, _brake, _enginesOn, _enableSmoke, _enableBrakingSparks, LocalDirection);
            TrainPhysics.UpdateWheels(_wheelsScripts, _brake, convertedSpeed);
        }

        #endregion

        #region PRIVATE METHODS

        private SplineFollowerSpeedUnit GetTargetSpeedUnit()
        {
            switch (_speedUnit)
            {
                case SpeedUnits.kph:
                    return SplineFollowerSpeedUnit.kph;
                case SpeedUnits.mph:
                    return SplineFollowerSpeedUnit.mph;
                default:
                    return SplineFollowerSpeedUnit.kph;
            }
        }

        /// <summary>
        /// Initialize SFX
        /// </summary>
        private void InitializeSFX()
        {
            SFX sfx = new SFX();
            sfx.hornSFX = _hornSFX;
            sfx.bellSFX = _bellSFX;
            sfx.engineSFX = _engineSFX;
            sfx.wheelsSFX = _wheelsSFX;
            sfx.brakesSFX = _brakesSFX;
            sfx.idleEnginePitch = _idleEnginePitch;
            sfx.maxEnginePitch = _maxEnginePitch;
            sfx.minWheelsPitch = _minWheelsPitch;
            sfx.maxWheelsPitch = _maxWheelsPitch;

            _trainAudio = new TrainAudio(sfx);
        }

        /// <summary>
        /// Initialize VFX
        /// </summary>
        private void InitializeVFX()
        {
            VFX vfx = new VFX();
            vfx.smokeParticles = _smokeParticles;
            vfx.minSmokeEmission = Mathf.Abs(_minSmokeEmission);
            vfx.maxSmokeEmission = Mathf.Abs(_maxSmokeEmission);
            vfx.brakingSparksParticles = _brakingSparksParticles;

            _trainParticles = new TrainParticles(vfx);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Toggle engine on/off
        /// </summary>
        public void ToggleEngine()
        {
            _enginesOn = !_enginesOn;
        }

        /// <summary>
        /// Toggle emergency brakes on/off
        /// </summary>
        public void ToggleEmergencyBrakes()
        {
            _emergencyBrakes = !_emergencyBrakes;
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
            if (linkedFollowers == null || linkedFollowers.Count == 0)
                return;

            DecoupleWagon(linkedFollowers.Count - 1);
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
            //dont decouple if moving backwards to prevent locomotive from passing through wagons
            if (LocalDirection < 0) return;

            if (linkedFollowers == null || index > linkedFollowers.Count - 1)
                return;

            for (int i = (linkedFollowers.Count - 1); i >= index; i--)
            {
                SplineBasedWagon wagon = linkedFollowers[i].GetComponent<SplineBasedWagon>();
                if (wagon != null) wagon.Disconnect();
            }

            UpdateDoorController();
        }

        /// <summary>
        /// Turn external lights on/off
        /// </summary>
        public void ToggleLights()
        {
            if (_externalLights != null)
            {
                foreach (Light light in _externalLights)
                    light.enabled = !light.enabled;
            }

            if (linkedFollowers != null)
            {
                foreach (var wagon in ConnectedWagons)
                    wagon.GetComponent<IRailwayVehicle>().ToggleLights();
            }
        }

        /// <summary>
        /// Turn internal lights on/off
        /// </summary>
        public void ToggleInternalLights()
        {
            if (_internalLights != null)
            {
                foreach (Light light in _internalLights)
                    light.enabled = !light.enabled;
            }

            if (linkedFollowers != null)
            {
                foreach (var wagon in ConnectedWagons)
                    wagon.GetComponent<IRailwayVehicle>().ToggleInternalLights();
            }
        }

        public void Honk()
        {
            _trainAudio.Honk();
        }

        public void ToogleBell()
        {
            _trainAudio.ToogleBell();

            if (_bell != null)
                _bell.SetBool(AnimationParameters.BellPlaying, _trainAudio.BellOn);
        }

        /// <summary>
        /// Recalculate following offset based on couplers position
        /// </summary>
        public void RecalculateOffset()
        {
            float offset = _backJoint == null ? 0 : Mathf.Abs(_backJoint.transform.localPosition.z);
            followersOffset = offset == 0 ? followersOffset : offset;
        }

        /// <summary>
        /// Assign target route to locomotive
        /// </summary>
        /// <param name="newRoute"></param>
        public void AssignRoute(Route newRoute, float t = 0.5f)
        {
            base.customStartPosition = t * 100f;
            ChangeRoute(newRoute);
        }

        /// <summary>
        /// Change current train route
        /// </summary>
        /// <param name="newRoute"></param>
        protected void ChangeRoute(Route newRoute)
        {
            if (Application.isPlaying)
            {
                int splineDirection = _currentSplineIndex - _lastSplineIndex;

                //Identify current spline index on newRoute
                int newIndex = 0;
                if (splines != null && splines.Count > 0)
                {
                    Spline currentSpline = splines[_currentSplineIndex];
                    newIndex = newRoute.Splines.FindIndex(x => x == currentSpline);
                    newIndex = newIndex == -1 ? 0 : newIndex; //If not found 
                }

                ApplyRoute(newRoute);
                //NormalizedOrientedPoints for new route
                _normalizedOrientedPoints = newRoute.NormalizedRoute;
                //Change current spline index
                _currentSplineIndex = newIndex;
                _lastSplineIndex = _currentSplineIndex + splineDirection;
                _lastSplineIndex = _lastSplineIndex > splines.Count - 1 ? 0 : (_lastSplineIndex < 0 ? splines.Count - 1 : _lastSplineIndex);
            }
            else
            {
                ApplyRoute(newRoute);
                MoveToStartPosition(); //If not playing change move to start position
            }
        }

        /// <summary>
        /// Apply route to locomotive
        /// </summary>
        /// <param name="newRoute"></param>
        private void ApplyRoute(Route newRoute)
        {
            splines = new List<Spline>();
            if (newRoute.Splines != null && newRoute.Splines.Count > 0)
                splines.AddRange(newRoute.Splines);

#if UNITY_EDITOR
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
        }

        /// <summary>
        /// Add new wagons to train
        /// </summary>
        /// <param name="newWagons"></param>
        public void AddWagons(List<GameObject> newWagons)
        {
            linkedFollowers = (linkedFollowers == null) ? new List<LinkedSplineFollower>() : linkedFollowers;

            foreach (var item in newWagons)
            {
                SplineBasedWagon wagonScript = item.GetComponent<SplineBasedWagon>();
                if (wagonScript == null) continue;
                linkedFollowers.Add(wagonScript);
            }
        }

        /// <summary>
        /// Remove all wagons references (don't disconnect them)
        /// </summary>
        public void RemoveAllWagons()
        {
            linkedFollowers = null;
        }

        /// <summary>
        /// Move locomotive to start position and automatically calculates wagons initial positions
        /// </summary>
        public void CalculateWagonsPositions()
        {
            if (splines == null || splines.Count == 0)
            {
                if (_backJoint == null)
                {
                    Debug.LogWarning("Locomotive rear coupler cannot be null");
                    return;
                }

                List<IRailwayVehicle> targetwagons = new List<IRailwayVehicle>();

                foreach (var item in base.linkedFollowers)
                {
                    IRailwayVehicle targetWagon = item.gameObject.GetComponent<IRailwayVehicle>();
                    targetwagons.Add(targetWagon);
                }

                TrainPhysics.CalculateWagonsPositions(transform, targetwagons, _backJoint.transform);
            }
            else
                base.MoveToStartPosition();
        }

        /// <summary>
        /// Move locomotive to start position and automatically calculates wagons initial position
        /// </summary>
        /// <param name="targetRails"></param>
        public void CalculateWagonsPositions(List<Spline> targetRails)
        {
            splines = targetRails;
            CalculateWagonsPositions();
        }

        #endregion

        #region PROTECTED METHODS

        /// <summary>
        /// Executes when follower reaches the end of the spline list and follow behaviour is set to BACK AND FORWARD
        /// </summary>
        protected override void OnBackAndForward()
        {
            _acceleration *= -1;
            _currentSpeed *= -1;

            if (_onReachedEndOfRoute != null) _onReachedEndOfRoute.Invoke();
        }

        /// <summary>
        /// Executes when follower reaches the end of the spline list and follow behaviour is set to LOOP
        /// </summary>
        protected override void OnLoop()
        {
            if (_onReachedEndOfRoute != null) _onReachedEndOfRoute.Invoke();
        }

        /// <summary>
        /// Executes when follower reaches the end of the spline list and follow behaviour is set to STOP AT THE END
        /// </summary>
        protected override void OnStopAtTheEnd()
        {
            _enginesOn = false;
            _currentSpeed = 0;

            if (_onReachedEndOfRoute != null) _onReachedEndOfRoute.Invoke();
        }

        #endregion
    }
}
