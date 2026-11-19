using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Production.Tests
{
    public sealed class ZoneActivityProgressionTests
    {
        [Test] public void ProductionSceneBindingsAndCastTargetVisibilityValidate()
        {
            var setup=UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try { System.Type.GetType("Windrunner.Activities.Editor.ZoneActivityIntegration, Assembly-CSharp-Editor",true).GetMethod("ValidateSaved").Invoke(null,null); }
            finally { if(setup.Any(s=>s.isLoaded)&&setup.Any(s=>s.isActive)) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup); else UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single); }
        }
        const string Catalog="Assets/WindrunnerProduction/World/Zone01Catalog.asset";
        static ProgressionDefinitions Current()=>JsonUtility.FromJson<ProgressionDefinitions>(JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(Catalog).definitions));
        static void OriginalChapter(ProgressionService s,string id)
        {
            Assert.IsTrue(s.Accept(id));bool hunt=id=="basin-hunt";
            for(int i=0;i<(hunt?3:1);i++)s.Observe(new GameplayFact{playerId=s.PlayerId,occurrenceId=id+i,definitionId=hunt?"basin-creature":"ridge-stone",targetId=id+i,regionId=hunt?"North Basin":null,kind=hunt?FactKind.EnemyDefeated:FactKind.ItemCollected,seconds=1,verified=true});
            Assert.IsTrue(s.TurnIn(id));Assert.IsTrue(s.StartCutscene(id+"-milestone",false));Assert.IsTrue(s.ResolveCutscene(id+"-milestone",true));
        }
        [TestCase(1)] [TestCase(2)] public void OriginalSavesKeepRankRewardsAndUnlockNewChapter(int completed)
        {
            var old=Current();old.quests.RemoveRange(2,2);old.relationships[0].chapters.RemoveRange(2,2);old.relationships[0].ranks.RemoveRange(2,2);old.cutscenes.RemoveRange(2,2);old.challenges.Clear();var store=new MemoryProgressionStore();
            using(var previous=new ProgressionService(old,"existing-save",store)){OriginalChapter(previous,"basin-hunt");if(completed==2)OriginalChapter(previous,"ridge-retrieval");}
            using var loaded=new ProgressionService(Current(),"existing-save",store);Assert.AreEqual(completed,loaded.Snapshot().relationships.Single().rank);Assert.AreEqual(completed,loaded.Snapshot().rewards.Count);Assert.IsTrue(loaded.HasUnlock("basin-hunt-sigil"));
            Assert.AreEqual(completed==2?QuestStatus.Available:QuestStatus.Locked,loaded.Status("zone-wind-relay"));Assert.AreEqual(QuestStatus.Locked,loaded.Status("zone-pursuit"));
            if(completed==1)OriginalChapter(loaded,"ridge-retrieval");Assert.AreEqual(QuestStatus.Available,loaded.Status("zone-wind-relay"));
        }
        [Test] public void FourChaptersRemainSequentialAndOptionalMechanicsDoNotAdvanceRank()
        {
            var d=Current();Assert.IsEmpty(ProgressionValidation.Validate(d,true));CollectionAssert.AreEqual(new[]{"Ally","Trusted","Placeholder Rank 3","Placeholder Rank 4"},d.relationships.Single().ranks);
            var store=new MemoryProgressionStore();using var s=new ProgressionService(d,"test",store);OriginalChapter(s,"basin-hunt");OriginalChapter(s,"ridge-retrieval");
            Assert.IsFalse(s.Accept("zone-pursuit"));Assert.IsTrue(s.Accept("zone-wind-relay"));var relay=new ActivityProgression(s,"zone-wind-relay-course",null);Assert.IsTrue(relay.Begin());Assert.IsTrue(relay.Complete(10));Assert.IsFalse(relay.Complete(10));
            Assert.AreEqual(2,s.Snapshot().relationships.Single().rank);Assert.AreEqual(QuestStatus.ReadyToTurnIn,s.Status("zone-wind-relay"));Assert.IsTrue(s.TurnIn("zone-wind-relay"));Assert.IsFalse(s.TurnIn("zone-wind-relay"));
            Assert.AreEqual(QuestStatus.Locked,s.Status("zone-pursuit"));Assert.IsTrue(s.StartCutscene("zone-wind-relay-placeholder-milestone",false));Assert.IsTrue(s.ResolveCutscene("zone-wind-relay-placeholder-milestone",true));Assert.IsTrue(s.Accept("zone-pursuit"));
            Assert.IsTrue(s.ObjectiveOpen("zone-pursuit","ground"));Assert.IsFalse(s.ObjectiveOpen("zone-pursuit","air"));
            foreach(string phase in new[]{"ground","air"}){var activity=new ActivityProgression(s,"zone-pursuit-"+phase,null);Assert.IsTrue(activity.Begin());Assert.IsTrue(activity.Complete(7));Assert.IsFalse(activity.Complete(7));}
            Assert.AreEqual(QuestStatus.ReadyToTurnIn,s.Status("zone-pursuit"));Assert.AreEqual(3,s.Snapshot().relationships.Single().rank);Assert.IsTrue(s.TurnIn("zone-pursuit"));Assert.AreEqual(4,s.Snapshot().relationships.Single().rank);Assert.AreEqual(2,s.Snapshot().rewards.Count,"Only the two existing authored unlocks; no invented reward.");
            using var restored=new ProgressionService(d,"test",store);Assert.AreEqual(QuestStatus.Completed,restored.Status("zone-pursuit"));Assert.IsFalse(restored.TurnIn("zone-pursuit"));Assert.AreEqual(4,restored.Snapshot().relationships.Single().chapters.Count);
        }
    }
}
