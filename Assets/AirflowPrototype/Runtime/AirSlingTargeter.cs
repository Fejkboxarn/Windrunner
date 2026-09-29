using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    public sealed class AirSlingTargeter : MonoBehaviour
    {
        public readonly struct CandidateScore
        {
            public readonly AirSlingNode Node;
            public readonly float Total;
            public readonly float Screen;
            public readonly float Movement;
            public readonly float Distance;
            public readonly float CameraForward;
            public readonly bool Visible;

            public CandidateScore(
                AirSlingNode node,
                float total,
                float screen,
                float movement,
                float distance,
                float cameraForward,
                bool visible)
            {
                Node = node;
                Total = total;
                Screen = screen;
                Movement = movement;
                Distance = distance;
                CameraForward = cameraForward;
                Visible = visible;
            }
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirSlingSettings settings;

        private readonly List<CandidateScore> _candidates =
            new List<CandidateScore>(24);

        private AirSlingNode _currentTarget;
        private float _currentScore;

        public AirSlingNode CurrentTarget =>
            _currentTarget;

        public float CurrentScore =>
            _currentScore;

        public IReadOnlyList<CandidateScore> DebugCandidates =>
            _candidates;

        public event Action<AirSlingNode, AirSlingNode> TargetChanged;

        public void Configure(
            Camera newCamera,
            PlayerMotor newMotor,
            AirSlingSettings newSettings)
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
            if (targetCamera == null)
                targetCamera = Camera.main;

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

            IReadOnlyList<AirSlingNode> nodes =
                AirSlingNode.All;

            for (int i = 0;
                 i < nodes.Count;
                 i++)
            {
                AirSlingNode node =
                    nodes[i];

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
            AirSlingNode node,
            out CandidateScore result)
        {
            Vector3 targetPosition =
                node.TargetPosition;

            Vector3 playerToTarget =
                targetPosition -
                transform.position;

            float distance =
                playerToTarget.magnitude;

            if (distance <= 0.001f ||
                distance >
                settings.maxTargetDistance)
            {
                result = default;
                return false;
            }

            Vector3 cameraToTarget =
                targetPosition -
                targetCamera.transform.position;

            float cameraDistance =
                cameraToTarget.magnitude;

            if (cameraDistance <= 0.001f)
            {
                result = default;
                return false;
            }

            Vector3 cameraDirection =
                cameraToTarget /
                cameraDistance;

            float cameraDot =
                Vector3.Dot(
                    targetCamera.transform.forward,
                    cameraDirection);

            float minCameraDot =
                Mathf.Cos(
                    settings.maxCameraAngle *
                    Mathf.Deg2Rad);

            if (cameraDot <
                minCameraDot)
            {
                result = default;
                return false;
            }

            Vector3 viewport =
                targetCamera.WorldToViewportPoint(
                    targetPosition);

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

            float cameraScore =
                Mathf.InverseLerp(
                    minCameraDot,
                    1f,
                    cameraDot);

            Vector3 movement =
                motor.PlanarVelocity;

            movement.y = 0f;

            Vector3 planarToTarget =
                playerToTarget;

            planarToTarget.y = 0f;

            float movementScore =
                0.5f;

            if (movement.sqrMagnitude >
                    0.01f &&
                planarToTarget.sqrMagnitude >
                    0.01f)
            {
                float dot =
                    Vector3.Dot(
                        movement.normalized,
                        planarToTarget.normalized);

                movementScore =
                    Mathf.InverseLerp(
                        -1f,
                        1f,
                        dot);
            }

            bool visible =
                HasLineOfSight(
                    node,
                    targetPosition);

            if (!visible)
            {
                result = default;
                return false;
            }

            float weightSum =
                settings.screenCenterWeight +
                settings.movementDirectionWeight +
                settings.distanceWeight +
                settings.cameraForwardWeight;

            if (weightSum <= 0.0001f)
                weightSum = 1f;

            float total =
                (screenScore *
                    settings.screenCenterWeight +
                 movementScore *
                    settings.movementDirectionWeight +
                 distanceScore *
                    settings.distanceWeight +
                 cameraScore *
                    settings.cameraForwardWeight) /
                weightSum;

            result =
                new CandidateScore(
                    node,
                    total,
                    screenScore,
                    movementScore,
                    distanceScore,
                    cameraScore,
                    true);

            return true;
        }

        private bool HasLineOfSight(
            AirSlingNode node,
            Vector3 targetPosition)
        {
            Vector3 origin =
                targetCamera.transform.position;

            Vector3 direction =
                targetPosition -
                origin;

            float distance =
                direction.magnitude;

            if (distance <= 0.001f)
                return true;

            direction /=
                distance;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    direction,
                    distance,
                    settings.obstructionMask,
                    QueryTriggerInteraction.Ignore);

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Transform hit =
                    hits[i].transform;

                if (hit == null)
                    continue;

                if (hit == node.transform ||
                    hit.IsChildOf(node.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void SelectBestTarget()
        {
            AirSlingNode best = null;
            float bestScore = 0f;

            if (_candidates.Count > 0)
            {
                CandidateScore candidate =
                    _candidates[0];

                if (candidate.Total >=
                    settings.minimumScore)
                {
                    best = candidate.Node;
                    bestScore = candidate.Total;
                }
            }

            if (_currentTarget != best)
            {
                AirSlingNode previous =
                    _currentTarget;

                if (_currentTarget != null)
                    _currentTarget.SetSelected(false);

                _currentTarget = best;
                _currentScore = bestScore;

                if (_currentTarget != null)
                    _currentTarget.SetSelected(true);

                TargetChanged?.Invoke(
                    previous,
                    _currentTarget);
            }
            else
            {
                _currentScore =
                    bestScore;
            }
        }

        private void ClearTarget()
        {
            if (_currentTarget == null)
            {
                _currentScore = 0f;
                _candidates.Clear();
                return;
            }

            AirSlingNode previous =
                _currentTarget;

            _currentTarget.SetSelected(false);
            _currentTarget = null;
            _currentScore = 0f;
            _candidates.Clear();

            TargetChanged?.Invoke(
                previous,
                null);
        }

        private void OnDisable()
        {
            ClearTarget();
        }
    }
}
