using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Windrunner.Production.Progression
{
    public enum QuestKind { Story, Optional }
    public enum QuestStatus { Locked, Available, Active, ReadyToTurnIn, Completed }
    public enum FactKind { EnemyDefeated, RaceCompleted, ItemCollected, LocationDiscovered, Interaction, PuzzleCompleted, TraversalCompleted }
    public enum CreditPolicy { AfterAcceptance, PersistentRetroactive }
    public enum CinematicStatus { Pending, Playing, Watched, Skipped }
    public enum AttemptStatus { Ready, Active, Succeeded, Failed, Canceled }
    public enum PuzzleStatus { Dormant, Ready, Active, Resetting, Solved }

    [Serializable] public sealed class GuidanceDefinition
    {
        public string areaId;
        public string clue;
        public string clueTextId;
        public bool showArea;
    }
    [Serializable] public sealed class ObjectiveDefinition
    {
        public string id;
        public string description;
        public string textId;
        public string evaluator = "fact";
        public FactKind kind;
        public string definitionId;
        public string regionId;
        public int count = 1;
        public bool uniqueTargets;
        public CreditPolicy credit;
        public float maximumSeconds;
        public GuidanceDefinition guidance = new GuidanceDefinition();
    }
    [Serializable] public sealed class ObjectiveGroup
    {
        public string id;
        public List<string> requiresGroups = new List<string>();
        public List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();
    }
    [Serializable] public sealed class QuestDefinition
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public string nameTextId, descriptionTextId;
        public QuestKind kind;
        public string characterId, chapterId;
        public List<string> requiresQuests = new List<string>();
        public List<ObjectiveGroup> groups = new List<ObjectiveGroup>();
        public List<string> rewards = new List<string>();
        public bool automaticTurnIn;
        public GuidanceDefinition guidance = new GuidanceDefinition();
    }
    [Serializable] public sealed class ChapterDefinition
    {
        public string id, questId, cutsceneId;
        public int rank;
        public List<string> rewards = new List<string>();
    }
    [Serializable] public sealed class RelationshipDefinition
    {
        public string id, displayName, nameTextId;
        public string initialRankName;
        public List<string> ranks = new List<string>();
        public List<ChapterDefinition> chapters = new List<ChapterDefinition>();
    }
    [Serializable] public sealed class RewardDefinition
    {
        public string id;
        public string kind = "unlock";
        public string contentId;
        public int amount = 1;
    }
    [Serializable] public sealed class CutsceneDefinition
    {
        public string id, displayName;
        public PlayableAsset content;
        public UnityEngine.Video.VideoClip galleryReplay;
        public bool allowSkip = true;
    }
    [Serializable] public sealed class ChallengeDefinition
    {
        public string id, displayName;
        public FactKind resultKind = FactKind.TraversalCompleted;
        public bool replayable = true;
        public List<string> requiresQuests = new List<string>();
        public List<string> firstClearRewards = new List<string>();
    }
    [Serializable] public sealed class RaceDefinition
    {
        public string id, challengeId;
        // Each step accepts one of its authored alternatives; steps remain ordered.
        public List<RaceStep> checkpoints = new List<RaceStep>();
        public float timeLimit;
    }
    [Serializable] public sealed class RaceStep { public List<string> alternatives = new List<string>(); }
    [Serializable] public sealed class PuzzleDefinition
    {
        public string id, challengeId;
        public List<string> mechanisms = new List<string>();
        public bool ordered, permanent = true, persistPartial, resetOnExit = true, retry = true;
        public float timeWindow;
        public string destinationId;
    }
    [Serializable] public sealed class ProgressionDefinitions
    {
        public List<QuestDefinition> quests = new List<QuestDefinition>();
        public List<RelationshipDefinition> relationships = new List<RelationshipDefinition>();
        public List<RewardDefinition> rewards = new List<RewardDefinition>();
        public List<CutsceneDefinition> cutscenes = new List<CutsceneDefinition>();
        public List<ChallengeDefinition> challenges = new List<ChallengeDefinition>();
        public List<RaceDefinition> races = new List<RaceDefinition>();
        public List<PuzzleDefinition> puzzles = new List<PuzzleDefinition>();
    }
}
