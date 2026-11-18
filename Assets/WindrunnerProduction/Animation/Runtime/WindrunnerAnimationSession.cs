using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PortableAnimation;
using UnityEngine;
using Windrunner.Production.Feedback;

namespace Windrunner.Production.Animation
{
    public readonly struct WindrunnerMotionFrame
    {
        public readonly int FrameId;
        public readonly float DeltaTime;
        public readonly Vector3 LocalVelocity;
        public readonly Vector3 ResolvedLocalVelocity;
        public readonly float HorizontalSpeed;
        public readonly float VerticalSpeed;
        public readonly float YawRate;
        public readonly float AimYaw;
        public readonly float AimPitch;
        public readonly bool Grounded;
        public readonly bool JumpStarted;
        public readonly bool Slinging;

        public WindrunnerMotionFrame(int frameId, float deltaTime, Vector3 localVelocity,
            float horizontalSpeed, float verticalSpeed, float yawRate,
            float aimYaw, float aimPitch, bool grounded, bool jumpStarted,
            bool slinging, Vector3? resolvedLocalVelocity = null)
        {
            FrameId = frameId;
            DeltaTime = deltaTime;
            LocalVelocity = localVelocity;
            ResolvedLocalVelocity = resolvedLocalVelocity ?? localVelocity;
            HorizontalSpeed = horizontalSpeed;
            VerticalSpeed = verticalSpeed;
            YawRate = yawRate;
            AimYaw = aimYaw;
            AimPitch = aimPitch;
            Grounded = grounded;
            JumpStarted = jumpStarted;
            Slinging = slinging;
        }
    }

    /// <summary>
    /// Pure C# runtime owner: project motion enters as values, semantic events
    /// enter through IFeedbackService, and the game's movement motor never
    /// receives animation-driven displacement.
    /// </summary>
    public sealed class WindrunnerAnimationSession : IDisposable
    {
        // Weak keys track only portable graph ownership, not gameplay players.
        // Animator.hasBoundPlayables also includes the legacy controller graph.
        private static readonly ConditionalWeakTable<Animator, WindrunnerAnimationSession> Owners =
            new ConditionalWeakTable<Animator, WindrunnerAnimationSession>();
        private readonly struct SuspendedBehaviour
        {
            public readonly Behaviour Component;
            public readonly bool WasEnabled;
            public SuspendedBehaviour(Behaviour component)
            {
                Component = component;
                WasEnabled = component != null && component.enabled;
            }
        }

        private readonly Animator animator;
        private readonly WindrunnerAnimationProfile profile;
        private readonly Behaviour[] legacyWriters;
        private readonly Transform feedbackSource;
        private readonly bool filterFeedbackSource;
        private readonly List<SuspendedBehaviour> suspended = new List<SuspendedBehaviour>();
        private IFeedbackService feedback;
        private WeaponPosePipelineRuntime graph;
        private GroundOnlyPipelineRuntime standaloneGraph;
        private RuntimeAnimatorController legacyController;
        private bool legacyFireEvents;
        private bool legacyRootMotion;
        private bool ownsAnimator;
        private bool disposed;
        private int lastFrameId = -1;

        public bool ReadRecordingPose(out GroundOnlyPipelineRuntime.RecordingPose pose)
        {
            pose = default;
            if (standaloneGraph == null || !standaloneGraph.IsValid) return false;
            standaloneGraph.ReadRecordingPose(out pose);
            return true;
        }
        public int FrameSamples { get; private set; }
        public int GraphFrames { get; private set; }
        public int ObservedEvents { get; private set; }
        public int MappedEvents { get; private set; }
        public int StartedActions { get; private set; }
        public int BlockedActions { get; private set; }
        public FeedbackKey LastObservedEvent { get; private set; }
        public ContextualMotionState LastContext { get; private set; }
        public string LastError { get; private set; } = "";
        public bool HasActiveStandaloneAction => standaloneGraph?.HasActiveAction == true;
        public bool PortableActive => (graph != null && graph.IsValid) ||
                                      (standaloneGraph != null && standaloneGraph.IsValid);
        public string ActiveMontage => graph != null ? graph.ActiveMontage :
            standaloneGraph != null ? standaloneGraph.ActiveAction : "Legacy";
        public event Action<MontageSignal> MontageSignalRaised;

