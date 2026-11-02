using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class AdditivePoseStage:IDisposable
    {
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<int> parentIndices;
        private NativeArray<float> positions,rotations,scales;
        private NativeArray<Quaternion> oc,rc,bc;
        private readonly Dictionary<string,int> lookup=new Dictionary<string,int>();
        private AnimationScriptPlayable playable;
        private float weight;
        private readonly float rate;
        private bool valid;
        public AdditiveStageRole Role{get;}
        public Playable Output=>playable;
        public float Weight=>weight;
        public AdditivePoseStage(PlayableGraph graph,Animator animator,Playable incoming,AdditiveStageDefinition def)
        {
            def.AssertValid();Role=def.Role;rate=Mathf.Max(0,def.BlendRate);
            try
            {
                Transform root=animator.transform;
                Transform[] children=root.GetComponentsInChildren<Transform>(true);
                int count=children.Length-1;
                handles=new NativeArray<TransformStreamHandle>(count,Allocator.Persistent);
                parentIndices=new NativeArray<int>(count,Allocator.Persistent);
                positions=new NativeArray<float>(count,Allocator.Persistent);
                rotations=new NativeArray<float>(count,Allocator.Persistent);
                scales=new NativeArray<float>(count,Allocator.Persistent);
                oc=new NativeArray<Quaternion>(count,Allocator.Persistent);
                rc=new NativeArray<Quaternion>(count,Allocator.Persistent);
                bc=new NativeArray<Quaternion>(count,Allocator.Persistent);
                var indexByTransform=new Dictionary<Transform,int>();
                for(int i=0;i<count;i++)
                {
                    Transform bone=children[i+1];
                    string path=PathOf(root,bone);
                    lookup.Add(path,i);handles[i]=animator.BindStreamTransform(bone);
                    parentIndices[i]=bone.parent==root?-1:indexByTransform[bone.parent];
                    indexByTransform.Add(bone,i);
                    var weights=def.BoneMask.Resolve(path);
                    positions[i]=weights.position;rotations[i]=weights.rotation;scales[i]=weights.scale;
                }
                var job=new AdditivePoseJob{handles=handles,parentIndices=parentIndices,positionWeights=positions,rotationWeights=rotations,scaleWeights=scales,overlayComponents=oc,referenceComponents=rc,resultComponents=bc,rotationMode=(int)def.RotationMode,strength=0f};
                playable=AnimationScriptPlayable.Create(graph,job,3);
                playable.SetProcessInputs(false);
                var clip=AnimationClipPlayable.Create(graph,def.AdditiveClip);
                var reference=AnimationClipPlayable.Create(graph,def.ReferenceClip);
                if(!graph.Connect(incoming,0,playable,0)||!graph.Connect(clip,0,playable,1)||!graph.Connect(reference,0,playable,2))throw new InvalidOperationException("Additive stage connect failed.");
                for(int i=0;i<3;i++)playable.SetInputWeight(i,1f);
                valid=true;
            }
            catch{Dispose();throw;}
        }
        public bool TrySetBoneWeights(string path,BoneChannelWeights value)
        {if(!valid||!lookup.TryGetValue(path,out int i))return false;positions[i]=value.position;rotations[i]=value.rotation;scales[i]=value.scale;return true;}
        public void SetRotationMode(AdditiveRotationMode mode)
        {if(!valid)return;var job=playable.GetJobData<AdditivePoseJob>();job.rotationMode=(int)mode;playable.SetJobData(job);}
        public void Tick(float alpha,float dt)
        {if(!valid)return;weight=Mathf.MoveTowards(weight,Mathf.Clamp01(alpha),rate*dt);var job=playable.GetJobData<AdditivePoseJob>();job.strength=weight;playable.SetJobData(job);}
        public void Dispose()
        {valid=false;if(handles.IsCreated)handles.Dispose();if(parentIndices.IsCreated)parentIndices.Dispose();if(positions.IsCreated)positions.Dispose();if(rotations.IsCreated)rotations.Dispose();if(scales.IsCreated)scales.Dispose();if(oc.IsCreated)oc.Dispose();if(rc.IsCreated)rc.Dispose();if(bc.IsCreated)bc.Dispose();}
        private static string PathOf(Transform root,Transform bone)
        {var stack=new Stack<string>();for(var x=bone;x!=null&&x!=root;x=x.parent)stack.Push(x.name);return string.Join("/",stack.ToArray());}
    }
}
