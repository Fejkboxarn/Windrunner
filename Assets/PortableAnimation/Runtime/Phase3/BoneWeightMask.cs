using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    [Serializable] public struct BoneChannelWeights
    {
        [Range(0f,1f)] public float position,rotation,scale;
        public BoneChannelWeights(float position,float rotation,float scale)
        {this.position=Mathf.Clamp01(position);this.rotation=Mathf.Clamp01(rotation);this.scale=Mathf.Clamp01(scale);}
    }
    [Serializable] public sealed class BoneMaskRule
    {
        [SerializeField] private string bonePath;
        [SerializeField] private bool includeChildren=true;
        [SerializeField] private BoneChannelWeights channels;
        public string BonePath=>bonePath;
        public bool IncludeChildren=>includeChildren;
        public BoneChannelWeights Channels=>channels;
        public BoneMaskRule(string path,bool recursive,BoneChannelWeights weights)
        {bonePath=path;includeChildren=recursive;channels=weights;}
        public bool Matches(string path) => path==bonePath || (includeChildren&&path.StartsWith(bonePath+"/",StringComparison.Ordinal));
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 3 Bone Weight Mask",fileName="BoneWeightMask")]
    public sealed class BoneWeightMask : ScriptableObject
    {
        [SerializeField] private List<BoneMaskRule> rules=new List<BoneMaskRule>();
        public IReadOnlyList<BoneMaskRule> Rules=>rules;
        public void SetRules(params BoneMaskRule[] incoming){rules=new List<BoneMaskRule>(incoming??Array.Empty<BoneMaskRule>());}
        public BoneChannelWeights Resolve(string path)
        {
            var result=new BoneChannelWeights(0f,0f,0f);
            foreach(var rule in rules)if(rule!=null&&rule.Matches(path))result=rule.Channels;
            return result;
        }
    }
}
