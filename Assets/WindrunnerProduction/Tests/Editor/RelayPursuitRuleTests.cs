using System;
using NUnit.Framework;
using UnityEngine;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Production.Tests
{
    public sealed class RelayPursuitRuleTests
    {
        static RelayDefinition Relay(RelayPattern pattern=RelayPattern.Sequential)
        {
            var d=new RelayDefinition{challengeId="relay",pattern=pattern};
            foreach(string id in new[]{"a","b","c"})d.nodes.Add(new RelayNodeDefinition{id=id,window=2,methods=ActivationMethod.Cast});
            d.nodes[0].destinations.Add("b");d.nodes[1].destinations.Add("c");return d;
        }
        static PursuitDefinition Route()
        {
            var d=new PursuitDefinition{challengeId="pursuit",mode=PursuitMode.Air,speed=10,acceleration=1000,end=RouteEnd.PingPong,maxSeconds=0};
            d.points.Add(new PursuitWaypoint{position=Vector3.zero});d.points.Add(new PursuitWaypoint{position=new Vector3(0,0,100)});return d;
        }
        [Test] public void RelaySequenceRejectsDuplicatesAndWrongOrder()
        {
            int commits=0;var s=new WindRelaySession(Relay(),_=>{commits++;return true;});Assert.IsTrue(s.Begin());
            Assert.IsFalse(s.Activate("b",ActivationMethod.Cast,"b"));Assert.IsTrue(s.Activate("a",ActivationMethod.Cast,"a"));
            Assert.IsFalse(s.Activate("b",ActivationMethod.Cast,"a"));Assert.IsTrue(s.Activate("b",ActivationMethod.Cast,"b"));Assert.IsTrue(s.Activate("c",ActivationMethod.Cast,"c"));
            Assert.AreEqual(ActivityState.Completed,s.State);Assert.AreEqual(1,commits);Assert.IsFalse(s.Activate("c",ActivationMethod.Cast,"d"));
        }
        [Test] public void RelayUnorderedPartialRetryAndExpiryPrecedence()
        {
            var d=Relay(RelayPattern.Unordered);d.reset=RelayReset.RetainActivated;var s=new WindRelaySession(d,_=>true);s.Begin();
            Assert.IsTrue(s.Activate("c",ActivationMethod.Cast,"c"));s.Tick(2);Assert.AreEqual(ActivityState.Failed,s.State);
            Assert.IsFalse(s.Activate("a",ActivationMethod.Cast,"a"));Assert.IsTrue(s.Begin());Assert.AreEqual(RelayState.Activated,s.NodeState(2));
            Assert.IsTrue(s.Activate("a",ActivationMethod.Cast,"a"));Assert.IsTrue(s.Activate("b",ActivationMethod.Cast,"b"));Assert.AreEqual(ActivityState.Completed,s.State);
        }
        [Test] public void RelayGraphJoinDelayCycleAndFailedCommit()
        {
            var d=Relay(RelayPattern.Graph);d.nodes[0].destinations.Add("c");d.nodes[2].prerequisites=RelayJoin.All;d.nodes[2].delay=1;
            bool durable=false;var s=new WindRelaySession(d,_=>durable);s.Begin();s.Activate("a",ActivationMethod.Cast,"a");Assert.AreEqual(RelayState.Inactive,s.NodeState(2));
            s.Activate("b",ActivationMethod.Cast,"b");Assert.AreEqual(RelayState.Delayed,s.NodeState(2));s.Tick(1);Assert.AreEqual(RelayState.Expiring,s.NodeState(2));
            Assert.IsFalse(s.Activate("c",ActivationMethod.Cast,"c"));durable=true;Assert.IsTrue(s.Activate("c",ActivationMethod.Cast,"c"));
            d.nodes[2].destinations.Add("a");Assert.IsNotEmpty(d.Validate());
        }
        [Test] public void RelayFullResetAndRetryLimits()
        {var d=Relay();d.retryLimit=1;var s=new WindRelaySession(d,_=>true);s.Begin();s.Activate("a",ActivationMethod.Cast,"a");s.Tick(2);Assert.IsTrue(s.Begin());Assert.AreEqual(RelayState.Available,s.NodeState(0));s.Tick(2);Assert.IsFalse(s.Begin());}
        [Test] public void CurvedRouteUsesDistanceAndKeepsAirElevation()
        {
            var d=Route();d.points[0].curved=true;d.points[0].outgoing=new Vector3(40,30,20);d.points[1].incoming=new Vector3(-40,20,-20);var route=new PursuitRoute(d);
            Assert.Greater(route.Length,100);Vector3 previous=route.Evaluate(0,out _,out _);float sum=0;
            for(int i=1;i<=1000;i++){Vector3 p=route.Evaluate(route.Length*i/1000,out _,out _);sum+=Vector3.Distance(p,previous);previous=p;}
            Assert.AreEqual(route.Length,sum,.1);Assert.Greater(route.Evaluate(route.Length*.5f,out _,out _).y,0);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)] public void PursuitDistanceAndGroundProjectionAreFrameRateStable(int fps)
        {
            var d=Route();d.mode=PursuitMode.Ground;var s=new PursuitSession(d,_=>true,p=>new Vector3(p.x,3+p.z*.1f,p.z));s.Begin(new Vector3(50,0,0));
            for(int i=0;i<fps*3;i++)s.Tick(1f/fps,new Vector3(50,0,0));Assert.AreEqual(30,s.Distance,.12);Assert.AreEqual(3+s.Position.z*.1f,s.Position.y,.001);
        }
        [Test] public void SweptCaptureWinsAndTeleportsDoNotCapture()
        {
            var d=Route();d.speed=0;d.maxSeconds=0;int count=0;var s=new PursuitSession(d,_=>{count++;return true;});s.Begin(new Vector3(-10,0,0));s.Tick(.1f,new Vector3(10,0,0));Assert.AreEqual(PursuitState.Captured,s.State);Assert.AreEqual(0,s.Reactions);Assert.AreEqual(1,count);
            s=new PursuitSession(d,_=>true);s.Begin(new Vector3(-100,0,0));s.Tick(.1f,new Vector3(100,0,0));Assert.AreNotEqual(PursuitState.Captured,s.State);
        }
        [Test] public void NearMissRequiresApproachRetreatAndRearmsOutsideRadius()
        {
            var d=Route();var m=new NearMissTracker(d);Assert.IsFalse(m.Step(new Vector3(3,0,0),new Vector3(3,0,0),.1f,false,true,0));
            Assert.IsFalse(m.Step(new Vector3(5,0,0),new Vector3(2,0,0),.1f,false,true,0));Assert.IsTrue(m.Step(new Vector3(2,0,0),new Vector3(4,0,0),.1f,false,true,0));
            Assert.IsFalse(m.Step(new Vector3(4,0,0),new Vector3(2,0,0),.1f,false,true,0));Assert.AreEqual(1,m.Reactions);
            Assert.IsFalse(m.Step(new Vector3(2,0,0),new Vector3(6,0,0),3,false,true,0));Assert.IsFalse(m.Step(new Vector3(6,0,0),new Vector3(2,0,0),.1f,true,true,0));
        }
        [Test] public void PursuitCaptureMethodsCancelAndHybridTransitions()
        {
            var d=Route();d.mode=PursuitMode.Hybrid;d.capture=ActivationMethod.Cast;d.points.Insert(1,new PursuitWaypoint{position=new Vector3(0,10,10),airborne=true});
            var s=new PursuitSession(d,_=>true,p=>new Vector3(p.x,0,p.z));s.Begin(new Vector3(20,0,0));s.Tick(2,new Vector3(20,0,0));Assert.IsTrue(s.Airborne);
            Assert.IsFalse(s.TryCapture(ActivationMethod.Cast,s.Position));Assert.IsTrue(s.TryCapture(ActivationMethod.Cast,s.Position,true));
            Assert.IsFalse(s.TryCapture(ActivationMethod.Cast,s.Position,true));s.Cancel();Assert.AreEqual(PursuitState.Captured,s.State);
        }
        [Test] public void InvalidRoutesAreRejectedAndProfilesAreNotMutated()
        {var d=Route();var s=new PursuitSession(d,_=>true);d.speed=29;s.Begin(new Vector3(50,0,0));s.Tick(1,new Vector3(50,0,0));Assert.Less(s.Speed,11);d.points[1].position=Vector3.zero;Assert.Throws<ArgumentException>(()=>new PursuitRoute(d));}
        [Test] public void PursuitReversalAnticipatesAndRespectsCooldown()
        {
            var d=Route();d.startDistance=50;d.speed=4;d.capture=ActivationMethod.Cast;d.reactionDelay=.2f;d.deceleration=100;
            var s=new PursuitSession(d,_=>true);s.Begin(new Vector3(-8,0,50));s.Tick(.1f,new Vector3(8,0,50));
            Assert.AreEqual(1,s.Reactions);Assert.AreEqual(PursuitState.Reacting,s.State);Assert.AreEqual(1,s.Direction);
            s.Tick(.5f,new Vector3(8,0,50));Assert.AreEqual(-1,s.Direction);Assert.AreEqual(PursuitState.Pursuing,s.State);
            s.Tick(.1f,new Vector3(-8,0,50));Assert.AreEqual(1,s.Reactions);
        }
        [Test] public void LoopPingPongWaypointPauseAndLongRunRemainBounded()
        {
            var d=Route();d.points[1].position=Vector3.forward*10;d.end=RouteEnd.Loop;d.capture=ActivationMethod.Cast;d.nearMiss=false;d.speed=20;d.acceleration=10000;
            var s=new PursuitSession(d,_=>true);int crossed=0;s.WaypointReached+=(i,id)=>crossed++;s.Begin(Vector3.right*50);
            for(int i=0;i<36000;i++)s.Tick(1f/120,Vector3.right*50);
            Assert.Greater(crossed,500);Assert.That(s.Distance,Is.InRange(0,s.Route.Length));Assert.IsFalse(float.IsNaN(s.Position.x));
            d.end=RouteEnd.PingPong;d.points[1].pause=.5f;s=new PursuitSession(d,_=>true);s.Begin(Vector3.right*50);s.Tick(.6f,Vector3.right*50);
            Assert.AreEqual(PursuitState.Waiting,s.State);Assert.AreEqual(-1,s.Direction);s.Tick(.6f,Vector3.right*50);Assert.Less(s.Distance,10);
        }
        [Test] public void NoEvasionAcknowledgesMissWithoutStoppingOrReversing()
        {
            var d=Route();d.startDistance=50;d.speed=4;d.capture=ActivationMethod.Cast;d.evasion=Evasion.None;
            var session=new PursuitSession(d,_=>true);session.Begin(new Vector3(-8,0,50));session.Tick(.1f,new Vector3(8,0,50));
            Assert.AreEqual(1,session.Reactions);Assert.AreEqual(PursuitState.Pursuing,session.State);Assert.AreEqual(1,session.Direction);
            float speed=session.Speed;session.Tick(.1f,new Vector3(8,0,50));Assert.GreaterOrEqual(session.Speed,speed);
        }
        [Test] public void GroundDistanceIncludesProjectedSlopeAndHybridEndpointsStayContinuous()
        {
            var d=Route();d.mode=PursuitMode.Ground;d.points[1].position=new Vector3(0,80,100);var route=new PursuitRoute(d,64,p=>new Vector3(p.x,p.z*.1f,p.z));
            Assert.AreEqual(Mathf.Sqrt(10100),route.Length,.01f);
            d.mode=PursuitMode.Hybrid;d.points.Clear();d.points.Add(new PursuitWaypoint{position=Vector3.zero});
            d.points.Add(new PursuitWaypoint{position=new Vector3(0,10,20),airborne=true});d.points.Add(new PursuitWaypoint{position=new Vector3(0,15,40),airborne=true});d.points.Add(new PursuitWaypoint{position=new Vector3(0,1,60)});
            route=new PursuitRoute(d,64,p=>new Vector3(p.x,0,p.z));float join=route.WaypointDistances[1];
            Assert.Less(Vector3.Distance(route.Evaluate(join-.001f,out _,out _),route.Evaluate(join+.001f,out _,out _)),.003f);
            Assert.AreEqual(0,route.Evaluate(join,out _,out _).y,.001);Assert.Greater(route.Evaluate(route.WaypointDistances[2],out _,out _).y,14);
        }
    }
}
