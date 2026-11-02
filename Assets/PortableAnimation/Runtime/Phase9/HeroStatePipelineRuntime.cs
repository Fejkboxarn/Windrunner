using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// One authoritative graph:
    /// Locomotion -> body/head HitReact -> montage AbilityBlend -> AimOffset
    /// -> HeroState override policy stages -> Animator output.
    ///
    /// HeroState clips are full poses, selectively mixed according to semantic
    /// body-region policy. Gameplay requests are independent from graph ownership.
    /// </summary>
    public sealed class HeroStatePipelineRuntime : IDisposable
    {
        private readonly struct BoundBone
        {
            public readonly string Path;
            public readonly AbilityRegion Region;
            public BoundBone(string path, AbilityRegion region)
            { Path = path; Region = region; }
        }

        private readonly HeroStateProfile heroProfile;
        private readonly HitReactController hit;
        private readonly ActionMontageController montage;
        private readonly HeroStateController hero;
        private readonly ActionMontageProfile montageProfile;
        private readonly BoundBone[] bones;
        private readonly float[] actionP, actionR, actionS, actionMixP, actionMixR, actionMixS;
        private readonly float[] stateP, stateR, stateS, stateMixP, stateMixR, stateMixS;
        private readonly List<ControllableRegionStage> actionStages = new List<ControllableRegionStage>();
        private readonly List<ControllableRegionStage> heroStages = new List<ControllableRegionStage>();

        private PlayableGraph graph;
        private ClipLocomotionSource locomotion;
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
        public AimOffsetSource AimSource => aim.SelectedSource;
        public float AimStrength => aim.Strength;
        public float BodyHitWeight => hit.BodyWeight;
        public float HeadHitWeight => hit.HeadWeight;
        public float GetStateWeight(int i) => hero.GetWeight(i);
        public HeroPoseState GetStateAt(int i) => hero.Entry(i).State;
        public float GetActionWeight(int i) => montage.GetWeight(i);

        public HeroStatePipelineRuntime(Animator animator, HeroStateProfile profile)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            heroProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();

            HitReactProfile hitProfile = profile.PreviousPipeline;
            montageProfile = hitProfile.AimOffsetProfile.MontageProfile;
            hero = new HeroStateController(profile);
            hit = new HitReactController(hitProfile);
            montage = new ActionMontageController(montageProfile);

            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            bones = new BoundBone[Mathf.Max(0, transforms.Length - 1)];
            for (int i = 0; i < bones.Length; i++)
            {
                string path = GetPath(animator.transform, transforms[i + 1]);
                bones[i] = new BoundBone(path,
                    montageProfile.BaseProfile.RegionMap.Resolve(path));
            }

            int aCount = montageProfile.Actions.Count;
            actionP = new float[aCount]; actionR = new float[aCount]; actionS = new float[aCount];
            actionMixP = new float[aCount]; actionMixR = new float[aCount]; actionMixS = new float[aCount];

            int hCount = profile.States.Count;
            stateP = new float[hCount]; stateR = new float[hCount]; stateS = new float[hCount];
            stateMixP = new float[hCount]; stateMixR = new float[hCount]; stateMixS = new float[hCount];

            graph = PlayableGraph.Create("PortableAnimation.HeroStateIntegrated");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baseProfile = montageProfile.BaseProfile;
                locomotion = new ClipLocomotionSource(graph, baseProfile.Idle,
                    baseProfile.Moving, baseProfile.LocomotionBlendRate);

                var bodyClips = new AnimationClip[4];
                var headClips = new AnimationClip[4];
                for (int i = 0; i < 4; i++)
                {
                    bodyClips[i] = hitProfile.BodyClip((HitDirection)i);
                    headClips[i] = hitProfile.HeadClip((HitDirection)i);
                }

                bodyHit = new HitReactPoseStage(graph, animator, locomotion.Output,
                    bodyClips, hitProfile.NeutralReference, hitProfile.BodyMask,
                    AdditiveRotationMode.LocalSpace);
                headHit = new HitReactPoseStage(graph, animator, bodyHit.Output,
                    headClips, hitProfile.NeutralReference, hitProfile.HeadMask,
                    AdditiveRotationMode.ComponentSpace);

                Playable pose = headHit.Output;
                foreach (var entry in montageProfile.Actions)
                {
                    var stage = new ControllableRegionStage(graph, animator, pose, entry.Clip);
                    actionStages.Add(stage);
                    pose = stage.Output;
                }
                aim = new AimOffsetPoseStage(graph, animator, pose,
                    hitProfile.AimOffsetProfile);
                pose = aim.Output;

                foreach (var entry in heroProfile.States)
                {
                    var stage = new ControllableRegionStage(graph, animator, pose, entry.Clip);
                    heroStages.Add(stage);
                    pose = stage.Output;
                }

                var output = AnimationPlayableOutput.Create(graph, "FinalHeroStatePose", animator);
                output.SetSourcePlayable(pose);
                graph.Play();
                graph.Evaluate(0f);
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

        public void Evaluate(in AimOffsetFrame frame)
        {
            if (!graph.IsValid())
                throw new ObjectDisposedException(nameof(HeroStatePipelineRuntime));
            if (lastFrame == frame.Locomotion.FrameId) return;
            lastFrame = frame.Locomotion.FrameId;
            float dt = frame.Locomotion.DeltaTime;

            locomotion.Tick(frame.Locomotion.NormalizedSpeed, dt);
            hero.Advance(dt);
            hit.Advance(dt);
            bodyHit.SetState(hit, false);
            headHit.SetState(hit, true);

            // A blocking state cancels existing actions, including automatic
            // knockdown -> recovery transitions that happened this frame.
            if (hero.BlocksActions && montage.ActiveIndex >= 0)
                montage.Cancel(true);

            montage.Advance(dt);
            bool montageVisible = montage.ActiveIndex >= 0;
            for (int i = 0; i < actionStages.Count; i++)
            {
                actionStages[i].Sample(montage.GetClipTime(i));
                if (montage.GetWeight(i) > 0.001f) montageVisible = true;
            }
            ResolveActionRegions();

            var filteredAim = new AimOffsetFrame(
                frame.Locomotion, frame.Yaw, frame.Pitch,
                frame.Enabled && !hero.SuppressesAimOffset,
                frame.CustomIdleActive, frame.PhysicsWeaponActive,
                frame.UpperBodyOnly);
            aim.Tick(in filteredAim, montageVisible);

            for (int i = 0; i < heroStages.Count; i++)
                heroStages[i].Sample(hero.GetClipTime(i));
            ResolveHeroRegions();
            graph.Evaluate(dt);
        }

        private void ResolveActionRegions()
        {
            int count = actionStages.Count;
            for (int b = 0; b < bones.Length; b++)
            {
                var region = bones[b].Region;
                for (int i = 0; i < count; i++)
                {
                    BoneChannelWeights policy = montageProfile.Actions[i].Policy.Resolve(region);
                    float w = montage.GetWeight(i);
                    actionP[i] = w * policy.position;
                    actionR[i] = w * policy.rotation;
                    actionS[i] = w * policy.scale;
                }
                AbilityBlendMath.NormalizeSequential(actionP, actionMixP);
                AbilityBlendMath.NormalizeSequential(actionR, actionMixR);
                AbilityBlendMath.NormalizeSequential(actionS, actionMixS);
                for (int i = 0; i < count; i++)
                    actionStages[i].SetBoneWeights(bones[b].Path,
                        new BoneChannelWeights(actionMixP[i], actionMixR[i], actionMixS[i]));
            }
        }

        private void ResolveHeroRegions()
        {
            int count = heroStages.Count;
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
                    heroStages[i].SetBoneWeights(bones[b].Path,
                        new BoneChannelWeights(stateMixP[i], stateMixR[i], stateMixS[i]));
            }
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            foreach (var stage in heroStages) stage.Dispose();
            heroStages.Clear();
            aim?.Dispose(); aim = null;
            foreach (var stage in actionStages) stage.Dispose();
            actionStages.Clear();
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
