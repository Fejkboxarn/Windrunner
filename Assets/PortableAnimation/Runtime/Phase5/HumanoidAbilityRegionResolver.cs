using System;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Resolves PortableAnimation body regions from a real Humanoid avatar first,
    /// then falls back to the authored path map. This keeps character-specific
    /// skeleton paths from silently zeroing upper-body montage masks.
    /// </summary>
    public static class HumanoidAbilityRegionResolver
    {
        public static AbilityRegion Resolve(
            Animator animator,
            Transform bone,
            string relativePath,
            AbilityRegionMap fallback)
        {
            if (animator != null &&
                animator.avatar != null &&
                animator.avatar.isValid &&
                animator.isHuman &&
                bone != null)
            {
                AbilityRegion humanoid = ResolveHumanoid(animator, bone);
                if (humanoid != AbilityRegion.None)
                    return humanoid;
            }

            return fallback != null
                ? fallback.Resolve(relativePath)
                : AbilityRegion.None;
        }

        private static AbilityRegion ResolveHumanoid(
            Animator animator,
            Transform bone)
        {
            // Most-specific groups first. Helper/twist bones inherit the region
            // of the nearest canonical Humanoid ancestor.
            if (Under(animator, bone, HumanBodyBones.Head) ||
                Under(animator, bone, HumanBodyBones.Neck) ||
                Under(animator, bone, HumanBodyBones.Jaw) ||
                Under(animator, bone, HumanBodyBones.LeftEye) ||
                Under(animator, bone, HumanBodyBones.RightEye))
                return AbilityRegion.Head;

            if (Under(animator, bone, HumanBodyBones.LeftHand) ||
                Under(animator, bone, HumanBodyBones.LeftLowerArm) ||
                Under(animator, bone, HumanBodyBones.LeftUpperArm))
                return AbilityRegion.LeftArm;

            if (Under(animator, bone, HumanBodyBones.LeftShoulder))
                return AbilityRegion.LeftClavicle;

            if (Under(animator, bone, HumanBodyBones.RightHand) ||
                Under(animator, bone, HumanBodyBones.RightLowerArm) ||
                Under(animator, bone, HumanBodyBones.RightUpperArm))
                return AbilityRegion.RightArm;

            if (Under(animator, bone, HumanBodyBones.RightShoulder))
                return AbilityRegion.RightClavicle;

            if (Under(animator, bone, HumanBodyBones.LeftToes) ||
                Under(animator, bone, HumanBodyBones.LeftFoot) ||
                Under(animator, bone, HumanBodyBones.LeftLowerLeg) ||
                Under(animator, bone, HumanBodyBones.LeftUpperLeg))
                return AbilityRegion.LeftLeg;

            if (Under(animator, bone, HumanBodyBones.RightToes) ||
                Under(animator, bone, HumanBodyBones.RightFoot) ||
                Under(animator, bone, HumanBodyBones.RightLowerLeg) ||
                Under(animator, bone, HumanBodyBones.RightUpperLeg))
                return AbilityRegion.RightLeg;

            if (Under(animator, bone, HumanBodyBones.UpperChest))
                return AbilityRegion.Spine3;

            if (Under(animator, bone, HumanBodyBones.Chest))
                return AbilityRegion.Spine2;

            if (Under(animator, bone, HumanBodyBones.Spine))
                return AbilityRegion.Spine1;

            if (Under(animator, bone, HumanBodyBones.Hips))
                return AbilityRegion.Pelvis;

            return AbilityRegion.None;
        }

        private static bool Under(
            Animator animator,
            Transform bone,
            HumanBodyBones humanBone)
        {
            if (humanBone == HumanBodyBones.LastBone)
                return false;

            Transform anchor;
            try
            {
                anchor = animator.GetBoneTransform(humanBone);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (anchor == null)
                return false;

            for (Transform current = bone;
                 current != null;
                 current = current.parent)
            {
                if (current == anchor)
                    return true;

                if (current == animator.transform)
                    break;
            }

            return false;
        }
    }
}
