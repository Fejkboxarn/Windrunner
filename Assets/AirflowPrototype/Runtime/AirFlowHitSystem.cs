using System;
using System.Collections;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirFlowHitSystem : MonoBehaviour
    {
        [SerializeField] private AirCaster caster;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirPower airPower;
        [SerializeField] private Camera referenceCamera;
        [SerializeField] private AirFlowHitSettings settings;

        public event Action<AirNode, float> ReturnStarted;
        public event Action<AirNode, float> RewardDelivered;

        public void Configure(
            AirCaster newCaster,
            PlayerMotor newMotor,
            AirPower newAirPower,
            Camera newReferenceCamera,
            AirFlowHitSettings newSettings)
        {
            Unsubscribe();

            caster = newCaster;
            motor = newMotor;
            airPower = newAirPower;
            referenceCamera = newReferenceCamera;
            settings = newSettings;

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (caster == null)
                caster = GetComponent<AirCaster>();

            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (airPower == null)
                airPower = GetComponent<AirPower>();

            if (referenceCamera == null)
                referenceCamera = Camera.main;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAllCoroutines();
        }

        private void Subscribe()
        {
            if (caster == null)
                return;

            caster.PulseHit -= OnPulseHit;
            caster.PulseHit += OnPulseHit;
        }

        private void Unsubscribe()
        {
            if (caster != null)
                caster.PulseHit -= OnPulseHit;
        }

        private void OnPulseHit(
            AirNode node,
            float charge01)
        {
            if (node == null ||
                motor == null ||
                airPower == null ||
                settings == null)
            {
                return;
            }

            float chargeIntensity =
                Mathf.Lerp(
                    settings.minimumChargeRewardMultiplier,
                    1f,
                    Mathf.Clamp01(charge01));

            float gameplayIntensity =
                chargeIntensity *
                node.RewardMultiplier;

            float presentationIntensity =
                Mathf.Clamp(
                    gameplayIntensity,
                    0f,
                    1.35f);

            node.BeginCooldown(
                settings.nodeTargetingCooldown);

            ReturnStarted?.Invoke(
                node,
                presentationIntensity);

            StartCoroutine(
                DeliverRewardAfterDelay(
                    node,
                    gameplayIntensity,
                    presentationIntensity,
                    Mathf.Max(
                        0.03f,
                        settings.returnStreamDuration)));
        }

        private IEnumerator DeliverRewardAfterDelay(
            AirNode node,
            float gameplayIntensity,
            float presentationIntensity,
            float delay)
        {
            yield return
                new WaitForSeconds(delay);

            if (motor == null ||
                airPower == null ||
                settings == null)
            {
                yield break;
            }

            airPower.Restore(
                settings.airPowerRestore *
                gameplayIntensity);

            Vector3 direction =
                GetBoostDirection();

            motor.AddRewardPlanarBoost(
                direction *
                (settings.planarBoostImpulse *
                 gameplayIntensity),
                settings.maxBoostedSpeed,
                settings.boostHoldDuration,
                settings.boostSpeedDecayPerSecond);

            if (!motor.IsGrounded)
            {
                motor.ApplyAerialFlowReward(
                    settings.risingLift *
                    gameplayIntensity,
                    settings.fallingLift *
                    gameplayIntensity,
                    settings.fallingVelocityCancelFraction,
                    settings.maxRewardUpwardSpeed,
                    settings.aerialFlowGravityScale,
                    settings.aerialFlowGravityDuration,
                    settings.aerialFlowSteeringMultiplier,
                    settings.aerialFlowSteeringDuration);
            }

            RewardDelivered?.Invoke(
                node,
                presentationIntensity);
        }

        private Vector3 GetBoostDirection()
        {
            Vector3 planarVelocity =
                motor.PlanarVelocity;

            planarVelocity.y = 0f;

            if (planarVelocity.magnitude >=
                settings.minimumTravelSpeedForDirection)
            {
                return
                    planarVelocity.normalized;
            }

            if (referenceCamera != null)
            {
                Vector3 cameraForward =
                    referenceCamera.transform.forward;

                cameraForward.y = 0f;

                if (cameraForward.sqrMagnitude >
                    0.0001f)
                {
                    return
                        cameraForward.normalized;
                }
            }

            Vector3 fallback =
                transform.forward;

            fallback.y = 0f;

            return
                fallback.sqrMagnitude > 0.0001f
                    ? fallback.normalized
                    : Vector3.forward;
        }
    }
}
