using System.Collections.Generic;
using UnityEngine;
using JUTPS.VehicleSystem;

namespace Project.Vehicles
{
    /// <summary>
    /// Enhances vehicle physics stability, eliminates rollovers, applies anti-roll stabilizer forces,
    /// dynamic aerodynamic downforce, and tunes tire traction for responsive arcade-racing handling.
    /// </summary>
    [RequireComponent(typeof(Vehicle))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Project/Vehicles/Vehicle Physics Enhancer")]
    public class VehiclePhysicsEnhancer : MonoBehaviour
    {
        [Header("Center of Mass")]
        [Tooltip("Center of mass offset. Negative Y lowers CG below wheel axles, eliminating high-speed rollovers.")]
        public Vector3 CenterOfMassOffset = new Vector3(0.07f, -0.3f, -0.2f);
        public bool EnforceCenterOfMass = true;

        [Header("Anti-Roll Stabilizer Bar")]
        [Tooltip("Anti-roll force applied across wheel axle pairs to keep the chassis flat in sharp turns.")]
        public float AntiRollForce = 8500f;

        [Header("Aerodynamic Downforce")]
        [Tooltip("Downforce factor applied downwards proportional to speed squared (F = k * v^2).")]
        public float DownforceFactor = 4.0f;
        public float MaxDownforce = 12000f;

        [Header("Tire Grip & Handling")]
        [Tooltip("Multiplier applied to forward tire friction stiffness.")]
        [Range(1f, 3f)] public float ForwardFrictionMultiplier = 1.35f;

        [Tooltip("Multiplier applied to sideways tire friction stiffness (eliminates unnatural ice-skating drift).")]
        [Range(1f, 3f)] public float SidewaysFrictionMultiplier = 1.35f;

        [Header("Torque & Acceleration Tuning")]
        [Tooltip("Optional multiplier for Engine TorqueForce for punchier acceleration.")]
        public float TorqueMultiplier = 1.3f;

        [Tooltip("Optional multiplier for Engine BrakeForce.")]
        public float BrakeMultiplier = 1.25f;

        [Header("Anti-Turtle (Auto-Right Assist)")]
        [Tooltip("Gently flips the vehicle upright if flipped upside-down on its roof.")]
        public bool EnableAutoRight = true;
        public float InvertedThresholdSeconds = 2.0f;

        private Vehicle _vehicle;
        private Rigidbody _rb;
        private CarController _carController;
        private List<AxlePair> _axlePairs = new List<AxlePair>();
        private float _invertedTimer = 0f;

        private struct AxlePair
        {
            public WheelCollider Left;
            public WheelCollider Right;

            public AxlePair(WheelCollider l, WheelCollider r)
            {
                Left = l;
                Right = r;
            }
        }

        private void Awake()
        {
            _vehicle = GetComponent<Vehicle>();
            _rb = GetComponent<Rigidbody>();
            _carController = GetComponent<CarController>();

            ApplyCenterOfMass();
            TuneTireFriction();
            TuneEngineForces();
            CollectAxles();
        }

        private void Start()
        {
            ApplyCenterOfMass();
        }

        public void ApplyCenterOfMass()
        {
            if (_rb == null || !EnforceCenterOfMass) return;
            _rb.centerOfMass = CenterOfMassOffset;
        }

        private void TuneTireFriction()
        {
            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>();
            foreach (WheelCollider wc in wheels)
            {
                if (wc == null) continue;

                WheelFrictionCurve fFriction = wc.forwardFriction;
                fFriction.stiffness *= ForwardFrictionMultiplier;
                wc.forwardFriction = fFriction;

                WheelFrictionCurve sFriction = wc.sidewaysFriction;
                sFriction.stiffness *= SidewaysFrictionMultiplier;
                wc.sidewaysFriction = sFriction;
            }
        }

        private void TuneEngineForces()
        {
            if (_vehicle != null && _vehicle.Engine != null)
            {
                _vehicle.Engine.TorqueForce *= TorqueMultiplier;
                _vehicle.Engine.BrakeForce *= BrakeMultiplier;
            }
        }

        private void CollectAxles()
        {
            _axlePairs.Clear();

            if (_carController != null && _carController.WheelAxles != null)
            {
                for (int i = 0; i < _carController.WheelAxles.Length; i++)
                {
                    WheelCollider l = _carController.WheelAxles[i].LeftWheelCollider;
                    WheelCollider r = _carController.WheelAxles[i].RightWheelCollider;
                    if (l != null && r != null)
                    {
                        _axlePairs.Add(new AxlePair(l, r));
                    }
                }
            }

            // Fallback: If no CarController axles registered, pair up WheelColliders by X positions
            if (_axlePairs.Count == 0)
            {
                WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>();
                List<WheelCollider> lefts = new List<WheelCollider>();
                List<WheelCollider> rights = new List<WheelCollider>();

                foreach (WheelCollider w in wheels)
                {
                    if (w.transform.localPosition.x < 0) lefts.Add(w);
                    else rights.Add(w);
                }

                int count = Mathf.Min(lefts.Count, rights.Count);
                for (int i = 0; i < count; i++)
                {
                    _axlePairs.Add(new AxlePair(lefts[i], rights[i]));
                }
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || _vehicle == null) return;

            // Keep Center of Mass locked
            if (EnforceCenterOfMass && _rb.centerOfMass != CenterOfMassOffset)
            {
                _rb.centerOfMass = CenterOfMassOffset;
            }

            ApplyAntiRollBars();
            ApplyDownforce();
            CheckAutoRight();
        }

        private void ApplyAntiRollBars()
        {
            if (_axlePairs.Count == 0 || AntiRollForce <= 0f) return;

            for (int i = 0; i < _axlePairs.Count; i++)
            {
                WheelCollider left = _axlePairs[i].Left;
                WheelCollider right = _axlePairs[i].Right;

                if (left == null || right == null) continue;

                float travelLeft = 1.0f;
                float travelRight = 1.0f;

                bool groundedLeft = left.GetGroundHit(out WheelHit hitLeft);
                if (groundedLeft)
                {
                    travelLeft = (-left.transform.InverseTransformPoint(hitLeft.point).y - left.radius) / left.suspensionDistance;
                }

                bool groundedRight = right.GetGroundHit(out WheelHit hitRight);
                if (groundedRight)
                {
                    travelRight = (-right.transform.InverseTransformPoint(hitRight.point).y - right.radius) / right.suspensionDistance;
                }

                float antiRollForce = (travelLeft - travelRight) * AntiRollForce;

                if (groundedLeft)
                {
                    _rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
                }

                if (groundedRight)
                {
                    _rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
                }
            }
        }

        private void ApplyDownforce()
        {
            if (DownforceFactor <= 0f) return;

            float speed = Mathf.Abs(_vehicle.ForwardSpeed);
            if (speed > 1.5f)
            {
                float downforce = Mathf.Clamp(DownforceFactor * speed * speed, 0f, MaxDownforce);
                _rb.AddForce(-transform.up * downforce, ForceMode.Force);
            }
        }

        private void CheckAutoRight()
        {
            if (!EnableAutoRight) return;

            // Dot product between vehicle's UP and world UP:
            // 1 = perfectly upright, 0 = on side, -1 = upside down
            float dotUp = Vector3.Dot(transform.up, Vector3.up);

            if (dotUp < 0.25f && _rb.velocity.magnitude < 3.0f)
            {
                _invertedTimer += Time.fixedDeltaTime;

                if (_invertedTimer >= InvertedThresholdSeconds)
                {
                    // Gently rotate toward upright
                    Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
                    _rb.AddTorque(cross * 2500f, ForceMode.Acceleration);

                    // Slightly lift up to clear terrain collision
                    _rb.AddForce(Vector3.up * 500f, ForceMode.Acceleration);

                    if (dotUp > 0.7f)
                    {
                        _invertedTimer = 0f;
                    }
                }
            }
            else
            {
                _invertedTimer = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_rb != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.TransformPoint(CenterOfMassOffset), 0.25f);
            }
        }
    }
}
