using System;
using System.Collections.Generic;
using UnityEngine;
namespace Windrunner.Production.Progression.Activities
{
    /// <summary>Immutable arc-length polyline cache. Curves bake once; runtime sampling is binary search, without allocations.</summary>
    public sealed class PursuitRoute
    {
        readonly Vector3[] positions;
        readonly float[] distances;
        readonly int[] segments;
        public readonly float[] WaypointDistances;
        public float Length=>distances[distances.Length-1];
        public PursuitRoute(PursuitDefinition definition,int subdivisions=64,Func<Vector3,Vector3> groundProjection=null)
        {
            if(subdivisions<2)throw new ArgumentOutOfRangeException(nameof(subdivisions));
            var errors=definition.Validate();if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            var p=new List<Vector3>();var d=new List<float>();var s=new List<int>();WaypointDistances=new float[definition.points.Count];
            var anchors=new Vector3[definition.points.Count];
            for(int i=0;i<anchors.Length;i++)
            {
                var point=definition.points[i];bool groundAnchor=definition.mode==PursuitMode.Ground;
                if(definition.mode==PursuitMode.Hybrid)
                {
                    int previous=i>0?i-1:definition.end==RouteEnd.Loop?anchors.Length-1:-1;
                    groundAnchor=!point.airborne||(previous>=0&&!definition.points[previous].airborne);
                }
                anchors[i]=groundAnchor&&groundProjection!=null?groundProjection(point.position):point.position;
            }
            p.Add(anchors[0]);d.Add(0);s.Add(0);float length=0;
            int count=definition.end==RouteEnd.Loop?definition.points.Count:definition.points.Count-1;
            for(int i=0;i<count;i++)
            {
                var a=definition.points[i];var b=definition.points[(i+1)%definition.points.Count];Vector3 start=anchors[i],end=anchors[(i+1)%anchors.Length];
                bool onGround=definition.mode==PursuitMode.Ground||(definition.mode==PursuitMode.Hybrid&&!a.airborne);
                int steps=a.curved||onGround?subdivisions:1;
                for(int j=1;j<=steps;j++)
                {
                    float t=(float)j/steps,u=1-t;
                    Vector3 pos=a.curved?u*u*u*start+3*u*u*t*(start+a.outgoing)+3*u*t*t*(end+b.incoming)+t*t*t*end:Vector3.Lerp(start,end,t);
                    if(onGround&&groundProjection!=null)pos=groundProjection(pos);
                    float delta=Vector3.Distance(p[p.Count-1],pos);if(delta<.000001f)continue;
                    length+=delta;p.Add(pos);d.Add(length);s.Add(i);
                }
                if(i+1<WaypointDistances.Length)WaypointDistances[i+1]=length;
            }
            if(length<=.000001f)throw new ArgumentException("Route has zero length.");
            positions=p.ToArray();distances=d.ToArray();segments=s.ToArray();
        }
        public Vector3 Evaluate(float distance,out Vector3 tangent,out int segment)
        {
            distance=Mathf.Clamp(distance,0,Length);int lo=1,hi=distances.Length-1;
            while(lo<hi){int mid=(lo+hi)/2;if(distances[mid]<=distance)lo=mid+1;else hi=mid;}
            int i=lo;float t=(distance-distances[i-1])/(distances[i]-distances[i-1]);
            tangent=(positions[i]-positions[i-1]).normalized;segment=segments[i];return Vector3.LerpUnclamped(positions[i-1],positions[i],t);
        }
    }
    /// <summary>Relative-motion attempt with swept closest approach, exit hysteresis and capture precedence.</summary>
    public sealed class NearMissTracker
    {
        readonly PursuitDefinition config;
        bool tracking,armed=true;
        float closest,duration,cooldown;
        public int Reactions {get;private set;}
        public NearMissTracker(PursuitDefinition config){this.config=config;}
        public void Reset(){tracking=false;armed=true;closest=float.PositiveInfinity;duration=0;cooldown=0;Reactions=0;}
        public static float SweptDistance(Vector3 a,Vector3 b)
        {Vector3 v=b-a;float t=v.sqrMagnitude>1e-10f?Mathf.Clamp01(-Vector3.Dot(a,v)/v.sqrMagnitude):0;return (a+v*t).magnitude;}
        public bool Step(Vector3 previous,Vector3 current,float delta,bool captured,bool allowed,float random)
        {
            if(delta<=0)return false;cooldown=Mathf.Max(0,cooldown-delta);
            if(captured||!allowed){tracking=false;armed=false;return false;}
            float now=current.magnitude,before=previous.magnitude;
            if(now>config.missRadius+config.retreatDistance&&!tracking)armed=true;
            if(!config.nearMiss||cooldown>0||Reactions>=config.maxReactions)return false;
            float approach=(before-now)/delta;
            float sweep=SweptDistance(previous,current);
            // Swept pass-through may end farther away than it started: initial relative velocity still approaches.
            float initialClosing=-Vector3.Dot(previous,(current-previous)/delta)/Mathf.Max(before,.001f);
            if(!tracking&&armed&&sweep<=config.missRadius&&initialClosing>=config.approachSpeed)
            {tracking=true;armed=false;closest=sweep;duration=0;}
            if(!tracking)return false;
            closest=Mathf.Min(closest,sweep);duration+=delta;
            bool retreat=now>=closest+config.retreatDistance&&Vector3.Dot(current,current-previous)>0;
            if(!retreat)return false;
            tracking=false;
            if(duration<config.minimumApproachTime)return false;
            cooldown=config.cooldown;
            if(config.reactionProbability<=0||(config.reactionProbability<1&&random>=config.reactionProbability))return false;
            Reactions++;return true;
        }
    }
}
