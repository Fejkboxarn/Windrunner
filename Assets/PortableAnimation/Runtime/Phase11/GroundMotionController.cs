using System;
using UnityEngine;

namespace PortableAnimation
{
    public readonly struct GroundMotionSignal
    {
        public readonly GroundMotionState Previous;
        public readonly GroundMotionState Current;
        public readonly GroundPose Pose;
        public GroundMotionSignal(GroundMotionState previous,
            GroundMotionState current, GroundPose pose)
        { Previous = previous; Current = current; Pose = pose; }
        public override string ToString() => Previous + " -> " + Current + " / " + Pose;
    }

    /// <summary>
    /// Deterministic pure C# locomotion state and weights, no Animator or Playables.
    /// It consumes movement data; it NEVER moves or rotates the character.
    /// Hysteresis, cooldown and timed one-shots are independent of animation content.
    /// </summary>
    public sealed class GroundMotionController
    {
        private readonly GroundMotionProfile profile;
        private readonly float[] weights = new float[(int)GroundPose.Count];
        private readonly float[] desired = new float[(int)GroundPose.Count];
        private Vector2 lastHeading = Vector2.up;
        private Vector2 direction = Vector2.up;
        private bool wasGrounded = true;
        private float elapsed;
        private float pivotCooldown;
        private bool firstTick = true;
        private float lastMovingSpeed;
        public int StopDirection { get; private set; }
        public int CurrentMoveDirection => DirectionIndex(lastHeading);
        public bool IsRunningOneShot => State == GroundMotionState.Starting ||
            State == GroundMotionState.Stopping || State == GroundMotionState.JumpStarting ||
            State == GroundMotionState.Landing || State == GroundMotionState.Pivoting;
        private GroundPose oneShot = GroundPose.Idle;

