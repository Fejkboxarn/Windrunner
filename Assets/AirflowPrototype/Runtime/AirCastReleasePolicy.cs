using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Snapshot of the asset's cast timing and aim-assist settings at release.
    /// Pure value data: no scene objects, component lookups, or allocations.
    /// </summary>
    public readonly struct AirCastReleaseRules
    {
        public readonly float MinimumFireTime;
        public readonly float PerfectTime;
        public readonly float PerfectLeeway;
        public readonly float FullTimingTime;
        public readonly float MinimumAimAssist;
        public readonly float ReadyAimAssist;

        public AirCastReleaseRules(
            float minimumFireTime, float perfectTime, float perfectLeeway,
            float fullTimingTime, float minimumAimAssist, float readyAimAssist)
        {
            MinimumFireTime = minimumFireTime;
            PerfectTime = perfectTime;
            PerfectLeeway = perfectLeeway;
            FullTimingTime = fullTimingTime;
            MinimumAimAssist = minimumAimAssist;
            ReadyAimAssist = readyAimAssist;
        }
    }

    public readonly struct AirCastReleaseDecision
    {
        public readonly bool Cancel;
        public readonly bool Perfect;
        public readonly bool Ready;
        public readonly float PerfectQuality;
        public readonly float Charge01;
        public readonly float AimAssist;

        public AirCastReleaseDecision(
            bool cancel, bool perfect, bool ready,
            float perfectQuality, float charge01, float aimAssist)
        {
            Cancel = cancel;
            Perfect = perfect;
            Ready = ready;
            PerfectQuality = perfectQuality;
            Charge01 = charge01;
            AimAssist = aimAssist;
        }
    }

    /// <summary>
    /// Deterministic cast release decision and aim-assist interpolation.
    /// AirCaster owns the target, events, and projectile spawning; this class
    /// owns only the numerical release decision. No MonoBehaviour needed.
    /// </summary>
    public static class AirCastReleasePolicy
    {
        public static AirCastReleaseDecision Evaluate(
            float chargeSeconds, float targetLockSeconds,
            bool hasTarget, bool sameTarget, AirCastReleaseRules rules)
        {
            float charge01 = rules.FullTimingTime > 0f
                ? Mathf.Clamp01(chargeSeconds / rules.FullTimingTime)
                : 0f;

            float effectiveTiming = sameTarget
                ? Mathf.Min(chargeSeconds, targetLockSeconds)
                : 0f;

            float perfectQuality = sameTarget
                ? EvaluatePerfectQuality(effectiveTiming, rules)
                : 0f;

            bool perfect = perfectQuality > 0f;
            bool normallyReady = sameTarget &&
                                 chargeSeconds >= rules.PerfectTime &&
                                 targetLockSeconds >= rules.PerfectTime;
            bool ready = normallyReady || perfect;
            bool cancel = chargeSeconds < rules.MinimumFireTime || !hasTarget;

            float accuracy;
            if (perfect)
            {
                accuracy = rules.ReadyAimAssist;
            }
            else
            {
                float airAccuracy01 = Mathf.InverseLerp(
                    rules.MinimumFireTime, rules.PerfectTime, chargeSeconds);

                float targetAccuracy01 = Mathf.InverseLerp(
                    0f, rules.PerfectTime,
                    sameTarget ? targetLockSeconds : 0f);

                float accuracy01 = Mathf.Min(airAccuracy01, targetAccuracy01);
                accuracy = Mathf.Lerp(
                    rules.MinimumAimAssist, rules.ReadyAimAssist, accuracy01);
            }

            return new AirCastReleaseDecision(
                cancel, perfect, ready, perfectQuality, charge01, accuracy);
        }

        private static float EvaluatePerfectQuality(
            float effectiveSeconds, AirCastReleaseRules rules)
        {
            float distance = Mathf.Abs(effectiveSeconds - rules.PerfectTime);
            if (distance > rules.PerfectLeeway)
                return 0f;

            return 1f - Mathf.Clamp01(
                distance / Mathf.Max(0.001f, rules.PerfectLeeway));
        }
    }
}