        public WindrunnerAnimationSession(Animator animator, WindrunnerAnimationProfile profile,
            Behaviour[] legacyWriters, Transform feedbackSource = null)
        {
            this.animator = animator;
            this.profile = profile;
            this.legacyWriters = legacyWriters ?? Array.Empty<Behaviour>();
            this.feedbackSource = feedbackSource;
            filterFeedbackSource = !ReferenceEquals(feedbackSource, null);
        }

        public void BindFeedback(IFeedbackService source)
        {
            if (disposed) return;
            if (ReferenceEquals(feedback, source)) return;
            if (feedback != null) feedback.Observed -= OnFeedbackObserved;
            feedback = source;
            if (feedback != null) feedback.Observed += OnFeedbackObserved;
        }

        private void OnFeedbackObserved(FeedbackKey key, FeedbackRequest request)
        {
            if (filterFeedbackSource && (feedbackSource == null || request.Source != feedbackSource)) return;
            ObservedEvents++;
            LastObservedEvent = key;
            ApplyPresentationBinding(key);
        }

        // Reconstruct sustained presentation from current gameplay state. This
        // never publishes feedback or invokes gameplay events/resources.
        public void RecoverSustainedActions(FeedbackKey slingPhase, bool casting, bool dashing)
            => RestoreAbilityPoses(slingPhase, casting, dashing, false);

        public void ResumeHeldLoops(FeedbackKey slingPhase, bool casting, bool dashing)
            => RestoreAbilityPoses(slingPhase, casting, dashing, true);

        private void RestoreAbilityPoses(FeedbackKey slingPhase, bool casting, bool dashing, bool loopsOnly)
        {
            if(!PortableActive || disposed)return;
            // A service rebind may have missed an end event. Clear only
            // sustained groups; completed/transient flip actions are not replayed.
            standaloneGraph?.CancelGroup("Sling");
            standaloneGraph?.CancelGroup("Cast");
            standaloneGraph?.CancelGroup("Dash");
            if(dashing)ApplyPresentationBinding(FeedbackKey.DashStarted,loopsOnly);
            if(casting)ApplyPresentationBinding(FeedbackKey.CastChargeStarted,loopsOnly);
            if(slingPhase!=FeedbackKey.None)ApplyPresentationBinding(slingPhase,loopsOnly);
        }

        private void ApplyPresentationBinding(FeedbackKey key, bool loopsOnly = false)
        {
            if (profile == null || profile.actionBindings == null) return;
            for (int i = 0; i < profile.actionBindings.Length; i++)
            {
                WindrunnerAnimationActionBinding binding = profile.actionBindings[i];
                if (binding == null || binding.feedbackEvent != key ||
                    binding.montageSlot < 0) continue;
                MappedEvents++;
                if (!PortableActive) return; // Observation never plays an animation.
                int count = standaloneGraph != null
                    ? standaloneGraph.LiveActionCount : graph.LiveActionCount;
                if (binding.montageSlot >= count)
                {
                    BlockedActions++;
                    LastError = "Montage slot " + binding.montageSlot +
                                " is outside configured action range.";
                    return;
                }
                try
                {
                    if (standaloneGraph != null)
                    {
                        if (loopsOnly && !standaloneGraph.IsLoopingSlot(binding.montageSlot)) return;
                        standaloneGraph.CancelGroup(binding.cancelGroup);
                        if (binding.operation == WindrunnerAnimationOperation.Cancel)
                        {
                            standaloneGraph.CancelAction(binding.montageSlot);
                            return;
                        }
                        if (binding.cancelBeforePlaySlot >= 0)
                            standaloneGraph.CancelAction(binding.cancelBeforePlaySlot);
                        if (standaloneGraph.RequestAction(binding.montageSlot))
                            StartedActions++;
                        else BlockedActions++;
                    }
                    else
                    {
                        MontageRequestResult result = graph.RequestAction(binding.montageSlot);
                        if (result == MontageRequestResult.Started) StartedActions++;
                        else BlockedActions++;
                    }
                }
                catch (Exception ex)
                {
                    // This callback runs inside FeedbackService's event delivery.
                    // Never let a broken montage interrupt other feedback listeners.
                    BlockedActions++;
                    LastError = ex.ToString();
                    StopPortable();
                    Debug.LogError("[Windrunner Animation] Montage request failed; " +
                                   (profile.productionOnly ? "production graph stopped without legacy fallback. " : "legacy Animator restored. ") + LastError);
                }
                return; // At most one route per semantic event.
            }
        }

