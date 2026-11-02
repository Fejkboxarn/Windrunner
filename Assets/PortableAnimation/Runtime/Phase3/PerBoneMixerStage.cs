using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class PerBoneMixerStage : IDisposable
    {
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<float> p,r,s;
        private readonly Dictionary<string,int> indices=new Dictionary<string,int>(StringComparer.Ordinal);
        private AnimationScriptPlayable playable;
        private float strength,rate;
        private bool attached;
        public Playable Output=>playable;
        public float Weight=>strength;
        public int BoneCount=>indices.Count;
        public PerBoneMixerStage(PlayableGraph graph,Animator animator,string name,Playable incomingPose,AnimationClip clip,BoneWeightMask mask,float blendRate)
        {
            if(animator==null||clip==null||mask==null)throw new ArgumentException("Per-bone stage is missing references.");
            rate=Mathf.Max(0f,blendRate);
            try
            {
                var transforms=animator.GetComponentsInChildren<Transform>(true);
                int count=transforms.Length-1;
                handles=new NativeArray<TransformStreamHandle>(count,Allocator.Persistent);
                p=new NativeArray<float>(count,Allocator.Persistent);
                r=new NativeArray<float>(count,Allocator.Persistent);
                s=new NativeArray<float>(count,Allocator.Persistent);
                for(int i=0;i<count;i++)
                {
                    string path=Relative(animator.transform,transforms[i+1]);
                    indices.Add(path,i);
                    handles[i]=animator.BindStreamTransform(transforms[i+1]);
                    var weights=mask.Resolve(path);
                    p[i]=weights.position;r[i]=weights.rotation;s[i]=weights.scale;
                }
                var job=new PerBoneMixJob{handles=handles,positionWeights=p,rotationWeights=r,scaleWeights=s,strength=0f};
                playable=AnimationScriptPlayable.Create(graph,job,2);
                playable.SetProcessInputs(false);
                var overlay=AnimationClipPlayable.Create(graph,clip);
                if(!graph.Connect(incomingPose,0,playable,0)||!graph.Connect(overlay,0,playable,1))throw new InvalidOperationException("Cannot connect per-bone stage.");
                playable.SetInputWeight(0,1);playable.SetInputWeight(1,1);attached=true;
            }
            catch{Dispose();throw;}
        }
        public bool TrySetBoneWeights(string path,BoneChannelWeights weights)
        {
            if(!attached||!indices.TryGetValue(path,out int index))return false;
            p[index]=weights.position;r[index]=weights.rotation;s[index]=weights.scale;return true;
        }
        public void Tick(float requestedStrength,float dt)
        {
            if(!attached)return;
            strength=Mathf.MoveTowards(strength,Mathf.Clamp01(requestedStrength),rate*Mathf.Max(0f,dt));
            var job=playable.GetJobData<PerBoneMixJob>();job.strength=strength;playable.SetJobData(job);
        }
        public void Dispose()
        {
            attached=false;
            if(handles.IsCreated)handles.Dispose();if(p.IsCreated)p.Dispose();if(r.IsCreated)r.Dispose();if(s.IsCreated)s.Dispose();indices.Clear();
        }
        private static string Relative(Transform root,Transform target)
        {
            var names=new Stack<string>();
            for(var x=target;x!=null&&x!=root;x=x.parent)names.Push(x.name);
            return string.Join("/",names.ToArray());
        }
    }
}
