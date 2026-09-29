using UnityEngine;

namespace AirflowPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private CharacterController controller;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private AirflowMovementSettings settings;

        private Vector3 _planarVelocity;
        private Vector3 _actualVelocity;
        private float _verticalVelocity;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressedTime = float.NegativeInfinity;
        private bool _jumpConsumed;
        private bool _grounded;
        private bool _dashActive;

        // External traversal feedback.
        // Systems such as Sling can move the CharacterController after the
        // motor update. Reporting that displacement here makes all existing
        // speed-driven feedback see the real traversal speed.
        private Vector3 _externalTraversalVelocity;
        private int _externalTraversalVelocityFrame = -1000;

        // Reward speed envelope.
        private float _rewardBoostSpeedFloor;
        private float _rewardBoostHoldRemaining;
        private float _rewardBoostDecayPerSecond;

        // Temporary aerial-flow state.
        private float _aerialGravityScale = 1f;
        private float _aerialGravityRemaining;
        private float _aerialSteeringMultiplier = 1f;
        private float _aerialSteeringRemaining;

        public bool HasExternalTraversalVelocity =>
            Time.frameCount -
            _externalTraversalVelocityFrame <= 1;

        public Vector3 Velocity =>
            HasExternalTraversalVelocity
                ? _externalTraversalVelocity
                : _actualVelocity;

        public Vector3 PlanarVelocity
        {
            get
            {
                if (!HasExternalTraversalVelocity)
                    return _planarVelocity;

                Vector3 planar =
                    _externalTraversalVelocity;

                planar.y = 0f;
                return planar;
            }
        }

        public float HorizontalSpeed
        {
            get
            {
                Vector3 velocity =
                    Velocity;

                velocity.y = 0f;
                return velocity.magnitude;
            }
        }

        public float VerticalSpeed =>
            Velocity.y;
        public bool IsGrounded => _grounded;
        public bool IsDashActive => _dashActive;

        public bool HasRewardSpeedBoost => _rewardBoostSpeedFloor > 0.01f;
        public float RewardBoostSpeedFloor => _rewardBoostSpeedFloor;
        public float RewardBoostHoldRemaining => _rewardBoostHoldRemaining;

        public bool HasAerialFlow =>
            !_grounded &&
            (_aerialGravityRemaining > 0f ||
             _aerialSteeringRemaining > 0f);

        public float AerialGravityRemaining => _aerialGravityRemaining;
        public float AerialSteeringRemaining => _aerialSteeringRemaining;

        public AirflowMovementSettings Settings => settings;

        public float CoyoteRemaining =>
            Mathf.Max(
                0f,
                settings != null
                    ? settings.coyoteTime - (Time.time - _lastGroundedTime)
                    : 0f);

        public float JumpBufferRemaining =>
            Mathf.Max(
                0f,
                settings != null
                    ? settings.jumpBufferTime - (Time.time - _lastJumpPressedTime)
                    : 0f);

        public void Configure(
            CharacterController newController,
            PlayerInputReader newInput,
            Transform newCameraTransform,
            Transform newVisualRoot,
            AirflowMovementSettings newSettings)
        {
            controller = newController;
            input = newInput;
            cameraTransform = newCameraTransform;
            visualRoot = newVisualRoot;
            settings = newSettings;
        }

        public void SetDashActive(bool active)
        {
            _dashActive = active;
        }

        /// <summary>
        /// Reports movement applied outside PlayerMotor.Update so existing
        /// speed/FOV/wind/lean/audio systems see the actual traversal velocity.
        /// Report once per frame while the external traversal is active.
        /// </summary>
        public void ReportExternalTraversalVelocity(
            Vector3 velocity)
        {
            _externalTraversalVelocity =
                velocity;

            _externalTraversalVelocityFrame =
                Time.frameCount;
        }

        public void ClearExternalTraversalVelocity()
        {
            _externalTraversalVelocity =
                Vector3.zero;

            _externalTraversalVelocityFrame =
                -1000;
        }

        public void AddPlanarImpulse(
            Vector3 impulse,
            float maxResultSpeed = 30f)
        {
            impulse.y = 0f;
            _planarVelocity += impulse;

            float speed = _planarVelocity.magnitude;

            if (maxResultSpeed > 0f &&
                speed > maxResultSpeed)
            {
                _planarVelocity =
                    _planarVelocity.normalized *
                    maxResultSpeed;
            }
        }

        public void AddRewardPlanarBoost(
            Vector3 impulse,
            float maxResultSpeed,
            float holdDuration,
            float decayPerSecond)
        {
            AddPlanarImpulse(
                impulse,
                maxResultSpeed);

            float resultingSpeed =
                _planarVelocity.magnitude;

            _rewardBoostSpeedFloor =
                Mathf.Max(
                    _rewardBoostSpeedFloor,
                    resultingSpeed);

            _rewardBoostHoldRemaining =
                Mathf.Max(
                    _rewardBoostHoldRemaining,
                    Mathf.Max(0f, holdDuration));

            _rewardBoostDecayPerSecond =
                Mathf.Max(
                    0.01f,
                    decayPerSecond);
        }

        public void AddVerticalImpulse(
            float velocityDelta)
        {
            _verticalVelocity +=
                velocityDelta;
        }

        /// <summary>
        /// Applies the complete aerial reward state.
        /// Rising rewards add a smaller lift.
        /// Falling rewards cancel a configurable portion of the fall first,
        /// then apply the stronger falling lift.
        /// </summary>
        public void ApplyAerialFlowReward(
            float risingLift,
            float fallingLift,
            float fallingCancelFraction,
            float maxUpwardSpeed,
            float gravityScale,
            float gravityDuration,
            float steeringMultiplier,
            float steeringDuration)
        {
            ApplyAerialFlowRewardInternal(
                risingLift,
                fallingLift,
                fallingCancelFraction,
                maxUpwardSpeed,
                gravityScale,
                gravityDuration,
                steeringMultiplier,
                steeringDuration,
                false);
        }

        /// <summary>
        /// Same tuned aerial reward as ApplyAerialFlowReward, but intentionally
        /// ignores the grounded guard. Traversal impact points can therefore
        /// plant the controller for one frame and still launch it immediately.
        /// </summary>
        public void ApplyForcedAerialFlowReward(
            float risingLift,
            float fallingLift,
            float fallingCancelFraction,
            float maxUpwardSpeed,
            float gravityScale,
            float gravityDuration,
            float steeringMultiplier,
            float steeringDuration)
        {
            ApplyAerialFlowRewardInternal(
                risingLift,
                fallingLift,
                fallingCancelFraction,
                maxUpwardSpeed,
                gravityScale,
                gravityDuration,
                steeringMultiplier,
                steeringDuration,
                true);
        }

        /// <summary>
        /// Clears movement/reward state for a deliberate traversal impact frame.
        /// Does not move the CharacterController.
        /// </summary>
        public void StopForTraversalImpact()
        {
            _planarVelocity =
                Vector3.zero;

            _verticalVelocity = 0f;
            _actualVelocity =
                Vector3.zero;

            ClearRewardBoost();
            ClearAerialFlow();
            ClearExternalTraversalVelocity();
        }

        private void ApplyAerialFlowRewardInternal(
            float risingLift,
            float fallingLift,
            float fallingCancelFraction,
            float maxUpwardSpeed,
            float gravityScale,
            float gravityDuration,
            float steeringMultiplier,
            float steeringDuration,
            bool forceWhenGrounded)
        {
            if (_grounded &&
                !forceWhenGrounded)
            {
                return;
            }

            bool falling =
                _verticalVelocity < 0f;

            if (falling)
            {
                float cancelFraction =
                    Mathf.Clamp01(
                        fallingCancelFraction);

                _verticalVelocity =
                    Mathf.Lerp(
                        _verticalVelocity,
                        0f,
                        cancelFraction);

                _verticalVelocity +=
                    Mathf.Max(
                        0f,
                        fallingLift);
            }
            else
            {
                _verticalVelocity +=
                    Mathf.Max(
                        0f,
                        risingLift);
            }

            _verticalVelocity =
                Mathf.Min(
                    _verticalVelocity,
                    Mathf.Max(
                        0.1f,
                        maxUpwardSpeed));

            _aerialGravityScale =
                Mathf.Clamp(
                    gravityScale,
                    0.1f,
                    1f);

            _aerialGravityRemaining =
                Mathf.Max(
                    _aerialGravityRemaining,
                    Mathf.Max(
                        0f,
                        gravityDuration));

            _aerialSteeringMultiplier =
                Mathf.Max(
                    1f,
                    steeringMultiplier);

            _aerialSteeringRemaining =
                Mathf.Max(
                    _aerialSteeringRemaining,
                    Mathf.Max(
                        0f,
                        steeringDuration));
        }

        private void Reset()
        {
            controller =
                GetComponent<CharacterController>();

            input =
                GetComponent<PlayerInputReader>();
        }

        private void Awake()
        {
            if (controller == null)
                controller =
                    GetComponent<CharacterController>();

            if (input == null)
                input =
                    GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (controller == null ||
                input == null ||
                cameraTransform == null ||
                settings == null)
            {
                return;
            }

            float dt =
                Time.deltaTime;

            if (dt <= 0f)
                return;

            bool groundedAtFrameStart =
                controller.isGrounded;

            if (groundedAtFrameStart)
            {
                _lastGroundedTime =
                    Time.time;

                _jumpConsumed =
                    false;

                ClearAerialFlow();

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity =
                        -settings.groundedStickForce;
                }
            }
            else
            {
                TickAerialFlow(dt);
            }

            if (input.JumpPressedThisFrame)
            {
                _lastJumpPressedTime =
                    Time.time;
            }

            Vector3 desiredDirection =
                GetCameraRelativeMove(
                    input.Move);

            float inputMagnitude =
                Mathf.Clamp01(
                    desiredDirection.magnitude);

            UpdateRewardBoostEnvelope(
                inputMagnitude,
                dt);

            UpdatePlanarVelocity(
                desiredDirection,
                groundedAtFrameStart,
                dt);

            TryConsumeBufferedJump();
            UpdateGravity(
                groundedAtFrameStart,
                dt);

            Vector3 frameVelocity =
                _planarVelocity +
                Vector3.up *
                _verticalVelocity;

            Vector3 beforeMove =
                transform.position;

            CollisionFlags flags =
                controller.Move(
                    frameVelocity *
                    dt);

            Vector3 afterMove =
                transform.position;

            _actualVelocity =
                (afterMove - beforeMove) /
                dt;

            _grounded =
                controller.isGrounded ||
                (flags &
                 CollisionFlags.Below) != 0;

            if (_grounded)
            {
                _lastGroundedTime =
                    Time.time;

                _jumpConsumed =
                    false;

                ClearAerialFlow();
            }

            UpdateFacing(
                desiredDirection,
                dt);
        }

        private void TickAerialFlow(
            float dt)
        {
            if (_aerialGravityRemaining > 0f)
            {
                _aerialGravityRemaining =
                    Mathf.Max(
                        0f,
                        _aerialGravityRemaining -
                        dt);
            }

            if (_aerialSteeringRemaining > 0f)
            {
                _aerialSteeringRemaining =
                    Mathf.Max(
                        0f,
                        _aerialSteeringRemaining -
                        dt);
            }

            if (_aerialGravityRemaining <= 0f)
                _aerialGravityScale = 1f;

            if (_aerialSteeringRemaining <= 0f)
                _aerialSteeringMultiplier = 1f;
        }

        private void ClearAerialFlow()
        {
            _aerialGravityScale = 1f;
            _aerialGravityRemaining = 0f;
            _aerialSteeringMultiplier = 1f;
            _aerialSteeringRemaining = 0f;
        }

        private void UpdateRewardBoostEnvelope(
            float inputMagnitude,
            float dt)
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
                _rewardBoostHoldRemaining =
                    Mathf.Max(
                        0f,
                        _rewardBoostHoldRemaining -
                        dt);

                return;
            }

            float normalDashSpeed =
                settings != null
                    ? settings.dashMaxSpeed
                    : 0f;

            _rewardBoostSpeedFloor =
                Mathf.MoveTowards(
                    _rewardBoostSpeedFloor,
                    normalDashSpeed,
                    _rewardBoostDecayPerSecond *
                    dt);

            if (_rewardBoostSpeedFloor <=
                normalDashSpeed + 0.01f)
            {
                ClearRewardBoost();
            }
        }

        private void ClearRewardBoost()
        {
            _rewardBoostSpeedFloor = 0f;
            _rewardBoostHoldRemaining = 0f;
        }

        private Vector3 GetCameraRelativeMove(
            Vector2 moveInput)
        {
            Vector2 clamped =
                Vector2.ClampMagnitude(
                    moveInput,
                    1f);

            Vector3 forward =
                cameraTransform.forward;

            Vector3 right =
                cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            forward =
                forward.sqrMagnitude > 0.0001f
                    ? forward.normalized
                    : Vector3.forward;

            right =
                right.sqrMagnitude > 0.0001f
                    ? right.normalized
                    : Vector3.right;

            Vector3 world =
                forward * clamped.y +
                right * clamped.x;

            return
                world.sqrMagnitude > 1f
                    ? world.normalized
                    : world;
        }

        private void UpdatePlanarVelocity(
            Vector3 desiredDirection,
            bool grounded,
            float dt)
        {
            float inputMagnitude =
                Mathf.Clamp01(
                    desiredDirection.magnitude);

            Vector3 desiredUnit =
                inputMagnitude > 0.0001f
                    ? desiredDirection /
                      inputMagnitude
                    : Vector3.zero;

            if (_dashActive &&
                inputMagnitude > 0.0001f)
            {
                UpdateDashVelocity(
                    desiredUnit,
                    inputMagnitude,
                    grounded,
                    dt);

                return;
            }

            float currentSpeed =
                _planarVelocity.magnitude;

            if (currentSpeed >
                settings.maxGroundSpeed +
                0.001f)
            {
                UpdateExcessMomentum(
                    desiredUnit,
                    inputMagnitude,
                    grounded,
                    dt);

                return;
            }

            Vector3 desiredVelocity =
                desiredUnit *
                (settings.maxGroundSpeed *
                 inputMagnitude);

            if (grounded)
            {
                float rate =
                    inputMagnitude > 0.0001f
                        ? settings.groundAcceleration
                        : settings.groundDeceleration;

                _planarVelocity =
                    Vector3.MoveTowards(
                        _planarVelocity,
                        desiredVelocity,
                        rate * dt);
            }
            else
            {
                float steeringMultiplier =
                    _aerialSteeringRemaining > 0f
                        ? _aerialSteeringMultiplier
                        : 1f;

                if (inputMagnitude > 0.0001f)
                {
                    _planarVelocity =
                        Vector3.MoveTowards(
                            _planarVelocity,
                            desiredVelocity,
                            settings.airAcceleration *
                            steeringMultiplier *
                            dt);
                }
                else if (
                    settings.airNoInputRetention <
                    1f)
                {
                    float retention =
                        Mathf.Pow(
                            Mathf.Clamp01(
                                settings.airNoInputRetention),
                            dt);

                    _planarVelocity *=
                        retention;
                }
            }

            _planarVelocity.y = 0f;
        }

        private void UpdateDashVelocity(
            Vector3 desiredUnit,
            float inputMagnitude,
            bool grounded,
            float dt)
        {
            float targetSpeed =
                settings.dashMaxSpeed *
                inputMagnitude;

            if (_rewardBoostSpeedFloor > 0f)
            {
                targetSpeed =
                    Mathf.Max(
                        targetSpeed,
                        _rewardBoostSpeedFloor *
                        inputMagnitude);
            }

            float currentSpeed =
                _planarVelocity.magnitude;

            Vector3 currentDirection =
                currentSpeed > 0.01f
                    ? _planarVelocity /
                      currentSpeed
                    : desiredUnit;

            float steeringMultiplier =
                !grounded &&
                _aerialSteeringRemaining > 0f
                    ? _aerialSteeringMultiplier
                    : 1f;

            float steerT =
                1f -
                Mathf.Exp(
                    -settings.dashSteeringSharpness *
                    steeringMultiplier *
                    dt);

            Vector3 steeredDirection =
                Vector3.Slerp(
                    currentDirection,
                    desiredUnit,
                    steerT).normalized;

            float acceleration =
                grounded
                    ? settings.dashAcceleration
                    : settings.dashAirAcceleration *
                      steeringMultiplier;

            float nextSpeed =
                Mathf.MoveTowards(
                    currentSpeed,
                    targetSpeed,
                    acceleration *
                    dt);

            _planarVelocity =
                steeredDirection *
                nextSpeed;
        }

        private void UpdateExcessMomentum(
            Vector3 desiredUnit,
            float inputMagnitude,
            bool grounded,
            float dt)
        {
            float currentSpeed =
                _planarVelocity.magnitude;

            Vector3 currentDirection =
                currentSpeed > 0.01f
                    ? _planarVelocity /
                      currentSpeed
                    : desiredUnit;

            bool hasMoveInput =
                inputMagnitude > 0.0001f;

            if (!grounded &&
                !hasMoveInput)
            {
                float nextAirSpeed =
                    Mathf.Max(
                        0f,
                        currentSpeed -
                        settings.airborneMomentumDecay *
                        dt);

                _planarVelocity =
                    currentDirection *
                    nextAirSpeed;

                return;
            }

            Vector3 targetDirection =
                hasMoveInput
                    ? desiredUnit
                    : currentDirection;

            float steeringMultiplier =
                !grounded &&
                _aerialSteeringRemaining > 0f
                    ? _aerialSteeringMultiplier
                    : 1f;

            float steerSharpness =
                grounded
                    ? settings.dashSteeringSharpness
                    : settings.dashSteeringSharpness *
                      0.55f *
                      steeringMultiplier;

            float steerT =
                1f -
                Mathf.Exp(
                    -steerSharpness *
                    dt);

            Vector3 steeredDirection =
                Vector3.Slerp(
                    currentDirection,
                    targetDirection,
                    steerT).normalized;

            float targetSpeed;
            float deceleration;

            if (!hasMoveInput)
            {
                targetSpeed = 0f;
                deceleration =
                    settings.dashBrakeDeceleration;
            }
            else
            {
                float directionDot =
                    Vector3.Dot(
                        currentDirection,
                        desiredUnit);

                bool reversing =
                    directionDot < -0.15f;

                targetSpeed =
                    settings.maxGroundSpeed *
                    inputMagnitude;

                if (_rewardBoostSpeedFloor > 0f)
                {
                    targetSpeed =
                        Mathf.Max(
                            targetSpeed,
                            _rewardBoostSpeedFloor *
                            inputMagnitude);
                }

                deceleration =
                    reversing
                        ? settings.dashReverseBrakeDeceleration
                        : settings.dashReleaseDeceleration;
            }

            float nextSpeed =
                Mathf.MoveTowards(
                    currentSpeed,
                    targetSpeed,
                    deceleration *
                    dt);

            _planarVelocity =
                steeredDirection *
                nextSpeed;
        }

        private void TryConsumeBufferedJump()
        {
            if (_jumpConsumed)
                return;

            bool buffered =
                Time.time -
                _lastJumpPressedTime <=
                settings.jumpBufferTime;

            bool coyoteValid =
                Time.time -
                _lastGroundedTime <=
                settings.coyoteTime;

            if (!buffered ||
                !coyoteValid)
            {
                return;
            }

            _verticalVelocity =
                Mathf.Sqrt(
                    2f *
                    settings.gravity *
                    settings.jumpHeight);

            _jumpConsumed = true;

            _lastJumpPressedTime =
                float.NegativeInfinity;

            _lastGroundedTime =
                float.NegativeInfinity;
        }

        private void UpdateGravity(
            bool groundedAtFrameStart,
            float dt)
        {
            if (groundedAtFrameStart &&
                _verticalVelocity <= 0f)
            {
                return;
            }

            float fallMultiplier =
                _verticalVelocity < 0f
                    ? settings.fallGravityMultiplier
                    : 1f;

            float flowGravityScale =
                _aerialGravityRemaining > 0f
                    ? _aerialGravityScale
                    : 1f;

            _verticalVelocity -=
                settings.gravity *
                fallMultiplier *
                flowGravityScale *
                dt;
        }

        private void UpdateFacing(
            Vector3 desiredDirection,
            float dt)
        {
            if (visualRoot == null ||
                desiredDirection.sqrMagnitude <
                0.001f)
            {
                return;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    desiredDirection.normalized,
                    Vector3.up);

            float t =
                1f -
                Mathf.Exp(
                    -settings.turnSharpness *
                    dt);

            visualRoot.rotation =
                Quaternion.Slerp(
                    visualRoot.rotation,
                    targetRotation,
                    t);
        }

        private void OnControllerColliderHit(
            ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.5f)
            {
                Vector3 intoWall =
                    Vector3.Project(
                        _planarVelocity,
                        hit.normal);

                if (Vector3.Dot(
                        intoWall,
                        hit.normal) < 0f)
                {
                    _planarVelocity -=
                        intoWall;
                }
            }
        }
    }
}
