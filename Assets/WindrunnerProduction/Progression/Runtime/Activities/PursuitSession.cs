using System;
using UnityEngine;
namespace Windrunner.Production.Progression.Activities
{
    public sealed class PursuitSession
    {
        readonly PursuitDefinition definition;
        readonly PursuitRoute route;
        readonly NearMissTracker miss;
        readonly Func<float,bool> complete;

        readonly System.Random random;
        Vector3 previousPlayer;
        float wait,reaction,burst;
        bool currentCapturable=true,currentReverse=true,reactionPending;
        int direction;
        public event Action<int,string> WaypointReached;
        public PursuitState State {get;private set;}=PursuitState.Ready;
        public Vector3 Position {get;private set;}
        public Vector3 Tangent {get;private set;}
        public float Distance {get;private set;}
        public float Speed {get;private set;}
        public float Elapsed {get;private set;}
        public bool Airborne {get;private set;}
        public int Direction=>direction;
        public int Reactions=>miss.Reactions;
        public PursuitRoute Route=>route;
        public PursuitSession(PursuitDefinition source,Func<float,bool> completion,Func<Vector3,Vector3> groundProjection=null,int seed=1749)
        {
            definition=JsonUtility.FromJson<PursuitDefinition>(JsonUtility.ToJson(source));route=new PursuitRoute(definition,64,groundProjection);miss=new NearMissTracker(definition);
            complete=completion??throw new ArgumentNullException(nameof(completion));random=new System.Random(seed);Sample();
        }
        public bool Begin(Vector3 player)
        {
            if(State==PursuitState.Pursuing||State==PursuitState.Waiting||State==PursuitState.Reacting||State==PursuitState.Captured)return false;
            Distance=Mathf.Clamp(definition.startDistance,0,route.Length);direction=definition.reverse?-1:1;Speed=0;Elapsed=0;wait=definition.startDelay;burst=0;reaction=0;
            previousPlayer=player;reactionPending=false;miss.Reset();State=wait>0?PursuitState.Waiting:PursuitState.Pursuing;Sample();return true;
        }
        public void Cancel(){if(State!=PursuitState.Captured)State=PursuitState.Canceled;}
        public void Tick(float delta,Vector3 player)
        {
            if(!RelayDefinition.Finite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
            if(State!=PursuitState.Pursuing&&State!=PursuitState.Reacting&&State!=PursuitState.Waiting)return;
            if(delta==0){previousPlayer=player;return;}
            bool teleported=Vector3.Distance(previousPlayer,player)>definition.teleportDistance;
            // Bound curve chord error and independently sweep relative player/target displacement each substep.
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta*120));float dt=delta/steps;Vector3 initialPlayer=previousPlayer;
            for(int i=0;i<steps;i++)
            {
                Vector3 p0=Vector3.Lerp(initialPlayer,player,(float)i/steps),p1=Vector3.Lerp(initialPlayer,player,(float)(i+1)/steps),old=Position;
                Elapsed+=dt;
                if((definition.maxSeconds>0&&Elapsed>=definition.maxSeconds)||(definition.escapeDistance>0&&Vector3.Distance(p1,Position)>definition.escapeDistance)){State=PursuitState.Escaped;break;}
                if(wait>0){wait=Mathf.Max(0,wait-dt);if(wait==0)State=reactionPending?PursuitState.Reacting:PursuitState.Pursuing;}
                else if(State==PursuitState.Reacting)
                {
                    float nextSpeed=Mathf.MoveTowards(Speed,0,definition.deceleration*dt);Move((Speed+nextSpeed)*.5f*dt);Speed=nextSpeed;Sample();reaction-=dt;
                    if(reaction<=0&&Speed<=.0001f&&State!=PursuitState.Escaped)
                    {
                        if(currentReverse&&(definition.evasion==Evasion.Reverse||definition.evasion==Evasion.ReverseAndBurst))direction=-direction;
                        if(definition.evasion==Evasion.Burst||definition.evasion==Evasion.ReverseAndBurst)burst=definition.burstSeconds;
                        reactionPending=false;State=PursuitState.Pursuing;Sample();
                    }
                }
                else
                {
                    route.Evaluate(Distance,out _,out int segment);var point=definition.points[segment];
                    float desired=point.speedOverride>=0?point.speedOverride:definition.speed;
                    if(definition.distanceResponse)desired*=definition.response.Evaluate(Vector3.Distance(p1,Position));
                    float acceleration=definition.acceleration;
                    if(burst>0){desired=definition.burstSpeed;acceleration=definition.burstAcceleration;burst=Mathf.Max(0,burst-dt);}
                    desired=Mathf.Clamp(desired,definition.minSpeed,definition.maxSpeed);
                    float next=Mathf.MoveTowards(Speed,desired,(desired>Speed?acceleration:definition.deceleration)*dt);
                    float travel=(Speed+next)*.5f*dt;Speed=next;Move(travel);Sample();
                }
                Vector3 relative0=p0-old,relative1=p1-Position;
                if(!teleported&&currentCapturable&&(definition.capture&ActivationMethod.Proximity)!=0&&NearMissTracker.SweptDistance(relative0,relative1)<=definition.captureRadius)
                {if(Capture()){break;}}
                if(State==PursuitState.Escaped||State==PursuitState.Captured)break;
                if(miss.Step(relative0,relative1,dt,teleported,currentReverse&&State==PursuitState.Pursuing,(float)random.NextDouble()))
                {if(definition.evasion!=Evasion.None){reactionPending=true;State=PursuitState.Reacting;reaction=definition.reactionDelay;}}
            }
            previousPlayer=player;
        }
        public bool TryCapture(ActivationMethod method,Vector3 player,bool authoritativeHit=false)
        {
            if(State!=PursuitState.Pursuing&&State!=PursuitState.Reacting&&State!=PursuitState.Waiting)return false;
            if(!currentCapturable||(method&definition.capture)==0)return false;
            bool hitMethod=method==ActivationMethod.Cast||method==ActivationMethod.Sling;
            if(hitMethod&&!authoritativeHit)return false;
            if(!hitMethod&&Vector3.Distance(player,Position)>definition.captureRadius)return false;
            return Capture();
        }
        bool Capture(){if(!complete(Elapsed))return false;State=PursuitState.Captured;Speed=0;return true;}
        void Sample()
        {
            var pos=route.Evaluate(Distance,out var tangent,out int segment);var point=definition.points[segment];
            Airborne=definition.mode==PursuitMode.Air||(definition.mode==PursuitMode.Hybrid&&point.airborne);
            Position=pos;Tangent=tangent*(direction==0?1:direction);currentCapturable=point.capturable;currentReverse=point.allowReversal;
        }
        void Move(float travel)
        {
            int crossings=0;
            while(travel>0)
            {
                if(++crossings>1024)throw new InvalidOperationException("Route crossing budget exceeded: increase route length or reduce extreme speed.");
                float boundary=direction>0?route.Length:0;int waypoint=direction>0&&definition.end!=RouteEnd.Loop?definition.points.Count-1:0;
                for(int i=0;i<route.WaypointDistances.Length;i++)
                {
                    float d=route.WaypointDistances[i];
                    if(direction>0&&d>Distance+.00001f&&d<boundary){boundary=d;waypoint=i;}
                    if(direction<0&&d<Distance-.00001f&&d>boundary){boundary=d;waypoint=i;}
                }
                float available=Mathf.Abs(boundary-Distance),used=Mathf.Min(travel,available);Distance+=direction*used;travel-=used;
                if(used<available)break;
                var node=definition.points[waypoint];WaypointReached?.Invoke(waypoint,node.eventId);
                wait=node.pause;
                if(Distance<=0||Distance>=route.Length)
                {
                    if(definition.end==RouteEnd.Stop){State=PursuitState.Escaped;Speed=0;break;}
                    if(definition.end==RouteEnd.Loop)Distance=direction>0?0:route.Length;else direction=-direction;
                }
                if(wait>0){State=PursuitState.Waiting;Speed=0;break;}
                if(available==0&&route.Length<.00001f)break;
            }
        }
    }
}
