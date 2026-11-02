using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    public enum AbilityRegion { None,Root,Pelvis,Spine1,Spine2,Spine3,LeftClavicle,LeftArm,RightClavicle,RightArm,Head,LeftLeg,RightLeg }
    [Serializable] public sealed class AbilityRegionBinding
    {
        [SerializeField] private string path;
        [SerializeField] private AbilityRegion region;
        [SerializeField] private bool includeDescendants=true;
        public string Path=>path;
        public AbilityRegion Region=>region;
        public AbilityRegionBinding(string path,AbilityRegion region,bool children=true){this.path=path;this.region=region;includeDescendants=children;}
        public bool Matches(string other)=>other==path||(includeDescendants&&other.StartsWith(path+"/",StringComparison.Ordinal));
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 5 Region Map")]
    public sealed class AbilityRegionMap:ScriptableObject
    {
        [SerializeField] private List<AbilityRegionBinding> bindings=new List<AbilityRegionBinding>();
        public IReadOnlyList<AbilityRegionBinding> Bindings=>bindings;
        public void Configure(params AbilityRegionBinding[] entries){bindings=new List<AbilityRegionBinding>(entries??Array.Empty<AbilityRegionBinding>());}
        public AbilityRegion Resolve(string path){AbilityRegion result=AbilityRegion.None;foreach(var b in bindings)if(b!=null&&b.Matches(path))result=b.Region;return result;}
        public void AssertValid(){if(bindings==null||bindings.Count==0)throw new InvalidOperationException("No region bindings.");}
    }
}
