using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirflowPrototype
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class AirSlingController : MonoBehaviour
    {
        private enum SlingState
        {
            Idle,
            Charging,
            DelayBeforeDash,
            DashingToNode
        }

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CharacterController controller;
        [SerializeField] private AirSlingTargeter slingTargeter;
        [SerializeField] private AirPower airPower;
        [SerializeField] private AirFlowHitSettings regularNodeRewardSettings;
        [SerializeField] private AirSlingImpactAudioLibrary impactAudioLibrary;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private AirPerfectTimingFeedback perfectTimingFeedback;
        [SerializeField] private PlayerMovementVisuals movementVisuals;
        [SerializeField] private PlayerSpeedFeedback speedFeedback;
        [SerializeField] private PlayerOrbitCamera orbitCamera;
        [SerializeField] private AirSlingSettings settings;

        private SlingState _state =
            SlingState.Idle;

        private AirSlingNode _trackedTarget;

        private float _chargeElapsed;
        private float _capturedPerfectQuality;
        private float _delayRemaining;

        private float _dashSpeed;
        private float _dashElapsed;
        private float _dashProgress;
        private float _dashPathDistance;

        private Vector3 _dashStartRootPosition;
        private Vector3 _dashLandingRootPosition;

        private Vector3 _bezierP0;
        private Vector3 _bezierP1;
        private Vector3 _bezierP2;
        private Vector3 _bezierP3;

        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;

        // Direction from the player INTO the node. The final launch continues
        // forward through the node instead of bouncing backward.
        private Vector3 _incomingForwardDirection;

        public bool IsCharging =>
            _state ==
            SlingState.Charging;

        public bool IsSlinging =>
            _state !=
            SlingState.Idle;

        public bool IsDashingToNode =>
            _state ==
            SlingState.DashingToNode;

        public AirSlingNode CurrentTarget =>
            _state != SlingState.Idle &&
            IsTrackedTargetAvailable()
                ? _trackedTarget
                : slingTargeter != null
                    ? slingTargeter.CurrentTarget
                    : null;

        public AirSlingNode DisplayTarget =>
            _state != SlingState.Idle
                ? _trackedTarget
                : slingTargeter != null
                    ? slingTargeter.CurrentTarget
                    : null;

        public float TimingProgress01
        {
            get
            {
                if (_state !=
                    SlingState.Charging)
                {
                    return 0f;
                }

                float fullTime =
                    GetFullTimingTime();

                return Mathf.Clamp01(
                    _chargeElapsed /
                    Mathf.Max(
                        0.05f,
                        fullTime));
            }
        }

        public float PerfectCenter01
        {
            get
            {
                float fullTime =
                    GetFullTimingTime();

                return
                    fullTime > 0f
                        ? Mathf.Clamp01(
                            GetPerfectTime() /
                            fullTime)
                        : 0.5f;
            }
        }

        public float PerfectHalfWidth01
        {
            get
            {
                float fullTime =
                    GetFullTimingTime();

                return
                    fullTime > 0f
                        ? Mathf.Clamp(
                            GetPerfectLeeway() /
                            fullTime,
                            0.001f,
                            0.49f)
                        : 0.15f;
            }
        }

        public bool IsPerfectWindow =>
            EvaluatePerfectQuality(
                _chargeElapsed) > 0f;

        public AirSlingSettings Settings =>
            settings;

        public event Action<AirSlingNode> SlingStarted;
        public event Action<AirSlingNode, float> InitialBoostDelivered;
        public event Action<AirSlingNode> NodeDashStarted;
        public event Action<AirSlingNode> NodeImpact;
        public event Action<AirSlingNode, float, bool> SlingLaunched;
        public event Action<AirSlingNode, float> PerfectSlingLaunched;
        public event Action SlingCanceled;

        public void Configure(
            PlayerMotor newMotor,
            CharacterController newController,
            AirSlingTargeter newSlingTargeter,
            AirPower newAirPower,
            Camera newCamera,
            AirPerfectTimingFeedback newPerfectTimingFeedback,
            AirSlingSettings newSettings,
            AirFlowHitSettings newRegularNodeRewardSettings = null,
            PlayerMovementVisuals newMovementVisuals = null,
            PlayerOrbitCamera newOrbitCamera = null,
            AirSlingImpactAudioLibrary newImpactAudioLibrary = null,
            PlayerSpeedFeedback newSpeedFeedback = null)
        {
            motor = newMotor;
            controller = newController;
            slingTargeter = newSlingTargeter;
            airPower = newAirPower;
            targetCamera = newCamera;
            perfectTimingFeedback =
                newPerfectTimingFeedback;
            settings = newSettings;

            if (newRegularNodeRewardSettings != null)
            {
                regularNodeRewardSettings =
                    newRegularNodeRewardSettings;
            }

            if (newMovementVisuals != null)
            {
                movementVisuals =
                    newMovementVisuals;
            }

            if (newOrbitCamera != null)
            {
                orbitCamera =
                    newOrbitCamera;
            }

            if (newSpeedFeedback != null)
            {
                speedFeedback =
                    newSpeedFeedback;
            }

            if (newImpactAudioLibrary != null)
            {
                impactAudioLibrary =
                    newImpactAudioLibrary;
            }

            ResolveOptionalReferences();
            ForceIdleState();

            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (controller == null)
                controller = GetComponent<CharacterController>();

            if (slingTargeter == null)
                slingTargeter = GetComponent<AirSlingTargeter>();

            if (airPower == null)
                airPower = GetComponent<AirPower>();

            ResolveOptionalReferences();
            ForceIdleState();

            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void ResolveOptionalReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (perfectTimingFeedback == null)
            {
                perfectTimingFeedback =
                    GetComponent<AirPerfectTimingFeedback>();
            }

            if (movementVisuals == null)
            {
                movementVisuals =
                    GetComponent<PlayerMovementVisuals>();
            }

            if (speedFeedback == null)
            {
                speedFeedback =
                    GetComponent<PlayerSpeedFeedback>();
            }

            if (speedFeedback == null)
            {
                speedFeedback =
                    FindAnyObjectByType<PlayerSpeedFeedback>();
            }

            if (orbitCamera == null)
            {
                orbitCamera =
                    FindAnyObjectByType<PlayerOrbitCamera>();
            }
        }

        private void OnEnable()
        {
            ForceIdleState();

            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (motor == null ||
                controller == null ||
                slingTargeter == null ||
                settings == null)
            {
                FinishFrame();
                return;
            }

            Mouse mouse =
                Mouse.current;

            switch (_state)
            {
                case SlingState.Idle:
                    UpdateIdle(mouse);
                    break;

                case SlingState.Charging:
                    UpdateCharging(mouse);
                    break;

                case SlingState.DelayBeforeDash:
                    UpdateDelayBeforeDash();
                    break;

                case SlingState.DashingToNode:
                    UpdateDashToNode();
                    break;
            }

            FinishFrame();
        }

        private void UpdateIdle(
            Mouse mouse)
        {
            // Targeting remains presentation-only until RMB is actually pressed.
            if (mouse == null ||
                !mouse.rightButton.wasPressedThisFrame)
            {
                return;
            }

            AirSlingNode target =
                slingTargeter.CurrentTarget;

            if (target == null ||
                !target.IsAvailable)
            {
                return;
            }

            BeginCharge(
                target);
        }

        private void BeginCharge(
            AirSlingNode target)
        {
            _trackedTarget = target;
            _state = SlingState.Charging;

            _chargeElapsed = 0f;
            _capturedPerfectQuality = 0f;

            _trackedTarget.SetSelected(
                true);

            _trackedTarget.SetInteractionActive(
                true);

            _trackedTarget
                .PlayEngagedAnimation();

            SlingStarted?.Invoke(
                _trackedTarget);
        }

        private void UpdateCharging(
            Mouse mouse)
        {
            if (!IsTrackedTargetAvailable())
            {
                CancelSling();
                return;
            }

            if (mouse == null)
            {
                CancelSling();
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                _chargeElapsed +=
                    Time.deltaTime;

                return;
            }

            if (mouse.rightButton.wasReleasedThisFrame ||
                !mouse.rightButton.isPressed)
            {
                ReleaseCharge();
            }
        }

        private void ReleaseCharge()
        {
            float perfectQuality =
                EvaluatePerfectQuality(
                    _chargeElapsed);

            bool perfect =
                perfectQuality > 0f;

            if (settings.requirePerfectReleaseForSequence &&
                !perfect)
            {
                CancelSling();
                return;
            }

            if (!TrySpendAirPower())
            {
                CancelSling();
                return;
            }

            _capturedPerfectQuality =
                perfect
                    ? perfectQuality
                    : 0f;

            if (settings.enableSlingFovProfile &&
                speedFeedback != null)
            {
                speedFeedback.PulseFovOverride(
                    settings.releaseFov,
                    settings.releaseFovResponse,
                    settings.releaseFovHoldDuration,
                    settings.releaseFovRecoveryResponse);
            }

            if (perfect)
            {
                // Exact normal Air Node perfect movement reward, but the
                // aerial part is forced even if the player is grounded.
                ApplyExactRegularPerfectReward(
                    settings.initialPerfectRewardMultiplier);

                InitialBoostDelivered?.Invoke(
                    _trackedTarget,
                    perfectQuality);

                if (settings.enablePerfectReleaseTimeStop &&
                    perfectTimingFeedback != null)
                {
                    perfectTimingFeedback
                        .TriggerPerfectFeedback(
                            perfectQuality,
                            settings
                                .perfectReleaseTimeStopDuration,
                            settings
                                .perfectReleaseTimeStopRecovery,
                            settings
                                .perfectReleaseTimeScale);
                }
            }

            _delayRemaining =
                settings.delayBeforeNodeDash;

            _state =
                SlingState.DelayBeforeDash;

            if (_delayRemaining <= 0f)
                BeginDashToNode();
        }

        private void ApplyExactRegularPerfectReward(
            float multiplier)
        {
            multiplier =
                Mathf.Max(
                    0f,
                    multiplier);

            AirFlowHitSettings nodeSettings =
                regularNodeRewardSettings;

            if (nodeSettings != null)
            {
                Vector3 direction =
                    GetRegularRewardDirection(
                        nodeSettings
                            .minimumTravelSpeedForDirection);

                motor.AddRewardPlanarBoost(
                    direction *
                    nodeSettings.planarBoostImpulse *
                    multiplier,
                    nodeSettings.maxBoostedSpeed,
                    nodeSettings.boostHoldDuration,
                    nodeSettings
                        .boostSpeedDecayPerSecond);

                motor.ApplyForcedAerialFlowReward(
                    nodeSettings.risingLift *
                    multiplier,
                    nodeSettings.fallingLift *
                    multiplier,
                    nodeSettings
                        .fallingVelocityCancelFraction,
                    nodeSettings
                        .maxRewardUpwardSpeed,
                    nodeSettings
                        .aerialFlowGravityScale,
                    nodeSettings
                        .aerialFlowGravityDuration,
                    nodeSettings
                        .aerialFlowSteeringMultiplier,
                    nodeSettings
                        .aerialFlowSteeringDuration);

                return;
            }

            Vector3 fallbackDirection =
                GetRegularRewardDirection(
                    0.75f);

            motor.AddRewardPlanarBoost(
                fallbackDirection *
                settings.initialPlanarBoostImpulse *
                multiplier,
                settings.initialMaxBoostedSpeed,
                settings.initialBoostHoldDuration,
                settings.initialBoostDecayPerSecond);

            motor.ApplyForcedAerialFlowReward(
                settings.initialRisingLift *
                multiplier,
                settings.initialFallingLift *
                multiplier,
                settings.initialFallingCancelFraction,
                settings.initialMaxUpwardSpeed,
                settings.initialGravityScale,
                settings.initialGravityDuration,
                settings.initialSteeringMultiplier,
                settings.initialSteeringDuration);
        }

        private void UpdateDelayBeforeDash()
        {
            if (!IsTrackedTargetAvailable())
            {
                CancelSling();
                return;
            }

            UpdateTrackedNodeFacing();

            _delayRemaining -=
                Time.deltaTime;

            if (_delayRemaining <= 0f)
                BeginDashToNode();
        }

        private void BeginDashToNode()
        {
            if (!IsTrackedTargetAvailable())
            {
                CancelSling();
                return;
            }

            _dashStartRootPosition =
                transform.position;

            _dashLandingRootPosition =
                GetLandingRootPosition();

            BuildBezierPath();

            _dashPathDistance =
                EstimateBezierLength(
                    Mathf.Clamp(
                        settings.bezierLengthSamples,
                        8,
                        64));

            Vector3 finalTangent =
                EvaluateBezierTangent(
                    1f);

            if (finalTangent.sqrMagnitude >
                0.0001f)
            {
                _incomingForwardDirection =
                    finalTangent.normalized;
            }
            else
            {
                Vector3 fallback =
                    _dashLandingRootPosition -
                    _dashStartRootPosition;

                _incomingForwardDirection =
                    fallback.sqrMagnitude > 0.0001f
                        ? fallback.normalized
                        : transform.forward;
            }

            _dashProgress = 0f;

            float initialCurveSpeed =
                EvaluateDashSpeed(
                    0f);

            _dashSpeed =
                Mathf.Max(
                    settings.dashStartSpeed +
                    settings.dashInitialKickSpeed,
                    initialCurveSpeed);

            _dashElapsed = 0f;

            _state =
                SlingState.DashingToNode;

            if (settings.enableSlingFovProfile &&
                speedFeedback != null)
            {
                speedFeedback.SetFovOverride(
                    settings.dashFov,
                    settings.dashFovResponse);
            }

            if (settings.enableDashStartCameraKick &&
                orbitCamera != null)
            {
                orbitCamera.AddRewardImpulse(
                    _trackedTarget.LandingPosition,
                    1f,
                    settings.dashCameraKickBack,
                    settings.dashCameraKickUp,
                    settings.dashCameraKickRoll,
                    settings.dashCameraKickRecovery);
            }

            NodeDashStarted?.Invoke(
                _trackedTarget);
        }

        private void UpdateDashToNode()
        {
            if (!IsTrackedTargetAvailable())
            {
                CancelSling();
                return;
            }

            float dt =
                Time.deltaTime;

            if (dt <= 0f)
                return;

            _dashElapsed += dt;

            if (_dashElapsed >=
                settings.maximumDashDuration)
            {
                CancelSling();
                return;
            }

            UpdateTrackedNodeFacing();

            Vector3 directToLanding =
                _bezierP3 -
                transform.position;

            if (directToLanding.magnitude <=
                settings.landingTolerance)
            {
                ResolveNodeImpact(
                    _bezierP3);
                return;
            }

            float targetSpeed =
                EvaluateDashSpeed(
                    _dashProgress);

            float accelerationMultiplier =
                _dashProgress >=
                    settings.dashArrivalAccelerationStart
                    ? settings
                        .dashArrivalAccelerationMultiplier
                    : 1f;

            _dashSpeed =
                Mathf.MoveTowards(
                    _dashSpeed,
                    targetSpeed,
                    settings.dashAcceleration *
                    accelerationMultiplier *
                    dt);

            float progressStep =
                (_dashSpeed * dt) /
                Mathf.Max(
                    0.01f,
                    _dashPathDistance);

            float nextProgress =
                Mathf.Clamp01(
                    _dashProgress +
                    progressStep);

            Vector3 desiredPathRoot =
                EvaluateBezier(
                    nextProgress);

            Vector3 correction =
                desiredPathRoot -
                transform.position;

            controller.Move(
                correction);

            _dashProgress =
                nextProgress;

            if (_hasPreviousPosition)
            {
                Vector3 actualFrameVelocity =
                    (transform.position -
                     _previousPosition) /
                    dt;

                motor.ReportExternalTraversalVelocity(
                    actualFrameVelocity);
            }

            UpdateCurveBank();
            UpdateSlingFovDuringDash();

            Vector3 remaining =
                _bezierP3 -
                transform.position;

            if (remaining.magnitude <=
                    settings.landingTolerance ||
                _dashProgress >= 1f)
            {
                ResolveNodeImpact(
                    _bezierP3);
            }
        }

        private void ResolveNodeImpact(
            Vector3 desiredRoot)
        {
            AirSlingNode target =
                _trackedTarget;

            if (target == null)
            {
                CancelSling();
                return;
            }

            controller.Move(
                desiredRoot -
                transform.position);

            // Clean planted impact frame before the second reward.
            motor.StopForTraversalImpact();

            // Generic particle + impact audio happen on every contact.
            target.PlayImpactVFX();
            target.PlayImpactAudio(
                impactAudioLibrary);

            bool releaseWasPerfect =
                _capturedPerfectQuality > 0f;

            int impactDamage =
                releaseWasPerfect
                    ? settings.perfectReleaseDamage
                    : settings.normalReleaseDamage;

            target.ApplyImpactDamage(
                impactDamage);

            NodeImpact?.Invoke(
                target);

            if (settings.enableNodeImpactTimeStop &&
                perfectTimingFeedback != null)
            {
                perfectTimingFeedback
                    .TriggerPerfectFeedback(
                        1f,
                        settings
                            .nodeImpactTimeStopDuration,
                        settings
                            .nodeImpactTimeStopRecovery,
                        settings
                            .nodeImpactTimeScale);
            }

            if (movementVisuals != null)
            {
                movementVisuals.ClearTraversalBank(
                    settings.curveBankResponse);
            }

            if (settings.enableSlingFovProfile &&
                speedFeedback != null)
            {
                speedFeedback.PulseFovOverride(
                    settings.launchFov,
                    settings.launchFovResponse,
                    settings.launchFovHoldDuration,
                    settings.launchFovRecoveryResponse);
            }

            ApplyFinalForwardReward();

            if (settings.enableFinalLaunchFlip &&
                movementVisuals != null)
            {
                movementVisuals
                    .TriggerTraversalFlip(
                        settings
                            .finalLaunchFlipIntensity);
            }

            _state =
                SlingState.Idle;

            _trackedTarget = null;

            _dashElapsed = 0f;
            _dashSpeed = 0f;
            _dashProgress = 0f;
            _delayRemaining = 0f;

            target.SetSelected(
                false);

            target.SetInteractionActive(
                false);

            float releaseQuality =
                releaseWasPerfect
                    ? _capturedPerfectQuality
                    : 0f;

            SlingLaunched?.Invoke(
                target,
                releaseQuality,
                releaseWasPerfect);

            if (releaseWasPerfect)
            {
                PerfectSlingLaunched?.Invoke(
                    target,
                    releaseQuality);
            }
        }

        private void ApplyFinalForwardReward()
        {
            Vector3 direction =
                BuildFinalForwardDirection();

            motor.AddRewardPlanarBoost(
                direction *
                settings.finalPlanarBoostImpulse,
                settings.finalMaxBoostedSpeed,
                settings.finalBoostHoldDuration,
                settings.finalBoostDecayPerSecond);

            motor.ApplyForcedAerialFlowReward(
                settings.finalRisingLift,
                settings.finalFallingLift,
                settings.finalFallingCancelFraction,
                settings.finalMaxUpwardSpeed,
                settings.finalGravityScale,
                settings.finalGravityDuration,
                settings.finalSteeringMultiplier,
                settings.finalSteeringDuration);
        }

        private Vector3 BuildFinalForwardDirection()
        {
            Vector3 forward =
                _incomingForwardDirection;

            forward.y = 0f;

            if (forward.sqrMagnitude <
                0.001f)
            {
                forward =
                    GetRegularRewardDirection(
                        0.75f);
            }
            else
            {
                forward.Normalize();
            }

            if (targetCamera == null ||
                settings.finalCameraInfluence <= 0f)
            {
                return forward;
            }

            Vector3 cameraForward =
                targetCamera.transform.forward;

            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude <
                0.001f)
            {
                return forward;
            }

            cameraForward.Normalize();

            Vector3 blended =
                Vector3.Slerp(
                    forward,
                    cameraForward,
                    settings.finalCameraInfluence);

            if (Vector3.Dot(
                    blended.normalized,
                    forward) <
                settings.minimumForwardDot)
            {
                return forward;
            }

            return blended.normalized;
        }

        private Vector3 GetRegularRewardDirection(
            float minimumTravelSpeed)
        {
            Vector3 planarVelocity =
                motor.PlanarVelocity;

            planarVelocity.y = 0f;

            if (planarVelocity.magnitude >=
                minimumTravelSpeed)
            {
                return planarVelocity.normalized;
            }

            if (targetCamera != null)
            {
                Vector3 cameraForward =
                    targetCamera.transform.forward;

                cameraForward.y = 0f;

                if (cameraForward.sqrMagnitude >
                    0.0001f)
                {
                    return cameraForward.normalized;
                }
            }

            Vector3 fallback =
                transform.forward;

            fallback.y = 0f;

            return
                fallback.sqrMagnitude >
                0.0001f
                    ? fallback.normalized
                    : Vector3.forward;
        }

        private float EvaluatePerfectQuality(
            float chargeSeconds)
        {
            float distance =
                Mathf.Abs(
                    chargeSeconds -
                    GetPerfectTime());

            if (distance >
                GetPerfectLeeway())
            {
                return 0f;
            }

            return
                1f -
                Mathf.Clamp01(
                    distance /
                    Mathf.Max(
                        0.001f,
                        GetPerfectLeeway()));
        }

        private float GetPerfectTime()
        {
            return
                settings != null
                    ? settings.perfectTime
                    : 0.30f;
        }

        private float GetFullTimingTime()
        {
            return
                settings != null
                    ? settings.fullTimingTime
                    : 0.60f;
        }

        private float GetPerfectLeeway()
        {
            return
                settings != null
                    ? settings.perfectLeeway
                    : 0.09f;
        }

        private void BuildBezierPath()
        {
            _bezierP0 =
                _dashStartRootPosition;

            _bezierP3 =
                _dashLandingRootPosition;

            Vector3 direct =
                _bezierP3 -
                _bezierP0;

            Vector3 directDirection =
                direct.sqrMagnitude > 0.0001f
                    ? direct.normalized
                    : transform.forward;

            Vector3 directPlanar =
                directDirection;

            directPlanar.y = 0f;

            if (directPlanar.sqrMagnitude <
                0.0001f)
            {
                directPlanar =
                    transform.forward;
                directPlanar.y = 0f;
            }

            directPlanar.Normalize();

            Vector3 incoming =
                motor != null
                    ? motor.PlanarVelocity
                    : Vector3.zero;

            incoming.y = 0f;

            if (incoming.sqrMagnitude <
                0.25f)
            {
                incoming =
                    directPlanar;
            }
            else
            {
                incoming.Normalize();
            }

            Vector3 startDirection =
                (directPlanar +
                 incoming *
                 settings.startMomentumInfluence);

            if (startDirection.sqrMagnitude <
                0.0001f)
            {
                startDirection =
                    directPlanar;
            }

            startDirection.Normalize();

            Vector3 side =
                targetCamera != null
                    ? targetCamera.transform.right
                    : Vector3.Cross(
                        Vector3.up,
                        directPlanar);

            side.y = 0f;

            if (side.sqrMagnitude <
                0.0001f)
            {
                side =
                    Vector3.Cross(
                        Vector3.up,
                        directPlanar);
            }

            side.Normalize();

            float sideDot =
                Vector3.Dot(
                    incoming,
                    side);

            float sideSign =
                Mathf.Abs(sideDot) > 0.08f
                    ? Mathf.Sign(sideDot)
                    : 1f;

            Vector3 lateral =
                side *
                sideSign *
                settings.bezierLateralOffset;

            _bezierP1 =
                _bezierP0 +
                startDirection *
                settings.startTangentDistance +
                Vector3.up *
                settings.bezierVerticalLift +
                lateral;

            _bezierP2 =
                _bezierP3 -
                directPlanar *
                settings.approachTangentDistance +
                Vector3.up *
                (settings.bezierVerticalLift *
                 0.55f) +
                lateral *
                0.55f;
        }

        private Vector3 EvaluateBezier(
            float t)
        {
            t =
                Mathf.Clamp01(
                    t);

            float u =
                1f -
                t;

            return
                u * u * u * _bezierP0 +
                3f * u * u * t * _bezierP1 +
                3f * u * t * t * _bezierP2 +
                t * t * t * _bezierP3;
        }

        private Vector3 EvaluateBezierTangent(
            float t)
        {
            t =
                Mathf.Clamp01(
                    t);

            float u =
                1f -
                t;

            Vector3 tangent =
                3f * u * u *
                    (_bezierP1 - _bezierP0) +
                6f * u * t *
                    (_bezierP2 - _bezierP1) +
                3f * t * t *
                    (_bezierP3 - _bezierP2);

            return tangent;
        }

        private float EstimateBezierLength(
            int samples)
        {
            samples =
                Mathf.Max(
                    2,
                    samples);

            float length = 0f;

            Vector3 previous =
                EvaluateBezier(
                    0f);

            for (int i = 1;
                 i <= samples;
                 i++)
            {
                float t =
                    i /
                    (float)samples;

                Vector3 point =
                    EvaluateBezier(
                        t);

                length +=
                    Vector3.Distance(
                        previous,
                        point);

                previous =
                    point;
            }

            return Mathf.Max(
                0.01f,
                length);
        }

        private float EvaluateDashSpeed(
            float progress01)
        {
            float multiplier =
                settings.dashSpeedCurve != null
                    ? settings.dashSpeedCurve.Evaluate(
                        Mathf.Clamp01(
                            progress01))
                    : 1f;

            multiplier =
                Mathf.Max(
                    0.05f,
                    multiplier);

            return
                settings.dashMaximumSpeed *
                multiplier;
        }

        private void UpdateCurveBank()
        {
            if (movementVisuals == null)
                return;

            if (!settings.enablePlayerCurveBank)
            {
                movementVisuals.ClearTraversalBank(
                    settings.curveBankResponse);

                return;
            }

            float previewT =
                Mathf.Clamp01(
                    _dashProgress +
                    settings.curveBankPreview);

            Vector3 tangent =
                EvaluateBezierTangent(
                    _dashProgress);

            Vector3 preview =
                EvaluateBezierTangent(
                    previewT);

            tangent.y = 0f;
            preview.y = 0f;

            if (tangent.sqrMagnitude <
                    0.0001f ||
                preview.sqrMagnitude <
                    0.0001f)
            {
                movementVisuals.ClearTraversalBank(
                    settings.curveBankResponse);

                return;
            }

            float signedAngle =
                Vector3.SignedAngle(
                    tangent.normalized,
                    preview.normalized,
                    Vector3.up);

            float bank01 =
                Mathf.Clamp(
                    signedAngle /
                    Mathf.Max(
                        1f,
                        settings.curveAngleForMaximumBank),
                    -1f,
                    1f);

            float bankDegrees =
                -bank01 *
                settings.maximumPlayerCurveBankDegrees;

            movementVisuals.SetTraversalBank(
                bankDegrees,
                settings.curveBankResponse);
        }

        private void UpdateSlingFovDuringDash()
        {
            if (!settings.enableSlingFovProfile ||
                speedFeedback == null)
            {
                return;
            }

            if (_dashProgress <
                settings.impactFovCompressionStart)
            {
                speedFeedback.SetFovOverride(
                    settings.dashFov,
                    settings.dashFovResponse);

                return;
            }

            float compression01 =
                Mathf.InverseLerp(
                    settings.impactFovCompressionStart,
                    1f,
                    _dashProgress);

            float targetFov =
                Mathf.Lerp(
                    settings.dashFov,
                    settings.impactCompressionFov,
                    compression01);

            speedFeedback.SetFovOverride(
                targetFov,
                settings.impactCompressionFovResponse);
        }

        private void UpdateTrackedNodeFacing()
        {
            if (!settings.makeNodeVisualFacePlayerDuringSling ||
                _trackedTarget == null)
            {
                return;
            }

            AirSlingNodeVisualAnimator visualAnimator =
                _trackedTarget
                    .GetComponent<AirSlingNodeVisualAnimator>();

            if (visualAnimator == null)
                return;

            visualAnimator.FaceWorldPosition(
                transform.position,
                settings
                    .nodeVisualTurnSpeedDegreesPerSecond,
                Time.unscaledDeltaTime);
        }

        private Vector3 GetLandingRootPosition()
        {
            Vector3 landing =
                _trackedTarget.LandingPosition;

            float rootToFeet =
                controller.bounds.min.y -
                transform.position.y;

            return new Vector3(
                landing.x,
                landing.y -
                rootToFeet,
                landing.z);
        }

        private bool TrySpendAirPower()
        {
            if (!settings.useAirPower ||
                settings.airPowerCost <= 0f)
            {
                return true;
            }

            if (airPower == null)
                return !settings.requireEnoughAirPower;

            if (settings.requireEnoughAirPower &&
                airPower.Current <
                settings.airPowerCost)
            {
                return false;
            }

            airPower.Spend(
                settings.airPowerCost);

            return true;
        }

        public void CancelSling()
        {
            AirSlingNode previous =
                _trackedTarget;

            ForceIdleState();

            if (previous != null)
            {
                previous.SetSelected(
                    false);

                previous.SetInteractionActive(
                    false);

                previous.PlayIdleAnimation();
            }

            if (movementVisuals != null)
            {
                movementVisuals.ClearTraversalBank(
                    settings != null
                        ? settings.curveBankResponse
                        : 15f);
            }

            if (speedFeedback != null)
            {
                speedFeedback.ClearFovOverride(
                    settings != null
                        ? settings.launchFovRecoveryResponse
                        : 7f);
            }

            SlingCanceled?.Invoke();
        }

        private void ForceIdleState()
        {
            _state =
                SlingState.Idle;

            _trackedTarget = null;

            _chargeElapsed = 0f;
            _capturedPerfectQuality = 0f;
            _delayRemaining = 0f;

            _dashSpeed = 0f;
            _dashElapsed = 0f;
            _dashProgress = 0f;
            _dashPathDistance = 0f;

            _incomingForwardDirection =
                Vector3.zero;

            _bezierP0 = Vector3.zero;
            _bezierP1 = Vector3.zero;
            _bezierP2 = Vector3.zero;
            _bezierP3 = Vector3.zero;

            if (movementVisuals != null)
            {
                movementVisuals.ClearTraversalBank(
                    settings != null
                        ? settings.curveBankResponse
                        : 15f);
            }

            if (speedFeedback != null)
            {
                speedFeedback.ClearFovOverride(
                    settings != null
                        ? settings.launchFovRecoveryResponse
                        : 7f);
            }

            if (motor != null)
            {
                motor.ClearExternalTraversalVelocity();
            }
        }

        private bool IsTrackedTargetAvailable()
        {
            return
                _trackedTarget != null &&
                _trackedTarget.IsAvailable;
        }

        private void FinishFrame()
        {
            _previousPosition =
                transform.position;

            _hasPreviousPosition = true;
        }

        private void OnDisable()
        {
            CancelSling();
            _hasPreviousPosition = false;
        }
    }
}
