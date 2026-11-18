using System;
using UnityEngine;

namespace PortableAnimation
{
    [Serializable]
    public sealed class AdaptiveLocomotionSettings
    {
        public bool enabled;
        public bool calibrated;
        [Min(.01f)] public float referenceSpeed = 6;
        [Min(0)] public float minimumSpeed = .65f;
        [Min(0)] public float maximumSpeed = 1.5f;
        [Min(0)] public float smoothingSeconds = .08f;
        [Min(0)] public float designerMultiplier = 1;
        public bool useResponseCurve;
        public AnimationCurve responseCurve = AnimationCurve.Linear(0, 0, 2, 2);
        [Range(0, 8)] public int phaseGroup;
        [Range(0, 1)] public float leftContact;
        [Range(0, 1)] public float rightContact = .5f;

        public void AssertValid()
        {
            if (!Finite(referenceSpeed, .01f, 1000) || !Finite(minimumSpeed, 0, 100) ||
                !Finite(maximumSpeed, minimumSpeed, 100) || !Finite(smoothingSeconds, 0, 10) ||
                !Finite(designerMultiplier, 0, 100) || phaseGroup < 0 || phaseGroup > 8 ||
                !Finite(leftContact, 0, 1) || !Finite(rightContact, 0, 1))
                throw new InvalidOperationException("Adaptive locomotion needs finite reference, clamps, smoothing and contact markers.");
            if (!useResponseCurve) return;
            if (responseCurve == null || responseCurve.length == 0)
                throw new InvalidOperationException("Adaptive response curve needs keys.");
            foreach (var key in responseCurve.keys)
                if (!Finite(key.time, 0, 100) || !Finite(key.value, 0, 100))
                    throw new InvalidOperationException("Adaptive response uses nonnegative speed ratios and multipliers up to 100.");
            for (int i = 0; i <= 1024; i++)
                if (!Finite(responseCurve.Evaluate(i * 100f / 1024), 0, 100))
                    throw new InvalidOperationException("Adaptive response tangents exceed the supported range.");
        }
        static bool Finite(float value, float min, float max) => value >= min && value <= max && !float.IsInfinity(value);
    }

    public readonly struct AdaptiveLocomotionSample
    {
        public readonly bool Enabled, Synchronized;
        public readonly float ActualSpeed, ReferenceSpeed, Ratio, Response, Designer, Multiplier;
        public AdaptiveLocomotionSample(bool enabled, float actual, float reference, float ratio,
            float response, float designer, float multiplier, bool synchronized = false)
        {
            Enabled = enabled; ActualSpeed = actual; ReferenceSpeed = reference; Ratio = ratio;
            Response = response; Designer = designer; Multiplier = multiplier; Synchronized = synchronized;
        }
    }

    /// <summary>Pure presentation speed calculation shared by the graph and Studio.</summary>
    public sealed class AdaptiveLocomotionClock
    {
        readonly AdaptiveLocomotionSettings settings;
        float current;
        public AdaptiveLocomotionClock(AdaptiveLocomotionSettings settings)
        {
            settings.AssertValid(); this.settings = settings;
            current = settings.minimumSpeed;
        }
        public AdaptiveLocomotionSample Advance(float actualSpeed, float deltaTime)
        {
            if (float.IsNaN(actualSpeed) || float.IsInfinity(actualSpeed) || actualSpeed < 0 ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException("Adaptive speed and delta time must be finite and nonnegative.");
            float ratio = actualSpeed / settings.referenceSpeed;
            float response = settings.useResponseCurve ? settings.responseCurve.Evaluate(Mathf.Min(100, ratio)) : ratio;
            float target = Mathf.Clamp(response * settings.designerMultiplier, settings.minimumSpeed, settings.maximumSpeed);
            float blend = settings.smoothingSeconds <= 0 ? 1 : 1 - Mathf.Exp(-deltaTime / settings.smoothingSeconds);
            current = Mathf.Lerp(current, target, blend);
            return new AdaptiveLocomotionSample(true, actualSpeed, settings.referenceSpeed, ratio,
                response, settings.designerMultiplier, current);
        }
    }
}