        public event Action<GroundMotionSignal> StateChanged;
        public GroundMotionState State { get; private set; } = GroundMotionState.Idle;
        public GroundPose PrimaryPose { get; private set; } = GroundPose.Idle;
        public float StateTime => elapsed;
        public float Speed { get; private set; }
        public Vector2 LocalDirection => direction;
        public float Weight(GroundPose pose) => weights[(int)pose];
        public float TargetWeight(GroundPose pose) => desired[(int)pose];
        public static void CalculateGaitWeights(float speed, float idleToWalk,
            float runStart, float fullRun, out float idle, out float walk, out float run)
        {
            float v = Mathf.Clamp01(speed);
            float move = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0f, Mathf.Max(0.001f, idleToWalk), v));
            float runShare = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(runStart, fullRun, v));
            idle = 1f - move;
            walk = move * (1f - runShare);
            run = move * runShare;
        }

        public GroundMotionController(GroundMotionProfile settings)
        {
            profile = settings ?? throw new ArgumentNullException(nameof(settings));
            profile.AssertValid();
            weights[(int)GroundPose.Idle] = 1f;
            desired[(int)GroundPose.Idle] = 1f;
        }

        public void Advance(in GroundMotionFrame frame)
        {
            float dt = frame.Locomotion.DeltaTime;
            float speed = Mathf.Clamp01(frame.Locomotion.NormalizedSpeed);
            Vector2 input = new Vector2(frame.Locomotion.LocalVelocity.x,
                frame.Locomotion.LocalVelocity.z);
            if (input.sqrMagnitude > 0.0001f) direction = input.normalized;
            Speed = speed;
            elapsed += dt;
            pivotCooldown = Mathf.Max(0f, pivotCooldown - dt);

            if (firstTick)
            {
                firstTick = false;
                wasGrounded = frame.Grounded;
                if (!frame.Grounded)
                    Enter(frame.VerticalVelocity > profile.RiseThreshold
                        ? GroundMotionState.Rising : GroundMotionState.Falling,
                        frame.VerticalVelocity > profile.RiseThreshold
                            ? GroundPose.Rise : GroundPose.Fall);
            }

            // A gameplay motor may mark itself airborne on the SAME FRAME it
            // issues JumpRequested. Consume that edge before airborne branching.
            if (frame.JumpRequested && (frame.Grounded || wasGrounded) &&
                State != GroundMotionState.JumpStarting &&
                State != GroundMotionState.Landing)
                Enter(GroundMotionState.JumpStarting, GroundPose.JumpStart);

            if (!frame.Grounded)
            {
                if (State == GroundMotionState.JumpStarting &&
                    elapsed < DurationOf(GroundPose.JumpStart))
                {
                    // Hold jump anticipation for its authored duration.
                }
                else
                {
                    var next = frame.VerticalVelocity > profile.RiseThreshold
                        ? GroundMotionState.Rising : GroundMotionState.Falling;
                    GroundPose pose = next == GroundMotionState.Rising
                        ? GroundPose.Rise : GroundPose.Fall;
                    if (State != next) Enter(next, pose);
                }
            }
            else if (!wasGrounded || State == GroundMotionState.Rising ||
                     State == GroundMotionState.Falling)
            {
                Enter(GroundMotionState.Landing, GroundPose.Land);
            }
            else
            {
                AdvanceGrounded(frame, speed, input);
            }

            wasGrounded = frame.Grounded;
            BuildTargets(speed);
            Smooth(dt);
        }

        private void AdvanceGrounded(in GroundMotionFrame frame,
            float speed, Vector2 input)
        {
            bool moving = speed >= profile.MoveThreshold;
            bool stopped = speed <= profile.StopThreshold;
            switch (State)
            {
                case GroundMotionState.JumpStarting:
                    if (elapsed >= DurationOf(GroundPose.JumpStart))
                        Enter(moving ? GroundMotionState.Moving : GroundMotionState.Idle,
                            moving ? PickMove(direction) : GroundPose.Idle);
                    break;
                case GroundMotionState.Landing:
                    if (elapsed >= DurationOf(GroundPose.Land))
                        Enter(moving ? GroundMotionState.Starting : GroundMotionState.Idle,
                            moving ? PickStart(direction) : GroundPose.Idle);
                    break;
                case GroundMotionState.Starting:
                    if (stopped) BeginStop();
                    else if (elapsed >= DurationOf(oneShot))
                    {
                        Enter(GroundMotionState.Moving, PickMove(direction));
                        lastHeading = direction;
                    }
                    break;
                case GroundMotionState.Stopping:
                    if (moving) Enter(speed >= profile.MinimumStartSpeed
                        ? GroundMotionState.Starting : GroundMotionState.Moving,
                        speed >= profile.MinimumStartSpeed
                            ? PickStart(direction) : PickMove(direction));
                    else if (elapsed >= DurationOf(GroundPose.Stop))
                        Enter(GroundMotionState.Idle, GroundPose.Idle);
                    break;
                case GroundMotionState.Pivoting:
                    if (elapsed >= DurationOf(oneShot))
                    {
                        Enter(moving ? GroundMotionState.Moving : GroundMotionState.Idle,
                            moving ? PickMove(direction) : GroundPose.Idle);
                        lastHeading = direction;
                    }
                    break;
                case GroundMotionState.Turning:
                    if (!profile.EnableTurning)
                        Enter(moving ? GroundMotionState.Moving : GroundMotionState.Idle,
                            moving ? PickMove(direction) : GroundPose.Idle);
                    else if (moving)
                        Enter(GroundMotionState.Starting, PickStart(direction));
                    else if (elapsed >= DurationOf(oneShot))
                        Enter(GroundMotionState.Idle, GroundPose.Idle);
                    break;
                case GroundMotionState.Idle:
                    if (moving)
                    {
                        lastHeading = direction;
                        if (speed >= profile.MinimumStartSpeed)
                            Enter(GroundMotionState.Starting, PickStart(direction));
                        else
                            Enter(GroundMotionState.Moving, PickMove(direction));
                    }
                    else if (profile.EnableTurning &&
                             Mathf.Abs(frame.YawRate) >= profile.TurnRateThreshold)
                        Enter(GroundMotionState.Turning,
                            frame.YawRate > 0f ? GroundPose.TurnRight : GroundPose.TurnLeft);
                    break;
                case GroundMotionState.Moving:
                    if (stopped) BeginStop();
                    else
                    {
                        lastMovingSpeed = speed;
                        bool reverse = input.sqrMagnitude > 0.0001f &&
                            Vector2.Angle(lastHeading, direction) >=
                                profile.ReversalDegrees;
                        bool yawPivot = profile.EnableTurning &&
                            Mathf.Abs(frame.YawRate) >=
                                profile.TurnRateThreshold * 1.5f;
                        if (pivotCooldown <= 0f && (reverse || yawPivot))
                        {
                            float cross = lastHeading.x * direction.y -
                                lastHeading.y * direction.x;
                            GroundPose side = reverse
                                ? (cross >= 0f ? GroundPose.PivotLeft : GroundPose.PivotRight)
                                : (frame.YawRate > 0f ? GroundPose.PivotRight
                                    : GroundPose.PivotLeft);
                            pivotCooldown = profile.PivotCooldown;
                            Enter(GroundMotionState.Pivoting, side);
                        }
                        else if (input.sqrMagnitude > 0.0001f)
                            lastHeading = direction;
                    }
                    break;
            }
        }

        private float DurationOf(GroundPose pose)
        {
            float fallback;
            switch (pose)
            {
                case GroundPose.StartForward:
                case GroundPose.StartBackward:
                case GroundPose.StartLeft:
                case GroundPose.StartRight: fallback = profile.StartSeconds; break;
                case GroundPose.Stop: fallback = profile.StopSeconds; break;
                case GroundPose.PivotLeft:
                case GroundPose.PivotRight: fallback = profile.PivotSeconds; break;
                case GroundPose.TurnLeft:
                case GroundPose.TurnRight: fallback = profile.TurnSeconds; break;
                case GroundPose.JumpStart: fallback = profile.JumpStartSeconds; break;
                case GroundPose.Land: fallback = profile.LandSeconds; break;
                default: fallback = 1f; break;
            }
            return pose == GroundPose.Stop ? profile.StopDuration(StopDirection)
                : profile.DurationFor(pose, fallback);
        }

        private void Enter(GroundMotionState next, GroundPose pose)
        {
            if (State == next && PrimaryPose == pose) return;
            var previous = State;
            State = next;
            PrimaryPose = pose;
            oneShot = pose;
            elapsed = 0f;
            StateChanged?.Invoke(new GroundMotionSignal(previous, next, pose));
        }

        private void BuildTargets(float speed)
        {
            Array.Clear(desired, 0, desired.Length);
            switch (State)
            {
                case GroundMotionState.Moving:
                    // Four cardinal loops blended in local space, not world space.
                    float f = Mathf.Max(0f, direction.y);
                    float b = Mathf.Max(0f, -direction.y);
                    float l = Mathf.Max(0f, -direction.x);
                    float r = Mathf.Max(0f, direction.x);
                    float sum = Mathf.Max(0.0001f, f + b + l + r);
                    CalculateGaitWeights(speed, profile.IdleToWalkEnd,
                        profile.WalkToRunStart, profile.FullRunSpeed,
                        out float gaitIdle, out float gaitWalk, out float gaitRun);
                    float movingAlpha = gaitWalk + gaitRun;
                    desired[(int)GroundPose.Idle] = gaitIdle;
                    desired[(int)GroundPose.MoveForward] = movingAlpha * f / sum;
                    desired[(int)GroundPose.MoveBackward] = movingAlpha * b / sum;
                    desired[(int)GroundPose.MoveLeft] = movingAlpha * l / sum;
                    desired[(int)GroundPose.MoveRight] = movingAlpha * r / sum;
                    break;
                case GroundMotionState.Idle:
                    desired[(int)GroundPose.Idle] = 1f;
                    break;
                default:
                    desired[(int)oneShot] = 1f;
                    break;
            }
        }

        private void Smooth(float dt)
        {
            // Interruption-safe: blend from CURRENT pose weights.
            // Framerate-independent single exponential, normalized once.
            float step = 1f - Mathf.Exp(-Mathf.Max(0f, dt) * profile.BlendSpeed);
            float sum = 0f;
            int best = 0;
            float bestWeight = -1f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.Lerp(weights[i], desired[i], step);
                sum += weights[i];
                if (weights[i] > bestWeight)
                { bestWeight = weights[i]; best = i; }
            }
            if (sum <= 0.0001f)
            {
                weights[(int)GroundPose.Idle] = 1f;
                sum = 1f;
            }
            for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
        }

        private void BeginStop()
        {
            StopDirection = DirectionIndex(lastHeading);
            if (lastMovingSpeed >= profile.MinimumStopSpeed)
                Enter(GroundMotionState.Stopping, GroundPose.Stop);
            else
                Enter(GroundMotionState.Idle, GroundPose.Idle);
            lastMovingSpeed = 0f;
        }
        private static int DirectionIndex(Vector2 heading)
        {
            if (Mathf.Abs(heading.x) > Mathf.Abs(heading.y))
                return heading.x < 0f ? 2 : 3;
            return heading.y < 0f ? 1 : 0;
        }
        private static GroundPose PickStart(Vector2 heading)
        {
            if (Mathf.Abs(heading.x) > Mathf.Abs(heading.y))
                return heading.x >= 0f ? GroundPose.StartRight : GroundPose.StartLeft;
            return heading.y >= 0f ? GroundPose.StartForward : GroundPose.StartBackward;
        }
        private static GroundPose PickMove(Vector2 heading)
        {
            if (Mathf.Abs(heading.x) > Mathf.Abs(heading.y))
                return heading.x >= 0f ? GroundPose.MoveRight : GroundPose.MoveLeft;
            return heading.y >= 0f ? GroundPose.MoveForward : GroundPose.MoveBackward;
        }
    }
}
