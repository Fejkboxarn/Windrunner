using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class PerBonePipelineRuntime:IDisposable
    {
        public const string RightArmPath="MotionRoot/AnimatedBody/RightArmPivot";
        public const string LeftArmPath="MotionRoot/AnimatedBody/LeftArmPivot";
        public const string HeadPath="MotionRoot/AnimatedBody/HeadPivot";
        private PlayableGraph graph;
        private ClipLocomotionSource ground,aerial;
        private AnimationMixerPlayable selector;
        private PerBoneMixerStage action,reaction;
        private float aerialWeight;
        private readonly float modeBlendRate;
        private int lastFrame=-1;
        public float ActionWeight=>action?.Weight??0;
        public float ReactionWeight=>reaction?.Weight??0;
        public float AerialWeight=>aerialWeight;
        public PerBonePipelineRuntime(Animator animator,PerBonePipelineProfile config)
        {
            config.AssertValid();modeBlendRate=config.Locomotion.LocomotionSwitchRate;
            graph=PlayableGraph.Create("PortableAnimation.PerBonePipeline");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var loc=config.Locomotion;
                ground=new ClipLocomotionSource(graph,loc.GroundIdle,loc.GroundMoving,loc.GroundBlendRate);
                aerial=new ClipLocomotionSource(graph,loc.AerialHover,loc.AerialTravel,loc.AerialBlendRate);
                selector=AnimationMixerPlayable.Create(graph,2);
                graph.Connect(ground.Output,0,selector,0);graph.Connect(aerial.Output,0,selector,1);
                selector.SetInputWeight(0,1);selector.SetInputWeight(1,0);
                action=new PerBoneMixerStage(graph,animator,"Action",selector,config.ActionPose,config.ActionMask,config.ActionBlendRate);
                reaction=new PerBoneMixerStage(graph,animator,"Reaction",action.Output,config.ReactionPose,config.ReactionMask,config.ReactionBlendRate);
                AnimationPlayableOutput.Create(graph,"PerBonePose",animator).SetSourcePlayable(reaction.Output);
                graph.Play();graph.Evaluate(0f);
            }
            catch{Dispose();throw;}
        }
        public void Evaluate(in PoseFrameState state)
        {
            if(state.Locomotion.FrameId==lastFrame)return;lastFrame=state.Locomotion.FrameId;
            float dt=state.Locomotion.DeltaTime;
            ground.Tick(state.Locomotion.NormalizedSpeed,dt);aerial.Tick(state.Locomotion.NormalizedSpeed,dt);
            aerialWeight=Mathf.MoveTowards(aerialWeight,state.Locomotion.Mode==LocomotionMode.Grounded?0f:1f,modeBlendRate*dt);
            selector.SetInputWeight(0,1-aerialWeight);selector.SetInputWeight(1,aerialWeight);
            action.Tick(state.AllowAction?state.ActionWeight:0f,dt);reaction.Tick(state.AllowReaction?state.ReactionWeight:0f,dt);
            graph.Evaluate(dt);
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();reaction?.Dispose();action?.Dispose();}
    }
}
