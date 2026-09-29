using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirAudioSettings",
        menuName = "Airflow Prototype/Air Audio Settings")]
    public sealed class AirAudioSettings : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private int audioTuningVersion;

        [Header("Master")]
        [Tooltip("Disables the entire prototype audio system when off.")]
        public bool audioEnabled = true;

        [Header("Enabled - Movement")]
        public bool footstepEnabled = true;
        public bool jumpEnabled = true;
        public bool landingEnabled = true;
        public bool flipWhooshEnabled = true;
        public bool dashStartEnabled = true;
        public bool dashWindLoopEnabled = true;

        [Header("Enabled - Air Cast")]
        public bool chargeLoopEnabled = true;
        public bool targetReadyEnabled = true;
        public bool castReleaseEnabled = true;
        public bool nodeHitEnabled = true;
        public bool returnLaunchEnabled = true;
        public bool rewardArrivalEnabled = true;
        public bool airPowerDepletedEnabled = true;

        [Header("Enabled - World")]
        public bool ambienceEnabled = true;
        public bool musicEnabled = true;

        [Header("Clips - Movement")]
        public AudioClip footstep;
        public AudioClip jump;
        public AudioClip landing;
        public AudioClip flipWhoosh;
        public AudioClip dashStart;
        public AudioClip dashWindLoop;

        [Header("Clips - Air Cast")]
        public AudioClip chargeLoop;
        public AudioClip targetReady;
        public AudioClip castRelease;
        public AudioClip nodeHit;
        public AudioClip returnLaunch;
        public AudioClip rewardArrival;
        public AudioClip airPowerDepleted;

        [Header("Clips - World")]
        public AudioClip ambienceLoop;
        public AudioClip musicLoop;

        [Header("Global Mix")]
        public bool useProceduralFallbacks = true;

        [Range(0f, 1f)]
        public float masterVolume = 0.82f;

        [Range(0f, 1f)]
        public float movementVolume = 0.65f;

        [Range(0f, 1f)]
        public float abilityVolume = 0.78f;

        [Range(0f, 1f)]
        public float ambienceVolume = 0.20f;

        [Range(0f, 1f)]
        public float musicVolume = 0.16f;

        [Header("Footstep")]
        [Range(0f, 2f)] public float footstepVolume = 1f;
        [Range(0.5f, 1.5f)] public float footstepPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float footstepPitchMax = 1f;

        [Header("Jump")]
        [Range(0f, 2f)] public float jumpVolume = 1f;
        [Range(0.5f, 1.5f)] public float jumpPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float jumpPitchMax = 1f;

        [Header("Landing")]
        [Range(0f, 2f)] public float landingVolume = 1f;
        [Range(0.5f, 1.5f)] public float landingPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float landingPitchMax = 1f;

        [Header("Flip Whoosh")]
        [Range(0f, 2f)] public float flipWhooshVolume = 1f;
        [Range(0.5f, 1.5f)] public float flipWhooshPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float flipWhooshPitchMax = 1f;

        [Header("Dash Start")]
        [Range(0f, 2f)] public float dashStartVolume = 1f;
        [Range(0.5f, 1.5f)] public float dashStartPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float dashStartPitchMax = 1f;

        [Header("Target Ready")]
        [Range(0f, 2f)] public float targetReadyVolume = 1f;
        [Range(0.5f, 1.5f)] public float targetReadyPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float targetReadyPitchMax = 1f;

        [Header("Cast Release")]
        [Range(0f, 2f)] public float castReleaseVolume = 1f;
        [Range(0.5f, 1.5f)] public float castReleasePitchMin = 1f;
        [Range(0.5f, 1.5f)] public float castReleasePitchMax = 1f;

        [Header("Node Hit")]
        [Range(0f, 2f)] public float nodeHitVolume = 1f;
        [Range(0.5f, 1.5f)] public float nodeHitPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float nodeHitPitchMax = 1f;

        [Header("Return Launch")]
        [Range(0f, 2f)] public float returnLaunchVolume = 1f;
        [Range(0.5f, 1.5f)] public float returnLaunchPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float returnLaunchPitchMax = 1f;

        [Header("Reward Arrival")]
        [Range(0f, 2f)] public float rewardArrivalVolume = 1f;
        [Range(0.5f, 1.5f)] public float rewardArrivalPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float rewardArrivalPitchMax = 1f;

        [Header("Air Power Depleted")]
        [Range(0f, 2f)] public float airPowerDepletedVolume = 1f;
        [Range(0.5f, 1.5f)] public float airPowerDepletedPitchMin = 1f;
        [Range(0.5f, 1.5f)] public float airPowerDepletedPitchMax = 1f;

        [Header("Dash Wind Loop")]
        [Min(0f)] public float windStartSpeed = 8.5f;
        [Min(0.01f)] public float windFullSpeed = 19f;
        [Range(0f, 1f)] public float windMaxVolume = 0.42f;
        [Range(0.25f, 2f)] public float windMinPitch = 0.82f;
        [Range(0.25f, 2f)] public float windMaxPitch = 1.22f;

        [Header("Charge Loop")]
        [Range(0f, 1f)] public float chargeMaxVolume = 0.48f;
        [Range(0.25f, 2f)] public float chargeStartPitch = 0.78f;
        [Range(0.25f, 2f)] public float chargeReadyPitch = 1.22f;

        [Header("Ambience")]
        [Range(0.25f, 2f)] public float ambiencePitch = 1f;

        [Header("Music")]
        [Range(0.25f, 2f)] public float musicPitch = 1f;

        [Header("Legacy General Pitch")]
        [Tooltip("Kept for compatibility. Per-sound pitch controls above are now preferred.")]
        [Range(0f, 0.35f)]
        public float pitchVariation = 0.07f;

        private void OnEnable()
        {
            MigrateIfNeeded();
        }

        private void OnValidate()
        {
            MigrateIfNeeded();

            NormalizeRange(ref footstepPitchMin, ref footstepPitchMax);
            NormalizeRange(ref jumpPitchMin, ref jumpPitchMax);
            NormalizeRange(ref landingPitchMin, ref landingPitchMax);
            NormalizeRange(ref flipWhooshPitchMin, ref flipWhooshPitchMax);
            NormalizeRange(ref dashStartPitchMin, ref dashStartPitchMax);

            NormalizeRange(ref targetReadyPitchMin, ref targetReadyPitchMax);
            NormalizeRange(ref castReleasePitchMin, ref castReleasePitchMax);
            NormalizeRange(ref nodeHitPitchMin, ref nodeHitPitchMax);
            NormalizeRange(ref returnLaunchPitchMin, ref returnLaunchPitchMax);
            NormalizeRange(ref rewardArrivalPitchMin, ref rewardArrivalPitchMax);
            NormalizeRange(ref airPowerDepletedPitchMin, ref airPowerDepletedPitchMax);

            windFullSpeed =
                Mathf.Max(
                    windStartSpeed + 0.01f,
                    windFullSpeed);
        }

        private void MigrateIfNeeded()
        {
            if (audioTuningVersion < 1)
            {
                footstepVolume = 1f;
                footstepPitchMin = 1f;
                footstepPitchMax = 1f;

                jumpVolume = 1f;
                jumpPitchMin = 1f;
                jumpPitchMax = 1f;

                landingVolume = 1f;
                landingPitchMin = 1f;
                landingPitchMax = 1f;

                flipWhooshVolume = 1f;
                flipWhooshPitchMin = 1f;
                flipWhooshPitchMax = 1f;

                dashStartVolume = 1f;
                dashStartPitchMin = 1f;
                dashStartPitchMax = 1f;
            }

            if (audioTuningVersion < 2)
            {
                targetReadyVolume = 1f;
                targetReadyPitchMin = 1f;
                targetReadyPitchMax = 1f;

                castReleaseVolume = 1f;
                castReleasePitchMin = 1f;
                castReleasePitchMax = 1f;

                nodeHitVolume = 1f;
                nodeHitPitchMin = 1f;
                nodeHitPitchMax = 1f;

                returnLaunchVolume = 1f;
                returnLaunchPitchMin = 1f;
                returnLaunchPitchMax = 1f;

                rewardArrivalVolume = 1f;
                rewardArrivalPitchMin = 1f;
                rewardArrivalPitchMax = 1f;

                airPowerDepletedVolume = 1f;
                airPowerDepletedPitchMin = 1f;
                airPowerDepletedPitchMax = 1f;

                ambiencePitch = 1f;
                musicPitch = 1f;
            }

            audioTuningVersion = 2;
        }

        private static void NormalizeRange(
            ref float min,
            ref float max)
        {
            min = Mathf.Clamp(min, 0.5f, 1.5f);
            max = Mathf.Clamp(max, 0.5f, 1.5f);

            if (max < min)
            {
                float swap = min;
                min = max;
                max = swap;
            }
        }
    }
}
