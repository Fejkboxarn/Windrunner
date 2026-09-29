using System;
using UnityEngine;

namespace AirflowPrototype
{
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(AirPower))]
    public sealed class AirDash : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private AirPower airPower;
        [SerializeField] private AirflowMovementSettings settings;
        [SerializeField] private AirFlowHitSystem flowHitSystem;

        private bool _isDashing;
        private bool _autoDashLatched;

        public bool IsDashing => _isDashing;
        public bool IsAutoDashLatched => _autoDashLatched;

        public event Action DashStarted;
        public event Action DashStopped;

        public void Configure(
            PlayerMotor newMotor,
            PlayerInputReader newInput,
            AirPower newAirPower,
            AirflowMovementSettings newSettings)
        {
            motor = newMotor;
            input = newInput;
            airPower = newAirPower;
            settings = newSettings;

            ResolveFlowHitSystem();
            ResubscribeToReward();
        }

        /// <summary>
        /// Starts reward-driven auto dash.
        /// It remains latched while the player keeps moving and has Air Power.
        /// </summary>
        public void TriggerAutoDash()
        {
            if (airPower == null || !airPower.CanStartDash)
                return;

            _autoDashLatched = true;
        }

        /// <summary>
        /// Explicitly cancels reward-driven auto dash.
        /// Manual hold-dash can still work normally afterward.
        /// </summary>
        public void CancelAutoDash()
        {
            _autoDashLatched = false;
        }

        private void Reset()
        {
            motor = GetComponent<PlayerMotor>();
            input = GetComponent<PlayerInputReader>();
            airPower = GetComponent<AirPower>();
            flowHitSystem = GetComponent<AirFlowHitSystem>();
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (input == null)
                input = GetComponent<PlayerInputReader>();

            if (airPower == null)
                airPower = GetComponent<AirPower>();

            ResolveFlowHitSystem();
        }

        private void OnEnable()
        {
            ResolveFlowHitSystem();
            SubscribeToReward();
        }

        private void OnDisable()
        {
            UnsubscribeFromReward();
            _autoDashLatched = false;
            SetDashing(false);
        }

        private void Update()
        {
            if (motor == null || input == null || airPower == null || settings == null)
            {
                _autoDashLatched = false;
                SetDashing(false);
                return;
            }

            float minimumInput = settings.minimumMoveInputToDash;
            bool hasMoveIntent =
                input.Move.sqrMagnitude >= minimumInput * minimumInput;

            // Releasing movement is an explicit request to end the reward dash.
            // Moving again later does not silently restart it.
            if (_autoDashLatched && !hasMoveIntent)
                _autoDashLatched = false;

            bool manualDashRequested =
                input.DashHeld && hasMoveIntent;

            bool autoDashRequested =
                _autoDashLatched && hasMoveIntent;

            bool wantsDash =
                manualDashRequested || autoDashRequested;

            if (!wantsDash)
            {
                SetDashing(false);
                return;
            }

            if (!_isDashing && !airPower.CanStartDash)
            {
                // Running out of Air Power permanently ends this auto-dash chain.
                _autoDashLatched = false;
                SetDashing(false);
                return;
            }

            float cost =
                settings.dashDrainPerSecond * Time.deltaTime;

            bool stillHasPower = airPower.Spend(cost);

            if (!stillHasPower)
                _autoDashLatched = false;

            SetDashing(stillHasPower);
        }

        private void ResolveFlowHitSystem()
        {
            if (flowHitSystem == null)
                flowHitSystem = GetComponent<AirFlowHitSystem>();
        }

        private void ResubscribeToReward()
        {
            UnsubscribeFromReward();

            if (isActiveAndEnabled)
                SubscribeToReward();
        }

        private void SubscribeToReward()
        {
            if (flowHitSystem == null)
                return;

            flowHitSystem.RewardDelivered -= OnRewardDelivered;
            flowHitSystem.RewardDelivered += OnRewardDelivered;
        }

        private void UnsubscribeFromReward()
        {
            if (flowHitSystem != null)
                flowHitSystem.RewardDelivered -= OnRewardDelivered;
        }

        private void OnRewardDelivered(AirNode node, float intensity)
        {
            TriggerAutoDash();
        }

        private void SetDashing(bool value)
        {
            if (_isDashing == value)
            {
                if (motor != null)
                    motor.SetDashActive(value);

                return;
            }

            _isDashing = value;

            if (motor != null)
                motor.SetDashActive(value);

            if (_isDashing)
                DashStarted?.Invoke();
            else
                DashStopped?.Invoke();
        }
    }
}
