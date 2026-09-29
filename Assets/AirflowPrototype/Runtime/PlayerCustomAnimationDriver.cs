using System.Collections.Generic;
using UnityEngine;

namespace AirflowPrototype
{
    /// <summary>
    /// Optional custom-animation layer.
    ///
    /// Put clips in PlayerAnimationProfile. Missing clips intentionally fall
    /// through to PlayerMovementVisuals' procedural animation for that state.
    /// </summary>
    public sealed class PlayerCustomAnimationDriver : MonoBehaviour
    {
        private const string FallbackState = "Fallback";
        private const string IdleState = "Idle";
        private const string LocomotionState = "Locomotion";
        private const string JumpState = "Jump";
        private const string FallState = "Fall";
        private const string LandState = "Land";
        private const string FlipAState = "Flip A";
        private const string FlipBState = "Flip B";
        private const string FlipCState = "Flip C";

        private static readonly int LocomotionRateHash =
            Animator.StringToHash("LocomotionRate");

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerMovementVisuals movementVisuals;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerAnimationProfile profile;

        private AnimatorOverrideController _overrideController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _overrides =
            new List<KeyValuePair<AnimationClip, AnimationClip>>();

        private int _currentStateHash;
        private float _landingLockRemaining;
        private float _flipLockRemaining;

        public PlayerAnimationProfile Profile => profile;

        public bool HasCustomIdle =>
            profile != null &&
            profile.HasIdle;

        public bool HasCustomLocomotion =>
            profile != null &&
            profile.HasLocomotion;

        public bool HasCustomJump =>
            profile != null &&
            profile.HasJump;

        public bool HasCustomFall =>
            profile != null &&
            profile.HasFall;

        public bool HasCustomLand =>
            profile != null &&
            profile.HasLand;

        public bool HasCustomFlip(int variant)
        {
            return
                profile != null &&
                profile.HasFlip(variant);
        }

        public void Configure(
            PlayerMotor newMotor,
            PlayerMovementVisuals newMovementVisuals,
            Animator newAnimator,
            PlayerAnimationProfile newProfile)
        {
            Unsubscribe();

            motor = newMotor;
            movementVisuals = newMovementVisuals;
            animator = newAnimator;
            profile = newProfile;

            BuildOverrideController();

            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();

            if (movementVisuals == null)
                movementVisuals = GetComponent<PlayerMovementVisuals>();

            BuildOverrideController();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (movementVisuals == null)
                return;

            movementVisuals.JumpStarted -= OnJumpStarted;
            movementVisuals.JumpStarted += OnJumpStarted;

            movementVisuals.Landed -= OnLanded;
            movementVisuals.Landed += OnLanded;

            movementVisuals.FlipStarted -= OnFlipStarted;
            movementVisuals.FlipStarted += OnFlipStarted;
        }

        private void Unsubscribe()
        {
            if (movementVisuals == null)
                return;

            movementVisuals.JumpStarted -= OnJumpStarted;
            movementVisuals.Landed -= OnLanded;
            movementVisuals.FlipStarted -= OnFlipStarted;
        }

        private void BuildOverrideController()
        {
            if (animator == null ||
                profile == null ||
                profile.TemplateController == null)
            {
                return;
            }

            _overrideController =
                new AnimatorOverrideController(
                    profile.TemplateController);

            _overrides.Clear();
            _overrideController.GetOverrides(
                _overrides);

            ReplaceOverride(
                PlayerAnimationProfile.IdlePlaceholderName,
                profile.idle);

            ReplaceOverride(
                PlayerAnimationProfile.LocomotionPlaceholderName,
                profile.locomotion);

            ReplaceOverride(
                PlayerAnimationProfile.JumpPlaceholderName,
                profile.jump);

            ReplaceOverride(
                PlayerAnimationProfile.FallPlaceholderName,
                profile.fall);

            ReplaceOverride(
                PlayerAnimationProfile.LandPlaceholderName,
                profile.land);

            ReplaceOverride(
                PlayerAnimationProfile.FlipAPlaceholderName,
                profile.flipA);

            ReplaceOverride(
                PlayerAnimationProfile.FlipBPlaceholderName,
                profile.flipB);

            ReplaceOverride(
                PlayerAnimationProfile.FlipCPlaceholderName,
                profile.flipC);

            _overrideController.ApplyOverrides(
                _overrides);

            animator.runtimeAnimatorController =
                _overrideController;

            animator.applyRootMotion = false;

            PlayState(
                FallbackState,
                true);
        }

