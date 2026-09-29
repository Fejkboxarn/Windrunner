using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Intent-aware Air Node selection.
    ///
    /// Grounded:
    ///   screen + movement + distance + camera alignment.
    ///
    /// Airborne:
    ///   base score plus route continuation, useful lead distance and
    ///   a height-recovery preference while falling.
    ///
    /// Explicit camera aim can still override normal hysteresis.
    /// </summary>
    public sealed class AirTargeter : MonoBehaviour
    {
        public readonly struct CandidateScore
        {
            public CandidateScore(
                AirNode node,
                float total,
                float screen,
                float movement,
                float distance,
                float cameraForward,
                float aerialContinuation,
                float aerialLead,
                float heightRecovery,
                bool visible)
            {
                Node = node;
                Total = total;
                Screen = screen;
                Movement = movement;
                Distance = distance;
                CameraForward = cameraForward;
                AerialContinuation = aerialContinuation;
                AerialLead = aerialLead;
                HeightRecovery = heightRecovery;
                Visible = visible;
            }

            public AirNode Node { get; }
            public float Total { get; }
            public float Screen { get; }
            public float Movement { get; }
            public float Distance { get; }
            public float CameraForward { get; }
            public float AerialContinuation { get; }
            public float AerialLead { get; }
            public float HeightRecovery { get; }
            public bool Visible { get; }
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirTargetingSettings settings;

        private readonly List<CandidateScore> _candidates =
            new List<CandidateScore>(32);

        private AirNode _currentTarget;
        private float _currentScore;

        public AirNode CurrentTarget => _currentTarget;
        public float CurrentScore => _currentScore;
        public IReadOnlyList<CandidateScore> DebugCandidates => _candidates;

        public event Action<AirNode, AirNode> TargetChanged;

        public void Configure(
            Camera newCamera,
            PlayerMotor newMotor,
            AirTargetingSettings newSettings)
        {
            targetCamera = newCamera;
            motor = newMotor;
            settings = newSettings;
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
            if (targetCamera == null ||
                motor == null ||
                settings == null)
            {
                ClearTarget();
                return;
            }

            EvaluateCandidates();
            SelectBestTarget();
        }

        private void EvaluateCandidates()
        {
            _candidates.Clear();

            IReadOnlyList<AirNode> nodes =
                AirNode.All;

            for (int i = 0; i < nodes.Count; i++)
            {
                AirNode node = nodes[i];

                if (node == null ||
                    !node.IsAvailable)
                {
                    continue;
                }

                if (TryScore(
                        node,
                        out CandidateScore score))
                {
                    _candidates.Add(score);
                }
            }

            _candidates.Sort(
                (a, b) =>
                    b.Total.CompareTo(a.Total));
        }

        private bool TryScore(
            AirNode node,
            out CandidateScore result)
        {
            Vector3 nodePosition =
                node.TargetPosition;

            Vector3 cameraToNode =
                nodePosition -
                targetCamera.transform.position;

            Vector3 playerToNode =
                nodePosition -
                transform.position;

            float distance =
                playerToNode.magnitude;

            if (distance <= 0.001f ||
                distance >
                settings.maxTargetDistance)
            {
                result = default;
                return false;
            }

            float cameraDistance =
                cameraToNode.magnitude;

            if (cameraDistance <= 0.001f)
            {
                result = default;
                return false;
            }

            Vector3 cameraDirection =
                cameraToNode /
                cameraDistance;

            float cameraDot =
                Vector3.Dot(
                    targetCamera.transform.forward,
                    cameraDirection);

            float minCameraDot =
                Mathf.Cos(
                    settings.maxCameraAngle *
                    Mathf.Deg2Rad);

            if (cameraDot < minCameraDot)
            {
                result = default;
                return false;
            }

            Vector3 viewport =
                targetCamera.WorldToViewportPoint(
                    nodePosition);

            if (viewport.z <= 0f)
            {
                result = default;
                return false;
            }

            Vector2 viewportOffset =
                new Vector2(
                    viewport.x - 0.5f,
                    viewport.y - 0.5f);

            float normalizedScreenDistance =
                Mathf.Clamp01(
                    viewportOffset.magnitude /
                    0.70710678f);

            float screenScore =
                1f -
                normalizedScreenDistance;

            float distanceScore =
                1f -
                Mathf.Clamp01(
                    distance /
                    settings.maxTargetDistance);

            float cameraForwardScore =
                Mathf.InverseLerp(
                    minCameraDot,
                    1f,
                    cameraDot);

            Vector3 travelDirection =
                GetTravelDirection();

            Vector3 planarToNode =
                playerToNode;

            planarToNode.y = 0f;

            float movementScore =
                ScoreDirection(
                    travelDirection,
                    planarToNode);

            bool visible =
                HasLineOfSight(
                    nodePosition);

            if (!visible)
            {
                result =
                    new CandidateScore(
                        node,
                        0f,
                        screenScore,
                        movementScore,
                        distanceScore,
                        cameraForwardScore,
                        0f,
                        0f,
                        0f,
                        false);

                return false;
            }

            float aerialContinuation = 0f;
            float aerialLead = 0f;
            float heightRecovery = 0f;

            float weightSum =
                settings.screenCenterWeight +
                settings.movementDirectionWeight +
                settings.distanceWeight +
                settings.cameraForwardWeight;

            float weightedScore =
                screenScore *
                    settings.screenCenterWeight +
                movementScore *
                    settings.movementDirectionWeight +
                distanceScore *
                    settings.distanceWeight +
                cameraForwardScore *
                    settings.cameraForwardWeight;

            if (!motor.IsGrounded)
            {
                ScoreAerialRoute(
                    playerToNode,
                    planarToNode,
                    travelDirection,
                    out aerialContinuation,
                    out aerialLead,
                    out heightRecovery);

                weightedScore +=
                    aerialContinuation *
                    settings.aerialContinuationWeight;

                weightedScore +=
                    aerialLead *
                    settings.aerialLeadDistanceWeight;

                weightedScore +=
                    heightRecovery *
                    settings.fallingHeightRecoveryWeight;

                weightSum +=
                    settings.aerialContinuationWeight +
                    settings.aerialLeadDistanceWeight +
                    settings.fallingHeightRecoveryWeight;
            }

            if (weightSum <= 0.0001f)
                weightSum = 1f;

            float total =
                weightedScore /
                weightSum;

            result =
                new CandidateScore(
                    node,
                    total,
                    screenScore,
                    movementScore,
                    distanceScore,
                    cameraForwardScore,
                    aerialContinuation,
                    aerialLead,
                    heightRecovery,
                    true);

            return true;
        }

        private void ScoreAerialRoute(
            Vector3 playerToNode,
            Vector3 planarToNode,
            Vector3 travelDirection,
            out float continuation,
            out float lead,
            out float heightRecovery)
        {
            continuation =
                ScoreDirection(
                    travelDirection,
                    planarToNode);

            float forwardDistance = 0f;

            if (travelDirection.sqrMagnitude >
                    0.0001f &&
                planarToNode.sqrMagnitude >
                    0.0001f)
            {
                forwardDistance =
                    Vector3.Dot(
                        planarToNode,
                        travelDirection.normalized);
            }

            if (forwardDistance <= 0f)
            {
                lead = 0f;
            }
            else
            {
                float preferred =
                    Mathf.Max(
                        1f,
                        settings.preferredAerialLeadDistance);

                float normalizedDifference =
                    Mathf.Abs(
                        forwardDistance -
                        preferred) /
                    preferred;

                lead =
                    1f -
                    Mathf.Clamp01(
                        normalizedDifference);
            }

            float heightRange =
                Mathf.Max(
                    0.5f,
                    settings.aerialHeightRange);

            float nodeHeightScore =
                Mathf.InverseLerp(
                    -heightRange,
                    heightRange,
                    playerToNode.y);

            float fallingNeed =
                Mathf.InverseLerp(
                    0f,
                    settings.fallSpeedForMaxHeightBias,
                    Mathf.Max(
                        0f,
                        -motor.VerticalSpeed));

            // While rising this contribution settles to neutral 0.5,
            // so it does not push the player upward indefinitely.
            heightRecovery =
                Mathf.Lerp(
                    0.5f,
                    nodeHeightScore,
                    fallingNeed);
        }

        private static float ScoreDirection(
            Vector3 travelDirection,
            Vector3 planarToNode)
        {
            if (travelDirection.sqrMagnitude <=
                    0.0001f ||
                planarToNode.sqrMagnitude <=
                    0.0001f)
            {
                return 0.5f;
            }

            float moveDot =
                Vector3.Dot(
                    travelDirection.normalized,
                    planarToNode.normalized);

            return
                moveDot *
                0.5f +
                0.5f;
        }

        private Vector3 GetTravelDirection()
        {
            Vector3 velocity =
                motor.PlanarVelocity;

            velocity.y = 0f;

            if (velocity.magnitude >=
                settings.movementDirectionMinSpeed)
            {
                return
                    velocity.normalized;
            }

            Vector3 forward =
                targetCamera.transform.forward;

            forward.y = 0f;

            return
                forward.sqrMagnitude > 0.0001f
                    ? forward.normalized
                    : transform.forward;
        }

        private bool HasLineOfSight(
            Vector3 targetPosition)
        {
            Vector3 origin =
                targetCamera.transform.position;

            Vector3 delta =
                targetPosition -
                origin;

            float distance =
                delta.magnitude;

            if (distance <= 0.001f)
                return true;

            return
                !Physics.Raycast(
                    origin,
                    delta / distance,
                    distance,
                    settings.obstructionMask,
                    QueryTriggerInteraction.Ignore);
        }

        private void SelectBestTarget()
        {
            CandidateScore? bestTotal = null;
            CandidateScore? mostCentered = null;
            CandidateScore? current = null;

            for (int i = 0;
                 i < _candidates.Count;
                 i++)
            {
                CandidateScore candidate =
                    _candidates[i];

                if (candidate.Total >=
                    settings.minimumScore)
                {
                    if (!bestTotal.HasValue)
                        bestTotal = candidate;

                    if (!mostCentered.HasValue ||
                        candidate.Screen >
                        mostCentered.Value.Screen)
                    {
                        mostCentered = candidate;
                    }
                }

                if (candidate.Node ==
                    _currentTarget)
                {
                    current = candidate;
                }
            }

            if (_currentTarget != null &&
                current.HasValue)
            {
                float graceMinimum =
                    Mathf.Max(
                        0f,
                        settings.minimumScore -
                        settings.currentTargetScoreGrace);

                bool currentStillAcceptable =
                    current.Value.Total >=
                    graceMinimum;

                if (currentStillAcceptable)
                {
                    _currentScore =
                        current.Value.Total;

                    if (mostCentered.HasValue &&
                        mostCentered.Value.Node !=
                        _currentTarget)
                    {
                        float screenAdvantage =
                            mostCentered.Value.Screen -
                            current.Value.Screen;

                        float totalDeficit =
                            current.Value.Total -
                            mostCentered.Value.Total;

                        bool deliberateAimTakeover =
                            screenAdvantage >=
                                settings.aimTakeoverScreenAdvantage &&
                            totalDeficit <=
                                settings.aimTakeoverAllowedScoreDeficit;

                        if (deliberateAimTakeover)
                        {
                            SetTarget(
                                mostCentered.Value.Node,
                                mostCentered.Value.Total);

                            return;
                        }
                    }

                    if (!bestTotal.HasValue ||
                        bestTotal.Value.Node ==
                        _currentTarget)
                    {
                        return;
                    }

                    if (bestTotal.Value.Total <
                        current.Value.Total +
                        settings.switchScoreAdvantage)
                    {
                        return;
                    }
                }
            }

            if (bestTotal.HasValue)
            {
                SetTarget(
                    bestTotal.Value.Node,
                    bestTotal.Value.Total);
            }
            else
            {
                ClearTarget();
            }
        }

        private void SetTarget(
            AirNode newTarget,
            float score)
        {
            if (_currentTarget ==
                newTarget)
            {
                _currentScore = score;
                return;
            }

            AirNode previous =
                _currentTarget;

            if (previous != null)
                previous.SetSelected(false);

            _currentTarget =
                newTarget;

            _currentScore =
                score;

            if (_currentTarget != null)
                _currentTarget.SetSelected(true);

            TargetChanged?.Invoke(
                previous,
                _currentTarget);
        }

        private void ClearTarget()
        {
            if (_currentTarget == null)
            {
                _currentScore = 0f;
                return;
            }

            AirNode previous =
                _currentTarget;

            previous.SetSelected(false);

            _currentTarget = null;
            _currentScore = 0f;

            TargetChanged?.Invoke(
                previous,
                null);
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying ||
                settings == null ||
                !settings.drawSceneGizmos)
            {
                return;
            }

            int count =
                Mathf.Min(
                    settings.debugCandidateCount,
                    _candidates.Count);

            for (int i = 0;
                 i < count;
                 i++)
            {
                CandidateScore candidate =
                    _candidates[i];

                if (candidate.Node == null)
                    continue;

                bool selected =
                    candidate.Node ==
                    _currentTarget;

                Gizmos.color =
                    selected
                        ? Color.white
                        : Color.Lerp(
                            new Color(
                                0.25f,
                                0.35f,
                                0.45f,
                                0.65f),
                            new Color(
                                0.45f,
                                0.85f,
                                1f,
                                0.85f),
                            candidate.Total);

                Gizmos.DrawLine(
                    transform.position +
                    Vector3.up,
                    candidate.Node.TargetPosition);

                Gizmos.DrawWireSphere(
                    candidate.Node.TargetPosition,
                    selected
                        ? 0.7f
                        : 0.5f);
            }
        }
    }
}
