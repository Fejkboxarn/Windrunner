using System;
using PortableAnimation;
using UnityEngine;
using Windrunner.Production.Animation;
using Windrunner.Production.Feedback;

namespace AirflowPrototype
{
    /// <summary>
    /// Scene references and Unity lifecycle only. WindrunnerAnimationSession
    /// owns animation graph, semantic action routing and rollback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(11000)]
    public sealed class AirflowPortableAnimationBridge : MonoBehaviour
    {
        [SerializeField] private WindrunnerAnimationProfile profile;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AirSlingController sling;
        [SerializeField] private Animator humanoidAnimator;
        [SerializeField] private Camera referenceCamera;
        [SerializeField] private Transform optionalWorldLookTarget;
        [SerializeField] private PlayerHumanoidAnimatorDriver legacyHumanoid;
        [SerializeField] private PlayerCustomAnimationDriver legacyCustom;
        [SerializeField] private PlayerRunningArmPoseController legacyArms;
        [SerializeField] private PlayerProceduralKnifeFlick legacyKnife;
        [SerializeField] private PlayerDualThrowPose legacyThrow;
        [SerializeField] private PlayerGroundedFootIK legacyFootIK;

        private WindrunnerAnimationSession session;
        private IFeedbackService connectedFeedback;
        private bool hadGroundedSample;
        private bool lastGrounded;
        private Vector3 previousForward;
        private string mostRecentMarker = "None";
        private int markerCount;

        public WindrunnerAnimationProfile Profile => profile;
        public Animator HumanoidAnimator => humanoidAnimator;
        public bool PortableActive => session != null && session.PortableActive;
        public string LastError => session != null ? session.LastError : "Not initialized";

        public void Configure(WindrunnerAnimationProfile newProfile, PlayerMotor newMotor,
            AirSlingController newSling, Animator newAnimator, Camera newCamera,
            PlayerHumanoidAnimatorDriver newLegacyHumanoid)
        {
            profile = newProfile;
            motor = newMotor;
            sling = newSling;
            humanoidAnimator = newAnimator;
            referenceCamera = newCamera;
            legacyHumanoid = newLegacyHumanoid;
            if (motor == null) return;
            legacyCustom = motor.GetComponent<PlayerCustomAnimationDriver>();
            legacyArms = motor.GetComponent<PlayerRunningArmPoseController>();
            legacyKnife = motor.GetComponent<PlayerProceduralKnifeFlick>();
            legacyThrow = motor.GetComponent<PlayerDualThrowPose>();
            legacyFootIK = motor.GetComponentInChildren<PlayerGroundedFootIK>(true);
        }

        private void Start()
        {
            if (profile == null || motor == null || humanoidAnimator == null)
            {
                Debug.LogWarning("[Windrunner Animation] Incomplete observation bridge setup. " +
                                 "Run the Batch 4 installer in Edit Mode.", this);
                return;
            }
            CreateSession();
            TryBindFeedback();
            if (profile.automaticallyStartPortableInPlayMode)
                TryStartPortable();
        }

        private void OnEnable()
        {
            // Disabling this component disposes the graph and its subscriptions.
            // Re-enabling it in Play Mode must resume observation even though
            // Unity does not call Start() a second time.
            if (!Application.isPlaying || profile == null || motor == null ||
                humanoidAnimator == null || session != null)
                return;
            CreateSession();
            TryBindFeedback();
        }

        private void CreateSession()
        {
            if (session != null) return;
            session = new WindrunnerAnimationSession(humanoidAnimator, profile,
                new Behaviour[]
                {
                    legacyHumanoid, legacyCustom, legacyArms, legacyKnife,
                    legacyThrow, legacyFootIK
                });
            session.MontageSignalRaised += OnMontageSignal;
            hadGroundedSample = false;
            previousForward = humanoidAnimator != null
                ? humanoidAnimator.transform.forward : transform.forward;
        }

        private void TryBindFeedback()
        {
            if (session == null) return;
            WindrunnerFeedbackHost host = WindrunnerFeedbackHost.Active;
            IFeedbackService service = host != null ? host.Service : null;
            if (ReferenceEquals(connectedFeedback, service)) return;
            connectedFeedback = service;
            session.BindFeedback(service);
        }

