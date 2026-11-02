using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class AnimationGraphRuntime : IDisposable
    {
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private float weight;
        private int frame=-1;
        public float MovementWeight => weight;
        public AnimationGraphRuntime(Animator animator,AnimationClip idleClip,AnimationClip movingClip)
        {
            if (animator == null || idleClip == null || movingClip == null) throw new ArgumentException("Missing animation graph inputs.");
            graph=PlayableGraph.Create("PortableAnimation.Phase1");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer=AnimationMixerPlayable.Create(graph,2);
                var idle=AnimationClipPlayable.Create(graph,idleClip);
                var move=AnimationClipPlayable.Create(graph,movingClip);
                if(!graph.Connect(idle,0,mixer,0)||!graph.Connect(move,0,mixer,1)) throw new InvalidOperationException("Mixer connection failed.");
                mixer.SetInputWeight(0,1f);
                mixer.SetInputWeight(1,0f);
                AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(mixer);
                graph.Play();graph.Evaluate(0f);
            }
            catch {Dispose();throw;}
        }
        public void Evaluate(in AnimationFrameState state,float rate)
        {
            if(!graph.IsValid())throw new ObjectDisposedException(nameof(AnimationGraphRuntime));
            if(state.FrameId==frame)return;
            frame=state.FrameId;
            weight=Mathf.MoveTowards(weight,state.NormalizedSpeed,Mathf.Max(0f,rate)*state.DeltaTime);
            mixer.SetInputWeight(0,1f-weight);mixer.SetInputWeight(1,weight);
            graph.Evaluate(state.DeltaTime);
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();}
    }
}
