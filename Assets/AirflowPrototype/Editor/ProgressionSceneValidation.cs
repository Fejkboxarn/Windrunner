using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.Production.Progression;

namespace AirflowPrototype.EditorTools
{
    public static class ProgressionSceneValidation
    {
        public static List<string> Validate(Scene scene)
        {
            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded) return errors;
            var roots = scene.GetRootGameObjects();
            var hosts = roots.SelectMany(r => r.GetComponentsInChildren<ProgressionHost>(true)).ToArray();
            if (hosts.Length == 0) return errors;
            if (hosts.Length != 1) errors.Add(scene.path + ": one progression composition host required per authored scene.");
            var catalog = hosts[0].catalog;
            if (catalog == null) { errors.Add(scene.path + ": host catalog missing."); return errors; }
            var ids = new HashSet<string>();
            foreach (var interaction in roots.SelectMany(r => r.GetComponentsInChildren<ProgressionInteraction>(true)))
            {
                string location = scene.path + "/" + interaction.name;
                if (string.IsNullOrWhiteSpace(interaction.stableId) || !ids.Add(interaction.stableId)) errors.Add(location + ": missing/duplicate world object ID.");
                if (interaction.range <= 0 || interaction.GetComponent<Collider>() == null || (!interaction.allowInput && !interaction.allowAbilityHit && !interaction.allowTraversal)) errors.Add(location + ": invalid range/collider/interaction channels.");
                if (!string.IsNullOrEmpty(interaction.requiredCompletedQuest) && !catalog.definitions.quests.Any(q => q.id == interaction.requiredCompletedQuest)) errors.Add(location + ": missing prerequisite quest.");
                switch (interaction.command)
                {
                    case InteractionCommand.AcceptQuest:
                    case InteractionCommand.TurnInQuest:
                        if (!catalog.definitions.quests.Any(q => q.id == interaction.definitionId)) errors.Add(location + ": invalid quest ID."); break;
                    case InteractionCommand.DiscoverChallenge:
                        if (!catalog.definitions.challenges.Any(c => c.id == interaction.definitionId)) errors.Add(location + ": invalid challenge ID."); break;
                    case InteractionCommand.StartRace:
                        if (interaction.race == null) errors.Add(location + ": RaceHost missing."); break;
                    case InteractionCommand.PuzzleMechanism:
                    case InteractionCommand.PuzzleDestination:
                        var puzzle = interaction.puzzle == null ? null : catalog.definitions.puzzles.Find(p => p.id == interaction.puzzle.puzzleId);
                        if (puzzle == null || (interaction.command == InteractionCommand.PuzzleMechanism ? !puzzle.mechanisms.Contains(interaction.definitionId) : puzzle.destinationId != interaction.definitionId)) errors.Add(location + ": invalid puzzle/mechanism/destination binding."); break;
                    default:
                        if (string.IsNullOrWhiteSpace(interaction.definitionId) || interaction.factKind == FactKind.EnemyDefeated || interaction.factKind == FactKind.RaceCompleted || interaction.factKind == FactKind.PuzzleCompleted || interaction.factKind == FactKind.TraversalCompleted) errors.Add(location + ": generic facts require an interaction/item/discovery ID; challenges and defeats need authoritative adapters."); break;
                }
            }
            foreach (var race in roots.SelectMany(r => r.GetComponentsInChildren<RaceHost>(true)))
                if (race.catalog != catalog || !catalog.definitions.races.Any(r => r.id == race.raceId)) errors.Add(race.name + ": invalid race catalog/ID.");
            foreach (var puzzle in roots.SelectMany(r => r.GetComponentsInChildren<PuzzleHost>(true)))
                if (puzzle.actor == null || puzzle.catalog != catalog || !catalog.definitions.puzzles.Any(p => p.id == puzzle.puzzleId)) errors.Add(puzzle.name + ": invalid puzzle actor/catalog/ID.");
            foreach (var trigger in roots.SelectMany(r => r.GetComponentsInChildren<ProgressionTrigger>(true)))
            {
                if (trigger.actor == null || trigger.GetComponent<Collider>() == null || !trigger.GetComponent<Collider>().isTrigger) errors.Add(trigger.name + ": explicit actor and trigger collider required.");
                if (trigger.race != null)
                {
                    var race = catalog.definitions.races.Find(r => r.id == trigger.race.raceId);
                    if (race == null || !race.checkpoints.Any(s => s.alternatives.Contains(trigger.checkpointId))) errors.Add(trigger.name + ": checkpoint not in its course.");
                }
            }
            foreach (var cinematic in roots.SelectMany(r => r.GetComponentsInChildren<ProgressionCinematicPlayer>(true)))
                if (cinematic.actor == null || cinematic.catalog != catalog || cinematic.director == null || cinematic.gameplayCamera == null || cinematic.cinematicCamera == null || cinematic.gameplayCamera == cinematic.cinematicCamera || !(cinematic.safety is IProgressionControlSafety)) errors.Add(cinematic.name + ": missing cinematic control/camera/director bindings.");
            return errors;
        }
    }
}
