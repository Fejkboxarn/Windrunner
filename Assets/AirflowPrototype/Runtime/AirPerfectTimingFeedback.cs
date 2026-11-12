using UnityEngine;
using Windrunner.Production.Core;
using Windrunner.Production.Unity;

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

        private readonly HitStopEnvelope envelope = new HitStopEnvelope();
        private TimeScaleArbiter.Request slowdown;

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
                caster = GetComponentInParent<AirCaster>();

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
            if (!envelope.Active || settings == null) return;
            envelope.Advance(Time.unscaledDeltaTime, UnityTimeAuthority.IsPaused);
            if (!envelope.Active) RestoreTime();
            else slowdown?.SetScale(envelope.Scale);
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

            envelope.Start(quality, holdDuration, recoveryDuration, timeScale,
                UnityTimeAuthority.BaselineScale);
            if (slowdown == null) slowdown = UnityTimeAuthority.Slowdown(envelope.Scale);
            else slowdown.SetScale(envelope.Scale);
        }

        private void RestoreTime()
        {
            envelope.Stop();
            slowdown?.Dispose();
            slowdown = null;
        }
    }
}
