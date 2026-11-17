using System;
using System.Collections.Generic;
using PortableAnimation.Authoring;
using UnityEngine;

namespace PortableAnimation
{
    [Flags]
    public enum VisualTimingFields { Segment=1,Speed=2,FadeIn=4,FadeOut=8,Priority=16,Interruption=32,Recovery=64,All=127 }

    [Serializable]
    public sealed class BoneInfluence
    {
        public HumanBodyBones bone;
        [Range(0,1)] public float weight=1;
    }

    [Serializable]
    public sealed class ActionVisualTiming
    {
        [Range(0,1)] public float start;
        [Range(0,1)] public float end=1;
        [Min(.01f)] public float speed=1;
        [Min(.001f)] public float fadeIn=.08f, fadeOut=.14f;
        public int priority;
        public bool interruptible=true, loop, restart;
        public bool Valid => start>=0 && end<=1 && end>start && speed>0 && fadeIn>0 && fadeOut>0 &&
            !float.IsInfinity(speed+fadeIn+fadeOut) && priority>=0;
    }

    [Serializable]
    public sealed class RegionMaskSettings
    {
        public bool enabled;
        public BoneWeightMask source;
        public List<BoneInfluence> bones=new List<BoneInfluence>();
        public List<RegionInfluenceSettings> regions=new List<RegionInfluenceSettings>();

        public static AuthoringBodyRegion Region(HumanBodyBones bone)
        {
            if(bone>=HumanBodyBones.LeftThumbProximal && bone<=HumanBodyBones.LeftLittleDistal)return AuthoringBodyRegion.LeftFingers;
            if(bone>=HumanBodyBones.RightThumbProximal && bone<=HumanBodyBones.RightLittleDistal)return AuthoringBodyRegion.RightFingers;
            foreach(var pair in LiveActionMaskEvaluator.Mapping)if(pair.bone==bone)return pair.region;
            return AuthoringBodyRegion.Head;
        }

        public static AvatarMaskBodyPart Part(AuthoringBodyRegion region)
        {
            switch(region)
            {
                case AuthoringBodyRegion.Pelvis: case AuthoringBodyRegion.Root: return AvatarMaskBodyPart.Root;
                case AuthoringBodyRegion.Head: case AuthoringBodyRegion.Neck:return AvatarMaskBodyPart.Head;
                case AuthoringBodyRegion.LeftFingers:return AvatarMaskBodyPart.LeftFingers;
                case AuthoringBodyRegion.RightFingers:return AvatarMaskBodyPart.RightFingers;
            }
            if(region>=AuthoringBodyRegion.LeftClavicle && region<=AuthoringBodyRegion.LeftHand)return AvatarMaskBodyPart.LeftArm;
            if(region>=AuthoringBodyRegion.RightClavicle && region<=AuthoringBodyRegion.RightHand)return AvatarMaskBodyPart.RightArm;
            if(region>=AuthoringBodyRegion.LeftUpperLeg && region<=AuthoringBodyRegion.LeftToes)return AvatarMaskBodyPart.LeftLeg;
            if(region>=AuthoringBodyRegion.RightUpperLeg && region<=AuthoringBodyRegion.RightToes)return AvatarMaskBodyPart.RightLeg;
            return AvatarMaskBodyPart.Body;
        }

        public BoneChannelWeights Resolve(AuthoringBodyRegion region,string path,AvatarMask native,float phase,HumanBodyBones bone=HumanBodyBones.LastBone,RegionMaskSettings parent=null)
        {
            float fallback=!native || native.GetHumanoidBodyPartActive(Part(region))?1:0;
            var value=source?source.Resolve(path):parent!=null && parent.enabled?
                parent.Resolve(region,path,native,phase,bone):new BoneChannelWeights(fallback,fallback,fallback);
            if(bones!=null)foreach(var rule in bones)
                if(rule!=null && rule.bone==bone)return new BoneChannelWeights(rule.weight,rule.weight,rule.weight);
            if(regions!=null)foreach(var rule in regions)
            {
                if(rule==null || rule.region!=region)continue;
                float envelope=rule.clipInfluence==null || rule.clipInfluence.length==0?1:rule.clipInfluence.Evaluate(phase);
                return new BoneChannelWeights(rule.position*envelope,rule.rotation*envelope,rule.scale*envelope);
            }
            return value;
        }
    }
}