        private void ReplaceOverride(
            string placeholderName,
            AnimationClip replacement)
        {
            if (replacement == null)
                return;

            for (int i = 0;
                 i < _overrides.Count;
                 i++)
            {
                AnimationClip original =
                    _overrides[i].Key;

                if (original != null &&
                    original.name == placeholderName)
                {
                    _overrides[i] =
                        new KeyValuePair<AnimationClip, AnimationClip>(
                            original,
                            replacement);

                    return;
                }
            }
        }

        private void Update()
        {
            if (animator == null ||
                profile == null ||
                motor == null)
            {
                return;
            }

            float dt =
                Time.deltaTime;

            if (_flipLockRemaining > 0f)
            {
                _flipLockRemaining =
                    Mathf.Max(
                        0f,
                        _flipLockRemaining - dt);

                return;
            }

            if (_landingLockRemaining > 0f)
            {
                _landingLockRemaining =
                    Mathf.Max(
                        0f,
                        _landingLockRemaining - dt);

                return;
            }

            if (!motor.IsGrounded)
            {
                if (motor.VerticalSpeed >= -0.05f)
                {
                    PlayState(
                        profile.HasJump
                            ? JumpState
                            : FallbackState);
                }
                else
                {
                    PlayState(
                        profile.HasFall
                            ? FallState
                            : FallbackState);
                }

                return;
            }

            float speed =
                motor.HorizontalSpeed;

            if (profile.HasLocomotion &&
                speed > 0.35f)
            {
                float rate =
                    Mathf.Clamp(
                        speed /
                        Mathf.Max(
                            0.1f,
                            profile.locomotionReferenceSpeed),
                        profile.minimumLocomotionPlayback,
                        profile.maximumLocomotionPlayback);

                animator.SetFloat(
                    LocomotionRateHash,
                    rate);

                PlayState(
                    LocomotionState);

                return;
            }

            PlayState(
                profile.HasIdle
                    ? IdleState
                    : FallbackState);
        }

        private void OnJumpStarted()
        {
            _landingLockRemaining = 0f;

            if (profile != null &&
                profile.HasJump)
            {
                PlayState(
                    JumpState,
                    true);
            }
        }

        private void OnLanded(
            float intensity)
        {
            _flipLockRemaining = 0f;

            if (profile == null ||
                !profile.HasLand)
            {
                return;
            }

            float duration =
                Mathf.Max(
                    0.05f,
                    profile.land.length *
                    profile.landingLockFraction);

            _landingLockRemaining =
                duration;

            PlayState(
                LandState,
                true);
        }

        private void OnFlipStarted(
            int variant,
            float intensity)
        {
            if (profile == null ||
                !profile.HasFlip(variant))
            {
                return;
            }

            AnimationClip clip =
                profile.GetFlip(variant);

            _flipLockRemaining =
                clip != null
                    ? Mathf.Max(
                        0.05f,
                        clip.length)
                    : 0f;

            string stateName =
                variant == 0
                    ? FlipAState
                    : variant == 1
                        ? FlipBState
                        : FlipCState;

            PlayState(
                stateName,
                true);
        }

        private void PlayState(
            string stateName,
            bool force = false)
        {
            if (animator == null)
                return;

            int stateHash =
                Animator.StringToHash(
                    "Base Layer." +
                    stateName);

            if (!force &&
                stateHash == _currentStateHash)
            {
                return;
            }

            if (!animator.HasState(
                    0,
                    stateHash))
            {
                return;
            }

            float fade =
                profile != null
                    ? profile.crossFadeDuration
                    : 0.08f;

            animator.CrossFadeInFixedTime(
                stateHash,
                fade,
                0,
                0f);

            _currentStateHash =
                stateHash;
        }
    }
}
