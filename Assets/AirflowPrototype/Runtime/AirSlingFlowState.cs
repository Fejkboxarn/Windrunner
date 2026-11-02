using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// States owned by the sling flow model. Kept independent from animation,
    /// Input System, transforms, components, and C# gameplay event delivery.
    /// </summary>
    public enum AirSlingPhase
    {
        Idle,
        Charging,
        DelayBeforeDash,
        DashingToNode,
        ImpactHold
    }

    public enum AirSlingChargeCommand
    {
        Cancel,
        Accumulate,
        Release
    }

    /// <summary>
    /// Plain C# owner of sling phase transitions, charge/dash substate, and
    /// impact-hold state. The MonoBehaviour retains Unity-facing effects,
    /// physics and legacy public events in their existing call order.
    /// </summary>
    public sealed class AirSlingFlowState
    {
        private AirSlingPhase _phase = AirSlingPhase.Idle;
        private float _impactHoldRemaining;
        private Vector3 _impactRootPosition;

        public AirSlingChargeState Charge { get; } = new AirSlingChargeState();
        public AirSlingDashProgression Dash { get; } = new AirSlingDashProgression();

        public AirSlingPhase Phase => _phase;
        public bool IsCharging => _phase == AirSlingPhase.Charging;
        public bool IsSlinging => _phase != AirSlingPhase.Idle;
        public bool IsDashingToNode => _phase == AirSlingPhase.DashingToNode;
        public float ImpactHoldRemaining => _impactHoldRemaining;
        public Vector3 ImpactRootPosition => _impactRootPosition;
        // Keep the legacy <= 0 test rather than negating a > 0 check. Those
        // differ for NaN, which can appear in malformed external settings.
        public bool ShouldLaunchImmediately => _impactHoldRemaining <= 0f;

        // This preserves the legacy priority: missing target / input first,
        // then held RMB, then released (or no longer held).
        public AirSlingChargeCommand DecideCharging(
            bool targetAvailable, bool mouseAvailable, bool rightPressed, bool rightReleased)
        {
            if (!targetAvailable || !mouseAvailable)
                return AirSlingChargeCommand.Cancel;

            if (rightPressed)
                return AirSlingChargeCommand.Accumulate;

            if (rightReleased || !rightPressed)
                return AirSlingChargeCommand.Release;

            return AirSlingChargeCommand.Accumulate;
        }

        public void BeginCharge()
        {
            _phase = AirSlingPhase.Charging;
            Charge.BeginCharge();
        }

        public void BeginDelayBeforeDash(float delay)
        {
            Charge.SetDelay(delay);
            _phase = AirSlingPhase.DelayBeforeDash;
        }

        public void BeginDash(AirSlingSettings settings)
        {
            Dash.Begin(settings);
            _phase = AirSlingPhase.DashingToNode;
        }

        public void CaptureImpactRoot(Vector3 root) => _impactRootPosition = root;

        public void SetImpactHold(float duration) =>
            _impactHoldRemaining = Mathf.Max(0f, duration);

        // The legacy controller only enters ImpactHold when the hold > 0.
        // For zero hold, it launches immediately while still in DashingToNode.
        public void EnterImpactHold() => _phase = AirSlingPhase.ImpactHold;

        public bool AdvanceImpactHold(float unscaledDeltaTime)
        {
            _impactHoldRemaining -= unscaledDeltaTime;
            return _impactHoldRemaining <= 0f;
        }

        // Preserve perfect-quality data: the adapter reads it after cleanup
        // when emitting SlingLaunched and PerfectSlingLaunched.
        public void CompleteLaunch()
        {
            _phase = AirSlingPhase.Idle;
            Dash.ClearAfterLaunch();
            Charge.ClearDelay();
            _impactHoldRemaining = 0f;
        }

        public void Reset()
        {
            _phase = AirSlingPhase.Idle;
            Charge.Reset();
            Dash.Reset();
            _impactHoldRemaining = 0f;
            _impactRootPosition = Vector3.zero;
        }
    }
}
