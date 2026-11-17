using System;
using System.Collections.Generic;
using UnityEngine;
using NWH.Common.Vehicles;

#if UNITY_EDITOR
using UnityEditor;
using NWH.NUI;
#endif

namespace NWH.VehiclePhysics2.Powertrain
{
    [Serializable]
    public partial class DifferentialComponent : PowertrainComponent
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="T">Input torque</param>
        /// <param name="Wa">Angular velocity of the outputA</param>
        /// <param name="Wb">Angular velocity of the outputB</param>
        /// <param name="Ia">Inertia of the outputA</param>
        /// <param name="Ib">Inertia of the outputB</param>
        /// <param name="dt">Time step</param>
        /// <param name="biasAB">Torque bias between outputA and outputB. 0 = all torque goes to A, 1 = all torque goes to B</param>
        /// <param name="stiffness">Stiffness of the limited slip or locked differential</param>
        /// <param name="powerRamp">Stiffness under power</param>
        /// <param name="coastRamp">Stiffness under braking</param>
        /// <param name="slipTorque">Slip torque of the limited slip differential</param>
        /// <param name="Ta">Torque output towards outputA</param>
        /// <param name="Tb">Torque output towards outputB</param>
        public delegate void SplitTorque(float T, float Wa, float Wb, float Ia, float Ib, float dt, float biasAB,
            float stiffness, float powerRamp, float coastRamp, float slipTorque, out float Ta, out float Tb);


        public enum Type
        {
            Open,
            Locked,
            LimitedSlip,
            External
        }


        /// <summary>
        ///     Differential type.
        /// </summary>
        public Type DifferentialType
        {
            get { return _differentialType; }
            set
            {
                _differentialType = value;
                AssignDifferentialDelegate();
            }
        }

        [ShowInTelemetry]
        [ShowInSettings("Type")]
        [SerializeField]
        private Type _differentialType;


        /// <summary>
        ///     Torque bias between left (A) and right (B) output in [0,1] range.
        /// </summary>
        [SerializeField]
        [Range(0, 1)]
        [Tooltip("    Torque bias between left (A) and right (B) output in [0,1] range.")]
        [ShowInTelemetry]
        [ShowInSettings("Bias A/B", 0f, 1f, 0.1f)]
        public float biasAB = 0.5f;


        /// <summary>
        ///     Stiffness of locking differential [0,1]. Higher value
        ///     will result in lower difference in rotational velocity between left and right wheel.
        ///     Too high value might introduce slight oscillation due to drivetrain windup and a vehicle that is hard to steer.
        /// </summary>
        [SerializeField]
        [Range(0, 1)]
        [Tooltip(
            "Stiffness of locking differential [0,1]. Higher value\r\nwill result in lower difference in rotational velocity between left and right wheel." +
            "\r\nToo high value might introduce slight oscillation due to drivetrain windup.")]
        [ShowInTelemetry]
        [ShowInSettings("Stiffness", 0f, 1f, 0.1f)]
        public float stiffness = 0.5f;


        /// <summary>
        /// Stiffness of the LSD differential under acceleration.
        /// </summary>
        [SerializeField]
        [Range(0, 1)]
        [ShowInTelemetry]
        [ShowInSettings("Power Ramp", 0f, 1f, 0.1f)]
        [UnityEngine.Tooltip("Stiffness of the LSD differential under acceleration.")]
        public float powerRamp = 1f;


        /// <summary>
        /// Stiffness of the LSD differential under braking.
        /// </summary>
        [Range(0, 1)]
        [ShowInTelemetry]
        [ShowInSettings("Coast Ramp", 0f, 1f, 0.1f)]
        public float coastRamp = 0.5f;


        /// <summary>
        ///     Second output of differential.
        /// </summary>
        public PowertrainComponent OutputB
        {
            get { return _outputB; }
            set
            {
                if (value == this)
                {
                    Debug.LogWarning($"{name}: PowertrainComponent Output can not be self.");
                    outputBNameHash = 0;
                    _output = null;
                }
                else
                {
                    if (_outputB != null)
                    {
                        _outputB.inputNameHash = 0;
                        _outputB.Input = null;
                    }

                    _outputB = value;

                    if (_outputB != null)
                    {
                        outputBNameHash = _outputB.name.GetHashCode();
                        _outputB.Input = this;
                    }
                    else
                    {
                        outputBNameHash = 0;
                    }
                }
            }
        }

        [NonSerialized]
        protected PowertrainComponent _outputB;
        public int outputBNameHash;


        /// <summary>
        ///     Slip torque of limited slip differentials.
        /// </summary>
        [SerializeField]
        [Tooltip("    Slip torque of limited slip differentials.")]
        [ShowInTelemetry]
        [ShowInSettings("LSD Slip Tq", 0f, 2000f, 100f)]
        public float slipTorque = 400f;


