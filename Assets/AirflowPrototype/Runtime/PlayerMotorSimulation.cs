using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Per-player movement state and integration. The adapter supplies input,
    /// clocks and collision results; this class never polls Unity lifecycle or
    /// moves scene objects. Preserve BeginFrame -> collision callbacks ->
    /// CompleteMove ordering, including callbacks during CharacterController.Move.
    /// </summary>
    public sealed class PlayerMotorSimulation
    {
        public Vector3 PlanarVelocity { get; private set; }
        public Vector3 ActualVelocity { get; private set; }
        public float VerticalVelocity { get; private set; }
        public bool Grounded { get; private set; }
        public bool DashActive { get; set; }
        public PlayerMotorJumpWindow JumpWindow { get; } = new PlayerMotorJumpWindow();
        public PlayerMotorFlowState Flow { get; } = new PlayerMotorFlowState();
        private Vector3 externalVelocity;
        private int externalVelocityFrame = -1000;

        public bool HasExternalVelocity(int frame) => frame - externalVelocityFrame <= 1;
        public Vector3 VelocityAt(int frame) => HasExternalVelocity(frame) ? externalVelocity : ActualVelocity;
        public Vector3 PlanarVelocityAt(int frame)
        {
            if (!HasExternalVelocity(frame)) return PlanarVelocity;
            var value = externalVelocity;
            value.y = 0f;
            return value;
        }

        public void ReportExternalVelocity(Vector3 velocity, int frame)
        {
            externalVelocity = velocity;
            externalVelocityFrame = frame;
        }

        public void ClearExternalVelocity()
        {
            externalVelocity = Vector3.zero;
            externalVelocityFrame = -1000;
        }

        public void AddPlanarImpulse(Vector3 impulse, float maxResultSpeed = 30f)
        {
            impulse.y = 0f;
            PlanarVelocity += impulse;
            if (maxResultSpeed > 0f && PlanarVelocity.magnitude > maxResultSpeed)
                PlanarVelocity = PlanarVelocity.normalized * maxResultSpeed;
        }

        public void AddRewardPlanarBoost(Vector3 impulse, float maxResultSpeed,
            float holdDuration, float decayPerSecond)
        {
            AddPlanarImpulse(impulse, maxResultSpeed);
            Flow.AddRewardPlanarBoost(PlanarVelocity.magnitude, holdDuration, decayPerSecond);
        }

        public void AddVerticalImpulse(float velocityDelta) => VerticalVelocity += velocityDelta;

        public void ApplyAerialFlowReward(float risingLift, float fallingLift,
            float fallingCancelFraction, float maxUpwardSpeed, float gravityScale,
            float gravityDuration, float steeringMultiplier, float steeringDuration,
            bool forceWhenGrounded)
        {
            VerticalVelocity = Flow.ApplyAerialFlowReward(VerticalVelocity, Grounded,
                forceWhenGrounded, risingLift, fallingLift, fallingCancelFraction,
                maxUpwardSpeed, gravityScale, gravityDuration, steeringMultiplier, steeringDuration);
        }

        public void StopForTraversalImpact()
        {
            PlanarVelocity = Vector3.zero;
            VerticalVelocity = 0f;
            ActualVelocity = Vector3.zero;
            Flow.ClearRewardBoost();
            Flow.ClearAerialFlow();
            ClearExternalVelocity();
        }

        public static Vector3 CameraRelativeMove(Vector2 input, Vector3 forward, Vector3 right)
        {
            var clamped = Vector2.ClampMagnitude(input, 1f);
            forward.y = 0f;
            right.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            var world = forward * clamped.y + right * clamped.x;
            return world.sqrMagnitude > 1f ? world.normalized : world;
        }

        public Vector3 BeginFrame(Vector3 desiredDirection, bool jumpPressed,
            bool groundedAtFrameStart, float now, float dt, AirflowMovementSettings settings)
        {
            if (groundedAtFrameStart)
            {
                JumpWindow.ReportGrounded(now);
                Flow.ClearAerialFlow();
                if (VerticalVelocity < 0f) VerticalVelocity = -settings.groundedStickForce;
            }
            else Flow.TickAerialFlow(dt);

            if (jumpPressed) JumpWindow.ReportJumpPressed(now);
            Flow.TickRewardBoost(Mathf.Clamp01(desiredDirection.magnitude), dt, settings.dashMaxSpeed);
            PlanarVelocity = PlayerMotorPlanarSolver.Update(PlanarVelocity, desiredDirection,
                DashActive, groundedAtFrameStart, dt, settings, Flow.RewardBoostSpeedFloor,
                Flow.AerialSteeringRemaining, Flow.AerialSteeringMultiplier);

            if (JumpWindow.TryConsume(now, settings.jumpBufferTime, settings.coyoteTime))
                VerticalVelocity = Mathf.Sqrt(2f * settings.gravity * settings.jumpHeight);

            if (!groundedAtFrameStart || VerticalVelocity > 0f)
            {
                float fallMultiplier = VerticalVelocity < 0f ? settings.fallGravityMultiplier : 1f;
                float flowGravity = Flow.AerialGravityRemaining > 0f ? Flow.AerialGravityScale : 1f;
                VerticalVelocity -= settings.gravity * fallMultiplier * flowGravity * dt;
            }
            return PlanarVelocity + Vector3.up * VerticalVelocity;
        }

        public void CompleteMove(Vector3 displacement, float dt, bool grounded, float now)
        {
            ActualVelocity = displacement / dt;
            Grounded = grounded;
            if (!grounded) return;
            JumpWindow.ReportGrounded(now);
            Flow.ClearAerialFlow();
        }

        public void HandleCollision(Vector3 normal)
        {
            if (normal.y >= 0.5f) return;
            var intoWall = Vector3.Project(PlanarVelocity, normal);
            if (Vector3.Dot(intoWall, normal) < 0f) PlanarVelocity -= intoWall;
        }
    }
}
