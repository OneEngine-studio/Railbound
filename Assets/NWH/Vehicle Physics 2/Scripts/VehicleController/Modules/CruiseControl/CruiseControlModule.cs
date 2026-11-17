using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using NWH.NUI;
#endif

namespace NWH.VehiclePhysics2.Modules.CruiseControl
{
    /// <summary>
    ///     Cruise Control implemented through a PID controller.
    ///     Does not work in reverse.
    ///     Can both accelerated and brake.
    /// </summary>
    [Serializable]
    public partial class CruiseControlModule : VehicleComponent
    {
        /// <summary>
        /// Is the cruise control currently active and trying to hold the vehicle speed?
        /// </summary>
        public bool cruiseControlActive;
        
        /// <summary>
        ///     Derivative gain of PID controller.
        /// </summary>
        [Tooltip("    Derivative gain of PID controller.")]
        public float Kd = 0.1f;

        /// <summary>
        ///     Integral gain of PID controller.
        /// </summary>
        [Tooltip("    Integral gain of PID controller.")]
        public float Ki = 0.25f;

        /// <summary>
        ///     Proportional gain of PID controller.
        /// </summary>
        [Tooltip("    Proportional gain of PID controller.")]
        public float Kp = 0.5f;

        /// <summary>
        ///     Should the speed be set automatically when the module is enabled?
        /// </summary>
        [Tooltip("    Should the speed be set automatically when the module is enabled?")]
        public bool setTargetSpeedOnEnable;

        /// <summary>
        ///     If true cruise control will be disabled if brakes are activated.
        /// </summary>
        [Tooltip("    If true cruise control will be disabled if brakes are activated.")]
        public bool deactivateOnBrake;

        /// <summary>
        ///  If true brakes will be applied when speeding.
        /// </summary>
        [UnityEngine.Tooltip(" If true brakes will be applied when speeding.")]
        public bool applyBrakesWhenSpeeding = true;

        /// <summary>
        ///     The speed the vehicle will try to hold.
        /// </summary>
        [Tooltip("    The speed the vehicle will try to hold.")]
        public float targetSpeed;

        private float _e;
        private float _ed;
        private float _ei;
        private float _eprev;
        private float _output;


        public override bool VC_Enable(bool calledByParent)
        {
            if (base.VC_Enable(calledByParent))
            {
                vehicleController.input.inputModifyCallback.AddListener(SetOutput);
                return true;
            }

            return false;
        }


        public override bool VC_Disable(bool calledByParent)
        {
            if (base.VC_Disable(calledByParent))
            {
                vehicleController.input.inputModifyCallback.RemoveListener(SetOutput);
                return true;
            }

            return false;

        }


        private void SetOutput()
        {
            if (cruiseControlActive)
            {
                vehicleController.input.Vertical = _output;
            }
        }

        
        public override void VC_FixedUpdate()
        {
            base.VC_FixedUpdate();

            float speed = vehicleController.SpeedSigned;
            float dt = vehicleController.fixedDeltaTime;
            
            if (vehicleController.input.states.cruiseControl)
            {
                cruiseControlActive = !cruiseControlActive;
                if (cruiseControlActive && speed > 0f)
                {
                    if (Math.Abs(speed - targetSpeed) > 0.05f)
                    {
                        _ei = 0;
                    }
                    targetSpeed = speed;
                }
                vehicleController.input.states.cruiseControl = false;
            }

            if (!cruiseControlActive)
            {
                return;
            }

            if (deactivateOnBrake && vehicleController.input.InputSwappedBrakes > 0.25f || speed <= 0f)
            {
                cruiseControlActive = false;
                return;
            }
            
            _eprev = _e;
            _e = targetSpeed - speed;
            if (_e > -0.5f && _e < 0.5f)
            {
                _ei = 0f;
            }

            _ei += _e * dt;
            _ed = (_e - _eprev) / dt;
            float newOutput = _e * Kp + _ei * Ki + _ed * Kd;
            newOutput = newOutput < -1f ? -1f : newOutput > 1f ? 1f : newOutput;
            _output = Mathf.Lerp(_output, newOutput, dt * 5f);

            if (!applyBrakesWhenSpeeding)
            {
                _output = _output < 0 ? 0 : _output;
            }
        }
    }
}


#if UNITY_EDITOR
namespace NWH.VehiclePhysics2.Modules.CruiseControl
{
    [CustomPropertyDrawer(typeof(CruiseControlModule))]
    public partial class CruiseControlModuleDrawer : ComponentNUIPropertyDrawer
    {
        public override bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!base.OnNUI(position, property, label))
            {
                return false;
            }

            drawer.Field("cruiseControlActive", false);
            
            drawer.BeginSubsection("Speed");
            drawer.Field("targetSpeed");
            drawer.Field("setTargetSpeedOnEnable");
            drawer.Field("applyBrakesWhenSpeeding");
            drawer.Field("deactivateOnBrake");
            drawer.EndSubsection();

            drawer.BeginSubsection("PID Controller Settings");
            drawer.Field("Kp");
            drawer.Field("Ki");
            drawer.Field("Kd");
            drawer.EndSubsection();

            drawer.EndProperty();
            return true;
        }
    }
}

#endif