        public bool TryStartPortable()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Windrunner Animation] Start Portable is Play Mode only.", this);
                return false;
            }
            PlayerMovementVisuals visuals =
                motor != null ? motor.GetComponent<PlayerMovementVisuals>() : null;
            if (visuals != null && visuals.LegacyProceduralAnimationEnabled)
            {
                Debug.LogWarning("[Windrunner Animation] Kept legacy Animator: " +
                                 "PlayerMovementVisuals still has legacy procedural animation " +
                                 "enabled. For an isolated live graph comparison, first turn on " +
                                 "'Disable All Legacy Procedural Animation' in that component. " +
                                 "Record its previous value so it can be restored.", this);
                return false;
            }
            if (session == null) CreateSession();
            bool started = session != null && session.TryStartPortable();
            Debug.Log(started ?
                "[Windrunner Animation] Portable graph now owns the Humanoid Animator. " +
                "Gameplay movement remains under PlayerMotor." :
                "[Windrunner Animation] Kept legacy Animator: " + LastError, this);
            return started;
        }

        public void StopPortable()
        {
            if (session == null) return;
            session.StopPortable();
            Debug.Log("[Windrunner Animation] Legacy Animator and pose scripts restored.", this);
        }

        private void LateUpdate()
        {
            if (session == null || motor == null) return;
            TryBindFeedback();
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
            if (dt <= 0f) return;
            bool grounded = motor.IsGrounded;
            bool jumpEdge = hadGroundedSample && lastGrounded &&
                            !grounded && motor.VerticalSpeed > 0.1f;
            hadGroundedSample = true;
            lastGrounded = grounded;

            Transform basis = humanoidAnimator != null ? humanoidAnimator.transform : transform;
            Vector3 motion = motor.PlanarVelocity;
            Vector3 local = basis.InverseTransformDirection(motion);
            local.y = 0f;
            Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
            float turnRate = 0f;
            if (forward.sqrMagnitude > 0.001f && previousForward.sqrMagnitude > 0.001f)
                turnRate = Vector3.SignedAngle(previousForward, forward, Vector3.up) /
                           Mathf.Max(0.001f, dt);
            if (forward.sqrMagnitude > 0.001f) previousForward = forward;

            Vector3 point = optionalWorldLookTarget != null
                ? optionalWorldLookTarget.position
                : referenceCamera != null
                    ? referenceCamera.transform.position +
                      referenceCamera.transform.forward * 25f
                    : basis.position + basis.forward * 25f;
            Vector3 look = basis.InverseTransformDirection(point - basis.position);
            float aimYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            float aimPitch = Mathf.Atan2(look.y,
                new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;

            var frame = new WindrunnerMotionFrame(Time.frameCount, dt, local,
                motor.HorizontalSpeed, motor.VerticalSpeed, turnRate,
                aimYaw, aimPitch, grounded, jumpEdge,
                sling != null && sling.IsSlinging);
            session.Evaluate(in frame);
        }

        private void OnMontageSignal(MontageSignal signal)
        {
            if (signal.Kind != MontageSignalKind.Marker) return;
            markerCount++;
            mostRecentMarker = signal.MarkerId;
            // Never invoke AirCaster/AirSling gameplay from animation-only
            // preview markers; gameplay timing migration is a separate step.
        }

        [ContextMenu("Report Animation Bridge Counters")]
        public void ReportCounters()
        {
            if (session == null)
            {
                Debug.Log("[Windrunner Animation] Session not active. Enter Play Mode.", this);
                return;
            }
            Debug.Log("[Windrunner Animation] portable=" + session.PortableActive +
                      ", frame samples=" + session.FrameSamples +
                      ", graph frames=" + session.GraphFrames +
                      ", semantic events=" + session.ObservedEvents +
                      ", mapped events=" + session.MappedEvents +
                      ", started actions=" + session.StartedActions +
                      ", blocked actions=" + session.BlockedActions +
                      ", last event=" + session.LastObservedEvent +
                      ", context=" + session.LastContext +
                      ", montage=" + session.ActiveMontage +
                      ", markers=" + markerCount +
                      ", last marker=" + mostRecentMarker +
                      ", last error=" + session.LastError, this);
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.MontageSignalRaised -= OnMontageSignal;
            session.Dispose();
            session = null;
            connectedFeedback = null;
        }

        private void OnDestroy() => OnDisable();
    }
}
