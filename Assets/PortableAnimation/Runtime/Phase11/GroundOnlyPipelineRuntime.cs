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
        private AnimationLayerMixerPlayable layers;
        private AnimationMixerPlayable actions;
        private AnimationClipPlayable[] clips;
        private int activeIndex = -1;
        private float actionTime;
        private float actionWeight;
        private int lastFrame = -1;

        public bool IsValid => graph.IsValid();
        public int LiveActionCount => actionProfile != null ? actionProfile.Count : 0;
        public string ActiveAction => activeIndex < 0 ? "None" :
            actionProfile.Action(activeIndex).id;
        public float IdleBlend => ground.IdleBlend;
        public float WalkBlend => ground.WalkBlend;
        public float RunBlend => ground.RunBlend;
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
                    actions = AnimationMixerPlayable.Create(graph, LiveActionCount);
                    clips = new AnimationClipPlayable[LiveActionCount];
                    for (int i = 0; i < LiveActionCount; i++)
                    {
                        clips[i] = AnimationClipPlayable.Create(graph,
                            actionProfile.Action(i).clip);
                        clips[i].SetSpeed(0d);
                        clips[i].SetTime(0d);
                        if (!graph.Connect(clips[i], 0, actions, i))
                            throw new InvalidOperationException(
                                "Action layer could not connect clip " + i);
                        actions.SetInputWeight(i, 0f);
                    }
                    layers = AnimationLayerMixerPlayable.Create(graph, 2);
                    if (!graph.Connect(ground.Output, 0, layers, 0) ||
                        !graph.Connect(actions, 0, layers, 1))
                        throw new InvalidOperationException(
                            "Standalone character layers could not connect.");
                    layers.SetInputWeight(0, 1f);
                    layers.SetInputWeight(1, 0f);
                    layers.SetLayerAdditive(1, false);
                    finalPose = layers;
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
            PortableCharacterAction next = actionProfile.Action(index);
            if (activeIndex >= 0)
            {
                PortableCharacterAction previous = actionProfile.Action(activeIndex);
                if (previous.priority > next.priority ||
                    (index == activeIndex && !next.allowSameActionRestart))
                    return false;
            }

            activeIndex = index;
            actionTime = 0f;
            actionWeight = 0f;
            for (int i = 0; i < clips.Length; i++)
                actions.SetInputWeight(i, i == index ? 1f : 0f);
            clips[index].SetTime(0d);
            if (next.mask != null)
                layers.SetLayerMaskFromAvatarMask(1, next.mask);
            else
                layers.SetLayerMaskFromAvatarMask(1, fullBodyMask);
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

        private void UpdateAction(float dt)
        {
            if (activeIndex < 0) return;
            PortableCharacterAction action = actionProfile.Action(activeIndex);
            actionTime = Mathf.Max(0f, actionTime + dt);
            float length = action.clip.length;
            if (actionTime >= length)
            {
                activeIndex = -1;
                actionWeight = 0f;
                layers.SetInputWeight(1, 0f);
                return;
            }
            float fadeIn = Mathf.Clamp01(actionTime / action.blendInSeconds);
            float fadeOut = Mathf.Clamp01((length - actionTime) / action.blendOutSeconds);
            actionWeight = Mathf.Min(fadeIn, fadeOut);
            clips[activeIndex].SetTime(actionTime);
            layers.SetInputWeight(1, actionWeight);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (fullBodyMask != null) UnityEngine.Object.Destroy(fullBodyMask);
        }
    }
}
