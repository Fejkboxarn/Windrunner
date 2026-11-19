using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Production.Tests
{
    public sealed class ActivityPersistenceTests
    {
        sealed class Store:IProgressionStore
        {
            public bool Fail;public int Writes;readonly MemoryProgressionStore memory=new MemoryProgressionStore();
            public ProgressionState Load()=>memory.Load();
            public void Commit(ProgressionState s){if(Fail)throw new IOException("Injected write failure");Writes++;memory.Commit(s);}
        }
        static ProgressionDefinitions Definitions(bool replayable)
        {
            var d=new ProgressionDefinitions();d.rewards.Add(new RewardDefinition{id="prize",contentId="activity-unlock"});
            d.challenges.Add(new ChallengeDefinition{id="activity",resultKind=FactKind.TraversalCompleted,replayable=replayable,firstClearRewards=new List<string>{"prize"}});
            d.quests.Add(new QuestDefinition{id="quest",kind=QuestKind.Optional,automaticTurnIn=true,groups=new List<ObjectiveGroup>{new ObjectiveGroup{id="g",objectives=new List<ObjectiveDefinition>{new ObjectiveDefinition{id="o",definitionId="activity",kind=FactKind.TraversalCompleted}}}}});return d;
        }
        [Test] public void CompletionAndRewardsRemainDurableAndIdempotentAfterReload()
        {
            var d=Definitions(false);var store=new Store();
            using(var service=new ProgressionService(d,"player",store))
            {
                service.Accept("quest");var binding=new ActivityProgression(service,"activity","region");Assert.IsTrue(binding.Begin());
                Assert.IsTrue(binding.Complete(4));Assert.IsFalse(binding.Complete(4));Assert.AreEqual(QuestStatus.Completed,service.Status("quest"));Assert.IsTrue(service.HasUnlock("activity-unlock"));
            }
            using var loaded=new ProgressionService(d,"player",store);Assert.AreEqual(QuestStatus.Completed,loaded.Status("quest"));Assert.IsFalse(new ActivityProgression(loaded,"activity","region").Begin());Assert.AreEqual(1,loaded.Snapshot().rewards.Count);
        }
        [Test] public void FailedWriteCannotCaptureOrGrantAndRetryUsesSameTransaction()
        {
            var store=new Store();using var service=new ProgressionService(Definitions(true),"player",store);service.Accept("quest");var binding=new ActivityProgression(service,"activity",null);binding.Begin();
            var d=new PursuitDefinition{challengeId="activity",capture=ActivationMethod.Cast,mode=PursuitMode.Air};d.points.Add(new PursuitWaypoint());d.points.Add(new PursuitWaypoint{position=Vector3.forward*10});
            var session=new PursuitSession(d,binding.Complete);session.Begin(Vector3.zero);store.Fail=true;
            Assert.Throws<IOException>(()=>session.TryCapture(ActivationMethod.Cast,Vector3.zero,true));Assert.AreNotEqual(PursuitState.Captured,session.State);Assert.IsFalse(service.HasUnlock("activity-unlock"));
            store.Fail=false;Assert.IsTrue(session.TryCapture(ActivationMethod.Cast,Vector3.zero,true));Assert.AreEqual(1,service.Snapshot().rewards.Count);
            var replay=new ActivityProgression(service,"activity",null);Assert.IsTrue(replay.Begin());Assert.IsTrue(replay.Complete(5));Assert.AreEqual(1,service.Snapshot().rewards.Count);
        }
        [Test] public void MidAttemptReloadRestartsWithoutChangingQuestOrOtherSave()
        {
            var d=Definitions(true);var a=new Store();var b=new Store();
            using(var service=new ProgressionService(d,"a",a)){service.Accept("quest");var binding=new ActivityProgression(service,"activity",null);binding.Begin();}
            using var loaded=new ProgressionService(d,"a",a);Assert.AreEqual(QuestStatus.Active,loaded.Status("quest"));Assert.IsTrue(new ActivityProgression(loaded,"activity",null).Begin());
            using var other=new ProgressionService(d,"b",b);Assert.AreEqual(QuestStatus.Available,other.Status("quest"));Assert.IsFalse(other.HasUnlock("activity-unlock"));
        }
    }
}
