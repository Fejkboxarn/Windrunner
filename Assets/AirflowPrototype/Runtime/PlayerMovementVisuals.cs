using System;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class PlayerMovementVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private AirflowMovementSettings settings;
        [SerializeField] private AirFeelSettings feelSettings;

        [Header("Procedural Animation")]
        [SerializeField] private CapsuleAnimationSettings animationSettings;
        [SerializeField] private AirFlowHitSystem hitSystem;

        [Header("Animation Hierarchy")]
        [SerializeField] private Transform flipPivot;

        [Header("Optional Custom Clips")]
        [SerializeField] private PlayerCustomAnimationDriver customAnimationDriver;

        [Header("Legacy Procedural Animation Toggles")]
        [Tooltip(
            "Disables the older procedural presentation as a group. " +
            "Authored Animator clips, dual-hand throwing, and gameplay are unaffected.")]
        [SerializeField] private bool disableAllLegacyProceduralAnimation;

        [Tooltip("Disables idle breathing scale/bob.")]
        [SerializeField] private bool disableIdleBreathing;

        [Tooltip("Disables procedural walk/run bob, side sway, pitch and contact squash.")]
        [SerializeField] private bool disableWalkRunWobble;

        [Tooltip("Disables procedural takeoff and landing squash/rebound.")]
        [SerializeField] private bool disableTakeoffLandingSquash;

        [Tooltip("Disables procedural rising/falling stretch and apex squash.")]
        [SerializeField] private bool disableAirborneSquashStretch;

        [Tooltip("Disables speed-driven forward lean and turn lean.")]
        [SerializeField] private bool disableSpeedLean;

        [Tooltip("Disables the visual banking applied while travelling on the curved Sling path.")]
        [SerializeField] private bool disableSlingCurveBank;

        [Tooltip(
            "Disables rotation/scale of the procedural front-flip pivot. " +
            "FlipStarted events still fire so authored animation systems can react.")]
        [SerializeField] private bool disableProceduralFlip;

        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private bool _basePoseCaptured;

        private Quaternion _flipPivotBaseRotation = Quaternion.identity;
        private Vector3 _flipPivotBaseScale = Vector3.one;
        private bool _flipPivotCaptured;

        private Vector3 _lastTravelDirection;

        private float _traversalBankTarget;
        private float _traversalBankCurrent;
        private float _traversalBankResponse = 15f;
        private float _smoothedLeanPitch;
        private float _smoothedLeanRoll;

        private float _locomotionPhase;
        private int _lastFootstepBeat;
        private bool _wasLocomoting;

        private bool _wasGrounded;
        private float _lastVerticalSpeed;

        private float _takeoffRemaining;
        private float _landingRemaining;

        private bool _flipActive;
        private float _flipElapsed;
        private float _activeFlipDuration;

        private Vector3 _smoothedPosition;
        private Vector3 _smoothedScale;

        public event Action<float> Footstep;
        public event Action JumpStarted;
        public event Action<float> Landed;
        public event Action<int, float> FlipStarted;

        public bool LegacyProceduralAnimationEnabled =>
            !disableAllLegacyProceduralAnimation;

        public bool IdleBreathingEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableIdleBreathing;

        public bool WalkRunWobbleEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableWalkRunWobble;

        public bool TakeoffLandingSquashEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableTakeoffLandingSquash;

        public bool AirborneSquashStretchEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableAirborneSquashStretch;

        public bool SpeedLeanEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableSpeedLean;

        public bool SlingCurveBankEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableSlingCurveBank;

        public bool ProceduralFlipEnabled =>
            LegacyProceduralAnimationEnabled &&
            !disableProceduralFlip;

        public void Configure(
            PlayerMotor newMotor,
            Transform newPresentationRoot,
            AirflowMovementSettings newSettings)
        {
            motor = newMotor;
            presentationRoot = newPresentationRoot;
            settings = newSettings;
            CaptureBasePose();
        }

        public void ConfigureFeelSettings(
            AirFeelSettings newFeelSettings)
        {
            feelSettings = newFeelSettings;
        }

        public void ConfigureAnimation(
            CapsuleAnimationSettings newAnimationSettings,
            AirFlowHitSystem newHitSystem)
        {
            UnsubscribeHitSystem();

            animationSettings = newAnimationSettings;
            hitSystem = newHitSystem;

            CaptureBasePose();
            CaptureFlipPivot();

            if (Application.isPlaying &&
                isActiveAndEnabled)
            {
                SubscribeHitSystem();
            }
        }

        public void ConfigureCustomAnimationDriver(
            PlayerCustomAnimationDriver driver)
        {
            customAnimationDriver = driver;
        }

        public void ConfigureFlipPivot(
            Transform newFlipPivot)
        {
            flipPivot = newFlipPivot;
            CaptureFlipPivot();
        }

        public void SetTraversalBank(
            float degrees,
            float response)
        {
            _traversalBankTarget =
                degrees;

            _traversalBankResponse =
                Mathf.Max(
                    0.01f,
                    response);
        }

        public void ClearTraversalBank(
            float response)
        {
            _traversalBankTarget = 0f;

            _traversalBankResponse =
                Mathf.Max(
                    0.01f,
                    response);
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (hitSystem == null)
                hitSystem = GetComponent<AirFlowHitSystem>();

            if (customAnimationDriver == null)
                customAnimationDriver = GetComponent<PlayerCustomAnimationDriver>();

            CaptureBasePose();
            CaptureFlipPivot();

            _wasGrounded =
                motor != null &&
                motor.IsGrounded;

            _lastVerticalSpeed =
                motor != null
                    ? motor.VerticalSpeed
                    : 0f;
        }

        private void OnEnable()
        {
            SubscribeHitSystem();

            if (motor != null)
            {
                _wasGrounded = motor.IsGrounded;
                _lastVerticalSpeed = motor.VerticalSpeed;
            }
        }

        private void OnDisable()
        {
            UnsubscribeHitSystem();

            _traversalBankTarget = 0f;
            _traversalBankCurrent = 0f;

            RestoreBasePositionAndScale();
            RestoreFlipPivot();
        }

        private void SubscribeHitSystem()
        {
            if (hitSystem == null)
                return;

            hitSystem.RewardDelivered -= OnRewardDelivered;
            hitSystem.RewardDelivered += OnRewardDelivered;
        }

        private void UnsubscribeHitSystem()
        {
            if (hitSystem != null)
                hitSystem.RewardDelivered -= OnRewardDelivered;
        }

        private void CaptureBasePose()
        {
            if (presentationRoot == null)
                return;

            _baseLocalPosition = presentationRoot.localPosition;
            _baseLocalScale = presentationRoot.localScale;

            _smoothedPosition = _baseLocalPosition;
            _smoothedScale = _baseLocalScale;

            _basePoseCaptured = true;
        }

        private void CaptureFlipPivot()
        {
            if (flipPivot == null)
                return;

            _flipPivotBaseRotation = flipPivot.localRotation;
            _flipPivotBaseScale = flipPivot.localScale;
            _flipPivotCaptured = true;
        }

        private void LateUpdate()
        {
            if (motor == null ||
                presentationRoot == null ||
                settings == null)
            {
                return;
            }

            if (!_basePoseCaptured)
                CaptureBasePose();

            if (!_flipPivotCaptured &&
                flipPivot != null)
            {
                CaptureFlipPivot();
            }

            float dt = Time.deltaTime;

            if (dt <= 0f)
                return;

            float bankDt =
                Time.unscaledDeltaTime;

            float desiredTraversalBank =
                SlingCurveBankEnabled
                    ? _traversalBankTarget
                    : 0f;

            _traversalBankCurrent =
                Mathf.Lerp(
                    _traversalBankCurrent,
                    desiredTraversalBank,
                    1f -
                    Mathf.Exp(
                        -_traversalBankResponse *
                        bankDt));

            DetectMovementEvents();

            float speed = motor.HorizontalSpeed;

            UpdateLean(
                speed,
                dt,
                out float leanPitch,
                out float leanRoll);

            Vector3 positionOffset = Vector3.zero;
            Vector3 scaleMultiplier = Vector3.one;

            float animationPitch = 0f;
            float animationRoll = 0f;

            if (animationSettings != null)
            {
                ApplyIdleAndLocomotion(
                    speed,
                    dt,
                    ref positionOffset,
                    ref scaleMultiplier,
                    ref animationPitch,
                    ref animationRoll);

                ApplyJumpAndLanding(
                    dt,
                    ref positionOffset,
                    ref scaleMultiplier,
                    ref animationPitch);

                UpdateFrontFlip(dt);
            }

            Vector3 desiredPosition =
                _baseLocalPosition +
                positionOffset;

            Vector3 desiredScale =
                Vector3.Scale(
                    _baseLocalScale,
                    scaleMultiplier);

            float positionResponse =
                animationSettings != null
                    ? animationSettings.positionResponse
                    : 18f;

            float scaleResponse =
                animationSettings != null
                    ? animationSettings.scaleResponse
                    : 18f;

            float positionT =
                1f -
                Mathf.Exp(
                    -positionResponse *
                    dt);

            float scaleT =
                1f -
                Mathf.Exp(
                    -scaleResponse *
                    dt);

            _smoothedPosition =
                Vector3.Lerp(
                    _smoothedPosition,
                    desiredPosition,
                    positionT);

            _smoothedScale =
                Vector3.Lerp(
                    _smoothedScale,
                    desiredScale,
                    scaleT);

            presentationRoot.localPosition =
                _smoothedPosition;

            presentationRoot.localScale =
                _smoothedScale;

            float currentYaw =
                presentationRoot.localEulerAngles.y;

            presentationRoot.localRotation =
                Quaternion.Euler(
                    leanPitch + animationPitch,
                    currentYaw,
                    leanRoll +
                    animationRoll +
                    _traversalBankCurrent);

            _wasGrounded =
                motor.IsGrounded;

            _lastVerticalSpeed =
                motor.VerticalSpeed;
        }

        private void DetectMovementEvents()
        {
            bool grounded =
                motor.IsGrounded;

            if (_wasGrounded &&
                !grounded &&
                motor.VerticalSpeed > 0.1f)
            {
                if (animationSettings != null)
                {
                    _takeoffRemaining =
                        animationSettings.takeoffSquashDuration;
                }

                JumpStarted?.Invoke();
            }

            if (!_wasGrounded &&
                grounded)
            {
                float landingIntensity =
                    Mathf.InverseLerp(
                        1.5f,
                        11f,
                        Mathf.Abs(
                            Mathf.Min(
                                0f,
                                _lastVerticalSpeed)));

                if (animationSettings != null)
                {
                    _landingRemaining =
                        animationSettings.landingSquashDuration;
                }

                if (_flipActive)
                    EndFlip();

                Landed?.Invoke(
                    landingIntensity);
            }
        }

        private void UpdateLean(
            float speed,
            float dt,
            out float pitch,
            out float roll)
        {
            float desiredPitch;
            float desiredRoll = 0f;
            float response;

            if (!SpeedLeanEnabled)
            {
                desiredPitch = 0f;
                desiredRoll = 0f;

                response =
                    feelSettings != null
                        ? feelSettings.characterLeanResponse
                        : settings.bodyLeanResponse;

                float disabledT =
                    1f -
                    Mathf.Exp(
                        -Mathf.Max(
                            0.01f,
                            response) *
                        dt);

                _smoothedLeanPitch =
                    Mathf.Lerp(
                        _smoothedLeanPitch,
                        0f,
                        disabledT);

                _smoothedLeanRoll =
                    Mathf.Lerp(
                        _smoothedLeanRoll,
                        0f,
                        disabledT);

                pitch = _smoothedLeanPitch;
                roll = _smoothedLeanRoll;
                return;
            }

            if (feelSettings != null)
            {
                float forwardLean01 =
                    Mathf.InverseLerp(
                        feelSettings.characterForwardLeanStartSpeed,
                        feelSettings.maxFeedbackSpeed,
                        speed);

                desiredPitch =
                    feelSettings.forwardLeanAtMaxSpeed *
                    forwardLean01;

                Vector3 velocity =
                    motor.PlanarVelocity;

                velocity.y = 0f;

                Vector3 travelDirection =
                    velocity.sqrMagnitude > 0.04f
                        ? velocity.normalized
                        : Vector3.zero;

                float turnLeanSpeedInfluence =
                    Mathf.InverseLerp(
                        feelSettings.characterTurnLeanStartSpeed,
                        feelSettings.characterTurnLeanFullSpeed,
                        speed);

                if (turnLeanSpeedInfluence > 0f &&
                    travelDirection.sqrMagnitude > 0.01f &&
                    _lastTravelDirection.sqrMagnitude > 0.01f)
                {
                    float signedAngle =
                        Vector3.SignedAngle(
                            _lastTravelDirection,
                            travelDirection,
                            Vector3.up);

                    float degreesPerSecond =
                        signedAngle /
                        Mathf.Max(
                            0.0001f,
                            dt);

                    float turn01 =
                        Mathf.Clamp(
                            degreesPerSecond /
                            feelSettings.turnRateForMaxCharacterLean,
                            -1f,
                            1f);

                    desiredRoll =
                        -turn01 *
                        feelSettings.maxCharacterTurnLean *
                        turnLeanSpeedInfluence;
                }

                if (travelDirection.sqrMagnitude > 0.01f)
                {
                    _lastTravelDirection =
                        travelDirection;
                }

                response =
                    feelSettings.characterLeanResponse;
            }
            else
            {
                float normalSpeed =
                    Mathf.Max(
                        0.01f,
                        settings.maxGroundSpeed);

                float dashRange =
                    Mathf.Max(
                        0.01f,
                        settings.dashMaxSpeed -
                        normalSpeed);

                float excess01 =
                    Mathf.Clamp01(
                        (speed - normalSpeed) /
                        dashRange);

                desiredPitch =
                    settings.dashBodyLeanDegrees *
                    excess01;

                response =
                    settings.bodyLeanResponse;
            }

            float t =
                1f -
                Mathf.Exp(
                    -response *
                    dt);

            _smoothedLeanPitch =
                Mathf.Lerp(
                    _smoothedLeanPitch,
                    desiredPitch,
                    t);

            _smoothedLeanRoll =
                Mathf.Lerp(
                    _smoothedLeanRoll,
                    desiredRoll,
                    t);

            pitch = _smoothedLeanPitch;
            roll = _smoothedLeanRoll;
        }

        private void ApplyIdleAndLocomotion(
            float speed,
            float dt,
            ref Vector3 positionOffset,
            ref Vector3 scaleMultiplier,
            ref float animationPitch,
            ref float animationRoll)
        {
            bool grounded =
                motor.IsGrounded;

            float move01 =
                grounded
                    ? Mathf.InverseLerp(
                        animationSettings.locomotionStartSpeed,
                        animationSettings.fullRunAnimationSpeed,
                        speed)
                    : 0f;

            bool useProceduralIdle =
                customAnimationDriver == null ||
                !customAnimationDriver.HasCustomIdle;

            float idleWeight =
                grounded &&
                useProceduralIdle &&
                IdleBreathingEnabled
                    ? 1f -
                      Mathf.Clamp01(
                          speed /
                          Mathf.Max(
                              0.01f,
                              animationSettings.locomotionStartSpeed * 1.5f))
                    : 0f;

            if (idleWeight > 0f)
            {
                float breath =
                    Mathf.Sin(
                        Time.time *
                        animationSettings.breathingCyclesPerSecond *
                        Mathf.PI *
                        2f);

                float breathScale =
                    breath *
                    animationSettings.breathingScale *
                    idleWeight;

                scaleMultiplier.y +=
                    breathScale;

                scaleMultiplier.x -=
                    breathScale * 0.28f;

                scaleMultiplier.z -=
                    breathScale * 0.28f;

                positionOffset.y +=
                    breath *
                    animationSettings.breathingBob *
                    idleWeight;
            }

            bool locomoting =
                grounded &&
                speed >
                animationSettings.locomotionStartSpeed;

            if (!locomoting)
            {
                _wasLocomoting = false;
                return;
            }

            float cadence =
                Mathf.Lerp(
                    animationSettings.walkCyclesPerSecond,
                    animationSettings.runCyclesPerSecond,
                    move01);

            _locomotionPhase +=
                cadence *
                Mathf.PI *
                2f *
                dt;

            if (!_wasLocomoting)
            {
                _lastFootstepBeat =
                    Mathf.FloorToInt(
                        _locomotionPhase /
                        Mathf.PI);

                _wasLocomoting = true;
            }

            int footstepBeat =
                Mathf.FloorToInt(
                    _locomotionPhase /
                    Mathf.PI);

            if (footstepBeat !=
                _lastFootstepBeat)
            {
                _lastFootstepBeat =
                    footstepBeat;

                Footstep?.Invoke(
                    Mathf.Lerp(
                        0.35f,
                        1f,
                        move01));
            }

            bool useProceduralLocomotion =
                customAnimationDriver == null ||
                !customAnimationDriver.HasCustomLocomotion;

            if (!useProceduralLocomotion ||
                !WalkRunWobbleEnabled)
            {
                return;
            }

            float stride =
                Mathf.Sin(
                    _locomotionPhase);

            float contactWave =
                0.5f +
                0.5f *
                Mathf.Cos(
                    _locomotionPhase *
                    2f);

            float liftWave =
                1f -
                contactWave;

            float intensity =
                Mathf.Lerp(
                    0.35f,
                    1f,
                    move01);

            positionOffset.y +=
                liftWave *
                animationSettings.walkRunBob *
                intensity;

            animationRoll +=
                stride *
                animationSettings.walkRunSideSwayDegrees *
                intensity;

            animationPitch +=
                -contactWave *
                animationSettings.walkRunPitchDegrees *
                intensity;

            float squash =
                contactWave *
                animationSettings.walkRunContactSquash *
                intensity;

            scaleMultiplier.y -=
                squash;

            scaleMultiplier.x +=
                squash * 0.40f;

            scaleMultiplier.z +=
                squash * 0.40f;
        }

        private void ApplyJumpAndLanding(
            float dt,
            ref Vector3 positionOffset,
            ref Vector3 scaleMultiplier,
            ref float animationPitch)
        {
            bool customJump =
                customAnimationDriver != null &&
                customAnimationDriver.HasCustomJump;

            bool customFall =
                customAnimationDriver != null &&
                customAnimationDriver.HasCustomFall;

            bool customLand =
                customAnimationDriver != null &&
                customAnimationDriver.HasCustomLand;

            if (_takeoffRemaining > 0f)
            {
                if (!customJump &&
                    TakeoffLandingSquashEnabled)
                {
                    float duration =
                        Mathf.Max(
                            0.02f,
                            animationSettings.takeoffSquashDuration);

                    float progress =
                        1f -
                        _takeoffRemaining /
                        duration;

                    float pulse =
                        Mathf.Sin(
                            Mathf.Clamp01(progress) *
                            Mathf.PI);

                    float squash =
                        animationSettings.takeoffSquash *
                        pulse;

                    scaleMultiplier.y -=
                        squash;

                    scaleMultiplier.x +=
                        squash * 0.50f;

                    scaleMultiplier.z +=
                        squash * 0.50f;

                    positionOffset.y -=
                        squash * 0.20f;
                }

                _takeoffRemaining =
                    Mathf.Max(
                        0f,
                        _takeoffRemaining - dt);
            }

            if (!motor.IsGrounded)
            {
                float vertical =
                    motor.VerticalSpeed;

                float rising01 =
                    Mathf.Clamp01(
                        vertical /
                        animationSettings.verticalSpeedForFullPose);

                float falling01 =
                    Mathf.Clamp01(
                        -vertical /
                        animationSettings.verticalSpeedForFullPose);

                float apex01 =
                    1f -
                    Mathf.Clamp01(
                        Mathf.Abs(vertical) /
                        animationSettings.verticalSpeedForFullPose);

                bool useRisingProcedural =
                    AirborneSquashStretchEnabled &&
                    vertical >= 0f &&
                    !customJump;

                bool useFallingProcedural =
                    AirborneSquashStretchEnabled &&
                    vertical < 0f &&
                    !customFall;

                if (useRisingProcedural)
                {
                    float stretch =
                        animationSettings.risingStretch *
                        rising01;

                    scaleMultiplier.y +=
                        stretch;

                    scaleMultiplier.x -=
                        stretch * 0.28f;

                    scaleMultiplier.z -=
                        stretch * 0.28f;

                    animationPitch -=
                        animationSettings.risingPitchDegrees *
                        rising01;
                }

                if (apex01 > 0f &&
                    (useRisingProcedural ||
                     useFallingProcedural))
                {
                    float apex =
                        animationSettings.apexSquash *
                        apex01;

                    scaleMultiplier.y -=
                        apex;

                    scaleMultiplier.x +=
                        apex * 0.34f;

                    scaleMultiplier.z +=
                        apex * 0.34f;
                }

                if (useFallingProcedural)
                {
                    float stretch =
                        animationSettings.fallingStretch *
                        falling01;

                    scaleMultiplier.y +=
                        stretch;

                    scaleMultiplier.x -=
                        stretch * 0.24f;

                    scaleMultiplier.z -=
                        stretch * 0.24f;

                    animationPitch +=
                        animationSettings.fallingPitchDegrees *
                        falling01;
                }
            }

            if (_landingRemaining > 0f)
            {
                float duration =
                    Mathf.Max(
                        0.02f,
                        animationSettings.landingSquashDuration);

                float progress =
                    1f -
                    _landingRemaining /
                    duration;

                if (!customLand &&
                    TakeoffLandingSquashEnabled)
                {
                    float compression =
                        Mathf.Sin(
                            Mathf.Clamp01(progress) *
                            Mathf.PI);

                    float rebound =
                        Mathf.Sin(
                            Mathf.Clamp01(progress) *
                            Mathf.PI *
                            2f);

                    rebound =
                        Mathf.Max(
                            0f,
                            -rebound);

                    float squash =
                        animationSettings.landingSquash *
                        compression;

                    scaleMultiplier.y -=
                        squash;

                    scaleMultiplier.x +=
                        squash * 0.58f;

                    scaleMultiplier.z +=
                        squash * 0.58f;

                    positionOffset.y -=
                        squash * 0.25f;

                    positionOffset.y +=
                        rebound *
                        animationSettings.landingRecoveryBounce;
                }

                _landingRemaining =
                    Mathf.Max(
                        0f,
                        _landingRemaining - dt);
            }
        }

        private void UpdateFrontFlip(
            float dt)
        {
            if (!_flipActive)
            {
                RestoreFlipPivot();
                return;
            }

            _flipElapsed += dt;

            float t =
                Mathf.Clamp01(
                    _flipElapsed /
                    Mathf.Max(
                        0.1f,
                        _activeFlipDuration));

            bool customFlip =
                customAnimationDriver != null &&
                customAnimationDriver.HasCustomFlip(0);

            if (ProceduralFlipEnabled &&
                !customFlip &&
                flipPivot != null &&
                _flipPivotCaptured)
            {
                float eased =
                    t * t *
                    (3f -
                     2f * t);

                float angle =
                    animationSettings.frontFlipDegrees *
                    eased;

                flipPivot.localRotation =
                    _flipPivotBaseRotation *
                    Quaternion.Euler(
                        angle,
                        0f,
                        0f);

                float tuck =
                    Mathf.Sin(
                        t *
                        Mathf.PI) *
                    animationSettings.flipTuckSquash;

                flipPivot.localScale =
                    new Vector3(
                        _flipPivotBaseScale.x *
                            (1f + tuck * 0.35f),
                        _flipPivotBaseScale.y *
                            (1f - tuck),
                        _flipPivotBaseScale.z *
                            (1f + tuck * 0.35f));
            }

            if (t >= 1f)
                EndFlip();
        }

        private void OnRewardDelivered(
            AirNode node,
            float intensity)
        {
            if (animationSettings == null ||
                motor == null ||
                motor.IsGrounded ||
                intensity <
                animationSettings.minimumFlipRewardIntensity)
            {
                return;
            }

            TriggerTraversalFlip(
                intensity);
        }

        /// <summary>
        /// Starts the same procedural/custom front flip used by airborne
        /// Air Node rewards, but can be called by authored traversal moments
        /// such as a Sling Node impact launch.
        /// </summary>
        public bool TriggerTraversalFlip(
            float intensity = 1f)
        {
            if (animationSettings == null)
                return false;

            intensity =
                Mathf.Max(
                    0f,
                    intensity);

            if (_flipActive)
            {
                float normalized =
                    _flipElapsed /
                    Mathf.Max(
                        0.1f,
                        _activeFlipDuration);

                if (normalized <
                    animationSettings.earliestFlipRestartNormalizedTime)
                {
                    return false;
                }
            }

            _flipElapsed = 0f;
            _activeFlipDuration =
                animationSettings.flipDuration;

            _flipActive = true;

            FlipStarted?.Invoke(
                0,
                intensity);

            return true;
        }

        private void EndFlip()
        {
            _flipActive = false;
            _flipElapsed = 0f;
            RestoreFlipPivot();
        }

        private void RestoreFlipPivot()
        {
            if (flipPivot == null ||
                !_flipPivotCaptured)
            {
                return;
            }

            flipPivot.localRotation =
                _flipPivotBaseRotation;

            flipPivot.localScale =
                _flipPivotBaseScale;
        }

        private void RestoreBasePositionAndScale()
        {
            if (presentationRoot == null ||
                !_basePoseCaptured)
            {
                return;
            }

            presentationRoot.localPosition =
                _baseLocalPosition;

            presentationRoot.localScale =
                _baseLocalScale;
        }
    }
}
