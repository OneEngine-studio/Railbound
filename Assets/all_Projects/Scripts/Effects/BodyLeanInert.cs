using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using JUTPSActions;
using JUTPS;

namespace JUTPS.FX
{
    [AddComponentMenu("JU TPS/FX/Body Lean")]
    public class BodyLeanInert : JUTPSAction
    {
        public JUFootPlacement JUFootPlacer;
        public Transform RootBone;

        public bool RootBoneSpineLean = true;
        public bool RootBoneSpineMovement = true;

        public float RootBoneLeanIntensity = 30;
        public float RootBoneLeanSpeed = 8;
        public float RootBoneDownMovementIntensity = 0.5f;
        public float BlockForwardLeanWeight = 8;

        float Speed;
        float Lean;

        Vector3 NotAffectedEulerAngles;
        Vector3 NotAffectedUpward;

        public Axis AxisToLean;


        public enum Axis { X, Y, Z }


        public override void Awake()
        {
            base.Awake();
            if (JUFootPlacer == null) JUFootPlacer = GetComponent<JUFootPlacement>();
            if (RootBone == null) RootBone = anim.GetBoneTransform(HumanBodyBones.Hips);
            // Keep Animator in Normal update mode so character locomotion evaluates every rendered frame without 50Hz stepping.
            anim.updateMode = AnimatorUpdateMode.Normal;
        }
        void OnAnimatorIK()
        {
            if (RootBone != null)
                NotAffectedEulerAngles = RootBone.localEulerAngles;
        }
        void LateUpdate()
        {
            DoInert();
        }
        private void OnEnable()
        {
            Speed = 0;
            Lean = 0;
        }
        void DoInert()
        {
            if (RootBone == null) return;
            NotAffectedUpward = RootBone.up;

            if (TPSCharacter.IsMeleeAttacking || TPSCharacter.IsRagdolled || TPSCharacter.IsAiming || TPSCharacter.FiringMode || TPSCharacter.IsDriving || TPSCharacter.IsDead || !TPSCharacter.IsGrounded || TPSCharacter.IsProne)
            {
                Speed = 0;
                Lean = Mathf.Lerp(Lean, 0f, RootBoneLeanSpeed * Time.deltaTime);
                return;
            }

            Speed = Mathf.Lerp(Speed, TPSCharacter.VelocityMultiplier, 10 * Time.deltaTime);

            if (TPSCharacter.IsMoving)
            {
                float targetLean = Mathf.Clamp(Speed * RootBoneLeanIntensity / Mathf.Max(BlockForwardLeanWeight, 4f), -8f, 8f);
                Lean = Mathf.Lerp(Lean, targetLean, RootBoneLeanSpeed * Time.deltaTime);
            }
            else
            {
                Lean = Mathf.Lerp(Lean, 0f, RootBoneLeanSpeed * Time.deltaTime);
                if (JUFootPlacer != null && RootBoneSpineMovement)
                {
                    JUFootPlacer.LastBodyPositionY -= RootBoneDownMovementIntensity * Mathf.Abs(Lean) / 10 * Time.deltaTime;
                }
            }

            if (Mathf.Abs(Lean) > 0.01f)
            {
                RootBone.rotation = Quaternion.AngleAxis(Lean, TPSCharacter.transform.right) * RootBone.rotation;
            }
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (RootBone == null) return;
            float angle = Vector3.SignedAngle(NotAffectedUpward, RootBone.up, RootBone.right);
            if (angle == 0) return;

            Color color = Color.Lerp(Color.green, Color.red, angle / 10);
            Handles.color = color;
            Handles.DrawWireArc(RootBone.position, -RootBone.right, RootBone.up, angle, 0.5f);

            Color colortransparent = color; colortransparent.a = 0.1f;
            Handles.color = colortransparent;
            Handles.DrawSolidArc(RootBone.position, -RootBone.right, RootBone.up, angle, 0.5f);

            Handles.DrawLine(RootBone.position, RootBone.position + RootBone.up * 0.5f);
            Handles.color = Color.white;
            Handles.DrawDottedLine(RootBone.position, RootBone.position + NotAffectedUpward * 0.5f, 2);
            Handles.Label(RootBone.position + NotAffectedUpward * 0.6f, ((int)angle).ToString());
        }
#endif
    }
}