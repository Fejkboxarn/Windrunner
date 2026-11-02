namespace AirflowPrototype
{
    /// <summary>
    /// Plain C# owner of manual/reward dash intent, auto-dash latching and
    /// the active state. No input actions, scene objects, or Unity callbacks.
    /// The AirDash adapter still owns AirPower.Spend and gameplay events.
    /// </summary>
    public sealed class AirDashSession
    {
        public bool IsDashing { get; private set; }
        public bool IsAutoDashLatched { get; private set; }

        public void TryLatchReward(bool canStartDash)
        {
            if (canStartDash)
                IsAutoDashLatched = true;
        }

        public void CancelAutoDash()
        {
            IsAutoDashLatched = false;
        }

        /// <summary>
        /// Evaluates intent before any AirPower check or drain. A false return
        /// means no drain should occur and the adapter should end the dash.
        /// </summary>
        public bool WantsDash(bool hasMoveIntent, bool dashHeld)
        {
            // Releasing movement permanently clears reward-dash intent;
            // resuming movement must not silently relatch it.
            if (IsAutoDashLatched && !hasMoveIntent)
                IsAutoDashLatched = false;

            bool manualDashRequested = dashHeld && hasMoveIntent;
            bool autoDashRequested = IsAutoDashLatched && hasMoveIntent;
            bool wantsDash = manualDashRequested || autoDashRequested;

            return wantsDash;
        }

        /// <summary>
        /// Only used when trying to begin a new dash. AirPower.CanStartDash
        /// is intentionally not queried when no dash is wanted or already
        /// dashing, matching the prototype's call order.
        /// </summary>
        public bool CanBeginDash(bool canStartDash)
        {
            if (!canStartDash)
            {
                IsAutoDashLatched = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Called exactly once after an attempted Spend. Clearing the latch
        /// here preserves the existing low-power auto-dash behavior.
        /// </summary>
        public void AfterSpend(bool stillHasPower)
        {
            if (!stillHasPower)
                IsAutoDashLatched = false;
        }

        /// <returns>True only for a dash state transition.</returns>
        public bool SetDashing(bool value)
        {
            if (IsDashing == value)
                return false;

            IsDashing = value;
            return true;
        }
    }
}
