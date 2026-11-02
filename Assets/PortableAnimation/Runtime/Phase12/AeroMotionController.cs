using System;
using UnityEngine;

namespace PortableAnimation
{
    public readonly struct AeroMotionSignal
    {
        public readonly AeroMotionState Previous;
        public readonly AeroMotionState Current;
        public readonly AeroPose Pose;
        public AeroMotionSignal(AeroMotionState previous, AeroMotionState current, AeroPose pose)
        { Previous = previous; Current = current; Pose = pose; }
        public override string ToString() => Previous + " -> " + Current + " / " + Pose;
    }

    /// <summary>
    /// Pure state controller. Never moves the Transform, owns no Playables and
    /// has no MonoBehaviour lifecycle. Supports mid-flight direction changes,
    /// takeoff, braking, bank/turn, boost, controlled landing, and crossfade.
    /// </summary>
    public sealed class AeroMotionController
    {
        private readonly AeroMotionProfile profile;
        private readonly float[] weights = new float[(int)AeroPose.Count];
        private readonly float[] targets = new float[(int)AeroPose.Count];
        private float stateTime;
        private float airBlend;
        private float speed;
        private Vector2 direction = Vector2.up;

        public event Action<AeroMotionSignal> StateChanged;
        public AeroMotionState State { get; private set; } = AeroMotionState.Grounded;
        public AeroPose PrimaryPose { get; private set; } = AeroPose.Hover;
        public float StateTime => stateTime;
        public float AirBlend => airBlend;
        public float Speed => speed;
        public float Weight(AeroPose pose) => weights[(int)pose];

        public AeroMotionController(AeroMotionProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            weights[(int)AeroPose.Hover] = 1f;
        }

        public void Advance(in AeroMotionFrame frame)
        {
            float dt = Mathf.Max(0f, frame.Ground.Locomotion.DeltaTime);
            speed = Mathf.Clamp01(frame.Ground.Locomotion.NormalizedSpeed);
            Vector3 localVelocity = frame.Ground.Locomotion.LocalVelocity;
            Vector2 horizontal = new Vector2(localVelocity.x, localVelocity.z);
            if (horizontal.sqrMagnitude > 0.0001f)
                direction = horizontal.normalized;

            stateTime += dt;
            bool flight = frame.FlightEnabled;
            bool grounded = frame.Ground.Grounded;

            if (State == AeroMotionState.Grounded && flight)
                Enter(AeroMotionState.TakingOff, AeroPose.Takeoff);

            switch (State)
            {
                case AeroMotionState.Grounded:
                    break;
                case AeroMotionState.TakingOff:
                    if (!flight)
                        Enter(grounded ? AeroMotionState.Grounded : AeroMotionState.LandingApproach,
                            grounded ? AeroPose.Hover : AeroPose.LandingApproach);
                    else if (stateTime >= profile.TakeoffSeconds)
                        Enter(speed >= profile.MovementThreshold ? AeroMotionState.Flying :
                            AeroMotionState.Hovering, AeroPose.Hover);
                    break;
                case AeroMotionState.Hovering:
                case AeroMotionState.Flying:
                    if (!flight)
                        Enter(grounded ? AeroMotionState.Grounded : AeroMotionState.LandingApproach,
                            grounded ? AeroPose.Hover : AeroPose.LandingApproach);
                    else if (frame.BoostRequested && speed >= profile.MovementThreshold)
                        Enter(AeroMotionState.Boosting, AeroPose.Boost);
                    // Horizontal stopping should only trigger Braking when
                    // there is no meaningful vertical motion. A character
                    // ascending or descending at zero horizontal speed must
                    // remain in continuous flight so SelectContinuousPose
                    // can choose the correct Ascend/Descend animation.
                    else if (State == AeroMotionState.Flying &&
                        speed < profile.MovementThreshold * 0.55f &&
                        Mathf.Abs(frame.VerticalVelocity) <= profile.VerticalThreshold)
                        Enter(AeroMotionState.Braking, AeroPose.Brake);
                    else if (State == AeroMotionState.Hovering && speed >= profile.MovementThreshold)
                        Enter(AeroMotionState.Flying, AeroPose.FlyForward);
                    else
                        PrimaryPose = SelectContinuousPose(frame, speed, direction);
                    break;
                case AeroMotionState.Boosting:
                    if (!flight)
                        Enter(grounded ? AeroMotionState.Grounded : AeroMotionState.LandingApproach,
                            grounded ? AeroPose.Hover : AeroPose.LandingApproach);
                    else if (stateTime >= profile.BoostSeconds)
                        Enter(speed >= profile.MovementThreshold ? AeroMotionState.Flying :
                            AeroMotionState.Hovering, AeroPose.Hover);
                    break;
                case AeroMotionState.Braking:
                    if (!flight)
                        Enter(grounded ? AeroMotionState.Grounded : AeroMotionState.LandingApproach,
                            grounded ? AeroPose.Hover : AeroPose.LandingApproach);
                    else if (speed >= profile.MovementThreshold)
                        Enter(AeroMotionState.Flying, AeroPose.FlyForward);
                    else if (stateTime >= profile.BrakeSeconds)
                        Enter(AeroMotionState.Hovering, AeroPose.Hover);
                    break;
                case AeroMotionState.LandingApproach:
                    if (flight)
                        Enter(AeroMotionState.TakingOff, AeroPose.Takeoff);
                    else if (grounded)
                        Enter(AeroMotionState.Grounded, AeroPose.Hover);
                    break;
            }

            float targetBlend = State == AeroMotionState.Grounded ? 0f : 1f;
            airBlend = Mathf.MoveTowards(airBlend, targetBlend,
                dt * profile.GroundAirBlendRate);
            BuildTargets(frame);
            Smooth(dt);
        }

