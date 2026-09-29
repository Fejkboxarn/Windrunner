using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirflowMovementSettings",
        menuName = "Airflow Prototype/Movement Settings")]
    public sealed class AirflowMovementSettings : ScriptableObject
    {
        [Header("Ground Movement")]
        [Min(0f)] public float maxGroundSpeed = 8.5f;
        [Min(0f)] public float groundAcceleration = 42f;
        [Min(0f)] public float groundDeceleration = 52f;
        [Min(0f)] public float turnSharpness = 16f;

        [Header("Air Movement")]
        [Min(0f)] public float airAcceleration = 18f;
        [Range(0f, 1f)] public float airNoInputRetention = 1f;

        [Header("Jump")]
        [Min(0.01f)] public float jumpHeight = 2.25f;
        [Min(0.01f)] public float gravity = 30f;
        [Min(1f)] public float fallGravityMultiplier = 1.25f;
        [Min(0f)] public float groundedStickForce = 3f;
        [Range(0f, 0.5f)] public float coyoteTime = 0.12f;
        [Range(0f, 0.5f)] public float jumpBufferTime = 0.14f;

        [Header("Air Dash / Momentum")]
        [Min(0f)] public float dashMaxSpeed = 15.5f;
        [Min(0f)] public float dashAcceleration = 24f;
        [Min(0f)] public float dashAirAcceleration = 13f;
        [Min(0f)] public float dashReleaseDeceleration = 7f;
        [Min(0f)] public float dashBrakeDeceleration = 34f;
        [Min(0f)] public float dashReverseBrakeDeceleration = 42f;
        [Min(0f)] public float airborneMomentumDecay = 1.5f;
        [Min(0f)] public float dashSteeringSharpness = 7f;
        [Range(0f, 1f)] public float minimumMoveInputToDash = 0.12f;

        [Header("Air Power")]
        [Min(1f)] public float maxAirPower = 100f;
        [Min(0f)] public float dashDrainPerSecond = 22f;

        [Tooltip("Prototype fallback. Leave OFF for the intended node-driven flow loop.")]
        public bool enablePassiveRecovery = false;

        [Min(0f)] public float passiveRecoveryPerSecond = 28f;
        [Min(0f)] public float passiveRecoveryDelay = 0.55f;
        [Range(0f, 1f)] public float dashRestartFraction = 0.16f;

        [Header("Camera")]
        [Min(0.5f)] public float cameraDistance = 5.8f;
        [Min(0f)] public float cameraPivotHeight = 1.45f;
        [Min(0f)] public float cameraPositionSmoothTime = 0.045f;
        [Min(0f)] public float mouseSensitivity = 0.10f;
        [Min(0f)] public float gamepadLookDegreesPerSecond = 150f;
        public float minCameraPitch = -28f;
        public float maxCameraPitch = 68f;
        [Min(0.01f)] public float cameraCollisionRadius = 0.22f;
        [Min(0f)] public float cameraCollisionPadding = 0.08f;

        [Header("Speed Feedback")]
        [Range(30f, 110f)] public float baseFov = 65f;
        [Range(30f, 110f)] public float fastFov = 78f;
        [Min(0.01f)] public float speedForMaxFov = 15.5f;
        [Min(0.01f)] public float fovResponse = 7f;
        [Range(0f, 18f)] public float dashBodyLeanDegrees = 8f;
        [Min(0.01f)] public float bodyLeanResponse = 9f;
    }
}
