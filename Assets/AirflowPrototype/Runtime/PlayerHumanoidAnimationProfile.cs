using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "PlayerHumanoidAnimationProfile",
        menuName = "Airflow Prototype/Player Humanoid Animation Profile")]
    public sealed class PlayerHumanoidAnimationProfile : ScriptableObject
    {
        [Header("Grounded Base")]
        public AnimationClip idle;
        public AnimationClip walk;

        [Header("Combat Run Loops")]
        public AnimationClip runForward;
        public AnimationClip runForwardLeft45;
        public AnimationClip runForwardRight45;
        public AnimationClip runLeft90;
        public AnimationClip runRight90;
        public AnimationClip runLeanLeft;
        public AnimationClip runLeanRight;

        [Header("Combat Run Starts")]
        public AnimationClip runStartForward;
        public AnimationClip runStartLeft;
        public AnimationClip runStartRight;
        public AnimationClip runStartBackLeft;
        public AnimationClip runStartBackRight;

        [Header("Combat Run Stop / Hard Turns")]
        public AnimationClip runStop;
        public AnimationClip runTurnLeft;
        public AnimationClip runTurnRight;

        [Header("Airborne")]
        public AnimationClip jump;

        [Tooltip(
            "Add as many Double Jump / aerial flourish clips as you want. " +
            "One is chosen randomly every time the traversal-flip reward event fires.")]
        public List<AnimationClip> doubleJumps =
            new List<AnimationClip>();

        [SerializeField, HideInInspector]
        private AnimationClip doubleJump;

        [Tooltip(
            "Chance that a chosen Double Jump variant is mirrored by the Animator.")]
        [Range(0f, 100f)]
        public float doubleJumpMirrorChancePercent = 35f;

        public AnimationClip fall;

        [Header("Landing")]
        [Tooltip("Landing animation used when horizontal speed is below the running threshold.")]
        public AnimationClip landIdle;

        [Tooltip("Landing animation used when horizontal speed is at or above the running threshold.")]
        public AnimationClip landRunning;

        [Tooltip("Horizontal speed at landing that selects Land Running instead of Land Idle.")]
        [Min(0f)]
        public float runningLandingSpeedThreshold = 3.25f;

        [SerializeField, HideInInspector]
        private AnimationClip land;

        [Header("Sling / Death From Above")]
        public AnimationClip slingStart;
        public AnimationClip slingAir;
        public AnimationClip slingImpact;

        [Header("Locomotion Blend")]
        [Range(0.05f, 0.8f)]
        public float walkThreshold = 0.35f;

        [Range(0.2f, 1.5f)]
        public float runThreshold = 1f;

        [Tooltip(
            "How much extra left/right lean is blended in when the character turns sharply.")]
        [Range(0f, 1f)]
        public float turnLeanBlendExtension = 0.5f;

        [Min(30f)]
        public float turnRateForFullLean = 240f;

        [Tooltip(
            "How quickly the directional run pose catches up when travel direction changes. " +
            "Lower values are softer/smoother; higher values react faster.")]
        [Min(0.1f)]
        public float locomotionDirectionResponse = 8f;

        [Header("Idle Start Facing Alignment")]
        [Tooltip(
            "When starting from rest while facing away from the requested movement direction, " +
            "temporarily treat the locomotion pose as forward while gameplay rotates the character.")]
        public bool alignFacingBeforeDirectionalBlend = true;

        [Tooltip(
            "If the requested movement direction is farther from the current facing than this angle, " +
            "the directional start clip is skipped and facing-alignment mode is used instead.")]
        [Range(5f, 120f)]
        public float maximumFacingErrorForAuthoredStart = 38f;

        [Tooltip(
            "Facing error below this angle releases the temporary forward-only start alignment.")]
        [Range(1f, 90f)]
        public float startFacingAlignmentReleaseAngle = 20f;

        [Tooltip(
            "Safety duration for facing-alignment mode if the character takes longer to rotate.")]
        [Min(0.05f)]
        public float startFacingAlignmentMaxDuration = 0.32f;

        [Header("Start / Stop / Turn Detection")]
        [Range(0.01f, 0.75f)]
        public float moveInputDeadzone = 0.12f;

        [Min(0f)]
        public float startMaximumSpeed = 3f;

        [Min(0f)]
        public float stopMinimumSpeed = 2.2f;

        [Range(60f, 175f)]
        public float hardTurnAngle = 112f;

        [Min(0f)]
        public float hardTurnMinimumSpeed = 4f;

        [Min(0.05f)]
        public float hardTurnCooldown = 0.45f;

        [Tooltip(
            "Uses the authored Turn L/R one-shot clips for very large direction changes. " +
            "Disabled by default because these clips rotate the visual character while gameplay " +
            "already rotates it, which can create a double-turn/backwards-running result.")]
        public bool enableAuthoredHardTurns = false;

        [Tooltip(
            "Forward sector used to choose Run Start Forward. Larger side angles use L/R, " +
            "and directions behind the character use Back L/R.")]
        [Range(10f, 80f)]
        public float forwardStartHalfAngle = 32f;

        [Range(70f, 160f)]
        public float backwardStartAngle = 112f;

        [Header("One-Shot Exit Timing")]
        [Range(0.1f, 1f)]
        public float runStartExitTime = 0.72f;

        [Range(0.1f, 1f)]
        public float runStopExitTime = 0.62f;

        [Range(0.1f, 1f)]
        public float runTurnExitTime = 0.72f;

        [Header("Transition Feel")]
        [Range(0f, 0.3f)]
        public float locomotionTransitionDuration = 0.06f;

        [Range(0f, 0.3f)]
        public float jumpTransitionDuration = 0.05f;

        [Range(0f, 0.3f)]
        public float fallTransitionDuration = 0.08f;

        [Range(0f, 0.3f)]
        public float doubleJumpTransitionDuration = 0f;

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

        // Former v19.1-v19.4 forward-only run slot. Kept hidden only for migration.
        [SerializeField, HideInInspector]
        private AnimationClip run;

        [SerializeField, HideInInspector]
        private int profileVersion = 199;

        private void OnEnable()
        {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults()
        {
            if (profileVersion < 193)
            {
                doubleJumpTransitionDuration = 0.04f;
                doubleJumpExitTime = 0.86f;
                profileVersion = 193;
            }

            if (profileVersion < 194)
            {
                if (doubleJumps == null)
                    doubleJumps = new List<AnimationClip>();

                if (doubleJump != null &&
                    !doubleJumps.Contains(doubleJump))
                {
                    doubleJumps.Add(doubleJump);
                }

                doubleJumpTransitionDuration = 0f;
                profileVersion = 194;
            }

            if (profileVersion < 196)
            {
                if (runForward == null &&
                    run != null)
                {
                    runForward = run;
                }

                doubleJumpMirrorChancePercent = 35f;

                turnLeanBlendExtension = 0.5f;
                turnRateForFullLean = 240f;

                moveInputDeadzone = 0.12f;
                startMaximumSpeed = 3f;
                stopMinimumSpeed = 2.2f;
                hardTurnAngle = 112f;
                hardTurnMinimumSpeed = 4f;
                hardTurnCooldown = 0.45f;
                forwardStartHalfAngle = 32f;
                backwardStartAngle = 112f;

                runStartExitTime = 0.72f;
                runStopExitTime = 0.62f;
                runTurnExitTime = 0.72f;
                locomotionTransitionDuration = 0.06f;

                profileVersion = 196;
            }

            if (profileVersion < 197)
            {
                // New v19.7 presentation-only defaults. Existing animation
                // assignments and all earlier tuning remain untouched.
                locomotionDirectionResponse = 8f;
                enableAuthoredHardTurns = false;

                profileVersion = 197;
            }

            if (profileVersion < 198)
            {
                // v19.8 only initializes the new idle-start alignment settings.
                alignFacingBeforeDirectionalBlend = true;
                maximumFacingErrorForAuthoredStart = 38f;
                startFacingAlignmentReleaseAngle = 20f;
                startFacingAlignmentMaxDuration = 0.32f;

                profileVersion = 198;
            }

            if (profileVersion < 199)
            {
                // Preserve the old single landing assignment as the default
                // standing/slow landing. The running slot stays optional.
                if (landIdle == null)
                    landIdle = land;

                runningLandingSpeedThreshold = 3.25f;
                profileVersion = 199;
            }
        }

        public int DoubleJumpVariantCount
        {
            get
            {
                if (doubleJumps == null)
                    return 0;

                int count = 0;

                for (int i = 0; i < doubleJumps.Count; i++)
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

            for (int i = 0; i < doubleJumps.Count; i++)
            {
                AnimationClip clip = doubleJumps[i];

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

            walkThreshold = Mathf.Clamp(walkThreshold, 0.05f, 0.8f);
            runThreshold = Mathf.Max(walkThreshold + 0.05f, runThreshold);

            doubleJumpMirrorChancePercent =
                Mathf.Clamp(doubleJumpMirrorChancePercent, 0f, 100f);

            turnLeanBlendExtension =
                Mathf.Clamp01(turnLeanBlendExtension);

            turnRateForFullLean =
                Mathf.Max(30f, turnRateForFullLean);

            locomotionDirectionResponse =
                Mathf.Max(
                    0.1f,
                    locomotionDirectionResponse);

            maximumFacingErrorForAuthoredStart =
                Mathf.Clamp(
                    maximumFacingErrorForAuthoredStart,
                    5f,
                    120f);

            startFacingAlignmentReleaseAngle =
                Mathf.Clamp(
                    startFacingAlignmentReleaseAngle,
                    1f,
                    90f);

            startFacingAlignmentMaxDuration =
                Mathf.Max(
                    0.05f,
                    startFacingAlignmentMaxDuration);

            moveInputDeadzone =
                Mathf.Clamp(moveInputDeadzone, 0.01f, 0.75f);

            startMaximumSpeed =
                Mathf.Max(0f, startMaximumSpeed);

            stopMinimumSpeed =
                Mathf.Max(0f, stopMinimumSpeed);

            hardTurnAngle =
                Mathf.Clamp(hardTurnAngle, 60f, 175f);

            hardTurnMinimumSpeed =
                Mathf.Max(0f, hardTurnMinimumSpeed);

            hardTurnCooldown =
                Mathf.Max(0.05f, hardTurnCooldown);

            forwardStartHalfAngle =
                Mathf.Clamp(forwardStartHalfAngle, 10f, 80f);

            backwardStartAngle =
                Mathf.Clamp(backwardStartAngle, 70f, 160f);

            locomotionTransitionDuration =
                Mathf.Max(0f, locomotionTransitionDuration);

            jumpTransitionDuration =
                Mathf.Max(0f, jumpTransitionDuration);

            fallTransitionDuration =
                Mathf.Max(0f, fallTransitionDuration);

            doubleJumpTransitionDuration =
                Mathf.Max(0f, doubleJumpTransitionDuration);

            runningLandingSpeedThreshold =
                Mathf.Max(
                    0f,
                    runningLandingSpeedThreshold);

            landTransitionDuration =
                Mathf.Max(0f, landTransitionDuration);

            slingTransitionDuration =
                Mathf.Max(0f, slingTransitionDuration);

            doubleJumpExitTime =
                Mathf.Clamp01(doubleJumpExitTime);

            landExitTime =
                Mathf.Clamp01(landExitTime);

            slingImpactExitTime =
                Mathf.Clamp01(slingImpactExitTime);

            runStartExitTime =
                Mathf.Clamp01(runStartExitTime);

            runStopExitTime =
                Mathf.Clamp01(runStopExitTime);

            runTurnExitTime =
                Mathf.Clamp01(runTurnExitTime);
        }
    }
}
