using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Windrunner.Production.Progression;

namespace Windrunner.Production.Tests
{
    public sealed class WorldProgressionTests
    {
        private static ProgressionDefinitions Definitions()
        {
            var d = new ProgressionDefinitions();
            d.rewards.Add(new RewardDefinition { id = "first", contentId = "route" });
            d.challenges.Add(new ChallengeDefinition { id = "race-challenge", resultKind = FactKind.RaceCompleted, firstClearRewards = new List<string> { "first" } });
            d.challenges.Add(new ChallengeDefinition { id = "puzzle-challenge", resultKind = FactKind.PuzzleCompleted });
            d.races.Add(new RaceDefinition { id = "race", challengeId = "race-challenge", timeLimit = 10, checkpoints = new List<RaceStep> {
                new RaceStep { alternatives = new List<string> { "first", "branch" } }, new RaceStep { alternatives = new List<string> { "finish" } } } });
            d.puzzles.Add(new PuzzleDefinition { id = "puzzle", challengeId = "puzzle-challenge", ordered = true, mechanisms = new List<string> { "switch", "cast" }, destinationId = "exit" });
            return d;
        }
        [Test] public void RaceFailureRestartCheckpointOrderingAndFirstClearAreIndependent()
        {
            var d = Definitions(); var store = new MemoryProgressionStore(); using var s = new ProgressionService(d, "p", store);
            var race = new RaceSession(d.races[0], s); Assert.IsFalse(race.Start()); s.DiscoverChallenge("race-challenge");
            Assert.IsTrue(race.Start()); Assert.IsFalse(race.Reach("finish", "p")); Assert.IsFalse(race.Reach("first", "other"));
            race.Tick(11); Assert.AreEqual(AttemptStatus.Failed, race.Status); Assert.IsEmpty(s.Snapshot().rewards);
            race.Start(); race.Tick(0); Assert.AreEqual(0, race.Elapsed); race.Tick(3); race.Reach("branch", "p");
            Assert.IsFalse(race.Reach("first", "p")); Assert.IsTrue(race.Reach("finish", "p")); Assert.IsFalse(race.Reach("finish", "p"));
            race.Start(); race.Tick(2); race.Reach("first", "p"); race.Reach("finish", "p");
            Assert.AreEqual(1, s.Snapshot().rewards.Count); Assert.AreEqual(2, s.Snapshot().challenges.Single().personalBest); Assert.IsEmpty(s.Snapshot().relationships);
            using var loaded = new ProgressionService(d, "p", store); Assert.AreEqual(1, loaded.Snapshot().rewards.Count);
            var restored = new RaceSession(d.races[0], loaded); Assert.AreEqual(AttemptStatus.Ready, restored.Status); restored.Start(); restored.Cancel();
            Assert.AreEqual(AttemptStatus.Canceled, restored.Status);
        }
        [Test] public void PuzzleSequenceExitPartialReloadAndPermanentSolution()
        {
            var d = Definitions(); d.puzzles[0].persistPartial = true; var store = new MemoryProgressionStore();
            using var s = new ProgressionService(d, "p", store); s.DiscoverChallenge("puzzle-challenge");
            var puzzle = new PuzzleSession(d.puzzles[0], s); puzzle.Begin(); Assert.IsFalse(puzzle.Activate("cast", "p"));
            Assert.AreEqual(PuzzleStatus.Ready, puzzle.Status); puzzle.Activate("switch", "p");
            using var loaded = new ProgressionService(d, "p", store);
            var restored = new PuzzleSession(d.puzzles[0], loaded); Assert.AreEqual("switch", restored.Activated.Single());
            restored.Begin(); restored.Leave(); Assert.IsEmpty(restored.Activated); Assert.AreEqual(PuzzleStatus.Dormant, restored.Status);
            restored.Begin(); restored.Activate("switch", "p"); restored.Activate("cast", "p"); Assert.IsFalse(restored.ReachDestination("wrong", "p"));
            Assert.IsTrue(restored.ReachDestination("exit", "p")); Assert.AreEqual(PuzzleStatus.Solved, restored.Status);
            restored.Leave(); Assert.AreEqual(PuzzleStatus.Solved, restored.Status);
            using var final = new ProgressionService(d, "p", store);
            Assert.AreEqual(PuzzleStatus.Solved, new PuzzleSession(d.puzzles[0], final).Status);
            Assert.IsTrue(final.Snapshot().challenges.Single().completed); Assert.IsEmpty(final.Snapshot().relationships);
        }
        [Test] public void TemporaryPuzzleTimeoutResetsWithoutPersistentCredit()
        {
            var d = Definitions(); var definition = d.puzzles[0]; definition.permanent = false; definition.timeWindow = 1;
            using var s = new ProgressionService(d, "p", new MemoryProgressionStore()); s.DiscoverChallenge("puzzle-challenge");
            var puzzle = new PuzzleSession(definition, s); puzzle.Begin(); puzzle.Activate("switch", "p"); puzzle.Tick(2);
            Assert.AreEqual(PuzzleStatus.Ready, puzzle.Status); Assert.IsEmpty(puzzle.Activated);
            puzzle.Activate("switch", "p"); puzzle.Activate("cast", "p"); puzzle.ReachDestination("exit", "p");
            Assert.IsEmpty(s.Snapshot().persistentFacts); Assert.IsEmpty(s.Snapshot().puzzles);
        }
    }
}
