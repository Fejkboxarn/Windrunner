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

        private PlayerMotorSimulation _simulation;
        // Optional per-player influence, sampled after steering/gravity and before the single motor Move.
        public System.Func<Vector3, bool, float, Vector3> MovementInfluence { get; set; }
        public PlayerInputReader Input => input != null ? input : (input = GetComponent<PlayerInputReader>());
        public Transform ViewTransform => cameraTransform;
        public Camera ViewCamera => cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        private PlayerDisplacementAuthority _displacement;

        public PlayerDisplacementAuthority Displacement
        {
            get
            {
                if (_displacement == null || _displacement.Controller != controller)
                    _displacement = new PlayerDisplacementAuthority(controller);
                return _displacement;
            }
        }

        public CollisionFlags ApplyDisplacement(Vector3 displacement, PlayerMovementPhase phase)
        {
            return Displacement.Apply(displacement, phase, Time.frameCount);
        }
        private PlayerMotorSimulation Simulation =>
            _simulation ?? (_simulation = new PlayerMotorSimulation());
        private PlayerMotorJumpWindow JumpWindow => Simulation.JumpWindow;
        private PlayerMotorFlowState FlowState => Simulation.Flow;

        public bool HasExternalTraversalVelocity => Simulation.HasExternalVelocity(Time.frameCount);
        public Vector3 Velocity => Simulation.VelocityAt(Time.frameCount);
        public Vector3 PlanarVelocity => Simulation.PlanarVelocityAt(Time.frameCount);

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
        public bool IsGrounded => Simulation.Grounded;
        public bool IsDashActive => Simulation.DashActive;

        public bool HasRewardSpeedBoost => FlowState.HasRewardSpeedBoost;
        public float RewardBoostSpeedFloor => FlowState.RewardBoostSpeedFloor;
        public float RewardBoostHoldRemaining => FlowState.RewardBoostHoldRemaining;

        public bool HasAerialFlow =>
            !Simulation.Grounded &&
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
            Simulation.DashActive = active;
        }

        /// <summary>
        /// Reports movement applied outside PlayerMotor.Update so existing
        /// speed/FOV/wind/lean/audio systems see the actual traversal velocity.
        /// Report once per frame while the external traversal is active.
        /// </summary>
        public void ReportExternalTraversalVelocity(
            Vector3 velocity)
        {
            Simulation.ReportExternalVelocity(velocity, Time.frameCount);
        }

        public void ClearExternalTraversalVelocity()
        {
            Simulation.ClearExternalVelocity();
        }

        public void AddPlanarImpulse(
            Vector3 impulse,
            float maxResultSpeed = 30f)
        {
            Simulation.AddPlanarImpulse(impulse, maxResultSpeed);
        }

        public void AddRewardPlanarBoost(
            Vector3 impulse,
            float maxResultSpeed,
            float holdDuration,
            float decayPerSecond)
        {
            Simulation.AddRewardPlanarBoost(impulse, maxResultSpeed, holdDuration, decayPerSecond);
        }

        public void AddVerticalImpulse(
            float velocityDelta)
        {
            Simulation.AddVerticalImpulse(velocityDelta);
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
            Simulation.StopForTraversalImpact();
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
            Simulation.ApplyAerialFlowReward(risingLift, fallingLift, fallingCancelFraction,
                maxUpwardSpeed, gravityScale, gravityDuration, steeringMultiplier,
                steeringDuration, forceWhenGrounded);
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
            if (controller == null || input == null || cameraTransform == null || settings == null)
                return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 desiredDirection = GetCameraRelativeMove(input.Move);
            Vector3 frameVelocity = Simulation.BeginFrame(desiredDirection,
                input.JumpPressedThisFrame, controller.isGrounded, Time.time, dt, settings);
            if (MovementInfluence != null)
            {
                var delta = MovementInfluence(frameVelocity, controller.isGrounded, dt);
                Simulation.AddPlanarImpulse(delta, 0);
                Simulation.AddVerticalImpulse(delta.y);
                frameVelocity += delta;
            }
            Vector3 beforeMove = transform.position;
            CollisionFlags flags = ApplyDisplacement(frameVelocity * dt, PlayerMovementPhase.Locomotion);
            // OnControllerColliderHit may update simulation during Move, just as
            // it updated the original motor state before displacement reporting.
            Simulation.CompleteMove(transform.position - beforeMove, dt,
                controller.isGrounded || (flags & CollisionFlags.Below) != 0, Time.time);
            UpdateFacing(desiredDirection, dt);
        }

        private Vector3 GetCameraRelativeMove(
            Vector2 moveInput)
        {
            return PlayerMotorSimulation.CameraRelativeMove(moveInput, cameraTransform.forward, cameraTransform.right);
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
            Simulation.HandleCollision(hit.normal);
        }
    }
}
