using System;
using UnityEngine;

namespace PortableAnimation
{
    // Fixed order shared by the source, controller and authoring tools.
    public enum AeroPose
    {
        Hover, Takeoff, FlyForward, FlyBackward, FlyLeft, FlyRight,
        Ascend, Descend, Boost, Brake, BankLeft, BankRight,
        TurnLeft, TurnRight, LandingApproach, Dive, Recover, Count
    }

    public enum AeroMotionState
    {
        Grounded, TakingOff, Hovering, Flying, Boosting, Braking,
        LandingApproach
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 12 Aero Motion",
        fileName = "AeroMotionProfile")]
    public sealed class AeroMotionProfile : ScriptableObject
    {
        [SerializeField] private GroundMotionProfile previousPipeline;
        [SerializeField] private AnimationClip[] clips = new AnimationClip[(int)AeroPose.Count];
        [Header("Flight thresholds")]
        [SerializeField, Min(0.01f)] private float movementThreshold = 0.2f;
        [SerializeField, Min(0.01f)] private float verticalThreshold = 0.5f;
        [SerializeField, Min(1f)] private float bankYawRate = 65f;
        [SerializeField, Min(1f)] private float turnYawRate = 95f;
        [SerializeField, Min(0.01f)] private float takeoffSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float boostSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float brakeSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float landingApproachSeconds = 0.35f;
        [Header("Blend and playback")]
        [SerializeField, Min(0.01f)] private float poseBlendRate = 6f;
        [SerializeField, Min(0.01f)] private float groundAirBlendRate = 2.7f;
        [SerializeField, Min(0.1f)] private float loopPlaybackRate = 1f;

        public GroundMotionProfile PreviousPipeline => previousPipeline;
        public float MovementThreshold => movementThreshold;
        public float VerticalThreshold => verticalThreshold;
        public float BankYawRate => bankYawRate;
        public float TurnYawRate => turnYawRate;
        public float TakeoffSeconds => takeoffSeconds;
        public float BoostSeconds => boostSeconds;
        public float BrakeSeconds => brakeSeconds;
        public float LandingApproachSeconds => landingApproachSeconds;
        public float PoseBlendRate => poseBlendRate;
        public float GroundAirBlendRate => groundAirBlendRate;
        public float LoopPlaybackRate => loopPlaybackRate;

        public AnimationClip Clip(AeroPose pose)
        {
            int index = (int)pose;
            return clips != null && index >= 0 && index < clips.Length ? clips[index] : null;
        }

        public void Configure(GroundMotionProfile previous, AnimationClip[] samples)
        {
            previousPipeline = previous;
            clips = samples == null ? null : (AnimationClip[])samples.Clone();
        }

        public void AssertValid()
        {
            if (previousPipeline == null)
                throw new InvalidOperationException("AeroMotion requires a Phase 11 GroundMotion profile.");
            previousPipeline.AssertValid();
            if (clips == null || clips.Length != (int)AeroPose.Count)
                throw new InvalidOperationException("AeroMotion requires exactly 17 clips.");
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] == null || clips[i].length <= 0f)
                    throw new InvalidOperationException("Missing/empty AeroMotion clip: " + (AeroPose)i);
            if (movementThreshold <= 0f || groundAirBlendRate <= 0f || poseBlendRate <= 0f)
                throw new InvalidOperationException("AeroMotion thresholds and blend rates must be positive.");
        }
    }

    /// <summary>
    /// Synthetic or gameplay-derived flight intent. FlightEnabled is the authoritative
    /// mode request: an ordinary jump does NOT automatically enter sustained flight.
    /// Grounded and velocity originate in the motor, never from animation output.
    /// </summary>
    public readonly struct AeroMotionFrame
    {
        public readonly GroundMotionFrame Ground;
        public readonly bool FlightEnabled;
        public readonly bool BoostRequested;
        public readonly float VerticalVelocity;
        public readonly float YawRate;

        public AeroMotionFrame(in GroundMotionFrame ground, bool flightEnabled,
            bool boostRequested = false)
        {
            Ground = ground;
            FlightEnabled = flightEnabled;
            BoostRequested = boostRequested;
            VerticalVelocity = ground.VerticalVelocity;
            YawRate = ground.YawRate;
        }
    }
}
