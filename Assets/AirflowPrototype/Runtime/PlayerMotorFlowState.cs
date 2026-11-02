using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Traversal-reward speed envelope and temporary aerial-flow modifiers.
    /// This class owns state/rules; the PlayerMotor component owns movement and collision.
    /// </summary>
    public sealed class PlayerMotorFlowState
    {
        private float _rewardBoostSpeedFloor;
        private float _rewardBoostHoldRemaining;
        private float _rewardBoostDecayPerSecond;

        private float _aerialGravityScale = 1f;
        private float _aerialGravityRemaining;
        private float _aerialSteeringMultiplier = 1f;
        private float _aerialSteeringRemaining;

        public bool HasRewardSpeedBoost => _rewardBoostSpeedFloor > 0.01f;
        public float RewardBoostSpeedFloor => _rewardBoostSpeedFloor;
        public float RewardBoostHoldRemaining => _rewardBoostHoldRemaining;
        public float AerialGravityScale => _aerialGravityScale;
        public float AerialGravityRemaining => _aerialGravityRemaining;
        public float AerialSteeringMultiplier => _aerialSteeringMultiplier;
        public float AerialSteeringRemaining => _aerialSteeringRemaining;
        public bool HasAerialFlow => _aerialGravityRemaining > 0f || _aerialSteeringRemaining > 0f;

        public void AddRewardPlanarBoost(float resultingSpeed, float holdDuration, float decayPerSecond)
        {
            _rewardBoostSpeedFloor = Mathf.Max(_rewardBoostSpeedFloor, resultingSpeed);
            _rewardBoostHoldRemaining = Mathf.Max(_rewardBoostHoldRemaining, Mathf.Max(0f, holdDuration));
            _rewardBoostDecayPerSecond = Mathf.Max(0.01f, decayPerSecond);
        }

        public void TickRewardBoost(float inputMagnitude, float dt, float normalDashSpeed)
        {
            if (_rewardBoostSpeedFloor <= 0f)
                return;

            if (inputMagnitude <= 0.0001f)
            {
                ClearRewardBoost();
                return;
            }

            if (_rewardBoostHoldRemaining > 0f)
            {
                _rewardBoostHoldRemaining = Mathf.Max(0f, _rewardBoostHoldRemaining - dt);
                return;
            }

            _rewardBoostSpeedFloor = Mathf.MoveTowards(
                _rewardBoostSpeedFloor,
                normalDashSpeed,
                _rewardBoostDecayPerSecond * dt);

            if (_rewardBoostSpeedFloor <= normalDashSpeed + 0.01f)
                ClearRewardBoost();
        }

        public void ClearRewardBoost()
        {
            _rewardBoostSpeedFloor = 0f;
            _rewardBoostHoldRemaining = 0f;
            // Preserve the legacy decay value, which is overwritten on the next reward.
        }

        public float ApplyAerialFlowReward(
            float verticalVelocity,
            bool grounded,
            bool forceWhenGrounded,
            float risingLift,
            float fallingLift,
            float fallingCancelFraction,
            float maxUpwardSpeed,
            float gravityScale,
            float gravityDuration,
            float steeringMultiplier,
            float steeringDuration)
        {
            if (grounded && !forceWhenGrounded)
                return verticalVelocity;

            bool falling = verticalVelocity < 0f;
            if (falling)
            {
                float cancelFraction = Mathf.Clamp01(fallingCancelFraction);
                verticalVelocity = Mathf.Lerp(verticalVelocity, 0f, cancelFraction);
                verticalVelocity += Mathf.Max(0f, fallingLift);
            }
            else
            {
                verticalVelocity += Mathf.Max(0f, risingLift);
            }

            verticalVelocity = Mathf.Min(verticalVelocity, Mathf.Max(0.1f, maxUpwardSpeed));
            _aerialGravityScale = Mathf.Clamp(gravityScale, 0.1f, 1f);
            _aerialGravityRemaining = Mathf.Max(_aerialGravityRemaining, Mathf.Max(0f, gravityDuration));
            _aerialSteeringMultiplier = Mathf.Max(1f, steeringMultiplier);
            _aerialSteeringRemaining = Mathf.Max(_aerialSteeringRemaining, Mathf.Max(0f, steeringDuration));
            return verticalVelocity;
        }

        public void TickAerialFlow(float dt)
        {
            if (_aerialGravityRemaining > 0f)
                _aerialGravityRemaining = Mathf.Max(0f, _aerialGravityRemaining - dt);

            if (_aerialSteeringRemaining > 0f)
                _aerialSteeringRemaining = Mathf.Max(0f, _aerialSteeringRemaining - dt);

            if (_aerialGravityRemaining <= 0f)
                _aerialGravityScale = 1f;

            if (_aerialSteeringRemaining <= 0f)
                _aerialSteeringMultiplier = 1f;
        }

        public void ClearAerialFlow()
        {
            _aerialGravityScale = 1f;
            _aerialGravityRemaining = 0f;
            _aerialSteeringMultiplier = 1f;
            _aerialSteeringRemaining = 0f;
        }
    }
}
