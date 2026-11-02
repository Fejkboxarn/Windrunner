using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// Intentionally minimal Humanoid import/control reference: one native 2-input
    /// mixer, no custom animation jobs, no AnimatorController, no root motion.
    /// This isolates conversion/retargeting defects from the integrated pipeline.
    /// </summary>
    public sealed class Phase16HumanoidClipSession : IDisposable
    {
        private readonly Animator animator;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable first;
        private AnimationClipPlayable second;
        private float blend;
        private AnimationClip clipA;
        private AnimationClip clipB;

        public bool IsValid => graph.IsValid();
        public float Blend => blend;
        public string FirstName => clipA == null ? "None" : clipA.name;
        public string SecondName => clipB == null ? "None" : clipB.name;

        public Phase16HumanoidClipSession(Animator animator)
        {
            this.animator = animator != null ? animator :
                throw new ArgumentNullException(nameof(animator));
            if (animator.avatar == null || !animator.avatar.isValid ||
                !animator.avatar.isHuman || !animator.isHuman)
                throw new InvalidOperationException("Valid Humanoid Avatar required.");
            if (animator.runtimeAnimatorController != null)
                throw new InvalidOperationException("Remove Animator Controller for Playables test.");
            animator.applyRootMotion = false;
        }

        public void Play(AnimationClip a, AnimationClip b = null, float weight = 0f)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (!a.humanMotion) throw new InvalidOperationException(
                "Clip is not Humanoid: " + a.name);
            if (b != null && !b.humanMotion)
                throw new InvalidOperationException("Second clip is not Humanoid: " + b.name);

            Dispose();
            clipA = a;
            clipB = b ?? a;
            graph = PlayableGraph.Create("PortableAnimation.PriestessClipReference");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                first = AnimationClipPlayable.Create(graph, clipA);
                second = AnimationClipPlayable.Create(graph, clipB);
                first.SetApplyFootIK(false);
                second.SetApplyFootIK(false);
                if (!graph.Connect(first, 0, mixer, 0) ||
                    !graph.Connect(second, 0, mixer, 1))
                    throw new InvalidOperationException("Couldn't connect native clip mixer.");
                var output = AnimationPlayableOutput.Create(graph, "HumanoidReference", animator);
                output.SetSourcePlayable(mixer);
                SetBlend(weight);
                graph.Play();
                graph.Evaluate(0f);
            }
            catch { Dispose(); throw; }
        }

        public void SetBlend(float alpha)
        {
            blend = Mathf.Clamp01(alpha);
            if (!graph.IsValid()) return;
            mixer.SetInputWeight(0, 1f - blend);
            mixer.SetInputWeight(1, blend);
        }

        public void Evaluate(float dt)
        {
            if (graph.IsValid()) graph.Evaluate(Mathf.Clamp(dt, 0f, 0.1f));
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
