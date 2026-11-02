using System;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Runtime air-resource rules, independent of scene objects and Unity lifecycle.
    /// The AirPower component is the compatibility facade used by existing systems.
    /// </summary>
    public sealed class AirPowerState
    {
        private float _current;
        private float _lastSpendTime = float.NegativeInfinity;
        private bool _depletedLockout;

        public float Current => _current;
        public bool IsDepletedLocked => _depletedLockout;

        public event Action<float, float> Changed;
        public event Action Depleted;
        public event Action RecoveredFromDepletion;

        // Matches the old AirPower.Awake: initial fill deliberately does not emit Changed.
        public void Initialize(float max)
        {
            if (_current <= 0f)
                _current = max;
        }

        public void Tick(
            float now,
            float deltaTime,
            float max,
            bool passiveRecoveryEnabled,
            float passiveRecoveryDelay,
            float passiveRecoveryPerSecond,
            float dashRestartFraction)
        {
            if (passiveRecoveryEnabled &&
                now - _lastSpendTime >= passiveRecoveryDelay &&
                _current < max)
            {
                Restore(passiveRecoveryPerSecond * deltaTime, max, dashRestartFraction);
            }

            if (_depletedLockout && _current >= max * dashRestartFraction)
            {
                _depletedLockout = false;
                RecoveredFromDepletion?.Invoke();
            }
        }

        public bool Spend(float amount, float now, float max)
        {
            if (amount <= 0f)
                return true;

            if (_current <= 0f)
                return false;

            float before = _current;
            _current = Mathf.Max(0f, _current - amount);
            _lastSpendTime = now;

            if (!Mathf.Approximately(before, _current))
                Changed?.Invoke(_current, max);

            if (_current <= 0f)
            {
                _depletedLockout = true;
                Depleted?.Invoke();
                return false;
            }

            return true;
        }

        public void Restore(float amount, float max, float dashRestartFraction)
        {
            if (amount <= 0f)
                return;

            bool wasLocked = _depletedLockout;
            SetCurrent(_current + amount, max);

            if (wasLocked && _current >= max * dashRestartFraction)
            {
                _depletedLockout = false;
                RecoveredFromDepletion?.Invoke();
            }
        }

        public void Refill(float max)
        {
            _depletedLockout = false;
            SetCurrent(max, max);
        }

        // Also used by Configure(refill:true), which must NOT reset depletion lockout.
        public void SetCurrent(float value, float max)
        {
            float next = Mathf.Clamp(value, 0f, max);
            if (Mathf.Approximately(next, _current))
                return;

            _current = next;
            Changed?.Invoke(_current, max);
        }
    }
}
