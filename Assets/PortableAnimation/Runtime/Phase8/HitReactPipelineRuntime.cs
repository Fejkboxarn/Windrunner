using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// Integrated Phase 8 topology, one manually evaluated PlayableGraph:
    /// Locomotion -> Body Hit -> Head Hit -> Montage ability chain
    /// -> 2D AimOffset -> Animator.
    ///
    /// Does NOT create a disconnected second Animator/PlayableGraph.
    /// The gameplay project supplies state, montage commands, and hit events.
    /// </summary>
    public sealed class HitReactPipelineRuntime : IDisposable
    {
        private readonly struct BoundBone
        {
            public readonly string Path;
            public readonly AbilityRegion Region;
            public BoundBone(string path, AbilityRegion region)
            { Path = path; Region = region; }
        }

        private PlayableGraph graph;
        private ClipLocomotionSource locomotion;
        private HitReactPoseStage bodyStage;
        private HitReactPoseStage headStage;
        private AimOffsetPoseStage aimStage;
        private readonly List<ControllableRegionStage> actionStages =
            new List<ControllableRegionStage>();

        private readonly ActionMontageProfile montageProfile;
        private readonly ActionMontageController montage;
        private readonly HitReactController hit;
        private readonly BoundBone[] bones;
        private readonly float[] p, r, s, mixedP, mixedR, mixedS;
        private int lastFrame = -1;

        public event Action<MontageSignal> MontageSignalRaised
        {
            add { montage.SignalRaised += value; }
            remove { montage.SignalRaised -= value; }
        }
        public event Action<HitReactSignal> HitSignalRaised
        {
            add { hit.SignalRaised += value; }
            remove { hit.SignalRaised -= value; }
        }

        public bool IsValid => graph.IsValid();
        public bool HitActive => hit.IsActive;
        public HitSeverity CurrentSeverity => hit.Severity;
        public HitDirection CurrentDirection => hit.Direction;
        public float HitElapsed => hit.Elapsed;
        public float BodyHitWeight => hit.BodyWeight;
        public float HeadHitWeight => hit.HeadWeight;
        public float DirectionWeight(int index) => hit.DirectionWeight(index);
        public int BoundBoneCount => bones.Length;
        public AimOffsetSource SelectedAimSource => aimStage.SelectedSource;
        public float AimStrength => aimStage.Strength;
        public string ActiveActionName => montage.GetName(montage.ActiveIndex);
        public string QueuedActionName => montage.GetName(montage.QueuedIndex);
        public int ActiveActionIndex => montage.ActiveIndex;
        public float ActionWeight(int index) => montage.GetWeight(index);

        public HitReactPipelineRuntime(Animator animator, HitReactProfile profile)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.AssertValid();

            montageProfile = profile.AimOffsetProfile.MontageProfile;
            montage = new ActionMontageController(montageProfile);
            hit = new HitReactController(profile);

            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            bones = new BoundBone[Mathf.Max(0, transforms.Length - 1)];
            for (int i = 0; i < bones.Length; i++)
            {
                string path = GetPath(animator.transform, transforms[i + 1]);
                bones[i] = new BoundBone(path,
                    montageProfile.BaseProfile.RegionMap.Resolve(path));
            }

            int actionCount = montageProfile.Actions.Count;
            p = new float[actionCount]; r = new float[actionCount]; s = new float[actionCount];
            mixedP = new float[actionCount]; mixedR = new float[actionCount]; mixedS = new float[actionCount];

            graph = PlayableGraph.Create("PortableAnimation.HitReactIntegrated");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AbilityBlendProfile baseProfile = montageProfile.BaseProfile;
                locomotion = new ClipLocomotionSource(graph, baseProfile.Idle,
                    baseProfile.Moving, baseProfile.LocomotionBlendRate);

                var bodyClips = new AnimationClip[4];
                var headClips = new AnimationClip[4];
                for (int i = 0; i < 4; i++)
                {
                    bodyClips[i] = profile.BodyClip((HitDirection)i);
                    headClips[i] = profile.HeadClip((HitDirection)i);
                }

                bodyStage = new HitReactPoseStage(graph, animator, locomotion.Output,
                    bodyClips, profile.NeutralReference, profile.BodyMask,
                    AdditiveRotationMode.LocalSpace);
                headStage = new HitReactPoseStage(graph, animator, bodyStage.Output,
                    headClips, profile.NeutralReference, profile.HeadMask,
                    AdditiveRotationMode.ComponentSpace);

                Playable composed = headStage.Output;
                foreach (var entry in montageProfile.Actions)
                {
                    var action = new ControllableRegionStage(graph, animator,
                        composed, entry.Clip);
                    actionStages.Add(action);
                    composed = action.Output;
                }

                aimStage = new AimOffsetPoseStage(graph, animator,
                    composed, profile.AimOffsetProfile);

                var output = AnimationPlayableOutput.Create(graph, "FinalHitReactPose", animator);
                output.SetSourcePlayable(aimStage.Output);
                graph.Play();
                graph.Evaluate(0f);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void TriggerHit(HitSeverity severity, HitDirection direction) =>
            hit.Trigger(severity, direction);

        public MontageRequestResult RequestAction(int actionIndex) => montage.Request(actionIndex);
        public void CancelAction() => montage.Cancel();
        public void ClearActionQueue() => montage.ClearQueue();

        public void Evaluate(in AimOffsetFrame frame)
        {
            if (!graph.IsValid())
                throw new ObjectDisposedException(nameof(HitReactPipelineRuntime));
            if (lastFrame == frame.Locomotion.FrameId) return;
            lastFrame = frame.Locomotion.FrameId;
            float dt = frame.Locomotion.DeltaTime;

            locomotion.Tick(frame.Locomotion.NormalizedSpeed, dt);
            hit.Advance(dt);
            bodyStage.SetState(hit, false);
            headStage.SetState(hit, true);

            montage.Advance(dt);
            bool montagePresent = montage.ActiveIndex >= 0;
            for (int i = 0; i < actionStages.Count; i++)
            {
                actionStages[i].Sample(montage.GetClipTime(i));
                if (montage.GetWeight(i) > 0.001f) montagePresent = true;
            }

            ResolveAbilityRegions();
            aimStage.Tick(in frame, montagePresent);
            graph.Evaluate(dt);
        }

        private void ResolveAbilityRegions()
        {
            int count = actionStages.Count;
            for (int b = 0; b < bones.Length; b++)
            {
                AbilityRegion region = bones[b].Region;
                for (int a = 0; a < count; a++)
                {
                    var policy = montageProfile.Actions[a].Policy.Resolve(region);
                    float alpha = montage.GetWeight(a);
                    p[a] = alpha * policy.position;
                    r[a] = alpha * policy.rotation;
                    s[a] = alpha * policy.scale;
                }

                AbilityBlendMath.NormalizeSequential(p, mixedP);
                AbilityBlendMath.NormalizeSequential(r, mixedR);
                AbilityBlendMath.NormalizeSequential(s, mixedS);
                for (int a = 0; a < count; a++)
                    actionStages[a].SetBoneWeights(bones[b].Path,
                        new BoneChannelWeights(mixedP[a], mixedR[a], mixedS[a]));
            }
        }

        public void Dispose()
        {
            // Graph destruction completes playback before NativeArray release.
            if (graph.IsValid()) graph.Destroy();
            aimStage?.Dispose(); aimStage = null;
            foreach (var action in actionStages) action.Dispose();
            actionStages.Clear();
            headStage?.Dispose(); headStage = null;
            bodyStage?.Dispose(); bodyStage = null;
        }

        private static string GetPath(Transform root, Transform target)
        {
            var pieces = new Stack<string>();
            while (target != null && target != root)
            { pieces.Push(target.name); target = target.parent; }
            if (target != root) throw new ArgumentException("Bone outside Animator hierarchy.");
            return string.Join("/", pieces.ToArray());
        }
    }
}
