using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerMotorSimulationChecks
    {
        // Frozen integration order from PlayerMotor at 7bfdfe5e. Individual
        // planar/jump/reward rules have separate independent legacy parity tests.
        private sealed class LegacyFrame
        {
            public Vector3 Planar;
            public float Vertical;
            public bool Grounded;
            public readonly PlayerMotorJumpWindow Jump = new PlayerMotorJumpWindow();
            public readonly PlayerMotorFlowState Flow = new PlayerMotorFlowState();

            public Vector3 Tick(Vector3 desired, bool pressed, bool ground, bool dash,
                float now, float dt, AirflowMovementSettings settings)
            {
                if (ground)
                {
                    Jump.ReportGrounded(now);
                    Flow.ClearAerialFlow();
                    if (Vertical < 0f) Vertical = -settings.groundedStickForce;
                }
                else Flow.TickAerialFlow(dt);
                if (pressed) Jump.ReportJumpPressed(now);
                float magnitude = Mathf.Clamp01(desired.magnitude);
                Flow.TickRewardBoost(magnitude, dt, settings.dashMaxSpeed);
                Planar = PlayerMotorPlanarSolver.Update(Planar, desired, dash, ground,
                    dt, settings, Flow.RewardBoostSpeedFloor, Flow.AerialSteeringRemaining,
                    Flow.AerialSteeringMultiplier);
                if (Jump.TryConsume(now, settings.jumpBufferTime, settings.coyoteTime))
                    Vertical = Mathf.Sqrt(2f * settings.gravity * settings.jumpHeight);
                if (!(ground && Vertical <= 0f))
                {
                    float fall = Vertical < 0f ? settings.fallGravityMultiplier : 1f;
                    float gravity = Flow.AerialGravityRemaining > 0f ? Flow.AerialGravityScale : 1f;
                    Vertical -= settings.gravity * fall * gravity * dt;
                }
                return Planar + Vector3.up * Vertical;
            }
        }

        [MenuItem("Tools/Windrunner/Production/Run Motor Simulation Parity")]
        public static void Run()
        {
            var settings = ScriptableObject.CreateInstance<AirflowMovementSettings>();
            try
            {
                var random = new System.Random(20261112);
                int frames = 0;
                for (int sequence = 0; sequence < 200; sequence++)
                {
                    var old = new LegacyFrame();
                    var current = new PlayerMotorSimulation();
                    float now = 0f;
                    for (int frame = 0; frame < 300; frame++)
                    {
                        float dt = new[] { 1f / 30f, 1f / 60f, 1f / 144f, 0.1f }[random.Next(4)];
                        now += dt;
                        bool ground = random.Next(4) == 0;
                        bool dash = random.Next(3) == 0;
                        bool jump = random.Next(9) == 0;
                        var desired = Vector3.ClampMagnitude(new Vector3((float)random.NextDouble() * 2f - 1f,
                            0f, (float)random.NextDouble() * 2f - 1f), 1f);
                        if (random.Next(5) == 0) desired = Vector3.zero;
                        if (random.Next(9) == 0)
                        {
                            var impulse = desired * 9f;
                            old.Planar += impulse;
                            if (old.Planar.magnitude > 30f) old.Planar = old.Planar.normalized * 30f;
                            old.Flow.AddRewardPlanarBoost(old.Planar.magnitude, 0.5f, 8f);
                            current.AddRewardPlanarBoost(impulse, 30f, 0.5f, 8f);
                        }
                        if (random.Next(11) == 0)
                        {
                            bool forced = random.Next(2) == 0;
                            old.Vertical = old.Flow.ApplyAerialFlowReward(old.Vertical, old.Grounded,
                                forced, 3f, 5f, 0.7f, 15f, 0.4f, 0.8f, 1.5f, 0.7f);
                            current.ApplyAerialFlowReward(3f, 5f, 0.7f, 15f, 0.4f, 0.8f, 1.5f, 0.7f, forced);
                        }
                        if (random.Next(17) == 0)
                        {
                            old.Vertical += 2f;
                            current.AddVerticalImpulse(2f);
                        }
                        current.DashActive = dash;
                        var expected = old.Tick(desired, jump, ground, dash, now, dt, settings);
                        var actual = current.BeginFrame(desired, jump, ground, now, dt, settings);
                        Equal(expected, actual, "integrated velocity", sequence, frame);
                        var normal = random.Next(2) == 0 ? Vector3.left : new Vector3(0f, 0.6f, -0.8f);
                        if (normal.y < 0.5f)
                        {
                            var intoWall = Vector3.Project(old.Planar, normal);
                            if (Vector3.Dot(intoWall, normal) < 0f) old.Planar -= intoWall;
                        }
                        current.HandleCollision(normal);
                        Equal(old.Planar, current.PlanarVelocity, "collision momentum", sequence, frame);
                        bool landed = random.Next(7) == 0;
                        old.Grounded = landed;
                        if (landed) { old.Jump.ReportGrounded(now); old.Flow.ClearAerialFlow(); }
                        var displacement = actual * dt * 0.8f;
                        current.CompleteMove(displacement, dt, landed, now);
                        Equal(displacement / dt, current.VelocityAt(frame), "measured velocity", sequence, frame);
                        if (current.Grounded != old.Grounded) throw new Exception("Ground result mismatch");
                        if (random.Next(19) == 0)
                        {
                            current.ReportExternalVelocity(Vector3.one * 20f, frame);
                            Equal(Vector3.one * 20f, current.VelocityAt(frame + 1), "external velocity lifetime", sequence, frame);
                            Equal(displacement / dt, current.VelocityAt(frame + 2), "external velocity expiry", sequence, frame);
                            current.ClearExternalVelocity();
                        }
                        if (random.Next(23) == 0)
                        {
                            old.Planar = Vector3.zero;
                            old.Vertical = 0f;
                            old.Flow.ClearRewardBoost();
                            old.Flow.ClearAerialFlow();
                            current.StopForTraversalImpact();
                            Equal(Vector3.zero, current.VelocityAt(frame), "impact stop", sequence, frame);
                        }
                        frames++;
                    }
                }
                Equal(Vector3.forward, PlayerMotorSimulation.CameraRelativeMove(Vector2.up, Vector3.up, Vector3.right), "vertical camera fallback", 0, 0);
                Equal(new Vector3(1f, 0f, 1f).normalized,
                    PlayerMotorSimulation.CameraRelativeMove(Vector2.one, Vector3.forward, Vector3.right), "diagonal cap", 0, 0);
                Debug.Log("[Windrunner Motor Simulation] PASS — " + frames + " integration frames; jump/gravity/rewards/collision/impact/external velocity and camera projection.");
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        private static void Equal(Vector3 expected, Vector3 actual, string label, int sequence, int frame)
        {
            if ((expected - actual).sqrMagnitude > 0.00000001f || float.IsNaN(actual.sqrMagnitude))
                throw new InvalidOperationException(label + " mismatch at " + sequence + "/" + frame + ": " + expected + " vs " + actual);
        }
    }
}
