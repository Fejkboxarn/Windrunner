using System;
using UnityEngine;
using UnityEngine.Events;

namespace Windrunner.Production.Progression
{
    public enum InteractionCommand { Fact, AcceptQuest, TurnInQuest, DiscoverChallenge, StartRace, PuzzleMechanism, PuzzleDestination }
    public enum InteractionChannel { Input, AbilityHit, Traversal }
    public class ProgressionInteraction : MonoBehaviour
    {
        public string stableId, definitionId, regionId, prompt = "Interact";
        public InteractionCommand command;
        public FactKind factKind = FactKind.Interaction;
        public bool persistent, allowInput = true, allowAbilityHit, allowTraversal;
        [Min(0.1f)] public float range = 3;
        public string requiredCompletedQuest;
        public string requiredUnlockId;
        public RaceHost race;
        public PuzzleHost puzzle;
        public UnityEvent succeeded;
        public bool CanUse(ProgressionActor actor, InteractionChannel channel)
            => string.IsNullOrEmpty(RejectionReason(actor, channel));
        public virtual string RejectionReason(ProgressionActor actor, InteractionChannel channel)
        {
            if (!isActiveAndEnabled) return "Station disabled.";
            if (actor == null || actor.Service == null) return "Player progression binding missing.";
            if (string.IsNullOrWhiteSpace(stableId)) return "Station identity missing.";
            if (actor.Input != null && actor.Input.IsGameplayBlocked) return "Gameplay input is blocked by a menu or cinematic.";
            if (channel == InteractionChannel.Input && !allowInput || channel == InteractionChannel.AbilityHit && !allowAbilityHit || channel == InteractionChannel.Traversal && !allowTraversal) return "This interaction channel is not allowed.";
            if ((actor.Position - transform.position).sqrMagnitude > range * range) return "Move closer (range " + range.ToString("0.0") + " m).";
            if (!string.IsNullOrEmpty(requiredCompletedQuest) && actor.Service.Status(requiredCompletedQuest) != QuestStatus.Completed) return "Required quest is not completed: " + requiredCompletedQuest;
            if (!string.IsNullOrEmpty(requiredUnlockId) && !actor.Service.HasUnlock(requiredUnlockId)) return "Required unlock missing: " + requiredUnlockId;
            if (command == InteractionCommand.AcceptQuest && actor.Service.Status(definitionId) != QuestStatus.Available) return "Quest is " + actor.Service.Status(definitionId) + ".";
            if (command == InteractionCommand.TurnInQuest && actor.Service.Status(definitionId) != QuestStatus.ReadyToTurnIn) return "Quest is " + actor.Service.Status(definitionId) + "; finish its objectives first.";
            if (command == InteractionCommand.StartRace && race == null) return "Race binding missing.";
            if (command == InteractionCommand.StartRace && race.Session?.Status == AttemptStatus.Active) return "Race already active; cross the checkpoint and finish gates.";
            if ((command == InteractionCommand.PuzzleMechanism || command == InteractionCommand.PuzzleDestination) && puzzle == null) return "Puzzle binding missing.";
            if (puzzle != null && puzzle.Session?.Status == PuzzleStatus.Solved) return "Puzzle solved.";
            return null;
        }
        public virtual bool TryUse(ProgressionActor actor, InteractionChannel channel, string occurrence = null)
        {
            if (!CanUse(actor, channel)) return false;
            bool result;
            switch (command)
            {
                case InteractionCommand.AcceptQuest: result = actor.Service.Accept(definitionId); break;
                case InteractionCommand.TurnInQuest: result = actor.Service.TurnIn(definitionId); break;
                case InteractionCommand.DiscoverChallenge: result = actor.Service.DiscoverChallenge(definitionId); break;
                case InteractionCommand.StartRace: result = race != null && race.StartAttempt(actor); break;
                case InteractionCommand.PuzzleMechanism: result = puzzle != null && puzzle.Activate(definitionId, actor); break;
                case InteractionCommand.PuzzleDestination: result = puzzle != null && puzzle.Reach(definitionId, actor); break;
                default:
                    if (factKind == FactKind.RaceCompleted || factKind == FactKind.PuzzleCompleted || factKind == FactKind.TraversalCompleted || factKind == FactKind.EnemyDefeated) return false;
                    // Persistent world objects have one stable occurrence; repeatable uses get a fresh occurrence.
                    result = actor.Service.Observe(new GameplayFact { playerId = actor.Service.PlayerId, occurrenceId = persistent ? "world/" + stableId : occurrence ?? Guid.NewGuid().ToString("N"),
                        definitionId = definitionId, targetId = stableId, regionId = regionId, kind = factKind, verified = true, persistent = persistent }); break;
            }
            if (result) succeeded?.Invoke(); return result;
        }
    }
}
