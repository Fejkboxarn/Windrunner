using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Progression.Activities
{
    [Flags] public enum ActivationMethod { None=0, Cast=1, Sling=2, Proximity=4, Interact=8, External=16 }
    public enum RelayPlayerState { Any, Grounded, Airborne }
    public enum RelayPattern { Sequential, Unordered, Graph }
    public enum RelayJoin { Any, All }
    public enum RelayReset { Restart, RetainActivated }
    public enum RelayState { Inactive, Delayed, Available, Activated, Expiring, Expired, Completed, Invalid }
    public enum ActivityState { Ready, Active, Failed, Canceled, Completed }
    [Serializable] public sealed class RelayNodeDefinition
    {
        public string id;
        public RelayPlayerState playerState;
        public ActivationMethod methods=ActivationMethod.Cast;
        public float radius=4, delay, window=10, warning=2, airPowerReward;
        public bool required=true;
        public RelayJoin prerequisites=RelayJoin.Any;
        public bool overrideExpiryReset;
        public RelayReset expiryReset;
        public List<string> destinations=new List<string>();
    }
    [Serializable] public sealed class RelayDefinition
    {
        public string challengeId,regionId;
        public RelayPattern pattern;
        public RelayReset reset;
        public bool timerOnEntry=true;
        public float overallSeconds;
        public int retryLimit=-1;
        public List<RelayNodeDefinition> nodes=new List<RelayNodeDefinition>();
        public List<string> Validate()
        {
            var errors=new List<string>();var ids=new HashSet<string>();
            if(string.IsNullOrWhiteSpace(challengeId))errors.Add("Challenge binding is required.");
            if(nodes==null||nodes.Count==0){errors.Add("Add at least one relay.");return errors;}
            if(!Finite(overallSeconds)||overallSeconds<0||retryLimit< -1)errors.Add("Invalid timer/retry limit.");
            foreach(var n in nodes)
            {
                if(n==null){errors.Add("Null relay.");continue;}
                if(string.IsNullOrWhiteSpace(n.id)||!ids.Add(n.id))errors.Add("Missing or duplicate relay ID: "+n.id);
                if(n.methods==ActivationMethod.None||((int)n.methods&~31)!=0)errors.Add("Invalid activation methods: "+n.id);
                if(!Finite(n.airPowerReward)||n.airPowerReward<0)errors.Add("Invalid AirPower reward: "+n.id);
                if(!Finite(n.radius)||n.radius<=0||!Finite(n.delay)||n.delay<0||!Finite(n.window)||n.window<0||!Finite(n.warning)||n.warning<0)errors.Add("Invalid range/timing: "+n.id);
            }
            if(errors.Count>0)return errors;
            bool required=false;foreach(var n in nodes){required|=n.required;foreach(var dest in n.destinations)if(!ids.Contains(dest)||dest==n.id)errors.Add("Invalid link "+n.id+" -> "+dest);}
            if(!required)errors.Add("At least one completion-contributing relay is required.");
            if(pattern==RelayPattern.Graph&&errors.Count==0)
            {
                var degree=new Dictionary<string,int>();foreach(var n in nodes)degree[n.id]=0;
                foreach(var n in nodes)foreach(var dest in n.destinations)degree[dest]++;
                var queue=new Queue<string>();foreach(var n in nodes)if(degree[n.id]==0)queue.Enqueue(n.id);
                int visited=0;while(queue.Count>0){string id=queue.Dequeue();visited++;var n=nodes.Find(x=>x.id==id);foreach(var dest in n.destinations)if(--degree[dest]==0)queue.Enqueue(dest);}
                if(visited!=nodes.Count)errors.Add("Graph contains a cycle; use a directed acyclic relay graph.");
            }
            return errors;
        }
        internal static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    [CreateAssetMenu(menuName="Windrunner/Quests/Wind Relay",fileName="WindRelay")]
    public sealed class WindRelayProfile:ScriptableObject { public RelayDefinition definition=new RelayDefinition(); }
}
