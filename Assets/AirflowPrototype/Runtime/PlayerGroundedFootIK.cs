using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerGroundedFootIK : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirSlingController slingController;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Min(0.01f)]
        [SerializeField] private float raycastStartHeight = 0.45f;

        [Min(0.01f)]
        [SerializeField] private float raycastDistance = 0.85f;

        [Range(0f, 80f)]
        [SerializeField] private float maximumGroundAngle = 60f;

        [Header("Foot Placement")]
        [Tooltip("Small lift along the ground normal so the sole does not clip into the surface.")]
        [Min(0f)]
        [SerializeField] private float soleOffset = 0.025f;

        [Tooltip("Animated foot height where the foot is considered fully planted.")]
        [Min(0f)]
        [SerializeField] private float fullContactHeight = 0.07f;

        [Tooltip("Animated foot height where IK fully releases so the swing foot can lift normally.")]
        [Min(0.01f)]
        [SerializeField] private float releaseHeight = 0.28f;

        [Min(0.01f)]
        [SerializeField] private float weightResponse = 18f;

        [Header("Pelvis")]
        [Tooltip("Allows a small visual pelvis drop so the downhill leg can reach without stretching.")]
        [SerializeField] private bool adjustPelvis = true;

        [Min(0f)]
        [SerializeField] private float maximumPelvisDrop = 0.18f;

        [Min(0.01f)]
        [SerializeField] private float pelvisResponse = 14f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRays;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private float _leftWeight;
        private float _rightWeight;
        private float _pelvisOffset;

        private FootSolution _left;
        private FootSolution _right;

        private struct FootSolution
        {
            public bool valid;
            public Vector3 animatedPosition;
            public Vector3 targetPosition;
            public Quaternion targetRotation;
            public float desiredWeight;
            public float verticalDelta;
        }

        public void Configure(
            Animator newAnimator,
            PlayerMotor newMotor,
            AirSlingController newSlingController)
        {
            animator = newAnimator;
            motor = newMotor;
            slingController = newSlingController;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (motor == null)
                motor = GetComponentInParent<PlayerMotor>();

            if (slingController == null)
                slingController = GetComponentInParent<AirSlingController>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 ||
                animator == null ||
                !animator.isHuman)
            {
                return;
            }

            bool grounded =
                motor != null &&
                motor.IsGrounded;

            bool blockedBySling =
                slingController != null &&
                slingController.IsSlinging;

            bool active =
                grounded &&
                !blockedBySling;

            if (!active)
            {
                FadeOutIK();
                return;
            }

            _left =
                SolveFoot(
                    AvatarIKGoal.LeftFoot);

            _right =
                SolveFoot(
                    AvatarIKGoal.RightFoot);

            _leftWeight =
                SmoothWeight(
                    _leftWeight,
                    _left.valid
                        ? _left.desiredWeight
                        : 0f);

            _rightWeight =
                SmoothWeight(
                    _rightWeight,
                    _right.valid
                        ? _right.desiredWeight
                        : 0f);

            ApplyPelvis();
            ApplyFoot(
                AvatarIKGoal.LeftFoot,
                _left,
                _leftWeight);

            ApplyFoot(
                AvatarIKGoal.RightFoot,
                _right,
                _rightWeight);
        }

        private FootSolution SolveFoot(
            AvatarIKGoal goal)
        {
            FootSolution result =
                new FootSolution();

            Vector3 animatedPosition =
                animator.GetIKPosition(goal);

            Quaternion animatedRotation =
                animator.GetIKRotation(goal);

            result.animatedPosition =
                animatedPosition;

            Vector3 origin =
                animatedPosition +
                Vector3.up *
                raycastStartHeight;

            float castLength =
                raycastStartHeight +
                raycastDistance;

            if (!TryFindGround(
                    origin,
                    castLength,
                    out RaycastHit hit))
            {
                if (drawDebugRays)
                {
                    Debug.DrawRay(
                        origin,
                        Vector3.down *
                        castLength,
                        Color.red);
                }

                return result;
            }

            if (Vector3.Angle(
                    hit.normal,
                    Vector3.up) >
                maximumGroundAngle)
            {
                return result;
            }

            Vector3 targetPosition =
                hit.point +
                hit.normal *
                soleOffset;

            float animatedHeight =
                Vector3.Dot(
                    animatedPosition -
                    hit.point,
                    Vector3.up);

            float desiredWeight =
                1f -
                Mathf.InverseLerp(
                    fullContactHeight,
                    Mathf.Max(
                        fullContactHeight +
                        0.01f,
                        releaseHeight),
                    animatedHeight);

            desiredWeight =
                Mathf.Clamp01(
                    desiredWeight);

            Quaternion slopeRotation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    hit.normal);

            Quaternion targetRotation =
                slopeRotation *
                animatedRotation;

            result.valid = true;
            result.targetPosition = targetPosition;
            result.targetRotation = targetRotation;
            result.desiredWeight = desiredWeight;
            result.verticalDelta =
                targetPosition.y -
                animatedPosition.y;

            if (drawDebugRays)
            {
                Debug.DrawLine(
                    origin,
                    hit.point,
                    Color.green);

                Debug.DrawRay(
                    hit.point,
                    hit.normal *
                    0.22f,
                    Color.cyan);
            }

            return result;
        }

        private bool TryFindGround(
            Vector3 origin,
            float distance,
            out RaycastHit bestHit)
        {
            bestHit = default;

            int count =
                Physics.RaycastNonAlloc(
                    origin,
                    Vector3.down,
                    _hits,
                    distance,
                    groundMask,
                    QueryTriggerInteraction.Ignore);

            float bestDistance =
                float.PositiveInfinity;

            bool found = false;

            for (int i = 0;
                 i < count;
                 i++)
            {
                RaycastHit hit =
                    _hits[i];

                if (hit.collider == null)
                    continue;

                if (motor != null &&
                    hit.collider.transform.IsChildOf(
                        motor.transform))
                {
                    continue;
                }

                if (hit.distance >=
                    bestDistance)
                {
                    continue;
                }

                bestDistance =
                    hit.distance;

                bestHit =
                    hit;

                found =
                    true;
            }

            return found;
        }

        private void ApplyPelvis()
        {
            float targetOffset = 0f;

            if (adjustPelvis)
            {
                bool hasLeft =
                    _left.valid &&
                    _leftWeight > 0.05f;

                bool hasRight =
                    _right.valid &&
                    _rightWeight > 0.05f;

                if (hasLeft &&
                    hasRight)
                {
                    targetOffset =
                        Mathf.Min(
                            _left.verticalDelta,
                            _right.verticalDelta);
                }
                else if (hasLeft)
                {
                    targetOffset =
                        _left.verticalDelta;
                }
                else if (hasRight)
                {
                    targetOffset =
                        _right.verticalDelta;
                }

                targetOffset =
                    Mathf.Clamp(
                        targetOffset,
                        -maximumPelvisDrop,
                        0f);
            }

            float t =
                1f -
                Mathf.Exp(
                    -Mathf.Max(
                        0.01f,
                        pelvisResponse) *
                    Time.deltaTime);

            _pelvisOffset =
                Mathf.Lerp(
                    _pelvisOffset,
                    targetOffset,
                    t);

            if (Mathf.Abs(
                    _pelvisOffset) <=
                0.0001f)
            {
                return;
            }

            Vector3 body =
                animator.bodyPosition;

            body.y +=
                _pelvisOffset;

            animator.bodyPosition =
                body;
        }

        private void ApplyFoot(
            AvatarIKGoal goal,
            FootSolution solution,
            float weight)
        {
            float applied =
                solution.valid
                    ? Mathf.Clamp01(weight)
                    : 0f;

            animator.SetIKPositionWeight(
                goal,
                applied);

            animator.SetIKRotationWeight(
                goal,
                applied);

            if (!solution.valid)
                return;

            animator.SetIKPosition(
                goal,
                solution.targetPosition);

            animator.SetIKRotation(
                goal,
                solution.targetRotation);
        }

        private float SmoothWeight(
            float current,
            float target)
        {
            float t =
                1f -
                Mathf.Exp(
                    -Mathf.Max(
                        0.01f,
                        weightResponse) *
                    Time.deltaTime);

            return Mathf.Lerp(
                current,
                target,
                t);
        }

        private void FadeOutIK()
        {
            _leftWeight =
                SmoothWeight(
                    _leftWeight,
                    0f);

            _rightWeight =
                SmoothWeight(
                    _rightWeight,
                    0f);

            _pelvisOffset =
                Mathf.Lerp(
                    _pelvisOffset,
                    0f,
                    1f -
                    Mathf.Exp(
                        -Mathf.Max(
                            0.01f,
                            pelvisResponse) *
                        Time.deltaTime));

            animator.SetIKPositionWeight(
                AvatarIKGoal.LeftFoot,
                _leftWeight);

            animator.SetIKRotationWeight(
                AvatarIKGoal.LeftFoot,
                _leftWeight);

            animator.SetIKPositionWeight(
                AvatarIKGoal.RightFoot,
                _rightWeight);

            animator.SetIKRotationWeight(
                AvatarIKGoal.RightFoot,
                _rightWeight);

            if (Mathf.Abs(
                    _pelvisOffset) >
                0.0001f)
            {
                Vector3 body =
                    animator.bodyPosition;

                body.y +=
                    _pelvisOffset;

                animator.bodyPosition =
                    body;
            }
        }

        private void OnValidate()
        {
            raycastStartHeight =
                Mathf.Max(
                    0.01f,
                    raycastStartHeight);

            raycastDistance =
                Mathf.Max(
                    0.01f,
                    raycastDistance);

            maximumGroundAngle =
                Mathf.Clamp(
                    maximumGroundAngle,
                    0f,
                    80f);

            soleOffset =
                Mathf.Max(
                    0f,
                    soleOffset);

            fullContactHeight =
                Mathf.Max(
                    0f,
                    fullContactHeight);

            releaseHeight =
                Mathf.Max(
                    fullContactHeight +
                    0.01f,
                    releaseHeight);

            weightResponse =
                Mathf.Max(
                    0.01f,
                    weightResponse);

            maximumPelvisDrop =
                Mathf.Max(
                    0f,
                    maximumPelvisDrop);

            pelvisResponse =
                Mathf.Max(
                    0.01f,
                    pelvisResponse);
        }
    }
}
