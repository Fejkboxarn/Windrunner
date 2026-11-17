using System;
using AirflowPrototype;
using Windrunner.Production.Progression;
namespace Windrunner.Daggers
{
    public sealed class DaggerEquipment
    {
        readonly ProgressionService progression;
        public readonly DaggerCatalog Catalog;
        readonly AirPower power;
        int lastActivation=-1;
        public DaggerPair Equipped=>Catalog.Find(progression.EquippedDagger);
        public event Action<DaggerPair> EquippedChanged, Activated;
        public bool Owns(DaggerPair pair)=>pair && progression.OwnsDagger(pair.id);
        public DaggerEquipment(DaggerCatalog catalog,ProgressionService service,AirPower resource)
        {
            Catalog=catalog;progression=service;power=resource;
            Synchronize();
        }
        public void Synchronize()
        {
            var state=progression.ReadDaggers();bool changed=false;
            if(!state.initialized)
            {
                state=new DaggerLoadout();
                foreach(var p in Catalog.startingPairs)if(p && Catalog.Find(p.id)==p && !state.owned.Contains(p.id))state.owned.Add(p.id);
                state.equipped=Catalog.initialPair?Catalog.initialPair.id:"";state.initialized=true;changed=true;
            }
            foreach(var pair in Catalog.pairs)
                if(pair && !string.IsNullOrEmpty(pair.unlockContentId) && !state.owned.Contains(pair.id) && progression.HasUnlock(pair.unlockContentId))
                {state.owned.Add(pair.id);changed=true;}
            if(!string.IsNullOrEmpty(state.equipped) && (!Catalog.Find(state.equipped)||!state.owned.Contains(state.equipped)))
            {state.equipped="";changed=true;}
            if(changed)progression.SaveDaggers(state);
        }
        public bool Equip(DaggerPair pair)
        {
            if(pair && (Catalog.Find(pair.id)!=pair || !Owns(pair)))return false;
            if(Equipped==pair)return false;
            var state=progression.ReadDaggers();state.equipped=pair?pair.id:"";progression.SaveDaggers(state);
            EquippedChanged?.Invoke(pair);return true;
        }
        public void Tick(float scaledSeconds)=>progression.AdvanceDaggerCooldowns(scaledSeconds);
        public float Remaining(DaggerPair pair)=>pair && pair.active?progression.DaggerCooldownRemaining(pair.active.id):0;
        public string Status(DaggerPair pair)
        {
            if(!pair || !pair.active)return "No active ability";
            float seconds=Remaining(pair);
            if(seconds>0)return "Cooldown · "+UnityEngine.Mathf.CeilToInt(seconds)+" s";
            return pair.active.Unavailable(power)??"Ready";
        }
        public bool TryActivate(bool permitted,int pressFrame)
        {
            var pair=Equipped;
            if(!permitted || pressFrame==lastActivation || !pair || !pair.active || Remaining(pair)>0 || pair.active.Unavailable(power)!=null)return false;
            var effect=pair.active;var state=progression.ReadDaggers();
            state.cooldowns.RemoveAll(c=>c.effectId==effect.id);
            state.cooldowns.Add(new DaggerCooldown{effectId=effect.id,remaining=effect.cooldown});
            progression.SaveDaggers(state);lastActivation=pressFrame;
            effect.Execute(power);Activated?.Invoke(pair);return true;
        }
    }
}
