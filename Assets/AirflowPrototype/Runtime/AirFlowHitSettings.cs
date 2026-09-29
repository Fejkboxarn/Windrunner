using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirFlowHitSettings",
        menuName = "Airflow Prototype/Air Flow Hit Settings")]
    public sealed class AirFlowHitSettings : ScriptableObject
    {
        [Header("Gameplay Reward")]
        [Min(0f)] public float airPowerRestore = 28f;

        [Tooltip("Instant horizontal speed impulse when the return stream reaches the player.")]
        [Min(0f)] public float planarBoostImpulse = 4.4f;

        [Tooltip("Absolute speed cap for the instant node-hit boost.")]
        [Min(0f)] public float maxBoostedSpeed = 20f;

        [Tooltip("How long the newly gained boosted speed is protected before it begins falling back toward normal dash speed.")]
        [Min(0f)] public float boostHoldDuration = 0.45f;

        [Tooltip("After Boost Hold Duration, how many metres/second of boosted speed are lost per second until normal dash speed is reached.")]
        [Min(0.01f)] public float boostSpeedDecayPerSecond = 4.5f;

        [Range(0f, 1f)] public float minimumChargeRewardMultiplier = 0.72f;
        [Min(0f)] public float minimumTravelSpeedForDirection = 0.75f;

        [Header("Aerial Flow Reward")]
        [Tooltip("Upward velocity added when the reward arrives while the player is already rising.")]
        [Min(0f)] public float risingLift = 1.5f;

        [Tooltip("Upward velocity added when the reward arrives while the player is falling.")]
        [Min(0f)] public float fallingLift = 3.4f;

        [Tooltip("How much downward velocity is canceled before Falling Lift is applied. 1 = cancel the fall completely.")]
        [Range(0f, 1f)] public float fallingVelocityCancelFraction = 1f;

        [Tooltip("Caps upward speed after an aerial reward so repeated chains do not launch the player uncontrollably high.")]
        [Min(0.1f)] public float maxRewardUpwardSpeed = 7.5f;

        [Tooltip("Gravity multiplier during the short aerial-flow grace window. Lower = floatier.")]
        [Range(0.1f, 1f)] public float aerialFlowGravityScale = 0.48f;

        [Tooltip("How long reduced gravity lasts after an airborne reward.")]
        [Min(0f)] public float aerialFlowGravityDuration = 0.32f;

        [Tooltip("Multiplier applied to normal air steering while the aerial-flow window is active.")]
        [Min(1f)] public float aerialFlowSteeringMultiplier = 1.65f;

        [Tooltip("How long enhanced air steering lasts after an airborne reward.")]
        [Min(0f)] public float aerialFlowSteeringDuration = 0.48f;

        [Header("Node Chaining")]
        [Min(0f)] public float nodeTargetingCooldown = 0.90f;

        [Header("Return Delivery")]
        [Min(0.03f)] public float returnStreamDuration = 0.16f;

        [Header("Camera Feedback")]
        [Min(0f)] public float fovPunchDegrees = 3.5f;
        [Min(0.01f)] public float fovPunchDecayPerSecond = 18f;

        [Min(0f)] public float rewardCameraKickBack = 0.16f;
        [Min(0f)] public float rewardCameraKickUp = 0.035f;
        [Range(0f, 8f)] public float rewardCameraRollKick = 1.4f;
        [Min(0.01f)] public float rewardCameraKickRecovery = 16f;

        [Header("Return Stream Visual")]
        [Min(0.01f)] public float returnTrailTime = 0.13f;
        [Min(0.005f)] public float returnTrailStartWidth = 0.12f;
        [Min(0.001f)] public float returnTrailEndWidth = 0.02f;

        [Header("Gamepad Hit")]
        [Range(0f, 1f)] public float lowFrequencyRumble = 0.28f;
        [Range(0f, 1f)] public float highFrequencyRumble = 0.62f;
        [Min(0f)] public float rumbleDuration = 0.10f;
    }
}
