namespace AirflowPrototype
{
    /// <summary>Sticky target/charge ownership with explicit availability and clocks.</summary>
    public sealed class AirCastSession<TTarget> where TTarget : class
    {
        public bool IsCharging { get; private set; }
        public float ChargeTime { get; private set; }
        public float TargetLockTime { get; private set; }
        public TTarget Target { get; private set; }
        public void Begin(TTarget target)
        {
            IsCharging = true;
            ChargeTime = TargetLockTime = 0f;
            Target = target;
        }
        public bool Advance(float dt, bool targetAvailable)
        {
            ChargeTime += dt;
            if (!targetAvailable) return false;
            TargetLockTime += dt;
            return true;
        }
        public bool Cancel()
        {
            if (!IsCharging) return false;
            IsCharging = false;
            ChargeTime = 0f;
            ResetTargetLock();
            return true;
        }
        public AirCastReleaseDecision Release(bool hasTarget, bool sameTarget, AirCastReleaseRules rules)
        {
            var decision = AirCastReleasePolicy.Evaluate(ChargeTime, TargetLockTime, hasTarget, sameTarget, rules);
            IsCharging = false;
            ChargeTime = 0f;
            // Adapter resets the target AFTER release events, preserving what
            // subscribers observe during TargetedPulseReleased/PulseReleased.
            return decision;
        }
        public void ResetTargetLock() { Target = null; TargetLockTime = 0f; }
    }
}
