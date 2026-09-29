using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerHumanoidAnimatorDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerMovementVisuals movementVisuals;
        [SerializeField] private AirSlingController slingController;
        [SerializeField] private Animator animator;

        [Header("Base Locomotion Parameters")]
        [SerializeField] private string speedFloatParameter = "Speed";
        [SerializeField] private string groundedBoolParameter = "Grounded";
        [SerializeField] private string verticalSpeedFloatParameter = "VerticalSpeed";
        [SerializeField] private string jumpTriggerParameter = "Jump";
        [SerializeField] private string doubleJumpTriggerParameter = "DoubleJump";
        [SerializeField] private string doubleJumpVariantIntParameter = "DoubleJumpVariant";
        [SerializeField] private string landTriggerParameter = "Land";

        [Tooltip(
            "Player horizontal speed that writes 1.0 into the Speed parameter.")]
        [Min(0.1f)]
        [SerializeField] private float speedForFullRun = 8.5f;

        [Tooltip(
            "Allows values above 1 so a controller can optionally react to dash speed.")]
        [Min(1f)]
        [SerializeField] private float maximumNormalizedSpeed = 1.35f;

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
        [Tooltip(
            "Safety timeout only. Normally the Sling layer remains fully weighted " +
            "until the generated Sling Impact state actually exits.")]
        [Min(0.2f)]
        [SerializeField] private float slingImpactLayerFailSafeDuration = 1.5f;

        private static readonly int SlingImpactStateHash =
            Animator.StringToHash(
                "Sling Impact");

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

        [SerializeField, HideInInspector]
        private int doubleJumpVariantCount = 1;

        private int _lastDoubleJumpVariant = -1;

        private bool _waitingForSlingImpactLayerRelease;
        private bool _slingImpactStateObserved;
        private float _slingImpactLayerHoldElapsed;

        private bool _subscribed;

        public Animator Animator =>
            animator;

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

                int hash =
                    Animator.StringToHash(
                        doubleJumpTriggerParameter);

                return
                    _triggerParameters.Contains(
                        hash);
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

            RefreshAnimatorCache();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshAnimatorCache();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshAnimatorCache();
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

            float normalizedSpeed =
                Mathf.Clamp(
                    motor.HorizontalSpeed /
                    Mathf.Max(
                        0.1f,
                        speedForFullRun),
                    0f,
                    maximumNormalizedSpeed);

            SetFloatIfPresent(
                speedFloatParameter,
                normalizedSpeed);

            SetBoolIfPresent(
                groundedBoolParameter,
                motor.IsGrounded);

            SetFloatIfPresent(
                verticalSpeedFloatParameter,
                motor.VerticalSpeed);

            UpdateSlingImpactLayerRelease();
            UpdateSlingLayerWeight();
        }

        private void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

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

            SetIntIfPresent(
                doubleJumpVariantIntParameter,
                selected);

            // Let the generated Animator Controller resolve the chosen
            // variant through its zero-duration Any State transitions.
            // This avoids brittle generated state-path lookups while keeping
            // the animation synchronized to the traversal reward event.
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

            // Keep full layer ownership while the authored impact clip plays.
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

            // The character may already be physically launching away, but keep
            // the authored impact pose visible until the Animator exits it.
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

            float next =
                Mathf.Lerp(
                    current,
                    _slingLayerTargetWeight,
                    t);

            animator.SetLayerWeight(
                _slingLayerIndex,
                next);
        }

        private void SetFloatIfPresent(
            string parameterName,
            float value)
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_floatParameters.Contains(
                    hash))
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
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_boolParameters.Contains(
                    hash))
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
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_intParameters.Contains(
                    hash))
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
                string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return;
            }

            int hash =
                Animator.StringToHash(
                    parameterName);

            if (_triggerParameters.Contains(
                    hash))
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

            doubleJumpVariantCount =
                Mathf.Max(
                    1,
                    doubleJumpVariantCount);
        }
    }
}