        /// <summary>
        /// Function delegate that will be used to split the torque between output(A) and outputB.
        /// </summary>
        [UnityEngine.Tooltip("Function delegate that will be used to split the torque between output(A) and outputB.")]
        public SplitTorque splitTorqueDelegate;


        protected override void VC_Initialize()
        {
            LoadComponentFromHash(vehicleController, ref _outputB, outputBNameHash);
            AssignDifferentialDelegate();
            
            base.VC_Initialize();
        }


        public override void VC_Validate(VehicleController vc)
        {
            base.VC_Validate(vc);

            if (outputBNameHash == 0)
            {
                Debug.Log(outputBNameHash);
                PC_LogWarning(vc, "PowertrainComponent output not set. This might be a result of the 10.20f update, in which case the " +
                    $"powertrain outputs need to be re-assigned.");
            }
            
            
            if (Application.isPlaying && Input == null)
            {
                PC_LogWarning(vc, "Differential has no input. Differential that are in no way connected to the engine" +
                                  " will not be updated and should be removed or they might cause the wheels attached to them" +
                                  " to spin up slower than usual due to the inertia of a dangling/dead differential.");
            }
        }


        public override void VC_SetDefaults()
        {
            base.VC_SetDefaults();

            name = "Differential";
            inertia = 0.02f;
        }



        public void LimitedDiffTorqueSplit(float T, float Wa, float Wb, float Ia, float Ib, float dt, float biasAB,
            float stiffness, float powerRamp, float coastRamp, float slipTorque, out float Ta, out float Tb)
        {
            if (Wa < 0 || Wb < 0)
            {
                Ta = T * (1f - biasAB);
                Tb = T * biasAB;
                return;
            }

            //Wa and Wb are positive at this point
            float c = T > 0 ? powerRamp : coastRamp;
            float Wtotal = (Wa < 0 ? -Wa : Wa) + (Wb < 0 ? -Wb : Wb);
            float slip = Wtotal == 0 ? 0 : (Wa - Wb) / Wtotal;
            float Td = slip * stiffness * c * slipTorque;

            float Tabs = Mathf.Abs(T);
            Td = Mathf.Clamp(Td, -Tabs * 0.5f, Tabs * 0.5f);

            Ta = T * 0.5f - Td;
            Tb = T * 0.5f + Td;
        }


        public void LockingDiffTorqueSplit(float T, float Wa, float Wb, float Ia, float Ib, float dt, float biasAB,
            float stiffness, float powerRamp, float coastRamp, float slipTorque, out float Ta, out float Tb)
        {
            float Isum = Ia + Ib;

            float Wtarget = Ia / Isum * Wa + Ib / Isum * Wb;
            float TaCorrective = (Wtarget - Wa) * Ia / dt;
            TaCorrective *= stiffness;
            float TbCorrective = (Wtarget - Wb) * Ib / dt;
            TbCorrective *= stiffness;

            float Tabs = T < 0 ? -T : T;
            TbCorrective = TbCorrective > 0 ?
                (TbCorrective > Tabs ? Tabs : TbCorrective) :
                (TbCorrective < -Tabs ? -Tabs : TbCorrective);

            float biasA = 0.5f + (Wb - Wa) * 10f * stiffness;
            biasA = biasA < 0 ? 0 : biasA > 1f ? 1f : biasA;

            Ta = T * biasA + TaCorrective;
            Tb = T * (1f - biasA) + TbCorrective;
        }


        public void OpenDiffTorqueSplit(float T, float Wa, float Wb, float Ia, float Ib, float dt, float biasAB,
            float stiffness, float powerRamp, float coastRamp, float slipTorque, out float Ta, out float Tb)
        {
            Ta = T * (1f - biasAB);
            Tb = T * biasAB;
        }


        public override float QueryAngularVelocity(float angularVelocity, float dt)
        {
            inputAngularVelocity = angularVelocity;

            if (outputNameHash == 0 || outputBNameHash == 0)
            {
                return angularVelocity;
            }

            outputAngularVelocity = inputAngularVelocity;
            float Wa = _output.QueryAngularVelocity(outputAngularVelocity, dt);
            float Wb = _outputB.QueryAngularVelocity(outputAngularVelocity, dt);
            return (Wa + Wb) * 0.5f;
        }


        public override float QueryInertia()
        {
            if (outputNameHash == 0 || outputBNameHash == 0)
            {
                return inertia;
            }

            float Ia = _output.QueryInertia();
            float Ib = _outputB.QueryInertia();
            float I = inertia + (Ia + Ib);
            return I;
        }


