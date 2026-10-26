using UnityEngine;

namespace AirflowPrototype
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class PlayerProceduralKnifeFlick : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AirCaster caster;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerKnifeFlickPreset preset;

        [Header("Knife Visual")]
        [SerializeField] private GameObject knifeVisualPrefab;
        [SerializeField] private Transform leftKnifeSocket;
        [SerializeField] private Transform rightKnifeSocket;

        [Header("Debug Pose Preview")]
        [Tooltip("In Play Mode, holds one procedural throw phase so it can be tuned while the character stands still.")]
        [SerializeField] private bool debugPreviewPose;

        [SerializeField] private PlayerKnifeFlickPhase debugPreviewPhase =
            PlayerKnifeFlickPhase.Strike;

        [SerializeField] private bool debugPreviewRightHand = true;

        [Range(0f, 1f)]
        [SerializeField] private float debugPreviewWeight = 1f;

        private Transform _chest;

        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;

        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;

        private GameObject _leftHeldKnife;
        private GameObject _rightHeldKnife;

        private AirNode _chargeTarget;
        private AirNode _releaseTarget;
        private Vector3 _fixedReleaseTargetPosition;

        private bool _charging;
        private bool _throwActive;
        private bool _selectedRightHand;
        private bool _selectedHandValid;
        private bool _lastThrowRightHand;
        private bool _hasLastThrowHand;

        private float _chargeWeight;
        private float _releaseStartWeight;
        private float _throwElapsed;

        private bool _heldKnifeShown;
        private bool _releasePending;
        private bool _knifeReleased;

        private bool _subscribed;

        private struct PoseSample
        {
            public Vector3 handDirection;
            public float chestYaw;
            public float forearmSnap;
            public float wristSnap;

            public static PoseSample Lerp(
                PoseSample a,
                PoseSample b,
                float t)
            {
                t = Mathf.Clamp01(t);

                return new PoseSample
                {
                    handDirection =
                        Vector3.Lerp(
                            a.handDirection,
                            b.handDirection,
                            t),

                    chestYaw =
                        Mathf.Lerp(
                            a.chestYaw,
                            b.chestYaw,
                            t),

                    forearmSnap =
                        Mathf.Lerp(
                            a.forearmSnap,
                            b.forearmSnap,
                            t),

                    wristSnap =
                        Mathf.Lerp(
                            a.wristSnap,
                            b.wristSnap,
                            t)
                };
            }
        }

        public bool IsPresentationActive =>
            debugPreviewPose ||
            _charging ||
            _throwActive ||
            _chargeWeight > 0.01f;

        public bool SelectedRightHand =>
            _selectedRightHand;

        public PlayerKnifeFlickPreset Preset =>
            preset;

        public void Configure(
            AirCaster newCaster,
            Animator newAnimator,
            PlayerKnifeFlickPreset newPreset,
            Transform newLeftKnifeSocket = null,
            Transform newRightKnifeSocket = null)
        {
            Unsubscribe();

            caster = newCaster;
            animator = newAnimator;
            preset = newPreset;

            if (newLeftKnifeSocket != null)
                leftKnifeSocket = newLeftKnifeSocket;

            if (newRightKnifeSocket != null)
                rightKnifeSocket = newRightKnifeSocket;

            ResolveReferences();
            ResolveHumanoidBones();
            ResolveSockets();
            RebuildHeldKnifeVisuals();

            if (isActiveAndEnabled)
                Subscribe();
        }

        public void SetPreset(
            PlayerKnifeFlickPreset newPreset)
        {
            preset = newPreset;
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveHumanoidBones();
            ResolveSockets();
            RebuildHeldKnifeVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveHumanoidBones();
            ResolveSockets();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            _charging = false;
            _throwActive = false;
            _selectedHandValid = false;
            _chargeWeight = 0f;
            _throwElapsed = 0f;
            _releasePending = false;
            _knifeReleased = false;

            HideHeldKnives();
        }

        private void Update()
        {
            if (preset == null)
                return;

            float desiredCharge =
                _charging &&
                !_throwActive
                    ? preset.chargeReadyWeight *
                      Mathf.Lerp(
                          0.35f,
                          1f,
                          caster != null
                              ? caster.TimingProgress01
                              : 0f)
                    : 0f;

            float response =
                Mathf.Max(
                    0.01f,
                    preset.chargePoseResponse);

            _chargeWeight =
                Mathf.Lerp(
                    _chargeWeight,
                    desiredCharge,
                    1f -
                    Mathf.Exp(
                        -response *
                        Time.unscaledDeltaTime));

            if (!_throwActive)
                return;

            _throwElapsed +=
                Time.unscaledDeltaTime;

            if (!_heldKnifeShown &&
                _throwElapsed >=
                preset.knifeMaterializeTime)
            {
                ShowSelectedHeldKnife();
                _heldKnifeShown = true;
            }

            if (!_knifeReleased &&
                !_releasePending &&
                _throwElapsed >=
                preset.knifeReleaseTime)
            {
                // LateUpdate applies the throw pose first, then releases from
                // the newly posed hand transform on this same frame.
                _releasePending = true;
            }

            float totalDuration =
                preset.prepDuration +
                preset.strikeDuration +
                preset.followThroughDuration +
                preset.recoveryDuration;

            if (_throwElapsed >=
                totalDuration)
            {
                if (!_knifeReleased)
                    _releasePending = true;

                if (_releasePending)
                    return;

                EndThrow();
            }
        }

        private void LateUpdate()
        {
            if (animator == null ||
                !animator.isHuman ||
                preset == null)
            {
                return;
            }

            if (_rightUpperArm == null ||
                _leftUpperArm == null)
            {
                ResolveHumanoidBones();
            }

            if (debugPreviewPose)
            {
                ApplyPose(
                    GetPhasePose(
                        debugPreviewPhase),
                    debugPreviewWeight,
                    debugPreviewRightHand);

                return;
            }

            if (_throwActive)
            {
                EvaluateThrowPose(
                    out PoseSample pose,
                    out float weight);

                ApplyPose(
                    pose,
                    weight,
                    _selectedRightHand);

                if (_releasePending)
                {
                    ReleaseKnifeVisual();
                }

                return;
            }

            if (_chargeWeight > 0.001f &&
                _selectedHandValid)
            {
                ApplyPose(
                    GetReadyPose(),
                    _chargeWeight,
                    _selectedRightHand);
            }
        }

        private void EvaluateThrowPose(
            out PoseSample pose,
            out float weight)
        {
            PoseSample ready =
                GetReadyPose();

            PoseSample prep =
                GetPrepPose();

            PoseSample strike =
                GetStrikePose();

            PoseSample follow =
                GetFollowPose();

            float prepEnd =
                preset.prepDuration;

            float strikeEnd =
                prepEnd +
                preset.strikeDuration;

            float followEnd =
                strikeEnd +
                preset.followThroughDuration;

            if (_throwElapsed <
                prepEnd)
            {
                float t =
                    Mathf.Clamp01(
                        _throwElapsed /
                        preset.prepDuration);

                float eased =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t);

                pose =
                    PoseSample.Lerp(
                        ready,
                        prep,
                        eased);

                weight =
                    Mathf.Lerp(
                        Mathf.Max(
                            0.18f,
                            _releaseStartWeight),
                        1f,
                        eased);

                return;
            }

            if (_throwElapsed <
                strikeEnd)
            {
                float t =
                    Mathf.Clamp01(
                        (_throwElapsed -
                         prepEnd) /
                        preset.strikeDuration);

                float curved =
                    EvaluateCurve01(
                        preset.strikeCurve,
                        t);

                pose =
                    PoseSample.Lerp(
                        prep,
                        strike,
                        curved);

                weight = 1f;
                return;
            }

            if (_throwElapsed <
                followEnd)
            {
                float t =
                    Mathf.Clamp01(
                        (_throwElapsed -
                         strikeEnd) /
                        preset.followThroughDuration);

                float curved =
                    EvaluateCurve01(
                        preset.followCurve,
                        t);

                pose =
                    PoseSample.Lerp(
                        strike,
                        follow,
                        curved);

                weight = 1f;
                return;
            }

            float recoveryT =
                Mathf.Clamp01(
                    (_throwElapsed -
                     followEnd) /
                    preset.recoveryDuration);

            float recovery =
                EvaluateCurve01(
                    preset.recoveryCurve,
                    recoveryT);

            pose = follow;
            weight = 1f - recovery;
        }

        private void ApplyPose(
            PoseSample pose,
            float weight,
            bool rightHand)
        {
            weight =
                Mathf.Clamp01(
                    weight);

            if (weight <= 0.0001f)
                return;

            float side =
                rightHand
                    ? 1f
                    : -1f;

            Transform poseSpace =
                animator != null
                    ? animator.transform
                    : transform;

            if (_chest != null &&
                preset.chestWeight > 0f)
            {
                float yaw =
                    pose.chestYaw *
                    side *
                    preset.chestWeight *
                    weight;

                _chest.rotation =
                    Quaternion.AngleAxis(
                        yaw,
                        poseSpace.up) *
                    _chest.rotation;
            }

            Transform upperArm =
                rightHand
                    ? _rightUpperArm
                    : _leftUpperArm;

            Transform lowerArm =
                rightHand
                    ? _rightLowerArm
                    : _leftLowerArm;

            Transform hand =
                rightHand
                    ? _rightHand
                    : _leftHand;

            if (upperArm == null ||
                hand == null)
            {
                return;
            }

            Vector3 localDirection =
                pose.handDirection;

            localDirection.x *=
                side;

            if (localDirection.sqrMagnitude <
                0.0001f)
            {
                localDirection =
                    new Vector3(
                        0.35f * side,
                        0f,
                        1f);
            }

            Vector3 desiredDirection =
                poseSpace.TransformDirection(
                    localDirection.normalized);

            Vector3 currentDirection =
                hand.position -
                upperArm.position;

            if (currentDirection.sqrMagnitude >
                0.0001f)
            {
                Quaternion fullDelta =
                    Quaternion.FromToRotation(
                        currentDirection.normalized,
                        desiredDirection);

                float maxCorrection =
                    preset.maximumUpperArmCorrection *
                    preset.armAimWeight *
                    weight;

                Quaternion correction =
                    Quaternion.RotateTowards(
                        Quaternion.identity,
                        fullDelta,
                        maxCorrection);

                upperArm.rotation =
                    correction *
                    upperArm.rotation;
            }

            Vector3 bendAxis =
                EvaluateArmBendAxis(
                    upperArm,
                    lowerArm,
                    hand,
                    poseSpace,
                    side);

            if (lowerArm != null &&
                Mathf.Abs(
                    pose.forearmSnap) >
                0.001f)
            {
                lowerArm.rotation =
                    Quaternion.AngleAxis(
                        pose.forearmSnap *
                        weight,
                        bendAxis) *
                    lowerArm.rotation;
            }

            if (Mathf.Abs(
                    pose.wristSnap) >
                0.001f)
            {
                hand.rotation =
                    Quaternion.AngleAxis(
                        pose.wristSnap *
                        weight,
                        bendAxis) *
                    hand.rotation;
            }
        }

        private static Vector3 EvaluateArmBendAxis(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Transform poseSpace,
            float side)
        {
            if (upperArm != null &&
                lowerArm != null &&
                hand != null)
            {
                Vector3 upperSegment =
                    lowerArm.position -
                    upperArm.position;

                Vector3 lowerSegment =
                    hand.position -
                    lowerArm.position;

                if (upperSegment.sqrMagnitude >
                        0.0001f &&
                    lowerSegment.sqrMagnitude >
                        0.0001f)
                {
                    Vector3 axis =
                        Vector3.Cross(
                            upperSegment.normalized,
                            lowerSegment.normalized);

                    if (axis.sqrMagnitude >
                        0.0001f)
                    {
                        return axis.normalized;
                    }
                }
            }

            if (poseSpace != null)
            {
                return
                    poseSpace.right *
                    Mathf.Sign(
                        Mathf.Abs(side) >
                        0.001f
                            ? side
                            : 1f);
            }

            return Vector3.right;
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
                    Animator candidate =
                        animators[i];

                    if (candidate != null &&
                        candidate.isHuman)
                    {
                        animator = candidate;
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
                    HumanBodyBones.UpperChest);

            if (_chest == null)
            {
                _chest =
                    animator.GetBoneTransform(
                        HumanBodyBones.Chest);
            }

            if (_chest == null)
            {
                _chest =
                    animator.GetBoneTransform(
                        HumanBodyBones.Spine);
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

        private void ResolveSockets()
        {
            if (leftKnifeSocket == null &&
                _leftHand != null)
            {
                leftKnifeSocket =
                    _leftHand.Find(
                        "Left Knife Flick Socket");

                if (leftKnifeSocket == null)
                {
                    leftKnifeSocket =
                        _leftHand.Find(
                            "Left Throw Origin");
                }

                if (leftKnifeSocket == null)
                    leftKnifeSocket = _leftHand;
            }

            if (rightKnifeSocket == null &&
                _rightHand != null)
            {
                rightKnifeSocket =
                    _rightHand.Find(
                        "Right Knife Flick Socket");

                if (rightKnifeSocket == null)
                {
                    rightKnifeSocket =
                        _rightHand.Find(
                            "Right Throw Origin");
                }

                if (rightKnifeSocket == null)
                    rightKnifeSocket = _rightHand;
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
            if (node == null)
                return;

            if (!_throwActive)
                SelectHand();

            _chargeTarget = node;
            _charging = true;
        }

        private void OnTargetedPulseReleased(
            AirNode node,
            float charge01,
            bool ready)
        {
            if (node == null ||
                preset == null)
            {
                return;
            }

            // Gameplay remains responsive even if the previous visual
            // follow-through is still recovering.
            if (_throwActive)
                EndThrow();

            if (!_selectedHandValid)
                SelectHand();

            _releaseTarget = node;
            _fixedReleaseTargetPosition =
                node.TargetPosition;

            _charging = false;
            _chargeTarget = null;

            _throwActive = true;
            _throwElapsed = 0f;
            _releaseStartWeight =
                _chargeWeight;

            _heldKnifeShown = false;
            _releasePending = false;
            _knifeReleased = false;

            HideHeldKnives();

            _lastThrowRightHand =
                _selectedRightHand;

            _hasLastThrowHand = true;
        }

        private void OnChargeCanceled()
        {
            _charging = false;
            _chargeTarget = null;

            if (!_throwActive)
                _selectedHandValid = false;
        }

        private void SelectHand()
        {
            if (preset == null)
            {
                _selectedRightHand = true;
                _selectedHandValid = true;
                return;
            }

            bool selectedRight =
                UnityEngine.Random.value *
                100f <
                preset.rightHandChancePercent;

            if (preset.avoidImmediateHandRepeat &&
                _hasLastThrowHand &&
                selectedRight ==
                _lastThrowRightHand)
            {
                selectedRight =
                    !selectedRight;
            }

            _selectedRightHand =
                selectedRight;

            _selectedHandValid =
                true;
        }

        private void ShowSelectedHeldKnife()
        {
            EnsureHeldKnifeVisuals();

            if (_leftHeldKnife != null)
            {
                _leftHeldKnife.SetActive(
                    !_selectedRightHand);
            }

            if (_rightHeldKnife != null)
            {
                _rightHeldKnife.SetActive(
                    _selectedRightHand);
            }
        }

        private void ReleaseKnifeVisual()
        {
            _releasePending = false;
            _knifeReleased = true;

            Transform socket =
                _selectedRightHand
                    ? rightKnifeSocket
                    : leftKnifeSocket;

            GameObject held =
                _selectedRightHand
                    ? _rightHeldKnife
                    : _leftHeldKnife;

            if (held != null)
                held.SetActive(false);

            if (socket == null)
                return;

            PlayerKnifeFlickProjectileVisual.Spawn(
                socket.position,
                socket.rotation,
                _releaseTarget,
                _fixedReleaseTargetPosition,
                knifeVisualPrefab,
                preset.visualTravelDuration,
                preset.trailWidth,
                preset.trailColor,
                preset.spinDegreesPerSecond);

            float totalDuration =
                preset.prepDuration +
                preset.strikeDuration +
                preset.followThroughDuration +
                preset.recoveryDuration;

            if (_throwElapsed >=
                totalDuration)
            {
                EndThrow();
            }
        }

        private void EndThrow()
        {
            _throwActive = false;
            _selectedHandValid = false;
            _throwElapsed = 0f;
            _releaseStartWeight = 0f;

            _heldKnifeShown = false;
            _releasePending = false;
            _knifeReleased = false;

            _releaseTarget = null;

            HideHeldKnives();
        }

        private void RebuildHeldKnifeVisuals()
        {
            DestroyHeldKnife(
                ref _leftHeldKnife);

            DestroyHeldKnife(
                ref _rightHeldKnife);

            EnsureHeldKnifeVisuals();
        }

        private void EnsureHeldKnifeVisuals()
        {
            if (knifeVisualPrefab == null)
                return;

            if (_leftHeldKnife == null &&
                leftKnifeSocket != null)
            {
                _leftHeldKnife =
                    Instantiate(
                        knifeVisualPrefab,
                        leftKnifeSocket);

                _leftHeldKnife.name =
                    "Left Flick Knife Visual";

                ResetLocalTransform(
                    _leftHeldKnife.transform);

                _leftHeldKnife.SetActive(
                    false);
            }

            if (_rightHeldKnife == null &&
                rightKnifeSocket != null)
            {
                _rightHeldKnife =
                    Instantiate(
                        knifeVisualPrefab,
                        rightKnifeSocket);

                _rightHeldKnife.name =
                    "Right Flick Knife Visual";

                ResetLocalTransform(
                    _rightHeldKnife.transform);

                _rightHeldKnife.SetActive(
                    false);
            }
        }

        private static void ResetLocalTransform(
            Transform target)
        {
            target.localPosition =
                Vector3.zero;

            target.localRotation =
                Quaternion.identity;

            target.localScale =
                Vector3.one;
        }

        private void HideHeldKnives()
        {
            if (_leftHeldKnife != null)
                _leftHeldKnife.SetActive(false);

            if (_rightHeldKnife != null)
                _rightHeldKnife.SetActive(false);
        }

        private void DestroyHeldKnife(
            ref GameObject visual)
        {
            if (visual == null)
                return;

            if (Application.isPlaying)
                Destroy(visual);
            else
                DestroyImmediate(visual);

            visual = null;
        }

        private PoseSample GetPhasePose(
            PlayerKnifeFlickPhase phase)
        {
            switch (phase)
            {
                case PlayerKnifeFlickPhase.Prep:
                    return GetPrepPose();

                case PlayerKnifeFlickPhase.Strike:
                    return GetStrikePose();

                case PlayerKnifeFlickPhase.FollowThrough:
                    return GetFollowPose();

                default:
                    return GetReadyPose();
            }
        }

        private PoseSample GetReadyPose()
        {
            return new PoseSample
            {
                handDirection =
                    preset.readyHandDirection,
                chestYaw =
                    preset.readyChestYaw,
                forearmSnap =
                    preset.readyForearmSnap,
                wristSnap =
                    preset.readyWristSnap
            };
        }

        private PoseSample GetPrepPose()
        {
            return new PoseSample
            {
                handDirection =
                    preset.prepHandDirection,
                chestYaw =
                    preset.prepChestYaw,
                forearmSnap =
                    preset.prepForearmSnap,
                wristSnap =
                    preset.prepWristSnap
            };
        }

        private PoseSample GetStrikePose()
        {
            return new PoseSample
            {
                handDirection =
                    preset.strikeHandDirection,
                chestYaw =
                    preset.strikeChestYaw,
                forearmSnap =
                    preset.strikeForearmSnap,
                wristSnap =
                    preset.strikeWristSnap
            };
        }

        private PoseSample GetFollowPose()
        {
            return new PoseSample
            {
                handDirection =
                    preset.followHandDirection,
                chestYaw =
                    preset.followChestYaw,
                forearmSnap =
                    preset.followForearmSnap,
                wristSnap =
                    preset.followWristSnap
            };
        }

        private static float EvaluateCurve01(
            AnimationCurve curve,
            float t)
        {
            t = Mathf.Clamp01(t);

            if (curve == null ||
                curve.length == 0)
            {
                return t;
            }

            return Mathf.Clamp01(
                curve.Evaluate(t));
        }

        private void OnValidate()
        {
            debugPreviewWeight =
                Mathf.Clamp01(
                    debugPreviewWeight);
        }
    }
}
