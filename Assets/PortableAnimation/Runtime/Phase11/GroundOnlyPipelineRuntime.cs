using System;
using System.Collections.Generic;
using UnityEngine;
using PortableAnimation.Authoring;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PortableAnimation
{
    /// <summary>
    /// Small production composition: eight-way GroundMotionSource plus an
    /// optional masked clip-action layer. The animation graph NEVER writes
    /// movement or fires gameplay events. Suitable for characters with no
    /// weapons, hit-react profiles or mandatory montage/hero-state graph.
    /// </summary>
    public sealed class GroundOnlyPipelineRuntime : IDisposable
    {
        private readonly GroundMotionSource ground;
        private readonly bool originalTiming;
        private readonly PortableCharacterActionProfile actionProfile;
        private readonly AvatarMask fullBodyMask;
        private PlayableGraph graph;

        private sealed class PoseLayer
        {
            public int action;
            public ActionMotionContext context;
            public AnimationClip clip;
            public AvatarMask mask;
            public float weight, share;
            public AnimationClipPlayable playable;
            public AnimationLayerMixerPlayable mixer;
            public int input;
            public int stackIndex;
            public RegionMaskSettings regions,parent;
            public FlattenedPoseStack weighted;
            public float applied,seconds,phase,start,end=1;
            public PortableActionPlayback.Timing timing;
        }
        private static readonly RegionMaskSettings nativeMaskWeights = new RegionMaskSettings();
        private readonly List<FlattenedPoseStack> stacks=new List<FlattenedPoseStack>();
        private readonly List<PoseLayer> layers = new List<PoseLayer>();
        private readonly bool[,] available;
        private ActionMotionContext context;
        private (HumanBodyBones bone, AuthoringBodyRegion region,string path)[] bones;
        private Transform[] hierarchy;
        private PoseLayer[] clock;
        private PoseLayer[,] choices;
        public ActionMotionContext Context => context;
        public float FinalRotationContribution(int action,ActionMotionContext variant,HumanBodyBones bone)
        {
            float remainder=1f,contribution=0;
            for(int i=layers.Count-1;i>=0;i--)
            {
                var layer=layers[i];
                float coefficient=layer.applied;
                if(coefficient<=0)continue;
                var region=RegionMaskSettings.Region(bone);
                if(layer.weighted!=null)
                {
                    string path=null;
                    foreach(var mapped in bones)if(mapped.bone==bone){path=mapped.path;break;}
                    if(path==null)continue;
                    var settings = layer.regions?.enabled == true ? layer.regions : nativeMaskWeights;
                    coefficient*=settings.Resolve(region,path,layer.mask,layer.phase,bone,layer.parent).rotation;
                }
                else if(layer.mask && !layer.mask.GetHumanoidBodyPartActive(RegionMaskSettings.Part(region)))coefficient=0;
                if((action<0 || layer.action==action) && ((int)variant<0 || layer.context==variant))contribution+=coefficient*remainder;
                remainder*=1-coefficient;
            }
            return contribution;
        }
        private readonly PortableActionPlayback playback;

        private int lastFrame = -1;

        public bool IsLoopingSlot(int index) => playback.IsLoopingSlot(index);
        public bool HasActiveAction => playback.ActiveIndex >= 0;
        public bool IsValid => graph.IsValid();
        private bool debugIsolateLocomotion;
        public bool DebugIsolateLocomotion
        {
            get => debugIsolateLocomotion;
            set { if (Application.isEditor || Debug.isDebugBuild) debugIsolateLocomotion = value; }
        }
        public int LiveActionCount => actionProfile != null ? actionProfile.Count : 0;
        public string ActiveAction => playback.ActiveIndex < 0 ? "None" :
            actionProfile.Action(playback.ActiveIndex).id;
        public float IdleBlend => ground.IdleBlend;
        public float WalkBlend => ground.WalkBlend;
        public float RunBlend => ground.RunBlend;
        public void ReadRecordingPose(out RecordingPose pose)
        {
            pose = new RecordingPose { state = ground.State, cycles = ground.CyclesPerSecond,
                context = context, walk = ground.WalkBlend, run = ground.RunBlend, actualGroundSpeed = ground.ActualGroundSpeed };
            ground.ReadDominant(out pose.clip, out pose.weight, out pose.direction);
            pose.timing = ground.DominantTiming;
            pose.groundTiming = ground.DominantTiming;
            pose.adaptive = ground.DominantAdaptive;
            if (pose.adaptive.Enabled && pose.groundTiming.SourceLength > 0)
                pose.cycles = Mathf.Abs(pose.groundTiming.EffectiveSpeed) / pose.groundTiming.SourceLength;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                float w = layer.applied;
                if (w <= pose.actionWeight) continue;
                var entry = actionProfile.Action(layer.action);
                pose.actionWeight = w;
                pose.action = entry.id;
                pose.mask = layer.mask;
                pose.actionClip = layer.clip;
                pose.actionSeconds = layer.seconds;
                pose.timing = LayerTiming(layer);
                pose.weighted = layer.regions?.enabled == true;
                pose.phase = layer.phase;
                pose.weightSource = layer.regions?.source ? layer.regions.source : layer.parent?.source;
            }
        }
        VisualPlaybackSample LayerTiming(PoseLayer layer)
        {
            var active = clock[layer.action];
            if (active.timing.Visual != null)
            {
                var sample = playback.VisualSample(layer.action);
                float ratio = layer.clip.length * (layer.end-layer.start) / (active.clip.length * (active.end-active.start));
                return new VisualPlaybackSample(layer.seconds,layer.clip.length,sample.BaseSpeed,sample.CurveMultiplier,
                    sample.EffectiveSpeed*ratio,sample.EstimatedSeconds,sample.Mode,sample.Held);
            }
            var timing=active.timing;
            return new VisualPlaybackSample(layer.seconds,layer.clip.length,timing.Speed,1,
                layer.clip.length*(layer.end-layer.start)*timing.Speed/timing.Length,
                timing.Length/timing.Speed,VisualPlaybackMode.ExistingTiming);
        }
        public VisualPlaybackSample ReadActionTiming(int index,ActionMotionContext variant)
        {
            if(index<0 || index>=LiveActionCount)return default;
            var layer=choices[index,(int)variant]??choices[index,0];
            return layer==null?default:LayerTiming(layer);
        }
        public VisualPlaybackSample ReadGroundTiming(string key) => ground.ReadTiming(key);
        public AdaptiveLocomotionSample ReadGroundAdaptive(string key) => ground.ReadAdaptive(key);
        public void ResetGroundPlayback() => ground.ResetPlayback();
        public struct RecordingPose
        {
            public GroundMotionState state;
            public ActionMotionContext context;
            public AnimationClip actionClip;
            public AnimationClip clip;
            public int direction;
            public float weight, cycles, walk, run, actionWeight, actionSeconds, actualGroundSpeed;
            public string action;
            public AvatarMask mask;
            public bool weighted;
            public BoneWeightMask weightSource;
            public float phase;
            public VisualPlaybackSample timing, groundTiming;
            public AdaptiveLocomotionSample adaptive;
        }
        public GroundMotionState GroundState => ground.State;

        public GroundOnlyPipelineRuntime(Animator animator,
            GroundMotionProfile motionProfile,
            PortableCharacterActionProfile optionalActions, bool originalTiming = false, bool outputEnabled = true)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (motionProfile == null)
                throw new ArgumentNullException(nameof(motionProfile));
            motionProfile.AssertStandaloneValid();
            optionalActions?.AssertValid();
            actionProfile = optionalActions;
            this.originalTiming = originalTiming;
            available = new bool[LiveActionCount,3];
            clock=new PoseLayer[LiveActionCount];choices=new PoseLayer[LiveActionCount,3];
            var timings = new PortableActionPlayback.Timing[LiveActionCount];
            for (int i = 0; i < timings.Length; i++)
            {
                var action = actionProfile.Action(i);
                if (action.IsPlayable)
                {
                    var basis=new PoseLayer {action=i,clip=action.clip,mask=action.mask,regions=action.regions,weight=action.blendWeight,share=1};
                    ConfigureLayer(basis,action,null);
                    clock[i]=basis;
                    AddVariant(i,ActionMotionContext.Stationary,action.stationary);
                    AddVariant(i,ActionMotionContext.Airborne,action.airborne);
                }
                timings[i] = clock[i]!=null?clock[i].timing:new PortableActionPlayback.Timing(action.clip != null ? action.clip.length : 0f,
                    action.playbackSpeed, action.blendInSeconds, action.blendOutSeconds, action.priority,
                    action.IsPlayable, action.loopUntilCanceled, action.allowSameActionRestart, action.interruptible);
            }
            playback = new PortableActionPlayback(timings);
            fullBodyMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                fullBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
            fullBodyMask.AddTransformPath(animator.transform, true);

            graph = PlayableGraph.Create("PortableAnimation.GroundOnly");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ground = new GroundMotionSource(graph, motionProfile, true, originalTiming);
                Playable finalPose = ground.Output;

                if (LiveActionCount > 0)
                {
                    // Native Humanoid layers may precede transform-weighted composition.
                    // Once transforms are composed, keep later layers in the same
                    // space: a downstream Humanoid mixer can reconstruct masked legs
                    // instead of preserving the already-composed bone transforms.
                    for(int i=0;i<layers.Count;)
                    {
                        var layer=layers[i];
                        if(layer.regions?.enabled==true)
                        {
                            if(hierarchy==null)BindBones(animator);
                            int first=i;
                            i = layers.Count;
                            var assets=new AnimationClip[i-first];
                            for(int n=first;n<i;n++)assets[n-first]=layers[n].clip;
                            var stack=new FlattenedPoseStack(graph,animator,finalPose,assets,true,hierarchy);
                            stacks.Add(stack);
                            for(int n=first;n<i;n++){layers[n].weighted=stack;layers[n].stackIndex=n-first;}
                            finalPose=stack.Output;continue;
                        }
                        int begin=i;
                        while(i<layers.Count && layers[i].regions?.enabled!=true)i++;
                        var group=AnimationLayerMixerPlayable.Create(graph,i-begin+1);
                        graph.Connect(finalPose,0,group,0);group.SetInputWeight(0,1);
                        for(int n=begin;n<i;n++)
                        {
                            layer=layers[n];layer.mixer=group;layer.input=n-begin+1;
                            layer.playable=AnimationClipPlayable.Create(graph,layer.clip);
                            layer.playable.SetSpeed(0);
                            group.SetLayerMaskFromAvatarMask((uint)layer.input,layer.mask?layer.mask:fullBodyMask);
                            if(!graph.Connect(layer.playable,0,group,layer.input))throw new InvalidOperationException("Cannot connect "+layer.clip.name);
                        }
                        finalPose=group;
                    }
                }

                var output = AnimationPlayableOutput.Create(graph,
                    "CharacterPose", outputEnabled ? animator : null);
                output.SetSourcePlayable(finalPose);
                graph.Play();
                graph.Evaluate(0f);
            }
            catch
            {
                if (graph.IsValid()) graph.Destroy();
                foreach(var stack in stacks)stack.Dispose();
            if (fullBodyMask) {if(Application.isPlaying)UnityEngine.Object.Destroy(fullBodyMask);else UnityEngine.Object.DestroyImmediate(fullBodyMask);}
                throw;
            }
        }

        public bool RequestAction(int index)
        {
            if (!graph.IsValid() || actionProfile == null || index < 0 ||
                index >= actionProfile.Count)
                return false;
            if (!playback.Request(index)) return false;

            return true;
        }

        private void AddVariant(int action, ActionMotionContext variant, ActionPoseOverride pose)
        {
            var basis=actionProfile.Action(action);
            if(pose==null || !pose.enabled || (!pose.inheritClip && !pose.Available && !pose.muteWhenMissing))return;
            available[action,(int)variant]=true;
            ConfigureLayer(new PoseLayer{action=action,context=variant,clip=pose.inheritClip || !pose.Available?basis.clip:pose.clip,
                mask=pose.inheritMask?basis.mask:pose.mask,regions=pose.regions?.enabled==true?pose.regions:pose.inheritMask?basis.regions:pose.regions,
                parent=pose.inheritMask && pose.regions?.enabled==true?basis.regions:null,
                weight=!pose.Available && !pose.inheritClip?0:pose.inheritWeight?basis.blendWeight:pose.weight},basis,pose);
        }
        private void ConfigureLayer(PoseLayer layer,PortableCharacterAction action,ActionPoseOverride pose)
        {
            bool Has(VisualTimingFields field)=>pose!=null && pose.overrideTiming && (pose.timingFields&field)!=0;
            var t=pose?.timing;
            var segment=Has(VisualTimingFields.Segment)?t:action.segment;
            layer.start=segment?.start??0;layer.end=segment?.end??1;
            float length=(Has(VisualTimingFields.Speed)||Has(VisualTimingFields.Segment)?layer.clip.length:action.clip.length)*(layer.end-layer.start);
            var settings = pose?.playback;
            if (settings == null || settings.mode == VisualPlaybackMode.ExistingTiming) settings = action.playback;
            if (originalTiming) settings = new VisualPlaybackSettings { mode = VisualPlaybackMode.AuthoredSpeed,
                maximumActionSeconds = Mathf.Min(300,Mathf.Max(10,layer.clip.length*(layer.end-layer.start)+1)) };
            var visual = settings != null && settings.mode != VisualPlaybackMode.ExistingTiming
                ? new VisualPlaybackClock(settings, layer.clip.length, layer.start, layer.end) : null;
            layer.timing=new PortableActionPlayback.Timing(length,Has(VisualTimingFields.Speed)?t.speed:action.playbackSpeed,
                Has(VisualTimingFields.FadeIn)?t.fadeIn:action.blendInSeconds,Has(VisualTimingFields.FadeOut)?t.fadeOut:action.blendOutSeconds,
                Has(VisualTimingFields.Priority)?t.priority:action.priority,true,Has(VisualTimingFields.Recovery)?t.loop:action.loopUntilCanceled,
                Has(VisualTimingFields.Interruption)?t.restart:action.allowSameActionRestart,Has(VisualTimingFields.Interruption)?t.interruptible:action.interruptible, visual);
            layers.Add(layer);choices[layer.action,(int)layer.context]=layer;
        }
        private void BindBones(Animator animator)
        {
            var transforms=new List<Transform>{animator.transform};
            var mapped=new List<(HumanBodyBones,AuthoringBodyRegion,string)>();
            for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
            {
                var bone=(HumanBodyBones)i;
                var transform=animator.GetBoneTransform(bone);
                if(!transform || transforms.Contains(transform))continue;
                transforms.Add(transform);
                mapped.Add((bone,RegionMaskSettings.Region(bone),FlattenedPoseStack.PathOf(animator.transform,transform)));
            }
            bones=mapped.ToArray();hierarchy=transforms.ToArray();
        }

        public void Evaluate(in GroundMotionFrame motion) => Evaluate(motion,motion.Locomotion.LocalVelocity.magnitude);
        public void Evaluate(in GroundMotionFrame motion,float horizontalMetresPerSecond)
        {
            if (!graph.IsValid()) throw new ObjectDisposedException(nameof(GroundOnlyPipelineRuntime));
            if (motion.Locomotion.FrameId == lastFrame) return;
            lastFrame = motion.Locomotion.FrameId;
            ground.Tick(in motion);
            if(actionProfile)
                context=actionProfile.SelectContext(motion.Grounded,horizontalMetresPerSecond,context);
            UpdateAction(motion.Locomotion.DeltaTime);
            graph.Evaluate(motion.Locomotion.DeltaTime);
        }

        public void CancelAction(int index = -1) => playback.Cancel(index);
        public void CancelGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return;
            for (int i = 0; i < LiveActionCount; i++)
                if (actionProfile.Action(i).group == group) playback.Cancel(i);
        }

        private void UpdateAction(float dt)
        {
            if (LiveActionCount == 0) return;
            // Switch visual inputs on the existing action clock. A context change
            // cannot request an action, emit feedback or restart its release.
            float step = 1f - Mathf.Exp(-3f*dt/actionProfile.contextFadeSeconds);
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];
                var target=available[layer.action,(int)context] ? context : ActionMotionContext.GroundedMoving;
                float share=layer.context==target ? 1f : 0f;
                layer.share=playback.Weight(layer.action)<=0 ? share : Mathf.Lerp(layer.share,share,step);
            }
            for(int i=0;i<clock.Length;i++)
            {
                var next=choices[i,(int)context]??choices[i,0];
                if(next==clock[i])continue;
                playback.ChangeTiming(i,next.timing);clock[i]=next;
            }
            playback.Tick(dt);
            for(int i=0;i<layers.Count;i++)
            {
                var layer=layers[i];
                float phase=playback.ClipTime(layer.action)/clock[layer.action].timing.Length;
                layer.phase=phase;
                layer.seconds=Mathf.Min(Mathf.Lerp(layer.start,layer.end,phase)*layer.clip.length,
                    Mathf.Max(0,layer.clip.length-.0001f));
                float alpha=debugIsolateLocomotion ? 0 : layer.share*layer.weight*playback.Weight(layer.action);
                if(layer.weighted!=null)
                {
                    if(alpha>0 || layer.applied>0)
                    {
                        layer.weighted.SetSampleTime(layer.stackIndex,layer.seconds);
                        foreach(var bone in bones)
                        {
                            var settings = layer.regions?.enabled == true ? layer.regions : nativeMaskWeights;
                            var w=settings.Resolve(bone.region,bone.path,layer.mask,phase,bone.bone,layer.parent);
                            layer.weighted.SetBoneWeights(layer.stackIndex,bone.path,new BoneChannelWeights(w.position*alpha,w.rotation*alpha,w.scale*alpha));
                        }
                    }
                }
                else
                {
                    layer.playable.SetTime(layer.seconds);
                    layer.mixer.SetInputWeight(layer.input,alpha);
                }
                layer.applied=alpha;
            }
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            foreach(var stack in stacks)stack.Dispose();
            if (fullBodyMask) {if(Application.isPlaying)UnityEngine.Object.Destroy(fullBodyMask);else UnityEngine.Object.DestroyImmediate(fullBodyMask);}
        }
    }
}
