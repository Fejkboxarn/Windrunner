using System;
using UnityEngine;

namespace PortableAnimation
{
    // Fixed semantic indices: profiles, controller and clip mixer share this order.
    public enum GroundPose
    {
        Idle, StartForward, StartBackward, StartLeft, StartRight,
        MoveForward, MoveBackward, MoveLeft, MoveRight,
        Stop, PivotLeft, PivotRight, JumpStart, Rise, Fall, Land,
        TurnLeft, TurnRight, Count
    }

    public enum GroundMotionState
    {
        Idle, Starting, Moving, Stopping, Pivoting,
        JumpStarting, Rising, Falling, Landing, Turning
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 11 Ground Motion",
        fileName = "GroundMotionProfile")]
    public sealed class GroundMotionProfile : ScriptableObject
    {
        [SerializeField] private WeaponPoseProfile previousPipeline;
        [SerializeField] private AnimationClip[] clips =
            new AnimationClip[(int)GroundPose.Count];

        [Header("Grounded locomotion")]
        [SerializeField, Min(0.01f)] private float moveThreshold = 0.16f;
        [SerializeField, Min(0.001f)] private float stopThreshold = 0.10f;
        [SerializeField, Min(0.01f)] private float startSeconds = 0.22f;
        [SerializeField, Min(0.01f)] private float stopSeconds = 0.26f;
        [SerializeField, Min(0.01f)] private float pivotSeconds = 0.30f;
        [SerializeField, Min(0.01f)] private float turnSeconds = 0.32f;
        [Tooltip("Optional turn-in-place / yaw-driven turn animations. Disabled by default. " +
            "Directional movement blending and reversal pivots still work when this is off.")]
        [SerializeField] private bool enableTurning;
        [SerializeField, Range(30f, 180f)] private float reversalDegrees = 125f;
        [SerializeField, Min(1f)] private float turnRateThreshold = 100f;
        [SerializeField, Min(0f)] private float pivotCooldown = 0.32f;

        [Header("Air and landing")]
        [SerializeField, Min(0.01f)] private float jumpStartSeconds = 0.15f;
        [SerializeField, Min(0.01f)] private float landSeconds = 0.28f;
        [SerializeField] private float riseThreshold = 0.15f;

        [Header("Pose smoothing")]
        [SerializeField, Min(0.01f)] private float blendSpeed = 7f;
        [SerializeField, Min(0.1f)] private float movementPlaybackRate = 1f;

        [Header("Batch 16.1: real-character clip synchronization")]
        [Tooltip("Use each authored one-shot clip duration instead of fixed testbed timing.")]
        [SerializeField] private bool useAuthoredOneShotDurations;
        [SerializeField, Min(0.1f)] private float oneShotPlaybackSpeed = 1f;
        [Tooltip("Left/right footstep phase calibration for Fwd, Bwd, Left, Right (0..1).")]
        [SerializeField] private Vector4 movementPhaseOffsets;

        [Header("Batch 16.1.2: true Idle / Walk / Run")]
        [SerializeField] private AnimationClip walkForward;
        [SerializeField] private AnimationClip walkBackward;
        [SerializeField] private AnimationClip walkLeft;
        [SerializeField] private AnimationClip walkRight;

        [Header("8-way directional blend")]
        [Tooltip("Optional dedicated run loops. If a diagonal is empty, runtime blends its two neighboring cardinal clips instead.")]
        [SerializeField] private AnimationClip runForwardRight;
        [SerializeField] private AnimationClip runBackwardRight;
        [SerializeField] private AnimationClip runBackwardLeft;
        [SerializeField] private AnimationClip runForwardLeft;
        [Tooltip("Optional dedicated walk loops. If a diagonal is empty, runtime blends its two neighboring cardinal clips instead.")]
        [SerializeField] private AnimationClip walkForwardRight;
        [SerializeField] private AnimationClip walkBackwardRight;
        [SerializeField] private AnimationClip walkBackwardLeft;
        [SerializeField] private AnimationClip walkForwardLeft;

        [Header("Directional mirroring")]
        [Tooltip("Per-direction left/right Humanoid mirror flags for the Run blend space. " +
            "The dashboard creates character-local mirrored clip assets so Playables can sample them safely.")]
        [SerializeField, HideInInspector] private bool[] runMirror = new bool[8];
        [Tooltip("Per-direction left/right Humanoid mirror flags for the Walk blend space.")]
        [SerializeField, HideInInspector] private bool[] walkMirror = new bool[8];
        [SerializeField, HideInInspector] private AnimationClip[] runMirrorSources = new AnimationClip[8];
        [SerializeField, HideInInspector] private AnimationClip[] walkMirrorSources = new AnimationClip[8];
        [SerializeField, HideInInspector] private AnimationClip[] runMirroredClips = new AnimationClip[8];
        [SerializeField, HideInInspector] private AnimationClip[] walkMirroredClips = new AnimationClip[8];

        [SerializeField] private AnimationClip stopBackward;
        [SerializeField] private AnimationClip stopLeft;
        [SerializeField] private AnimationClip stopRight;
        [SerializeField, Range(0.05f, 0.95f)] private float idleToWalkEnd = 0.18f;
        [SerializeField, Range(0.1f, 0.99f)] private float walkToRunStart = 0.48f;
        [SerializeField, Range(0.2f, 1f)] private float fullRunSpeed = 0.88f;
        [SerializeField, Range(0f, 1f)] private float minimumStartSpeed = 0.68f;
        [SerializeField, Range(0f, 1f)] private float minimumStopSpeed = 0.68f;
        [SerializeField, Range(0f, 1f)] private float entryGaitPhase = 0.5f;

        public bool HasWalkClips => walkForward != null && walkBackward != null &&
            walkLeft != null && walkRight != null;
        public float IdleToWalkEnd => idleToWalkEnd;
        public float WalkToRunStart => walkToRunStart;
        public float FullRunSpeed => fullRunSpeed;
        public float MinimumStartSpeed => minimumStartSpeed;
        public float MinimumStopSpeed => minimumStopSpeed;
        public float EntryGaitPhase => entryGaitPhase;

        public AnimationClip WalkClip(int direction)
        {
            switch (direction)
            {
                case 0: return walkForward != null ? walkForward : Clip(GroundPose.MoveForward);
                case 1: return walkBackward != null ? walkBackward : Clip(GroundPose.MoveBackward);
                case 2: return walkLeft != null ? walkLeft : Clip(GroundPose.MoveLeft);
                default: return walkRight != null ? walkRight : Clip(GroundPose.MoveRight);
            }
        }

        // Clockwise 8-way order used by the runtime/dashboard:
        // 0 F, 1 FR, 2 R, 3 BR, 4 B, 5 BL, 6 L, 7 FL.
        public AnimationClip RunDirectionalSourceClip(int direction)
        {
            switch ((direction % 8 + 8) % 8)
            {
                case 0: return Clip(GroundPose.MoveForward);
                case 1: return runForwardRight;
                case 2: return Clip(GroundPose.MoveRight);
                case 3: return runBackwardRight;
                case 4: return Clip(GroundPose.MoveBackward);
                case 5: return runBackwardLeft;
                case 6: return Clip(GroundPose.MoveLeft);
                default: return runForwardLeft;
            }
        }

        public AnimationClip WalkDirectionalSourceClip(int direction)
        {
            switch ((direction % 8 + 8) % 8)
            {
                case 0: return walkForward != null ? walkForward : Clip(GroundPose.MoveForward);
                case 1: return walkForwardRight;
                case 2: return walkRight != null ? walkRight : Clip(GroundPose.MoveRight);
                case 3: return walkBackwardRight;
                case 4: return walkBackward != null ? walkBackward : Clip(GroundPose.MoveBackward);
                case 5: return walkBackwardLeft;
                case 6: return walkLeft != null ? walkLeft : Clip(GroundPose.MoveLeft);
                default: return walkForwardLeft;
            }
        }

        public bool DirectionalMirror(bool walk, int direction)
        {
            direction = (direction % 8 + 8) % 8;
            bool[] flags = walk ? walkMirror : runMirror;
            return flags != null && flags.Length == 8 && flags[direction];
        }

        public AnimationClip RunDirectionalClip(int direction)
        {
            return EffectiveDirectionalClip(false, direction);
        }

        public AnimationClip WalkDirectionalClip(int direction)
        {
            return EffectiveDirectionalClip(true, direction);
        }

        public bool HasUsableDirectionalClip(bool walk, int direction)
        {
            AnimationClip source = walk
                ? WalkDirectionalSourceClip(direction)
                : RunDirectionalSourceClip(direction);
            if (source == null || source.length <= 0f)
                return false;

            if (!DirectionalMirror(walk, direction))
                return true;

            direction = (direction % 8 + 8) % 8;
            AnimationClip[] sources = walk ? walkMirrorSources : runMirrorSources;
            AnimationClip[] mirrored = walk ? walkMirroredClips : runMirroredClips;
            return sources != null && sources.Length == 8 &&
                mirrored != null && mirrored.Length == 8 &&
                sources[direction] == source &&
                mirrored[direction] != null &&
                mirrored[direction].length > 0f;
        }

        public bool HasDedicatedRunDirection(int direction)
        {
            AnimationClip clip = RunDirectionalSourceClip(direction);
            return clip != null && clip.length > 0f;
        }

        public bool HasDedicatedWalkDirection(int direction)
        {
            AnimationClip clip = WalkDirectionalSourceClip(direction);
            return clip != null && clip.length > 0f;
        }

        public void SetDirectionalMirrorCache(
            bool walk,
            int direction,
            AnimationClip source,
            AnimationClip mirrored)
        {
            direction = (direction % 8 + 8) % 8;
            EnsureDirectionalMirrorArrays();

            AnimationClip[] sources = walk ? walkMirrorSources : runMirrorSources;
            AnimationClip[] mirrors = walk ? walkMirroredClips : runMirroredClips;
            sources[direction] = source;
            mirrors[direction] = mirrored;
        }

        private AnimationClip EffectiveDirectionalClip(bool walk, int direction)
        {
            direction = (direction % 8 + 8) % 8;
            AnimationClip source = walk
                ? WalkDirectionalSourceClip(direction)
                : RunDirectionalSourceClip(direction);
            if (source == null || !DirectionalMirror(walk, direction))
                return source;

            AnimationClip[] sources = walk ? walkMirrorSources : runMirrorSources;
            AnimationClip[] mirrors = walk ? walkMirroredClips : runMirroredClips;
            if (sources != null && sources.Length == 8 &&
                mirrors != null && mirrors.Length == 8 &&
                sources[direction] == source &&
                mirrors[direction] != null)
                return mirrors[direction];

            return null;
        }

        private void EnsureDirectionalMirrorArrays()
        {
            if (runMirror == null || runMirror.Length != 8) runMirror = new bool[8];
            if (walkMirror == null || walkMirror.Length != 8) walkMirror = new bool[8];
            if (runMirrorSources == null || runMirrorSources.Length != 8)
                runMirrorSources = new AnimationClip[8];
            if (walkMirrorSources == null || walkMirrorSources.Length != 8)
                walkMirrorSources = new AnimationClip[8];
            if (runMirroredClips == null || runMirroredClips.Length != 8)
                runMirroredClips = new AnimationClip[8];
            if (walkMirroredClips == null || walkMirroredClips.Length != 8)
                walkMirroredClips = new AnimationClip[8];
        }

        public void ConfigureDirectionalDiagonals(AnimationClip[] runDiagonals,
            AnimationClip[] walkDiagonals)
        {
            if (runDiagonals != null && runDiagonals.Length != 4)
                throw new ArgumentException("Need four run diagonal clips (FR, BR, BL, FL).");
            if (walkDiagonals != null && walkDiagonals.Length != 4)
                throw new ArgumentException("Need four walk diagonal clips (FR, BR, BL, FL).");

            runForwardRight = runDiagonals != null ? runDiagonals[0] : null;
            runBackwardRight = runDiagonals != null ? runDiagonals[1] : null;
            runBackwardLeft = runDiagonals != null ? runDiagonals[2] : null;
            runForwardLeft = runDiagonals != null ? runDiagonals[3] : null;

            walkForwardRight = walkDiagonals != null ? walkDiagonals[0] : null;
            walkBackwardRight = walkDiagonals != null ? walkDiagonals[1] : null;
            walkBackwardLeft = walkDiagonals != null ? walkDiagonals[2] : null;
            walkForwardLeft = walkDiagonals != null ? walkDiagonals[3] : null;

            EnsureDirectionalMirrorArrays();
            int[] diagonalDirections = { 1, 3, 5, 7 };
            foreach (int direction in diagonalDirections)
            {
                runMirror[direction] = false;
                walkMirror[direction] = false;
                runMirrorSources[direction] = null;
                walkMirrorSources[direction] = null;
                runMirroredClips[direction] = null;
                walkMirroredClips[direction] = null;
            }
        }

        public AnimationClip StopClip(int direction)
        {
            switch (direction)
            {
                case 1: return stopBackward != null ? stopBackward : Clip(GroundPose.Stop);
                case 2: return stopLeft != null ? stopLeft : Clip(GroundPose.Stop);
                case 3: return stopRight != null ? stopRight : Clip(GroundPose.Stop);
                default: return Clip(GroundPose.Stop);
            }
        }
        public void ConfigureRealClips(AnimationClip[] walks, AnimationClip[] stops)
        {
            if (walks == null || walks.Length != 4) throw new ArgumentException("Need four walk clips.");
            if (stops == null || stops.Length != 4) throw new ArgumentException("Need four stop clips.");
            walkForward = walks[0]; walkBackward = walks[1];
            walkLeft = walks[2]; walkRight = walks[3];
            stopBackward = stops[1]; stopLeft = stops[2]; stopRight = stops[3];

            EnsureDirectionalMirrorArrays();
            int[] cardinalDirections = { 0, 2, 4, 6 };
            foreach (int direction in cardinalDirections)
            {
                walkMirror[direction] = false;
                walkMirrorSources[direction] = null;
                walkMirroredClips[direction] = null;
            }

            useAuthoredOneShotDurations = true;
            // Permit actual low-speed walking in the analog speed test.
            moveThreshold = 0.035f;
            stopThreshold = 0.018f;
        }
        public void EnableAuthoredTiming(bool enabled) { useAuthoredOneShotDurations = enabled; }
        public float StopDuration(int direction)
        {
            AnimationClip clip = StopClip(direction);
            return useAuthoredOneShotDurations && clip != null
                ? Mathf.Max(0.01f, clip.length / Mathf.Max(0.1f, oneShotPlaybackSpeed))
                : stopSeconds;
        }
        public WeaponPoseProfile PreviousPipeline => previousPipeline;
        public float MoveThreshold => moveThreshold;
        public float StopThreshold => stopThreshold;
        public float StartSeconds => startSeconds;
        public float StopSeconds => stopSeconds;
        public float PivotSeconds => pivotSeconds;
        public float TurnSeconds => turnSeconds;
        public bool EnableTurning => enableTurning;
        public float ReversalDegrees => reversalDegrees;
        public float TurnRateThreshold => turnRateThreshold;
        public float PivotCooldown => pivotCooldown;
        public float JumpStartSeconds => jumpStartSeconds;
        public float LandSeconds => landSeconds;
        public float RiseThreshold => riseThreshold;
        public float BlendSpeed => blendSpeed;
        public float MovementPlaybackRate => movementPlaybackRate;
        public bool UseAuthoredOneShotDurations => useAuthoredOneShotDurations;
        public Vector4 MovementPhaseOffsets => movementPhaseOffsets;
        public float DurationFor(GroundPose pose, float fallback)
        {
            AnimationClip animation = Clip(pose);
            if (!useAuthoredOneShotDurations || animation == null || animation.length <= 0f)
                return fallback;
            return Mathf.Max(0.01f, animation.length / Mathf.Max(0.1f, oneShotPlaybackSpeed));
        }

        public AnimationClip Clip(GroundPose pose)
        {
            int index = (int)pose;
            return clips != null && index >= 0 && index < clips.Length
                ? clips[index] : null;
        }

        public void Configure(WeaponPoseProfile previous, AnimationClip[] samples)
        {
            previousPipeline = previous;
            clips = samples == null ? null : (AnimationClip[])samples.Clone();
        }

        public void AssertValid()
        {
            if (previousPipeline == null)
                throw new InvalidOperationException(
                    "Phase 11 requires the validated Phase 10 weapon profile.");
            previousPipeline.AssertValid();
            if (clips == null || clips.Length != (int)GroundPose.Count)
                throw new InvalidOperationException("GroundMotion needs exactly 18 clips.");
            for (int i = 0; i < clips.Length; i++)
            {
                GroundPose pose = (GroundPose)i;
                if (!enableTurning &&
                    (pose == GroundPose.TurnLeft || pose == GroundPose.TurnRight))
                    continue;

                // Movement loops are validated as a directional blend space below.
                // Individual cardinal samples may be intentionally absent when
                // neighboring diagonals (or mirrored samples) cover that direction.
                if (pose >= GroundPose.MoveForward && pose <= GroundPose.MoveRight)
                    continue;

                if (clips[i] == null || clips[i].length <= 0f)
                    throw new InvalidOperationException("Missing or empty GroundMotion clip: " +
                        pose);
            }

            int usableRunDirections = 0;
            int usableWalkDirections = 0;
            for (int direction = 0; direction < 8; direction++)
            {
                AnimationClip runSource = RunDirectionalSourceClip(direction);
                if (runSource != null && DirectionalMirror(false, direction) &&
                    !HasUsableDirectionalClip(false, direction))
                    throw new InvalidOperationException(
                        "Run direction " + direction +
                        " is marked Mirror but its generated mirrored clip is missing/stale. " +
                        "Open Animation Dashboard to rebuild mirrors.");

                AnimationClip walkSource = WalkDirectionalSourceClip(direction);
                if (walkSource != null && DirectionalMirror(true, direction) &&
                    !HasUsableDirectionalClip(true, direction))
                    throw new InvalidOperationException(
                        "Walk direction " + direction +
                        " is marked Mirror but its generated mirrored clip is missing/stale. " +
                        "Open Animation Dashboard to rebuild mirrors.");

                if (HasUsableDirectionalClip(false, direction))
                    usableRunDirections++;
                if (HasUsableDirectionalClip(true, direction))
                    usableWalkDirections++;
            }

            if (usableRunDirections < 2)
                throw new InvalidOperationException(
                    "GroundMotion needs at least two usable Run directional samples.");
            if (usableWalkDirections < 2)
                throw new InvalidOperationException(
                    "GroundMotion needs at least two usable Walk directional samples.");

            if (!(idleToWalkEnd < walkToRunStart &&
                walkToRunStart < fullRunSpeed))
                throw new InvalidOperationException("Idle/Walk/Run thresholds must increase.");
            if (stopThreshold >= moveThreshold)
                throw new InvalidOperationException(
                    "GroundMotion StopThreshold must be below MoveThreshold.");
        }
    }

    public readonly struct GroundMotionFrame
    {
        public readonly AnimationFrameState Locomotion;
        public readonly bool Grounded;
        public readonly bool JumpRequested;
        public readonly float VerticalVelocity;
        // Degrees per second about the character's up axis, positive to the right.
        public readonly float YawRate;

        public GroundMotionFrame(AnimationFrameState locomotion, bool grounded,
            float verticalVelocity, float yawRate, bool jumpRequested = false)
        {
            Locomotion = locomotion;
            Grounded = grounded;
            JumpRequested = jumpRequested;
            VerticalVelocity = verticalVelocity;
            YawRate = yawRate;
        }
    }
}
