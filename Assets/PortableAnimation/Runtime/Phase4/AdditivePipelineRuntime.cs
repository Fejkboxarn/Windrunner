using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public readonly struct AdditivePipelineFrame
    {
        public readonly AnimationFrameState Locomotion;
        public readonly float RightOverride,LeftAdditive,TorsoAdditive,HeadAdditive;
        public readonly AdditiveRotationMode HeadMode;
        public AdditivePipelineFrame(AnimationFrameState loc,float right,float left,float torso,float head,AdditiveRotationMode headMode)
        {Locomotion=loc;RightOverride=right;LeftAdditive=left;TorsoAdditive=torso;HeadAdditive=head;HeadMode=headMode;}
    }
    public sealed class AdditivePipelineRuntime:IDisposable
    {
        private PlayableGraph graph;
        private ClipLocomotionSource ground,aerial;
        private AnimationMixerPlayable selector;
        private PerBoneMixerStage right;
        private readonly List<AdditivePoseStage> stages=new List<AdditivePoseStage>();
        private readonly float modeRate;
        private float aeroWeight;
        private int lastFrame=-1;
        public AdditivePipelineRuntime(Animator animator,AdditivePipelineProfile config)
        {
            config.AssertValid();
            var old=config.Phase3Profile;
            var loc=old.Locomotion;modeRate=loc.LocomotionSwitchRate;
            graph=PlayableGraph.Create("PortableAnimation.AdditivePipeline");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ground=new ClipLocomotionSource(graph,loc.GroundIdle,loc.GroundMoving,loc.GroundBlendRate);
                aerial=new ClipLocomotionSource(graph,loc.AerialHover,loc.AerialTravel,loc.AerialBlendRate);
                selector=AnimationMixerPlayable.Create(graph,2);
                graph.Connect(ground.Output,0,selector,0);graph.Connect(aerial.Output,0,selector,1);
                selector.SetInputWeight(0,1);selector.SetInputWeight(1,0);
                right=new PerBoneMixerStage(graph,animator,"RightOverride",selector,old.ActionPose,old.ActionMask,old.ActionBlendRate);
                Playable pose=right.Output;
                foreach(var definition in config.Stages)
                {var stage=new AdditivePoseStage(graph,animator,pose,definition);stages.Add(stage);pose=stage.Output;}
                AnimationPlayableOutput.Create(graph,"AdditivePose",animator).SetSourcePlayable(pose);
                graph.Play();graph.Evaluate(0f);
            }
            catch{Dispose();throw;}
        }
        public void Evaluate(in AdditivePipelineFrame state)
        {
            if(state.Locomotion.FrameId==lastFrame)return;lastFrame=state.Locomotion.FrameId;
            float dt=state.Locomotion.DeltaTime;
            ground.Tick(state.Locomotion.NormalizedSpeed,dt);aerial.Tick(state.Locomotion.NormalizedSpeed,dt);
            aeroWeight=Mathf.MoveTowards(aeroWeight,state.Locomotion.Mode==LocomotionMode.Grounded?0f:1f,modeRate*dt);
            selector.SetInputWeight(0,1-aeroWeight);selector.SetInputWeight(1,aeroWeight);
            right.Tick(state.RightOverride,dt);
            foreach(var stage in stages)
            {
                float intensity=stage.Role==AdditiveStageRole.LeftArm?state.LeftAdditive:stage.Role==AdditiveStageRole.Torso?state.TorsoAdditive:state.HeadAdditive;
                if(stage.Role==AdditiveStageRole.Head)stage.SetRotationMode(state.HeadMode);
                stage.Tick(intensity,dt);
            }
            graph.Evaluate(dt);
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();foreach(var s in stages)s.Dispose();stages.Clear();right?.Dispose();}
    }
}
