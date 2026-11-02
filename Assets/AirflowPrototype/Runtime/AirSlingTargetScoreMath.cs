using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Geometry facts calculated before the camera viewport and physics tests.
    /// Plain structs keep per-candidate processing allocation-free.
    /// </summary>
    public readonly struct AirSlingTargetGeometry
    {
        public readonly Vector3 PlayerToTarget;
        public readonly float Distance;
        public readonly float CameraDot;
        public readonly float MinimumCameraDot;

        public AirSlingTargetGeometry(
            Vector3 playerToTarget, float distance, float cameraDot, float minimumCameraDot)
        {
            PlayerToTarget = playerToTarget;
            Distance = distance;
            CameraDot = cameraDot;
            MinimumCameraDot = minimumCameraDot;
        }
    }

    public readonly struct AirSlingTargetScores
    {
        public readonly float Screen;
        public readonly float Movement;
        public readonly float Distance;
        public readonly float CameraForward;

        public AirSlingTargetScores(float screen, float movement,
            float distance, float cameraForward)
        {
            Screen = screen;
            Movement = movement;
            Distance = distance;
            CameraForward = cameraForward;
        }
    }

    /// <summary>
    /// Stateless target geometry and weighting calculations, extracted from
    /// the prototype targeter with the original thresholds and math intact.
    /// It never accesses Camera, Physics, transforms, scene objects or Time.
    /// </summary>
    public static class AirSlingTargetScoreMath
    {
        public static bool TryPrepare(
            Vector3 playerPosition, Vector3 targetPosition,
            Vector3 cameraPosition, Vector3 cameraForward,
            float maxTargetDistance, float maxCameraAngle,
            out AirSlingTargetGeometry geometry)
        {
            Vector3 playerToTarget = targetPosition - playerPosition;
            float distance = playerToTarget.magnitude;

            if (distance <= 0.001f || distance > maxTargetDistance)
            {
                geometry = default;
                return false;
            }

            Vector3 cameraToTarget = targetPosition - cameraPosition;
            float cameraDistance = cameraToTarget.magnitude;

            if (cameraDistance <= 0.001f)
            {
                geometry = default;
                return false;
            }

            Vector3 cameraDirection = cameraToTarget / cameraDistance;
            float cameraDot = Vector3.Dot(cameraForward, cameraDirection);
            float minCameraDot = Mathf.Cos(maxCameraAngle * Mathf.Deg2Rad);

            if (cameraDot < minCameraDot)
            {
                geometry = default;
                return false;
            }

            geometry = new AirSlingTargetGeometry(
                playerToTarget, distance, cameraDot, minCameraDot);
            return true;
        }

        // Call only after WorldToViewportPoint has returned a positive z,
        // matching the old order of visibility checks.
        public static AirSlingTargetScores ComputeComponents(
            AirSlingTargetGeometry geometry, Vector3 viewport,
            Vector3 planarVelocity, float maxTargetDistance)
        {
            Vector2 viewportOffset = new Vector2(
                viewport.x - 0.5f,
                viewport.y - 0.5f);

            float normalizedScreenDistance = Mathf.Clamp01(
                viewportOffset.magnitude / 0.70710678f);

            float screenScore = 1f - normalizedScreenDistance;
            float distanceScore = 1f - Mathf.Clamp01(
                geometry.Distance / maxTargetDistance);
            float cameraScore = Mathf.InverseLerp(
                geometry.MinimumCameraDot, 1f, geometry.CameraDot);

            Vector3 movement = planarVelocity;
            movement.y = 0f;

            Vector3 planarToTarget = geometry.PlayerToTarget;
            planarToTarget.y = 0f;

            float movementScore = 0.5f;
            if (movement.sqrMagnitude > 0.01f &&
                planarToTarget.sqrMagnitude > 0.01f)
            {
                float dot = Vector3.Dot(
                    movement.normalized, planarToTarget.normalized);
                movementScore = Mathf.InverseLerp(-1f, 1f, dot);
            }

            return new AirSlingTargetScores(
                screenScore, movementScore, distanceScore, cameraScore);
        }

        public static float WeightedTotal(
            AirSlingTargetScores scores, AirSlingSettings settings)
        {
            float weightSum =
                settings.screenCenterWeight +
                settings.movementDirectionWeight +
                settings.distanceWeight +
                settings.cameraForwardWeight;

            if (weightSum <= 0.0001f)
                weightSum = 1f;

            return
                (scores.Screen * settings.screenCenterWeight +
                 scores.Movement * settings.movementDirectionWeight +
                 scores.Distance * settings.distanceWeight +
                 scores.CameraForward * settings.cameraForwardWeight) /
                weightSum;
        }
    }
}
