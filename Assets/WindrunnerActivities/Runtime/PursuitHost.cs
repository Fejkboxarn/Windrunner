using System;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.Events;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Activities
{
    [DefaultExecutionOrder(11000)]
    public sealed class PursuitHost:ProgressionInteraction
    {
        public PursuitProfile profile;
        public ProgressionActor actor;
        public Transform target;
        public AirCaster caster;
        public AirSlingController sling;
        public AirNode castNode;
        public AirSlingNode slingNode;
        public LayerMask groundLayers=1;
        public float groundRayHeight=50,groundOffset=.6f;
        public bool startOnEnable=true;
        public string requiredActiveQuest;
        public UnityEvent<int> stateChanged;
        public UnityEvent<string> waypointEvent;
        public PursuitSession Session {get;private set;}
        public string Error {get;private set;}
        ProgressionService owner;
        ActivityProgression completion;
        PursuitState presented=(PursuitState)(-1);
        Vector3 originalPosition;Quaternion originalRotation;
        bool capturedTransform;
        readonly RaycastHit[] groundHits=new RaycastHit[32];
        public Transform ObjectiveAnchor=>Session!=null&&(Session.State==PursuitState.Pursuing||Session.State==PursuitState.Reacting||Session.State==PursuitState.Waiting)?target:null;
        bool CanRun=>actor&&actor.Service!=null&&!actor.Service.IsDisposed&&(string.IsNullOrEmpty(requiredActiveQuest)||actor.Service.Status(requiredActiveQuest)==QuestStatus.Active);
        void OnEnable(){if(caster)caster.PulseHit+=Cast;if(sling)sling.NodeImpact+=Sling;}
        void Start()
        {
            // A completed quest rejects Begin before activity discovery. Restore terminal visibility
            // independently so scene recreation cannot expose its previously captured target.
            if(target&&actor&&actor.Service!=null&&!actor.Service.IsDisposed&&!string.IsNullOrEmpty(requiredActiveQuest)
                &&actor.Service.Status(requiredActiveQuest)==QuestStatus.Completed)target.gameObject.SetActive(false);
            if(startOnEnable)Begin();
        }
        public bool Begin()
        {
            if(!isActiveAndEnabled||!profile||!target||!CanRun||actor.Input?.IsGameplayBlocked==true)return false;
            var body=target.GetComponent<Rigidbody>();var animator=target.GetComponent<Animator>();
            if((body&&!body.isKinematic)||(animator&&animator.applyRootMotion)){Error="Pursuit owns target position; disable dynamic Rigidbody motion and Animator root motion.";return false;}
            if(target==transform||target==actor.transform||target.IsChildOf(actor.transform)){Error="Target must be a separate object; player motion cannot be bound.";return false;}
            if((transform.lossyScale-Vector3.one).sqrMagnitude>.0001f){Error="Pursuit root must use unit scale; route distances are metres.";return false;}
            if(Session!=null&&(Session.State==PursuitState.Pursuing||Session.State==PursuitState.Reacting||Session.State==PursuitState.Waiting))return false;
            try
            {
                if(!capturedTransform){originalPosition=target.position;originalRotation=target.rotation;capturedTransform=true;}
                owner=actor.Service;completion=new ActivityProgression(owner,profile.definition.challengeId,profile.definition.regionId);if(!completion.Begin()){target.gameObject.SetActive(false);return false;}
                target.gameObject.SetActive(true);
                Session=new PursuitSession(profile.definition,completion.Complete,Ground);Session.WaypointReached+=Waypoint;
                Error=null;bool result=Session.Begin(transform.InverseTransformPoint(actor.Position));Apply();return result;
            }
            catch(Exception e){Error=e.Message;Debug.LogException(e,this);return false;}
        }
        public void BeginActivity()=>Begin();
        Vector3 Ground(Vector3 local)
        {
            Vector3 world=transform.TransformPoint(local);
            int count=Physics.RaycastNonAlloc(world+Vector3.up*groundRayHeight,Vector3.down,groundHits,groundRayHeight*2,groundLayers,QueryTriggerInteraction.Ignore);
            if(count==groundHits.Length)throw new InvalidOperationException("Ground query saturated; narrow the authored ground mask.");
            float nearest=float.PositiveInfinity;Vector3 point=default;
            for(int i=0;i<count;i++){var hit=groundHits[i];if(hit.transform.IsChildOf(target)||(actor&&hit.transform.IsChildOf(actor.transform)))continue;if(hit.distance<nearest){nearest=hit.distance;point=hit.point;}}
            if(float.IsInfinity(nearest))throw new InvalidOperationException("Pursuit route has no ground at "+world);
            world.y=point.y+groundOffset;return transform.InverseTransformPoint(world);
        }
        void LateUpdate()
        {
            if(Session==null)return;
            if(Session.State==PursuitState.Captured){Apply();return;}
            if(!target||!CanRun||actor.Service!=owner||(!target.gameObject.activeInHierarchy&&Session.State!=PursuitState.Captured)){Cancel();return;}
            if(actor.Input?.IsGameplayBlocked==true)return;
            try{Session.Tick(Time.deltaTime,transform.InverseTransformPoint(actor.Position));Apply();}
            catch(Exception e){Error=e.Message;Cancel();Debug.LogException(e,this);}
        }
        void Apply()
        {
            if(!target||Session==null)return;target.position=transform.TransformPoint(Session.Position);
            Vector3 tangent=transform.TransformDirection(Session.Tangent);
            if(tangent.sqrMagnitude>1e-8f)target.rotation=Quaternion.RotateTowards(target.rotation,Quaternion.LookRotation(tangent,Vector3.up),profile.definition.turnDegrees*Time.deltaTime);
            if(Session.State==PursuitState.Captured&&(!sling||!sling.IsSlinging||sling.CurrentTarget!=slingNode))target.gameObject.SetActive(false);
            if(presented!=Session.State){presented=Session.State;stateChanged?.Invoke((int)presented);}
        }
        void Waypoint(int index,string id)
        {
            if(!string.IsNullOrEmpty(id)){completion.Credit(id,"waypoint/"+index);waypointEvent?.Invoke(id);}
        }
        void Cast(AirNode node,float charge){if(node==castNode)Capture(ActivationMethod.Cast,true);}
        void Sling(AirSlingNode node){if(node==slingNode)Capture(ActivationMethod.Sling,true);}
        public bool Capture(ActivationMethod method,bool authoritativeHit=false)
        {
            if(!CanRun||actor.Service!=owner||actor.Input?.IsGameplayBlocked==true||Session==null)return false;
            try{bool result=Session.TryCapture(method,transform.InverseTransformPoint(actor.Position),authoritativeHit);Apply();return result;}
            catch(Exception e){Error=e.Message;Debug.LogException(e,this);return false;}
        }
        public override string RejectionReason(ProgressionActor source,InteractionChannel channel)
        {
            if(!isActiveAndEnabled||actor!=source||!target||!CanRun||source.Input?.IsGameplayBlocked==true)return "Pursuit unavailable.";
            if(!profile||(profile.definition.capture&ActivationMethod.Interact)==0)return "Interact capture disabled.";
            if(Vector3.Distance(source.Position,target.position)>profile.definition.captureRadius)return "Move closer.";
            return ObjectiveAnchor?null:"Pursuit not active.";
        }
        public override bool TryUse(ProgressionActor source,InteractionChannel channel,string occurrence=null)=>channel==InteractionChannel.Input&&RejectionReason(source,channel)==null&&Capture(ActivationMethod.Interact);
        public void Cancel(){Session?.Cancel();if(capturedTransform&&target&&Session?.State!=PursuitState.Captured)target.SetPositionAndRotation(originalPosition,originalRotation);}
        void OnDisable(){if(caster)caster.PulseHit-=Cast;if(sling)sling.NodeImpact-=Sling;Cancel();}
    }
}
