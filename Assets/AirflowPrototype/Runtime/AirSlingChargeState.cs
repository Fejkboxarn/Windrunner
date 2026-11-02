using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Charge clock, perfect-window math and the pre-dash delay.
    /// No MonoBehaviour, input polling, scene state, feedback or Unity Time access.
    /// The controller retains all commit/cancel/event ordering decisions.
    /// </summary>
    public sealed class AirSlingChargeState
    {
        private float _chargeElapsed;
        private float _capturedPerfectQuality;
        private float _delayRemaining;

        public float ChargeElapsed => _chargeElapsed;
        public float CapturedPerfectQuality => _capturedPerfectQuality;
        public float DelayRemaining => _delayRemaining;

        public void BeginCharge()
        {
            _chargeElapsed = 0f;
            _capturedPerfectQuality = 0f;
        }

        public void AdvanceCharge(float deltaTime) => _chargeElapsed += deltaTime;

        public void CapturePerfectQuality(float quality) => _capturedPerfectQuality = quality;

        public void SetDelay(float seconds) => _delayRemaining = seconds;

        public void AdvanceDelay(float deltaTime) => _delayRemaining -= deltaTime;

        public void ClearDelay() => _delayRemaining = 0f;

        public void Reset()
        {
            _chargeElapsed = 0f;
            _capturedPerfectQuality = 0f;
            _delayRemaining = 0f;
        }

        public float EvaluatePerfectQuality(float perfectTime, float perfectLeeway)
        {
            float distance = Mathf.Abs(_chargeElapsed - perfectTime);
            if (distance > perfectLeeway)
                return 0f;

            return 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, perfectLeeway));
        }

        public float Progress01(float fullTimingTime)
        {
            return Mathf.Clamp01(_chargeElapsed / Mathf.Max(0.05f, fullTimingTime));
        }

        public static float PerfectCenter01(float fullTimingTime, float perfectTime)
        {
            return fullTimingTime > 0f
                ? Mathf.Clamp01(perfectTime / fullTimingTime)
                : 0.5f;
        }

        public static float PerfectHalfWidth01(float fullTimingTime, float perfectLeeway)
        {
            return fullTimingTime > 0f
                ? Mathf.Clamp(perfectLeeway / fullTimingTime, 0.001f, 0.49f)
                : 0.15f;
        }
    }
}
