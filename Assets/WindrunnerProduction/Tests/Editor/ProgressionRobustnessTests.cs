using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Windrunner.Production.Feedback;
using Windrunner.Production.Progression;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionRobustnessTests
    {
        private sealed class CinematicStore : IProgressionStore
        {
            public bool fail;
            private readonly MemoryProgressionStore store = new MemoryProgressionStore();
            public ProgressionState Load() => store.Load();
            public void Commit(ProgressionState state) { if (fail) throw new IOException("injected cinematic commit failure"); store.Commit(state); }
        }
        private sealed class Control : ICinematicControl
        {
            public bool CanStart => true;
            public bool held;
            public IDisposable Acquire() { held = true; return new Lease(this); }
            private sealed class Lease : IDisposable { private readonly Control owner; public Lease(Control owner) { this.owner = owner; } public void Dispose() { owner.held = false; } }
        }
        [Test] public void CinematicSaveFailureRestoresControlAndRecoversPendingOnReload()
        {
            var d = new ProgressionDefinitions(); d.cutscenes.Add(new CutsceneDefinition { id = "cinematic" });
            var store = new CinematicStore(); var seed = new ProgressionState { playerId = "p" }; seed.cutscenes.Add(new CutsceneRecord { id = "cinematic" }); store.Commit(seed);
            var control = new Control();
            using (var service = new ProgressionService(d, "p", store))
            using (var session = new CinematicSession(service, control))
            {
                Assert.IsTrue(session.Start("cinematic")); Assert.IsTrue(control.held); store.fail = true;
                Assert.Throws<IOException>(() => session.Finish(false)); Assert.IsFalse(control.held); Assert.IsNull(session.Current);
            }
            store.fail = false;
            using var restored = new ProgressionService(d, "p", store); Assert.AreEqual(CinematicStatus.Pending, restored.Snapshot().cutscenes.Single().status);
        }
        private sealed class Feedback : IFeedbackService
        {
            public event Action<FeedbackKey, FeedbackRequest> Observed;
            public readonly List<FeedbackKey> keys = new List<FeedbackKey>();
            public int ObservedCount => keys.Count;
            public int PlayedCount => 0;
            public void Publish(FeedbackKey key, FeedbackRequest request) { keys.Add(key); Observed?.Invoke(key, request); }
        }
        private static QuestDefinition Quest(string id, FactKind kind, string definition) => new QuestDefinition {
            id = id, kind = QuestKind.Optional, automaticTurnIn = true, groups = new List<ObjectiveGroup> {
                new ObjectiveGroup { id = "g", objectives = new List<ObjectiveDefinition> {
                    new ObjectiveDefinition { id = "o", kind = kind, definitionId = definition, regionId = "region", maximumSeconds = 10 } } } } };
        [Test] public void AllObjectiveKindsFilterActorRegionResultTimeAndDuplicateOccurrence()
        {
            foreach (FactKind kind in Enum.GetValues(typeof(FactKind)))
            {
                var d = new ProgressionDefinitions(); d.quests.Add(Quest("quest", kind, "definition"));
                if (kind == FactKind.RaceCompleted || kind == FactKind.PuzzleCompleted || kind == FactKind.TraversalCompleted)
                    d.challenges.Add(new ChallengeDefinition { id = "definition", resultKind = kind });
                using var s = new ProgressionService(d, "p", new MemoryProgressionStore()); s.Accept("quest");
                var fact = new GameplayFact { occurrenceId = "event", playerId = "wrong", definitionId = "definition", regionId = "region", kind = kind, verified = true };
                Assert.IsFalse(s.Observe(fact)); fact.playerId = "p"; fact.regionId = "wrong"; Assert.IsFalse(s.Observe(fact));
                fact.regionId = "region"; fact.seconds = 11; Assert.IsFalse(s.Observe(fact)); fact.seconds = 3; fact.verified = false; Assert.IsFalse(s.Observe(fact));
                fact.verified = true; Assert.IsTrue(s.Observe(fact)); Assert.IsFalse(s.Observe(fact)); Assert.AreEqual(QuestStatus.Completed, s.Status("quest"));
            }
        }
        [Test] public void FeedbackOrderRewardUnlockAndObserverDisposal()
        {
            var d = new ProgressionDefinitions(); var q = Quest("quest", FactKind.Interaction, "switch"); q.rewards.Add("reward"); d.quests.Add(q);
            d.rewards.Add(new RewardDefinition { id = "reward", contentId = "door" });
            using var s = new ProgressionService(d, "p", new MemoryProgressionStore()); var feedback = new Feedback();
            feedback.Observed += (key, request) => { if (key == FeedbackKey.QuestCompleted) throw new Exception("injected presentation failure"); };
            using (var observer = new ProgressionFeedbackObserver(s, feedback, null))
            {
                s.Accept("quest"); s.Observe(new GameplayFact { occurrenceId = "one", playerId = "p", regionId = "region", definitionId = "switch", kind = FactKind.Interaction, verified = true });
                CollectionAssert.AreEqual(new[] { FeedbackKey.QuestCompleted, FeedbackKey.ProgressionRewardGranted }, feedback.keys);
                Assert.IsTrue(s.HasUnlock("door")); Assert.AreEqual(1, s.Snapshot().rewards.Count);
            }
            Assert.IsFalse(s.TurnIn("quest")); Assert.AreEqual(2, feedback.keys.Count);
        }
        [Test] public void DefinitionsAreFrozenAndNewContentCannotKeepStaleReadyStatus()
        {
            var d = new ProgressionDefinitions(); var q = Quest("quest", FactKind.Interaction, "switch"); q.automaticTurnIn = false; d.quests.Add(q);
            var store = new MemoryProgressionStore();
            using (var s = new ProgressionService(d, "p", store))
            {
                s.Accept("quest"); q.groups[0].objectives[0].count = 99;
                s.Observe(new GameplayFact { occurrenceId = "one", playerId = "p", regionId = "region", definitionId = "switch", kind = FactKind.Interaction, verified = true });
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, s.Status("quest"));
            }
            using var loaded = new ProgressionService(d, "p", store); Assert.AreEqual(QuestStatus.Active, loaded.Status("quest")); Assert.IsFalse(loaded.TurnIn("quest"));
        }
        [Test, Category("Profiling")] public void ProgressionCommandTimingBaseline()
        {
            var measurements = new List<string>();
            foreach (int count in new[] { 10, 100 })
            {
                var d = new ProgressionDefinitions();
                for (int i = 0; i < count; i++) { var q = Quest("q" + i, FactKind.Interaction, "switch"); q.groups[0].objectives[0].count = 100; d.quests.Add(q); }
                using var s = new ProgressionService(d, "p", new MemoryProgressionStore()); foreach (var q in d.quests) s.Accept(q.id);
                var samples = new List<double>();
                for (int i = 0; i < 25; i++)
                {
                    var fact = new GameplayFact { occurrenceId = "e" + i, playerId = "p", definitionId = "switch", regionId = "region", kind = FactKind.Interaction, verified = true };
                    var timer = Stopwatch.StartNew(); s.Observe(fact); timer.Stop(); if (i >= 5) samples.Add(timer.Elapsed.TotalMilliseconds);
                }
                samples.Sort(); measurements.Add(count + " active quests: median=" + samples[10].ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "ms; p95=" + samples[18].ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "ms; saveBytes=" + ProgressionCodec.Encode(s.Snapshot()).Length);
                Assert.AreEqual(25, s.Snapshot().quests[0].objectives[0].count);
            }
            string path = Path.Combine(Path.GetTempPath(), "windrunner-progression-benchmark-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new FileProgressionStore(path); var state = new ProgressionState { playerId = "benchmark" }; var times = new List<double>();
                for (int i = 0; i < 25; i++) { var timer = Stopwatch.StartNew(); store.Commit(state); timer.Stop(); if (i >= 5) times.Add(timer.Elapsed.TotalMilliseconds); }
                times.Sort(); measurements.Add("Small durable file replacement: median=" + times[10].ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "ms; p95=" + times[18].ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "ms");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
            string output = string.Join("\n", measurements);
            File.WriteAllText("Logs/ProductionMigration/progression-command-profile-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt", output);
            UnityEngine.Debug.Log(output + "\nCommand CPU measurements, not frame/GPU/allocation claims. File sample is a small empty save, not the 100-quest state.");
        }
    }
}
