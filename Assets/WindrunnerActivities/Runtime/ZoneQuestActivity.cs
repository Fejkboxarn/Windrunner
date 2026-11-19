using UnityEngine;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Activities
{
    /// <summary>Scene composition only: the existing objective evaluator owns stage eligibility.</summary>
    [DefaultExecutionOrder(12000)]
    public sealed class ZoneQuestActivity : ProgressionInteraction, IObjectiveAnchorProvider
    {
        public ProgressionActor actor;
        public string questId, objectiveId;
        public WindRelayHost relay;
        public PursuitHost pursuit;
        public GameObject[] presentation = System.Array.Empty<GameObject>();
        public Transform turnInAnchor;
        ProgressionService owner;
        bool dirty=true, wasOpen, started;
        readonly Transform[] anchors=new Transform[16];
        public bool ObjectiveOpen => actor && actor.Service!=null && !actor.Service.IsDisposed && actor.Service.ObjectiveOpen(questId,objectiveId);
        public Transform ObjectiveAnchor
        {
            get
            {
                if(actor && actor.Service!=null && !actor.Service.IsDisposed && actor.Service.Status(questId)==QuestStatus.ReadyToTurnIn)return turnInAnchor;
                if(!ObjectiveOpen)return null;
                if(pursuit)return pursuit.ObjectiveAnchor;
                return relay && relay.CopyAvailableAnchors(anchors)>0?anchors[0]:transform;
            }
        }
        void Changed()=>dirty=true;
        void LateUpdate()
        {
            var service=actor?actor.Service:null;
            if(owner!=service){if(owner!=null)owner.Changed-=Changed;owner=service;if(owner!=null)owner.Changed+=Changed;dirty=true;started=false;wasOpen=false;}
            if(!dirty && started)return;
            bool open=ObjectiveOpen;
            if(!open)
            {
                if(wasOpen){relay?.Cancel();pursuit?.Cancel();}
                foreach(var go in presentation)if(go)go.SetActive(false);
                started=false;
            }
            else
            {
                if(!wasOpen)foreach(var go in presentation)if(go)go.SetActive(true);
                if(!started && actor.Input?.IsGameplayBlocked!=true)started=relay?relay.Begin():pursuit && pursuit.Begin();
            }
            wasOpen=open;dirty=false;
        }
        public override string RejectionReason(ProgressionActor source,InteractionChannel channel)
        {
            string reason=base.RejectionReason(source,channel);
            if(reason!=null)return reason;
            return source==actor && ObjectiveOpen?null:"This encounter's objective is not currently open.";
        }
        public override bool TryUse(ProgressionActor source,InteractionChannel channel,string occurrence=null)
            =>channel==InteractionChannel.Input && RejectionReason(source,channel)==null && RestartEncounter();
        [ContextMenu("Restart current attempt (Play Mode; no progress reset)")]
        public bool RestartEncounter()
        {
            if(!Application.isPlaying || !ObjectiveOpen || actor.Input?.IsGameplayBlocked==true)return false;
            relay?.Cancel();pursuit?.Cancel();
            foreach(var go in presentation)if(go)go.SetActive(true);
            started=relay?relay.Begin():pursuit && pursuit.Begin();return started;
        }
        void OnDisable(){if(owner!=null)owner.Changed-=Changed;owner=null;relay?.Cancel();pursuit?.Cancel();dirty=true;started=false;wasOpen=false;}
    }
}
