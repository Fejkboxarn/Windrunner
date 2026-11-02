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

        // Reused across every node and frame. A full buffer falls back to
        // RaycastAll so crowded geometry cannot silently hide an obstruction.
        private readonly RaycastHit[] _losHits = new RaycastHit[16];
        private int _losQueries;
        private int _losFullBufferFallbacks;

        private AirSlingNode _currentTarget;
        private float _currentScore;

        public AirSlingNode CurrentTarget =>
            _currentTarget;

        public float CurrentScore =>
            _currentScore;

        public IReadOnlyList<CandidateScore> DebugCandidates =>
            _candidates;

        public int LosQueries => _losQueries;
        public int LosFullBufferFallbacks => _losFullBufferFallbacks;

#if UNITY_EDITOR
        // Invoked only by the optional 5J parity tool. Not present in builds.
        public bool DebugHasLineOfSight(AirSlingNode node) =>
            node != null && HasLineOfSight(node, node.TargetPosition);
#endif

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

            if (!AirSlingTargetScoreMath.TryPrepare(
                    transform.position,
                    targetPosition,
                    targetCamera.transform.position,
                    targetCamera.transform.forward,
                    settings.maxTargetDistance,
                    settings.maxCameraAngle,
                    out AirSlingTargetGeometry geometry))
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

            AirSlingTargetScores scores =
                AirSlingTargetScoreMath.ComputeComponents(
                    geometry,
                    viewport,
                    motor.PlanarVelocity,
                    settings.maxTargetDistance);

            bool visible =
                HasLineOfSight(
                    node,
                    targetPosition);

            if (!visible)
            {
                result = default;
                return false;
            }

            float total = AirSlingTargetScoreMath.WeightedTotal(scores, settings);

            result =
                new CandidateScore(
                    node,
                    total,
                    scores.Screen,
                    scores.Movement,
                    scores.Distance,
                    scores.CameraForward,
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

            _losQueries++;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                _losHits,
                distance,
                settings.obstructionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0;
                 i < hitCount;
                 i++)
            {
                Transform hit =
                    _losHits[i].transform;

                if (hit == null)
                    continue;

                if (hit == node.transform ||
                    hit.IsChildOf(node.transform))
                {
                    continue;
                }

                return false;
            }

            // RaycastNonAlloc may omit arbitrary farther hits when full.
            // Only fall back if the buffered hits were all valid node parts;
            // a non-node obstruction found above is already conclusive.
            if (hitCount == _losHits.Length)
            {
                _losFullBufferFallbacks++;
                RaycastHit[] allHits = Physics.RaycastAll(
                    origin,
                    direction,
                    distance,
                    settings.obstructionMask,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < allHits.Length; i++)
                {
                    Transform hit = allHits[i].transform;
                    if (hit == null || hit == node.transform ||
                        hit.IsChildOf(node.transform))
                        continue;

                    return false;
                }
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
