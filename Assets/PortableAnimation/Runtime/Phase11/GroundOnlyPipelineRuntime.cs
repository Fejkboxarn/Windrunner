using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// Small production composition: eight-way GroundMotionSource plus an
    /// optional masked clip-action layer. The animation graph NEVER writes
    /// movement or fires gameplay events. Suitable for characters with no
    /// weapons, hit-react profiles or mandatory montage/hero-state graph.
    /// </summary>
    public sealed class GroundOnlyPipelineRuntime : IDisposable
    {
        private readonly GroundMotionSource ground;
        private readonly PortableCharacterActionProfile actionProfile;
        private readonly AvatarMask fullBodyMask;
        private PlayableGraph graph;
        private AnimationLayerMixerPlayable actions;
        private sealed class PoseLayer
        {
            public int action;
            public ActionMotionContext context;
            public AnimationClip clip;
            public AvatarMask mask;
            public float weight, share, lengthRatio;
            public AnimationClipPlayable playable;
        }
        private readonly List<PoseLayer> layers = new List<PoseLayer>();
        private readonly bool[,] available;
        private ActionMotionContext context;
        private readonly PortableActionPlayback playback;

        private int lastFrame = -1;

        public bool IsLoopingSlot(int index) => playback.IsLoopingSlot(index);
        public bool HasActiveAction => playback.ActiveIndex >= 0;
        public bool IsValid => graph.IsValid();
        public int LiveActionCount => actionProfile != null ? actionProfile.Count : 0;
        public string ActiveAction => playback.ActiveIndex < 0 ? "None" :
            actionProfile.Action(playback.ActiveIndex).id;
        public float IdleBlend => ground.IdleBlend;
        public float WalkBlend => ground.WalkBlend;
        public float RunBlend => ground.RunBlend;
        public void ReadRecordingPose(out RecordingPose pose)
        {
            pose = new RecordingPose { state = ground.State, cycles = ground.CyclesPerSecond,
                context = context, walk = ground.WalkBlend, run = ground.RunBlend };
            ground.ReadDominant(out pose.clip, out pose.weight, out pose.direction);
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                float w = actions.GetInputWeight(i + 1);
                if (w <= pose.actionWeight) continue;
                var entry = actionProfile.Action(layer.action);
                pose.actionWeight = w;
                pose.action = entry.id;
                pose.mask = layer.mask;
                pose.actionClip = layer.clip;
                pose.actionSeconds = (float)layer.playable.GetTime();
            }
        }
        public struct RecordingPose
        {
            public GroundMotionState state;
            public ActionMotionContext context;
            public AnimationClip actionClip;
            public AnimationClip clip;
            public int direction;
            public float weight, cycles, walk, run, actionWeight, actionSeconds;
            public string action;
            public AvatarMask mask;
        }
        public GroundMotionState GroundState => ground.State;

        public GroundOnlyPipelineRuntime(Animator animator,
            GroundMotionProfile motionProfile,
            PortableCharacterActionProfile optionalActions)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (motionProfile == null)
                throw new ArgumentNullException(nameof(motionProfile));
            motionProfile.AssertStandaloneValid();
            optionalActions?.AssertValid();
            actionProfile = optionalActions;
            available = new bool[LiveActionCount,3];
            var timings = new PortableActionPlayback.Timing[LiveActionCount];
            for (int i = 0; i < timings.Length; i++)
            {
                var action = actionProfile.Action(i);
                if (action.IsPlayable)
                {
                    layers.Add(new PoseLayer {action=i, clip=action.clip,mask=action.mask,weight=action.blendWeight,share=1});
                    AddVariant(i,ActionMotionContext.Stationary,action.stationary);
                    AddVariant(i,ActionMotionContext.Airborne,action.airborne);
                }
                timings[i] = new PortableActionPlayback.Timing(action.clip != null ? action.clip.length : 0f,
                    action.playbackSpeed, action.blendInSeconds, action.blendOutSeconds, action.priority,
                    action.IsPlayable, action.loopUntilCanceled, action.allowSameActionRestart, action.interruptible);
            }
            playback = new PortableActionPlayback(timings);
            fullBodyMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                fullBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
            fullBodyMask.AddTransformPath(animator.transform, true);

            graph = PlayableGraph.Create("PortableAnimation.GroundOnly");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ground = new GroundMotionSource(graph, motionProfile, true);
                Playable finalPose = ground.Output;

                if (LiveActionCount > 0)
                {
                    actions = AnimationLayerMixerPlayable.Create(graph, layers.Count + 1);
                    graph.Connect(ground.Output, 0, actions, 0);
                    actions.SetInputWeight(0, 1f);
                    for (int i=0;i<layers.Count;i++)
                    {
                        var layer=layers[i];
                        layer.lengthRatio=layer.clip.length/actionProfile.Action(layer.action).clip.length;
                        layer.playable=AnimationClipPlayable.Create(graph,layer.clip);
                        layer.playable.SetSpeed(0);
                        actions.SetLayerMaskFromAvatarMask((uint)(i+1),layer.mask ? layer.mask : fullBodyMask);
                        if (!graph.Connect(layer.playable,0,actions,i+1))
                            throw new InvalidOperationException("Could not connect action pose "+layer.clip.name);
                    }
                    finalPose = actions;
                }

                var output = AnimationPlayableOutput.Create(graph,
                    "CharacterPose", animator);
                output.SetSourcePlayable(finalPose);
                graph.Play();
                graph.Evaluate(0f);
            }
            catch
            {
                if (graph.IsValid()) graph.Destroy();
                if (fullBodyMask != null) UnityEngine.Object.Destroy(fullBodyMask);
                throw;
            }
        }

        public bool RequestAction(int index)
        {
            if (!graph.IsValid() || actionProfile == null || index < 0 ||
                index >= actionProfile.Count)
                return false;
            if (!playback.Request(index)) return false;

            return true;
        }

        private void AddVariant(int action, ActionMotionContext variant, ActionPoseOverride pose)
        {
            if (pose == null || !pose.Available) return;
            available[action,(int)variant]=true;
            layers.Add(new PoseLayer {action=action, context=variant, clip=pose.clip, mask=pose.mask, weight=pose.weight});
        }

        public void Evaluate(in GroundMotionFrame motion) => Evaluate(motion,motion.Locomotion.LocalVelocity.magnitude);
        public void Evaluate(in GroundMotionFrame motion,float horizontalMetresPerSecond)
        {
            if (!graph.IsValid()) throw new ObjectDisposedException(nameof(GroundOnlyPipelineRuntime));
            if (motion.Locomotion.FrameId == lastFrame) return;
            lastFrame = motion.Locomotion.FrameId;
            ground.Tick(in motion);
            if(actionProfile)
                context=actionProfile.SelectContext(motion.Grounded,horizontalMetresPerSecond,context);
            UpdateAction(motion.Locomotion.DeltaTime);
            graph.Evaluate(motion.Locomotion.DeltaTime);
        }

        public void CancelAction(int index = -1) => playback.Cancel(index);
        public void CancelGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return;
            for (int i = 0; i < LiveActionCount; i++)
                if (actionProfile.Action(i).group == group) playback.Cancel(i);
        }

        private void UpdateAction(float dt)
        {
            if (LiveActionCount == 0) return;
            // Switch visual inputs on the existing action clock. A context change
            // cannot request an action, emit feedback or restart its release.
            float step = 1f - Mathf.Exp(-3f*dt/actionProfile.contextFadeSeconds);
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];
                var target=available[layer.action,(int)context] ? context : ActionMotionContext.GroundedMoving;
                float share=layer.context==target ? 1f : 0f;
                layer.share=playback.Weight(layer.action)<=0 ? share : Mathf.Lerp(layer.share,share,step);
            }
            playback.Tick(dt);
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];
                layer.playable.SetTime(playback.ClipTime(layer.action)*layer.lengthRatio);
                actions.SetInputWeight(i+1,layer.share*layer.weight*playback.Weight(layer.action));
            }
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (fullBodyMask != null) UnityEngine.Object.Destroy(fullBodyMask);
        }
    }
}
