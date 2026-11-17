using System;
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
        private AnimationClipPlayable[] clips;
        private readonly PortableActionPlayback playback;
        private readonly float[] layerWeights;
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
                walk = ground.WalkBlend, run = ground.RunBlend };
            ground.ReadDominant(out pose.clip, out pose.weight, out pose.direction);
            for (int i = 0; i < LiveActionCount; i++)
            {
                float w = actions.GetInputWeight(i + 1);
                if (w <= pose.actionWeight) continue;
                var entry = actionProfile.Action(i);
                pose.actionWeight = w;
                pose.action = entry.id;
                pose.mask = entry.mask;
                pose.actionSeconds = playback.ClipTime(i);
            }
        }
        public struct RecordingPose
        {
            public GroundMotionState state;
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
            layerWeights = new float[LiveActionCount];
            var timings = new PortableActionPlayback.Timing[LiveActionCount];
            for (int i = 0; i < timings.Length; i++)
            {
                var action = actionProfile.Action(i);
                layerWeights[i] = action.blendWeight;
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
                    actions = AnimationLayerMixerPlayable.Create(graph, LiveActionCount + 1);
                    graph.Connect(ground.Output, 0, actions, 0);
                    actions.SetInputWeight(0, 1f);
                    clips = new AnimationClipPlayable[LiveActionCount];
                    for (int i = 0; i < LiveActionCount; i++)
                    {
                        if (!actionProfile.Action(i).IsPlayable) continue;
                        clips[i] = AnimationClipPlayable.Create(graph,
                            actionProfile.Action(i).clip);
                        clips[i].SetSpeed(0d);
                        clips[i].SetTime(0d);
                        // A single tree: each action keeps its own masked layer
                        // while outgoing phases fade back toward locomotion.
                        actions.SetLayerMaskFromAvatarMask((uint)(i + 1), actionProfile.Action(i).mask != null
                            ? actionProfile.Action(i).mask : fullBodyMask);
                        if (!graph.Connect(clips[i], 0, actions, i + 1))
                            throw new InvalidOperationException(
                                "Action layer could not connect clip " + i);
                        actions.SetInputWeight(i + 1, 0f);
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
            clips[index].SetTime(0d);
            return true;
        }

        public void Evaluate(in GroundMotionFrame motion)
        {
            if (!graph.IsValid()) throw new ObjectDisposedException(nameof(GroundOnlyPipelineRuntime));
            if (motion.Locomotion.FrameId == lastFrame) return;
            lastFrame = motion.Locomotion.FrameId;
            ground.Tick(in motion);
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
            playback.Tick(dt);
            for (int i = 0; i < clips.Length; i++)
            {
                if (!clips[i].IsValid()) continue;
                clips[i].SetTime(playback.ClipTime(i));
                actions.SetInputWeight(i + 1, layerWeights[i] * Mathf.Clamp01(playback.Weight(i)));
            }
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (fullBodyMask != null) UnityEngine.Object.Destroy(fullBodyMask);
        }
    }
}
