using System;
using Windrunner.Production.Diagnostics;
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
        public event Action<float> ReserveChanged;
        public float Stored(DaggerPair pair) => pair ? progression.StoredDaggerReserve(pair.id) : 0;
        public void Listen(bool enabled)
        {
            if (!power) return;
            power.RewardOverflow -= Collect;
            if (enabled) power.RewardOverflow += Collect;
        }
        void Collect(AirPowerRewardSource source, float excess)
        {
            RewardProbe.Begin(RewardProbe.Phase.OverflowRules);
            var pair = Equipped;
            if (!pair || !(pair.active is SecondWind wind) || (wind.eligibleSources & source) == 0) return;
            float old = Stored(pair);
            float next = UnityEngine.Mathf.Min(wind.capacity, old + excess * wind.storageRatio);
            if (next == old) return;
            RewardProbe.End(RewardProbe.Phase.OverflowRules);
            progression.SetDaggerReserve(pair.id, next);
            RewardProbe.Begin(RewardProbe.Phase.ReserveHud);
            ReserveChanged?.Invoke(next - old);
            RewardProbe.End(RewardProbe.Phase.ReserveHud);
        }
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
            foreach (var pair in Catalog.pairs)
            {
                if (!pair || !(pair.active is SecondWind wind)) continue;
                if (state.cooldowns.RemoveAll(c => c.effectId == wind.id) > 0) changed = true;
                if (state.Stored(pair.id) > wind.capacity)
                {
                    state.SetStored(pair.id, wind.capacity);
                    changed = true;
                }
            }
            if(changed)progression.SaveDaggers(state);
        }
        public bool Equip(DaggerPair pair)
        {
            if(pair && (Catalog.Find(pair.id)!=pair || !Owns(pair)))return false;
            if(Equipped==pair)return false;
            var state=progression.ReadDaggers();state.equipped=pair?pair.id:"";progression.SaveDaggers(state);
            EquippedChanged?.Invoke(pair);return true;
        }
        public void ReconcileEquippedCapacity()
        {
            var pair = Equipped;
            if (pair && pair.active is SecondWind wind && Stored(pair) > wind.capacity)
            {
                Synchronize();
                ReserveChanged?.Invoke(0);
            }
        }
        public string Status(DaggerPair pair, bool includeBalance=true)
        {
            if (!pair || !pair.active) return "No active ability";
            float stored = Stored(pair);
            string status = pair.active.Unavailable(power, stored) ?? "Ready";
            return includeBalance && pair.active is SecondWind wind ? $"{stored:0.#}/{wind.capacity:0.#} · {status}" : status;
        }
        public bool TryActivate(bool permitted, int pressFrame)
        {
            var pair = Equipped;
            if (!permitted || pressFrame == lastActivation || !pair || !pair.active) return false;
            float stored = Stored(pair);
            if (pair.active is SecondWind wind) stored = UnityEngine.Mathf.Min(stored, wind.capacity);
            if (pair.active.Unavailable(power, stored) != null) return false;
            progression.SetDaggerReserve(pair.id, 0);
            lastActivation = pressFrame;
            pair.active.Execute(power, stored);
            ReserveChanged?.Invoke(-stored);
            Activated?.Invoke(pair);
            return true;
        }
    }
}
