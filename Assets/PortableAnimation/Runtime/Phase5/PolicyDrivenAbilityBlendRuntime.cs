using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class PolicyDrivenAbilityBlendRuntime:IDisposable
    {
        private PlayableGraph graph;
        private ClipLocomotionSource locomotion;
        private readonly List<PerBoneMixerStage> stages=new List<PerBoneMixerStage>();
        private readonly List<BoneWeightMask> generatedMasks=new List<BoneWeightMask>();
        private readonly AbilityBlendProfile profile;
        private readonly AbilityWeightController controller;
        private readonly string[] paths;
        private readonly AbilityRegion[] regions;
        private readonly float[] p,r,s,mp,mr,ms;
        private int lastFrame=-1;
        public PolicyDrivenAbilityBlendRuntime(Animator animator,AbilityBlendProfile profile)
        {
            profile.AssertValid();this.profile=profile;
            int count=profile.Abilities.Count;
            controller=new AbilityWeightController(count);
            var bones=animator.GetComponentsInChildren<Transform>(true);
            paths=new string[bones.Length-1];regions=new AbilityRegion[paths.Length];
            for(int i=0;i<paths.Length;i++){paths[i]=Relative(animator.transform,bones[i+1]);regions[i]=profile.RegionMap.Resolve(paths[i]);}
            p=new float[count];r=new float[count];s=new float[count];mp=new float[count];mr=new float[count];ms=new float[count];
            graph=PlayableGraph.Create("PortableAnimation.PolicyAbilityBlend");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                locomotion=new ClipLocomotionSource(graph,profile.Idle,profile.Moving,profile.LocomotionBlendRate);
                Playable input=locomotion.Output;
                foreach(var policy in profile.Abilities)
                {
                    var mask=ScriptableObject.CreateInstance<BoneWeightMask>();mask.SetRules();generatedMasks.Add(mask);
                    var stage=new PerBoneMixerStage(graph,animator,policy.PolicyId,input,policy.Clip,mask,1000f);
                    stage.Tick(1f,1f);stages.Add(stage);input=stage.Output;
                }
                AnimationPlayableOutput.Create(graph,"AbilityPose",animator).SetSourcePlayable(input);
                graph.Play();graph.Evaluate(0f);
            }
            catch{Dispose();throw;}
        }
        public void RequestAbility(int index)
        {
            if(index==controller.TargetIndex)return;
            int outgoing=controller.GetDominantIndex();
            float duration=index>=0?profile.Abilities[index].BlendInSeconds:0f;
            if(outgoing>=0)duration=Mathf.Max(duration,profile.Abilities[outgoing].BlendOutSeconds);
            controller.Request(index,duration);
        }
        public void ClearAbility()=>RequestAbility(-1);
        public float GetAbilityWeight(int index)=>controller.GetWeight(index);
        public void Evaluate(in AnimationFrameState state)
        {
            if(state.FrameId==lastFrame)return;lastFrame=state.FrameId;
            locomotion.Tick(state.NormalizedSpeed,state.DeltaTime);controller.Advance(state.DeltaTime);
            for(int b=0;b<paths.Length;b++)
            {
                for(int i=0;i<stages.Count;i++)
                {
                    var weights=profile.Abilities[i].Resolve(regions[b]);float alpha=controller.GetWeight(i);
                    p[i]=alpha*weights.position;r[i]=alpha*weights.rotation;s[i]=alpha*weights.scale;
                }
                AbilityBlendMath.NormalizeSequential(p,mp);AbilityBlendMath.NormalizeSequential(r,mr);AbilityBlendMath.NormalizeSequential(s,ms);
                for(int i=0;i<stages.Count;i++)stages[i].TrySetBoneWeights(paths[b],new BoneChannelWeights(mp[i],mr[i],ms[i]));
            }
            graph.Evaluate(state.DeltaTime);
        }
        public void Dispose()
        {
            if(graph.IsValid())graph.Destroy();foreach(var stage in stages)stage.Dispose();stages.Clear();
            foreach(var mask in generatedMasks)if(mask)UnityEngine.Object.Destroy(mask);generatedMasks.Clear();
        }
        private static string Relative(Transform root,Transform node)
        {var stack=new Stack<string>();for(;node!=null&&node!=root;node=node.parent)stack.Push(node.name);return string.Join("/",stack.ToArray());}
    }
}