        public override float ForwardStep(float torque, float inertiaSum, float dt)
        {
            inputTorque = torque;
            inputInertia = inertiaSum;

            if (outputNameHash == 0 || outputBNameHash == 0)
            {
                return torque;
            }

            float Wa = _output.QueryAngularVelocity(outputAngularVelocity, dt);
            float Wb = _outputB.QueryAngularVelocity(outputAngularVelocity, dt);

            float Ia = _output.QueryInertia();
            float Ib = _outputB.QueryInertia();

            splitTorqueDelegate.Invoke(torque, Wa, Wb, Ia, Ib, dt, biasAB, stiffness, powerRamp,
                                       coastRamp, slipTorque, out float Ta, out float Tb);

            float outAInertia = inertiaSum * 0.5f + Ia;
            float outBInertia = inertiaSum * 0.5f + Ib;

            outputTorque = Ta + Tb;
            outputInertia = outAInertia + outBInertia;

            return _output.ForwardStep(Ta, outAInertia, dt) + _outputB.ForwardStep(Tb, outBInertia, dt);
        }


        private void AssignDifferentialDelegate()
        {
            switch (_differentialType)
            {
                case Type.Open:
                    splitTorqueDelegate = OpenDiffTorqueSplit;
                    break;
                case Type.Locked:
                    splitTorqueDelegate = LockingDiffTorqueSplit;
                    break;
                case Type.LimitedSlip:
                    splitTorqueDelegate = LimitedDiffTorqueSplit;
                    break;
                case Type.External:
                    break;
                default:
                    splitTorqueDelegate = OpenDiffTorqueSplit;
                    break;
            }
        }
    }
}


#if UNITY_EDITOR

namespace NWH.VehiclePhysics2.Powertrain
{
    [CustomPropertyDrawer(typeof(DifferentialComponent))]
    public partial class DifferentialComponentDrawer : PowertrainComponentDrawer
    {
        private int selectionA;
        private int selectionB;

        public override void DrawPowertrainOutputSection(ref Rect rect, VehicleController vc, PowertrainComponent pc)
        {
            // Cast the PowertrainComponent to DifferentialComponent
            DifferentialComponent dc = pc as DifferentialComponent;

            // Remember initial values of selections to know if the change happened later
            int initialSelectionA = selectionA;
            int initialSelectionB = selectionB;

            // Find the index of the current output A and output B
            selectionA = componentNames.FindIndex(n => n.GetHashCode() == dc.outputNameHash);
            selectionB = componentNames.FindIndex(n => n.GetHashCode() == dc.outputBNameHash);

            // Create the output A dropdown with the list of component names
            selectionA = EditorGUI.Popup(drawer.positionRect, "OutputA",
                selectionA < 0 ? 0 : selectionA, componentNames.ToArray());
            drawer.Space(22);

            // Create the output B dropdown with the list of component names
            selectionB = EditorGUI.Popup(drawer.positionRect, "OutputB",
                selectionB < 0 ? 0 : selectionB, componentNames.ToArray());

            drawer.Space(22);

            // Check if either output A or output B dropdown selection has changed
            if (selectionA != initialSelectionA || selectionB != initialSelectionB)
            {
                // Set the new output A and output B and mark the VehicleController as dirty to save changes
                dc.Output = components[selectionA];
                dc.OutputB = components[selectionB];
                EditorUtility.SetDirty(vc);
            }
        }


        public override bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!base.OnNUI(position, property, label))
            {
                return false;
            }

            DrawCommonProperties();

            drawer.BeginSubsection("Differential Settings");
            drawer.Field("_differentialType");

            int typeEnumValue = property.FindPropertyRelative("_differentialType").enumValueIndex;
            if (typeEnumValue != (int)DifferentialComponent.Type.External)
            {
                if (typeEnumValue != (int)DifferentialComponent.Type.LimitedSlip && typeEnumValue != (int)DifferentialComponent.Type.Locked)
                {
                    drawer.Field("biasAB");
                }

                if (typeEnumValue != (int)DifferentialComponent.Type.Open)
                {
                    drawer.Field("stiffness");

                    if (typeEnumValue != (int)DifferentialComponent.Type.Locked)
                    {
                        drawer.Field("slipTorque");
                        drawer.Field("powerRamp");
                        drawer.Field("coastRamp");
                    }
                }
            }
            else
            {
                drawer.Info(
                    "Using differential from external script. Check the script for settings. If no torque split delegate is assigned, " +
                    "differentiall will fall back to Open type.");
            }

            drawer.EndSubsection();
            drawer.EndProperty();
            return true;
        }
    }
}

#endif
