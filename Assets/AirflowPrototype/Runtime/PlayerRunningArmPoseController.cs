using UnityEngine;

namespace AirflowPrototype
{
    [DefaultExecutionOrder(1050)]
    [DisallowMultipleComponent]
    public sealed class PlayerRunningArmPoseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirSlingController slingController;
        [SerializeField] private Animator animator;

        [Header("Pose")]
        [SerializeField] private PlayerRunningArmPosePreset activePose;

        [Header("Debug")]
        [Tooltip(
            "Forces the selected arm pose to stay active at Pose Weight, ignoring run speed, grounded state, and Sling suppression. " +
            "Useful for tuning a pose while the character is standing still.")]
        [SerializeField] private bool debugAlwaysShowPose;

        [Range(0f, 1f)]
        [SerializeField] private float poseWeight = 1f;

        [Min(0f)]
        [SerializeField] private float minimumRunSpeed = 2.5f;

        [Min(0.01f)]
        [SerializeField] private float fullPoseSpeed = 6f;

        [Min(0.01f)]
        [SerializeField] private float blendResponse = 12f;

        [Tooltip("Fade the arm re-pose out while airborne.")]
        [SerializeField] private bool groundedOnly = true;

        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;

        private float _currentWeight;

        public PlayerRunningArmPosePreset ActivePose =>
            activePose;

        public void Configure(
            PlayerMotor newMotor,
            AirSlingController newSlingController,
            Animator newAnimator)
        {
            motor = newMotor;
            slingController = newSlingController;
            animator = newAnimator;

            ResolveReferences();
            CacheBones();
        }

        public void SetPose(
            PlayerRunningArmPosePreset pose)
        {
            activePose = pose;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheBones();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheBones();
        }

        private void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (slingController == null)
                slingController = GetComponent<AirSlingController>();

            if (animator == null)
            {
                Animator[] animators =
                    GetComponentsInChildren<Animator>(
                        true);

                for (int i = 0; i < animators.Length; i++)
                {
                    if (animators[i] != null &&
                        animators[i].isHuman)
                    {
                        animator = animators[i];
                        break;
                    }
                }
            }
        }

        private void CacheBones()
        {
            if (animator == null ||
                !animator.isHuman)
            {
                return;
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
        }

        private void LateUpdate()
        {
            if (animator == null ||
                motor == null ||
                activePose == null)
            {
                SmoothWeight(
                    0f);

                return;
            }

            bool blockedBySling =
                slingController != null &&
                slingController.IsSlinging;

            bool allowedByGround =
                !groundedOnly ||
                motor.IsGrounded;

            float speed01 =
                Mathf.InverseLerp(
                    minimumRunSpeed,
                    Mathf.Max(
                        minimumRunSpeed + 0.01f,
                        fullPoseSpeed),
                    motor.HorizontalSpeed);

            float target;

            if (debugAlwaysShowPose)
            {
                target =
                    poseWeight;
            }
            else
            {
                target =
                    blockedBySling ||
                    !allowedByGround
                        ? 0f
                        : speed01 * poseWeight;
            }

            SmoothWeight(
                target);

            if (_currentWeight <= 0.0001f)
                return;

            ApplyOffset(
                _leftUpperArm,
                activePose.leftUpperArmEuler,
                _currentWeight);

            ApplyOffset(
                _leftLowerArm,
                activePose.leftLowerArmEuler,
                _currentWeight);

            ApplyOffset(
                _leftHand,
                activePose.leftHandEuler,
                _currentWeight);

            ApplyOffset(
                _rightUpperArm,
                activePose.rightUpperArmEuler,
                _currentWeight);

            ApplyOffset(
                _rightLowerArm,
                activePose.rightLowerArmEuler,
                _currentWeight);

            ApplyOffset(
                _rightHand,
                activePose.rightHandEuler,
                _currentWeight);
        }

        private void SmoothWeight(
            float target)
        {
            float t =
                1f -
                Mathf.Exp(
                    -Mathf.Max(
                        0.01f,
                        blendResponse) *
                    Time.deltaTime);

            _currentWeight =
                Mathf.Lerp(
                    _currentWeight,
                    Mathf.Clamp01(target),
                    t);
        }

        private static void ApplyOffset(
            Transform bone,
            Vector3 euler,
            float weight)
        {
            if (bone == null)
                return;

            Quaternion additive =
                Quaternion.Slerp(
                    Quaternion.identity,
                    Quaternion.Euler(euler),
                    Mathf.Clamp01(weight));

            // Animator supplies the moving arm pose first. We then add a local
            // rotation offset, preserving the original swing/motion underneath.
            bone.localRotation =
                bone.localRotation *
                additive;
        }

        private void OnValidate()
        {
            poseWeight =
                Mathf.Clamp01(
                    poseWeight);

            minimumRunSpeed =
                Mathf.Max(
                    0f,
                    minimumRunSpeed);

            fullPoseSpeed =
                Mathf.Max(
                    minimumRunSpeed + 0.01f,
                    fullPoseSpeed);

            blendResponse =
                Mathf.Max(
                    0.01f,
                    blendResponse);
        }
    }
}
