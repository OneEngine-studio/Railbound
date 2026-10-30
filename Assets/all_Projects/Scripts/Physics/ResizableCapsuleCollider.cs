using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JUTPSEditor.JUHeader;

namespace JUTPS.PhysicsScripts
{
    [RequireComponent(typeof(Animator), typeof(CapsuleCollider))]
    [AddComponentMenu("JU TPS/Physics/Resizable Capsule Collider")]
    public class ResizableCapsuleCollider : JUTPSActions.JUTPSAction
    {
        [JUHeader("Capsule Resize")]
        public CapsuleCollider CapsuleToResize;
        public float HeightOffset = 0.73f;
        public float ProneAndRollCenterY = 0.38f; 
        public Vector3 CenterOffset;

        [JUSubHeader("Prone Collider Settings")]
        public float ProneRadius = 0.16f;
        public float ProneCenterY = 0.26f;
        public float ProneCenterZ = -0.05f;
        public float ProneHeight = 1.70f;

        [JUSubHeader("Roll Collider Settings")]
        public float RollRadius = 0.35f;
        public float RollCenterY = 0.38f;

        private float StartHeight = 2.118f;
        private Vector3 StartCenter = new Vector3(0, 0.651f, 0);
        private float StartRadius = 0.40f;

        private Transform rightFoot, leftFoot, head;

        public override void Awake()
        {
            base.Awake();
            if (CapsuleToResize == null) CapsuleToResize = GetComponent<CapsuleCollider>();
            if (CapsuleToResize != null)
            {
                StartHeight = CapsuleToResize.height;
                StartCenter = CapsuleToResize.center;
                StartRadius = CapsuleToResize.radius;
            }
            GetFeetReferences();
        }

        void Start()
        {
            if (CapsuleToResize == null) CapsuleToResize = GetComponent<CapsuleCollider>();
            if (CapsuleToResize != null)
            {
                if (StartHeight <= 0.01f) StartHeight = CapsuleToResize.height;
                if (StartRadius <= 0.01f) StartRadius = CapsuleToResize.radius;
                if (StartCenter == Vector3.zero) StartCenter = CapsuleToResize.center;
            }

            GetFeetReferences();
        }

        // Update is called once per frame
        void Update()
        {
            if (CapsuleToResize == null) return;
            if (TPSCharacter == null) TPSCharacter = GetComponent<JUCharacterController>();
            if (TPSCharacter == null) return;

            if (head == null || rightFoot == null || leftFoot == null)
            {
                GetFeetReferences();
                if (head == null || rightFoot == null || leftFoot == null) return;
            }

            if (TPSCharacter.IsProne)
            {
                CapsuleToResize.direction = 2;
                CapsuleToResize.radius = ProneRadius;
                CapsuleToResize.height = ProneHeight;
                CapsuleToResize.center = new Vector3(0, ProneCenterY, ProneCenterZ);
            }
            else if (TPSCharacter.IsRolling)
            {
                CapsuleToResize.direction = 2;
                CapsuleToResize.radius = RollRadius;
                CapsuleToResize.height = Mathf.Clamp(HeadFeetDistance() * StartHeight * HeightOffset, 1.2f, 1.8f);
                CapsuleToResize.center = new Vector3(0, RollCenterY, 0);
            }
            else
            {
                CapsuleToResize.direction = 1;
                CapsuleToResize.radius = StartRadius;
                if (TPSCharacter.IsGrounded)
                {
                    CapsuleToResize.height = HeadFeetDistance() * StartHeight * HeightOffset;
                    CapsuleToResize.center = CenterOffset + new Vector3(0, CapsuleToResize.height / 2, 0);
                }
                else
                {
                    CapsuleToResize.height = HeadFeetDistance() * StartHeight * HeightOffset;
                    CapsuleToResize.center = CenterOffset + transform.InverseTransformPoint(GetBodyCenter());
                    CapsuleToResize.center = new Vector3(0, CapsuleToResize.center.y, 0);
                }
            }
        }

        public float HeadFeetDistance()
        {
            if (head == null || rightFoot == null || leftFoot == null) return 1.35f;
            return Vector3.Distance(head.position, GetMiddlePointBetweenFeets());
        }

        private void GetFeetReferences()
        {
            if (anim == null) { anim = GetComponent<Animator>(); }
            if (anim == null) return;

            head = anim.GetBoneTransform(HumanBodyBones.Head);
            rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            leftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        }

        public Vector3 GetMiddlePointBetweenFeets()
        {
            if (rightFoot == null || leftFoot == null)
            {
                return transform.position;
            }

            Vector3 middlePoint = Vector3.Lerp(rightFoot.position, leftFoot.position, 0.5f);
            return middlePoint;
        }

        public Vector3 GetBodyCenter()
        {
            if (head == null) return transform.position + Vector3.up;
            Vector3 middlePoint = Vector3.Lerp(head.position, GetMiddlePointBetweenFeets(), 0.5f);
            return middlePoint;
        }
    }
}
