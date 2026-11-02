using System;
using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// CPU-only data evaluator. It does not create a Playable, job or NativeArray.
    /// Reads editable profile values every frame and smooths region-channel changes
    /// from their CURRENT values when context or action timing changes.
    /// </summary>
    public sealed class ContextualActionInfluenceController
    {
        private readonly ContextualActionInfluenceProfile profile;
        private readonly ActionMontageProfile montage;
        private readonly BoneChannelWeights[,] blended;
        private readonly float[] actionTimes;
        private readonly BoneChannelWeights[,] contextStart;
        private bool initialized;
        private float transitionElapsed;
        public float ContextTransitionProgress { get; private set; } = 1f;
        public float ActionNormalizedTime(int index) => actionTimes[index];

        public ContextualMotionState CurrentContext { get; private set; }
        public int ActionCount => montage.Actions.Count;
        public ContextualActionInfluenceController(ContextualActionInfluenceProfile profile,
            ActionMontageProfile montage)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.AssertValidFor(montage);
            this.profile = profile;
            this.montage = montage;
            blended = new BoneChannelWeights[montage.Actions.Count,
                Enum.GetValues(typeof(AbilityRegion)).Length];
            actionTimes = new float[montage.Actions.Count];
            contextStart = new BoneChannelWeights[montage.Actions.Count,
                Enum.GetValues(typeof(AbilityRegion)).Length];
        }

        public void Tick(ContextualMotionState context, float deltaTime,
            Func<int, double> actionSeconds)
        {
            float dt = Mathf.Max(0f, deltaTime);
            bool changed = initialized && CurrentContext != context;
            if (changed)
            {
                for (int a = 0; a < montage.Actions.Count; a++)
                    for (int r = 0; r < blended.GetLength(1); r++)
                        contextStart[a, r] = blended[a, r];
                transitionElapsed = 0f;
            }
            CurrentContext = context;
            if (initialized) transitionElapsed += dt;
            float t = Mathf.Clamp01(transitionElapsed / profile.RegionBlendSeconds);
            float eased = t * t * (3f - 2f * t);
            ContextTransitionProgress = initialized ? eased : 1f;

            for (int a = 0; a < montage.Actions.Count; a++)
            {
                var entry = montage.Actions[a];
                float length = Mathf.Max(0.0001f, entry.Clip.length);
                actionTimes[a] = Mathf.Clamp01((float)(actionSeconds(a) / length));
                var settings = profile.Find(entry.Id);
                for (int r = 0; r < blended.GetLength(1); r++)
                {
                    var region = (AbilityRegion)r;
                    BoneChannelWeights baseMask = entry.Policy.Resolve(region);
                    BoneChannelWeights target = settings == null ? baseMask :
                        settings.Resolve(context, region, actionTimes[a], baseMask);
                    if (!initialized || t >= 1f)
                    {
                        blended[a, r] = target;
                        continue;
                    }
                    var from = contextStart[a, r];
                    blended[a, r] = new BoneChannelWeights(
                        Mathf.Lerp(from.position, target.position, eased),
                        Mathf.Lerp(from.rotation, target.rotation, eased),
                        Mathf.Lerp(from.scale, target.scale, eased));
                }
            }
            if (!initialized)
            {
                // Initial samples are already evaluated directly above.
                // Do not accidentally interpolate from zero on frame two.
                transitionElapsed = profile.RegionBlendSeconds;
                ContextTransitionProgress = 1f;
            }
            initialized = true;
        }

        public BoneChannelWeights Weight(int actionIndex, AbilityRegion region)
        {
            if (actionIndex < 0 || actionIndex >= ActionCount)
                throw new ArgumentOutOfRangeException(nameof(actionIndex));
            return blended[actionIndex, (int)region];
        }

        public static ContextualMotionState ResolveContext(GroundMotionState ground,
            bool grounded, bool flying, AeroMotionState aero, HeroPoseState hero)
        {
            switch (hero)
            {
                case HeroPoseState.Stunned: return ContextualMotionState.Stunned;
                case HeroPoseState.KnockedDown: return ContextualMotionState.KnockedDown;
                case HeroPoseState.Recovering: return ContextualMotionState.Recovering;
                case HeroPoseState.Dead: return ContextualMotionState.Dead;
            }
            if (flying)
                return aero == AeroMotionState.Hovering
                    ? ContextualMotionState.Hovering : ContextualMotionState.Flying;
            if (!grounded && ground == GroundMotionState.Idle)
                return ContextualMotionState.Falling;
            switch (ground)
            {
                case GroundMotionState.Starting: return ContextualMotionState.Starting;
                case GroundMotionState.Moving: return ContextualMotionState.Moving;
                case GroundMotionState.Stopping: return ContextualMotionState.Stopping;
                case GroundMotionState.Pivoting: return ContextualMotionState.Pivoting;
                case GroundMotionState.Turning: return ContextualMotionState.Turning;
                case GroundMotionState.JumpStarting: return ContextualMotionState.JumpStarting;
                case GroundMotionState.Rising: return ContextualMotionState.Rising;
                case GroundMotionState.Falling: return ContextualMotionState.Falling;
                case GroundMotionState.Landing: return ContextualMotionState.Landing;
                default: return ContextualMotionState.Idle;
            }
        }
    }
}
