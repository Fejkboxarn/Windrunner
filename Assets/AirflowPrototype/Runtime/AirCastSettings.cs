using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirCastSettings",
        menuName = "Airflow Prototype/Air Cast Settings")]
    public sealed class AirCastSettings : ScriptableObject
    {
        [Header("Core Timing - Tune These")]
        [Tooltip("The ideal release time after acquiring/holding a target.")]
        [Min(0.02f)]
        public float perfectTime = 0.30f;

        [Tooltip("When the charge reaches the innermost/full-lock point.")]
        [Min(0.05f)]
        public float fullTimingTime = 0.60f;

        [Tooltip("Perfect timing forgiveness on EACH side of Perfect Time. Example: 0.09 means Perfect Time ±0.09 seconds.")]
        [Range(0.01f, 0.25f)]
        public float perfectLeeway = 0.09f;

        [Header("Release")]
        [Tooltip("Very early taps below this value cancel instead of firing.")]
        [Min(0f)]
        public float minimumFireTime = 0.07f;

        [Header("Aim Assist")]
        [Range(0f, 1f)]
        public float minimumAimAssist = 0.25f;

        [Range(0f, 1f)]
        public float readyAimAssist = 1f;

        [Header("Pulse")]
        [Min(0.1f)]
        public float pulseSpeed = 38f;

        [Min(0.1f)]
        public float pulseLifetime = 1.5f;

        [Min(0.05f)]
        public float nodeHitRadius = 0.75f;

        [Header("Pulse Trail")]
        [Min(0.01f)]
        public float trailLifetime = 0.18f;

        [Min(0.001f)]
        public float trailStartWidth = 0.10f;

        [Min(0.001f)]
        public float trailEndWidth = 0.015f;

        // ------------------------------------------------------------------
        // COMPATIBILITY
        // ------------------------------------------------------------------
        // Existing scripts/assets from earlier batches reference these names.
        // They are now derived from the three core timing values above.
        // Do not tune these directly.
        // ------------------------------------------------------------------

        [HideInInspector]
        public float readyTime = 0.30f;

        [HideInInspector]
        public float fullChargeTime = 0.60f;

        [HideInInspector]
        public float targetReadyTime = 0.30f;

        [HideInInspector]
        public float targetFullTime = 0.60f;

        [HideInInspector]
        public float perfectTimingCenter = 0.50f;

        [HideInInspector]
        public float perfectTimingHalfWidth = 0.15f;

        [HideInInspector]
        public bool perfectRequiresAirReady = false;

        [Header("Legacy Charge Ring (Compatibility)")]
        [Min(0.01f)]
        public float unchargedRingRadius = 0.72f;

        [Min(0.01f)]
        public float chargedRingRadius = 0.20f;

        public float ringRotationSpeed = 110f;

        [Min(0.001f)]
        public float ringWidth = 0.025f;

        public float PerfectStartTime =>
            Mathf.Max(
                minimumFireTime,
                perfectTime - perfectLeeway);

        public float PerfectEndTime =>
            perfectTime + perfectLeeway;

        public float PerfectCenter01 =>
            fullTimingTime > 0f
                ? Mathf.Clamp01(
                    perfectTime /
                    fullTimingTime)
                : 0.5f;

        public float PerfectHalfWidth01 =>
            fullTimingTime > 0f
                ? Mathf.Clamp(
                    perfectLeeway /
                    fullTimingTime,
                    0.001f,
                    0.49f)
                : 0.1f;

        public float GetTimingProgress01(
            float chargeSeconds,
            float targetLockSeconds,
            bool hasTarget)
        {
            if (!hasTarget ||
                fullTimingTime <= 0f)
            {
                return 0f;
            }

            float effectiveSeconds =
                Mathf.Min(
                    chargeSeconds,
                    targetLockSeconds);

            return Mathf.Clamp01(
                effectiveSeconds /
                fullTimingTime);
        }

        public float GetEffectiveTimingSeconds(
            float chargeSeconds,
            float targetLockSeconds,
            bool hasTarget)
        {
            if (!hasTarget)
                return 0f;

            return Mathf.Min(
                chargeSeconds,
                targetLockSeconds);
        }

        public float EvaluatePerfectQuality(
            float effectiveSeconds)
        {
            float distance =
                Mathf.Abs(
                    effectiveSeconds -
                    perfectTime);

            if (distance >
                perfectLeeway)
            {
                return 0f;
            }

            return
                1f -
                Mathf.Clamp01(
                    distance /
                    Mathf.Max(
                        0.001f,
                        perfectLeeway));
        }

        public void SynchronizeCompatibilityFields()
        {
            perfectTime =
                Mathf.Max(
                    minimumFireTime,
                    perfectTime);

            fullTimingTime =
                Mathf.Max(
                    perfectTime + 0.01f,
                    fullTimingTime);

            perfectLeeway =
                Mathf.Clamp(
                    perfectLeeway,
                    0.01f,
                    Mathf.Max(
                        0.01f,
                        fullTimingTime * 0.45f));

            readyTime =
                perfectTime;

            fullChargeTime =
                fullTimingTime;

            targetReadyTime =
                perfectTime;

            targetFullTime =
                fullTimingTime;

            perfectTimingCenter =
                PerfectCenter01;

            perfectTimingHalfWidth =
                PerfectHalfWidth01;

            perfectRequiresAirReady =
                false;
        }

        private void OnEnable()
        {
            // Migration safety for an existing asset receiving the new fields.
            if (perfectTime <= 0f)
            {
                perfectTime =
                    targetReadyTime > 0f
                        ? targetReadyTime
                        : readyTime > 0f
                            ? readyTime
                            : 0.30f;
            }

            if (fullTimingTime <= 0f)
            {
                fullTimingTime =
                    targetFullTime > 0f
                        ? targetFullTime
                        : fullChargeTime > 0f
                            ? fullChargeTime
                            : 0.60f;
            }

            if (perfectLeeway <= 0f)
            {
                perfectLeeway =
                    perfectTimingHalfWidth > 0f
                        ? perfectTimingHalfWidth *
                          fullTimingTime
                        : 0.09f;
            }

            SynchronizeCompatibilityFields();
        }

        private void OnValidate()
        {
            minimumFireTime =
                Mathf.Max(
                    0f,
                    minimumFireTime);

            pulseSpeed =
                Mathf.Max(
                    0.1f,
                    pulseSpeed);

            pulseLifetime =
                Mathf.Max(
                    0.1f,
                    pulseLifetime);

            nodeHitRadius =
                Mathf.Max(
                    0.05f,
                    nodeHitRadius);

            trailLifetime =
                Mathf.Max(
                    0.01f,
                    trailLifetime);

            trailStartWidth =
                Mathf.Max(
                    0.001f,
                    trailStartWidth);

            trailEndWidth =
                Mathf.Max(
                    0.001f,
                    trailEndWidth);

            unchargedRingRadius =
                Mathf.Max(
                    0.01f,
                    unchargedRingRadius);

            chargedRingRadius =
                Mathf.Max(
                    0.01f,
                    chargedRingRadius);

            ringWidth =
                Mathf.Max(
                    0.001f,
                    ringWidth);

            SynchronizeCompatibilityFields();
        }
    }
}
