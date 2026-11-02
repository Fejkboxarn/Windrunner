using System;
using UnityEngine;

namespace PortableAnimation
{
    public enum HitReactSignalKind { Started, Replaced, Finished }

    public readonly struct HitReactSignal
    {
        public readonly HitReactSignalKind Kind;
        public readonly HitSeverity Severity;
        public readonly HitDirection Direction;
        public HitReactSignal(HitReactSignalKind kind, HitSeverity severity, HitDirection direction)
        { Kind = kind; Severity = severity; Direction = direction; }
        public override string ToString() => Kind + ": " + Severity + " / " + Direction;
    }

    /// <summary>
    /// Plain C# hit-response state machine. It does not depend on Animator,
    /// Playables, a movement controller or a combat implementation.
    ///
    /// Supports severity, direction, independent body/head intensity,
    /// delayed head onset, damped decay and interruptible retriggers.
    /// These are a configurable Unity reconstruction, not recovered
    /// byte-for-byte native timing formulas.
    /// </summary>
    public sealed class HitReactController
    {
        private readonly HitReactProfile profile;
        private readonly float[] directionWeights = new float[4];
        private readonly float[] directionAtStart = new float[4];

        private HitSeveritySettings activeSettings;
        private HitSeverity severity;
        private HitDirection direction;
        private float elapsed;
        private float directionElapsed;
        private float bodyAtStart;
        private float headAtStart;
        private float bodyAlpha;
        private float headAlpha;
        private bool active;

        public event Action<HitReactSignal> SignalRaised;

        public bool IsActive => active;
        public HitSeverity Severity => severity;
        public HitDirection Direction => direction;
        public float Elapsed => elapsed;
        public float BodyWeight => bodyAlpha;
        public float HeadWeight => headAlpha;
        public float DirectionWeight(int index) => directionWeights[index];

        public HitReactController(HitReactProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            directionWeights[(int)HitDirection.Front] = 1f;
        }

        public void Trigger(HitSeverity nextSeverity, HitDirection nextDirection)
        {
            HitSeveritySettings next = profile.Find(nextSeverity);
            if (next == null) throw new ArgumentException("Severity not configured: " + nextSeverity);
            if ((int)nextDirection < 0 || (int)nextDirection >= 4)
                throw new ArgumentOutOfRangeException(nameof(nextDirection));

            if (active)
                SignalRaised?.Invoke(new HitReactSignal(HitReactSignalKind.Replaced, severity, direction));

            bodyAtStart = bodyAlpha;
            headAtStart = headAlpha;
            activeSettings = next;
            severity = nextSeverity;
            direction = nextDirection;
            elapsed = 0f;
            directionElapsed = 0f;
            active = true;
            Array.Copy(directionWeights, directionAtStart, 4);
            SignalRaised?.Invoke(new HitReactSignal(HitReactSignalKind.Started, severity, direction));
        }

        public void Advance(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            if (!active) return;

            elapsed += dt;
            directionElapsed = Mathf.Min(profile.DirectionBlendSeconds, directionElapsed + dt);
            float directionT = Smooth01(directionElapsed / profile.DirectionBlendSeconds);
            for (int i = 0; i < 4; i++)
            {
                float destination = i == (int)direction ? 1f : 0f;
                directionWeights[i] = Mathf.Lerp(directionAtStart[i], destination, directionT);
            }

            bodyAlpha = EvaluateChannel(elapsed, 0f, bodyAtStart,
                activeSettings.BodyStrength, activeSettings);
            headAlpha = EvaluateChannel(elapsed, activeSettings.HeadDelaySeconds,
                headAtStart, activeSettings.HeadStrength, activeSettings);

            float finish = activeSettings.HeadDelaySeconds +
                activeSettings.AttackSeconds + activeSettings.HoldSeconds +
                activeSettings.DecaySeconds;
            if (elapsed >= finish)
            {
                bodyAlpha = 0f;
                headAlpha = 0f;
                active = false;
                SignalRaised?.Invoke(new HitReactSignal(HitReactSignalKind.Finished, severity, direction));
            }
        }

        private static float EvaluateChannel(float now, float delay,
            float initial, float peak, HitSeveritySettings settings)
        {
            float t = now - delay;
            if (t <= 0f) return initial;

            if (t < settings.AttackSeconds)
                return Mathf.Lerp(initial, peak, Smooth01(t / settings.AttackSeconds));

            t -= settings.AttackSeconds;
            if (t <= settings.HoldSeconds) return peak;

            t -= settings.HoldSeconds;
            float u = Mathf.Clamp01(t / settings.DecaySeconds);
            float falloff = (1f - u) * (1f - u);

            // A controlled damped oscillatory envelope, not simulated
            // rigidbody or spring-effector physics.
            float spring = 1f + settings.SpringAmplitude *
                Mathf.Sin(2f * Mathf.PI * settings.SpringCycles * u) *
                Mathf.Exp(-settings.SpringDamping * u);

            return Mathf.Clamp01(peak * falloff * spring);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
