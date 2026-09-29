using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "AirTargetingSettings",
        menuName = "Airflow Prototype/Air Targeting Settings")]
    public sealed class AirTargetingSettings : ScriptableObject
    {
        [Header("Candidate Limits")]
        public float maxTargetDistance = 42f;
        public float maxCameraAngle = 64f;
        public float minimumScore = 0.22f;
        public LayerMask obstructionMask = 1;

        [Header("Base Score Weights")]
        public float screenCenterWeight = 0.42f;
        public float movementDirectionWeight = 0.30f;
        public float distanceWeight = 0.16f;
        public float cameraForwardWeight = 0.12f;

        [Header("Target Stability")]
        public float switchScoreAdvantage = 0.12f;
        public float currentTargetScoreGrace = 0.06f;

        [Header("Aim Takeover")]
        public float aimTakeoverScreenAdvantage = 0.035f;
        public float aimTakeoverAllowedScoreDeficit = 0.10f;

        [Header("Movement Intent")]
        public float movementDirectionMinSpeed = 1f;

        [Header("Aerial Route Intelligence")]
        [Tooltip("Extra weight for nodes that continue the player's current horizontal travel direction while airborne.")]
        [Min(0f)] public float aerialContinuationWeight = 0.24f;

        [Tooltip("Extra weight for nodes at a useful distance ahead in the current travel direction.")]
        [Min(0f)] public float aerialLeadDistanceWeight = 0.14f;

        [Tooltip("Ideal forward distance for the next node in an aerial chain.")]
        [Min(1f)] public float preferredAerialLeadDistance = 14f;

        [Tooltip("Extra weight for nodes above the player while falling. This fades away while rising.")]
        [Min(0f)] public float fallingHeightRecoveryWeight = 0.18f;

        [Tooltip("Vertical distance used to normalize the falling height-recovery score.")]
        [Min(0.5f)] public float aerialHeightRange = 10f;

        [Tooltip("Downward speed at which the full height-recovery preference is applied.")]
        [Min(0.1f)] public float fallSpeedForMaxHeightBias = 5f;

        [Header("Debug")]
        public bool drawSceneGizmos = true;
        public int debugCandidateCount = 8;
    }
}
