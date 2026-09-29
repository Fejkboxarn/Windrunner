using System;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirPower : MonoBehaviour
    {
        [SerializeField] private AirflowMovementSettings settings;

        private float _current;
        private float _lastSpendTime = float.NegativeInfinity;
        private bool _depletedLockout;

        public float Current => _current;
        public float Max => settings != null ? settings.maxAirPower : 0f;
        public float Normalized => Max > 0f ? Mathf.Clamp01(_current / Max) : 0f;
        public bool IsDepletedLocked => _depletedLockout;

        public bool CanStartDash =>
            settings != null &&
            !_depletedLockout &&
            _current > 0.001f;

        public event Action<float, float> Changed;
        public event Action Depleted;
        public event Action RecoveredFromDepletion;

        public void Configure(
            AirflowMovementSettings newSettings,
            bool refill = true)
        {
            settings = newSettings;

            if (refill && settings != null)
                SetCurrent(settings.maxAirPower);
        }

        private void Awake()
        {
            if (settings != null && _current <= 0f)
                _current = settings.maxAirPower;
        }

        private void Update()
        {
            if (settings == null)
                return;

            if (settings.enablePassiveRecovery &&
                Time.time - _lastSpendTime >= settings.passiveRecoveryDelay &&
                _current < settings.maxAirPower)
            {
                Restore(settings.passiveRecoveryPerSecond * Time.deltaTime);
            }

            if (_depletedLockout &&
                _current >= settings.maxAirPower * settings.dashRestartFraction)
            {
                _depletedLockout = false;
                RecoveredFromDepletion?.Invoke();
            }
        }

        public bool Spend(float amount)
        {
            if (settings == null || amount <= 0f)
                return true;

            if (_current <= 0f)
                return false;

            float before = _current;
            _current = Mathf.Max(0f, _current - amount);
            _lastSpendTime = Time.time;

            if (!Mathf.Approximately(before, _current))
                Changed?.Invoke(_current, settings.maxAirPower);

            if (_current <= 0f)
            {
                _depletedLockout = true;
                Depleted?.Invoke();
                return false;
            }

            return true;
        }

        public void Restore(float amount)
        {
            if (settings == null || amount <= 0f)
                return;

            bool wasLocked = _depletedLockout;

            SetCurrent(_current + amount);

            if (wasLocked &&
                _current >= settings.maxAirPower * settings.dashRestartFraction)
            {
                _depletedLockout = false;
                RecoveredFromDepletion?.Invoke();
            }
        }

        public void Refill()
        {
            if (settings == null)
                return;

            _depletedLockout = false;
            SetCurrent(settings.maxAirPower);
        }

        private void SetCurrent(float value)
        {
            if (settings == null)
                return;

            float next = Mathf.Clamp(value, 0f, settings.maxAirPower);

            if (Mathf.Approximately(next, _current))
                return;

            _current = next;
            Changed?.Invoke(_current, settings.maxAirPower);
        }
    }
}
