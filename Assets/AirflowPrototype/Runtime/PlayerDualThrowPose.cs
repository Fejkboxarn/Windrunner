using UnityEngine;

namespace AirflowPrototype
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class PlayerDualThrowPose : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AirCaster caster;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform leftThrowOrigin;
        [SerializeField] private Transform rightThrowOrigin;

        [Header("Charge Pose")]
        [Range(0f, 1f)]
        [SerializeField] private float chargeAimWeight = 0.36f;

        [Range(0f, 50f)]
        [SerializeField] private float maximumChestYaw = 16f;

        [Range(0f, 90f)]
        [SerializeField] private float maximumArmAimDegrees = 58f;

        [Range(0f, 45f)]
        [SerializeField] private float chargeArmSpreadDegrees = 18f;

        [Range(0f, 35f)]
        [SerializeField] private float chargeElbowLiftDegrees = 12f;

        [Min(0.01f)]
        [SerializeField] private float chargePoseResponse = 15f;

        [Header("Throw")]
        [Tooltip(
            "Right hand releases first. Left follows after this delay.")]
        [Min(0f)]
        [SerializeField] private float leftHandDelay = 0.055f;

        [Min(0.03f)]
        [SerializeField] private float throwPulseDuration = 0.17f;

        [Range(0f, 45f)]
        [SerializeField] private float forearmSnapDegrees = 16f;

        [Range(0f, 45f)]
        [SerializeField] private float wristSnapDegrees = 12f;

        [Min(0.01f)]
        [SerializeField] private float recoveryResponse = 18f;

        [Header("Dual Throw Tracers")]
        public bool spawnDualThrowTracers = true;

        [Min(0.03f)]
        public float tracerTravelDuration = 0.11f;

        [Min(0.001f)]
        public float tracerWidth = 0.045f;

        public Color tracerColor =
            new Color(
                0.80f,
                0.94f,
                1f,
                0.95f);

        private Transform _chest;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;

        private AirNode _chargeTarget;
        private Vector3 _releaseTargetPosition;

        private bool _charging;
        private bool _releasing;

        private float _chargePoseWeight;
        private float _releaseElapsed;

        private bool _subscribed;

        public Transform LeftThrowOrigin =>
            leftThrowOrigin != null
                ? leftThrowOrigin
                : _leftHand;

        public Transform RightThrowOrigin =>
            rightThrowOrigin != null
                ? rightThrowOrigin
                : _rightHand;

        public void Configure(
            AirCaster newCaster,
            Animator newAnimator,
            Transform newLeftThrowOrigin,
            Transform newRightThrowOrigin)
        {
            Unsubscribe();

            caster = newCaster;
            animator = newAnimator;
            leftThrowOrigin = newLeftThrowOrigin;
            rightThrowOrigin = newRightThrowOrigin;

            ResolveHumanoidBones();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveHumanoidBones();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveHumanoidBones();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            _charging = false;
            _releasing = false;
            _chargePoseWeight = 0f;
            _releaseElapsed = 0f;
            _chargeTarget = null;
        }

        private void Update()
        {
            float desiredCharge =
                _charging &&
                caster != null
                    ? Mathf.Lerp(
                        0.35f,
                        1f,
                        caster.TimingProgress01)
                    : 0f;

            float response =
                desiredCharge >
                _chargePoseWeight
                    ? chargePoseResponse
                    : recoveryResponse;

            _chargePoseWeight =
                Mathf.Lerp(
                    _chargePoseWeight,
                    desiredCharge,
                    1f -
                    Mathf.Exp(
                        -Mathf.Max(
                            0.01f,
                            response) *
                        Time.unscaledDeltaTime));

            if (_releasing)
            {
                _releaseElapsed +=
                    Time.unscaledDeltaTime;

                if (_releaseElapsed >=
                    leftHandDelay +
                    throwPulseDuration)
                {
                    _releasing = false;
                }
            }
        }

        private void LateUpdate()
        {
            if (animator == null ||
                !animator.isHuman)
            {
                return;
            }

            if (_chest == null ||
                _leftUpperArm == null ||
                _rightUpperArm == null)
            {
                ResolveHumanoidBones();
            }

            Vector3 targetPosition =
                GetPoseTargetPosition();

            float rightPulse =
                _releasing
                    ? EvaluateThrowPulse(
                        _releaseElapsed)
                    : 0f;

            float leftPulse =
                _releasing
                    ? EvaluateThrowPulse(
                        _releaseElapsed -
                        leftHandDelay)
                    : 0f;

            float releaseWeight =
                Mathf.Max(
                    rightPulse,
                    leftPulse);

            float overallWeight =
                Mathf.Clamp01(
                    Mathf.Max(
                        _chargePoseWeight,
                        releaseWeight));

            if (overallWeight <= 0.001f)
                return;

            ApplyChestAim(
                targetPosition,
                overallWeight);

            ApplyArmPose(
                _rightUpperArm,
                _rightLowerArm,
                _rightHand,
                targetPosition,
                _chargePoseWeight,
                rightPulse,
                1f);

            ApplyArmPose(
                _leftUpperArm,
                _leftLowerArm,
                _leftHand,
                targetPosition,
                _chargePoseWeight,
                leftPulse,
                -1f);
        }

        private void ApplyChestAim(
            Vector3 targetPosition,
            float weight)
        {
            if (_chest == null)
                return;

            Vector3 planar =
                targetPosition -
                _chest.position;

            planar.y = 0f;

            if (planar.sqrMagnitude <
                0.0001f)
            {
                return;
            }

            Vector3 forward =
                transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude <
                0.0001f)
            {
                return;
            }

            float yaw =
                Vector3.SignedAngle(
                    forward.normalized,
                    planar.normalized,
                    Vector3.up);

            yaw =
                Mathf.Clamp(
                    yaw,
                    -maximumChestYaw,
                    maximumChestYaw);

            Quaternion additive =
                Quaternion.AngleAxis(
                    yaw *
                    weight,
                    Vector3.up);

            _chest.rotation =
                additive *
                _chest.rotation;
        }

        private void ApplyArmPose(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 targetPosition,
            float chargeWeight,
            float throwPulse,
            float side)
        {
            if (upperArm == null ||
                hand == null)
            {
                return;
            }

            float aimWeight =
                Mathf.Clamp01(
                    chargeWeight *
                    chargeAimWeight +
                    throwPulse);

            Vector3 currentDirection =
                hand.position -
                upperArm.position;

            Vector3 desiredDirection =
                targetPosition -
                upperArm.position;

            if (currentDirection.sqrMagnitude >
                    0.0001f &&
                desiredDirection.sqrMagnitude >
                    0.0001f)
            {
                Quaternion fullDelta =
                    Quaternion.FromToRotation(
                        currentDirection.normalized,
                        desiredDirection.normalized);

                Quaternion clampedDelta =
                    Quaternion.RotateTowards(
                        Quaternion.identity,
                        fullDelta,
                        maximumArmAimDegrees *
                        aimWeight);

                upperArm.rotation =
                    clampedDelta *
                    upperArm.rotation;
            }

            if (chargeWeight > 0.001f)
            {
                Quaternion spread =
                    Quaternion.AngleAxis(
                        -side *
                        chargeArmSpreadDegrees *
                        chargeWeight,
                        transform.up);

                Quaternion lift =
                    Quaternion.AngleAxis(
                        -chargeElbowLiftDegrees *
                        chargeWeight,
                        transform.right);

                upperArm.rotation =
                    spread *
                    lift *
                    upperArm.rotation;
            }

            if (throwPulse <= 0.001f)
                return;

            if (lowerArm != null)
            {
                Quaternion forearmSnap =
                    Quaternion.AngleAxis(
                        -forearmSnapDegrees *
                        throwPulse,
                        transform.right);

                lowerArm.rotation =
                    forearmSnap *
                    lowerArm.rotation;
            }

            Quaternion wristSnap =
                Quaternion.AngleAxis(
                    -wristSnapDegrees *
                    throwPulse,
                    transform.right);

            hand.rotation =
                wristSnap *
                hand.rotation;
        }

        private float EvaluateThrowPulse(
            float localTime)
        {
            if (localTime < 0f ||
                localTime >
                throwPulseDuration)
            {
                return 0f;
            }

            float t =
                Mathf.Clamp01(
                    localTime /
                    Mathf.Max(
                        0.03f,
                        throwPulseDuration));

            // Fast attack, slightly slower recovery.
            if (t < 0.34f)
            {
                return
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t / 0.34f);
            }

            return
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    (t - 0.34f) /
                    0.66f);
        }

        private Vector3 GetPoseTargetPosition()
        {
            if (_charging &&
                _chargeTarget != null &&
                _chargeTarget.IsAvailable)
            {
                return
                    _chargeTarget.TargetPosition;
            }

            if (_releasing)
                return _releaseTargetPosition;

            if (caster != null &&
                caster.CurrentTarget != null)
            {
                return
                    caster.CurrentTarget.TargetPosition;
            }

            return
                transform.position +
                transform.forward *
                6f +
                Vector3.up;
        }

        private void ResolveReferences()
        {
            if (caster == null)
                caster = GetComponent<AirCaster>();

            if (animator == null)
            {
                Animator[] animators =
                    GetComponentsInChildren<Animator>(
                        true);

                for (int i = 0;
                     i < animators.Length;
                     i++)
                {
                    if (animators[i] != null &&
                        animators[i].isHuman)
                    {
                        animator =
                            animators[i];

                        break;
                    }
                }
            }
        }

        private void ResolveHumanoidBones()
        {
            if (animator == null ||
                !animator.isHuman)
            {
                return;
            }

            _chest =
                animator.GetBoneTransform(
                    HumanBodyBones.Chest);

            if (_chest == null)
            {
                _chest =
                    animator.GetBoneTransform(
                        HumanBodyBones.UpperChest);
            }

            _leftUpperArm =
                animator.GetBoneTransform(
                    HumanBodyBones.LeftUpperArm);

            _leftLowerArm =
                animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerArm);

            _leftHand =
                animator.GetBoneTransform(
                    HumanBodyBones.LeftHand);

            _rightUpperArm =
                animator.GetBoneTransform(
                    HumanBodyBones.RightUpperArm);

            _rightLowerArm =
                animator.GetBoneTransform(
                    HumanBodyBones.RightLowerArm);

            _rightHand =
                animator.GetBoneTransform(
                    HumanBodyBones.RightHand);

            if (leftThrowOrigin == null &&
                _leftHand != null)
            {
                leftThrowOrigin =
                    _leftHand.Find(
                        "Left Throw Origin");
            }

            if (rightThrowOrigin == null &&
                _rightHand != null)
            {
                rightThrowOrigin =
                    _rightHand.Find(
                        "Right Throw Origin");
            }
        }

        private void Subscribe()
        {
            if (_subscribed ||
                caster == null)
            {
                return;
            }

            caster.TargetedChargeStarted +=
                OnTargetedChargeStarted;

            caster.TargetedPulseReleased +=
                OnTargetedPulseReleased;

            caster.ChargeCanceled +=
                OnChargeCanceled;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed ||
                caster == null)
            {
                return;
            }

            caster.TargetedChargeStarted -=
                OnTargetedChargeStarted;

            caster.TargetedPulseReleased -=
                OnTargetedPulseReleased;

            caster.ChargeCanceled -=
                OnChargeCanceled;

            _subscribed = false;
        }

        private void OnTargetedChargeStarted(
            AirNode node)
        {
            _chargeTarget = node;
            _charging = node != null;
            _releasing = false;
            _releaseElapsed = 0f;
        }

        private void OnTargetedPulseReleased(
            AirNode node,
            float charge01,
            bool ready)
        {
            if (node == null)
                return;

            _releaseTargetPosition =
                node.TargetPosition;

            _chargeTarget = null;
            _charging = false;

            _releasing = true;
            _releaseElapsed = 0f;

            if (!spawnDualThrowTracers)
                return;

            Transform right =
                RightThrowOrigin;

            Transform left =
                LeftThrowOrigin;

            if (right != null)
            {
                PlayerDualThrowTracerVisual.Spawn(
                    right,
                    node,
                    0f,
                    tracerTravelDuration,
                    tracerWidth,
                    tracerColor);
            }

            if (left != null)
            {
                PlayerDualThrowTracerVisual.Spawn(
                    left,
                    node,
                    leftHandDelay,
                    tracerTravelDuration,
                    tracerWidth,
                    tracerColor);
            }
        }

        private void OnChargeCanceled()
        {
            _chargeTarget = null;
            _charging = false;
        }

        private void OnValidate()
        {
            chargePoseResponse =
                Mathf.Max(
                    0.01f,
                    chargePoseResponse);

            recoveryResponse =
                Mathf.Max(
                    0.01f,
                    recoveryResponse);

            leftHandDelay =
                Mathf.Max(
                    0f,
                    leftHandDelay);

            throwPulseDuration =
                Mathf.Max(
                    0.03f,
                    throwPulseDuration);

            tracerTravelDuration =
                Mathf.Max(
                    0.03f,
                    tracerTravelDuration);

            tracerWidth =
                Mathf.Max(
                    0.001f,
                    tracerWidth);
        }
    }
}
