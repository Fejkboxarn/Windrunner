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

        private AirDashSession _session;
        private AirDashSession Session => _session ?? (_session = new AirDashSession());

        public bool IsDashing => Session.IsDashing;
        public bool IsAutoDashLatched => Session.IsAutoDashLatched;

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
            Session.TryLatchReward(airPower != null && airPower.CanStartDash);
        }

        /// <summary>
        /// Explicitly cancels reward-driven auto dash.
        /// Manual hold-dash can still work normally afterward.
        /// </summary>
        public void CancelAutoDash()
        {
            Session.CancelAutoDash();
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
            Session.CancelAutoDash();
            SetDashing(false);
        }

        private void Update()
        {
            if (motor == null || input == null || airPower == null || settings == null)
            {
                Session.CancelAutoDash();
                SetDashing(false);
                return;
            }

            float minimumInput = settings.minimumMoveInputToDash;
            bool hasMoveIntent =
                input.Move.sqrMagnitude >= minimumInput * minimumInput;

            if (!Session.WantsDash(hasMoveIntent, input.DashHeld))
            {
                SetDashing(false);
                return;
            }

            if (!Session.IsDashing &&
                !Session.CanBeginDash(airPower.CanStartDash))
            {
                SetDashing(false);
                return;
            }

            float cost =
                settings.dashDrainPerSecond * Time.deltaTime;

            bool stillHasPower = airPower.Spend(cost);

            Session.AfterSpend(stillHasPower);

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
            if (!Session.SetDashing(value))
            {
                if (motor != null)
                    motor.SetDashActive(value);

                return;
            }

            if (motor != null)
                motor.SetDashActive(value);

            if (Session.IsDashing)
                DashStarted?.Invoke();
            else
                DashStopped?.Invoke();
        }
    }
}
