using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Sling dash speed envelope, path progress and elapsed time. Pure runtime state;
    /// no MonoBehaviour, CharacterController, scene references or Unity Time polling.
    /// Speed preview deliberately occurs before the controller moves, and progress
    /// is committed separately AFTER CharacterController.Move, preserving event order.
    /// </summary>
    public sealed class AirSlingDashProgression
    {
        private float _dashSpeed;
        private float _dashElapsed;
        private float _dashProgress;
        private float _dashPathDistance;

        public float Speed => _dashSpeed;
        public float Elapsed => _dashElapsed;
        public float Progress => _dashProgress;
        public float PathDistance => _dashPathDistance;

        public void SetPathDistance(float distance) => _dashPathDistance = distance;

        public void Begin(AirSlingSettings settings)
        {
            _dashProgress = 0f;
            float initialCurveSpeed = EvaluateDashSpeed(settings, 0f);
            _dashSpeed = Mathf.Max(
                settings.dashStartSpeed + settings.dashInitialKickSpeed,
                initialCurveSpeed);
            _dashElapsed = 0f;
        }

        public void AdvanceElapsed(float dt) => _dashElapsed += dt;

        public bool HasTimedOut(float maximumDashDuration) =>
            _dashElapsed >= maximumDashDuration;

        /// <summary>
        /// Updates dash speed and returns the next progress WITHOUT committing it.
        /// Caller must move the CharacterController before CommitProgress.
        /// </summary>
        public float PreviewNextProgress(float dt, AirSlingSettings settings)
        {
            float targetSpeed = EvaluateDashSpeed(settings, _dashProgress);
            float accelerationMultiplier =
                _dashProgress >= settings.dashArrivalAccelerationStart
                    ? settings.dashArrivalAccelerationMultiplier
                    : 1f;

            _dashSpeed = Mathf.MoveTowards(
                _dashSpeed,
                targetSpeed,
                settings.dashAcceleration * accelerationMultiplier * dt);

            float progressStep = (_dashSpeed * dt) / Mathf.Max(0.01f, _dashPathDistance);
            return Mathf.Clamp01(_dashProgress + progressStep);
        }

        public void CommitProgress(float nextProgress) => _dashProgress = nextProgress;

        public static float EvaluateDashSpeed(AirSlingSettings settings, float progress01)
        {
            float multiplier = settings.dashSpeedCurve != null
                ? settings.dashSpeedCurve.Evaluate(Mathf.Clamp01(progress01))
                : 1f;
            multiplier = Mathf.Max(0.05f, multiplier);
            return settings.dashMaximumSpeed * multiplier;
        }

        // Exact old CompleteImpactLaunch behavior: retain distance until full reset.
        public void ClearAfterLaunch()
        {
            _dashElapsed = 0f;
            _dashSpeed = 0f;
            _dashProgress = 0f;
        }

        public void Reset()
        {
            _dashSpeed = 0f;
            _dashElapsed = 0f;
            _dashProgress = 0f;
            _dashPathDistance = 0f;
        }
    }
}
