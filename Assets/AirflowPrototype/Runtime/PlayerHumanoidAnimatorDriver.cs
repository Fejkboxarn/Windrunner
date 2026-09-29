using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerHumanoidAnimatorDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMovementVisuals movementVisuals;
        [SerializeField] private AirSlingController slingController;
        [SerializeField] private Animator animator;
        [SerializeField] private Camera movementCamera;

        [Header("Animator Parameters")]
        [SerializeField] private string speedFloatParameter = "Speed";
        [SerializeField] private string groundedBoolParameter = "Grounded";
        [SerializeField] private string verticalSpeedFloatParameter = "VerticalSpeed";
        [SerializeField] private string locomotionDirectionFloatParameter = "LocomotionDirection";
        [SerializeField] private string hasMoveInputBoolParameter = "HasMoveInput";

        [SerializeField] private string runStartTriggerParameter = "RunStart";
        [SerializeField] private string runStartDirectionIntParameter = "RunStartDirection";
        [SerializeField] private string runStopTriggerParameter = "RunStop";
        [SerializeField] private string hardTurnTriggerParameter = "HardTurn";
        [SerializeField] private string hardTurnDirectionIntParameter = "HardTurnDirection";

        [SerializeField] private string jumpTriggerParameter = "Jump";
        [SerializeField] private string doubleJumpTriggerParameter = "DoubleJump";
        [SerializeField] private string doubleJumpVariantIntParameter = "DoubleJumpVariant";
        [SerializeField] private string doubleJumpMirrorBoolParameter = "DoubleJumpMirror";
        [SerializeField] private string landTriggerParameter = "Land";

        [Header("Speed Normalization")]
        [Min(0.1f)]
        [SerializeField] private float speedForFullRun = 8.5f;

        [Min(1f)]
        [SerializeField] private float maximumNormalizedSpeed = 1.35f;

        [Header("Generated Locomotion Tuning")]
        [SerializeField, HideInInspector] private float moveInputDeadzone = 0.12f;
        [SerializeField, HideInInspector] private float startMaximumSpeed = 3f;
        [SerializeField, HideInInspector] private float stopMinimumSpeed = 2.2f;
        [SerializeField, HideInInspector] private float hardTurnAngle = 112f;
        [SerializeField, HideInInspector] private float hardTurnMinimumSpeed = 4f;
        [SerializeField, HideInInspector] private float hardTurnCooldown = 0.45f;
        [SerializeField, HideInInspector] private float forwardStartHalfAngle = 32f;
        [SerializeField, HideInInspector] private float backwardStartAngle = 112f;
        [SerializeField, HideInInspector] private float turnLeanBlendExtension = 0.5f;
        [SerializeField, HideInInspector] private float turnRateForFullLean = 240f;

        [Header("Generated Double Jump Tuning")]
        [SerializeField, HideInInspector] private int doubleJumpVariantCount = 1;
        [SerializeField, HideInInspector] private float doubleJumpMirrorChancePercent = 35f;

        [Header("Sling Parameters")]
        [SerializeField] private string slingStartTriggerParameter = "SlingStart";
        [SerializeField] private string slingAirBoolParameter = "SlingAir";
        [SerializeField] private string slingImpactTriggerParameter = "SlingImpact";

        [Header("Sling Override Layer")]
        [SerializeField] private bool manageSlingLayerWeight = true;
        [SerializeField] private string slingLayerName = "Sling";

        [Min(0.01f)]
        [SerializeField] private float slingLayerBlendInResponse = 22f;

        [Min(0.01f)]
        [SerializeField] private float slingLayerBlendOutResponse = 12f;

        [Header("Sling Impact Visibility")]
        [Min(0.2f)]
        [SerializeField] private float slingImpactLayerFailSafeDuration = 1.5f;

        private static readonly int SlingImpactStateHash =
            Animator.StringToHash("Sling Impact");

        private readonly HashSet<int> _floatParameters =
            new HashSet<int>();

        private readonly HashSet<int> _boolParameters =
            new HashSet<int>();

        private readonly HashSet<int> _intParameters =
            new HashSet<int>();

        private readonly HashSet<int> _triggerParameters =
            new HashSet<int>();

        private int _slingLayerIndex = -1;
        private float _slingLayerTargetWeight;

        private int _lastDoubleJumpVariant = -1;

        private bool _hadMoveInput;
        private float _hardTurnCooldownRemaining;
        private Vector3 _lastAnimatorForward;
        private bool _hasLastAnimatorForward;

        private bool _waitingForSlingImpactLayerRelease;
        private bool _slingImpactStateObserved;
        private float _slingImpactLayerHoldElapsed;

        private bool _subscribed;

        public Animator Animator => animator;

        public bool HasAuthoredDoubleJump
        {
            get
            {
                if (animator == null ||
                    string.IsNullOrWhiteSpace(
                        doubleJumpTriggerParameter))
                {
                    return false;
                }

                return _triggerParameters.Contains(
                    Animator.StringToHash(
                        doubleJumpTriggerParameter));
            }
        }

        public void ApplyGeneratedProfileSettings(
            PlayerHumanoidAnimationProfile profile)
        {
            if (profile == null)
                return;

            profile.EnsureCurrentDefaults();

            doubleJumpVariantCount =
                Mathf.Max(
                    1,
                    profile.DoubleJumpVariantCount);

            doubleJumpMirrorChancePercent =
                Mathf.Clamp(
                    profile.doubleJumpMirrorChancePercent,
                    0f,
                    100f);

            moveInputDeadzone =
                profile.moveInputDeadzone;

            startMaximumSpeed =
                profile.startMaximumSpeed;

            stopMinimumSpeed =
                profile.stopMinimumSpeed;

            hardTurnAngle =
                profile.hardTurnAngle;

            hardTurnMinimumSpeed =
                profile.hardTurnMinimumSpeed;

            hardTurnCooldown =
                profile.hardTurnCooldown;

            forwardStartHalfAngle =
                profile.forwardStartHalfAngle;

            backwardStartAngle =
                profile.backwardStartAngle;

            turnLeanBlendExtension =
                profile.turnLeanBlendExtension;

            turnRateForFullLean =
                profile.turnRateForFullLean;

            if (_lastDoubleJumpVariant >=
                doubleJumpVariantCount)
            {
                _lastDoubleJumpVariant = -1;
            }
        }

        public void SetDoubleJumpVariantCount(
            int count)
        {
            doubleJumpVariantCount =
                Mathf.Max(
                    1,
                    count);

            if (_lastDoubleJumpVariant >=
                doubleJumpVariantCount)
            {
                _lastDoubleJumpVariant = -1;
            }
        }

        public void Configure(
            PlayerMotor newMotor,
            PlayerMovementVisuals newMovementVisuals,
            AirSlingController newSlingController,
            Animator newAnimator)
        {
            Unsubscribe();

            motor = newMotor;
            movementVisuals = newMovementVisuals;
            slingController = newSlingController;
            animator = newAnimator;

            ResolveReferences();
            RefreshAnimatorCache();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshAnimatorCache();
            CaptureAnimatorForward();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshAnimatorCache();
            CaptureAnimatorForward();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            SetBoolIfPresent(
                slingAirBoolParameter,
                false);

            _slingLayerTargetWeight = 0f;

            _waitingForSlingImpactLayerRelease = false;
            _slingImpactStateObserved = false;
            _slingImpactLayerHoldElapsed = 0f;

            _hadMoveInput = false;
            _hardTurnCooldownRemaining = 0f;
            _hasLastAnimatorForward = false;

            if (animator != null &&
                _slingLayerIndex >= 0)
            {
                animator.SetLayerWeight(
                    _slingLayerIndex,
                    0f);
            }
        }

        private void Update()
        {
            if (animator == null ||
                motor == null)
            {
                ResolveReferences();

                if (animator == null ||
                    motor == null)
                {
                    return;
                }
            }

            float dt =
                Mathf.Max(
                    0.0001f,
                    Time.deltaTime);

            if (_hardTurnCooldownRemaining > 0f)
            {
                _hardTurnCooldownRemaining =
                    Mathf.Max(
                        0f,
                        _hardTurnCooldownRemaining - dt);
            }

            float normalizedSpeed =
                Mathf.Clamp(
                    motor.HorizontalSpeed /
                    Mathf.Max(
                        0.1f,
                        speedForFullRun),
                    0f,
                    maximumNormalizedSpeed);

            bool grounded =
                motor.IsGrounded;

            Vector2 moveInput =
                input != null
                    ? input.Move
                    : Vector2.zero;

            bool hasMoveInput =
                moveInput.sqrMagnitude >=
                moveInputDeadzone *
                moveInputDeadzone;

            SetFloatIfPresent(
                speedFloatParameter,
                normalizedSpeed);

            SetBoolIfPresent(
                groundedBoolParameter,
                grounded);

            SetFloatIfPresent(
                verticalSpeedFloatParameter,
                motor.VerticalSpeed);

            SetBoolIfPresent(
                hasMoveInputBoolParameter,
                hasMoveInput);

            UpdateLocomotionDirection(
                dt);

            UpdateGroundedOneShots(
                moveInput,
                hasMoveInput,
                grounded);

            _hadMoveInput =
                hasMoveInput;

            UpdateSlingImpactLayerRelease();
            UpdateSlingLayerWeight();
        }

        private void UpdateLocomotionDirection(
            float dt)
        {
            if (animator == null ||
                motor == null)
            {
                return;
            }

            Vector3 forward =
                animator.transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;

            forward.Normalize();

            Vector3 planarVelocity =
                motor.PlanarVelocity;

            planarVelocity.y = 0f;

            float directionBlend = 0f;

            if (planarVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 travel =
                    planarVelocity.normalized;

                float movementAngle =
                    Vector3.SignedAngle(
                        forward,
                        travel,
                        Vector3.up);

                movementAngle =
                    Mathf.Clamp(
                        movementAngle,
                        -90f,
                        90f);

                directionBlend =
                    movementAngle /
                    90f;
            }

            if (_hasLastAnimatorForward)
            {
                Vector3 lastForward =
                    _lastAnimatorForward;

                lastForward.y = 0f;

                if (lastForward.sqrMagnitude > 0.0001f)
                {
                    lastForward.Normalize();

                    float yawDelta =
                        Vector3.SignedAngle(
                            lastForward,
                            forward,
                            Vector3.up);

                    float turnRate =
                        yawDelta /
                        Mathf.Max(
                            0.0001f,
                            dt);

                    float turn01 =
                        Mathf.Clamp(
                            turnRate /
                            Mathf.Max(
                                30f,
                                turnRateForFullLean),
                            -1f,
                            1f);

                    directionBlend +=
                        turn01 *
                        turnLeanBlendExtension;
                }
            }

            directionBlend =
                Mathf.Clamp(
                    directionBlend,
                    -1f - turnLeanBlendExtension,
                    1f + turnLeanBlendExtension);

            SetFloatIfPresent(
                locomotionDirectionFloatParameter,
                directionBlend);

            _lastAnimatorForward =
                forward;

            _hasLastAnimatorForward =
                true;
        }

        private void UpdateGroundedOneShots(
            Vector2 moveInput,
            bool hasMoveInput,
            bool grounded)
        {
            if (!grounded ||
                IsSlingTakingControl())
            {
                return;
            }

            Vector3 desiredDirection =
                BuildDesiredMoveDirection(
                    moveInput);

            if (hasMoveInput &&
                !_hadMoveInput &&
                motor.HorizontalSpeed <=
                startMaximumSpeed &&
                desiredDirection.sqrMagnitude >
                0.001f)
            {
                int startDirection =
                    EvaluateStartDirection(
                        desiredDirection);

                SetIntIfPresent(
                    runStartDirectionIntParameter,
                    startDirection);

                TriggerIfPresent(
                    runStartTriggerParameter);
            }

            if (!hasMoveInput &&
                _hadMoveInput &&
                motor.HorizontalSpeed >=
                stopMinimumSpeed)
            {
                TriggerIfPresent(
                    runStopTriggerParameter);
            }

            if (!hasMoveInput ||
                desiredDirection.sqrMagnitude <
                0.001f ||
                motor.HorizontalSpeed <
                hardTurnMinimumSpeed ||
                _hardTurnCooldownRemaining > 0f)
            {
                return;
            }

            Vector3 travel =
                motor.PlanarVelocity;

            travel.y = 0f;

            if (travel.sqrMagnitude < 0.01f)
                return;

            float turnAngle =
                Vector3.SignedAngle(
                    travel.normalized,
                    desiredDirection.normalized,
                    Vector3.up);

            if (Mathf.Abs(turnAngle) <
                hardTurnAngle)
            {
                return;
            }

            int turnDirection =
                turnAngle < 0f
                    ? 0
                    : 1;

            SetIntIfPresent(
                hardTurnDirectionIntParameter,
                turnDirection);

            TriggerIfPresent(
                hardTurnTriggerParameter);

            _hardTurnCooldownRemaining =
                hardTurnCooldown;
        }

        private Vector3 BuildDesiredMoveDirection(
            Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude <
                moveInputDeadzone *
                moveInputDeadzone)
            {
                return Vector3.zero;
            }

            Transform reference =
                movementCamera != null
                    ? movementCamera.transform
                    : animator != null
                        ? animator.transform
                        : transform;

            Vector3 forward =
                reference.forward;

            Vector3 right =
                reference.right;

            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;

            if (right.sqrMagnitude < 0.0001f)
                right = transform.right;

            forward.Normalize();
            right.Normalize();

            Vector3 desired =
                right *
                moveInput.x +
                forward *
                moveInput.y;

            desired.y = 0f;

            if (desired.sqrMagnitude > 1f)
                desired.Normalize();

            return desired;
        }

        private int EvaluateStartDirection(
            Vector3 desiredDirection)
        {
            Vector3 forward =
                animator != null
                    ? animator.transform.forward
                    : transform.forward;

            forward.y = 0f;
            desiredDirection.y = 0f;

            if (forward.sqrMagnitude < 0.0001f ||
                desiredDirection.sqrMagnitude < 0.0001f)
            {
                return 0;
            }

            float angle =
                Vector3.SignedAngle(
                    forward.normalized,
                    desiredDirection.normalized,
                    Vector3.up);

            float abs =
                Mathf.Abs(
                    angle);

            if (abs <=
                forwardStartHalfAngle)
            {
                return 0;
            }

            if (abs >=
                backwardStartAngle)
            {
                return
                    angle < 0f
                        ? 3
                        : 4;
            }

            return
                angle < 0f
                    ? 1
                    : 2;
        }

        private bool IsSlingTakingControl()
        {
            return
                slingController != null &&
                slingController.IsSlinging;
        }

        private void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (input == null)
                input = GetComponent<PlayerInputReader>();

            if (movementVisuals == null)
            {
                movementVisuals =
                    GetComponent<PlayerMovementVisuals>();
            }

            if (slingController == null)
            {
                slingController =
                    GetComponent<AirSlingController>();
            }

            if (movementCamera == null)
                movementCamera = Camera.main;

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

            if (animator != null)
                animator.applyRootMotion = false;
        }

        private void CaptureAnimatorForward()
        {
            if (animator == null)
                return;

            _lastAnimatorForward =
                animator.transform.forward;

            _lastAnimatorForward.y = 0f;

            _hasLastAnimatorForward =
                _lastAnimatorForward.sqrMagnitude >
                0.0001f;
        }

        private void RefreshAnimatorCache()
        {
            _floatParameters.Clear();
            _boolParameters.Clear();
            _intParameters.Clear();
            _triggerParameters.Clear();

            _slingLayerIndex = -1;

            if (animator == null)
                return;

            AnimatorControllerParameter[] parameters =
                animator.parameters;

            for (int i = 0;
                 i < parameters.Length;
                 i++)
            {
                AnimatorControllerParameter parameter =
                    parameters[i];

                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        _floatParameters.Add(
                            parameter.nameHash);
                        break;

                    case AnimatorControllerParameterType.Bool:
                        _boolParameters.Add(
                            parameter.nameHash);
                        break;

                    case AnimatorControllerParameterType.Int:
                        _intParameters.Add(
                            parameter.nameHash);
                        break;

                    case AnimatorControllerParameterType.Trigger:
                        _triggerParameters.Add(
                            parameter.nameHash);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    slingLayerName))
            {
                _slingLayerIndex =
                    animator.GetLayerIndex(
                        slingLayerName);
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            if (movementVisuals != null)
            {
                movementVisuals.JumpStarted +=
                    OnJumpStarted;

                movementVisuals.Landed +=
                    OnLanded;

                movementVisuals.FlipStarted +=
                    OnFlipStarted;
            }

            if (slingController != null)
            {
                slingController.SlingCommitted +=
                    OnSlingCommitted;

                slingController.NodeDashStarted +=
                    OnNodeDashStarted;

                slingController.NodeImpact +=
                    OnNodeImpact;

                slingController.SlingLaunched +=
                    OnSlingLaunched;

                slingController.SlingCanceled +=
                    OnSlingCanceled;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (movementVisuals != null)
            {
                movementVisuals.JumpStarted -=
                    OnJumpStarted;

                movementVisuals.Landed -=
                    OnLanded;

                movementVisuals.FlipStarted -=
                    OnFlipStarted;
            }

            if (slingController != null)
            {
                slingController.SlingCommitted -=
                    OnSlingCommitted;

                slingController.NodeDashStarted -=
                    OnNodeDashStarted;

                slingController.NodeImpact -=
                    OnNodeImpact;

                slingController.SlingLaunched -=
                    OnSlingLaunched;

                slingController.SlingCanceled -=
                    OnSlingCanceled;
            }

            _subscribed = false;
        }

        private void OnJumpStarted()
        {
            TriggerIfPresent(
                jumpTriggerParameter);
        }

        private void OnLanded(
            float intensity)
        {
            TriggerIfPresent(
                landTriggerParameter);
        }

        private void OnFlipStarted(
            int variant,
            float intensity)
        {
            int count =
                Mathf.Max(
                    1,
                    doubleJumpVariantCount);

            int selected =
                UnityEngine.Random.Range(
                    0,
                    count);

            if (count > 1 &&
                selected ==
                _lastDoubleJumpVariant)
            {
                selected =
                    (selected +
                     UnityEngine.Random.Range(
                         1,
                         count)) %
                    count;
            }

            _lastDoubleJumpVariant =
                selected;

            bool mirrored =
                UnityEngine.Random.value *
                100f <
                doubleJumpMirrorChancePercent;

            SetIntIfPresent(
                doubleJumpVariantIntParameter,
                selected);

            SetBoolIfPresent(
                doubleJumpMirrorBoolParameter,
                mirrored);

            TriggerIfPresent(
                doubleJumpTriggerParameter);
        }

        private void OnSlingCommitted(
            AirSlingNode node,
            float quality,
            bool perfect)
        {
            _waitingForSlingImpactLayerRelease = false;
            _slingImpactStateObserved = false;
            _slingImpactLayerHoldElapsed = 0f;

            SetBoolIfPresent(
                slingAirBoolParameter,
                false);

            TriggerIfPresent(
                slingStartTriggerParameter);

            _slingLayerTargetWeight = 1f;
        }

        private void OnNodeDashStarted(
            AirSlingNode node)
        {
            SetBoolIfPresent(
                slingAirBoolParameter,
                true);

            _slingLayerTargetWeight = 1f;
        }

        private void OnNodeImpact(
            AirSlingNode node)
        {
            SetBoolIfPresent(
                slingAirBoolParameter,
                false);

            TriggerIfPresent(
                slingImpactTriggerParameter);

            _waitingForSlingImpactLayerRelease = true;
            _slingImpactStateObserved = false;
            _slingImpactLayerHoldElapsed = 0f;

            _slingLayerTargetWeight = 1f;
        }

        private void OnSlingLaunched(
            AirSlingNode node,
            float quality,
            bool perfect)
        {
            SetBoolIfPresent(
                slingAirBoolParameter,
                false);

            if (!_waitingForSlingImpactLayerRelease)
            {
                _slingLayerTargetWeight = 0f;
            }
        }

        private void OnSlingCanceled()
        {
            SetBoolIfPresent(
                slingAirBoolParameter,
                false);

            _waitingForSlingImpactLayerRelease = false;
            _slingImpactStateObserved = false;
            _slingImpactLayerHoldElapsed = 0f;

            _slingLayerTargetWeight = 0f;
        }

        private void UpdateSlingImpactLayerRelease()
        {
            if (!_waitingForSlingImpactLayerRelease ||
                animator == null ||
                _slingLayerIndex < 0)
            {
                return;
            }

            _slingImpactLayerHoldElapsed +=
                Time.unscaledDeltaTime;

            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(
                    _slingLayerIndex);

            bool inTransition =
                animator.IsInTransition(
                    _slingLayerIndex);

            AnimatorStateInfo next =
                inTransition
                    ? animator.GetNextAnimatorStateInfo(
                        _slingLayerIndex)
                    : default;

            bool currentIsImpact =
                current.shortNameHash ==
                SlingImpactStateHash;

            bool nextIsImpact =
                inTransition &&
                next.shortNameHash ==
                SlingImpactStateHash;

            bool currentIsDoubleJump =
                current.IsTag(
                    "SlingDoubleJump");

            bool nextIsDoubleJump =
                inTransition &&
                next.IsTag(
                    "SlingDoubleJump");

            if (currentIsImpact ||
                nextIsImpact)
            {
                _slingImpactStateObserved = true;
            }

            bool stillShowingAuthoredSequence =
                currentIsImpact ||
                nextIsImpact ||
                currentIsDoubleJump ||
                nextIsDoubleJump;

            bool impactFinished =
                _slingImpactStateObserved &&
                !stillShowingAuthoredSequence;

            bool failSafeExpired =
                _slingImpactLayerHoldElapsed >=
                Mathf.Max(
                    0.2f,
                    slingImpactLayerFailSafeDuration);

            if (!impactFinished &&
                !failSafeExpired)
            {
                _slingLayerTargetWeight = 1f;
                return;
            }

            _waitingForSlingImpactLayerRelease = false;
            _slingImpactStateObserved = false;
            _slingImpactLayerHoldElapsed = 0f;

            _slingLayerTargetWeight = 0f;
        }

        private void UpdateSlingLayerWeight()
        {
            if (!manageSlingLayerWeight ||
                animator == null ||
                _slingLayerIndex < 0)
            {
                return;
            }

            float current =
                animator.GetLayerWeight(
                    _slingLayerIndex);

            float response =
                _slingLayerTargetWeight >
                current
                    ? slingLayerBlendInResponse
                    : slingLayerBlendOutResponse;

            float t =
                1f -
                Mathf.Exp(
                    -Mathf.Max(
                        0.01f,
                        response) *
                    Time.unscaledDeltaTime);

            animator.SetLayerWeight(
                _slingLayerIndex,
                Mathf.Lerp(
                    current,
                    _slingLayerTargetWeight,
                    t));
        }

        private void SetFloatIfPresent(
            string parameterName,
            float value)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_floatParameters.Contains(hash))
            {
                animator.SetFloat(
                    hash,
                    value);
            }
        }

        private void SetBoolIfPresent(
            string parameterName,
            bool value)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_boolParameters.Contains(hash))
            {
                animator.SetBool(
                    hash,
                    value);
            }
        }

        private void SetIntIfPresent(
            string parameterName,
            int value)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_intParameters.Contains(hash))
            {
                animator.SetInteger(
                    hash,
                    value);
            }
        }

        private void TriggerIfPresent(
            string parameterName)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_triggerParameters.Contains(hash))
            {
                animator.SetTrigger(
                    hash);
            }
        }

        private void OnValidate()
        {
            speedForFullRun =
                Mathf.Max(
                    0.1f,
                    speedForFullRun);

            maximumNormalizedSpeed =
                Mathf.Max(
                    1f,
                    maximumNormalizedSpeed);

            moveInputDeadzone =
                Mathf.Clamp(
                    moveInputDeadzone,
                    0.01f,
                    0.75f);

            startMaximumSpeed =
                Mathf.Max(
                    0f,
                    startMaximumSpeed);

            stopMinimumSpeed =
                Mathf.Max(
                    0f,
                    stopMinimumSpeed);

            hardTurnAngle =
                Mathf.Clamp(
                    hardTurnAngle,
                    60f,
                    175f);

            hardTurnMinimumSpeed =
                Mathf.Max(
                    0f,
                    hardTurnMinimumSpeed);

            hardTurnCooldown =
                Mathf.Max(
                    0.05f,
                    hardTurnCooldown);

            turnRateForFullLean =
                Mathf.Max(
                    30f,
                    turnRateForFullLean);

            doubleJumpVariantCount =
                Mathf.Max(
                    1,
                    doubleJumpVariantCount);

            doubleJumpMirrorChancePercent =
                Mathf.Clamp(
                    doubleJumpMirrorChancePercent,
                    0f,
                    100f);

            slingLayerBlendInResponse =
                Mathf.Max(
                    0.01f,
                    slingLayerBlendInResponse);

            slingLayerBlendOutResponse =
                Mathf.Max(
                    0.01f,
                    slingLayerBlendOutResponse);

            slingImpactLayerFailSafeDuration =
                Mathf.Max(
                    0.2f,
                    slingImpactLayerFailSafeDuration);
        }
    }
}
