using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Windrunner.Production.Progression;
namespace Windrunner.Production.Tests
{
    public sealed class WayfinderTests
    {
        [TestCase(48,"48 m")][TestCase(275,"275 m")][TestCase(1200,"1.2 km")][TestCase(999.6f,"1.0 km")][TestCase(-2,"0 m")]
        public void MetricDistance(float value,string expected)=>Assert.AreEqual(expected,WayfinderRules.Distance(value));
        [Test] public void ProjectionKeepsBehindAndOffscreenTargetsInsideHudSafeBounds()
        {
            var center=WayfinderRules.Project(new Vector3(.5f,.5f,10),out var edge,out _);Assert.IsFalse(edge);Assert.AreEqual(new Vector2(640,285),center);
            foreach(var v in new[]{new Vector3(2,.5f,10),new Vector3(-2,.5f,10),new Vector3(.5f,.5f,-10),new Vector3(2,2,-10),new Vector3(.99f,.99f,10)})
            {var p=WayfinderRules.Project(v,out edge,out var angle);Assert.IsTrue(edge);Assert.That(p.x,Is.InRange(120,1160));Assert.That(p.y,Is.InRange(150,570));Assert.IsFalse(float.IsNaN(angle));}
        }
        [Test] public void EdgeHysteresisDoesNotToggleAtBoundaryNoise()
        {
            WayfinderRules.Project(new Vector3(.5f+516f/1280,.5f,10),out var outside,out _,true);Assert.IsTrue(outside);
            WayfinderRules.Project(new Vector3(.5f+510f/1280,.5f,10),out outside,out _,true);Assert.IsFalse(outside);
        }
        [Test] public void SelectionUsesTrackedOpenObjectiveDisclosureAndLiveAnchorValidity()
        {
            var player=new GameObject("actor");var target=new GameObject("area");var npc=new GameObject("contact");var catalog=ScriptableObject.CreateInstance<ProgressionCatalog>();
            try
            {
                var actor=player.AddComponent<ProgressionActor>();
                var quest=new QuestDefinition{id="q",kind=QuestKind.Optional,guidance=new GuidanceDefinition{showArea=true,areaId="area"},groups=new List<ObjectiveGroup>{new ObjectiveGroup{id="g",objectives=new List<ObjectiveDefinition>{new ObjectiveDefinition{id="o",kind=FactKind.Interaction,definitionId="hit"}}}}};
                catalog.definitions.quests.Add(quest);using var service=new ProgressionService(catalog.definitions,"p",new MemoryProgressionStore());
                var a=new ObjectiveAnchor{questId="q",objectiveId="o",anchor=target.transform,approximateArea=true,areaId="area"};
                var b=new ObjectiveAnchor{questId="q",phase=WayfinderPhase.TurnIn,anchor=npc.transform};var anchors=new[]{a,b};
                ObjectiveAnchor Select()=>WayfinderRules.Select(service,service.Snapshot(),catalog,anchors,actor);
                Assert.IsNull(Select());service.Accept("q");service.Track("q");Assert.AreSame(a,Select());
                quest.guidance.showArea=false;Assert.IsNull(Select());quest.guidance.showArea=true;
                target.SetActive(false);Assert.IsNull(Select());target.SetActive(true);
                var other=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                try{UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target,other);Assert.IsNull(Select());UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target,actor.gameObject.scene);Assert.AreSame(a,Select());}
                finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(other);}
                Assert.IsNull(WayfinderRules.Select(service,service.Snapshot(),catalog,Array.Empty<ObjectiveAnchor>(),actor));
                service.Track(null);Assert.IsNull(Select());service.Track("q");
                service.Observe(new GameplayFact{playerId="p",occurrenceId="one",kind=FactKind.Interaction,definitionId="hit",verified=true});Assert.AreSame(b,Select());
                UnityEngine.Object.DestroyImmediate(npc);Assert.IsNull(Select());service.TurnIn("q");Assert.IsNull(Select());
            }
            finally{UnityEngine.Object.DestroyImmediate(player);UnityEngine.Object.DestroyImmediate(target);if(npc)UnityEngine.Object.DestroyImmediate(npc);UnityEngine.Object.DestroyImmediate(catalog);}
        }
        [Test] public void PreferenceDefaultsOnAndPersistsWithoutChangingQuests()
        {
            const string key="Windrunner.UI.ObjectiveWayfinder";bool existed=PlayerPrefs.HasKey(key);int old=PlayerPrefs.GetInt(key);
            try{PlayerPrefs.DeleteKey(key);Assert.IsTrue(WindrunnerUITheme.ObjectiveWayfinder);WindrunnerUITheme.ObjectiveWayfinder=false;Assert.AreEqual(0,PlayerPrefs.GetInt(key));Assert.IsFalse(WindrunnerUITheme.ObjectiveWayfinder);WindrunnerUITheme.ObjectiveWayfinder=true;Assert.IsTrue(WindrunnerUITheme.ObjectiveWayfinder);Assert.IsFalse(new CutsceneDefinition().isPlaceholder);}
            finally{if(existed)PlayerPrefs.SetInt(key,old);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
    }
}
