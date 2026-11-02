using System;
using PortableAnimation;
using UnityEngine;
using Windrunner.Production.Feedback;

namespace Windrunner.Production.Animation
{
    [Serializable]
    public sealed class WindrunnerAnimationActionBinding
    {
        public FeedbackKey feedbackEvent;
        [Tooltip("Index in the portable montage profile. -1 means observe only.")]
        public int montageSlot = -1;
    }

    /// <summary>
    /// Project-specific animation configuration without any Windrunner motor
    /// dependencies. Importing this asset cannot steal an Animator.
    /// </summary>
    [CreateAssetMenu(menuName = "Windrunner/Production/Animation Profile",
        fileName = "WindrunnerAnimationProfile")]
    public sealed class WindrunnerAnimationProfile : ScriptableObject
    {
        [Header("Migration gate")]
        [Tooltip("OFF by default. Manual Play Mode start is available through the Batch 4 menu.")]
        public bool automaticallyStartPortableInPlayMode;

        [Header("Portable profile chain")]
        public WeaponPoseProfile weaponProfile;
        public GroundMotionProfile groundProfile;
        public AeroMotionProfile aeroProfile;
        public CharacterExtensionProfile extensionProfile;
        public PostPoseIKProfile postPoseIKProfile;
        public ContextualActionInfluenceProfile contextualActionProfile;

        [Header("Motion normalization")]
        [Min(0.1f)] public float fullRunSpeed = 8.5f;
        [Range(0.25f, 2f)] public float motionSpeedMultiplier = 1f;
        [Tooltip("Rotation of locomotion coordinates around local Y. Use only if character rig has a heading offset.")]
        [Range(-180f, 180f)] public float locomotionYawOffset;
        public bool enableNativeAimOffset;

        [Header("Semantic event -> montage slot")]
        [Tooltip("Assignments begin at -1 so the test never plays a random animation.")]
        public WindrunnerAnimationActionBinding[] actionBindings =
        {
            new WindrunnerAnimationActionBinding { feedbackEvent = FeedbackKey.Flip },
            new WindrunnerAnimationActionBinding { feedbackEvent = FeedbackKey.CastReleased },
            new WindrunnerAnimationActionBinding { feedbackEvent = FeedbackKey.SlingDashStarted },
            new WindrunnerAnimationActionBinding { feedbackEvent = FeedbackKey.SlingImpact },
            new WindrunnerAnimationActionBinding { feedbackEvent = FeedbackKey.SlingLaunched },
        };

        public bool ValidateForLiveGraph(out string error)
        {
            if (weaponProfile == null || groundProfile == null)
            {
                error = "A valid weapon-profile chain and 18-pose GroundMotionProfile " +
                        "are required before Playables can own the Animator. " +
                        "Observation mode works without them.";
                return false;
            }
            if (groundProfile.PreviousPipeline != weaponProfile)
            {
                error = "GroundMotionProfile.PreviousPipeline must point to this weapon profile.";
                return false;
            }
            if (aeroProfile != null && aeroProfile.PreviousPipeline != groundProfile)
            {
                error = "AeroMotionProfile.PreviousPipeline must point to this ground profile.";
                return false;
            }
            if (extensionProfile != null && (aeroProfile == null ||
                extensionProfile.PreviousPipeline != aeroProfile))
            {
                error = "CharacterExtensionProfile requires the selected AeroMotionProfile.";
                return false;
            }
            if (postPoseIKProfile != null && (extensionProfile == null ||
                postPoseIKProfile.PreviousPipeline != extensionProfile))
            {
                error = "PostPoseIKProfile requires the selected CharacterExtensionProfile.";
                return false;
            }
            try
            {
                weaponProfile.AssertValid();
                groundProfile.AssertValid();
                aeroProfile?.AssertValid();
                extensionProfile?.AssertValid();
                postPoseIKProfile?.AssertValid();
                if (fullRunSpeed < 0.1f)
                    throw new InvalidOperationException("Full-run speed must exceed zero.");
                int actionCount = weaponProfile.PreviousPipeline.PreviousPipeline
                    .AimOffsetProfile.MontageProfile.Actions.Count;
                if (actionBindings != null)
                {
                    var seen = new System.Collections.Generic.HashSet<FeedbackKey>();
                    foreach (WindrunnerAnimationActionBinding binding in actionBindings)
                    {
                        if (binding == null) continue;
                        if (binding.montageSlot < -1 ||
                            binding.montageSlot >= actionCount)
                            throw new InvalidOperationException("Invalid montage slot for " +
                                binding.feedbackEvent + ": " + binding.montageSlot +
                                ". Available actions=" + actionCount);
                        if (!seen.Add(binding.feedbackEvent))
                            throw new InvalidOperationException(
                                "Duplicate semantic binding for " + binding.feedbackEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            error = "";
            return true;
        }
    }
}
