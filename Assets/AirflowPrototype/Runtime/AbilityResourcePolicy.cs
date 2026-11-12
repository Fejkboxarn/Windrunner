namespace AirflowPrototype
{
    /// <summary>Per-player resource boundary; spending returns remaining-power state, not payment success.</summary>
    public interface IAirPowerBudget
    {
        float Current { get; }
        bool CanStartDash { get; }
        bool SpendAndHasRemaining(float amount);
    }

    public static class AbilityResourcePolicy
    {
        public static bool TryCommitSling(IAirPowerBudget power, bool enabled, bool requireEnough, float cost)
        {
            if (!enabled || cost <= 0f) return true;
            if (power == null) return !requireEnough;
            if (requireEnough && power.Current < cost) return false;
            // Exact depletion is a successful Sling payment. Legacy Spend's
            // false means empty afterwards, not that the transaction failed.
            power.SpendAndHasRemaining(cost);
            return true;
        }
    }
}
