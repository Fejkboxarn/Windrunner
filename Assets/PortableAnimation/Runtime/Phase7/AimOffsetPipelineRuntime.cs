using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class AimOffsetPipelineRuntime:IDisposable
    {
        private readonly struct Bound
        {
            public readonly string Path;public readonly AbilityRegion Region;
            public Bound(string path,AbilityRegion region){Path=path;Region=region;}
        }
        private PlayableGraph graph;
        private ClipLocomotionSource locomotion;
        private readonly List<ControllableRegionStage> actions=new List<ControllableRegionStage>();
        private AimOffsetPoseStage aim;
        private readonly ActionMontageController montage;
        private readonly ActionMontageProfile montageProfile;
        private readonly Bound[] bones;
        private readonly float[] p,r,s,mp,mr,ms;
        private int lastFrame=-1;
        public bool IsValid=>graph.IsValid();
        public AimOffsetSource SelectedSource=>aim.SelectedSource;
        public float AimStrength=>aim.Strength;
        public float CurrentYaw=>aim.CurrentYaw;
        public float CurrentPitch=>aim.CurrentPitch;
        public int ActiveActionIndex=>montage.ActiveIndex;
        public string ActiveActionName=>montage.GetName(montage.ActiveIndex);
        public event Action<MontageSignal> SignalRaised{add=>montage.SignalRaised+=value;remove=>montage.SignalRaised-=value;}
        public AimOffsetPipelineRuntime(Animator animator,AimOffsetProfile profile)
        {
            profile.AssertValid();montageProfile=profile.MontageProfile;montage=new ActionMontageController(montageProfile);
            var all=animator.GetComponentsInChildren<Transform>(true);
            bones=new Bound[all.Length-1];
            for(int i=0;i<bones.Length;i++){string path=Relative(animator.transform,all[i+1]);bones[i]=new Bound(path,montageProfile.BaseProfile.RegionMap.Resolve(path));}
            int n=montageProfile.Actions.Count;
            p=new float[n];r=new float[n];s=new float[n];mp=new float[n];mr=new float[n];ms=new float[n];
            graph=PlayableGraph.Create("PortableAnimation.AimOffset");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var source=montageProfile.BaseProfile;
                locomotion=new ClipLocomotionSource(graph,source.Idle,source.Moving,source.LocomotionBlendRate);
                Playable input=locomotion.Output;
                foreach(var action in montageProfile.Actions)
                {
                    var stage=new ControllableRegionStage(graph,animator,input,action.Clip);
                    actions.Add(stage);input=stage.Output;
                }
                aim=new AimOffsetPoseStage(graph,animator,input,profile);
                AnimationPlayableOutput.Create(graph,"FinalPose",animator).SetSourcePlayable(aim.Output);
                graph.Play();graph.Evaluate(0f);
            }
            catch{Dispose();throw;}
        }
        public MontageRequestResult RequestAction(int index)=>montage.Request(index);
        public void CancelAction()=>montage.Cancel();
        public void Evaluate(in AimOffsetFrame frame)
        {
            if(!graph.IsValid())throw new ObjectDisposedException(nameof(AimOffsetPipelineRuntime));
            if(frame.Locomotion.FrameId==lastFrame)return;
            lastFrame=frame.Locomotion.FrameId;
            float dt=frame.Locomotion.DeltaTime;
            locomotion.Tick(frame.Locomotion.NormalizedSpeed,dt);
            montage.Advance(dt);
            bool active=montage.ActiveIndex>=0;
            for(int i=0;i<actions.Count;i++)
            {actions[i].Sample(montage.GetClipTime(i));if(montage.GetWeight(i)>0.001f)active=true;}
            for(int b=0;b<bones.Length;b++)
            {
                for(int i=0;i<actions.Count;i++)
                {
                    var w=montageProfile.Actions[i].Policy.Resolve(bones[b].Region);
                    float alpha=montage.GetWeight(i);
                    p[i]=alpha*w.position;r[i]=alpha*w.rotation;s[i]=alpha*w.scale;
                }
                AbilityBlendMath.NormalizeSequential(p,mp);
                AbilityBlendMath.NormalizeSequential(r,mr);
                AbilityBlendMath.NormalizeSequential(s,ms);
                for(int i=0;i<actions.Count;i++)actions[i].SetBoneWeights(bones[b].Path,new BoneChannelWeights(mp[i],mr[i],ms[i]));
            }
            aim.Tick(in frame,active);
            graph.Evaluate(dt);
        }
        public void Dispose()
        {
            if(graph.IsValid())graph.Destroy();
            aim?.Dispose();aim=null;
            foreach(var stage in actions)stage.Dispose();actions.Clear();
        }
        private static string Relative(Transform root,Transform t)
        {var parts=new Stack<string>();for(;t!=null&&t!=root;t=t.parent)parts.Push(t.name);return string.Join("/",parts.ToArray());}
    }
}
