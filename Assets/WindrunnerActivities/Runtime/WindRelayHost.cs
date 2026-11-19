using System;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.Events;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Activities
{
    public sealed class WindRelayHost:MonoBehaviour
    {
        public WindRelayProfile profile;
        public ProgressionActor actor;
        public AirCaster caster;
        public AirPower airPower;
        public PlayerMotor motor;
        public AirSlingController sling;
        public RelayInteraction[] nodes=Array.Empty<RelayInteraction>();
        public bool startOnEnable=true;
        public string requiredActiveQuest;
        public UnityEvent<int> stateChanged;
        public WindRelaySession Session {get;private set;}
        public string Error {get;private set;}
        ProgressionService owner;
        ActivityProgression completion;
        int[] nodeIndexes;
        float[] nodeRewards;
        string[] proximityTickets;
        RelayPlayerState[] activationStates;
        ActivityState presented=(ActivityState)(-1);
        public bool CanRun=>actor!=null&&actor.Service!=null&&!actor.Service.IsDisposed&&(string.IsNullOrEmpty(requiredActiveQuest)||actor.Service.Status(requiredActiveQuest)==QuestStatus.Active);
        void OnEnable(){if(caster)caster.PulseHit+=Cast;if(sling)sling.NodeImpact+=Sling;}
        void Start(){if(startOnEnable)Begin();}
        public bool Begin()
        {
            if(!isActiveAndEnabled||!profile||!CanRun||actor.Input?.IsGameplayBlocked==true)return false;
            try
            {
                if(Session?.State==ActivityState.Completed&&owner==actor.Service&&owner.ChallengeAvailable(profile.definition.challengeId))Session=null;
                if(Session==null||owner!=actor.Service)
                {
                    owner=actor.Service;completion=new ActivityProgression(owner,profile.definition.challengeId,profile.definition.regionId);
                    if(!completion.Begin())return false;
                    Session=new WindRelaySession(profile.definition,completion.Complete,completion.Credit);
                    nodeIndexes=new int[nodes.Length];nodeRewards=new float[nodes.Length];proximityTickets=new string[nodes.Length];activationStates=new RelayPlayerState[nodes.Length];
                    for(int i=0;i<nodes.Length;i++)proximityTickets[i]="proximity/"+i;
                    for(int i=0;i<nodes.Length;i++){nodeIndexes[i]=-1;for(int j=0;j<profile.definition.nodes.Count;j++)if(nodes[i]&&profile.definition.nodes[j].id==nodes[i].relayId){nodeIndexes[i]=j;activationStates[i]=profile.definition.nodes[j].playerState;nodeRewards[i]=profile.definition.nodes[j].airPowerReward;nodes[i].range=profile.definition.nodes[j].radius;break;}}
                }
                bool result=Session.Begin();Present();return result;
            }
            catch(Exception e){Error=e.Message;Debug.LogException(e,this);return false;}
        }
        public bool Activate(RelayInteraction node,ActivationMethod method,string ticket)
        {
            if(!isActiveAndEnabled||!CanRun||owner!=actor.Service||actor.Input?.IsGameplayBlocked==true||Session==null||!node||!node.isActiveAndEnabled)return false;
            int binding=Array.IndexOf(nodes,node);if(binding<0||!PlayerStateValid(binding))return false;
            try{bool result=Session.Activate(node.relayId,method,ticket);if(result&&airPower){for(int i=0;i<nodes.Length;i++)if(nodes[i]==node&&nodeIndexes[i]>=0){airPower.Restore(nodeRewards[i]);break;}}Present();return result;}
            catch(Exception e){Error=e.Message;Debug.LogException(e,this);return false;}
        }
        public void BeginActivity()=>Begin();
        bool PlayerStateValid(int index)=>activationStates[index]==RelayPlayerState.Any||(motor&&(activationStates[index]==RelayPlayerState.Grounded?motor.IsGrounded:!motor.IsGrounded));
        void Cast(AirNode node,float charge){foreach(var n in nodes)if(n&&n.castNode==node)Activate(n,ActivationMethod.Cast,Guid.NewGuid().ToString("N"));}
        void Sling(AirSlingNode node){foreach(var n in nodes)if(n&&n.slingNode==node)Activate(n,ActivationMethod.Sling,Guid.NewGuid().ToString("N"));}
        void Update()
        {
            if(Session==null)return;
            if(!CanRun||owner!=actor.Service){Session.Cancel();Present();return;}
            if(actor.Input?.IsGameplayBlocked==true)return;
            Session.Tick(Time.deltaTime);
            for(int i=0;i<nodes.Length;i++)
            {
                var n=nodes[i];if(!n||!n.isActiveAndEnabled)continue;
                if((actor.Position-n.transform.position).sqrMagnitude<=n.range*n.range)Activate(n,ActivationMethod.Proximity,proximityTickets[i]);
            }
            Present();
        }
        void Present()
        {
            if(Session==null)return;
            if(presented!=Session.State){presented=Session.State;stateChanged?.Invoke((int)presented);}
            for(int i=0;i<nodes.Length;i++)if(nodes[i])
            {
                int index=nodeIndexes!=null&&i<nodeIndexes.Length?nodeIndexes[i]:-1;
                var state=index>=0?Session.NodeState(index):RelayState.Invalid;
                if((state==RelayState.Available||state==RelayState.Expiring)&&!PlayerStateValid(i))state=RelayState.Invalid;
                nodes[i].Present(state,index>=0?Session.Remaining(index):0);
            }
        }
        public void Cancel(){Session?.Cancel();Present();}
        void OnDisable(){if(caster)caster.PulseHit-=Cast;if(sling)sling.NodeImpact-=Sling;Cancel();}
        public int CopyAvailableAnchors(Transform[] buffer)
        {
            int count=0;if(Session==null||Session.State!=ActivityState.Active)return 0;
            foreach(var n in nodes)if(n&&(n.State==RelayState.Available||n.State==RelayState.Expiring)&&count<buffer.Length)buffer[count++]=n.transform;
            return count;
        }
    }
}
