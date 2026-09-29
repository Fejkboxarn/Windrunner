using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Short real-time perfect-timing slowdown.
    /// UI continues on unscaled time while gameplay briefly dips.
    /// </summary>
    public sealed class AirPerfectTimingFeedback : MonoBehaviour
    {
        [SerializeField] private AirCaster caster;
        [SerializeField] private AirTargetUISettings settings;

        private bool _active;
        private float _elapsed;

        private float _normalTimeScale = 1f;
        private float _normalFixedDeltaTime = 0.02f;

        private float _activeTimeScale = 0.24f;
        private float _activeHoldDuration = 0.055f;
        private float _activeRecoveryDuration = 0.16f;

        public void Configure(
            AirCaster newCaster,
            AirTargetUISettings newSettings)
        {
            Unsubscribe();

            caster = newCaster;
            settings = newSettings;

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (caster == null)
                caster = GetComponent<AirCaster>();

            _normalTimeScale =
                Time.timeScale;

            _normalFixedDeltaTime =
                Time.fixedDeltaTime;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreTime();
        }

        private void Subscribe()
        {
            if (caster == null)
                return;

            caster.PerfectReleased -=
                OnPerfectReleased;

            caster.PerfectReleased +=
                OnPerfectReleased;
        }

        private void Unsubscribe()
        {
            if (caster != null)
            {
                caster.PerfectReleased -=
                    OnPerfectReleased;
            }
        }

        private void Update()
        {
            if (!_active ||
                settings == null)
            {
                return;
            }

            _elapsed +=
                Time.unscaledDeltaTime;

            float hold =
                Mathf.Max(
                    0f,
                    _activeHoldDuration);

            float recovery =
                Mathf.Max(
                    0.01f,
                    _activeRecoveryDuration);

            float desiredScale;

            if (_elapsed <= hold)
            {
                desiredScale =
                    _activeTimeScale;
            }
            else
            {
                float t =
                    Mathf.Clamp01(
                        (_elapsed - hold) /
                        recovery);

                float smooth =
                    t * t *
                    (3f -
                     2f * t);

                desiredScale =
                    Mathf.Lerp(
                        _activeTimeScale,
                        _normalTimeScale,
                        smooth);

                if (t >= 1f)
                {
                    RestoreTime();
                    return;
                }
            }

            ApplyTimeScale(
                desiredScale);
        }

        private void OnPerfectReleased(
            AirNode node,
            float quality)
        {
            TriggerPerfectFeedback(
                quality);
        }

        /// <summary>
        /// Reuses the exact normal-node perfect timing slowdown for other
        /// traversal systems without duplicating time-scale logic.
        /// </summary>
        public void TriggerPerfectFeedback(
            float quality = 1f)
        {
            if (settings == null)
                return;

            TriggerPerfectFeedback(
                quality,
                settings.perfectSlowHoldDuration,
                settings.perfectSlowRecoveryDuration,
                settings.perfectTimeScale);
        }

        /// <summary>
        /// Same perfect-timing slowdown, with per-event overrides for duration,
        /// recovery and time scale. Normal Air Nodes continue using the
        /// AirTargetUISettings values through the overload above.
        /// </summary>
        public void TriggerPerfectFeedback(
            float quality,
            float holdDuration,
            float recoveryDuration,
            float timeScale)
        {
            if (settings == null)
                return;

            if (!_active)
            {
                _normalTimeScale =
                    Time.timeScale;

                _normalFixedDeltaTime =
                    Time.fixedDeltaTime;
            }

            _elapsed = 0f;
            _active = true;

            _activeHoldDuration =
                Mathf.Max(
                    0f,
                    holdDuration);

            _activeRecoveryDuration =
                Mathf.Max(
                    0.01f,
                    recoveryDuration);

            float requestedScale =
                Mathf.Clamp(
                    timeScale,
                    0.01f,
                    1f);

            _activeTimeScale =
                Mathf.Lerp(
                    Mathf.Min(
                        1f,
                        requestedScale * 1.18f),
                    requestedScale,
                    Mathf.Clamp01(quality));

            ApplyTimeScale(
                _activeTimeScale);
        }

        private void ApplyTimeScale(
            float scale)
        {
            float safeScale =
                Mathf.Clamp(
                    scale,
                    0.01f,
                    Mathf.Max(
                        0.01f,
                        _normalTimeScale));

            Time.timeScale =
                safeScale;

            if (_normalTimeScale > 0.0001f)
            {
                float ratio =
                    safeScale /
                    _normalTimeScale;

                Time.fixedDeltaTime =
                    _normalFixedDeltaTime *
                    ratio;
            }
        }

        private void RestoreTime()
        {
            if (!_active)
                return;

            Time.timeScale =
                _normalTimeScale;

            Time.fixedDeltaTime =
                _normalFixedDeltaTime;

            _active = false;
            _elapsed = 0f;
        }
    }
}
