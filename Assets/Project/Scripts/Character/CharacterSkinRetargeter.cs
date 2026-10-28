using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// Retargets a humanoid character visual prefab (such as Anim_Idle_MIL_R5) onto an existing
    /// JU TPS character rig in LateUpdate, preserving all weapons, hand IK, ragdoll physics,
    /// inventory items, and animations without modifying the underlying gameplay skeleton.
    /// Also solves 2-bone IK for legs and arms to eliminate foot/shin terrain penetration
    /// caused by mesh proportions and thick combat boot soles.
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

        [Header("Hips Position Offset")]
        [Tooltip("Vertical and local offset for the target hips to compensate for mesh height and leg proportion differences.")]
        public Vector3 HipsPositionOffset = new Vector3(0, 0.12f, 0);

        [Header("Leg IK Alignment (Prevents Terrain & Shin Clipping)")]
        public bool AlignLegsToFootPlacement = true;
        [Tooltip("Vertical offset to account for thick combat boot soles and ankle bone height.")]
        public float FootHeightOffset = 0.14f;

        public Transform SourceLeftUpperLeg;
        public Transform SourceLeftLowerLeg;
        public Transform SourceLeftFoot;
        public Transform SourceLeftToe;
        public Transform TargetLeftUpperLeg;
        public Transform TargetLeftLowerLeg;
        public Transform TargetLeftFoot;
        public Transform TargetLeftToe;
        public Quaternion LeftUpperLegRotOffset = Quaternion.identity;
        public Quaternion LeftLowerLegRotOffset = Quaternion.identity;
        public Quaternion LeftFootRotOffset = Quaternion.identity;
        public Quaternion LeftToeRotOffset = Quaternion.identity;

        public Transform SourceRightUpperLeg;
        public Transform SourceRightLowerLeg;
        public Transform SourceRightFoot;
        public Transform SourceRightToe;
        public Transform TargetRightUpperLeg;
        public Transform TargetRightLowerLeg;
        public Transform TargetRightFoot;
        public Transform TargetRightToe;
        public Quaternion RightUpperLegRotOffset = Quaternion.identity;
        public Quaternion RightLowerLegRotOffset = Quaternion.identity;
        public Quaternion RightFootRotOffset = Quaternion.identity;
        public Quaternion RightToeRotOffset = Quaternion.identity;

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

        private void OnEnable()
        {
            ValidateOrAutoBindLegs();
        }

        public void ValidateOrAutoBindLegs()
        {
            if (SourceHips == null || TargetHips == null) return;

            // Auto-resolve leg bone references if unassigned
            if (SourceLeftUpperLeg == null) SourceLeftUpperLeg = FindDeepChild(SourceHips, "LeftUpLeg");
            if (SourceLeftLowerLeg == null && SourceLeftUpperLeg != null) SourceLeftLowerLeg = FindDeepChild(SourceLeftUpperLeg, "LeftLeg");
            if (SourceLeftFoot == null && SourceLeftLowerLeg != null) SourceLeftFoot = FindDeepChild(SourceLeftLowerLeg, "LeftFoot");
            if (SourceLeftToe == null && SourceLeftFoot != null) SourceLeftToe = FindDeepChild(SourceLeftFoot, "LeftToeBase");

            if (TargetLeftUpperLeg == null) TargetLeftUpperLeg = FindDeepChild(TargetHips, "mixamorig:LeftUpLeg");
            if (TargetLeftLowerLeg == null && TargetLeftUpperLeg != null) TargetLeftLowerLeg = FindDeepChild(TargetLeftUpperLeg, "mixamorig:LeftLeg");
            if (TargetLeftFoot == null && TargetLeftLowerLeg != null) TargetLeftFoot = FindDeepChild(TargetLeftLowerLeg, "mixamorig:LeftFoot");
            if (TargetLeftToe == null && TargetLeftFoot != null) TargetLeftToe = FindDeepChild(TargetLeftFoot, "mixamorig:LeftToeBase");

            if (SourceRightUpperLeg == null) SourceRightUpperLeg = FindDeepChild(SourceHips, "RightUpLeg");
            if (SourceRightLowerLeg == null && SourceRightUpperLeg != null) SourceRightLowerLeg = FindDeepChild(SourceRightUpperLeg, "RightLeg");
            if (SourceRightFoot == null && SourceRightLowerLeg != null) SourceRightFoot = FindDeepChild(SourceRightLowerLeg, "RightFoot");
            if (SourceRightToe == null && SourceRightFoot != null) SourceRightToe = FindDeepChild(SourceRightFoot, "RightToeBase");

            if (TargetRightUpperLeg == null) TargetRightUpperLeg = FindDeepChild(TargetHips, "mixamorig:RightUpLeg");
            if (TargetRightLowerLeg == null && TargetRightUpperLeg != null) TargetRightLowerLeg = FindDeepChild(TargetRightUpperLeg, "mixamorig:RightLeg");
            if (TargetRightFoot == null && TargetRightLowerLeg != null) TargetRightFoot = FindDeepChild(TargetRightLowerLeg, "mixamorig:RightFoot");
            if (TargetRightToe == null && TargetRightFoot != null) TargetRightToe = FindDeepChild(TargetRightFoot, "mixamorig:RightToeBase");

            // Populate rotation offsets from BoneMappings if identity
            if (BoneMappings != null)
            {
                foreach (var m in BoneMappings)
                {
                    if (m.SourceBone == null) continue;
                    string n = m.SourceBone.name;
                    if (n == "LeftUpLeg") LeftUpperLegRotOffset = m.RotationOffset;
                    else if (n == "LeftLeg") LeftLowerLegRotOffset = m.RotationOffset;
                    else if (n == "LeftFoot") LeftFootRotOffset = m.RotationOffset;
                    else if (n == "LeftToeBase") LeftToeRotOffset = m.RotationOffset;
                    else if (n == "RightUpLeg") RightUpperLegRotOffset = m.RotationOffset;
                    else if (n == "RightLeg") RightLowerLegRotOffset = m.RotationOffset;
                    else if (n == "RightFoot") RightFootRotOffset = m.RotationOffset;
                    else if (n == "RightToeBase") RightToeRotOffset = m.RotationOffset;
                }
            }
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            Transform direct = parent.Find(childName);
            if (direct != null) return direct;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform res = FindDeepChild(parent.GetChild(i), childName);
                if (res != null) return res;
            }
            return null;
        }

        private void LateUpdate()
        {
            UpdateSkinPose();
        }

        public void UpdateSkinPose()
        {
            if (SourceHips == null || TargetHips == null || BoneMappings == null || BoneMappings.Count == 0)
                return;

            // 1. Sync Hips world position with vertical/proportional offset
            TargetHips.position = SourceHips.position + transform.TransformDirection(HipsPositionOffset);

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

            // 4. Analytical 2-Bone Leg IK alignment so Target feet and shins rest naturally on terrain
            if (AlignLegsToFootPlacement)
            {
                SolveLegTwoBoneIK(
                    SourceLeftUpperLeg, SourceLeftLowerLeg, SourceLeftFoot,
                    TargetLeftUpperLeg, TargetLeftLowerLeg, TargetLeftFoot,
                    LeftUpperLegRotOffset, LeftLowerLegRotOffset, LeftFootRotOffset,
                    FootHeightOffset, transform.forward);

                SolveLegTwoBoneIK(
                    SourceRightUpperLeg, SourceRightLowerLeg, SourceRightFoot,
                    TargetRightUpperLeg, TargetRightLowerLeg, TargetRightFoot,
                    RightUpperLegRotOffset, RightLowerLegRotOffset, RightFootRotOffset,
                    FootHeightOffset, transform.forward);

                if (SourceLeftToe != null && TargetLeftToe != null)
                {
                    TargetLeftToe.rotation = SourceLeftToe.rotation * LeftToeRotOffset;
                }
                if (SourceRightToe != null && TargetRightToe != null)
                {
                    TargetRightToe.rotation = SourceRightToe.rotation * RightToeRotOffset;
                }
            }

            // 5. Sync fingers and forearm twist bones AFTER limb IK is finalized
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
            Vector3 curLowerDir = (dstHand.position - newMidPos).normalized;
            Vector3 newLowerDir = (targetPos - newMidPos).normalized;
            Quaternion lowerSwing = Quaternion.FromToRotation(curLowerDir, newLowerDir);
            dstLower.rotation = lowerSwing * dstLower.rotation;

            // Lock hand rotation to match source hand rotation + offset
            dstHand.rotation = srcHand.rotation * handOffset;
        }

        private static void SolveLegTwoBoneIK(
            Transform srcUpper, Transform srcLower, Transform srcFoot,
            Transform dstUpper, Transform dstLower, Transform dstFoot,
            Quaternion upperOffset, Quaternion lowerOffset, Quaternion footOffset,
            float footHeightOffset, Vector3 characterForward)
        {
            if (srcUpper == null || srcLower == null || srcFoot == null ||
                dstUpper == null || dstLower == null || dstFoot == null)
                return;

            Vector3 rootPos = dstUpper.position;
            Vector3 midPos = dstLower.position;
            Vector3 endPos = dstFoot.position;

            // Target position for foot ankle: source foot ankle raised along world up by footHeightOffset
            Vector3 targetPos = srcFoot.position + Vector3.up * footHeightOffset;
            Vector3 kneeHint = srcLower.position;

            float len1 = Vector3.Distance(rootPos, midPos);
            float len2 = Vector3.Distance(midPos, endPos);
            if (len1 < 0.001f || len2 < 0.001f) return;

            Vector3 toTarget = targetPos - rootPos;
            float dist = toTarget.magnitude;
            float maxReach = (len1 + len2) * 0.999f;
            float minReach = Mathf.Abs(len1 - len2) + 0.005f;
            float clampedDist = Mathf.Clamp(dist, minReach, maxReach);

            // Law of cosines for upper leg angle
            float cosAngle0 = Mathf.Clamp(
                (len1 * len1 + clampedDist * clampedDist - len2 * len2) / (2f * len1 * clampedDist),
                -1f, 1f);
            float angle0 = Mathf.Acos(cosAngle0);

            Vector3 dirToTarget = toTarget / Mathf.Max(dist, 0.0001f);
            Vector3 toHint = kneeHint - rootPos;
            Vector3 bendPlaneNormal = Vector3.Cross(dirToTarget, toHint);

            // Fallback if knee hint is collinear with leg line
            if (bendPlaneNormal.sqrMagnitude < 0.0001f)
            {
                bendPlaneNormal = Vector3.Cross(dirToTarget, characterForward);
                if (bendPlaneNormal.sqrMagnitude < 0.0001f)
                {
                    bendPlaneNormal = dstUpper.right;
                }
            }
            bendPlaneNormal.Normalize();

            // Desired knee world position
            Vector3 bendDir = Quaternion.AngleAxis(angle0 * Mathf.Rad2Deg, bendPlaneNormal) * dirToTarget;
            Vector3 desiredMidPos = rootPos + bendDir * len1;

            // Swing dstUpper from current direction (midPos - rootPos) to (desiredMidPos - rootPos)
            Vector3 curUpperDir = (midPos - rootPos).normalized;
            Vector3 newUpperDir = (desiredMidPos - rootPos).normalized;
            Quaternion upperSwing = Quaternion.FromToRotation(curUpperDir, newUpperDir);
            dstUpper.rotation = upperSwing * dstUpper.rotation;

            // Swing dstLower so dstFoot lands on targetPos
            Vector3 newMidPos = dstLower.position;
            Vector3 curLowerDir = (dstFoot.position - newMidPos).normalized;
            Vector3 newLowerDir = (targetPos - newMidPos).normalized;
            Quaternion lowerSwing = Quaternion.FromToRotation(curLowerDir, newLowerDir);
            dstLower.rotation = lowerSwing * dstLower.rotation;

            // Align foot rotation to source foot rotation + offset
            dstFoot.rotation = srcFoot.rotation * footOffset;
        }
    }
}
