using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Windrunner.Production.Progression;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionRulesTests
    {
        private static ProgressionDefinitions Definitions()
        {
            var d = new ProgressionDefinitions();
            d.rewards.Add(new RewardDefinition { id = "reward", contentId = "route" });
            foreach (string character in new[] { "a", "b" })
            {
                var r = new RelationshipDefinition { id = character, ranks = new List<string> { "one", "two" } };
                for (int i = 1; i <= 2; i++)
                {
                    string q = character + i;
                    d.cutscenes.Add(new CutsceneDefinition { id = "cinematic-" + q });
                    r.chapters.Add(new ChapterDefinition { id = "chapter-" + q, rank = i, questId = q, cutsceneId = "cinematic-" + q, rewards = new List<string> { "reward" } });
                    d.quests.Add(new QuestDefinition { id = q, kind = QuestKind.Story, characterId = character, chapterId = "chapter-" + q,
                        groups = new List<ObjectiveGroup> { new ObjectiveGroup { id = "g", objectives = new List<ObjectiveDefinition> {
                            new ObjectiveDefinition { id = "o", kind = FactKind.Interaction, definitionId = "switch" } } } } });
                }
                d.relationships.Add(r);
            }
            return d;
        }
        private static GameplayFact Fact(string occurrence = "first", string target = "switch") => new GameplayFact {
            occurrenceId = occurrence, playerId = "player", kind = FactKind.Interaction, definitionId = "switch", targetId = target, verified = true };

        [Test] public void IndependentChaptersCommitOnceAndCutsceneGatesNextChapter()
        {
            var store = new MemoryProgressionStore(); var d = Definitions();
            using (var s = new ProgressionService(d, "player", store))
            {
                Assert.AreEqual(QuestStatus.Available, s.Status("a1")); Assert.AreEqual(QuestStatus.Locked, s.Status("a2"));
                Assert.IsFalse(s.Observe(Fact())); Assert.IsTrue(s.Accept("a1")); Assert.IsTrue(s.Accept("b1"));
                s.Track("a1"); s.Track("b1"); s.Observe(Fact());
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, s.Status("a1")); Assert.AreEqual(QuestStatus.ReadyToTurnIn, s.Status("b1"));
                Assert.IsTrue(s.TurnIn("a1")); Assert.IsFalse(s.TurnIn("a1"));
                Assert.AreEqual(1, s.Snapshot().rewards.Count); Assert.AreEqual(QuestStatus.Locked, s.Status("a2"));
            }
            using (var loaded = new ProgressionService(d, "player", store))
            {
                Assert.AreEqual(CinematicStatus.Pending, loaded.Snapshot().cutscenes.Single().status);
                Assert.IsTrue(loaded.StartCutscene("cinematic-a1"));
            }
            using (var loaded = new ProgressionService(d, "player", store))
            {
                Assert.AreEqual(CinematicStatus.Pending, loaded.Snapshot().cutscenes.Single().status);
                loaded.StartCutscene("cinematic-a1"); loaded.ResolveCutscene("cinematic-a1", true);
                Assert.AreEqual(QuestStatus.Available, loaded.Status("a2")); Assert.IsFalse(loaded.TurnIn("a1"));
                Assert.AreEqual(1, loaded.Snapshot().rewards.Count); Assert.IsTrue(loaded.TurnIn("b1"));
                Assert.AreEqual(2, loaded.Snapshot().relationships.Count);
            }
        }
        [Test] public void ParallelSequentialUniqueAndRetroactivePolicies()
        {
            var d = Definitions(); var q = d.quests[0];
            q.groups[0].objectives[0].count = 2; q.groups[0].objectives[0].uniqueTargets = true;
            q.groups.Add(new ObjectiveGroup { id = "later", requiresGroups = new List<string> { "g" }, objectives = new List<ObjectiveDefinition> {
                new ObjectiveDefinition { id = "later-o", kind = FactKind.Interaction, definitionId = "switch" } } });
            using (var s = new ProgressionService(d, "player", new MemoryProgressionStore()))
            {
                s.Accept("a1"); s.Observe(Fact()); Assert.IsFalse(s.Observe(Fact()));
                s.Observe(Fact("second", "switch")); s.Observe(Fact("third", "different"));
                Assert.IsFalse(s.Observe(Fact("third", "different")));
                Assert.AreEqual(QuestStatus.Active, s.Status("a1")); s.Observe(Fact("fourth")); Assert.AreEqual(QuestStatus.ReadyToTurnIn, s.Status("a1"));
            }
            q.groups.RemoveAt(1); var o = q.groups[0].objectives[0]; o.count = 1; o.credit = CreditPolicy.PersistentRetroactive;
            using (var s = new ProgressionService(d, "player", new MemoryProgressionStore()))
            {
                var fact = Fact(); fact.persistent = true; s.Observe(fact); s.Accept("a1");
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, s.Status("a1")); Assert.AreEqual(QuestStatus.Available, s.Status("b1"));
                s.Accept("b1"); Assert.AreEqual(QuestStatus.Active, s.Status("b1"));
            }
        }
        private sealed class FailingStore : IProgressionStore
        {
            private readonly MemoryProgressionStore saved=new MemoryProgressionStore();
            public int writes;
            public bool fail;
            public ProgressionState Load() => saved.Load();
            public void Commit(ProgressionState state)
            {
                if (fail) throw new IOException("injected");
                saved.Commit(state);
                writes++;
            }
        }
        private static void EquipFarseer(ProgressionService s)
        {
            var gear=new DaggerLoadout{initialized=true,equipped="farseer"};
            gear.owned.Add("farseer");
            s.SaveDaggers(gear);
        }

        [Test] public void FourCollectionsFlushOnceAndClosingFramePreventsLateDeferral()
        {
            var store=new FailingStore();
            using var s=new ProgressionService(Definitions(),"player",store);
            EquipFarseer(s);
            var published=new List<float>();
            s.Changed+=()=>published.Add(store.Load().daggers.Stored("farseer"));
            s.OpenReserveFrame(10);
            for(int n=1;n<=3;n++)s.CollectReserve("farseer",n,10);
            Assert.AreEqual(3,s.ReadDaggers().Stored("farseer"));
            Assert.Zero(store.Load().daggers.Stored("farseer"));Assert.IsEmpty(published);
            s.CollectReserve("farseer",4,10);
            s.CollectReserve("farseer",5,10);
            s.CloseReserveFrame();s.FlushPendingReserve();s.FlushPendingReserve();
            s.CollectReserve("farseer",6,10);
            CollectionAssert.AreEqual(new[]{4f,5f,6f},published);
            Assert.AreEqual(4,store.writes);
            s.OpenReserveFrame(11);s.CollectReserve("farseer",7,11);
            s.OpenReserveFrame(12); // Cannot extend a missed deadline.
            s.CollectReserve("farseer",8,12);
            Assert.AreEqual(8,store.Load().daggers.Stored("farseer"));
        }

        [Test] public void PendingReserveAndCriticalChapterUnlockCommitTogetherOrNeitherPublishes()
        {
            var store=new FailingStore();var d=Definitions();
            d.rewards[0].kind="unlock";
            using var s=new ProgressionService(d,"player",store);
            EquipFarseer(s);s.Accept("a1");s.Observe(Fact());
            s.OpenReserveFrame(1);s.CollectReserve("farseer",9,1);
            int previous=store.writes,events=0;s.Changed+=()=>events++;
            Assert.IsFalse(s.TurnIn("b1"));Assert.IsTrue(s.HasPendingReserve);
            store.fail=true;
            Assert.Throws<IOException>(()=>s.TurnIn("a1"));
            Assert.IsEmpty(store.Load().rewards);Assert.IsEmpty(store.Load().relationships);
            Assert.Zero(store.Load().daggers.Stored("farseer"));Assert.AreEqual(9,s.StoredDaggerReserve("farseer"));
            Assert.Zero(events);store.fail=false;
            Assert.IsTrue(s.TurnIn("a1"));s.FlushPendingReserve();
            Assert.AreEqual(previous+1,store.writes);Assert.AreEqual(1,events);
            using var reopened=new ProgressionService(d,"player",store);
            Assert.AreEqual(9,reopened.StoredDaggerReserve("farseer"));
            Assert.AreEqual(1,reopened.Snapshot().relationships.Single().rank);
            Assert.IsTrue(reopened.HasUnlock("route"));Assert.IsFalse(reopened.TurnIn("a1"));
            Assert.AreEqual(1,reopened.Snapshot().rewards.Count);
        }

        [Test] public void FailedFourthDeliveryRetainsAbsoluteBalanceThroughFailedDebitAndRetry()
        {
            var store=new FailingStore();using var s=new ProgressionService(Definitions(),"player",store);
            EquipFarseer(s);s.OpenReserveFrame(5);int events=0;s.Changed+=()=>events++;
            store.fail=true;
            for(int n=1;n<=4;n++)s.CollectReserve("farseer",n,5);
            Assert.IsInstanceOf<IOException>(s.ReserveWriteError);
            Assert.Throws<IOException>(()=>s.CollectReserve("farseer",5,5));
            Assert.Throws<IOException>(()=>s.SetDaggerReserve("farseer",0));
            Assert.AreEqual(4,s.StoredDaggerReserve("farseer"));Assert.Zero(events);
            store.fail=false;s.FlushPendingReserve();s.FlushPendingReserve();
            Assert.AreEqual(1,events);Assert.AreEqual(4,store.Load().daggers.Stored("farseer"));
            Assert.IsNull(s.ReserveWriteError);
        }

        [Test] public void FailedPersistenceIsAtomicAndListenersCannotSuppressCommittedState()
        {
            var store = new FailingStore(); using var s = new ProgressionService(Definitions(), "player", store);
            s.Accept("a1"); s.Observe(Fact()); store.fail = true;
            Assert.Throws<IOException>(() => s.TurnIn("a1")); Assert.AreEqual(QuestStatus.ReadyToTurnIn, s.Status("a1")); Assert.IsEmpty(s.Snapshot().rewards);
            int calls = 0; s.Changed += () => throw new Exception("listener"); s.Changed += () => calls++;
            store.fail = false; Assert.IsTrue(s.TurnIn("a1")); Assert.AreEqual(1, calls); Assert.AreEqual(1, s.Snapshot().rewards.Count);
            var external = s.Snapshot(); external.rewards.Clear(); Assert.AreEqual(1, s.Snapshot().rewards.Count);
        }
        [Test] public void SaveVersionsUnknownContentAndFileReplacement()
        {
            string directory = Path.Combine(Path.GetTempPath(), "windrunner-progression-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            try
            {
                var state = new ProgressionState { playerId = "player", schemaVersion = 0 };
                state.quests.Add(new QuestRecord { id = "retired", status = QuestStatus.Completed });
                var store = new FileProgressionStore(path); store.Commit(state);
                Assert.AreEqual(1, store.Load().schemaVersion); state.schemaVersion = 1; store.Commit(state);
                Assert.IsTrue(File.Exists(path + ".bak"));
                using var s = new ProgressionService(Definitions(), "player", store);
                s.Accept("a1"); Assert.IsTrue(s.Snapshot().quests.Any(q => q.id == "retired"));
                Assert.Throws<InvalidDataException>(() => ProgressionCodec.Decode("{\"schemaVersion\":99,\"playerId\":\"player\"}"));
                Assert.Throws<InvalidDataException>(() => ProgressionCodec.Decode("invalid"));
                Assert.Throws<InvalidDataException>(() => ProgressionCodec.Decode("{\"schemaVersion\":1,\"playerId\":\"player\",\"quests\":[null]}"));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        [Test] public void AuthoringRejectsCyclesBadOwnershipAndMissingContent()
        {
            var d = Definitions(); Assert.IsEmpty(ProgressionValidation.Validate(d));
            Assert.AreEqual(4, ProgressionValidation.Validate(d, true).Count);
            d.quests[0].requiresQuests.Add("b1"); d.quests[2].requiresQuests.Add("a1");
            d.quests[0].groups[0].requiresGroups.Add("g"); d.quests[0].groups[0].objectives[0].evaluator = "missing";
            d.quests[0].kind = QuestKind.Optional;
            var errors = ProgressionValidation.Validate(d);
            Assert.IsTrue(errors.Any(x => x.Contains("circular group"))); Assert.IsTrue(errors.Any(x => x.Contains("circular prerequisites")));
            Assert.IsTrue(errors.Any(x => x.Contains("unsupported evaluator"))); Assert.IsTrue(errors.Any(x => x.Contains("optional quest")));
        }
    }
}
