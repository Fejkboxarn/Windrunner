using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    public enum AbilityBodyMode { UpperBody,FullBody,Asymmetric,Custom }
    [Serializable] public sealed class AbilityRegionChannels
    {
        [SerializeField] private AbilityRegion region;
        [SerializeField] private BoneChannelWeights channels;
        public AbilityRegion Region=>region;
        public BoneChannelWeights Channels=>channels;
        public AbilityRegionChannels(AbilityRegion region,float position,float rotation,float scale=0f)
        {this.region=region;channels=new BoneChannelWeights(position,rotation,scale);}
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 5 Ability Policy")]
    public sealed class AbilityBlendPolicy:ScriptableObject
    {
        [SerializeField] private string policyId;
        [SerializeField] private AbilityBodyMode mode;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private float blendInSeconds=0.3f,blendOutSeconds=0.3f;
        [SerializeField] private List<AbilityRegionChannels> regions=new List<AbilityRegionChannels>();
        public string PolicyId=>policyId;
        public AbilityBodyMode Mode=>mode;
        public AnimationClip Clip=>clip;
        public float BlendInSeconds=>blendInSeconds;
        public float BlendOutSeconds=>blendOutSeconds;
        public IReadOnlyList<AbilityRegionChannels> Regions=>regions;
        public void Configure(string id,AbilityBodyMode bodyMode,AnimationClip animation,float blendIn,float blendOut,params AbilityRegionChannels[] rules)
        {policyId=id;mode=bodyMode;clip=animation;blendInSeconds=blendIn;blendOutSeconds=blendOut;regions=new List<AbilityRegionChannels>(rules??Array.Empty<AbilityRegionChannels>());}
        public BoneChannelWeights Resolve(AbilityRegion region){var result=new BoneChannelWeights(0,0,0);foreach(var item in regions)if(item!=null&&item.Region==region)result=item.Channels;return result;}
        public void AssertValid(){if(string.IsNullOrWhiteSpace(policyId)||!clip||regions==null||regions.Count==0)throw new InvalidOperationException("Ability policy missing clip/id/regions.");}
    }
}
