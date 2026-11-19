using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Progression.Activities
{
    /// <summary>Gameplay-scaled time; expiry precedes same-tick input. No scene or player movement ownership.</summary>
    public sealed class WindRelaySession
    {
        readonly RelayDefinition definition;
        readonly Func<float,bool> complete;
        readonly Action<string,string> credit;
        readonly Dictionary<string,int> indexes=new Dictionary<string,int>();
        readonly List<int>[] parents;
        readonly RelayState[] states;
        readonly float[] remaining;
        readonly HashSet<string> interactions=new HashSet<string>();
        string attempt;
        bool clockStarted;
        RelayReset failureReset;
        int retries;
        public ActivityState State {get;private set;}=ActivityState.Ready;
        public float Elapsed {get;private set;}
        public int Count=>states.Length;
        public RelayState NodeState(int index)=>states[index];
        public float Remaining(int index)=>remaining[index];
        public WindRelaySession(RelayDefinition source,Func<float,bool> completion,Action<string,string> nodeCredit=null)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            var errors=source.Validate();if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            definition=JsonUtility.FromJson<RelayDefinition>(JsonUtility.ToJson(source));complete=completion??throw new ArgumentNullException(nameof(completion));credit=nodeCredit;
            int count=definition.nodes.Count;states=new RelayState[count];remaining=new float[count];parents=new List<int>[count];
            for(int i=0;i<count;i++){indexes.Add(definition.nodes[i].id,i);parents[i]=new List<int>();}
            for(int i=0;i<count;i++)foreach(string id in definition.nodes[i].destinations)parents[indexes[id]].Add(i);
        }
        public bool Begin()
        {
            if(State==ActivityState.Active||State==ActivityState.Completed)return false;
            if(State!=ActivityState.Ready&&definition.retryLimit>=0&&retries>=definition.retryLimit)return false;
            bool keep=State==ActivityState.Failed&&failureReset==RelayReset.RetainActivated;
            if(State!=ActivityState.Ready)retries++;
            for(int i=0;i<Count;i++)if(!keep||states[i]!=RelayState.Activated)states[i]=RelayState.Inactive;
            interactions.Clear();attempt=Guid.NewGuid().ToString("N");Elapsed=0;clockStarted=definition.timerOnEntry;State=ActivityState.Active;Refresh();return true;
        }
        public void Cancel(){if(State==ActivityState.Active)State=ActivityState.Canceled;}
        public void Tick(float delta)
        {
            if(!RelayDefinition.Finite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
            if(State!=ActivityState.Active||delta==0)return;
            if(clockStarted)Elapsed+=delta;
            if(definition.overallSeconds>0&&Elapsed>=definition.overallSeconds){Expire(-1);return;}
            for(int i=0;i<Count;i++)
            {
                float budget=delta;
                if(states[i]==RelayState.Delayed)
                {
                    float used=Mathf.Min(remaining[i],budget);remaining[i]-=used;budget-=used;
                    if(remaining[i]>0)continue;states[i]=RelayState.Available;remaining[i]=definition.nodes[i].window;
                }
                if((states[i]==RelayState.Available||states[i]==RelayState.Expiring)&&definition.nodes[i].window>0)
                {
                    remaining[i]=Mathf.Max(0,remaining[i]-budget);
                    if(remaining[i]<=0){Expire(i);return;}
                    if(remaining[i]<=definition.nodes[i].warning)states[i]=RelayState.Expiring;
                }
            }
        }
        public bool Activate(string id,ActivationMethod method,string ticket)
        {
            if(State!=ActivityState.Active||!indexes.TryGetValue(id,out int i)||string.IsNullOrWhiteSpace(ticket)||interactions.Contains(ticket))return false;
            var n=definition.nodes[i];
            if((states[i]!=RelayState.Available&&states[i]!=RelayState.Expiring)||(method&n.methods)==0||method==ActivationMethod.None)return false;
            // Commit objective credit before changing local state. A failed write leaves activation retryable.
            credit?.Invoke(id,attempt+"/"+id);
            bool done=true;
            for(int j=0;j<Count;j++)if(j!=i&&definition.nodes[j].required&&states[j]!=RelayState.Activated){done=false;break;}
            if(done&&!complete(Elapsed))return false;
            interactions.Add(ticket);states[i]=RelayState.Activated;clockStarted=true;
            if(done){State=ActivityState.Completed;for(int j=0;j<Count;j++)states[j]=RelayState.Completed;}
            else Refresh();return true;
        }
        void Expire(int index){failureReset=index>=0&&definition.nodes[index].overrideExpiryReset?definition.nodes[index].expiryReset:definition.reset;State=ActivityState.Failed;for(int i=0;i<Count;i++)if(states[i]!=RelayState.Activated)states[i]=RelayState.Expired;}
        void Refresh()
        {
            for(int i=0;i<Count;i++)
            {
                if(states[i]!=RelayState.Inactive)continue;
                bool eligible=definition.pattern==RelayPattern.Unordered;
                if(definition.pattern==RelayPattern.Sequential)eligible=i==0||states[i-1]==RelayState.Activated;
                if(definition.pattern==RelayPattern.Graph)
                {
                    eligible=parents[i].Count==0||definition.nodes[i].prerequisites==RelayJoin.All;
                    foreach(int p in parents[i])if(definition.nodes[i].prerequisites==RelayJoin.All)eligible&=states[p]==RelayState.Activated;else eligible|=states[p]==RelayState.Activated;
                }
                if(!eligible)continue;
                states[i]=definition.nodes[i].delay>0?RelayState.Delayed:RelayState.Available;
                remaining[i]=definition.nodes[i].delay>0?definition.nodes[i].delay:definition.nodes[i].window;
            }
        }
    }
}