        private AeroPose SelectContinuousPose(in AeroMotionFrame frame,
            float speed, Vector2 heading)
        {
            if (frame.VerticalVelocity > profile.VerticalThreshold)
                return AeroPose.Ascend;
            if (frame.VerticalVelocity < -profile.VerticalThreshold)
                return AeroPose.Descend;
            if (Mathf.Abs(frame.YawRate) >= profile.TurnYawRate &&
                speed < profile.MovementThreshold)
                return frame.YawRate < 0 ? AeroPose.TurnLeft : AeroPose.TurnRight;
            if (Mathf.Abs(frame.YawRate) >= profile.BankYawRate &&
                speed >= profile.MovementThreshold)
                return frame.YawRate < 0 ? AeroPose.BankLeft : AeroPose.BankRight;
            if (speed < profile.MovementThreshold)
                return AeroPose.Hover;
            return Mathf.Abs(heading.x) > Mathf.Abs(heading.y)
                ? (heading.x < 0 ? AeroPose.FlyLeft : AeroPose.FlyRight)
                : (heading.y < 0 ? AeroPose.FlyBackward : AeroPose.FlyForward);
        }

        private void BuildTargets(in AeroMotionFrame frame)
        {
            Array.Clear(targets, 0, targets.Length);
            if (State == AeroMotionState.Flying &&
                Mathf.Abs(frame.VerticalVelocity) < profile.VerticalThreshold &&
                Mathf.Abs(frame.YawRate) < profile.BankYawRate)
            {
                // Four-cardinal horizontal flight blend, with speed fading
                // into Hover. This preserves diagonal movement continuously.
                float f = Mathf.Max(0f, direction.y);
                float b = Mathf.Max(0f, -direction.y);
                float l = Mathf.Max(0f, -direction.x);
                float r = Mathf.Max(0f, direction.x);
                float sum = Mathf.Max(0.00001f, f + b + l + r);
                float alpha = Mathf.Clamp01(speed);
                targets[(int)AeroPose.Hover] = 1f - alpha;
                targets[(int)AeroPose.FlyForward] = f * alpha / sum;
                targets[(int)AeroPose.FlyBackward] = b * alpha / sum;
                targets[(int)AeroPose.FlyLeft] = l * alpha / sum;
                targets[(int)AeroPose.FlyRight] = r * alpha / sum;
                PrimaryPose = SelectContinuousPose(frame, speed, direction);
            }
            else
            {
                if (State == AeroMotionState.Flying || State == AeroMotionState.Hovering)
                    PrimaryPose = SelectContinuousPose(frame, speed, direction);
                targets[(int)PrimaryPose] = 1f;
            }
        }

        private void Smooth(float dt)
        {
            float step = dt * profile.PoseBlendRate;
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], targets[i], step);
                sum += weights[i];
            }
            if (sum < 0.00001f)
            {
                weights[(int)AeroPose.Hover] = 1f;
                sum = 1f;
            }
            for (int i = 0; i < weights.Length; i++)
                weights[i] /= sum;
        }

        private void Enter(AeroMotionState next, AeroPose pose)
        {
            if (State == next && PrimaryPose == pose) return;
            AeroMotionState previous = State;
            State = next;
            PrimaryPose = pose;
            stateTime = 0f;
            StateChanged?.Invoke(new AeroMotionSignal(previous, next, pose));
        }
    }
}
