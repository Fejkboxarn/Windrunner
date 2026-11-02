using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    [CreateAssetMenu(menuName="Portable Animation/Phase 5 Ability Pipeline")]
    public sealed class AbilityBlendProfile:ScriptableObject
    {
        [SerializeField] private AnimationClip idle,moving;
        [SerializeField] private float locomotionBlendRate=4f;
        [SerializeField] private AbilityRegionMap regionMap;
        [SerializeField] private List<AbilityBlendPolicy> abilities=new List<AbilityBlendPolicy>();
        public AnimationClip Idle=>idle;
        public AnimationClip Moving=>moving;
        public float LocomotionBlendRate=>locomotionBlendRate;
        public AbilityRegionMap RegionMap=>regionMap;
        public IReadOnlyList<AbilityBlendPolicy> Abilities=>abilities;
        public void Configure(AnimationClip idle,AnimationClip moving,AbilityRegionMap map,params AbilityBlendPolicy[] policies)
        {this.idle=idle;this.moving=moving;regionMap=map;abilities=new List<AbilityBlendPolicy>(policies??Array.Empty<AbilityBlendPolicy>());}
        public void AssertValid()
        {if(!idle||!moving||!regionMap||abilities==null||abilities.Count==0)throw new InvalidOperationException("Ability pipeline references missing.");regionMap.AssertValid();foreach(var policy in abilities)policy.AssertValid();}
    }
}
