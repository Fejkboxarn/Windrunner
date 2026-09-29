using UnityEngine;
using UnityEngine.Serialization;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirSlingSettings",
        menuName = "Airflow Prototype/Air Sling Settings")]
    public sealed class AirSlingSettings : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private int settingsVersion;

        public int SettingsVersion =>
            settingsVersion;

        [Header("Targeting")]
        [Min(1f)] public float maxTargetDistance = 62f;
        [Range(5f, 89f)] public float maxCameraAngle = 68f;
        [Range(0f, 1f)] public float minimumScore = 0.20f;
        public LayerMask obstructionMask = 1;

        [Range(0f, 1f)] public float screenCenterWeight = 0.54f;
        [Range(0f, 1f)] public float movementDirectionWeight = 0.18f;
        [Range(0f, 1f)] public float distanceWeight = 0.14f;
        [Range(0f, 1f)] public float cameraForwardWeight = 0.14f;

        [Header("RMB Perfect Timing")]
        [Tooltip(
            "By default, the Sling sequence only starts when RMB is released " +
            "inside the Sling Node's own perfect timing window.")]
        public bool requirePerfectReleaseForSequence = true;

        [Tooltip(
            "Independent RMB perfect timing for Sling Nodes.")]
        [FormerlySerializedAs("fallbackPerfectTime")]
        [Min(0.02f)]
        public float perfectTime = 0.30f;

        [FormerlySerializedAs("fallbackFullTimingTime")]
        [Min(0.05f)]
        public float fullTimingTime = 0.60f;

        [FormerlySerializedAs("fallbackPerfectLeeway")]
        [Range(0.01f, 0.25f)]
        public float perfectLeeway = 0.09f;

        [Header("Initial Perfect Reward")]
        [Tooltip(
            "Multiplier applied to the ACTUAL regular Air Node movement reward " +
            "when RMB is released perfectly. 1 = exact same reward.")]
        [Min(0f)]
        public float initialPerfectRewardMultiplier = 1f;

        [Header("Initial Aerial Reward - Planar")]
        [Tooltip("Fallback only if the regular AirFlowHitSettings asset could not be found.")]
        [Min(0f)] public float initialPlanarBoostImpulse = 4.4f;
        [Min(0f)] public float initialMaxBoostedSpeed = 20f;
        [Min(0f)] public float initialBoostHoldDuration = 0.45f;
        [Min(0.01f)] public float initialBoostDecayPerSecond = 4.5f;

        [Header("Initial Aerial Reward - Lift")]
        [Min(0f)] public float initialRisingLift = 1.5f;
        [Min(0f)] public float initialFallingLift = 3.4f;
        [Range(0f, 1f)] public float initialFallingCancelFraction = 1f;
        [Min(0.1f)] public float initialMaxUpwardSpeed = 7.5f;
        [Range(0.1f, 1f)] public float initialGravityScale = 0.48f;
        [Min(0f)] public float initialGravityDuration = 0.32f;
        [Min(1f)] public float initialSteeringMultiplier = 1.65f;
        [Min(0f)] public float initialSteeringDuration = 0.48f;

        [Header("Delay Before Snap")]
        [Tooltip(
            "After the first reward, the player remains free for this long " +
            "before the node starts pulling them to its top.")]
        [Min(0f)]
        public float delayBeforeNodeDash = 0.32f;

        [Header("Dash To Node Top")]
        [Min(1f)] public float dashStartSpeed = 24f;
        [Min(1f)] public float dashMaximumSpeed = 48f;
        [Min(1f)] public float dashAcceleration = 125f;
        [Min(0.01f)] public float landingTolerance = 0.08f;
        [Min(0.1f)] public float maximumDashDuration = 1.6f;

        [Tooltip(
            "During the forced snap, cancel normal PlayerMotor displacement " +
            "so the CharacterController follows the landing path precisely.")]
        public bool overrideNormalMovementDuringDash = true;

        [Header("Final Impact Reward - Planar")]
        [Tooltip(
            "Applied away from the node after landing. Stronger than the " +
            "regular Air Node reward by default.")]
        [Min(0f)] public float finalPlanarBoostImpulse = 18f;
        [Min(0f)] public float finalMaxBoostedSpeed = 34f;
        [Min(0f)] public float finalBoostHoldDuration = 0.90f;
        [Min(0.01f)] public float finalBoostDecayPerSecond = 4.0f;

        [Header("Final Impact Reward - Lift")]
        [Min(0f)] public float finalRisingLift = 4.0f;
        [Min(0f)] public float finalFallingLift = 7.0f;
        [Range(0f, 1f)] public float finalFallingCancelFraction = 1f;
        [Min(0.1f)] public float finalMaxUpwardSpeed = 12.5f;
        [Range(0.1f, 1f)] public float finalGravityScale = 0.40f;
        [Min(0f)] public float finalGravityDuration = 0.45f;
        [Min(1f)] public float finalSteeringMultiplier = 1.85f;
        [Min(0f)] public float finalSteeringDuration = 0.62f;

        [Header("Final Launch Direction")]
        [Tooltip(
            "The final boost continues FORWARD through the node using the " +
            "incoming dash direction as its foundation.")]
        [Range(0f, 1f)]
        public float finalCameraInfluence = 0.12f;

        [Tooltip(
            "Minimum alignment with the incoming dash direction. " +
            "Prevents camera steering from turning the launch backward.")]
        [Range(-1f, 1f)]
        public float minimumForwardDot = 0.65f;

        [Header("Dash Feel")]
        [Tooltip(
            "Extra immediate speed added when the forced dash begins. " +
            "This gives the snap a strong initial catch instead of a soft ramp.")]
        [Min(0f)]
        public float dashInitialKickSpeed = 8f;

        [Tooltip(
            "Small authored arc above the straight line to the node. " +
            "Set to zero for a perfectly straight dash.")]
        [Min(0f)]
        public float dashArcHeight = 1.15f;

        [Tooltip(
            "Normalized dash progress where the final acceleration surge begins.")]
        [Range(0.35f, 0.95f)]
        public float dashArrivalAccelerationStart = 0.72f;

        [Tooltip(
            "Acceleration multiplier during the final part of the dash.")]
        [Min(1f)]
        public float dashArrivalAccelerationMultiplier = 1.65f;

        [Header("Curved Sling Path")]
        [Tooltip(
            "How strongly the player's real incoming planar movement defines the " +
            "first tangent of the Sling curve.")]
        [Range(0f, 2f)]
        public float startMomentumInfluence = 1f;

        [Tooltip(
            "Length of the first Bezier tangent leaving the player.")]
        [Min(0f)]
        public float startTangentDistance = 4.5f;

        [Tooltip(
            "Length of the final Bezier tangent approaching the Sling Node.")]
        [Min(0f)]
        public float approachTangentDistance = 2.7f;

        [Tooltip(
            "Additional height added to the Bezier control points.")]
        [Min(0f)]
        public float bezierVerticalLift = 1.45f;

        [Tooltip(
            "Small sideways bend in the path. Zero removes lateral curvature.")]
        [Min(0f)]
        public float bezierLateralOffset = 0.85f;

        [Tooltip(
            "Normalized speed profile across the Sling path. " +
            "Values multiply Dash Maximum Speed.")]
        public AnimationCurve dashSpeedCurve =
            new AnimationCurve(
                new Keyframe(0f, 0.60f),
                new Keyframe(0.08f, 0.95f),
                new Keyframe(0.35f, 0.85f),
                new Keyframe(0.65f, 0.90f),
                new Keyframe(0.82f, 1.00f),
                new Keyframe(1f, 1.15f));

        [Range(8, 64)]
        [Tooltip(
            "Samples used to estimate Bezier path length so speed remains consistent.")]
        public int bezierLengthSamples = 24;

        [Header("Player Curve Banking")]
        public bool enablePlayerCurveBank = true;

        [Min(0f)]
        public float maximumPlayerCurveBankDegrees = 16f;

        [Tooltip(
            "Look-ahead along the curve used to calculate bank direction.")]
        [Range(0.01f, 0.35f)]
        public float curveBankPreview = 0.12f;

        [Tooltip(
            "Tangent angle difference that produces maximum banking.")]
        [Min(1f)]
        public float curveAngleForMaximumBank = 18f;

        [Min(0.01f)]
        public float curveBankResponse = 15f;

        [Header("Bear Facing During Sling")]
        public bool makeNodeVisualFacePlayerDuringSling = true;

        [Min(0f)]
        public float nodeVisualTurnSpeedDegreesPerSecond = 420f;

        [Header("Sling Camera FOV")]
        public bool enableSlingFovProfile = true;

        [Range(30f, 120f)]
        public float releaseFov = 70f;

        [Min(0.01f)]
        public float releaseFovResponse = 24f;

        [Min(0f)]
        public float releaseFovHoldDuration = 0.10f;

        [Min(0.01f)]
        public float releaseFovRecoveryResponse = 10f;

        [Range(30f, 120f)]
        public float dashFov = 82f;

        [Min(0.01f)]
        public float dashFovResponse = 10f;

        [Tooltip(
            "When the pre-impact FOV compression begins.")]
        [Range(0.5f, 0.98f)]
        public float impactFovCompressionStart = 0.86f;

        [Range(30f, 120f)]
        public float impactCompressionFov = 75f;

        [Min(0.01f)]
        public float impactCompressionFovResponse = 28f;

        [Range(30f, 120f)]
        public float launchFov = 84f;

        [Min(0.01f)]
        public float launchFovResponse = 34f;

        [Min(0f)]
        public float launchFovHoldDuration = 0.11f;

        [Min(0.01f)]
        public float launchFovRecoveryResponse = 7f;

        [Header("Dash Start Camera Kick")]
        public bool enableDashStartCameraKick = true;

        [Min(0f)]
        public float dashCameraKickBack = 0.34f;

        [Min(0f)]
        public float dashCameraKickUp = 0.07f;

        [Min(0f)]
        public float dashCameraKickRoll = 0.65f;

        [Min(0.01f)]
        public float dashCameraKickRecovery = 14f;

        [Header("Perfect RMB Time Stop")]
        [Tooltip(
            "Optional hit-stop the instant RMB is released inside the perfect window.")]
        public bool enablePerfectReleaseTimeStop = true;

        [Range(0.01f, 1f)]
        public float perfectReleaseTimeScale = 0.24f;

        [Min(0f)]
        public float perfectReleaseTimeStopDuration = 0.055f;

        [Min(0.01f)]
        public float perfectReleaseTimeStopRecovery = 0.16f;

        [Header("Node Impact Time Stop")]
        public bool enableNodeImpactTimeStop = true;

        [Range(0.01f, 1f)]
        public float nodeImpactTimeScale = 0.24f;

        [Tooltip(
            "Editable length of the planted time-stop when the player reaches the node.")]
        [Min(0f)]
        public float nodeImpactTimeStopDuration = 0.085f;

        [Min(0.01f)]
        public float nodeImpactTimeStopRecovery = 0.16f;

        [Header("Sling Impact Pose")]
        [Tooltip(
            "Unscaled-time hold after contact so the authored Sling Impact animation " +
            "can read before the final forward/up launch.")]
        [Min(0f)]
        public float impactPoseHoldDuration = 0.16f;

        [Header("Final Launch Flip")]
        public bool enableFinalLaunchFlip = true;

        [Range(0f, 1.5f)]
        public float finalLaunchFlipIntensity = 1f;

        [Header("Default Sling Node Visual")]
        [Tooltip(
            "Optional default visual prefab for newly created Sling Nodes. " +
            "Drag the bear prefab here. Its child Animator will be wired automatically.")]
        public GameObject defaultNodeVisualPrefab;

        [Tooltip("Local offset applied when the default visual prefab is instantiated.")]
        public Vector3 defaultVisualLocalPosition = Vector3.zero;

        [Tooltip("Local Euler rotation applied when the default visual prefab is instantiated.")]
        public Vector3 defaultVisualLocalEulerAngles = Vector3.zero;

        [Tooltip("Local scale applied when the default visual prefab is instantiated.")]
        public Vector3 defaultVisualLocalScale = Vector3.one;

        [Header("Impact VFX Prefab")]
        [Tooltip(
            "Drag your customized Impact Splash VFX prefab here. " +
            "Every newly created Sling Node will instantiate this prefab.")]
        public GameObject impactSplashPrefab;

        [Header("Sling Impact Damage")]
        [Tooltip("Damage dealt when RMB release is not inside the perfect window.")]
        [Min(0)]
        public int normalReleaseDamage = 1;

        [Tooltip("Damage dealt when RMB release is inside the perfect window.")]
        [Min(0)]
        public int perfectReleaseDamage = 2;

        [Header("Optional Air Power Cost")]
        public bool useAirPower = false;

        [Min(0f)]
        public float airPowerCost = 15f;

        public bool requireEnoughAirPower = true;

        [Header("Reticle")]
        public Color slingReticleTint =
            new Color(
                1f,
                0.18f,
                0.12f,
                1f);

        [Tooltip(
            "Fallback UI position only. With an AirCaster present, the UI uses " +
            "the normal AirCastSettings PerfectCenter01 / PerfectHalfWidth01.")]
        [Range(0.05f, 0.95f)]
        public float reticlePerfectCenter01 = 0.50f;

        [Range(0.01f, 0.25f)]
        public float reticlePerfectHalfWidth01 = 0.15f;

        private void OnEnable()
        {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults()
        {
            if (settingsVersion < 184)
            {
                ResetPerfectJumpForwardDefaultsV184();
            }

            if (settingsVersion < 185)
            {
                MigrateToFeelImpactPolishV185();
            }

            if (settingsVersion < 187)
            {
                MigrateToSeparateSlingUIV187();
            }

            if (settingsVersion < 188)
            {
                MigrateToBearHealthV188();
            }

            if (settingsVersion < 189)
            {
                MigrateToBearWanderVisualV189();
            }

            if (settingsVersion < 1811)
            {
                MigrateToCurvedSlingCameraV1811();
            }

            if (settingsVersion < 1900)
            {
                MigrateToPlayerAnimationFoundationV1900();
            }
        }

        private void MigrateToPlayerAnimationFoundationV1900()
        {
            // Only initialize the new authored impact-pose hold.
            // Existing Sling movement/camera/combat tuning is preserved.
            impactPoseHoldDuration = 0.16f;

            settingsVersion = 1900;
        }

        private void MigrateToCurvedSlingCameraV1811()
        {
            // Only initialize NEW v18.11 feel controls.
            // Existing Sling timing, damage, audio, health and visual tuning is preserved.
            startMomentumInfluence = 1f;
            startTangentDistance = 4.5f;
            approachTangentDistance = 2.7f;
            bezierVerticalLift = 1.45f;
            bezierLateralOffset = 0.85f;

            dashSpeedCurve =
                new AnimationCurve(
                    new Keyframe(0f, 0.60f),
                    new Keyframe(0.08f, 0.95f),
                    new Keyframe(0.35f, 0.85f),
                    new Keyframe(0.65f, 0.90f),
                    new Keyframe(0.82f, 1.00f),
                    new Keyframe(1f, 1.15f));

            bezierLengthSamples = 24;

            enablePlayerCurveBank = true;
            maximumPlayerCurveBankDegrees = 16f;
            curveBankPreview = 0.12f;
            curveAngleForMaximumBank = 18f;
            curveBankResponse = 15f;

            makeNodeVisualFacePlayerDuringSling = true;
            nodeVisualTurnSpeedDegreesPerSecond = 420f;

            enableSlingFovProfile = true;
            releaseFov = 70f;
            releaseFovResponse = 24f;
            releaseFovHoldDuration = 0.10f;
            releaseFovRecoveryResponse = 10f;

            dashFov = 82f;
            dashFovResponse = 10f;

            impactFovCompressionStart = 0.86f;
            impactCompressionFov = 75f;
            impactCompressionFovResponse = 28f;

            launchFov = 84f;
            launchFovResponse = 34f;
            launchFovHoldDuration = 0.11f;
            launchFovRecoveryResponse = 7f;

            settingsVersion = 1811;
        }

        private void MigrateToBearWanderVisualV189()
        {
            // Only new visual-placement defaults are initialized.
            // Existing movement, timing, health and audio tuning is preserved.
            defaultVisualLocalPosition =
                Vector3.zero;

            defaultVisualLocalEulerAngles =
                Vector3.zero;

            defaultVisualLocalScale =
                Vector3.one;

            settingsVersion = 189;
        }

        private void MigrateToBearHealthV188()
        {
            // Normal RMB releases now complete the Sling sequence and deal
            // normalReleaseDamage. Perfect releases retain their special boost
            // and deal perfectReleaseDamage.
            requirePerfectReleaseForSequence = false;

            normalReleaseDamage = 1;
            perfectReleaseDamage = 2;

            settingsVersion = 188;
        }

        private void MigrateToSeparateSlingUIV187()
        {
            // Timing values are preserved through FormerlySerializedAs.
            // No existing movement/VFX tuning is reset.
            settingsVersion = 187;
        }

        private void MigrateToFeelImpactPolishV185()
        {
            // IMPORTANT: only initialize NEW v18.5 fields.
            // Existing v18.4 tuning is intentionally preserved.
            dashInitialKickSpeed = 8f;
            dashArcHeight = 1.15f;
            dashArrivalAccelerationStart = 0.72f;
            dashArrivalAccelerationMultiplier = 1.65f;

            enableDashStartCameraKick = true;
            dashCameraKickBack = 0.34f;
            dashCameraKickUp = 0.07f;
            dashCameraKickRoll = 0.65f;
            dashCameraKickRecovery = 14f;

            enablePerfectReleaseTimeStop = true;
            perfectReleaseTimeScale = 0.24f;
            perfectReleaseTimeStopDuration = 0.055f;
            perfectReleaseTimeStopRecovery = 0.16f;

            enableNodeImpactTimeStop = true;
            nodeImpactTimeScale = 0.24f;
            nodeImpactTimeStopDuration = 0.085f;
            nodeImpactTimeStopRecovery = 0.16f;

            enableFinalLaunchFlip = true;
            finalLaunchFlipIntensity = 1f;

            settingsVersion = 185;
        }

        public void ResetPerfectJumpForwardDefaultsV184()
        {
            maxTargetDistance = 62f;
            maxCameraAngle = 68f;
            minimumScore = 0.20f;
            obstructionMask = 1;

            screenCenterWeight = 0.54f;
            movementDirectionWeight = 0.18f;
            distanceWeight = 0.14f;
            cameraForwardWeight = 0.14f;

            requirePerfectReleaseForSequence = true;
            perfectTime = 0.30f;
            fullTimingTime = 0.60f;
            perfectLeeway = 0.09f;
            initialPerfectRewardMultiplier = 1f;

            initialPlanarBoostImpulse = 4.4f;
            initialMaxBoostedSpeed = 20f;
            initialBoostHoldDuration = 0.45f;
            initialBoostDecayPerSecond = 4.5f;

            initialRisingLift = 1.5f;
            initialFallingLift = 3.4f;
            initialFallingCancelFraction = 1f;
            initialMaxUpwardSpeed = 7.5f;
            initialGravityScale = 0.48f;
            initialGravityDuration = 0.32f;
            initialSteeringMultiplier = 1.65f;
            initialSteeringDuration = 0.48f;

            delayBeforeNodeDash = 0.32f;

            dashStartSpeed = 24f;
            dashMaximumSpeed = 48f;
            dashAcceleration = 125f;
            landingTolerance = 0.08f;
            maximumDashDuration = 1.6f;
            overrideNormalMovementDuringDash = true;

            finalPlanarBoostImpulse = 18f;
            finalMaxBoostedSpeed = 34f;
            finalBoostHoldDuration = 0.90f;
            finalBoostDecayPerSecond = 4.0f;

            finalRisingLift = 4.0f;
            finalFallingLift = 7.0f;
            finalFallingCancelFraction = 1f;
            finalMaxUpwardSpeed = 12.5f;
            finalGravityScale = 0.40f;
            finalGravityDuration = 0.45f;
            finalSteeringMultiplier = 1.85f;
            finalSteeringDuration = 0.62f;

            finalCameraInfluence = 0.12f;
            minimumForwardDot = 0.65f;

            useAirPower = false;
            airPowerCost = 15f;
            requireEnoughAirPower = true;

            slingReticleTint =
                new Color(
                    1f,
                    0.18f,
                    0.12f,
                    1f);

            reticlePerfectCenter01 = 0.50f;
            reticlePerfectHalfWidth01 = 0.15f;

            settingsVersion = 184;
        }

        private void OnValidate()
        {
            dashMaximumSpeed =
                Mathf.Max(
                    dashStartSpeed,
                    dashMaximumSpeed);

            finalMaxBoostedSpeed =
                Mathf.Max(
                    finalPlanarBoostImpulse,
                    finalMaxBoostedSpeed);

            perfectTime =
                Mathf.Max(
                    0.02f,
                    perfectTime);

            fullTimingTime =
                Mathf.Max(
                    perfectTime + 0.01f,
                    fullTimingTime);

            perfectLeeway =
                Mathf.Clamp(
                    perfectLeeway,
                    0.01f,
                    Mathf.Max(
                        0.01f,
                        fullTimingTime * 0.45f));

            dashArrivalAccelerationMultiplier =
                Mathf.Max(
                    1f,
                    dashArrivalAccelerationMultiplier);

            dashCameraKickRecovery =
                Mathf.Max(
                    0.01f,
                    dashCameraKickRecovery);

            perfectReleaseTimeStopRecovery =
                Mathf.Max(
                    0.01f,
                    perfectReleaseTimeStopRecovery);

            nodeImpactTimeStopRecovery =
                Mathf.Max(
                    0.01f,
                    nodeImpactTimeStopRecovery);


            startTangentDistance =
                Mathf.Max(
                    0f,
                    startTangentDistance);

            approachTangentDistance =
                Mathf.Max(
                    0f,
                    approachTangentDistance);

            bezierVerticalLift =
                Mathf.Max(
                    0f,
                    bezierVerticalLift);

            bezierLateralOffset =
                Mathf.Max(
                    0f,
                    bezierLateralOffset);

            bezierLengthSamples =
                Mathf.Clamp(
                    bezierLengthSamples,
                    8,
                    64);

            curveAngleForMaximumBank =
                Mathf.Max(
                    1f,
                    curveAngleForMaximumBank);

            curveBankResponse =
                Mathf.Max(
                    0.01f,
                    curveBankResponse);

            nodeVisualTurnSpeedDegreesPerSecond =
                Mathf.Max(
                    0f,
                    nodeVisualTurnSpeedDegreesPerSecond);

            releaseFovResponse =
                Mathf.Max(
                    0.01f,
                    releaseFovResponse);

            releaseFovRecoveryResponse =
                Mathf.Max(
                    0.01f,
                    releaseFovRecoveryResponse);

            dashFovResponse =
                Mathf.Max(
                    0.01f,
                    dashFovResponse);

            impactCompressionFovResponse =
                Mathf.Max(
                    0.01f,
                    impactCompressionFovResponse);

            launchFovResponse =
                Mathf.Max(
                    0.01f,
                    launchFovResponse);

            launchFovRecoveryResponse =
                Mathf.Max(
                    0.01f,
                    launchFovRecoveryResponse);


            impactPoseHoldDuration =
                Mathf.Max(
                    0f,
                    impactPoseHoldDuration);
        }
    }
}
