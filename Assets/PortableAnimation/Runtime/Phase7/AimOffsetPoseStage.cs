using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace PortableAnimation
{
    public sealed class AimOffsetPoseStage:IDisposable
    {
        private readonly AimOffsetProfile profile;
        private readonly string[] paths;
        private readonly AnimationMixerPlayable[] grids=new AnimationMixerPlayable[4];
        private readonly float[] gridWeights=new float[9];
        private readonly bool[] available;
        private readonly AimOffsetSourceBlend sources;
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<int> parents;
        private NativeArray<float> p,r,s;
        private NativeArray<Quaternion> oc,rc,bc;
        private AnimationMixerPlayable selector;
        private AnimationScriptPlayable additive;
        private float yaw,pitch;
        private bool valid;
        public Playable Output=>additive;
        public AimOffsetSource SelectedSource=>sources.Target;
        public float Strength=>sources.Strength;
        public float CurrentYaw=>yaw;
        public float CurrentPitch=>pitch;
        public float SourceWeight(int index)=>sources.Weight(index);
        public AimOffsetPoseStage(PlayableGraph graph,Animator animator,Playable incoming,AimOffsetProfile profile)
        {
            profile.AssertValid();this.profile=profile;
            available=profile.BuildAvailability();
            sources=new AimOffsetSourceBlend(profile.SourceTransition,profile.EnableFadeRate);
            Transform root=animator.transform;
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            int count=all.Length-1;
            paths=new string[count];
            try
            {
                handles=new NativeArray<TransformStreamHandle>(count,Allocator.Persistent);
                parents=new NativeArray<int>(count,Allocator.Persistent);
                p=new NativeArray<float>(count,Allocator.Persistent);
                r=new NativeArray<float>(count,Allocator.Persistent);
                s=new NativeArray<float>(count,Allocator.Persistent);
                oc=new NativeArray<Quaternion>(count,Allocator.Persistent);
                rc=new NativeArray<Quaternion>(count,Allocator.Persistent);
                bc=new NativeArray<Quaternion>(count,Allocator.Persistent);
                var ids=new Dictionary<Transform,int>();
                for(int i=0;i<count;i++)
                {
                    Transform bone=all[i+1];
                    paths[i]=Relative(root,bone);
                    handles[i]=animator.BindStreamTransform(bone);
                    parents[i]=bone.parent==root?-1:ids[bone.parent];
                    ids.Add(bone,i);
                    r[i]=profile.UpperMask.Resolve(paths[i]).rotation;
                }
                selector=AnimationMixerPlayable.Create(graph,4);
                for(int src=0;src<4;src++)
                {
                    var set=profile.GetSet((AimOffsetSource)src);
                    grids[src]=AnimationMixerPlayable.Create(graph,9);
                    for(int k=0;k<9;k++)
                    {
                        var clip=AnimationClipPlayable.Create(graph,set.Sample(k));
                        clip.SetSpeed(0d);clip.SetTime(0d);
                        if(!graph.Connect(clip,0,grids[src],k))throw new InvalidOperationException("AO sample cannot connect.");
                        grids[src].SetInputWeight(k,k==4?1f:0f);
                    }
                    if(!graph.Connect(grids[src],0,selector,src))throw new InvalidOperationException("AO source cannot connect.");
                    selector.SetInputWeight(src,src==0?1f:0f);
                }
                var reference=AnimationClipPlayable.Create(graph,profile.ReferencePose);
                reference.SetSpeed(0d);reference.SetTime(0d);
                var job=new AdditivePoseJob{handles=handles,parentIndices=parents,positionWeights=p,rotationWeights=r,scaleWeights=s,overlayComponents=oc,referenceComponents=rc,resultComponents=bc,strength=0f,rotationMode=(int)AdditiveRotationMode.ComponentSpace};
                additive=AnimationScriptPlayable.Create(graph,job,3);
                additive.SetProcessInputs(false);
                if(!graph.Connect(incoming,0,additive,0)||!graph.Connect(selector,0,additive,1)||!graph.Connect(reference,0,additive,2))throw new InvalidOperationException("AO composite connection failed.");
                for(int i=0;i<3;i++)additive.SetInputWeight(i,1f);
                valid=true;
            }
            catch{Dispose();throw;}
        }
        public void Tick(in AimOffsetFrame frame,bool montageActive)
        {
            if(!valid)return;
            float dt=frame.Locomotion.DeltaTime;
            sources.Select(AimOffsetResolver.Resolve(frame.Enabled,montageActive,frame.CustomIdleActive,frame.PhysicsWeaponActive,available));
            sources.Advance(dt);
            yaw=Mathf.MoveTowards(yaw,Mathf.Clamp(frame.Yaw,profile.MinYaw,profile.MaxYaw),profile.AimRate*dt);
            pitch=Mathf.MoveTowards(pitch,Mathf.Clamp(frame.Pitch,profile.MinPitch,profile.MaxPitch),profile.AimRate*dt);
            AimOffsetGrid.Fill(AimOffsetGrid.NormalizeDegrees(yaw,profile.MinYaw,profile.MaxYaw),AimOffsetGrid.NormalizeDegrees(pitch,profile.MinPitch,profile.MaxPitch),gridWeights);
            for(int source=0;source<4;source++)
            {
                selector.SetInputWeight(source,sources.Weight(source));
                for(int i=0;i<9;i++)grids[source].SetInputWeight(i,gridWeights[i]);
            }
            BoneWeightMask mask=frame.UpperBodyOnly?profile.UpperMask:profile.FullMask;
            for(int i=0;i<paths.Length;i++)r[i]=Mathf.MoveTowards(r[i],mask.Resolve(paths[i]).rotation,profile.MaskRate*dt);
            var job=additive.GetJobData<AdditivePoseJob>();job.strength=sources.Strength;additive.SetJobData(job);
        }
        public void Dispose()
        {valid=false;if(handles.IsCreated)handles.Dispose();if(parents.IsCreated)parents.Dispose();if(p.IsCreated)p.Dispose();if(r.IsCreated)r.Dispose();if(s.IsCreated)s.Dispose();if(oc.IsCreated)oc.Dispose();if(rc.IsCreated)rc.Dispose();if(bc.IsCreated)bc.Dispose();}
        private static string Relative(Transform root,Transform bone){var stack=new Stack<string>();for(var t=bone;t!=null&&t!=root;t=t.parent)stack.Push(t.name);return string.Join("/",stack.ToArray());}
    }
}
