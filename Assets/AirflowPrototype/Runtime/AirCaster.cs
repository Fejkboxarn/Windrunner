using System;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirCaster : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private AirTargeter targeter;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private AirCastSettings settings;

        private float _chargeTime;
        private bool _charging;

        private AirNode _trackedTarget;
        private float _targetLockTime;

        public bool IsCharging => _charging;
        public float ChargeTime => _chargeTime;

        public float Charge01 =>
            settings != null &&
            settings.fullTimingTime > 0f
                ? Mathf.Clamp01(_chargeTime / settings.fullTimingTime)
                : 0f;

        public float TargetLock01 =>
            settings != null &&
            _trackedTarget != null &&
            settings.fullTimingTime > 0f
                ? Mathf.Clamp01(_targetLockTime / settings.fullTimingTime)
                : 0f;

        public float EffectiveTimingSeconds =>
            settings != null
                ? settings.GetEffectiveTimingSeconds(
                    _chargeTime,
                    _targetLockTime,
                    _trackedTarget != null)
                : 0f;

        public float TimingProgress01 =>
            settings != null
                ? settings.GetTimingProgress01(
                    _chargeTime,
                    _targetLockTime,
                    _trackedTarget != null)
                : 0f;

        public bool IsAirReady =>
            settings != null &&
            _charging &&
            _chargeTime >= settings.perfectTime;

        public bool IsTargetReady =>
            settings != null &&
            _charging &&
            _trackedTarget != null &&
            _targetLockTime >= settings.perfectTime;

        public bool IsReady =>
            IsAirReady &&
            IsTargetReady;

        public float CombinedReady01
        {
            get
            {
                if (settings == null ||
                    !_charging ||
                    _trackedTarget == null)
                {
                    return 0f;
                }

                return Mathf.Clamp01(
                    EffectiveTimingSeconds /
                    Mathf.Max(0.01f, settings.perfectTime));
            }
        }

        /// <summary>
        /// Before charging, follows normal targeting priority.
        /// Once charging starts, the acquired target stays sticky while available.
        /// </summary>
        public AirNode CurrentTarget
        {
            get
            {
                if (_charging &&
                    IsTrackedTargetAvailable())
                {
                    return _trackedTarget;
                }

                return targeter != null
                    ? targeter.CurrentTarget
                    : null;
            }
        }

        public Transform CastOrigin => castOrigin;
        public AirCastSettings Settings => settings;

        public event Action ChargeStarted;
        public event Action<AirNode> TargetedChargeStarted;
        public event Action ChargeCanceled;
        public event Action<float, bool> PulseReleased;
        public event Action<AirNode, float, bool> TargetedPulseReleased;
        public event Action<AirNode, float> PulseHit;
        public event Action PulseMissed;
        public event Action<AirNode, float> PerfectReleased;

        public void Configure(
            PlayerInputReader newInput,
            AirTargeter newTargeter,
            Camera newCamera,
            Transform newCastOrigin,
            AirCastSettings newSettings)
        {
            input = newInput;
            targeter = newTargeter;
            targetCamera = newCamera;
            castOrigin = newCastOrigin;
            settings = newSettings;

            if (settings != null)
                settings.SynchronizeCompatibilityFields();
        }

        public void SetCastOrigin(Transform newCastOrigin)
        {
            castOrigin = newCastOrigin;
        }

        private void Awake()
        {
            if (input == null)
                input = GetComponent<PlayerInputReader>();

            if (targeter == null)
                targeter = GetComponent<AirTargeter>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (settings != null)
                settings.SynchronizeCompatibilityFields();

            ResolveCastOrigin();
        }

        private void ResolveCastOrigin()
        {
            if (castOrigin != null &&
                castOrigin != transform)
            {
                return;
            }

            Transform found =
                transform.Find("Air Cast Origin");

            if (found != null)
                castOrigin = found;
        }

        private void Update()
        {
            ResolveCastOrigin();

            if (input == null ||
                targeter == null ||
                targetCamera == null ||
                castOrigin == null ||
                settings == null)
            {
                return;
            }

            if (input.CastPressedThisFrame)
                BeginCharge();

            if (!_charging)
                return;

            if (input.CastHeld)
            {
                _chargeTime +=
                    Time.deltaTime;

                UpdateTargetLock(
                    Time.deltaTime);
            }

            if (!_charging)
                return;

            if (input.CastReleasedThisFrame)
                Release();
        }

        private void BeginCharge()
        {
            AirNode target =
                targeter != null
                    ? targeter.CurrentTarget
                    : null;

            // LMB is intentionally inert without a valid normal Air Node.
            // This keeps the action target-driven instead of behaving like
            // a free-firing projectile weapon.
            if (target == null ||
                !target.IsAvailable)
            {
                return;
            }

            _charging = true;
            _chargeTime = 0f;
            _targetLockTime = 0f;

            _trackedTarget =
                target;

            ChargeStarted?.Invoke();

            TargetedChargeStarted?.Invoke(
                _trackedTarget);
        }

        private void UpdateTargetLock(float dt)
        {
            // A charge is hard-locked to the node claimed on LMB press.
            // If that node disappears, cancel instead of jumping to another node.
            if (!IsTrackedTargetAvailable())
            {
                CancelCharge();
                return;
            }

            _targetLockTime +=
                dt;
        }

        private void CancelCharge()
        {
            if (!_charging)
                return;

            _charging = false;
            _chargeTime = 0f;

            ResetTargetLock();

            ChargeCanceled?.Invoke();
        }

        private bool IsTrackedTargetAvailable()
        {
            return
                _trackedTarget != null &&
                _trackedTarget.IsAvailable;
        }

        private void Release()
        {
            // CurrentTarget returns the sticky target while it is available.
            AirNode target =
                CurrentTarget;

            bool sameTarget =
                target != null &&
                target == _trackedTarget;

            // The value-only policy preserves the prototype's exact release
            // arithmetic. Scene interactions and events stay in this adapter.
            AirCastReleaseDecision decision = AirCastReleasePolicy.Evaluate(
                _chargeTime,
                _targetLockTime,
                target != null,
                sameTarget,
                new AirCastReleaseRules(
                    settings.minimumFireTime,
                    settings.perfectTime,
                    settings.perfectLeeway,
                    settings.fullTimingTime,
                    settings.minimumAimAssist,
                    settings.readyAimAssist));

            _charging = false;
            _chargeTime = 0f;

            if (decision.Cancel)
            {
                ResetTargetLock();
                ChargeCanceled?.Invoke();
                return;
            }

            Vector3 origin =
                castOrigin.position;

            Vector3 cameraForward =
                targetCamera.transform.forward.normalized;

            Vector3 directToTarget =
                (target.TargetPosition - origin).normalized;

            Vector3 launchDirection =
                Vector3.Slerp(
                    cameraForward,
                    directToTarget,
                    decision.AimAssist).normalized;

            AirPulseProjectile.Spawn(
                this,
                target,
                origin,
                launchDirection,
                decision.Charge01,
                decision.Ready,
                settings);

            TargetedPulseReleased?.Invoke(
                target,
                decision.Charge01,
                decision.Ready);

            PulseReleased?.Invoke(
                decision.Charge01,
                decision.Ready);

            ResetTargetLock();

            if (decision.Perfect)
            {
                PerfectReleased?.Invoke(
                    target,
                    decision.PerfectQuality);
            }
        }

        private void ResetTargetLock()
        {
            _trackedTarget = null;
            _targetLockTime = 0f;
        }

        internal void NotifyPulseHit(
            AirNode node,
            float charge01)
        {
            if (node == null)
                return;

            AirNodePulseFeedback feedback =
                node.GetComponent<AirNodePulseFeedback>();

            if (feedback != null)
            {
                feedback.PlayHit(
                    Mathf.Lerp(
                        0.65f,
                        1f,
                        charge01));
            }

            PulseHit?.Invoke(
                node,
                charge01);
        }

        internal void NotifyPulseMiss()
        {
            PulseMissed?.Invoke();
        }

        private void OnDisable()
        {
            CancelCharge();
        }
    }
}
