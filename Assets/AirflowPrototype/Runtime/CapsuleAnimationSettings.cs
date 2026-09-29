using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "CapsuleAnimationSettings",
        menuName = "Airflow Prototype/Capsule Animation Settings")]
    public sealed class CapsuleAnimationSettings : ScriptableObject
    {
        [Header("Idle Breathing")]
        [Min(0.01f)] public float breathingCyclesPerSecond = 0.30f;
        [Range(0f, 0.05f)] public float breathingScale = 0.012f;
        [Range(0f, 0.05f)] public float breathingBob = 0.008f;

        [Header("Walk / Run")]
        [Min(0f)] public float locomotionStartSpeed = 0.45f;
        [Min(1f)] public float fullRunAnimationSpeed = 18f;
        [Min(0.1f)] public float walkCyclesPerSecond = 1.30f;
        [Min(0.1f)] public float runCyclesPerSecond = 2.65f;
        [Range(0f, 0.18f)] public float walkRunBob = 0.055f;
        [Range(0f, 10f)] public float walkRunSideSwayDegrees = 2.2f;
        [Range(0f, 8f)] public float walkRunPitchDegrees = 2.0f;
        [Range(0f, 0.10f)] public float walkRunContactSquash = 0.016f;

        [Header("Jump / Fall")]
        [Min(0.02f)] public float takeoffSquashDuration = 0.10f;
        [Range(0f, 0.18f)] public float takeoffSquash = 0.075f;
        [Range(0f, 0.16f)] public float risingStretch = 0.040f;
        [Range(0f, 12f)] public float risingPitchDegrees = 3.5f;
        [Range(0f, 0.14f)] public float apexSquash = 0.024f;
        [Range(0f, 0.16f)] public float fallingStretch = 0.028f;
        [Range(0f, 12f)] public float fallingPitchDegrees = 3.0f;
        [Min(0.1f)] public float verticalSpeedForFullPose = 7f;

        [Header("Landing")]
        [Min(0.02f)] public float landingSquashDuration = 0.13f;
        [Range(0f, 0.25f)] public float landingSquash = 0.10f;
        [Range(0f, 0.12f)] public float landingRecoveryBounce = 0.025f;

        [Header("Air Reward Front Flip")]
        [Min(0.1f)] public float flipDuration = 0.54f;
        [Range(180f, 540f)] public float frontFlipDegrees = 360f;
        [Range(0f, 0.14f)] public float flipTuckSquash = 0.035f;
        [Min(0f)] public float minimumFlipRewardIntensity = 0.55f;
        [Range(0f, 1f)] public float earliestFlipRestartNormalizedTime = 0.72f;

        [Header("Pose Response")]
        [Min(0.01f)] public float positionResponse = 20f;
        [Min(0.01f)] public float scaleResponse = 20f;

        private void OnValidate()
        {
            fullRunAnimationSpeed =
                Mathf.Max(
                    locomotionStartSpeed + 0.1f,
                    fullRunAnimationSpeed);

            verticalSpeedForFullPose =
                Mathf.Max(
                    0.1f,
                    verticalSpeedForFullPose);

            flipDuration =
                Mathf.Max(
                    0.1f,
                    flipDuration);
        }
    }
}