        public bool TryStartPortable()
        {
            if (disposed)
            {
                LastError = "The animation session is disposed.";
                return false;
            }
            if (PortableActive) return true;
            if (animator != null && Owners.TryGetValue(animator, out var owner) && !ReferenceEquals(owner, this))
            {
                LastError = "This Animator already has a portable pose owner.";
                return false;
            }
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman || !animator.isHuman)
            {
                LastError = "Valid Humanoid Animator and Avatar required.";
                return false;
            }
            string reason = "";
            if (profile == null || !profile.ValidateForLiveGraph(out reason))
            {
                LastError = profile == null ? "Windrunner animation profile missing." : reason;
                return false;
            }

            // This must happen only AFTER validation. A failed setup always
            // restores the exact controller/events/component states.
            legacyController = animator.runtimeAnimatorController;
            legacyFireEvents = animator.fireEvents;
            legacyRootMotion = animator.applyRootMotion;
            suspended.Clear();
            try
            {
                Owners.Add(animator, this);
                var seen = new HashSet<Behaviour>();
                foreach (Behaviour writer in legacyWriters)
                {
                    if (writer == null || !seen.Add(writer)) continue;
                    suspended.Add(new SuspendedBehaviour(writer));
                    writer.enabled = false;
                }
                ownsAnimator = true;
                animator.fireEvents = false; // Portable montage markers own animation timing.
                animator.applyRootMotion = false; // Windrunner motor owns physical movement.
                animator.runtimeAnimatorController = null;

                if (profile.useStandaloneCharacterGraph)
                {
                    standaloneGraph = new GroundOnlyPipelineRuntime(animator,
                        profile.standaloneGroundProfile, profile.standaloneActionProfile, profile.previewOriginalTiming);
                }
                else
                {
                    graph = new WeaponPosePipelineRuntime(animator,
                        profile.weaponProfile, profile.groundProfile, profile.aeroProfile,
                        profile.extensionProfile, profile.postPoseIKProfile);
                    if (profile.contextualActionProfile != null)
                        graph.SetContextualActionProfile(profile.contextualActionProfile);
                    graph.MontageSignalRaised += OnMontageSignal;
                }
                lastFrameId = -1;
                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                StopPortable();
                return false;
            }
        }

