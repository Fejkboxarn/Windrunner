using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// One authoritative graph:
    /// Locomotion -> HitReact -> PhysicsWeapon idle overlays -> AbilityBlend
    /// -> AimOffset -> HeroState -> Animator output.
    ///
    /// HeroState clips are full poses, selectively mixed according to semantic
    /// body-region policy. Gameplay requests are independent from graph ownership.
    /// </summary>
    public sealed class WeaponPosePipelineRuntime : IDisposable
    {
        private readonly struct BoundBone
        {
            public readonly string Path;
            public readonly AbilityRegion Region;
            public BoundBone(string path, AbilityRegion region)
            { Path = path; Region = region; }
        }

        private readonly HeroStateProfile heroProfile;
        private readonly WeaponPoseProfile weaponProfile;
        private readonly WeaponPoseController weapon;
        private FlattenedPoseStack weaponStage;
        private FlattenedPoseStack actionStage;
        private FlattenedPoseStack heroStage;

        // Integration safety invariant: exactly three grouped overlay jobs.
        public const int GroupedOverlayJobs = 3;
        private readonly HitReactController hit;
        private readonly ActionMontageController montage;
        private readonly HeroStateController hero;
        private readonly ActionMontageProfile montageProfile;
        private readonly BoundBone[] bones;
        private readonly float[] actionP, actionR, actionS, actionMixP, actionMixR, actionMixS;
        private readonly float[] stateP, stateR, stateS, stateMixP, stateMixR, stateMixS;

        private PlayableGraph graph;
        private ILocomotionPoseSource locomotion;
        private GroundMotionSource groundMotion;
        private AeroMotionSource aeroMotion;
        private CharacterExtensionPoseStage extensionStage;
        private PostPoseIKRuntime postIK;
        private ContextualActionInfluenceController contextualActions;

        private ActionBlendComparisonMode actionComparison = ActionBlendComparisonMode.Contextual;
        // Batch 16.3.1: optional live authoring hooks. Null uses the validated behavior.
        // Evaluated in C# before the existing grouped action job; no graph rebuild.
        public Func<int, string, ContextualMotionState, float, BoneChannelWeights,
            BoneChannelWeights> LiveBoneMaskOverride { get; set; }
        public ContextualMotionState? LiveContextOverride { get; set; }
        public int LiveActionCount => montageProfile.Actions.Count;
        public ActionBlendComparisonMode ActionComparisonMode => actionComparison;
        public void SetActionComparisonMode(ActionBlendComparisonMode mode)
        {
            if ((int)mode < 0 || (int)mode > 2)
                throw new ArgumentOutOfRangeException(nameof(mode));
            actionComparison = mode;
        }
        public double ActiveActionTime => montage.ActiveIndex < 0 ? 0d :
            montage.GetClipTime(montage.ActiveIndex);
        public double ActiveActionClipLength => montage.ActiveIndex < 0 ? 0d :
            montageProfile.Actions[montage.ActiveIndex].Clip.length;
        public float ActiveActionSpeed => montage.ActiveIndex < 0 ? 0f :
            montageProfile.Actions[montage.ActiveIndex].PlaybackSpeed;
        public float ActiveActionWeight => montage.ActiveIndex < 0 ? 0f :
            montage.GetWeight(montage.ActiveIndex);
        public float GroundWalkBlend => groundMotion == null ? 0f : groundMotion.WalkBlend;
        public float GroundRunBlend => groundMotion == null ? 0f : groundMotion.RunBlend;
        public float GroundIdleBlend => groundMotion == null ? 1f : groundMotion.IdleBlend;
        public float GroundCyclePhase => groundMotion == null ? 0f : groundMotion.MovementCyclePhase;
        public bool UsesContextualActions => contextualActions != null;
        public ContextualMotionState CurrentActionContext => contextualActions == null
            ? ContextualMotionState.Idle : contextualActions.CurrentContext;
        public BoneChannelWeights GetContextualActionWeights(int actionIndex, AbilityRegion region)
        {
            if (actionIndex < 0 || actionIndex >= montageProfile.Actions.Count)
                throw new ArgumentOutOfRangeException(nameof(actionIndex));
            if (actionComparison == ActionBlendComparisonMode.RawFullBody)
                return new BoneChannelWeights(1f, 1f, 1f);
            if (actionComparison == ActionBlendComparisonMode.FixedPolicy ||
                contextualActions == null)
                return montageProfile.Actions[actionIndex].Policy.Resolve(region);
            return contextualActions.Weight(actionIndex, region);
        }
        public BoneChannelWeights GetEffectiveActionWeights(int index, AbilityRegion region)
        {
            BoneChannelWeights local = GetContextualActionWeights(index, region);
            float blend = montage.GetWeight(index);
            // Displayed monitor value is the pre-normalization layer contribution.
            return new BoneChannelWeights(local.position * blend,
                local.rotation * blend, local.scale * blend);
        }
        public string GetContextActionName(int index) => montageProfile.Actions[index].Id;

        public void SetContextualActionProfile(ContextualActionInfluenceProfile profile)
        {
            // Must be configured before evaluating or requesting actions.
            contextualActions = profile == null ? null :
                new ContextualActionInfluenceController(profile, montageProfile);
        }
        private AnimationMixerPlayable groundAeroBlend;
        private HitReactPoseStage bodyHit;
        private HitReactPoseStage headHit;
        private AimOffsetPoseStage aim;
        private int lastFrame = -1;

        public event Action<HeroStateSignal> StateSignalRaised
        { add { hero.SignalRaised += value; } remove { hero.SignalRaised -= value; } }
        public event Action<HitReactSignal> HitSignalRaised
        { add { hit.SignalRaised += value; } remove { hit.SignalRaised -= value; } }
        public event Action<MontageSignal> MontageSignalRaised
        { add { montage.SignalRaised += value; } remove { montage.SignalRaised -= value; } }

        public bool IsValid => graph.IsValid();
        public HeroPoseState ActiveState => hero.ActiveState;
        public bool IsTerminal => hero.IsTerminal;
        public bool IsTransitioning => hero.IsTransitioning;
        public bool BlocksActions => hero.BlocksActions;
        public bool BlocksNewHits => hero.BlocksNewHits;
        public bool SuppressesAim => hero.SuppressesAimOffset;
        public float ActiveStateTime => hero.ActiveTime;
        public int StateCount => hero.Count;
        public int BoundBoneCount => bones.Length;
        public string ActiveMontage => montage.GetName(montage.ActiveIndex);
        public int ActiveActionIndex => montage.ActiveIndex;
        public int QueuedActionIndex => montage.QueuedIndex;
        public AimOffsetSource AimSource => aim.SelectedSource;
        public float AimStrength => aim.Strength;
        public float BodyHitWeight => hit.BodyWeight;
        public float HeadHitWeight => hit.HeadWeight;
        public float GetStateWeight(int i) => hero.GetWeight(i);
        public HeroPoseState GetStateAt(int i) => hero.Entry(i).State;
        public float GetActionWeight(int i) => montage.GetWeight(i);
        public WeaponPoseKind RequestedWeapon => weapon.Requested;
        public bool IsSwitchingWeapon => weapon.IsBlending;
        public int WeaponPoseCount => weapon.Count;
        public float GetWeaponWeight(int i) => weapon.Weight(i);
        public string GetWeaponName(int i) => weaponProfile.Poses[i].Kind.ToString();
        public bool WeaponEquipped => weapon.IsEquipped;
        public bool HasWeaponPose(WeaponPoseKind kind) =>
            kind == WeaponPoseKind.Unarmed || weaponProfile.IndexOf(kind) >= 0;
        public event Action<WeaponPoseSignal> WeaponSignalRaised
        { add { weapon.SignalRaised += value; } remove { weapon.SignalRaised -= value; } }

        // Preserve the exact Phase 10 constructor for existing consumers.
        public WeaponPosePipelineRuntime(Animator animator, WeaponPoseProfile profile)
            : this(animator, profile, null, null, null) { }

        public bool UsesPostPoseIK => postIK != null;
        public float IKLeftWeight => postIK == null ? 0f : postIK.LeftWeight;
        public float IKRightWeight => postIK == null ? 0f : postIK.RightWeight;
        public bool IKLeftTwoBone => postIK != null && postIK.LeftTwoBone;
        public bool IKRightTwoBone => postIK != null && postIK.RightTwoBone;
        public void SetIKEnabled(bool enabled) { if (postIK != null) postIK.SetEnabled(enabled); }
        public void SetIKManualWeights(bool active, float left, float right)
        { if (postIK != null) postIK.SetManualWeights(active, left, right); }
        public void SetIKTargets(Transform left, Transform right,
            Transform leftPole, Transform rightPole)
        { if (postIK != null) postIK.SetTargets(left, right, leftPole, rightPole); }
        public float GetIKTipError(IKArm side) => postIK == null ? 0f : postIK.TipError(side);

        public bool UsesCharacterExtensions => extensionStage != null;
        public string ActiveCharacterExtension => extensionStage == null
            ? "Base" : extensionStage.Controller.ActiveId;
        public bool IsCharacterExtensionTransitioning => extensionStage != null &&
            extensionStage.Controller.IsTransitioning;
        public int CharacterExtensionCount => extensionStage == null ? 0 : extensionStage.EntryCount;
        public float CharacterExtensionBaseWeight => extensionStage == null ? 1f :
            extensionStage.Controller.BaseWeight;
        public float GetCharacterExtensionWeight(int index) => extensionStage == null
            ? 0f : extensionStage.Controller.Weight(index);
        public event Action<CharacterExtensionSignal> CharacterExtensionSignalRaised
        {
            add { if (extensionStage != null) extensionStage.Controller.SignalRaised += value; }
            remove { if (extensionStage != null) extensionStage.Controller.SignalRaised -= value; }
        }
        public CharacterExtensionRequestResult RequestCharacterExtension(
            string id, bool grounded, bool flight)
        {
            if (extensionStage == null) return CharacterExtensionRequestResult.Unknown;
            if (hero.BlocksActions || hero.IsTerminal)
                return CharacterExtensionRequestResult.BlockedByPriority;
            return extensionStage.Controller.Request(id, grounded, flight);
        }
        public void ExitCharacterExtension()
        { if (extensionStage != null) extensionStage.Controller.Exit(); }

        public bool UsesAeroMotion => aeroMotion != null;
        public AeroMotionState AeroState => aeroMotion == null ? AeroMotionState.Grounded : aeroMotion.State;
        public AeroPose CurrentAeroPose => aeroMotion == null ? AeroPose.Hover : aeroMotion.PrimaryPose;
        public float AeroStateTime => aeroMotion == null ? 0f : aeroMotion.StateTime;
        public float AirBlend => aeroMotion == null ? 0f : aeroMotion.AirBlend;
        public float GetAeroPoseWeight(AeroPose pose) => aeroMotion == null ? 0f : aeroMotion.Weight(pose);

        public bool UsesGroundMotion => groundMotion != null;
        public GroundMotionState GroundState => groundMotion == null
            ? GroundMotionState.Idle : groundMotion.State;
        public GroundPose CurrentGroundPose => groundMotion == null
            ? GroundPose.Idle : groundMotion.PrimaryPose;
        public float GroundStateTime => groundMotion == null ? 0f : groundMotion.StateTime;
        public float GetGroundPoseWeight(GroundPose pose) => groundMotion == null
            ? 0f : groundMotion.PoseWeight(pose);

        // Exact Batch 11 API retained for existing consumers.
        public WeaponPosePipelineRuntime(Animator animator, WeaponPoseProfile profile,
            GroundMotionProfile groundProfile)
            : this(animator, profile, groundProfile, null, null) { }

        // Preserve the Batch 12 constructor; no earlier consumers need edits.
        public WeaponPosePipelineRuntime(Animator animator, WeaponPoseProfile profile,
            GroundMotionProfile groundProfile, AeroMotionProfile aeroProfile)
            : this(animator, profile, groundProfile, aeroProfile, null) { }

        // Batch 13 signature remains source- and binary-compatible.
        public WeaponPosePipelineRuntime(Animator animator, WeaponPoseProfile profile,
            GroundMotionProfile groundProfile, AeroMotionProfile aeroProfile,
            CharacterExtensionProfile extensionProfile)
            : this(animator, profile, groundProfile, aeroProfile, extensionProfile, null) { }

        // Opt-in Batch 14. Existing scenes never instantiate an IK processor.
        public WeaponPosePipelineRuntime(Animator animator, WeaponPoseProfile profile,
            GroundMotionProfile groundProfile, AeroMotionProfile aeroProfile,
            CharacterExtensionProfile extensionProfile, PostPoseIKProfile ikProfile)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            weaponProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();
            heroProfile = profile.PreviousPipeline;
            weapon = new WeaponPoseController(profile);

            HitReactProfile hitProfile = heroProfile.PreviousPipeline;
            montageProfile = hitProfile.AimOffsetProfile.MontageProfile;
            hero = new HeroStateController(heroProfile);
            hit = new HitReactController(hitProfile);
            montage = new ActionMontageController(montageProfile);

            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            bones = new BoundBone[Mathf.Max(0, transforms.Length - 1)];
            int resolvedUpperBodyBones = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                string path = GetPath(animator.transform, transforms[i + 1]);
                AbilityRegion region =
                    HumanoidAbilityRegionResolver.Resolve(
                        animator,
                        transforms[i + 1],
                        path,
                        montageProfile.BaseProfile.RegionMap);

                bones[i] = new BoundBone(path, region);

                if (region == AbilityRegion.Spine1 ||
                    region == AbilityRegion.Spine2 ||
                    region == AbilityRegion.Spine3 ||
                    region == AbilityRegion.LeftClavicle ||
                    region == AbilityRegion.LeftArm ||
                    region == AbilityRegion.RightClavicle ||
                    region == AbilityRegion.RightArm ||
                    region == AbilityRegion.Head)
                {
                    resolvedUpperBodyBones++;
                }
            }

            if (animator.isHuman && resolvedUpperBodyBones == 0)
                throw new InvalidOperationException(
                    "PortableAnimation could not classify any Humanoid upper-body bones. " +
                    "Upper-body montages would have zero influence.");

            int aCount = montageProfile.Actions.Count;
            actionP = new float[aCount]; actionR = new float[aCount]; actionS = new float[aCount];
            actionMixP = new float[aCount]; actionMixR = new float[aCount]; actionMixS = new float[aCount];

            int hCount = heroProfile.States.Count;
            stateP = new float[hCount]; stateR = new float[hCount]; stateS = new float[hCount];
            stateMixP = new float[hCount]; stateMixR = new float[hCount]; stateMixS = new float[hCount];

            graph = PlayableGraph.Create("PortableAnimation.WeaponIntegrated");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baseProfile = montageProfile.BaseProfile;
                if (groundProfile != null)
                {
                    groundProfile.AssertValid();
                    if (groundProfile.PreviousPipeline != weaponProfile)
                        throw new InvalidOperationException(
                            "GroundMotion profile must reference the same Phase 10 profile.");
                    groundMotion = new GroundMotionSource(graph, groundProfile);
                    locomotion = groundMotion;
                }
                else
                    locomotion = new ClipLocomotionSource(graph, baseProfile.Idle,
                        baseProfile.Moving, baseProfile.LocomotionBlendRate);

                // Two-input native mixer between GroundMotion and AeroMotion.
                // It adds no custom-job depth to the stabilized graph.
                Playable locomotionPose = locomotion.Output;
                if (aeroProfile != null)
                {
                    if (groundProfile == null)
                        throw new InvalidOperationException(
                            "Phase 12 requires a GroundMotion profile.");
                    aeroProfile.AssertValid();
                    if (aeroProfile.PreviousPipeline != groundProfile)
                        throw new InvalidOperationException(
                            "AeroMotion must reference this Phase 11 profile.");
                    aeroMotion = new AeroMotionSource(graph, aeroProfile);
                    groundAeroBlend = AnimationMixerPlayable.Create(graph, 2);
                    if (!graph.Connect(locomotion.Output, 0, groundAeroBlend, 0) ||
                        !graph.Connect(aeroMotion.Output, 0, groundAeroBlend, 1))
                        throw new InvalidOperationException(
                            "Failed to connect Ground/Aero locomotion mixer.");
                    groundAeroBlend.SetInputWeight(0, 1f);
                    groundAeroBlend.SetInputWeight(1, 0f);
                    locomotionPose = groundAeroBlend;
                }

                // Optional per-character branch, BEFORE HitReact.
                // This is ONE native mixer with N clip inputs, not N serial jobs.
                if (extensionProfile != null)
                {
                    if (aeroProfile == null || groundProfile == null)
                        throw new InvalidOperationException(
                            "Phase 13 requires GroundMotion and AeroMotion profiles.");
                    extensionProfile.AssertValid();
                    if (extensionProfile.PreviousPipeline != aeroProfile)
                        throw new InvalidOperationException(
                            "Character extension must reference this Phase 12 profile.");
                    extensionStage = new CharacterExtensionPoseStage(
                        graph, locomotionPose, extensionProfile);
                    locomotionPose = extensionStage.Output;
                }

                var bodyClips = new AnimationClip[4];
                var headClips = new AnimationClip[4];
                for (int i = 0; i < 4; i++)
                {
                    bodyClips[i] = hitProfile.BodyClip((HitDirection)i);
                    headClips[i] = hitProfile.HeadClip((HitDirection)i);
                }

                bodyHit = new HitReactPoseStage(graph, animator, locomotionPose,
                    bodyClips, hitProfile.NeutralReference, hitProfile.BodyMask,
                    AdditiveRotationMode.LocalSpace);
                headHit = new HitReactPoseStage(graph, animator, bodyHit.Output,
                    headClips, hitProfile.NeutralReference, hitProfile.HeadMask,
                    AdditiveRotationMode.ComponentSpace);

                // One grouped job per processor family: no serial job-per-clip chain.
                // The order remains HitReact -> Weapon -> Actions -> Aim -> HeroState.
                var weaponClips = new AnimationClip[weaponProfile.Poses.Count];
                for (int i = 0; i < weaponClips.Length; i++)
                    weaponClips[i] = weaponProfile.Poses[i].IdleClip;

                Playable pose = headHit.Output;
                weaponStage = new FlattenedPoseStack(
                    graph, animator, pose, weaponClips, false);
                pose = weaponStage.Output;

                var actionClips = new AnimationClip[montageProfile.Actions.Count];
                for (int i = 0; i < actionClips.Length; i++)
                    actionClips[i] = montageProfile.Actions[i].Clip;
                actionStage = new FlattenedPoseStack(
                    graph, animator, pose, actionClips, true);
                pose = actionStage.Output;

                aim = new AimOffsetPoseStage(graph, animator, pose,
                    hitProfile.AimOffsetProfile);
                pose = aim.Output;

                var stateClips = new AnimationClip[heroProfile.States.Count];
                for (int i = 0; i < stateClips.Length; i++)
                    stateClips[i] = heroProfile.States[i].Clip;
                heroStage = new FlattenedPoseStack(
                    graph, animator, pose, stateClips, true);
                pose = heroStage.Output;

                var output = AnimationPlayableOutput.Create(graph, "FinalHeroStatePose", animator);
                output.SetSourcePlayable(pose);
                graph.Play();
                graph.Evaluate(0f);
                if (ikProfile != null)
                {
                    if (extensionProfile == null ||
                        ikProfile.PreviousPipeline != extensionProfile)
                        throw new InvalidOperationException(
                            "Phase 14 IK profile must reference this Phase 13 profile.");
                    postIK = new PostPoseIKRuntime(animator, ikProfile);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public HeroStateRequestResult RequestState(HeroPoseState state)
        {
            var result = hero.Request(state);
            if (result == HeroStateRequestResult.Started && hero.BlocksActions)
                montage.Cancel(true);
            return result;
        }

        /// <summary>Explicit revive; never occurs as a side effect of time advancing.</summary>
        public void ResetTerminal() => hero.ResetTerminal();

        public bool TriggerHit(HitSeverity severity, HitDirection direction)
        {
            if (hero.BlocksNewHits) return false;
            hit.Trigger(severity, direction);
            return true;
        }

        public MontageRequestResult RequestAction(int index)
        {
            return hero.BlocksActions ? MontageRequestResult.Rejected : montage.Request(index);
        }
        public void CancelAction() => montage.Cancel(true);
        public void ClearQueuedAction() => montage.ClearQueue();

        /// <summary>Reject equipment changes while in a terminal death state.</summary>
        public bool RequestWeapon(WeaponPoseKind kind)
        {
            if (hero.IsTerminal) return false;
            weapon.Select(kind);
            return true;
        }

        public void Evaluate(in AimOffsetFrame frame)
        {
            var fallback = new GroundMotionFrame(frame.Locomotion,
                frame.Locomotion.Mode == LocomotionMode.Grounded,
                0f, 0f);
            Evaluate(in frame, in fallback);
        }

        public void Evaluate(in AimOffsetFrame frame, in GroundMotionFrame motion)
        {
            var defaultAero = new AeroMotionFrame(in motion, false);
            Evaluate(in frame, in motion, in defaultAero);
        }

        public void Evaluate(in AimOffsetFrame frame, in GroundMotionFrame motion,
            in AeroMotionFrame flight)
        {
            if (!graph.IsValid())
                throw new ObjectDisposedException(nameof(WeaponPosePipelineRuntime));
            if (lastFrame == frame.Locomotion.FrameId) return;
            lastFrame = frame.Locomotion.FrameId;
            float dt = frame.Locomotion.DeltaTime;

            if (groundMotion != null)
                groundMotion.Tick(in motion);
            else
                locomotion.Tick(frame.Locomotion.NormalizedSpeed, dt);
            if (aeroMotion != null)
            {
                aeroMotion.Tick(in flight);
                float alpha = aeroMotion.AirBlend;
                groundAeroBlend.SetInputWeight(0, 1f - alpha);
                groundAeroBlend.SetInputWeight(1, alpha);
            }
            hero.Advance(dt);
            if (extensionStage != null)
                extensionStage.Tick(dt, motion.Grounded, flight.FlightEnabled,
                    hero.BlocksActions || hero.IsTerminal);
            hit.Advance(dt);
            bodyHit.SetState(hit, false);
            headHit.SetState(hit, true);

            weapon.Advance(dt);
            ResolveWeaponRegions();

            // A blocking state cancels existing actions, including automatic
            // knockdown -> recovery transitions that happened this frame.
            if (hero.BlocksActions && montage.ActiveIndex >= 0)
                montage.Cancel(true);

            montage.Advance(dt);
            if (contextualActions != null)
            {
                ContextualMotionState context = ContextualActionInfluenceController.ResolveContext(
                    groundMotion == null ? GroundMotionState.Idle : groundMotion.State,
                    motion.Grounded, aeroMotion != null && aeroMotion.AirBlend > 0.5f,
                    aeroMotion == null ? AeroMotionState.Grounded : aeroMotion.State,
                    hero.ActiveState);
                contextualActions.Tick(LiveContextOverride ?? context, dt, montage.GetClipTime);
            }
            bool montageVisible = montage.ActiveIndex >= 0;
            for (int i = 0; i < actionStage.OverlayCount; i++)
            {
                actionStage.SetSampleTime(i, montage.GetClipTime(i));
                if (montage.GetWeight(i) > 0.001f) montageVisible = true;
            }
            ResolveActionRegions();

            var filteredAim = new AimOffsetFrame(
                frame.Locomotion, frame.Yaw, frame.Pitch,
                frame.Enabled && !hero.SuppressesAimOffset,
                frame.CustomIdleActive,
                frame.PhysicsWeaponActive ||
                    (weaponProfile.WeaponFeedsAimOffset && weapon.IsEquipped),
                frame.UpperBodyOnly);
            aim.Tick(in filteredAim, montageVisible);

            for (int i = 0; i < heroStage.OverlayCount; i++)
                heroStage.SetSampleTime(i, hero.GetClipTime(i));
            ResolveHeroRegions();
            graph.Evaluate(dt);
            // No extra AnimationScriptPlayable: correction runs only after
            // graph evaluation has finished writing its base animation pose.
            if (postIK != null)
                postIK.Apply(dt, weapon.Requested,
                    hero.BlocksActions || hero.IsTerminal,
                    montage.ActiveIndex >= 0);
        }

        private void ResolveWeaponRegions()
        {
            // Preserve the exact sequential override semantics of Batch 10:
            // per-bone authored mask multiplied by live weapon-controller weight.
            for (int b = 0; b < bones.Length; b++)
            {
                for (int i = 0; i < weaponStage.OverlayCount; i++)
                {
                    BoneChannelWeights mask =
                        weaponProfile.Poses[i].BodyMask.Resolve(bones[b].Path);
                    float alpha = weapon.Weight(i);
                    weaponStage.SetBoneWeights(i, bones[b].Path,
                        new BoneChannelWeights(
                            mask.position * alpha,
                            mask.rotation * alpha,
                            mask.scale * alpha));
                }
            }
        }

        private void ResolveActionRegions()
        {
            int count = actionStage.OverlayCount;
            for (int b = 0; b < bones.Length; b++)
            {
                var region = bones[b].Region;
                for (int i = 0; i < count; i++)
                {
                    BoneChannelWeights policy = GetContextualActionWeights(i, region);
                    if (LiveBoneMaskOverride != null &&
                        actionComparison == ActionBlendComparisonMode.Contextual)
                    {
                        float clipDuration = Mathf.Max(0.0001f, montageProfile.Actions[i].Clip.length);
                        float t = Mathf.Clamp01((float)(montage.GetClipTime(i) / clipDuration));
                        ContextualMotionState context = LiveContextOverride ??
                            (contextualActions != null ? contextualActions.CurrentContext : ContextualMotionState.Idle);
                        policy = LiveBoneMaskOverride(i, bones[b].Path, context, t, policy);
                    }
                    float w = montage.GetWeight(i);
                    actionP[i] = w * policy.position;
                    actionR[i] = w * policy.rotation;
                    actionS[i] = w * policy.scale;
                }
                AbilityBlendMath.NormalizeSequential(actionP, actionMixP);
                AbilityBlendMath.NormalizeSequential(actionR, actionMixR);
                AbilityBlendMath.NormalizeSequential(actionS, actionMixS);
                for (int i = 0; i < count; i++)
                    actionStage.SetBoneWeights(i, bones[b].Path,
                        new BoneChannelWeights(actionMixP[i], actionMixR[i], actionMixS[i]));
            }
        }

        private void ResolveHeroRegions()
        {
            int count = heroStage.OverlayCount;
            for (int b = 0; b < bones.Length; b++)
            {
                var region = bones[b].Region;
                for (int i = 0; i < count; i++)
                {
                    BoneChannelWeights policy = heroProfile.States[i].Resolve(region);
                    float w = hero.GetWeight(i);
                    stateP[i] = w * policy.position;
                    stateR[i] = w * policy.rotation;
                    stateS[i] = w * policy.scale;
                }
                AbilityBlendMath.NormalizeSequential(stateP, stateMixP);
                AbilityBlendMath.NormalizeSequential(stateR, stateMixR);
                AbilityBlendMath.NormalizeSequential(stateS, stateMixS);
                for (int i = 0; i < count; i++)
                    heroStage.SetBoneWeights(i, bones[b].Path,
                        new BoneChannelWeights(stateMixP[i], stateMixR[i], stateMixS[i]));
            }
        }

        public void Dispose()
        {
            postIK = null; // Managed Transforms only; no NativeArray ownership.
            if (graph.IsValid()) graph.Destroy();
            extensionStage?.Dispose(); extensionStage = null;
            heroStage?.Dispose(); heroStage = null;
            aim?.Dispose(); aim = null;
            actionStage?.Dispose(); actionStage = null;
            weaponStage?.Dispose(); weaponStage = null;
            headHit?.Dispose(); headHit = null;
            bodyHit?.Dispose(); bodyHit = null;
        }

        private static string GetPath(Transform root, Transform node)
        {
            var pieces = new Stack<string>();
            while (node != null && node != root)
            { pieces.Push(node.name); node = node.parent; }
            if (node != root) throw new ArgumentException("Transform outside Animator hierarchy.");
            return string.Join("/", pieces.ToArray());
        }
    }
}
