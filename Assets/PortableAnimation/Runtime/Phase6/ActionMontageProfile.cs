using System;
using System.Collections.Generic;
using UnityEngine;
namespace PortableAnimation
{
    public enum MontageEndBehavior { ReturnToLocomotion,HoldFinalPose }
    [Serializable] public sealed class MontageEventMarker
    {
        [SerializeField] private string eventId;
        [SerializeField,Range(0f,1f)] private float normalizedTime;
        public string EventId=>eventId;
        public float NormalizedTime=>normalizedTime;
        public MontageEventMarker(string id,float time){eventId=id;normalizedTime=Mathf.Clamp01(time);}
    }
    [Serializable] public sealed class ActionMontageEntry
    {
        [SerializeField] private AbilityBlendPolicy abilityPolicy;
        [SerializeField] private float playbackSpeed=1f,blendIn=0.2f,blendOut=0.2f;
        [SerializeField] private MontageEndBehavior endBehavior;
        [SerializeField] private int priority=10;
        [SerializeField] private bool interruptible=true,queueWhenBlocked=true;
        [SerializeField] private List<MontageEventMarker> markers=new List<MontageEventMarker>();
        public AbilityBlendPolicy Policy=>abilityPolicy;
        public string Id=>abilityPolicy==null?"":abilityPolicy.PolicyId;
        public AnimationClip Clip=>abilityPolicy==null?null:abilityPolicy.Clip;
        public float PlaybackSpeed=>Mathf.Max(0.01f,playbackSpeed);
        public float BlendIn=>blendIn;
        public float BlendOut=>blendOut;
        public MontageEndBehavior EndBehavior=>endBehavior;
        public int Priority=>priority;
        public bool Interruptible=>interruptible;
        public bool QueueWhenBlocked=>queueWhenBlocked;
        public IReadOnlyList<MontageEventMarker> Markers=>markers;
        public ActionMontageEntry(AbilityBlendPolicy policy,int priority,bool interruptible,bool queueWhenBlocked,float speed,float blendIn,float blendOut,MontageEndBehavior endBehavior,params MontageEventMarker[] markers)
        {abilityPolicy=policy;this.priority=priority;this.interruptible=interruptible;this.queueWhenBlocked=queueWhenBlocked;playbackSpeed=speed;this.blendIn=blendIn;this.blendOut=blendOut;this.endBehavior=endBehavior;this.markers=new List<MontageEventMarker>(markers??Array.Empty<MontageEventMarker>());}
        public void AssertValid(){if(!abilityPolicy||!Clip||Clip.length<=0f||playbackSpeed<=0f)throw new InvalidOperationException("Invalid montage entry "+Id);abilityPolicy.AssertValid();}
    }
    [CreateAssetMenu(menuName="Portable Animation/Phase 6 Montage Profile")]
    public sealed class ActionMontageProfile:ScriptableObject
    {
        [SerializeField] private AbilityBlendProfile baseProfile;
        [SerializeField] private List<ActionMontageEntry> actions=new List<ActionMontageEntry>();
        public AbilityBlendProfile BaseProfile=>baseProfile;
        public IReadOnlyList<ActionMontageEntry> Actions=>actions;
        public void Configure(AbilityBlendProfile source,params ActionMontageEntry[] entries)
        {baseProfile=source;actions=new List<ActionMontageEntry>(entries??Array.Empty<ActionMontageEntry>());}
        public void AssertValid(){if(!baseProfile||actions==null||actions.Count==0)throw new InvalidOperationException("Montage references missing.");baseProfile.AssertValid();foreach(var e in actions)e.AssertValid();}
    }
}
