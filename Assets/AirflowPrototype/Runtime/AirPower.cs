using System;
using UnityEngine;

namespace AirflowPrototype
{
    [Flags]
    public enum AirPowerRewardSource { AirNode = 1, SlingNode = 2 }

    // One delivery per earned return stream, even if a resource callback re-enters.
    public sealed class AirPowerReward
    {
        public readonly AirPowerRewardSource Source;
        bool delivered;
        public AirPowerReward(AirPowerRewardSource source) { Source = source; }
        internal bool Claim()
        {
            if (delivered) return false;
            delivered = true;
            return true;
        }
    }

    /// <summary>
    /// Existing serialized reference and public API preserved; game rules live in AirPowerState.
    /// Keep this component on the player while legacy systems still GetComponent<AirPower>().
    /// </summary>
    public sealed class AirPower : MonoBehaviour, IAirPowerBudget
    {
        [SerializeField] private AirflowMovementSettings settings;

        private AirPowerState _state;
        private AirPowerState State => _state ?? (_state = new AirPowerState());

        public void ReconcileCapacity()
        { if(settings!=null)State.ReconcileCapacity(settings.maxAirPower,settings.dashRestartFraction); }
        public float Current { get { ReconcileCapacity();return State.Current; } }
        public float Max => settings != null ? settings.maxAirPower : 0f;
        public float Normalized => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        public bool IsDepletedLocked => State.IsDepletedLocked;

        public bool CanStartDash =>
            settings != null &&
            !State.IsDepletedLocked &&
            Current > 0.001f;

        public event Action<float, float> Changed
        {
            add { State.Changed += value; }
            remove { State.Changed -= value; }
        }

        public event Action Depleted
        {
            add { State.Depleted += value; }
            remove { State.Depleted -= value; }
        }

        public event Action RecoveredFromDepletion
        {
            add { State.RecoveredFromDepletion += value; }
            remove { State.RecoveredFromDepletion -= value; }
        }

        public void Configure(AirflowMovementSettings newSettings, bool refill = true)
        {
            settings = newSettings;
            if (refill && settings != null)
                State.SetCurrent(settings.maxAirPower, settings.maxAirPower);
        }

        private void Awake()
        {
            if (settings != null && Current <= 0f)
                State.Initialize(settings.maxAirPower);
        }

        private void Update()
        {
            if (settings == null)
                return;

            State.Tick(
                Time.time,
                Time.deltaTime,
                settings.maxAirPower,
                settings.enablePassiveRecovery,
                settings.passiveRecoveryDelay,
                settings.passiveRecoveryPerSecond,
                settings.dashRestartFraction);
        }

        public bool Spend(float amount)
        {
            if (settings == null || amount <= 0f)
                return true;

            ReconcileCapacity();
            return State.Spend(amount, Time.time, settings.maxAirPower);
        }

        bool IAirPowerBudget.SpendAndHasRemaining(float amount) => Spend(amount);

        public event Action<AirPowerRewardSource,float> RewardOverflow;

        public void GrantReward(AirPowerReward reward, float amount)
        {
            if (!settings || reward == null || !(amount > 0) || float.IsInfinity(amount) || !reward.Claim()) return;
            float excess = State.RestoreReward(amount, settings.maxAirPower, settings.dashRestartFraction);
            if (excess > 0) RewardOverflow?.Invoke(reward.Source, excess);
        }

        public void Restore(float amount)
        {
            if (settings == null || amount <= 0f)
                return;

            State.Restore(amount, settings.maxAirPower, settings.dashRestartFraction);
        }

        public void Refill()
        {
            if (settings == null)
                return;

            State.Refill(settings.maxAirPower);
        }
    }
}
