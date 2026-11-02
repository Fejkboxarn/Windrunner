using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class ModularPoseGraphRuntime : IDisposable
    {
        private PlayableGraph graph;
        private ClipLocomotionSource ground,aerial;
        private AnimationMixerPlayable selector;
        private AnimationMixerPlayable[] overlays;
        private AnimationClipPlayable[] overlayClips;
        private PosePipelineProfile config;
        private float[] weights;
        private float aerialWeight;
        private int lastFrame=-1;
        public float AerialWeight=>aerialWeight;
        public bool IsValid=>graph.IsValid();
        public ModularPoseGraphRuntime(Animator animator,PosePipelineProfile profile)
        {
            config=profile;
            profile.AssertValid();
            graph=PlayableGraph.Create("PortableAnimation.ModularPose");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ground=new ClipLocomotionSource(graph,profile.GroundIdle,profile.GroundMoving,profile.GroundBlendRate);
                aerial=new ClipLocomotionSource(graph,profile.AerialHover,profile.AerialTravel,profile.AerialBlendRate);
                selector=AnimationMixerPlayable.Create(graph,2);
                graph.Connect(ground.Output,0,selector,0);graph.Connect(aerial.Output,0,selector,1);
                selector.SetInputWeight(0,1f);selector.SetInputWeight(1,0f);
                overlays=new AnimationMixerPlayable[profile.Processors.Count];
                overlayClips=new AnimationClipPlayable[overlays.Length];
                weights=new float[overlays.Length];
                Playable current=selector;
                for(int i=0;i<overlays.Length;i++)
                {
                    overlays[i]=AnimationMixerPlayable.Create(graph,2);
                    overlayClips[i]=AnimationClipPlayable.Create(graph,profile.Processors[i].Clip);
                    graph.Connect(current,0,overlays[i],0);graph.Connect(overlayClips[i],0,overlays[i],1);
                    overlays[i].SetInputWeight(0,1f);overlays[i].SetInputWeight(1,0f);current=overlays[i];
                }
                AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(current);
                graph.Play();graph.Evaluate(0f);
            }
            catch{Dispose();throw;}
        }
        public void Evaluate(in PoseFrameState state)
        {
            if(state.Locomotion.FrameId==lastFrame)return;
            lastFrame=state.Locomotion.FrameId;
            float dt=state.Locomotion.DeltaTime;
            ground.Tick(state.Locomotion.NormalizedSpeed,dt);aerial.Tick(state.Locomotion.NormalizedSpeed,dt);
            aerialWeight=Mathf.MoveTowards(aerialWeight,state.Locomotion.Mode==LocomotionMode.Grounded?0f:1f,config.LocomotionSwitchRate*dt);
            selector.SetInputWeight(0,1-aerialWeight);selector.SetInputWeight(1,aerialWeight);
            for(int i=0;i<overlays.Length;i++)
            {
                var definition=config.Processors[i];
                float target=definition.EnabledByDefault?(definition.Signal==PoseOverlaySignal.Action?(state.AllowAction?state.ActionWeight:0):(state.AllowReaction?state.ReactionWeight:0)):0;
                weights[i]=Mathf.MoveTowards(weights[i],target,definition.BlendRate*dt);
                overlays[i].SetInputWeight(0,1-weights[i]);overlays[i].SetInputWeight(1,weights[i]);
            }
            graph.Evaluate(dt);
        }
        public float GetProcessorWeight(PoseOverlaySignal signal)
        {for(int i=0;i<weights.Length;i++)if(config.Processors[i].Signal==signal)return weights[i];return 0f;}
        public void Dispose(){if(graph.IsValid())graph.Destroy();}
    }
}
