using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Stateless planar movement calculation. No MonoBehaviour, Transform, input polling,
    /// CharacterController, Time access or scene state. Caller supplies one frame's inputs.
    /// This intentionally preserves the exact legacy acceleration/steering equations.
    /// </summary>
    public static class PlayerMotorPlanarSolver
    {
        public static Vector3 Update(
            Vector3 planarVelocity,
            Vector3 desiredDirection,
            bool dashActive,
            bool grounded,
            float dt,
            AirflowMovementSettings settings,
            float rewardBoostSpeedFloor,
            float aerialSteeringRemaining,
            float aerialSteeringMultiplier)
        {
            float inputMagnitude = Mathf.Clamp01(desiredDirection.magnitude);
            Vector3 desiredUnit = inputMagnitude > 0.0001f
                ? desiredDirection / inputMagnitude
                : Vector3.zero;

            if (dashActive && inputMagnitude > 0.0001f)
                return UpdateDashVelocity(planarVelocity, desiredUnit, inputMagnitude,
                    grounded, dt, settings, rewardBoostSpeedFloor,
                    aerialSteeringRemaining, aerialSteeringMultiplier);

            float currentSpeed = planarVelocity.magnitude;
            if (currentSpeed > settings.maxGroundSpeed + 0.001f)
                return UpdateExcessMomentum(planarVelocity, desiredUnit, inputMagnitude,
                    grounded, dt, settings, rewardBoostSpeedFloor,
                    aerialSteeringRemaining, aerialSteeringMultiplier);

            Vector3 desiredVelocity = desiredUnit * (settings.maxGroundSpeed * inputMagnitude);

            if (grounded)
            {
                float rate = inputMagnitude > 0.0001f
                    ? settings.groundAcceleration
                    : settings.groundDeceleration;
                planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, rate * dt);
            }
            else
            {
                float steeringMultiplier = aerialSteeringRemaining > 0f
                    ? aerialSteeringMultiplier
                    : 1f;

                if (inputMagnitude > 0.0001f)
                {
                    planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity,
                        settings.airAcceleration * steeringMultiplier * dt);
                }
                else if (settings.airNoInputRetention < 1f)
                {
                    float retention = Mathf.Pow(Mathf.Clamp01(settings.airNoInputRetention), dt);
                    planarVelocity *= retention;
                }
            }

            planarVelocity.y = 0f;
            return planarVelocity;
        }

        private static Vector3 UpdateDashVelocity(
            Vector3 planarVelocity,
            Vector3 desiredUnit,
            float inputMagnitude,
            bool grounded,
            float dt,
            AirflowMovementSettings settings,
            float rewardBoostSpeedFloor,
            float aerialSteeringRemaining,
            float aerialSteeringMultiplier)
        {
            float targetSpeed = settings.dashMaxSpeed * inputMagnitude;
            if (rewardBoostSpeedFloor > 0f)
                targetSpeed = Mathf.Max(targetSpeed, rewardBoostSpeedFloor * inputMagnitude);

            float currentSpeed = planarVelocity.magnitude;
            Vector3 currentDirection = currentSpeed > 0.01f
                ? planarVelocity / currentSpeed
                : desiredUnit;

            float steeringMultiplier = !grounded && aerialSteeringRemaining > 0f
                ? aerialSteeringMultiplier
                : 1f;
            float steerT = 1f - Mathf.Exp(-settings.dashSteeringSharpness * steeringMultiplier * dt);
            Vector3 steeredDirection = Vector3.Slerp(currentDirection, desiredUnit, steerT).normalized;

            float acceleration = grounded
                ? settings.dashAcceleration
                : settings.dashAirAcceleration * steeringMultiplier;
            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * dt);
            return steeredDirection * nextSpeed;
        }

        private static Vector3 UpdateExcessMomentum(
            Vector3 planarVelocity,
            Vector3 desiredUnit,
            float inputMagnitude,
            bool grounded,
            float dt,
            AirflowMovementSettings settings,
            float rewardBoostSpeedFloor,
            float aerialSteeringRemaining,
            float aerialSteeringMultiplier)
        {
            float currentSpeed = planarVelocity.magnitude;
            Vector3 currentDirection = currentSpeed > 0.01f
                ? planarVelocity / currentSpeed
                : desiredUnit;
            bool hasMoveInput = inputMagnitude > 0.0001f;

            if (!grounded && !hasMoveInput)
            {
                float nextAirSpeed = Mathf.Max(0f, currentSpeed - settings.airborneMomentumDecay * dt);
                return currentDirection * nextAirSpeed;
            }

            Vector3 targetDirection = hasMoveInput ? desiredUnit : currentDirection;
            float steeringMultiplier = !grounded && aerialSteeringRemaining > 0f
                ? aerialSteeringMultiplier
                : 1f;
            float steerSharpness = grounded
                ? settings.dashSteeringSharpness
                : settings.dashSteeringSharpness * 0.55f * steeringMultiplier;
            float steerT = 1f - Mathf.Exp(-steerSharpness * dt);
            Vector3 steeredDirection = Vector3.Slerp(currentDirection, targetDirection, steerT).normalized;

            float targetSpeed;
            float deceleration;
            if (!hasMoveInput)
            {
                targetSpeed = 0f;
                deceleration = settings.dashBrakeDeceleration;
            }
            else
            {
                float directionDot = Vector3.Dot(currentDirection, desiredUnit);
                bool reversing = directionDot < -0.15f;
                targetSpeed = settings.maxGroundSpeed * inputMagnitude;
                if (rewardBoostSpeedFloor > 0f)
                    targetSpeed = Mathf.Max(targetSpeed, rewardBoostSpeedFloor * inputMagnitude);
                deceleration = reversing
                    ? settings.dashReverseBrakeDeceleration
                    : settings.dashReleaseDeceleration;
            }

            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * dt);
            return steeredDirection * nextSpeed;
        }
    }
}
