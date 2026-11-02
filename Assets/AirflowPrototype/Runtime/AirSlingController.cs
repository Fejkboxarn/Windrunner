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

        private AirSlingNode _trackedTarget;

        // Batch 5I: all phase and numerical substate are owned by one regular
        // C# object. This component remains the Unity/physics/event adapter.
        private AirSlingFlowState _flowState;
        private AirSlingFlowState FlowState =>
            _flowState ?? (_flowState = new AirSlingFlowState());

        private AirSlingChargeState ChargeState => FlowState.Charge;
        private AirSlingDashProgression DashProgression => FlowState.Dash;

        private Vector3 _dashStartRootPosition;
        private Vector3 _dashLandingRootPosition;

        // The path is a regular C# object; no additional Unity component is needed.
        private AirSlingBezierPath _bezierPath;
        private AirSlingBezierPath BezierPath =>
            _bezierPath ?? (_bezierPath = new AirSlingBezierPath());

        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;

        // Direction from the player INTO the node. The final launch continues
        // forward through the node instead of bouncing backward.
        private Vector3 _incomingForwardDirection;

        public bool IsCharging => FlowState.IsCharging;

        public bool IsSlinging => FlowState.IsSlinging;

        public bool IsDashingToNode => FlowState.IsDashingToNode;

        public AirSlingNode CurrentTarget =>
            FlowState.IsSlinging &&
            IsTrackedTargetAvailable()
                ? _trackedTarget
                : slingTargeter != null
                    ? slingTargeter.CurrentTarget
                    : null;

        public AirSlingNode DisplayTarget =>
            FlowState.IsSlinging
                ? _trackedTarget
                : slingTargeter != null
                    ? slingTargeter.CurrentTarget
                    : null;

        public float TimingProgress01
        {
            get
            {
                if (!FlowState.IsCharging)
                {
                    return 0f;
                }

                float fullTime =
                    GetFullTimingTime();

                return ChargeState.Progress01(fullTime);
            }
        }

        public float PerfectCenter01
        {
            get
            {
                float fullTime =
                    GetFullTimingTime();

                return AirSlingChargeState.PerfectCenter01(fullTime, GetPerfectTime());
            }
        }

        public float PerfectHalfWidth01
        {
            get
            {
                float fullTime =
                    GetFullTimingTime();

                return AirSlingChargeState.PerfectHalfWidth01(fullTime, GetPerfectLeeway());
            }
        }

        public bool IsPerfectWindow => EvaluatePerfectQuality() > 0f;

        public AirSlingSettings Settings =>
            settings;

        public event Action<AirSlingNode> SlingStarted;
        public event Action<AirSlingNode, float, bool> SlingCommitted;
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

            switch (FlowState.Phase)
            {
                case AirSlingPhase.Idle:
                    UpdateIdle(mouse);
                    break;

                case AirSlingPhase.Charging:
                    UpdateCharging(mouse);
                    break;

                case AirSlingPhase.DelayBeforeDash:
                    UpdateDelayBeforeDash();
                    break;

                case AirSlingPhase.DashingToNode:
                    UpdateDashToNode();
                    break;

                case AirSlingPhase.ImpactHold:
                    UpdateImpactHold();
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
            FlowState.BeginCharge();

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
            AirSlingChargeCommand command = FlowState.DecideCharging(
                IsTrackedTargetAvailable(),
                mouse != null,
                mouse != null && mouse.rightButton.isPressed,
                mouse != null && mouse.rightButton.wasReleasedThisFrame);

            switch (command)
            {
                case AirSlingChargeCommand.Cancel:
                    CancelSling();
                    return;

                case AirSlingChargeCommand.Accumulate:
                    ChargeState.AdvanceCharge(Time.deltaTime);
                    return;

                case AirSlingChargeCommand.Release:
                    ReleaseCharge();
                    return;
            }
        }

        private void ReleaseCharge()
        {
            float perfectQuality =
                EvaluatePerfectQuality();

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

            ChargeState.CapturePerfectQuality(perfect ? perfectQuality : 0f);

            SlingCommitted?.Invoke(
                _trackedTarget,
                perfect
                    ? perfectQuality
                    : 0f,
                perfect);

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

            FlowState.BeginDelayBeforeDash(settings.delayBeforeNodeDash);

            if (ChargeState.DelayRemaining <= 0f)
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

            ChargeState.AdvanceDelay(Time.deltaTime);

            if (ChargeState.DelayRemaining <= 0f)
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

            DashProgression.SetPathDistance(
                EstimateBezierLength(
                    Mathf.Clamp(
                        settings.bezierLengthSamples,
                        8,
                        64)));

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

            FlowState.BeginDash(settings);

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

            DashProgression.AdvanceElapsed(dt);

            if (DashProgression.HasTimedOut(settings.maximumDashDuration))
            {
                CancelSling();
                return;
            }

            UpdateTrackedNodeFacing();

            Vector3 directToLanding =
                BezierPath.End -
                transform.position;

            if (directToLanding.magnitude <=
                settings.landingTolerance)
            {
                ResolveNodeImpact(
                    BezierPath.End);
                return;
            }

            float nextProgress = DashProgression.PreviewNextProgress(dt, settings);

            Vector3 desiredPathRoot =
                EvaluateBezier(
                    nextProgress);

            Vector3 correction =
                desiredPathRoot -
                transform.position;

            controller.Move(
                correction);

            DashProgression.CommitProgress(nextProgress);

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
                BezierPath.End -
                transform.position;

            if (remaining.magnitude <=
                    settings.landingTolerance ||
                DashProgression.Progress >= 1f)
            {
                ResolveNodeImpact(
                    BezierPath.End);
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

            FlowState.CaptureImpactRoot(transform.position);

            // Clean planted impact frame before the second reward.
            motor.StopForTraversalImpact();

            // Contact gameplay happens immediately. Only the final launch is delayed
            // so the authored Sling Impact pose has time to read.
            target.PlayImpactVFX();
            target.PlayImpactAudio(
                impactAudioLibrary);

            bool releaseWasPerfect =
                ChargeState.CapturedPerfectQuality > 0f;

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

            FlowState.SetImpactHold(settings.impactPoseHoldDuration);

            if (FlowState.ShouldLaunchImmediately)
            {
                CompleteImpactLaunch();
                return;
            }

            FlowState.EnterImpactHold();
        }

        private void UpdateImpactHold()
        {
            if (_trackedTarget == null)
            {
                CancelSling();
                return;
            }

            // The PlayerMotor still updates normally, so counter its displacement
            // and keep the character planted on the target during the impact pose.
            controller.Move(
                FlowState.ImpactRootPosition -
                transform.position);

            motor.StopForTraversalImpact();

            if (FlowState.AdvanceImpactHold(Time.unscaledDeltaTime))
            {
                CompleteImpactLaunch();
            }
        }

        private void CompleteImpactLaunch()
        {
            AirSlingNode target =
                _trackedTarget;

            if (target == null)
            {
                CancelSling();
                return;
            }

            bool releaseWasPerfect =
                ChargeState.CapturedPerfectQuality > 0f;

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

            FlowState.CompleteLaunch();
            _trackedTarget = null;

            target.SetSelected(
                false);

            target.SetInteractionActive(
                false);

            float releaseQuality =
                releaseWasPerfect
                    ? ChargeState.CapturedPerfectQuality
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

        private float EvaluatePerfectQuality() =>
            ChargeState.EvaluatePerfectQuality(GetPerfectTime(), GetPerfectLeeway());

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
            BezierPath.Build(
                _dashStartRootPosition,
                _dashLandingRootPosition,
                transform.forward,
                motor != null ? motor.PlanarVelocity : Vector3.zero,
                targetCamera != null,
                targetCamera != null ? targetCamera.transform.right : Vector3.zero,
                settings);
        }

        private Vector3 EvaluateBezier(float t) => BezierPath.Evaluate(t);

        private Vector3 EvaluateBezierTangent(float t) => BezierPath.EvaluateTangent(t);

        private float EstimateBezierLength(int samples) => BezierPath.EstimateLength(samples);

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
                    DashProgression.Progress +
                    settings.curveBankPreview);

            Vector3 tangent =
                EvaluateBezierTangent(
                    DashProgression.Progress);

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

            if (DashProgression.Progress <
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
                    DashProgression.Progress);

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
            FlowState.Reset();
            _trackedTarget = null;

            _incomingForwardDirection =
                Vector3.zero;

            BezierPath.Clear();

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
