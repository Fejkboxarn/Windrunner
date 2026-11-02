using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerMotorStateChecks
    {
        [MenuItem("Tools/Windrunner/Production/Batch 5C/1. Run Motor State Checks")]
        public static void Run()
        {
            try
            {
                CheckJumpWindows();
                CheckRewardEnvelope();
                CheckAerialFlow();
                Debug.Log("[Windrunner 5C] PASS — jump buffer, coyote window, " +
                          "single jump consumption, rearm, reward speed hold/decay, " +
                          "falling/rising lift, grounded guard, forced lift and aerial timers. " +
                          "Live movement parity still requires Play Mode tests.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5C] FAIL. Stop and report this before further migration.");
            }
        }

        private static void CheckJumpWindows()
        {
            var jump = new PlayerMotorJumpWindow();
            Check("initial coyote closed", Eq(jump.CoyoteRemaining(0f, 0.12f), 0f));
            Check("initial buffer closed", Eq(jump.BufferRemaining(0f, 0.14f), 0f));

            jump.ReportGrounded(1f);
            jump.ReportJumpPressed(1.10f);
            Check("coyote jump", jump.TryConsume(1.11f, 0.14f, 0.12f));
            Check("no repeated midair jump", !jump.TryConsume(1.11f, 0.14f, 0.12f));
            Check("jump press consumed", Eq(jump.BufferRemaining(1.12f, 0.14f), 0f));

            jump.ReportGrounded(2f);
            jump.ReportJumpPressed(2.01f);
            Check("rearmed on landing", jump.TryConsume(2.04f, 0.14f, 0.12f));

            // Buffering a press just BEFORE a ground contact must trigger on landing.
            var buffered = new PlayerMotorJumpWindow();
            buffered.ReportJumpPressed(3f);
            buffered.ReportGrounded(3.12f);
            Check("press buffered through landing", buffered.TryConsume(3.12f, 0.14f, 0.12f));

            var expiredCoyote = new PlayerMotorJumpWindow();
            expiredCoyote.ReportGrounded(10f);
            expiredCoyote.ReportJumpPressed(10.2f);
            Check("expired coyote rejects jump", !expiredCoyote.TryConsume(10.2f, 0.14f, 0.12f));

            var expiredBuffer = new PlayerMotorJumpWindow();
            expiredBuffer.ReportJumpPressed(20f);
            expiredBuffer.ReportGrounded(20.2f);
            Check("expired input buffer rejects jump", !expiredBuffer.TryConsume(20.2f, 0.14f, 0.12f));
        }

        private static void CheckRewardEnvelope()
        {
            var flow = new PlayerMotorFlowState();
            flow.AddRewardPlanarBoost(20f, 0.5f, 10f);
            Check("reward floor", flow.HasRewardSpeedBoost && Eq(flow.RewardBoostSpeedFloor, 20f));

            flow.TickRewardBoost(1f, 0.3f, 15.5f);
            Check("hold decrement", Eq(flow.RewardBoostHoldRemaining, 0.2f) &&
                                    Eq(flow.RewardBoostSpeedFloor, 20f));

            flow.TickRewardBoost(1f, 0.3f, 15.5f);
            Check("hold does not decay on expiry frame", Eq(flow.RewardBoostSpeedFloor, 20f));

            flow.TickRewardBoost(1f, 0.1f, 15.5f);
            Check("reward decays after hold", Eq(flow.RewardBoostSpeedFloor, 19f));

            flow.TickRewardBoost(0f, 0.01f, 15.5f);
            Check("releasing input clears reward", !flow.HasRewardSpeedBoost &&
                                                    Eq(flow.RewardBoostSpeedFloor, 0f));
        }

        private static void CheckAerialFlow()
        {
            var flow = new PlayerMotorFlowState();
            float lift = flow.ApplyAerialFlowReward(-8f, false, false,
                risingLift: 2f, fallingLift: 5f, fallingCancelFraction: 0.5f,
                maxUpwardSpeed: 12f, gravityScale: 0.4f, gravityDuration: 0.5f,
                steeringMultiplier: 1.8f, steeringDuration: 0.4f);
            Check("fall cancelled and lifted", Eq(lift, 1f));
            Check("flow modifiers set", Eq(flow.AerialGravityScale, 0.4f) &&
                                         Eq(flow.AerialSteeringMultiplier, 1.8f) &&
                                         flow.HasAerialFlow);

            flow.TickAerialFlow(0.25f);
            Check("flow durations decrease", Eq(flow.AerialGravityRemaining, 0.25f) &&
                                             Eq(flow.AerialSteeringRemaining, 0.15f));

            flow.TickAerialFlow(0.3f);
            Check("flow expires to defaults", !flow.HasAerialFlow &&
                                              Eq(flow.AerialGravityScale, 1f) &&
                                              Eq(flow.AerialSteeringMultiplier, 1f));

            float ignored = flow.ApplyAerialFlowReward(2f, true, false,
                2f, 5f, 0.5f, 12f, 0.4f, 0.5f, 1.8f, 0.4f);
            Check("grounded guard", Eq(ignored, 2f) && !flow.HasAerialFlow);

            float forced = flow.ApplyAerialFlowReward(2f, true, true,
                2f, 5f, 0.5f, 12f, 0.4f, 0.5f, 1.8f, 0.4f);
            Check("forced launch overrides grounded guard", Eq(forced, 4f) && flow.HasAerialFlow);

            flow.ClearAerialFlow();
            Check("impact clears flow", !flow.HasAerialFlow && Eq(flow.AerialGravityScale, 1f));
        }

        private static bool Eq(float a, float b) => Mathf.Abs(a - b) < 0.0001f;

        private static void Check(string label, bool passed)
        {
            if (!passed)
                throw new InvalidOperationException("Motor state check failed: " + label);
        }
    }
}
