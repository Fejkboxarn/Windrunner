using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableAnimation
{
    public enum HitSeverity { Local, Light, Medium, Severe }
    public enum HitDirection { Front, Back, Left, Right }

    [Serializable]
    public sealed class HitSeveritySettings
    {
        [SerializeField] private HitSeverity severity;
        [SerializeField, Range(0f, 1f)] private float bodyStrength = 0.45f;
        [SerializeField, Range(0f, 1f)] private float headStrength = 0.35f;
        [SerializeField, Min(0.001f)] private float attackSeconds = 0.07f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.04f;
        [SerializeField, Min(0.001f)] private float decaySeconds = 0.5f;
        [SerializeField, Min(0f)] private float headDelaySeconds = 0.025f;
        [SerializeField, Range(0f, 0.9f)] private float springAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float springCycles = 2f;
        [SerializeField, Min(0f)] private float springDamping = 3f;

        public HitSeverity Severity => severity;
        public float BodyStrength => bodyStrength;
        public float HeadStrength => headStrength;
        public float AttackSeconds => attackSeconds;
        public float HoldSeconds => holdSeconds;
        public float DecaySeconds => decaySeconds;
        public float HeadDelaySeconds => headDelaySeconds;
        public float SpringAmplitude => springAmplitude;
        public float SpringCycles => springCycles;
        public float SpringDamping => springDamping;

        public HitSeveritySettings(HitSeverity severity, float body, float head,
            float attack, float hold, float decay, float headDelay,
            float springAmplitude, float springCycles, float springDamping)
        {
            this.severity = severity;
            bodyStrength = Mathf.Clamp01(body);
            headStrength = Mathf.Clamp01(head);
            attackSeconds = Mathf.Max(0.001f, attack);
            holdSeconds = Mathf.Max(0f, hold);
            decaySeconds = Mathf.Max(0.001f, decay);
            headDelaySeconds = Mathf.Max(0f, headDelay);
            this.springAmplitude = Mathf.Clamp(springAmplitude, 0f, 0.9f);
            this.springCycles = Mathf.Max(0f, springCycles);
            this.springDamping = Mathf.Max(0f, springDamping);
        }

        public void AssertValid()
        {
            if (attackSeconds <= 0f || decaySeconds <= 0f || holdSeconds < 0f ||
                headDelaySeconds < 0f || bodyStrength < 0f || headStrength < 0f)
                throw new InvalidOperationException("Invalid HitReact timing or strength: " + severity);
        }
    }

    /// <summary>
    /// A dedicated HitReact profile. Direction clips are static reaction poses;
    /// timing and damped intensity envelopes are controlled independently by C#.
    /// Reuses Batch 7's complete montage/AimOffset configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "Portable Animation/Phase 8 HitReact Profile",
        fileName = "HitReactProfile")]
    public sealed class HitReactProfile : ScriptableObject
    {
        [SerializeField] private AimOffsetProfile aimOffsetProfile;
        [SerializeField] private AnimationClip neutralReference;
        [SerializeField] private AnimationClip[] bodyDirections = new AnimationClip[4];
        [SerializeField] private AnimationClip[] headDirections = new AnimationClip[4];
        [SerializeField] private BoneWeightMask bodyMask;
        [SerializeField] private BoneWeightMask headMask;
        [SerializeField, Min(0.001f)] private float directionBlendSeconds = 0.14f;
        [SerializeField] private List<HitSeveritySettings> severities = new List<HitSeveritySettings>();

        public AimOffsetProfile AimOffsetProfile => aimOffsetProfile;
        public AnimationClip NeutralReference => neutralReference;
        public BoneWeightMask BodyMask => bodyMask;
        public BoneWeightMask HeadMask => headMask;
        public float DirectionBlendSeconds => directionBlendSeconds;
        public IReadOnlyList<HitSeveritySettings> Severities => severities;

        public AnimationClip BodyClip(HitDirection direction) => bodyDirections[(int)direction];
        public AnimationClip HeadClip(HitDirection direction) => headDirections[(int)direction];

        public HitSeveritySettings Find(HitSeverity severity)
        {
            if (severities != null)
                foreach (var setting in severities)
                    if (setting != null && setting.Severity == severity)
                        return setting;
            return null;
        }

        public void Configure(AimOffsetProfile aim, AnimationClip neutral,
            AnimationClip[] body, AnimationClip[] head,
            BoneWeightMask bodyRegion, BoneWeightMask headRegion,
            params HitSeveritySettings[] presets)
        {
            aimOffsetProfile = aim;
            neutralReference = neutral;
            bodyDirections = body;
            headDirections = head;
            bodyMask = bodyRegion;
            headMask = headRegion;
            directionBlendSeconds = 0.14f;
            severities = presets == null
                ? new List<HitSeveritySettings>()
                : new List<HitSeveritySettings>(presets);
        }

        public void AssertValid()
        {
            if (aimOffsetProfile == null || neutralReference == null ||
                bodyMask == null || headMask == null)
                throw new InvalidOperationException("HitReact requires AO profile, reference and both masks.");
            aimOffsetProfile.AssertValid();

            if (bodyDirections == null || headDirections == null ||
                bodyDirections.Length != 4 || headDirections.Length != 4)
                throw new InvalidOperationException("Four body and four head direction clips are required.");
            for (int i = 0; i < 4; i++)
                if (bodyDirections[i] == null || headDirections[i] == null)
                    throw new InvalidOperationException("Direction clip missing at index " + i);

            if (directionBlendSeconds <= 0f)
                throw new InvalidOperationException("Direction transition must be greater than zero.");

            if (severities == null || severities.Count != 4)
                throw new InvalidOperationException("Four HitReact severity presets are required.");
            var seen = new HashSet<HitSeverity>();
            foreach (var entry in severities)
            {
                if (entry == null) throw new InvalidOperationException("Null hit severity preset.");
                entry.AssertValid();
                if (!seen.Add(entry.Severity))
                    throw new InvalidOperationException("Duplicate hit severity preset: " + entry.Severity);
            }
            foreach (HitSeverity s in Enum.GetValues(typeof(HitSeverity)))
                if (!seen.Contains(s))
                    throw new InvalidOperationException("Missing severity preset: " + s);
        }
    }
}
