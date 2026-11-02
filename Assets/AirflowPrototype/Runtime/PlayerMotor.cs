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
        private bool _grounded;
        private bool _dashActive;

        // Gameplay state is being extracted incrementally while preserving this
        // MonoBehaviour as the serialized facade used by existing abilities.
        private PlayerMotorJumpWindow _jumpWindow;
        private PlayerMotorFlowState _flowState;
        private PlayerMotorJumpWindow JumpWindow =>
            _jumpWindow ?? (_jumpWindow = new PlayerMotorJumpWindow());
        private PlayerMotorFlowState FlowState =>
            _flowState ?? (_flowState = new PlayerMotorFlowState());

        // External traversal feedback.
        // Systems such as Sling can move the CharacterController after the
        // motor update. Reporting that displacement here makes all existing
        // speed-driven feedback see the real traversal speed.
        private Vector3 _externalTraversalVelocity;
        private int _externalTraversalVelocityFrame = -1000;

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

        public bool HasRewardSpeedBoost => FlowState.HasRewardSpeedBoost;
        public float RewardBoostSpeedFloor => FlowState.RewardBoostSpeedFloor;
        public float RewardBoostHoldRemaining => FlowState.RewardBoostHoldRemaining;

        public bool HasAerialFlow =>
            !_grounded &&
            FlowState.HasAerialFlow;

        public float AerialGravityRemaining => FlowState.AerialGravityRemaining;
        public float AerialSteeringRemaining => FlowState.AerialSteeringRemaining;

        public AirflowMovementSettings Settings => settings;

        public float CoyoteRemaining =>
            settings != null
                ? JumpWindow.CoyoteRemaining(Time.time, settings.coyoteTime)
                : 0f;

        public float JumpBufferRemaining =>
            settings != null
                ? JumpWindow.BufferRemaining(Time.time, settings.jumpBufferTime)
                : 0f;

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

            FlowState.AddRewardPlanarBoost(resultingSpeed, holdDuration, decayPerSecond);
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

            FlowState.ClearRewardBoost();
            FlowState.ClearAerialFlow();
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
            _verticalVelocity = FlowState.ApplyAerialFlowReward(
                _verticalVelocity,
                _grounded,
                forceWhenGrounded,
                risingLift,
                fallingLift,
                fallingCancelFraction,
                maxUpwardSpeed,
                gravityScale,
                gravityDuration,
                steeringMultiplier,
                steeringDuration);
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
                JumpWindow.ReportGrounded(Time.time);
                FlowState.ClearAerialFlow();

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity =
                        -settings.groundedStickForce;
                }
            }
            else
            {
                FlowState.TickAerialFlow(dt);
            }

            if (input.JumpPressedThisFrame)
            {
                JumpWindow.ReportJumpPressed(Time.time);
            }

            Vector3 desiredDirection =
                GetCameraRelativeMove(
                    input.Move);

            float inputMagnitude =
                Mathf.Clamp01(
                    desiredDirection.magnitude);

            FlowState.TickRewardBoost(inputMagnitude, dt, settings.dashMaxSpeed);

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
                JumpWindow.ReportGrounded(Time.time);
                FlowState.ClearAerialFlow();
            }

            UpdateFacing(
                desiredDirection,
                dt);
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
            _planarVelocity = PlayerMotorPlanarSolver.Update(
                _planarVelocity,
                desiredDirection,
                _dashActive,
                grounded,
                dt,
                settings,
                FlowState.RewardBoostSpeedFloor,
                FlowState.AerialSteeringRemaining,
                FlowState.AerialSteeringMultiplier);
        }

        private void TryConsumeBufferedJump()
        {
            if (!JumpWindow.TryConsume(Time.time, settings.jumpBufferTime, settings.coyoteTime))
                return;

            _verticalVelocity =
                Mathf.Sqrt(
                    2f *
                    settings.gravity *
                    settings.jumpHeight);
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
                FlowState.AerialGravityRemaining > 0f
                    ? FlowState.AerialGravityScale
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
