using System;
using System.Collections.Generic;
using UnityEngine;
namespace Windrunner.Daggers
{
    [CreateAssetMenu(menuName="Windrunner/Daggers/Arsenal")]
    public sealed class DaggerCatalog : ScriptableObject
    {
        public DaggerPair[] pairs=Array.Empty<DaggerPair>();
        public DaggerPair[] startingPairs=Array.Empty<DaggerPair>();
        public DaggerPair initialPair;
        [Range(1.7f,3f)] public float previewDistance=2;
        [Range(.1f,4f)] public float previewKeyIntensity=1.7f;
        public DaggerPair Find(string id)
        {
            foreach(var pair in pairs)if(pair && pair.id==id)return pair;
            return null;
        }
        public string ValidateSetup(bool tolerateRemovedDefinitions=false)
        {
            var ids=new HashSet<string>();var effects=new Dictionary<string,DaggerActiveEffect>();
            foreach(var p in pairs)
            {
                if(!p && tolerateRemovedDefinitions)continue;
                if(!p || string.IsNullOrWhiteSpace(p.id) || !ids.Add(p.id))return "Assign a unique stable ID to each pair.";
                if(!p.leftModel || !p.rightModel || !p.icon || string.IsNullOrWhiteSpace(p.displayName))return p.name+" needs two models, an icon and a display name.";
                foreach(var model in new[]{p.leftModel,p.rightModel})
                    if(model.GetComponentsInChildren<MeshRenderer>(true).Length==0 || model.GetComponentsInChildren<Behaviour>(true).Length!=0 || model.GetComponentsInChildren<Collider>(true).Length!=0 ||
                       model.GetComponentsInChildren<Rigidbody>(true).Length!=0)return model.name+" must contain render-only geometry.";
                if(Array.Exists(p.passives,e=>!e))return p.name+" contains a missing passive effect.";
                if(p.activationVfx)
                {
                    if(p.activationVfx.GetComponentsInChildren<Behaviour>(true).Length!=0 ||
                        p.activationVfx.GetComponentsInChildren<Collider>(true).Length!=0 ||
                        p.activationVfx.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                        return p.name+" needs an activation burst without gameplay or audio components.";
                    foreach(var ps in p.activationVfx.GetComponentsInChildren<ParticleSystem>(true))
                        if(ps.main.loop)return p.name+" needs a non-looping activation burst.";
                }
                var a=p.active;if(!a)continue;
                if(a is SecondWind wind && !(wind.restoreFraction>0 && wind.restoreFraction<=1))return p.name+" needs a restoration fraction greater than zero and at most one.";
                if(string.IsNullOrWhiteSpace(a.id) || !(a.cooldown>0) || float.IsInfinity(a.cooldown) || (effects.TryGetValue(a.id,out var other)&&other!=a))
                    return p.name+" needs a valid stable effect ID and positive cooldown.";
                effects[a.id]=a;
            }
            if(!tolerateRemovedDefinitions && Array.Exists(startingPairs,p=>!p || !Array.Exists(pairs,v=>v==p)))return "Starting pairs must belong to the arsenal.";
            if(initialPair && Array.IndexOf(startingPairs,initialPair)<0)return "Initial pair must be initially owned.";
            return null;
        }
    }
}
