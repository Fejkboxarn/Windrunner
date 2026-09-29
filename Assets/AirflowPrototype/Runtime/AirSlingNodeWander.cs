using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class AirSlingNodeWander : MonoBehaviour
    {
        private enum WanderState
        {
            Paused,
            Walking
        }

        [Header("References")]
        [SerializeField] private AirSlingNode node;
        [SerializeField] private AirSlingNodeHealth health;
        [SerializeField] private AirSlingNodeVisualAnimator visualAnimator;

        [Header("Wander")]
        public bool enableWander = true;

        [Tooltip(
            "Maximum horizontal distance from the wander anchor.")]
        [Min(0f)]
        public float wanderRadius = 2.5f;

        [Min(0f)]
        public float walkSpeed = 0.8f;

        [Tooltip(
            "How quickly the Sling Node root turns toward its next wander point.")]
        [Min(0f)]
        public float turnSpeedDegreesPerSecond = 180f;

        [Min(0f)]
        public float arrivalDistance = 0.10f;

        [Header("Pause Between Walks")]
        [Min(0f)]
        public float minimumPause = 0.8f;

        [Min(0f)]
        public float maximumPause = 2.2f;

        [Header("Anchor")]
        [Tooltip(
            "Optional center of the wander area. If empty, the node's position " +
            "at spawn/respawn becomes the center.")]
        [SerializeField] private Transform wanderCenter;

        [Tooltip(
            "Keeps wandering on the anchor's original Y level. Good for flat " +
            "or gently placed Sling enemies and avoids requiring a NavMesh.")]
        public bool keepAnchorHeight = true;

        [Header("Debug")]
        public bool drawWanderArea = true;

        private WanderState _state =
            WanderState.Paused;

        private Vector3 _anchorPosition;
        private Vector3 _walkTarget;
        private float _pauseRemaining;
        private bool _subscribed;
        private bool _wasInteractionActive;

        public Vector3 AnchorPosition =>
            _anchorPosition;

        private void Awake()
        {
            ResolveReferences();
            CaptureAnchor();
            BeginPause();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!enableWander ||
                wanderRadius <= 0.01f ||
                walkSpeed <= 0.001f ||
                node == null)
            {
                return;
            }

            bool alive =
                health == null ||
                health.IsAlive;

            bool interactionActive =
                node.InteractionActive;

            if (!alive)
            {
                _wasInteractionActive =
                    interactionActive;

                return;
            }

            if (visualAnimator != null &&
                visualAnimator.IsHurtPlaying)
            {
                // Hurt has priority over wander movement/animation.
                return;
            }

            if (interactionActive)
            {
                // The Sling controller owns the engaged animation/movement
                // while the player is actively using this node.
                _wasInteractionActive = true;
                return;
            }

            if (_wasInteractionActive)
            {
                _wasInteractionActive = false;
                BeginPause();
                return;
            }

            if (_state ==
                WanderState.Paused)
            {
                _pauseRemaining -=
                    Time.deltaTime;

                if (_pauseRemaining <= 0f)
                {
                    PickNewTarget();
                }

                return;
            }

            WalkTowardTarget();
        }

        private void WalkTowardTarget()
        {
            Vector3 current =
                transform.position;

            Vector3 target =
                _walkTarget;

            if (keepAnchorHeight)
                target.y = _anchorPosition.y;

            Vector3 planar =
                target -
                current;

            if (keepAnchorHeight)
                planar.y = 0f;

            float distance =
                planar.magnitude;

            if (distance <=
                Mathf.Max(
                    0.01f,
                    arrivalDistance))
            {
                transform.position =
                    keepAnchorHeight
                        ? new Vector3(
                            target.x,
                            _anchorPosition.y,
                            target.z)
                        : target;

                BeginPause();
                return;
            }

            Vector3 direction =
                planar /
                Mathf.Max(
                    0.0001f,
                    distance);

            float step =
                Mathf.Min(
                    distance,
                    walkSpeed *
                    Time.deltaTime);

            Vector3 next =
                current +
                direction *
                step;

            if (keepAnchorHeight)
                next.y = _anchorPosition.y;

            transform.position =
                next;

            Vector3 facing =
                direction;

            facing.y = 0f;

            if (facing.sqrMagnitude >
                0.0001f &&
                turnSpeedDegreesPerSecond > 0f)
            {
                Quaternion desired =
                    Quaternion.LookRotation(
                        facing,
                        Vector3.up);

                transform.rotation =
                    Quaternion.RotateTowards(
                        transform.rotation,
                        desired,
                        turnSpeedDegreesPerSecond *
                        Time.deltaTime);
            }

            if (visualAnimator != null)
                visualAnimator.PlayWalk();
        }

        private void PickNewTarget()
        {
            Vector2 offset =
                UnityEngine.Random.insideUnitCircle *
                wanderRadius;

            _walkTarget =
                _anchorPosition +
                new Vector3(
                    offset.x,
                    0f,
                    offset.y);

            _state =
                WanderState.Walking;

            if (visualAnimator != null)
                visualAnimator.PlayWalk();
        }

        private void BeginPause()
        {
            _state =
                WanderState.Paused;

            float low =
                Mathf.Min(
                    minimumPause,
                    maximumPause);

            float high =
                Mathf.Max(
                    minimumPause,
                    maximumPause);

            _pauseRemaining =
                UnityEngine.Random.Range(
                    low,
                    high);

            if (visualAnimator != null &&
                !visualAnimator.IsHurtPlaying &&
                !visualAnimator.IsDead &&
                (health == null ||
                 health.IsAlive) &&
                (node == null ||
                 !node.InteractionActive))
            {
                visualAnimator.PlayIdle();
            }
        }

        private void CaptureAnchor()
        {
            _anchorPosition =
                wanderCenter != null
                    ? wanderCenter.position
                    : transform.position;

            _walkTarget =
                _anchorPosition;
        }

        private void ResolveReferences()
        {
            if (node == null)
                node = GetComponent<AirSlingNode>();

            if (health == null)
                health = GetComponent<AirSlingNodeHealth>();

            if (visualAnimator == null)
            {
                visualAnimator =
                    GetComponent<AirSlingNodeVisualAnimator>();
            }
        }

        public void Configure(
            AirSlingNode newNode,
            AirSlingNodeHealth newHealth,
            AirSlingNodeVisualAnimator newVisualAnimator)
        {
            if (newNode != null)
                node = newNode;

            if (newHealth != null)
                health = newHealth;

            if (newVisualAnimator != null)
            {
                visualAnimator =
                    newVisualAnimator;
            }

            CaptureAnchor();
            BeginPause();

            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed ||
                health == null)
            {
                return;
            }

            health.Respawned +=
                OnRespawned;

            health.Died +=
                OnDied;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                health == null)
            {
                return;
            }

            health.Respawned -=
                OnRespawned;

            health.Died -=
                OnDied;

            _subscribed = false;
        }

        private void OnRespawned()
        {
            CaptureAnchor();
            BeginPause();
        }

        private void OnDied()
        {
            _state =
                WanderState.Paused;

            _pauseRemaining =
                Mathf.Max(
                    minimumPause,
                    0.1f);
        }

        private void OnValidate()
        {
            wanderRadius =
                Mathf.Max(
                    0f,
                    wanderRadius);

            walkSpeed =
                Mathf.Max(
                    0f,
                    walkSpeed);

            turnSpeedDegreesPerSecond =
                Mathf.Max(
                    0f,
                    turnSpeedDegreesPerSecond);

            arrivalDistance =
                Mathf.Max(
                    0f,
                    arrivalDistance);

            minimumPause =
                Mathf.Max(
                    0f,
                    minimumPause);

            maximumPause =
                Mathf.Max(
                    minimumPause,
                    maximumPause);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawWanderArea)
                return;

            Vector3 center =
                Application.isPlaying
                    ? _anchorPosition
                    : wanderCenter != null
                        ? wanderCenter.position
                        : transform.position;

            Gizmos.color =
                new Color(
                    1f,
                    0.45f,
                    0.12f,
                    0.65f);

            const int segments = 32;

            Vector3 previous =
                center +
                Vector3.right *
                wanderRadius;

            for (int i = 1;
                 i <= segments;
                 i++)
            {
                float angle =
                    i /
                    (float)segments *
                    Mathf.PI *
                    2f;

                Vector3 next =
                    center +
                    new Vector3(
                        Mathf.Cos(angle),
                        0f,
                        Mathf.Sin(angle)) *
                    wanderRadius;

                Gizmos.DrawLine(
                    previous,
                    next);

                previous = next;
            }
        }
    }
}
