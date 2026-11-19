using System;
using System.Collections.Generic;
using UnityEngine;
namespace Windrunner.Production.Progression.Activities
{
    public enum PursuitMode { Ground, Air, Hybrid }
    public enum RouteEnd { Stop, Loop, PingPong }
    public enum Evasion { None, Reverse, Burst, ReverseAndBurst }
    public enum PursuitState { Ready, Waiting, Pursuing, Reacting, Captured, Escaped, Canceled }
    [Serializable] public sealed class PursuitWaypoint
    {
        public Vector3 position;
        public Vector3 incoming=new Vector3(0,0,-2), outgoing=new Vector3(0,0,2);
        public bool curved,airborne,allowReversal=true,capturable=true;
        public float speedOverride=-1,pause;
        public string eventId;
    }
    [Serializable] public sealed class PursuitDefinition
    {
        public string challengeId,regionId;
        public PursuitMode mode;
        public RouteEnd end=RouteEnd.PingPong;
        public List<PursuitWaypoint> points=new List<PursuitWaypoint>();
        public float speed=6,minSpeed,maxSpeed=30,acceleration=10,deceleration=15,turnDegrees=180;
        public float startDistance,startDelay,maxSeconds=90,escapeDistance;
        public bool reverse;
        public bool distanceResponse;
        public AnimationCurve response=AnimationCurve.Linear(0,1,30,1);
        public ActivationMethod capture=ActivationMethod.Proximity;
        public float captureRadius=1.5f,teleportDistance=40;
        public bool nearMiss=true;
        public float missRadius=4,approachSpeed=1,retreatDistance=0.8f,minimumApproachTime,reactionDelay=.3f,reactionProbability=1,cooldown=2;
        public int maxReactions=5;
        public Evasion evasion=Evasion.Reverse;
        public float burstSpeed=15,burstAcceleration=20,burstSeconds=2;
        public List<string> Validate()
        {
            var errors=new List<string>();if(string.IsNullOrWhiteSpace(challengeId))errors.Add("Challenge binding is required.");
            if(points==null||points.Count<2){errors.Add("At least two route points required.");return errors;}
            foreach(float f in new[]{speed,minSpeed,maxSpeed,acceleration,deceleration,turnDegrees,startDistance,startDelay,maxSeconds,escapeDistance,captureRadius,teleportDistance,missRadius,approachSpeed,retreatDistance,minimumApproachTime,reactionDelay,reactionProbability,cooldown,burstSpeed,burstAcceleration,burstSeconds})if(!RelayDefinition.Finite(f)||f<0){errors.Add("All numeric settings must be finite and nonnegative.");break;}
            if(nearMiss&&deceleration<=0)errors.Add("Near-miss reactions need positive deceleration.");
            if(minSpeed>maxSpeed||speed>maxSpeed||speed<minSpeed)errors.Add("Speed must be inside min/max bounds.");
            if(capture==ActivationMethod.None||((int)capture&~31)!=0||captureRadius<=0)errors.Add("Choose valid capture methods and positive radius.");
            if(nearMiss&&(missRadius<=captureRadius||teleportDistance<=missRadius*2||reactionProbability>1||maxReactions<0))errors.Add("Near-miss radius/teleport/probability/count invalid.");
            bool distance=false;for(int i=0;i<points.Count;i++)
            {
                var p=points[i];if(p==null){errors.Add("Null waypoint.");continue;}
                if(!Finite(p.position)||!Finite(p.incoming)||!Finite(p.outgoing)||!RelayDefinition.Finite(p.pause)||p.pause<0||!RelayDefinition.Finite(p.speedOverride)||p.speedOverride< -1)errors.Add("Invalid waypoint values.");
                if(i>0&&points[i-1]!=null)distance|=(p.position-points[i-1].position).sqrMagnitude>.000001f;
            }
            if(!distance)errors.Add("Route has no length.");return errors;
        }
        static bool Finite(Vector3 p)=>RelayDefinition.Finite(p.x)&&RelayDefinition.Finite(p.y)&&RelayDefinition.Finite(p.z);
    }
    [CreateAssetMenu(menuName="Windrunner/Quests/Dynamic Pursuit",fileName="Pursuit")]
    public sealed class PursuitProfile:ScriptableObject {public GameObject targetPrefab; public PursuitDefinition definition=new PursuitDefinition();}
}
