using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Production.Tests
{
    public sealed class ActivityAbilityIntegrationTests
    {
        AbilityExecutionTests fixture;GameObject root;ScriptableObject profile;ProgressionService service;
        static readonly BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static Type T(string name)=>Type.GetType("Windrunner.Activities."+name+", Assembly-CSharp",true);
        static object Read(object o,string f)=>o.GetType().GetField(f,fields).GetValue(o);
        static void Set(object o,string f,object value)=>o.GetType().GetField(f,fields).SetValue(o,value);
        [UnityTearDown] public IEnumerator Cleanup()
        {if(root)UnityEngine.Object.Destroy(root);if(profile)UnityEngine.Object.Destroy(profile);yield return null;if(fixture!=null)yield return fixture.Teardown();service?.Dispose();}
        [UnityTest] public IEnumerator RelayUsesRealCastHitAndPreservesRewardOrder(){yield return Run(false,false);}
        [UnityTest] public IEnumerator RelayUsesRealSlingImpactAndPreservesLaunch(){yield return Run(false,true);}
        [UnityTest] public IEnumerator PursuitUsesRealCastHitOnly(){yield return Run(true,false);}
        [UnityTest] public IEnumerator PursuitUsesRealSlingImpactWithoutCancelingLaunch(){yield return Run(true,true);}
        [UnityTest] public IEnumerator CompletedQuestDoesNotRespawnItsGatedCapturedTarget()
        {
            var definitions=new ProgressionDefinitions();definitions.challenges.Add(new ChallengeDefinition{id="activity",resultKind=FactKind.TraversalCompleted});
            definitions.quests.Add(new QuestDefinition{id="done",kind=QuestKind.Optional,automaticTurnIn=true,groups=new List<ObjectiveGroup>{new ObjectiveGroup{id="g",objectives=new List<ObjectiveDefinition>{new ObjectiveDefinition{id="o",definitionId="activity",kind=FactKind.TraversalCompleted}}}}});
            service=new ProgressionService(definitions,"fixture",new MemoryProgressionStore());service.Accept("done");var progress=new ActivityProgression(service,"activity",null);Assert.IsTrue(progress.Begin());Assert.IsTrue(progress.Complete(1));Assert.AreEqual(QuestStatus.Completed,service.Status("done"));
            root=new GameObject("Reloaded gated pursuit");root.SetActive(false);var player=new GameObject("Bound actor");player.transform.SetParent(root.transform);var actor=player.AddComponent<ProgressionActor>();actor.Bind(service,null);
            var target=new GameObject("Previously captured target");target.transform.SetParent(root.transform);var p=ScriptableObject.CreateInstance<PursuitProfile>();profile=p;p.definition.challengeId="activity";p.definition.mode=PursuitMode.Air;p.definition.points.Add(new PursuitWaypoint());p.definition.points.Add(new PursuitWaypoint{position=Vector3.forward*10});
            var host=root.AddComponent(T("PursuitHost"));Set(host,"profile",p);Set(host,"actor",actor);Set(host,"target",target.transform);Set(host,"requiredActiveQuest","done");root.SetActive(true);yield return null;
            Assert.IsFalse(target.activeSelf,"A completed quest must not leave its captured target visible after scene recreation.");Assert.IsFalse((bool)host.GetType().GetMethod("Begin").Invoke(host,null));
        }
        IEnumerator Run(bool pursuit,bool sling)
        {
            fixture=new AbilityExecutionTests();yield return fixture.Setup();
            var definitions=new ProgressionDefinitions();definitions.challenges.Add(new ChallengeDefinition{id="activity",resultKind=FactKind.TraversalCompleted,replayable=false});
            service=new ProgressionService(definitions,"fixture",new MemoryProgressionStore());
            var player=(GameObject)Read(fixture,"player");var actor=player.AddComponent<ProgressionActor>();
            var inputHost=(Windrunner.Production.Input.WindrunnerInputHost)Read(fixture,"host");actor.Bind(service,inputHost.Input);
            IEnumerator scenario=sling?fixture.SlingPerfectInputChargeCurveImpactLaunchPreservesEventOrder():fixture.CastPerfectReleaseProjectileAndRewardPreserveOrder();
            Component host=null;bool bound=false;
            while(scenario.MoveNext())
            {
                if(!bound)
                {
                    var node=(GameObject)Read(fixture,"node");Assert.IsNotNull(node);root=new GameObject("Isolated activity binding");root.SetActive(false);
                    if(pursuit)
                    {
                        var p=ScriptableObject.CreateInstance<PursuitProfile>();profile=p;p.definition.challengeId="activity";p.definition.mode=PursuitMode.Air;p.definition.speed=0;p.definition.capture=sling?ActivationMethod.Sling:ActivationMethod.Cast;p.definition.nearMiss=false;p.definition.maxSeconds=0;
                        p.definition.points.Add(new PursuitWaypoint{position=node.transform.position});p.definition.points.Add(new PursuitWaypoint{position=node.transform.position+Vector3.forward*10});
                        host=root.AddComponent(T("PursuitHost"));Set(host,"profile",p);Set(host,"target",node.transform);Set(host,sling?"slingNode":"castNode",node.GetComponent(Type.GetType("AirflowPrototype."+(sling?"AirSlingNode":"AirNode")+", Assembly-CSharp",true)));
                    }
                    else
                    {
                        var p=ScriptableObject.CreateInstance<WindRelayProfile>();profile=p;p.definition.challengeId="activity";p.definition.nodes.Add(new RelayNodeDefinition{id="one",window=0,methods=sling?ActivationMethod.Sling:ActivationMethod.Cast});
                        host=root.AddComponent(T("WindRelayHost"));Set(host,"profile",p);var nodeBinding=node.AddComponent(T("RelayInteraction"));Set(nodeBinding,"relay",host);Set(nodeBinding,"relayId","one");Set(nodeBinding,sling?"slingNode":"castNode",node.GetComponent(Type.GetType("AirflowPrototype."+(sling?"AirSlingNode":"AirNode")+", Assembly-CSharp",true)));
                        var array=Array.CreateInstance(T("RelayInteraction"),1);array.SetValue(nodeBinding,0);Set(host,"nodes",array);
                    }
                    Set(host,"actor",actor);Set(host,"caster",Read(fixture,"caster"));Set(host,"sling",Read(fixture,"sling"));Set(host,"startOnEnable",false);root.SetActive(true);
                    Assert.IsTrue((bool)host.GetType().GetMethod("Begin").Invoke(host,null));bound=true;
                }
                yield return scenario.Current;
            }
            Assert.IsTrue(bound);Assert.IsNull((string)host.GetType().GetProperty("Error").GetValue(host));
            Assert.IsTrue(service.Snapshot().challenges.Find(c=>c.id=="activity").completed);
            Assert.AreEqual(1,service.Snapshot().challenges.Find(c=>c.id=="activity").successfulAttempts.Count);
            ((Behaviour)host).enabled=false;((Behaviour)host).enabled=true;yield return null;
            Assert.IsFalse((bool)host.GetType().GetMethod("Begin").Invoke(host,null),"Completed non-replayable activity cannot restart on enable.");
        }
    }
}
