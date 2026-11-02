using System;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public static class WindrunnerPlanarParityChecks
    {
        [MenuItem("Tools/Windrunner/Production/Batch 5D/1. Run Steering Parity Checks")]
        public static void Run()
        {
            AirflowMovementSettings settings = ScriptableObject.CreateInstance<AirflowMovementSettings>();
            try
            {
                var random = new System.Random(530551);
                var reference = new LegacyPlanarReference();
                Vector3 accumulatedLegacy = Vector3.zero;
                Vector3 accumulatedNew = Vector3.zero;
                int comparisons = 0;

                for (int i = 0; i < 2000; i++)
                {
                    float dt = 0.002f + (float)random.NextDouble() * 0.048f;
                    bool grounded = i % 3 != 0;
                    bool dashing = i % 5 == 0;
                    settings.airNoInputRetention = i % 9 == 0 ? 0.65f : 1f;

                    float x = (float)random.NextDouble() * 2f - 1f;
                    float z = (float)random.NextDouble() * 2f - 1f;
                    Vector3 desired = i % 7 == 0 ? Vector3.zero : new Vector3(x, 0f, z);
                    Vector3 startVelocity = i % 2 == 0
                        ? new Vector3(x * 24f, 0f, z * 24f)
                        : new Vector3(x * 6f, 0f, z * 6f);
                    if (i % 11 == 0)
                        desired = -startVelocity.normalized;

                    var flow = new PlayerMotorFlowState();
                    if (i % 4 == 0)
                        flow.AddRewardPlanarBoost(17.5f + i % 8, 0.5f, 10f);
                    if (i % 5 == 0)
                        flow.ApplyAerialFlowReward(-5f, false, false,
                            2f, 4f, 0.5f, 12f, 0.6f, 0.5f, 1.8f, 0.5f);

                    Vector3 oldVelocity = reference.Step(startVelocity, desired,
                        dashing, grounded, dt, settings, flow);
                    Vector3 newVelocity = PlayerMotorPlanarSolver.Update(startVelocity, desired,
                        dashing, grounded, dt, settings, flow.RewardBoostSpeedFloor,
                        flow.AerialSteeringRemaining, flow.AerialSteeringMultiplier);
                    AssertEquivalent(i, oldVelocity, newVelocity);
                    comparisons++;

                    // Also compare sustained multi-frame trajectories, not only isolated steps.
                    accumulatedLegacy = reference.Step(accumulatedLegacy, desired,
                        dashing, grounded, dt, settings, flow);
                    accumulatedNew = PlayerMotorPlanarSolver.Update(accumulatedNew, desired,
                        dashing, grounded, dt, settings, flow.RewardBoostSpeedFloor,
                        flow.AerialSteeringRemaining, flow.AerialSteeringMultiplier);
                    AssertEquivalent(i + 2000, accumulatedLegacy, accumulatedNew);
                    comparisons++;
                }

                Debug.Log("[Windrunner 5D] PASS — " + comparisons +
                    " old-vs-new planar solver comparisons matched. Cases include reverse, " +
                    "braking, dash, reward boost, ground, air, momentum overflow and " +
                    "multi-frame direction changes. Live CharacterController parity still requires Play Mode.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5D] FAIL — Stop here. Do not tune away a parity difference.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void AssertEquivalent(int caseNumber, Vector3 expected, Vector3 actual)
        {
            if (float.IsNaN(actual.x) || float.IsNaN(actual.y) || float.IsNaN(actual.z) ||
                Vector3.Distance(expected, actual) > 0.0001f)
            {
                throw new InvalidOperationException(
                    "Planar solver mismatch at case " + caseNumber +
                    ": legacy=" + expected.ToString("F6") +
                    ", extracted=" + actual.ToString("F6"));
            }
        }

        // Frozen Batch 5C equations. Test-only code, not called by gameplay.
        private sealed class LegacyPlanarReference
        {
            private Vector3 _planarVelocity;
            private bool _dashActive;
            private AirflowMovementSettings settings;
            private PlayerMotorFlowState FlowState;

            public Vector3 Step(Vector3 velocity, Vector3 direction, bool dash,
                bool grounded, float dt, AirflowMovementSettings movement,
                PlayerMotorFlowState flow)
            {
                _planarVelocity = velocity;
                _dashActive = dash;
                settings = movement;
                FlowState = flow;
                UpdatePlanarVelocity(direction, grounded, dt);
                return _planarVelocity;
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
                        FlowState.AerialSteeringRemaining > 0f
                            ? FlowState.AerialSteeringMultiplier
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
    
                if (FlowState.RewardBoostSpeedFloor > 0f)
                {
                    targetSpeed =
                        Mathf.Max(
                            targetSpeed,
                            FlowState.RewardBoostSpeedFloor *
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
                    FlowState.AerialSteeringRemaining > 0f
                        ? FlowState.AerialSteeringMultiplier
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
                    FlowState.AerialSteeringRemaining > 0f
                        ? FlowState.AerialSteeringMultiplier
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
    
                    if (FlowState.RewardBoostSpeedFloor > 0f)
                    {
                        targetSpeed =
                            Mathf.Max(
                                targetSpeed,
                                FlowState.RewardBoostSpeedFloor *
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
        }
    }
}
