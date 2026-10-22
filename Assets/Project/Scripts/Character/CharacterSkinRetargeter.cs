using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// Retargets a humanoid character visual prefab (such as Man_02) onto an existing
    /// JU TPS character rig in LateUpdate, preserving all weapons, hand IK, ragdoll physics,
    /// inventory items, and animations without modifying the underlying gameplay skeleton.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    [AddComponentMenu("Project/Character/Character Skin Retargeter")]
    public class CharacterSkinRetargeter : MonoBehaviour
    {
        [Serializable]
        public struct BoneMapping
        {
            public string Name;
            public Transform SourceBone;
            public Transform TargetBone;
            public Quaternion RotationOffset;
            public bool IsFingerOrTwist;
        }

        [Header("References")]
        public Transform SourceHips;
        public Transform TargetHips;
        public Transform TargetSkinRoot;

        [Header("Hand IK Alignment (For Exact Weapon Grip)")]
        public bool AlignHandsToWeaponGrip = true;
        public Transform SourceLeftUpperArm;
        public Transform SourceLeftLowerArm;
        public Transform SourceLeftHand;
        public Transform TargetLeftUpperArm;
        public Transform TargetLeftLowerArm;
        public Transform TargetLeftHand;
        public Quaternion LeftUpperArmRotOffset;
        public Quaternion LeftLowerArmRotOffset;
        public Quaternion LeftHandRotOffset;

        public Transform SourceRightUpperArm;
        public Transform SourceRightLowerArm;
        public Transform SourceRightHand;
        public Transform TargetRightUpperArm;
        public Transform TargetRightLowerArm;
        public Transform TargetRightHand;
        public Quaternion RightUpperArmRotOffset;
        public Quaternion RightLowerArmRotOffset;
        public Quaternion RightHandRotOffset;

        [Header("Mapped Bones")]
        public List<BoneMapping> BoneMappings = new List<BoneMapping>();

        private void LateUpdate()
        {
            UpdateSkinPose();
        }

        public void UpdateSkinPose()
        {
            if (SourceHips == null || TargetHips == null || BoneMappings == null || BoneMappings.Count == 0)
                return;

            // 1. Sync Hips world position
            TargetHips.position = SourceHips.position;

            // 2. Sync primary body/limb bone rotations in hierarchy order (parent -> child)
            int count = BoneMappings.Count;
            for (int i = 0; i < count; i++)
            {
                BoneMapping m = BoneMappings[i];
                if (!m.IsFingerOrTwist && m.SourceBone != null && m.TargetBone != null)
                {
                    m.TargetBone.rotation = m.SourceBone.rotation * m.RotationOffset;
                }
            }

            // 3. Analytical 2-Bone Arm IK alignment so Target hands match Source hands (weapon grips)
            if (AlignHandsToWeaponGrip)
            {
                SolveArmTwoBoneIK(
                    SourceLeftUpperArm, SourceLeftLowerArm, SourceLeftHand,
                    TargetLeftUpperArm, TargetLeftLowerArm, TargetLeftHand,
                    LeftUpperArmRotOffset, LeftLowerArmRotOffset, LeftHandRotOffset);

                SolveArmTwoBoneIK(
                    SourceRightUpperArm, SourceRightLowerArm, SourceRightHand,
                    TargetRightUpperArm, TargetRightLowerArm, TargetRightHand,
                    RightUpperArmRotOffset, RightLowerArmRotOffset, RightHandRotOffset);
            }

            // 4. Sync fingers and forearm twist bones AFTER arm & hand IK is finalized
            for (int i = 0; i < count; i++)
            {
                BoneMapping m = BoneMappings[i];
                if (m.IsFingerOrTwist && m.SourceBone != null && m.TargetBone != null)
                {
                    m.TargetBone.rotation = m.SourceBone.rotation * m.RotationOffset;
                }
            }
        }

        private static void SolveArmTwoBoneIK(
            Transform srcUpper, Transform srcLower, Transform srcHand,
            Transform dstUpper, Transform dstLower, Transform dstHand,
            Quaternion upperOffset, Quaternion lowerOffset, Quaternion handOffset)
        {
            if (srcUpper == null || srcLower == null || srcHand == null ||
                dstUpper == null || dstLower == null || dstHand == null)
                return;

            Vector3 rootPos = dstUpper.position;
            Vector3 midPos = dstLower.position;
            Vector3 endPos = dstHand.position;
            Vector3 targetPos = srcHand.position;
            Vector3 elbowHint = srcLower.position;

            float len1 = Vector3.Distance(rootPos, midPos);
            float len2 = Vector3.Distance(midPos, endPos);
            if (len1 < 0.001f || len2 < 0.001f) return;

            Vector3 toTarget = targetPos - rootPos;
            float dist = toTarget.magnitude;
            float maxReach = (len1 + len2) * 0.999f;
            float minReach = Mathf.Abs(len1 - len2) + 0.005f;
            float clampedDist = Mathf.Clamp(dist, minReach, maxReach);

            // Law of cosines for upper arm angle
            float cosAngle0 = Mathf.Clamp(
                (len1 * len1 + clampedDist * clampedDist - len2 * len2) / (2f * len1 * clampedDist),
                -1f, 1f);
            float angle0 = Mathf.Acos(cosAngle0);

            Vector3 dirToTarget = toTarget / Mathf.Max(dist, 0.0001f);
            Vector3 toHint = elbowHint - rootPos;
            Vector3 bendPlaneNormal = Vector3.Cross(dirToTarget, toHint);
            if (bendPlaneNormal.sqrMagnitude < 0.0001f)
            {
                bendPlaneNormal = Vector3.Cross(dirToTarget, Vector3.up);
            }
            bendPlaneNormal.Normalize();

            // Desired elbow world position
            Vector3 bendDir = Quaternion.AngleAxis(angle0 * Mathf.Rad2Deg, bendPlaneNormal) * dirToTarget;
            Vector3 desiredMidPos = rootPos + bendDir * len1;

            // Swing dstUpper from current (midPos - rootPos) to (desiredMidPos - rootPos)
            Vector3 curUpperDir = (midPos - rootPos).normalized;
            Vector3 newUpperDir = (desiredMidPos - rootPos).normalized;
            Quaternion upperSwing = Quaternion.FromToRotation(curUpperDir, newUpperDir);
            dstUpper.rotation = upperSwing * dstUpper.rotation;

            // Swing dstLower so dstHand lands on targetPos
            Vector3 newMidPos = dstLower.position;
            Vector3 newEndPos = dstHand.position;
            Vector3 curLowerDir = (newEndPos - newMidPos).normalized;
            Vector3 newLowerDir = (targetPos - newMidPos).normalized;
            Quaternion lowerSwing = Quaternion.FromToRotation(curLowerDir, newLowerDir);
            dstLower.rotation = lowerSwing * dstLower.rotation;

            // Lock hand rotation to match source hand rotation + offset
            dstHand.rotation = srcHand.rotation * handOffset;
        }
    }
}
