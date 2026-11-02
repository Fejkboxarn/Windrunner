using System;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Existing serialized reference and public API preserved; game rules live in AirPowerState.
    /// Keep this component on the player while legacy systems still GetComponent<AirPower>().
    /// </summary>
    public sealed class AirPower : MonoBehaviour
    {
        [SerializeField] private AirflowMovementSettings settings;

        private AirPowerState _state;
        private AirPowerState State => _state ?? (_state = new AirPowerState());

        public float Current => State.Current;
        public float Max => settings != null ? settings.maxAirPower : 0f;
        public float Normalized => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        public bool IsDepletedLocked => State.IsDepletedLocked;

        public bool CanStartDash =>
            settings != null &&
            !State.IsDepletedLocked &&
            State.Current > 0.001f;

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

            return State.Spend(amount, Time.time, settings.maxAirPower);
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
