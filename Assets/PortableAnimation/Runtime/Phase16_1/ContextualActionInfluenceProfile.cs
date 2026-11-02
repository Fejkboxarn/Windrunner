using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    // Semantic contexts; not a second locomotion state machine.
    public enum ContextualMotionState
    {
        Idle, Starting, Moving, Stopping, Pivoting, Turning,
        JumpStarting, Rising, Falling, Landing, Hovering, Flying,
        Stunned, KnockedDown, Recovering, Dead,
        // Appended to preserve numeric serialization of existing assets.
        Walking, Running
    }

    [Serializable]
    public sealed class ContextRegionInfluence
    {
        [SerializeField] private AbilityRegion region;
        [SerializeField] private BoneChannelWeights channels;
        [SerializeField, Range(0f, 1f)] private float windup = 1f;
        [SerializeField, Range(0f, 1f)] private float active = 1f;
        [SerializeField, Range(0f, 1f)] private float recovery = 1f;
        [SerializeField] private bool useCustomCurve;
        [SerializeField] private AnimationCurve influenceCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public bool UseCustomCurve => useCustomCurve;
        public AnimationCurve InfluenceCurve => influenceCurve;
        public AbilityRegion Region => region;
        public BoneChannelWeights Channels => channels;
        public float Windup => windup;
        public float Active => active;
        public float Recovery => recovery;
        public ContextRegionInfluence(AbilityRegion region, BoneChannelWeights channels,
            float windup = 1f, float active = 1f, float recovery = 1f)
        {
            this.region = region;
            this.channels = channels;
            this.windup = Mathf.Clamp01(windup);
            this.active = Mathf.Clamp01(active);
            this.recovery = Mathf.Clamp01(recovery);
        }
        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
        public BoneChannelWeights Evaluate(float normalizedTime, float windupEnd, float activeEnd)
        {
            // Continuous authored action timing: never jump instantly at
            // windupEnd or activeEnd. Custom curves allow true per-region tuning.
            float t = Mathf.Clamp01(normalizedTime);
            float modifier;
            if (useCustomCurve && influenceCurve != null && influenceCurve.length > 0)
                modifier = Mathf.Clamp01(influenceCurve.Evaluate(t));
            else if (t <= windupEnd)
                modifier = Mathf.Lerp(windup, active,
                    Ease(t / Mathf.Max(0.001f, windupEnd)));
            else
                modifier = Mathf.Lerp(active, recovery,
                    Ease((t - activeEnd) / Mathf.Max(0.001f, 1f - activeEnd)));
            modifier = Mathf.Clamp01(modifier);
            return new BoneChannelWeights(channels.position * modifier,
                channels.rotation * modifier, channels.scale * modifier);
        }
    }

    [Serializable]
    public sealed class ContextStateInfluence
    {
        [SerializeField] private ContextualMotionState state;
        [SerializeField] private List<ContextRegionInfluence> regions =
            new List<ContextRegionInfluence>();
        public ContextualMotionState State => state;
        public IReadOnlyList<ContextRegionInfluence> Regions => regions;
        public ContextStateInfluence(ContextualMotionState state,
            IEnumerable<ContextRegionInfluence> regions)
        {
            this.state = state;
            this.regions = new List<ContextRegionInfluence>(regions);
        }
        public bool TryResolve(AbilityRegion region, float time, float windupEnd,
            float activeEnd, out BoneChannelWeights result)
        {
            if (regions != null)
                foreach (var rule in regions)
                    if (rule != null && rule.Region == region)
                    {
                        result = rule.Evaluate(time, windupEnd, activeEnd);
                        return true;
                    }
            result = default;
            return false;
        }
    }

    [Serializable]
    public sealed class ContextActionInfluence
    {
        [SerializeField] private string actionId;
        [SerializeField, Range(0f, 1f)] private float windupEnd = 0.2f;
        [SerializeField, Range(0f, 1f)] private float activeEnd = 0.7f;
        [SerializeField] private List<ContextStateInfluence> contexts =
            new List<ContextStateInfluence>();
        public string ActionId => actionId;
        public float WindupEnd => windupEnd;
        public float ActiveEnd => activeEnd;
        public IReadOnlyList<ContextStateInfluence> Contexts => contexts;
        public ContextActionInfluence(string id, IEnumerable<ContextStateInfluence> contexts,
            float windupEnd = 0.2f, float activeEnd = 0.7f)
        {
            actionId = id;
            this.contexts = new List<ContextStateInfluence>(contexts);
            this.windupEnd = windupEnd;
            this.activeEnd = activeEnd;
        }
        public BoneChannelWeights Resolve(ContextualMotionState state, AbilityRegion region,
            float normalizedTime, BoneChannelWeights fallback)
        {
            if (contexts != null)
                foreach (var context in contexts)
                    if (context != null && context.State == state &&
                        context.TryResolve(region, normalizedTime,
                            windupEnd, activeEnd, out var weights)) return weights;
            return fallback;
        }
        public void AssertValid()
        {
            if (string.IsNullOrWhiteSpace(actionId) ||
                windupEnd < 0f || activeEnd > 1f || windupEnd >= activeEnd)
                throw new InvalidOperationException("Invalid contextual action: " + actionId);
            var states = new HashSet<ContextualMotionState>();
            if (contexts == null) throw new InvalidOperationException("Missing context list.");
            foreach (var item in contexts)
            {
                if (item == null || !states.Add(item.State))
                    throw new InvalidOperationException("Duplicate/null state for " + actionId);
                var regions = new HashSet<AbilityRegion>();
                foreach (var r in item.Regions)
                    if (r == null || !regions.Add(r.Region))
                        throw new InvalidOperationException("Duplicate/null body region in " + actionId);
            }
        }
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 16.1 Contextual Actions",
        fileName = "ContextualActionInfluenceProfile")]
    public sealed class ContextualActionInfluenceProfile : ScriptableObject
    {
        [SerializeField] private ActionMontageProfile montageProfile;
        [SerializeField, Min(0.01f)] private float regionBlendSeconds = 0.16f;
        [SerializeField] private List<ContextActionInfluence> actions =
            new List<ContextActionInfluence>();
        public ActionMontageProfile MontageProfile => montageProfile;
        public float RegionBlendSeconds => Mathf.Max(0.01f, regionBlendSeconds);
        public IReadOnlyList<ContextActionInfluence> Actions => actions;
        public void Configure(ActionMontageProfile montage,
            IEnumerable<ContextActionInfluence> entries)
        {
            montageProfile = montage;
            actions = new List<ContextActionInfluence>(entries);
        }
        public ContextActionInfluence Find(string id)
        {
            if (actions == null) return null;
            foreach (var action in actions)
                if (action != null && string.Equals(action.ActionId, id, StringComparison.Ordinal))
                    return action;
            return null;
        }
        public void AssertValidFor(ActionMontageProfile montage)
        {
            if (montage == null || montageProfile != montage)
                throw new InvalidOperationException(
                    "Contextual profile must reference this exact montage profile.");
            if (actions == null) throw new InvalidOperationException("No contextual actions.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in actions)
            {
                if (action == null || !ids.Add(action.ActionId))
                    throw new InvalidOperationException("Duplicate/null contextual action.");
                action.AssertValid();
            }
            foreach (var montageAction in montage.Actions)
                if (Find(montageAction.Id) == null)
                    throw new InvalidOperationException("Missing contextual entry: " + montageAction.Id);
        }
    }
}
