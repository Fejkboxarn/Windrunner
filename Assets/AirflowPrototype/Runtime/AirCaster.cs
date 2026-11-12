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

        private readonly AirCastSession<AirNode> session = new AirCastSession<AirNode>();
        private bool wasPaused;
        private int chargeInputVersion;

        public bool IsCharging => session.IsCharging;
        public float ChargeTime => session.ChargeTime;

        public float Charge01 =>
            settings != null &&
            settings.fullTimingTime > 0f
                ? Mathf.Clamp01(session.ChargeTime / settings.fullTimingTime)
                : 0f;

        public float TargetLock01 =>
            settings != null &&
            session.Target != null &&
            settings.fullTimingTime > 0f
                ? Mathf.Clamp01(session.TargetLockTime / settings.fullTimingTime)
                : 0f;

        public float EffectiveTimingSeconds =>
            settings != null
                ? settings.GetEffectiveTimingSeconds(
                    session.ChargeTime,
                    session.TargetLockTime,
                    session.Target != null)
                : 0f;

        public float TimingProgress01 =>
            settings != null
                ? settings.GetTimingProgress01(
                    session.ChargeTime,
                    session.TargetLockTime,
                    session.Target != null)
                : 0f;

        public bool IsAirReady =>
            settings != null &&
            session.IsCharging &&
            session.ChargeTime >= settings.perfectTime;

        public bool IsTargetReady =>
            settings != null &&
            session.IsCharging &&
            session.Target != null &&
            session.TargetLockTime >= settings.perfectTime;

        public bool IsReady =>
            IsAirReady &&
            IsTargetReady;

        public float CombinedReady01
        {
            get
            {
                if (settings == null ||
                    !session.IsCharging ||
                    session.Target == null)
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
                if (session.IsCharging &&
                    IsTrackedTargetAvailable())
                {
                    return session.Target;
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
            {
                var owner = GetComponent<PlayerMotor>();
                targetCamera = owner != null ? owner.ViewCamera : Camera.main;
            }

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
            if (Windrunner.Production.Unity.UnityTimeAuthority.IsPaused) { wasPaused = true; return; }
            ResolveCastOrigin();

            if (input == null ||
                targeter == null ||
                targetCamera == null ||
                castOrigin == null ||
                settings == null)
            {
                return;
            }

            if (wasPaused)
            {
                wasPaused = false;
                if (session.IsCharging && !input.CastHeld) CancelCharge();
            }

            if (session.IsCharging && chargeInputVersion != input.InterruptionVersion)
            {
                CancelCharge();
                return;
            }

            if (input.CastPressedThisFrame)
                BeginCharge();

            if (!session.IsCharging)
                return;

            if (input.CastHeld)
            {
                if (!session.Advance(Time.deltaTime, IsTrackedTargetAvailable())) CancelCharge();
            }

            if (!session.IsCharging)
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

            session.Begin(target);
            chargeInputVersion = input.InterruptionVersion;

            ChargeStarted?.Invoke();

            TargetedChargeStarted?.Invoke(
                session.Target);
        }

        private void CancelCharge()
        {
            if (session.Cancel()) ChargeCanceled?.Invoke();
        }

        private bool IsTrackedTargetAvailable()
        {
            return
                session.Target != null &&
                session.Target.IsAvailable;
        }

        private void Release()
        {
            // CurrentTarget returns the sticky target while it is available.
            AirNode target =
                CurrentTarget;

            bool sameTarget =
                target != null &&
                target == session.Target;

            // The value-only policy preserves the prototype's exact release
            // arithmetic. Scene interactions and events stay in this adapter.
            AirCastReleaseDecision decision = session.Release(
                target != null,
                sameTarget,
                new AirCastReleaseRules(
                    settings.minimumFireTime,
                    settings.perfectTime,
                    settings.perfectLeeway,
                    settings.fullTimingTime,
                    settings.minimumAimAssist,
                    settings.readyAimAssist));

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
            session.ResetTargetLock();
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
