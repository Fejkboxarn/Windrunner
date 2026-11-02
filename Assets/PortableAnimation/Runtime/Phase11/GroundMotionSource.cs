using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// Flat Playables mixer for the Phase 11 locomotion state machine.
    /// Grounded movement is authored as an 8-way local-space blend:
    /// F, FR, R, BR, B, BL, L, FL. Any directional sample may be absent;
    /// its weight is redistributed to the nearest authored samples around the
    /// circle, matching the useful fallback behavior of a sparse 2D blend tree.
    ///
    /// Walk and run share one normalized gait clock so speed changes and
    /// directional blends do not restart the feet.
    /// </summary>
    public sealed class GroundMotionSource : ILocomotionPoseSource
    {
        private const int OriginalCount = (int)GroundPose.Count;
        private const int WalkOffset = OriginalCount;          // 4 cardinal walk loops
        private const int StopOffset = OriginalCount + 4;      // B/L/R stop alternatives
        private const int RunDiagonalOffset = OriginalCount + 7; // FR/BR/BL/FL run
        private const int WalkDiagonalOffset = OriginalCount + 11; // FR/BR/BL/FL walk
        private const int Total = OriginalCount + 15;

        private readonly GroundMotionProfile profile;
        private readonly GroundMotionController controller;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Total];
        private readonly AnimationClip[] assets = new AnimationClip[Total];
        private readonly double[] playbackTimes = new double[Total];

        private readonly float[] directionWeights = new float[8];
        private readonly float[] targetDirectionWeights = new float[8];
        private readonly float[] effectiveRunWeights = new float[8];
        private readonly float[] effectiveWalkWeights = new float[8];

        private double movementCycle;
        private GroundMotionState previousState = GroundMotionState.Idle;
        private bool gaitInitialized;
        private int fallbackFrame;
        private float weight;
        private float idleWeight;
        private float walkWeight;
        private float runWeight;
        private float lastWalkShare = 1f;
        private float lastRunShare;

        public Playable Output => mixer;
        public float Weight => weight;
        public GroundMotionState State => controller.State;
        public GroundPose PrimaryPose => controller.PrimaryPose;
        public float StateTime => controller.StateTime;
        public float Speed => controller.Speed;
        public float MovementCyclePhase => (float)movementCycle;
        public float IdleBlend => idleWeight;
        public float WalkBlend => walkWeight;
        public float RunBlend => runWeight;
        public float PoseWeight(GroundPose pose) => controller.Weight(pose);
        public event Action<GroundMotionSignal> StateChanged
        {
            add { controller.StateChanged += value; }
            remove { controller.StateChanged -= value; }
        }

        public GroundMotionSource(PlayableGraph graph, GroundMotionProfile settings,
            bool standalone = false)
        {
            profile = settings ?? throw new ArgumentNullException(nameof(settings));
            if (standalone) profile.AssertStandaloneValid();
            else profile.AssertValid();
            controller = new GroundMotionController(profile, standalone);
            mixer = AnimationMixerPlayable.Create(graph, Total);

            // Legacy semantic poses / one-shots.
            for (int i = 0; i < OriginalCount; i++)
                assets[i] = profile.Clip((GroundPose)i);

            // The four cardinal run slots are part of the original semantic array,
            // but the dashboard can now mark any directional sample as mirrored.
            // Resolve those effective (possibly generated-mirror) clips explicitly.
            assets[(int)GroundPose.MoveForward] = profile.RunDirectionalClip(0);
            assets[(int)GroundPose.MoveRight] = profile.RunDirectionalClip(2);
            assets[(int)GroundPose.MoveBackward] = profile.RunDirectionalClip(4);
            assets[(int)GroundPose.MoveLeft] = profile.RunDirectionalClip(6);

            // Cardinal walk loops preserve the existing profile serialization but
            // likewise resolve through the per-slot mirror cache.
            assets[WalkOffset + 0] = profile.WalkDirectionalClip(0);
            assets[WalkOffset + 1] = profile.WalkDirectionalClip(4);
            assets[WalkOffset + 2] = profile.WalkDirectionalClip(6);
            assets[WalkOffset + 3] = profile.WalkDirectionalClip(2);

            // Directional stop alternatives.
            for (int i = 1; i < 4; i++)
                assets[StopOffset + (i - 1)] = profile.StopClip(i);

            // Dedicated diagonals are optional. Order: FR, BR, BL, FL.
            assets[RunDiagonalOffset + 0] = profile.RunDirectionalClip(1);
            assets[RunDiagonalOffset + 1] = profile.RunDirectionalClip(3);
            assets[RunDiagonalOffset + 2] = profile.RunDirectionalClip(5);
            assets[RunDiagonalOffset + 3] = profile.RunDirectionalClip(7);

            assets[WalkDiagonalOffset + 0] = profile.WalkDirectionalClip(1);
            assets[WalkDiagonalOffset + 1] = profile.WalkDirectionalClip(3);
            assets[WalkDiagonalOffset + 2] = profile.WalkDirectionalClip(5);
            assets[WalkDiagonalOffset + 3] = profile.WalkDirectionalClip(7);

            for (int i = 0; i < Total; i++)
            {
                bool optionalDirectional =
                    IsRunDirectionalIndex(i) ||
                    IsWalkDirectionalIndex(i) ||
                    i >= RunDiagonalOffset;
                bool optionalTurn =
                    !profile.EnableTurning &&
                    (i == (int)GroundPose.TurnLeft ||
                     i == (int)GroundPose.TurnRight);

                if (assets[i] == null || assets[i].length <= 0f)
                {
                    if (standalone && !optionalDirectional && !optionalTurn)
                    {
                        // Sparse one-shots are allowed. Use the real idle pose
                        // as a safe visual fallback; GroundMotionController
                        // skips any missing one-shot's hold state entirely.
                        assets[i] = profile.Clip(GroundPose.Idle);
                    }
                }

                if (assets[i] == null || assets[i].length <= 0f)
                {
                    if (optionalDirectional || optionalTurn)
                        continue;
                    throw new InvalidOperationException(
                        "Missing GroundMotion asset at index " + i);
                }

                playables[i] = AnimationClipPlayable.Create(graph, assets[i]);
                playables[i].SetSpeed(0d);
                playables[i].SetTime(0d);
                if (!graph.Connect(playables[i], 0, mixer, i))
                    throw new InvalidOperationException(
                        "GroundMotion clip connection failed " + i);
                mixer.SetInputWeight(i,
                    i == (int)GroundPose.Idle ? 1f : 0f);
            }

            directionWeights[0] = 1f;
            targetDirectionWeights[0] = 1f;
        }

        public void Tick(in GroundMotionFrame frame)
        {
            controller.Advance(in frame);
            float dt = Mathf.Max(0f, frame.Locomotion.DeltaTime);
            weight = controller.Speed;

            GroundMotionController.CalculateGaitWeights(weight,
                profile.IdleToWalkEnd, profile.WalkToRunStart, profile.FullRunSpeed,
                out idleWeight, out walkWeight, out runWeight);

            if (controller.State == GroundMotionState.Moving)
            {
                if (!gaitInitialized)
                {
                    movementCycle = profile.EntryGaitPhase;
                    gaitInitialized = true;
                }

                // Walk and run track the SAME gait phase. Direction changes only
                // modify mixer weights; they never restart a clip.
                double rate = Math.Max(0.15,
                    profile.MovementPlaybackRate *
                    Mathf.Lerp(0.55f, 1.25f, weight));
                movementCycle = Repeat01(movementCycle + dt * rate /
                    Math.Max(0.0001, MovementReferenceLength()));
            }
            else if (controller.State == GroundMotionState.Idle &&
                previousState != GroundMotionState.Idle)
            {
                // Preserve phase through short stop/start interruptions.
                gaitInitialized = true;
            }
            previousState = controller.State;

            UpdateDirectionalBlend(controller.LocalDirection, dt);
            BuildEffectiveDirectionalWeights(false, effectiveRunWeights);
            BuildEffectiveDirectionalWeights(true, effectiveWalkWeights);

            for (int i = 0; i < Total; i++)
                mixer.SetInputWeight(i, 0f);

            float movingTarget = walkWeight + runWeight;
            if (movingTarget > 0.00001f)
            {
                lastWalkShare = walkWeight / movingTarget;
                lastRunShare = runWeight / movingTarget;
            }

            // At zero speed preserve the previous gait mixture while its
            // outgoing movement envelope fades.
            float walkShare = lastWalkShare;
            float runShare = lastRunShare;

            // Idle + one-shots still come from the deterministic GroundMotion
            // controller. Cardinal Move* poses are handled by the 8-way layer.
            for (int i = 0; i < OriginalCount; i++)
            {
                var pose = (GroundPose)i;
                if (pose >= GroundPose.MoveForward &&
                    pose <= GroundPose.MoveRight)
                    continue;

                float alpha = controller.Weight(pose);
                if (pose == GroundPose.Stop &&
                    controller.StopDirection != 0)
                    alpha = 0f;

                mixer.SetInputWeight(i, alpha);
                if (alpha > 0.00001f)
                    Sample(i, pose, dt);
            }

            float moveEnvelope = MoveShare();
            if (moveEnvelope > 0.00001f)
            {
                for (int direction = 0; direction < 8; direction++)
                {
                    float runAlpha =
                        moveEnvelope * runShare * effectiveRunWeights[direction];
                    int runIndex = RunIndex(direction);
                    if (runAlpha > 0.00001f && HasPlayable(runIndex))
                    {
                        mixer.SetInputWeight(runIndex, runAlpha);
                        SampleDirectionalLoop(runIndex, direction);
                    }

                    float walkAlpha =
                        moveEnvelope * walkShare * effectiveWalkWeights[direction];
                    int walkIndex = WalkIndex(direction);
                    if (walkAlpha > 0.00001f && HasPlayable(walkIndex))
                    {
                        mixer.SetInputWeight(walkIndex, walkAlpha);
                        SampleDirectionalLoop(walkIndex, direction);
                    }
                }
            }

            if (controller.StopDirection > 0 &&
                controller.Weight(GroundPose.Stop) > 0.00001f)
            {
                int idx = StopOffset + controller.StopDirection - 1;
                mixer.SetInputWeight(idx,
                    controller.Weight(GroundPose.Stop));
                if (controller.State == GroundMotionState.Stopping)
                    SampleOneShot(idx,
                        profile.StopDuration(controller.StopDirection));
                else
                    SetTime(idx, playbackTimes[idx]);
            }
        }

        private void UpdateDirectionalBlend(Vector2 localDirection, float dt)
        {
            if (localDirection.sqrMagnitude > 0.0001f)
            {
                Array.Clear(targetDirectionWeights, 0,
                    targetDirectionWeights.Length);

                Vector2 direction = localDirection.normalized;
                // 0 degrees = forward, positive clockwise/right.
                float angle = Mathf.Atan2(direction.x, direction.y) *
                    Mathf.Rad2Deg;
                float sector = Mathf.Repeat(angle / 45f, 8f);
                int lower = Mathf.FloorToInt(sector) & 7;
                int upper = (lower + 1) & 7;
                float t = sector - Mathf.Floor(sector);

                targetDirectionWeights[lower] = 1f - t;
                targetDirectionWeights[upper] += t;
            }

            float step = 1f - Mathf.Exp(
                -Mathf.Max(0f, dt) *
                Mathf.Max(0.01f, profile.BlendSpeed));

            float sum = 0f;
            for (int i = 0; i < 8; i++)
            {
                directionWeights[i] = Mathf.Lerp(
                    directionWeights[i],
                    targetDirectionWeights[i],
                    step);
                sum += directionWeights[i];
            }

            if (sum <= 0.0001f)
            {
                directionWeights[0] = 1f;
                sum = 1f;
            }

            for (int i = 0; i < 8; i++)
                directionWeights[i] /= sum;
        }

        private void BuildEffectiveDirectionalWeights(
            bool walk, float[] output)
        {
            Array.Clear(output, 0, output.Length);

            bool[] available = new bool[8];
            int availableCount = 0;
            for (int direction = 0; direction < 8; direction++)
            {
                available[direction] =
                    profile.HasUsableDirectionalClip(walk, direction);
                if (available[direction])
                    availableCount++;
            }

            if (availableCount == 0)
                return;

            // Treat authored clips as points around a circular 2D blend space.
            // Any missing point (diagonal OR cardinal) redistributes its weight
            // to the closest authored points on either side. This lets a set such
            // as FR + BR represent pure Right, and—combined with mirroring—lets a
            // sparse clip pack remain smoothly steerable.
            for (int direction = 0; direction < 8; direction++)
            {
                float alpha = directionWeights[direction];
                if (alpha <= 0.000001f)
                    continue;

                if (available[direction])
                {
                    output[direction] += alpha;
                    continue;
                }

                int ccw = FindAvailableDirection(available, direction, -1);
                int cw = FindAvailableDirection(available, direction, +1);

                if (ccw < 0 && cw < 0)
                    continue;
                if (ccw < 0 || ccw == cw)
                {
                    output[cw] += alpha;
                    continue;
                }
                if (cw < 0)
                {
                    output[ccw] += alpha;
                    continue;
                }

                int ccwDistance = CircularDistance(direction, ccw, -1);
                int cwDistance = CircularDistance(direction, cw, +1);
                float total = Mathf.Max(1f, ccwDistance + cwDistance);

                // Linear interpolation across the angular arc: the closer sample
                // receives the larger share.
                output[ccw] += alpha * (cwDistance / total);
                output[cw] += alpha * (ccwDistance / total);
            }

            float sum = 0f;
            for (int i = 0; i < 8; i++)
                sum += output[i];

            if (sum <= 0.0001f)
                return;

            for (int i = 0; i < 8; i++)
                output[i] /= sum;
        }

        private static int FindAvailableDirection(
            bool[] available, int from, int step)
        {
            for (int distance = 1; distance <= 8; distance++)
            {
                int candidate = (from + step * distance) & 7;
                if (available[candidate])
                    return candidate;
            }
            return -1;
        }

        private static int CircularDistance(int from, int to, int step)
        {
            for (int distance = 1; distance <= 8; distance++)
                if (((from + step * distance) & 7) == to)
                    return distance;
            return 8;
        }

        private static bool IsRunDirectionalIndex(int index)
        {
            return index == (int)GroundPose.MoveForward ||
                index == (int)GroundPose.MoveRight ||
                index == (int)GroundPose.MoveBackward ||
                index == (int)GroundPose.MoveLeft;
        }

        private static bool IsWalkDirectionalIndex(int index)
        {
            return index >= WalkOffset && index < WalkOffset + 4;
        }

        private double MovementReferenceLength()
        {
            // Prefer forward Run for stable historical timing, but sparse blend
            // spaces are allowed. Fall back to the first usable directional loop.
            int forward = RunIndex(0);
            if (HasPlayable(forward))
                return assets[forward].length;

            for (int direction = 0; direction < 8; direction++)
            {
                int run = RunIndex(direction);
                if (HasPlayable(run))
                    return assets[run].length;
            }

            for (int direction = 0; direction < 8; direction++)
            {
                int walk = WalkIndex(direction);
                if (HasPlayable(walk))
                    return assets[walk].length;
            }

            return 1d;
        }

        private float MoveShare()
        {
            return controller.Weight(GroundPose.MoveForward) +
                controller.Weight(GroundPose.MoveBackward) +
                controller.Weight(GroundPose.MoveLeft) +
                controller.Weight(GroundPose.MoveRight);
        }

        private bool HasPlayable(int index)
        {
            return index >= 0 && index < assets.Length &&
                assets[index] != null && assets[index].length > 0f;
        }

        private static int RunIndex(int direction)
        {
            switch ((direction % 8 + 8) % 8)
            {
                case 0: return (int)GroundPose.MoveForward;
                case 1: return RunDiagonalOffset + 0;
                case 2: return (int)GroundPose.MoveRight;
                case 3: return RunDiagonalOffset + 1;
                case 4: return (int)GroundPose.MoveBackward;
                case 5: return RunDiagonalOffset + 2;
                case 6: return (int)GroundPose.MoveLeft;
                default: return RunDiagonalOffset + 3;
            }
        }

        private static int WalkIndex(int direction)
        {
            switch ((direction % 8 + 8) % 8)
            {
                case 0: return WalkOffset + 0;
                case 1: return WalkDiagonalOffset + 0;
                case 2: return WalkOffset + 3;
                case 3: return WalkDiagonalOffset + 1;
                case 4: return WalkOffset + 1;
                case 5: return WalkDiagonalOffset + 2;
                case 6: return WalkOffset + 2;
                default: return WalkDiagonalOffset + 3;
            }
        }

        private void Sample(int index, GroundPose pose, float dt)
        {
            if (pose == GroundPose.Idle)
            {
                playbackTimes[index] += dt * 0.65;
                SetTime(index,
                    playbackTimes[index] % assets[index].length);
            }
            else if (pose == GroundPose.Fall)
            {
                playbackTimes[index] =
                    (playbackTimes[index] + dt) %
                    Math.Max(0.0001d, assets[index].length);
                SetTime(index, playbackTimes[index]);
            }
            else if (pose == controller.PrimaryPose)
            {
                SampleOneShot(index,
                    profile.DurationFor(pose, FallbackDuration(pose)));
            }
            else
            {
                // Outgoing one-shot pose freezes while its weight fades.
                SetTime(index, playbackTimes[index]);
            }
        }

        private void SampleDirectionalLoop(int index, int direction)
        {
            float phase = GroundMotionCycleMath.SampleDirectional(
                (float)movementCycle,
                direction,
                profile.MovementPhaseOffsets);
            SetTime(index,
                phase * Math.Max(0.0001d, assets[index].length));
        }

        private void SampleOneShot(int index, float stateDuration)
        {
            float normalized = Mathf.Clamp01(
                controller.StateTime /
                Mathf.Max(0.001f, stateDuration));
            SetTime(index,
                normalized *
                Math.Max(0d, assets[index].length - 0.0001d));
        }

        private void SetTime(int index, double seconds)
        {
            if (!HasPlayable(index))
                return;

            playbackTimes[index] = Math.Max(0d,
                Math.Min(seconds,
                    Math.Max(0d, assets[index].length - 0.0001d)));
            playables[index].SetTime(playbackTimes[index]);
        }

        private static double Repeat01(double value) =>
            value - Math.Floor(value);

        private float FallbackDuration(GroundPose pose)
        {
            switch (pose)
            {
                case GroundPose.StartForward:
                case GroundPose.StartBackward:
                case GroundPose.StartLeft:
                case GroundPose.StartRight:
                    return profile.StartSeconds;
                case GroundPose.Stop:
                    return profile.StopSeconds;
                case GroundPose.PivotLeft:
                case GroundPose.PivotRight:
                    return profile.PivotSeconds;
                case GroundPose.TurnLeft:
                case GroundPose.TurnRight:
                    return profile.TurnSeconds;
                case GroundPose.JumpStart:
                    return profile.JumpStartSeconds;
                case GroundPose.Land:
                    return profile.LandSeconds;
                default:
                    return 1f;
            }
        }

        public void Tick(float speed, float dt)
        {
            fallbackFrame++;
            var locomotion = new AnimationFrameState(
                fallbackFrame,
                dt,
                Vector3.forward * Mathf.Clamp01(speed),
                Mathf.Clamp01(speed),
                LocomotionMode.Grounded);
            var frame = new GroundMotionFrame(
                locomotion, true, 0f, 0f);
            Tick(in frame);
        }
    }
}
