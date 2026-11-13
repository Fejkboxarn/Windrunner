using System;
using System.Collections.Generic;

namespace Windrunner.Production.Progression
{
    [Serializable] public sealed class GameplayFact
    {
        public string occurrenceId, playerId, definitionId, targetId, regionId, attemptId;
        public FactKind kind;
        public bool verified, persistent;
        public float seconds;
    }
    [Serializable] public sealed class ObjectiveRecord
    {
        public string id;
        public int count;
        public List<string> occurrences = new List<string>();
        public List<string> targets = new List<string>();
    }
    [Serializable] public sealed class QuestRecord
    {
        public string id;
        public QuestStatus status;
        public List<string> receivedOccurrences = new List<string>();
        public List<ObjectiveRecord> objectives = new List<ObjectiveRecord>();
    }
    [Serializable] public sealed class RelationshipRecord
    {
        public string id;
        public int rank;
        public List<string> chapters = new List<string>();
    }
    [Serializable] public sealed class RewardClaim
    {
        public string id, rewardId, kind, contentId;
        public int amount;
    }
    [Serializable] public sealed class CutsceneRecord { public string id; public CinematicStatus status; }
    [Serializable] public sealed class ChallengeRecord
    {
        public string id;
        public bool discovered, completed;
        public float personalBest;
        public List<string> successfulAttempts = new List<string>();
    }
    [Serializable] public sealed class PuzzleRecord
    {
        public string id;
        public bool solved;
        public List<string> mechanisms = new List<string>();
    }
    [Serializable] public sealed class ProgressionState
    {
        public int schemaVersion = 1;
        public string playerId, trackedQuestId;
        public List<QuestRecord> quests = new List<QuestRecord>();
        public List<RelationshipRecord> relationships = new List<RelationshipRecord>();
        public List<RewardClaim> rewards = new List<RewardClaim>();
        public List<CutsceneRecord> cutscenes = new List<CutsceneRecord>();
        public List<GameplayFact> persistentFacts = new List<GameplayFact>();
        public List<ChallengeRecord> challenges = new List<ChallengeRecord>();
        public List<PuzzleRecord> puzzles = new List<PuzzleRecord>();
        public List<string> equipmentReferences = new List<string>();
    }
    public interface IObjectiveEvaluator { bool Matches(ObjectiveDefinition objective, GameplayFact fact); }
    public sealed class FactObjectiveEvaluator : IObjectiveEvaluator
    {
        public bool Matches(ObjectiveDefinition o, GameplayFact f) => f.verified && o.kind == f.kind &&
            (string.IsNullOrEmpty(o.definitionId) || o.definitionId == f.definitionId) &&
            (string.IsNullOrEmpty(o.regionId) || o.regionId == f.regionId) &&
            (o.maximumSeconds <= 0 || f.seconds <= o.maximumSeconds);
    }
}
