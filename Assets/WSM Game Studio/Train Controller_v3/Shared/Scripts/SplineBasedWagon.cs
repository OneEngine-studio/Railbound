using WSMGameStudio.Splines;
using UnityEngine;
using System.Collections.Generic;

namespace WSMGameStudio.RailroadSystem
{
    public class SplineBasedWagon : LinkedSplineFollower, IRailwayVehicle
    {
        #region VARIABLES
        private SplineBasedLocomotive _locomotive;

        [SerializeField] private List<TrainWheel_v3> _wheelsScripts;
        [SerializeField] private AudioSource _wheelsSFX;
        [SerializeField] private AudioSource _wagonConnectionSFX;
        [Range(0f, 3f)]
        [SerializeField] private float _minWheelsPitch = 0.6f;
        [Range(0f, 3f)]
        [SerializeField] private float _maxWheelsPitch = 1.5f;
        [SerializeField] private List<Light> _externalLights;
        [SerializeField] private List<Light> _internalLights;
        [SerializeField] private Rigidbody _backJoint;
        [SerializeField] private Rigidbody _frontJoint;
        [SerializeField] private WagonCoupling _coupling;
        [SerializeField] private WagonDecouplingSettings _decouplingSettings;
        [SerializeField] private float _recouplingTimeout = 1f;

        //SFX
        private TrainAudio _trainAudio;

        #endregion

        #region PROPERTIES

        public SplineBasedLocomotive Locomotive { get { return _locomotive; } set { _locomotive = value; } }

        public bool IsConected { get { return Master != null; } }

        public List<TrainWheel_v3> Wheels { get { return _wheelsScripts; } set { _wheelsScripts = value; } }
        public AudioSource WheelsSFX { get { return _wheelsSFX; } set { _wheelsSFX = value; } }
        public AudioSource WagonConnectionSFX { get { return _wagonConnectionSFX; } set { _wagonConnectionSFX = value; } }
        public AudioSource BrakesSFX { get { return null; } set { } }
        public List<Light> ExternalLights { get { return _externalLights; } set { _externalLights = value; } }
        public List<Light> InternalLights { get { return _internalLights; } set { _internalLights = value; } }
        public Sensors Sensors { get { return null; } set { } }
        public Rigidbody JointAnchor { get { return null; } set { } }
        public Rigidbody FrontJoint { get { return _frontJoint; } set { _frontJoint = value; RecalculateOffset(); } }
        public Rigidbody BackJoint { get { return _backJoint; } set { _backJoint = value; RecalculateOffset(); } }

        public GameObject GetGameObject { get { return gameObject; } }

        public TrainType TrainType { get { return TrainType.SplineBased; } }

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
                float convertedSpeed = Master.GetConvertedSpeedUnit();
                return convertedSpeed > 0f ? 1 : (convertedSpeed < 0f ? -1 : 0);
            }
        }

        #endregion

        #region UNITY EVENTS

        private new void Start()
        {
            InitializeSFX();
            base.Start();
        }

        private void Update()
        {
            float masterSpeed = Master != null? Master.GetConvertedSpeedUnit() : 0f;
            TrainPhysics.UpdateWheels(_wheelsScripts, 0, masterSpeed);
            _trainAudio.UpdateSFX(Speed.Convert_MPS_To_KPH(masterSpeed), 0, false, true);
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Initialize SFX
        /// </summary>
        private void InitializeSFX()
        {
            SFX sfx = new SFX();
            sfx.wheelsSFX = _wheelsSFX;
            sfx.wagonConnectionSFX = _wagonConnectionSFX;
            sfx.minWheelsPitch = _minWheelsPitch;
            sfx.maxWheelsPitch = _maxWheelsPitch;

            _trainAudio = new TrainAudio(sfx);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Turn external lights on/off
        /// </summary>
        public void ToggleLights()
        {
            if (_externalLights == null)
                return;

            foreach (Light light in _externalLights)
                light.enabled = !light.enabled;
        }

        /// <summary>
        /// Turn internal lights on/off
        /// </summary>
        public void ToggleInternalLights()
        {
            if (_internalLights == null)
                return;

            foreach (Light light in _internalLights)
                light.enabled = !light.enabled;
        }

        /// <summary>
        /// Recalculate following offset based on couplers position
        /// </summary>
        public void RecalculateOffset()
        {
            float offset = CouplersDistance;
            followingOffset = offset == 0 ? followingOffset : offset;
        }

        /// <summary>
        /// Connect wagon to train
        /// </summary>
        /// <param name="carCoupler">Current wagon coupler (front or back)</param>
        /// <param name="otherCarCoupler">Other wagon coupler</param>
        public void Connect(SplineBasedTrainCarCoupler carCoupler, SplineBasedTrainCarCoupler otherCarCoupler, bool playSFX)
        {
            if (_coupling == WagonCoupling.Enabled)
            {
                if (otherCarCoupler.IsLocomotive)
                {
                    _locomotive = otherCarCoupler.Locomotive;
                }
                else if (otherCarCoupler.IsWagon)
                {
                    if (!otherCarCoupler.Wagon.IsConected)
                        return;

                    _locomotive = otherCarCoupler.Wagon.Locomotive;
                }

                Master = _locomotive;
                FollowerBehaviour = Master.linkedFollowersBehaviour;

                if (!Master.linkedFollowers.Contains(this))
                    Master.linkedFollowers.Add(this);

                _locomotive.UpdateDoorController();

                if (playSFX && _trainAudio != null)
                    _trainAudio.PlayConnectionSFX();
            }
        }

        /// <summary>
        /// Disconnect wagon from train
        /// </summary>
        public void Disconnect()
        {
            if (Master != null && Master.linkedFollowers.Contains(this))
                Master.linkedFollowers.Remove(this);

            _locomotive = null;
            Master = null;

            _trainAudio.PlayConnectionSFX();

            _coupling = WagonCoupling.Disabled;

            if (_decouplingSettings == WagonDecouplingSettings.AllowRecoupling)
            {
                Invoke("ReenabledCoupling", Mathf.Abs(_recouplingTimeout));
            }
        }

        /// <summary>
        /// Reenable wagon coupling
        /// </summary>
        private void ReenabledCoupling()
        {
            _coupling = WagonCoupling.Enabled;
        }

        #endregion
    }
}
