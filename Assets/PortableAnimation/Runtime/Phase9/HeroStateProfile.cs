using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    public enum HeroPoseState { Normal = 0, Stunned = 1, KnockedDown = 2, Recovering = 3, Dead = 4 }

    public enum HeroStateSignalKind { Entered, Interrupted, Completed, Rejected, Reset }

    public readonly struct HeroStateSignal
    {
        public readonly HeroStateSignalKind Kind;
        public readonly HeroPoseState Previous;
        public readonly HeroPoseState Current;
        public HeroStateSignal(HeroStateSignalKind kind, HeroPoseState previous, HeroPoseState current)
        { Kind = kind; Previous = previous; Current = current; }
        public override string ToString() => Kind + ": " + Previous + " -> " + Current;
    }

    [Serializable]
    public sealed class HeroStateEntry
    {
        [SerializeField] private HeroPoseState state;
        [SerializeField] private AnimationClip poseClip;
        [SerializeField, Min(0f)] private float blendInSeconds = 0.25f;
        [SerializeField, Min(0f)] private float blendOutSeconds = 0.3f;
        [SerializeField, Min(0f)] private float durationSeconds = 1f;
        [SerializeField] private HeroPoseState nextOnCompletion = HeroPoseState.Normal;
        [SerializeField] private int priority = 20;
        [SerializeField] private bool interruptible = true;
        [SerializeField] private bool blocksActions = true;
        [SerializeField] private bool blocksNewHits;
        [SerializeField] private bool suppressesAimOffset;
        [SerializeField] private bool terminal;
        [SerializeField] private List<AbilityRegionChannels> regions = new List<AbilityRegionChannels>();

        public HeroPoseState State => state;
        public AnimationClip Clip => poseClip;
        public float BlendIn => blendInSeconds;
        public float BlendOut => blendOutSeconds;
        public float Duration => durationSeconds;
        public HeroPoseState Next => nextOnCompletion;
        public int Priority => priority;
        public bool Interruptible => interruptible;
        public bool BlocksActions => blocksActions;
        public bool BlocksNewHits => blocksNewHits;
        public bool SuppressesAimOffset => suppressesAimOffset;
        public bool Terminal => terminal;
        public IReadOnlyList<AbilityRegionChannels> Regions => regions;

        public HeroStateEntry(HeroPoseState state, AnimationClip clip, int priority,
            float blendIn, float blendOut, float duration, HeroPoseState next,
            bool interruptible, bool blocksActions, bool blocksHits, bool suppressAO,
            bool terminal, params AbilityRegionChannels[] weights)
        {
            this.state = state;
            poseClip = clip;
            this.priority = priority;
            blendInSeconds = Mathf.Max(0f, blendIn);
            blendOutSeconds = Mathf.Max(0f, blendOut);
            durationSeconds = Mathf.Max(0f, duration);
            nextOnCompletion = next;
            this.interruptible = interruptible;
            this.blocksActions = blocksActions;
            blocksNewHits = blocksHits;
            suppressesAimOffset = suppressAO;
            this.terminal = terminal;
            regions = weights == null ? new List<AbilityRegionChannels>() : new List<AbilityRegionChannels>(weights);
        }

        public BoneChannelWeights Resolve(AbilityRegion region)
        {
            var result = new BoneChannelWeights(0f, 0f, 0f);
            if (regions == null) return result;
            foreach (var item in regions)
                if (item != null && item.Region == region) result = item.Channels;
            return result;
        }

        public void AssertValid()
        {
            if (state == HeroPoseState.Normal || poseClip == null || poseClip.length <= 0f)
                throw new InvalidOperationException("HeroState must have a non-Normal state and a valid clip.");
            if (blendInSeconds < 0f || blendOutSeconds < 0f || durationSeconds < 0f)
                throw new InvalidOperationException("Negative HeroState timing: " + state);
            if (terminal && durationSeconds > 0f)
                throw new InvalidOperationException("Terminal HeroState must not auto-expire: " + state);
            if (!terminal && durationSeconds <= 0f)
                throw new InvalidOperationException("Nonterminal HeroState needs a duration: " + state);
            if (nextOnCompletion == state && !terminal)
                throw new InvalidOperationException("HeroState cannot automatically transition to itself.");
            if (regions == null || regions.Count == 0)
                throw new InvalidOperationException("HeroState requires region weights: " + state);
        }
    }

    [CreateAssetMenu(menuName = "Portable Animation/Phase 9 HeroState", fileName = "HeroStateProfile")]
    public sealed class HeroStateProfile : ScriptableObject
    {
        [SerializeField] private HitReactProfile previousPipeline;
        [SerializeField] private List<HeroStateEntry> states = new List<HeroStateEntry>();
        public HitReactProfile PreviousPipeline => previousPipeline;
        public IReadOnlyList<HeroStateEntry> States => states;

        public void Configure(HitReactProfile hitProfile, params HeroStateEntry[] entries)
        {
            previousPipeline = hitProfile;
            states = entries == null ? new List<HeroStateEntry>() : new List<HeroStateEntry>(entries);
        }

        public int IndexOf(HeroPoseState kind)
        {
            if (kind == HeroPoseState.Normal) return -1;
            for (int i = 0; i < states.Count; i++)
                if (states[i] != null && states[i].State == kind) return i;
            return -1;
        }

        public HeroStateEntry Find(HeroPoseState kind)
        {
            int index = IndexOf(kind);
            return index < 0 ? null : states[index];
        }

        public void AssertValid()
        {
            if (previousPipeline == null) throw new InvalidOperationException("Missing Phase 8 HitReact profile.");
            previousPipeline.AssertValid();
            if (states == null || states.Count != 4)
                throw new InvalidOperationException("Phase 9 requires four state definitions.");
            var unique = new HashSet<HeroPoseState>();
            foreach (var entry in states)
            {
                if (entry == null) throw new InvalidOperationException("Null HeroState entry.");
                entry.AssertValid();
                if (!unique.Add(entry.State)) throw new InvalidOperationException("Duplicate HeroState: " + entry.State);
            }
            foreach (HeroPoseState state in Enum.GetValues(typeof(HeroPoseState)))
                if (state != HeroPoseState.Normal && !unique.Contains(state))
                    throw new InvalidOperationException("Missing HeroState: " + state);
            if (!Find(HeroPoseState.Dead).Terminal)
                throw new InvalidOperationException("Dead must be terminal in this testbed.");
            if (Find(HeroPoseState.KnockedDown).Next != HeroPoseState.Recovering)
                throw new InvalidOperationException("KnockedDown should progress to Recovering.");
            if (Find(HeroPoseState.Recovering).Next != HeroPoseState.Normal)
                throw new InvalidOperationException("Recovering should progress to Normal.");
        }
    }
}
