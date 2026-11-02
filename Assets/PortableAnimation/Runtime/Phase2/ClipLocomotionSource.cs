using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public interface ILocomotionPoseSource
    {
        Playable Output{get;}
        float Weight{get;}
        void Tick(float speed,float deltaTime);
    }
    public sealed class ClipLocomotionSource : ILocomotionPoseSource
    {
        private readonly AnimationMixerPlayable mixer;
        private readonly float rate;
        private float weight;
        public Playable Output=>mixer;
        public float Weight=>weight;
        public ClipLocomotionSource(PlayableGraph graph,AnimationClip restingClip,AnimationClip movingClip,float rate)
        {
            if(restingClip==null||movingClip==null)throw new ArgumentNullException("locomotion clips");
            this.rate=Mathf.Max(0,rate);
            mixer=AnimationMixerPlayable.Create(graph,2);
            var a=AnimationClipPlayable.Create(graph,restingClip);
            var b=AnimationClipPlayable.Create(graph,movingClip);
            if(!graph.Connect(a,0,mixer,0)||!graph.Connect(b,0,mixer,1))throw new InvalidOperationException("Failed to connect locomotion mixer.");
            mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,0);
        }
        public void Tick(float speed,float deltaTime)
        {
            weight=Mathf.MoveTowards(weight,Mathf.Clamp01(speed),rate*Mathf.Max(0,deltaTime));
            mixer.SetInputWeight(0,1-weight);mixer.SetInputWeight(1,weight);
        }
    }
}