        public void Evaluate(in WindrunnerMotionFrame motion)
        {
            if (disposed || motion.FrameId == lastFrameId) return;
            lastFrameId = motion.FrameId;
            FrameSamples++;

            float full = Mathf.Max(0.1f, profile != null ? profile.fullRunSpeed : 8.5f);
            float speedMultiplier = profile != null ? profile.motionSpeedMultiplier : 1f;
            float normalized = Mathf.Clamp01(motion.HorizontalSpeed / full * speedMultiplier);
            LastContext = !motion.Grounded
                ? (motion.VerticalSpeed > 0.15f ?
                    ContextualMotionState.Rising : ContextualMotionState.Falling)
                : (normalized < 0.04f ? ContextualMotionState.Idle :
                    normalized < 0.40f ? ContextualMotionState.Walking :
                    ContextualMotionState.Running);

            if (!PortableActive) return;
            try
            {
                bool adaptive = profile != null && profile.standaloneGroundProfile != null && profile.standaloneGroundProfile.HasAdaptiveLocomotion;
                Vector3 observed = adaptive && motion.Grounded ? motion.ResolvedLocalVelocity : motion.LocalVelocity;
                float groundNormalized = adaptive && motion.Grounded ? Mathf.Clamp01(new Vector2(observed.x, observed.z).magnitude / full * speedMultiplier) : normalized;
                Vector3 local = observed / full * speedMultiplier;
                if (profile != null && Mathf.Abs(profile.locomotionYawOffset) > 0.01f)
                    local = Quaternion.Euler(0f, profile.locomotionYawOffset, 0f) * local;
                var state = new AnimationFrameState(motion.FrameId,
                    Mathf.Clamp(motion.DeltaTime, 0f, 0.1f), new Vector3(local.x, 0f, local.z),
                    groundNormalized, motion.Grounded ? LocomotionMode.Grounded : LocomotionMode.Airborne);
                var ground = new GroundMotionFrame(state, motion.Grounded,
                    motion.VerticalSpeed, motion.YawRate, motion.JumpStarted,
                    new Vector2(motion.ResolvedLocalVelocity.x, motion.ResolvedLocalVelocity.z).magnitude);
                if (standaloneGraph != null)
                {
                    standaloneGraph.Evaluate(in ground,motion.HorizontalSpeed);
                }
                else
                {
                    var aim = new AimOffsetFrame(state, motion.AimYaw, motion.AimPitch,
                        profile != null && profile.enableNativeAimOffset, false, false, true);
                    graph.LiveContextOverride = LastContext;
                    graph.Evaluate(in aim, in ground);
                }
                GraphFrames++;
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                StopPortable();
                Debug.LogError("[Windrunner Animation] Portable graph failed; " +
                               (profile.productionOnly ? "production graph stopped without legacy fallback. " : "legacy Animator restored. ") + LastError);
            }
        }

        private void OnMontageSignal(MontageSignal signal)
        {
            MontageSignalRaised?.Invoke(signal);
            // Deliberately no gameplay dispatch here. Native animation events
            // must be migrated per action before replacing legacy timing.
        }

        public void StopPortable()
        {
            if (standaloneGraph != null)
            {
                try { standaloneGraph.Dispose(); }
                catch (Exception ex) { Debug.LogException(ex); }
                standaloneGraph = null;
            }
            if (graph != null)
            {
                graph.MontageSignalRaised -= OnMontageSignal;
                try { graph.Dispose(); }
                catch (Exception ex) { Debug.LogException(ex); }
                graph = null;
            }
            if (ownsAnimator && animator != null && (profile == null || !profile.productionOnly))
            {
                animator.runtimeAnimatorController = legacyController;
                animator.fireEvents = legacyFireEvents;
                animator.applyRootMotion = legacyRootMotion;
            }
            ownsAnimator = false;
            if (!ReferenceEquals(animator, null) && Owners.TryGetValue(animator, out var owner) && ReferenceEquals(owner, this))
                Owners.Remove(animator);
            for (int i = suspended.Count - 1; i >= 0; i--)
                if (suspended[i].Component != null)
                    suspended[i].Component.enabled = profile != null && profile.productionOnly ? false : suspended[i].WasEnabled;
            suspended.Clear();
        }

        public void Dispose()
        {
            if (disposed) return;
            StopPortable();
            BindFeedback(null);
            disposed = true;
            MontageSignalRaised = null;
        }
    }
}
