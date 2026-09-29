using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "PlayerHumanoidAnimationProfile",
        menuName = "Airflow Prototype/Player Humanoid Animation Profile")]
    public sealed class PlayerHumanoidAnimationProfile : ScriptableObject
    {
        [Header("Grounded Locomotion")]
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;

        [Header("Airborne")]
        public AnimationClip jump;

        [Tooltip(
            "Add as many Double Jump / aerial flourish clips as you want. " +
            "One is chosen randomly every time the old traversal-flip event fires.")]
        public List<AnimationClip> doubleJumps =
            new List<AnimationClip>();

        [SerializeField, HideInInspector]
        private AnimationClip doubleJump;

        public AnimationClip fall;
        public AnimationClip land;

        [Header("Sling / Death From Above")]
        public AnimationClip slingStart;
        public AnimationClip slingAir;
        public AnimationClip slingImpact;

        [Header("Locomotion Blend Thresholds")]
        [Range(0.05f, 0.8f)]
        public float walkThreshold = 0.35f;

        [Range(0.2f, 1.5f)]
        public float runThreshold = 1f;

        [Header("Transition Feel")]
        [Range(0f, 0.3f)]
        public float locomotionTransitionDuration = 0.08f;

        [Range(0f, 0.3f)]
        public float jumpTransitionDuration = 0.05f;

        [Range(0f, 0.3f)]
        public float fallTransitionDuration = 0.08f;

        [Range(0f, 0.3f)]
        public float doubleJumpTransitionDuration = 0.04f;

        [Range(0f, 0.3f)]
        public float landTransitionDuration = 0.04f;

        [Range(0f, 0.3f)]
        public float slingTransitionDuration = 0.04f;

        [Range(0.1f, 1f)]
        public float doubleJumpExitTime = 0.86f;

        [Range(0.1f, 1f)]
        public float landExitTime = 0.82f;

        [Range(0.1f, 1f)]
        public float slingImpactExitTime = 0.88f;

        [SerializeField, HideInInspector]
        private int profileVersion = 194;

        private void OnEnable()
        {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults()
        {
            if (profileVersion < 193)
            {
                // Only initialize NEW v19.3 fields.
                // Existing locomotion/Sling clip assignments and tuning are preserved.
                doubleJumpTransitionDuration = 0.04f;
                doubleJumpExitTime = 0.86f;

                profileVersion = 193;
            }

            if (profileVersion < 194)
            {
                if (doubleJumps == null)
                {
                    doubleJumps =
                        new List<AnimationClip>();
                }

                // Preserve the old single Double Jump assignment as variant 1.
                if (doubleJump != null &&
                    !doubleJumps.Contains(
                        doubleJump))
                {
                    doubleJumps.Add(
                        doubleJump);
                }

                // The old 0.04 second Any State blend was enough to make the
                // boost happen visibly before the flourish. New controllers
                // enter the chosen Double Jump state immediately.
                doubleJumpTransitionDuration = 0f;

                profileVersion = 194;
            }
        }

        public int DoubleJumpVariantCount
        {
            get
            {
                if (doubleJumps == null)
                    return 0;

                int count = 0;

                for (int i = 0;
                     i < doubleJumps.Count;
                     i++)
                {
                    if (doubleJumps[i] != null)
                        count++;
                }

                return count;
            }
        }

        public AnimationClip GetDoubleJumpVariant(
            int validIndex)
        {
            if (doubleJumps == null ||
                validIndex < 0)
            {
                return null;
            }

            int found = 0;

            for (int i = 0;
                 i < doubleJumps.Count;
                 i++)
            {
                AnimationClip clip =
                    doubleJumps[i];

                if (clip == null)
                    continue;

                if (found == validIndex)
                    return clip;

                found++;
            }

            return null;
        }

        private void OnValidate()
        {
            EnsureCurrentDefaults();
            walkThreshold =
                Mathf.Clamp(
                    walkThreshold,
                    0.05f,
                    0.8f);

            runThreshold =
                Mathf.Max(
                    walkThreshold + 0.05f,
                    runThreshold);

            locomotionTransitionDuration =
                Mathf.Max(
                    0f,
                    locomotionTransitionDuration);

            jumpTransitionDuration =
                Mathf.Max(
                    0f,
                    jumpTransitionDuration);

            fallTransitionDuration =
                Mathf.Max(
                    0f,
                    fallTransitionDuration);

            doubleJumpTransitionDuration =
                Mathf.Max(
                    0f,
                    doubleJumpTransitionDuration);

            doubleJumpExitTime =
                Mathf.Clamp01(
                    doubleJumpExitTime);

            landTransitionDuration =
                Mathf.Max(
                    0f,
                    landTransitionDuration);

            slingTransitionDuration =
                Mathf.Max(
                    0f,
                    slingTransitionDuration);
        }
    }
}
