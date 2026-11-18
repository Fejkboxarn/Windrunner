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

        private WindrunnerAnimationSession session;
        private IFeedbackService connectedFeedback;
        private IFeedbackService boundFeedback;
        private bool hasFeedbackBinding;

        public void BindFeedbackService(IFeedbackService service)
        {
            hasFeedbackBinding = true;
            boundFeedback = service;
            TryBindFeedback();
        }
        private bool hadActionAtEvaluation;
        private bool hadGroundedSample;
        private bool lastGrounded;
        private Vector3 previousForward;
        private string mostRecentMarker = "None";
        private int markerCount;

        public WindrunnerAnimationProfile Profile => profile;
        public string ActiveAnimationAction => session?.ActiveMontage ?? "Inactive";
        public Animator HumanoidAnimator => humanoidAnimator;
        public bool PortableActive => session != null && session.PortableActive;
        public bool ProductionOnlyReady
        {
            get
            {
                if(profile == null || !profile.productionOnly || !PortableActive || humanoidAnimator == null ||
                   humanoidAnimator.runtimeAnimatorController != null || humanoidAnimator.applyRootMotion) return false;
                var visuals=motor != null ? motor.GetComponent<PlayerMovementVisuals>() : null;
                return visuals == null || !visuals.LegacyProceduralAnimationEnabled;
            }
        }
        public bool DebugIsolateLocomotion => session != null && session.DebugIsolateLocomotion;
        [ContextMenu("Development/Toggle locomotion-only pose")]
        public void ToggleLocomotionIsolation()
        {
            if (session != null && (Application.isEditor || Debug.isDebugBuild))
                session.DebugIsolateLocomotion = !session.DebugIsolateLocomotion;
        }

        public string LastError => session != null ? session.LastError : "Not initialized";

        public void Configure(WindrunnerAnimationProfile newProfile, PlayerMotor newMotor,
            AirSlingController newSling, Animator newAnimator, Camera newCamera)
        {
            profile = newProfile;
            motor = newMotor;
            sling = newSling;
            humanoidAnimator = newAnimator;
            referenceCamera = newCamera;

        }

        public bool ReadRecordingPose(out GroundOnlyPipelineRuntime.RecordingPose pose)
        {
            pose = default;
            return session != null && session.ReadRecordingPose(out pose);
        }

        private void Start()
        {
            if (profile == null || motor == null || humanoidAnimator == null)
            {
                Debug.LogWarning("[Windrunner Animation] Missing character profile, motor or Humanoid Animator reference. " +
                                 "Validate the production character composition in Character Tuning.", this);
                return;
            }
            if (Debug.isDebugBuild || Application.isEditor)
            {
                if (!TryGetComponent<AnimationRecordingOverlay>(out var recorder))
                    recorder = gameObject.AddComponent<AnimationRecordingOverlay>();
                recorder.Configure(this, motor);
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
            if (profile.automaticallyStartPortableInPlayMode) TryStartPortable();
        }

        private void CreateSession()
        {
            if (session != null) return;
            session = new WindrunnerAnimationSession(humanoidAnimator, profile,
                Array.Empty<Behaviour>(), motor != null ? motor.transform : transform);
            session.MontageSignalRaised += OnMontageSignal;
            hadActionAtEvaluation = false;
            hadGroundedSample = false;
            previousForward = humanoidAnimator != null
                ? humanoidAnimator.transform.forward : transform.forward;
        }

        private void TryBindFeedback()
        {
            if (session == null) return;
            IFeedbackService service = boundFeedback;
            if (!hasFeedbackBinding)
            {
                WindrunnerFeedbackHost host = WindrunnerFeedbackHost.Active;
                service = host != null ? host.Service : null;
            }
            if (ReferenceEquals(connectedFeedback, service)) return;
            connectedFeedback = service;
            session.BindFeedback(service);
            if(PortableActive)RecoverAbilityPresentation();
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
                Debug.LogWarning("[Windrunner Animation] Portable start rejected: " +
                                 "PlayerMovementVisuals still has legacy procedural animation " +
                                 "enabled. For an isolated live graph comparison, first turn on " +
                                 "'Disable All Legacy Procedural Animation' in that component. " +
                                 "Record its previous value so it can be restored.", this);
                return false;
            }
            if (session == null) CreateSession();
            bool wasActive=PortableActive;
            bool started = session != null && session.TryStartPortable();
            if(started && !wasActive)RecoverAbilityPresentation();
            Debug.Log(started ?
                "[Windrunner Animation] Portable graph now owns the Humanoid Animator. " +
                "Gameplay movement remains under PlayerMotor." :
                "[Windrunner Animation] Portable start rejected: " + LastError, this);
            return started;
        }

        private void RecoverAbilityPresentation(bool includeImpact = true)
        {
            FeedbackKey phase=FeedbackKey.None;
            if(sling!=null)
                switch(sling.PresentationPhase)
                {
                    case AirSlingPhase.Charging:
                    case AirSlingPhase.DelayBeforeDash:phase=FeedbackKey.SlingChargeStarted;break;
                    case AirSlingPhase.DashingToNode:phase=FeedbackKey.SlingDashStarted;break;
                    case AirSlingPhase.ImpactHold when includeImpact:phase=FeedbackKey.SlingImpact;break;
                }
            var caster=motor!=null?motor.GetComponent<AirCaster>():null;
            var dash=motor!=null?motor.GetComponent<AirDash>():null;
            bool casting=caster!=null && caster.IsCharging;
            bool dashing=dash!=null && dash.IsDashing;
            if (includeImpact) session?.RecoverSustainedActions(phase,casting,dashing);
            else session?.ResumeHeldLoops(phase,casting,dashing);
        }

        public void StopPortable()
        {
            if (session == null) return;
            session.StopPortable();
            Debug.Log(profile != null && profile.productionOnly ?
                "[Windrunner Animation] Portable graph stopped; production-only mode keeps legacy writers inactive." :
                "[Windrunner Animation] Legacy Animator and pose scripts restored.", this);
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

            Vector3 resolved = motor.Displacement.Frame == Time.frameCount
                ? motor.Displacement.ResolvedDisplacement / Time.deltaTime : Vector3.zero;
            resolved = basis.InverseTransformDirection(Vector3.ProjectOnPlane(resolved, Vector3.up));
            var frame = new WindrunnerMotionFrame(Time.frameCount, dt, local,
                motor.HorizontalSpeed, motor.VerticalSpeed, turnRate,
                aimYaw, aimPitch, grounded, jumpEdge,
                sling != null && sling.IsSlinging, resolved);
            // Once a higher-priority transient ends, resume any still-held traversal pose.
            // Impact is a one-shot. Only initial graph recovery can reconstruct it.
            if (hadActionAtEvaluation && PortableActive && !session.HasActiveStandaloneAction)
                RecoverAbilityPresentation(false);
            hadActionAtEvaluation = session.HasActiveStandaloneAction;
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
