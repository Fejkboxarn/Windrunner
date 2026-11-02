using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class ActionMontageRuntime:IDisposable
    {
        private PlayableGraph graph;
        private ClipLocomotionSource locomotion;
        private readonly List<ControllableRegionStage> stages=new List<ControllableRegionStage>();
        private readonly ActionMontageProfile profile;
        private readonly ActionMontageController montage;
        private readonly string[] paths;
        private readonly AbilityRegion[] regions;
        private readonly float[] p,r,s,mp,mr,ms;
        private int lastFrame=-1;
        public event Action<MontageSignal> SignalRaised{add=>montage.SignalRaised+=value;remove=>montage.SignalRaised-=value;}
        public int ActiveIndex=>montage.ActiveIndex;
        public int QueuedIndex=>montage.QueuedIndex;
        public ActionMontageRuntime(Animator animator,ActionMontageProfile profile)
        {
            profile.AssertValid();this.profile=profile;montage=new ActionMontageController(profile);
            var bones=animator.GetComponentsInChildren<Transform>(true);
            paths=new string[bones.Length-1];regions=new AbilityRegion[paths.Length];
            for(int i=0;i<paths.Length;i++){paths[i]=Relative(animator.transform,bones[i+1]);regions[i]=HumanoidAbilityRegionResolver.Resolve(animator,bones[i+1],paths[i],profile.BaseProfile.RegionMap);}
            int count=profile.Actions.Count;p=new float[count];r=new float[count];s=new float[count];mp=new float[count];mr=new float[count];ms=new float[count];
            graph=PlayableGraph.Create("PortableAnimation.Montage");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baseProfile=profile.BaseProfile;
                locomotion=new ClipLocomotionSource(graph,baseProfile.Idle,baseProfile.Moving,baseProfile.LocomotionBlendRate);
                Playable input=locomotion.Output;
                foreach(var entry in profile.Actions){var stage=new ControllableRegionStage(graph,animator,input,entry.Clip);stages.Add(stage);input=stage.Output;}
                AnimationPlayableOutput.Create(graph,"MontagePose",animator).SetSourcePlayable(input);
                graph.Play();graph.Evaluate(0f);
            }
            catch{Dispose();throw;}
        }
        public MontageRequestResult Request(int index)=>montage.Request(index);
        public void Cancel()=>montage.Cancel();
        public float GetActionWeight(int index)=>montage.GetWeight(index);
        public void Evaluate(in AnimationFrameState state)
        {
            if(state.FrameId==lastFrame)return;lastFrame=state.FrameId;
            locomotion.Tick(state.NormalizedSpeed,state.DeltaTime);montage.Advance(state.DeltaTime);
            for(int i=0;i<stages.Count;i++)stages[i].Sample(montage.GetClipTime(i));
            for(int b=0;b<paths.Length;b++)
            {
                for(int i=0;i<stages.Count;i++)
                {var policy=profile.Actions[i].Policy.Resolve(regions[b]);float alpha=montage.GetWeight(i);p[i]=policy.position*alpha;r[i]=policy.rotation*alpha;s[i]=policy.scale*alpha;}
                AbilityBlendMath.NormalizeSequential(p,mp);AbilityBlendMath.NormalizeSequential(r,mr);AbilityBlendMath.NormalizeSequential(s,ms);
                for(int i=0;i<stages.Count;i++)stages[i].SetBoneWeights(paths[b],new BoneChannelWeights(mp[i],mr[i],ms[i]));
            }
            graph.Evaluate(state.DeltaTime);
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();foreach(var stage in stages)stage.Dispose();stages.Clear();}
        private static string Relative(Transform root,Transform node){var s=new Stack<string>();for(;node!=null&&node!=root;node=node.parent)s.Push(node.name);return string.Join("/",s.ToArray());}
    }
}
