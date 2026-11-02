using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class ControllableRegionStage:IDisposable
    {
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<float> p,r,s;
        private readonly Dictionary<string,int> indices=new Dictionary<string,int>();
        private AnimationScriptPlayable mixer;
        private AnimationClipPlayable clipPlayable;
        private double clipLength;
        private bool valid;
        public Playable Output=>mixer;
        public ControllableRegionStage(PlayableGraph graph,Animator animator,Playable incoming,AnimationClip clip)
        {
            if(animator==null||clip==null||clip.length<=0f)throw new ArgumentException("Controllable stage missing clip/rig.");
            clipLength=clip.length;
            try
            {
                Transform root=animator.transform;
                var all=root.GetComponentsInChildren<Transform>(true);
                int count=all.Length-1;
                handles=new NativeArray<TransformStreamHandle>(count,Allocator.Persistent);
                p=new NativeArray<float>(count,Allocator.Persistent);
                r=new NativeArray<float>(count,Allocator.Persistent);
                s=new NativeArray<float>(count,Allocator.Persistent);
                for(int i=0;i<count;i++)
                {var bone=all[i+1];indices.Add(Relative(root,bone),i);handles[i]=animator.BindStreamTransform(bone);}
                var job=new PerBoneMixJob{handles=handles,positionWeights=p,rotationWeights=r,scaleWeights=s,strength=1f};
                mixer=AnimationScriptPlayable.Create(graph,job,2);
                mixer.SetProcessInputs(false);
                clipPlayable=AnimationClipPlayable.Create(graph,clip);
                clipPlayable.SetSpeed(0d);clipPlayable.SetTime(0d);
                if(!graph.Connect(incoming,0,mixer,0)||!graph.Connect(clipPlayable,0,mixer,1))throw new InvalidOperationException("Montage region stage connect failed.");
                mixer.SetInputWeight(0,1f);mixer.SetInputWeight(1,1f);valid=true;
            }
            catch{Dispose();throw;}
        }
        public void Sample(double seconds)
        {if(valid)clipPlayable.SetTime(Math.Max(0d,Math.Min(seconds,Math.Max(0d,clipLength-0.0001d))));}
        public bool SetBoneWeights(string path,BoneChannelWeights weights)
        {if(!valid||!indices.TryGetValue(path,out int i))return false;p[i]=weights.position;r[i]=weights.rotation;s[i]=weights.scale;return true;}
        public void Dispose()
        {valid=false;if(handles.IsCreated)handles.Dispose();if(p.IsCreated)p.Dispose();if(r.IsCreated)r.Dispose();if(s.IsCreated)s.Dispose();indices.Clear();}
        private static string Relative(Transform root,Transform target)
        {var stack=new Stack<string>();for(var t=target;t!=null&&t!=root;t=t.parent)stack.Push(t.name);return string.Join("/",stack.ToArray());}
    }
}
