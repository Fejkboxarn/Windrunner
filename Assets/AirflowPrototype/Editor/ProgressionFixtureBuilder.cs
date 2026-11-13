using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;

namespace AirflowPrototype.EditorTools
{
    public static class ProgressionFixtureBuilder
    {
        public const string Folder = "Assets/WindrunnerProduction/Progression/Fixtures";
        [MenuItem("Tools/Windrunner/Create Isolated Progression Fixture")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create fixtures in Edit Mode.");
            string scenePath = Folder + "/ProgressionFixture.unity";
            if (AssetDatabase.LoadMainAssetAtPath(scenePath) != null) { Debug.Log("Fixture already exists: " + scenePath); return; }
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool emptyUntitled = string.IsNullOrEmpty(previous.path) && previous.rootCount == 0 && !previous.isDirty;
            if (string.IsNullOrEmpty(previous.path) && !emptyUntitled) throw new InvalidOperationException("Save the untitled scene before creating the fixture; it will not be overwritten.");
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(Folder + "/FixtureCatalog.asset");
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder + "/FixtureTimeline.asset");
            ActivationTrack track;
            if (AssetDatabase.IsValidFolder(Folder))
            {
                // Resume a partially generated fixture without replacing its assets.
                if (catalog == null || timeline == null || ProgressionValidation.Validate(catalog.definitions, true).Count != 0) throw new InvalidOperationException("Incomplete fixture assets need inspection; no replacement performed.");
                track = timeline.GetRootTrack(0) as ActivationTrack;
                if (track == null) throw new InvalidOperationException("Fixture activation track missing.");
            }
            else
            {
                AssetDatabase.CreateFolder("Assets/WindrunnerProduction/Progression", "Fixtures");
                catalog = ScriptableObject.CreateInstance<ProgressionCatalog>(); Populate(catalog.definitions);
                AssetDatabase.CreateAsset(catalog, Folder + "/FixtureCatalog.asset");
                timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, Folder + "/FixtureTimeline.asset");
                track = timeline.CreateTrack<ActivationTrack>(null, "Engineering marker activation");
                var clip = track.CreateDefaultClip(); clip.start = 0; clip.duration = 1;
                foreach (var c in catalog.definitions.cutscenes) c.content = timeline;
                EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssetIfDirty(catalog); AssetDatabase.SaveAssetIfDirty(timeline);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("Fixture services").AddComponent<ProjectRoot>();
                var input = root.gameObject.AddComponent<WindrunnerInputHost>();
                input.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/WindrunnerProduction/Input/Generated/WindrunnerGameplay.inputactions"), null, null);
                var host = new GameObject("Progression composition").AddComponent<ProgressionHost>(); host.project = root; host.catalog = catalog; host.playerId = "engineering-fixture"; host.saveSlot = "progression-engineering-fixture";
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Fixture floor"; floor.transform.position = new Vector3(0, -0.5f, 10); floor.transform.localScale = new Vector3(30, 1, 40);
                var actor = new GameObject("Fixture player").AddComponent<ProgressionActor>(); actor.host = host; actor.inputHost = input; actor.transform.position = new Vector3(0, 1, 0);
                var body = actor.gameObject.AddComponent<CharacterController>(); body.height = 2; body.center = Vector3.up;
                var reader = actor.gameObject.AddComponent<PlayerInputReader>(); var motor = actor.gameObject.AddComponent<PlayerMotor>();
                var view = new GameObject("Fixture camera").AddComponent<Camera>(); view.gameObject.AddComponent<AudioListener>();
                var movement = AssetDatabase.LoadAssetAtPath<AirflowMovementSettings>("Assets/AirflowPrototype/Generated/Batch1_MovementSettings.asset");
                motor.Configure(body, reader, view.transform, null, movement); view.gameObject.AddComponent<PlayerOrbitCamera>().Configure(actor.transform, reader, movement, 1);
                var light = new GameObject("Fixture light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, 25, 0);
                var interactions = actor.gameObject.AddComponent<PlayerInteractionController>(); interactions.actor = actor; interactions.view = view; interactions.reach = 12;
                var race = new GameObject("Fixture race").AddComponent<RaceHost>(); race.catalog = catalog; race.raceId = "fixture-race";
                CreateInteraction("Accept character A", new Vector3(-2, 1, 2), InteractionCommand.AcceptQuest, "fixture-a-quest");
                CreateInteraction("Accept character B", new Vector3(2, 1, 2), InteractionCommand.AcceptQuest, "fixture-b-quest");
                CreateInteraction("World switch", new Vector3(0, 1, 5), InteractionCommand.Fact, "fixture-switch");
                var start = CreateInteraction("Start race", new Vector3(-2, 1, 5), InteractionCommand.StartRace, ""); start.race = race;
                foreach (var step in new[] { ("checkpoint", 12f), ("finish", 20f) })
                {
                    var gate = new GameObject(step.Item1); gate.transform.position = new Vector3(0, 1, step.Item2);
                    var collider = gate.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(10, 4, 1);
                    var trigger = gate.AddComponent<ProgressionTrigger>(); trigger.actor = actor; trigger.race = race; trigger.checkpointId = step.Item1;
                }
                CreateInteraction("Turn in A", new Vector3(-2, 1, 24), InteractionCommand.TurnInQuest, "fixture-a-quest");
                CreateInteraction("Turn in B", new Vector3(2, 1, 24), InteractionCommand.TurnInQuest, "fixture-b-quest");
                var puzzle = new GameObject("Independent fixture puzzle").AddComponent<PuzzleHost>(); puzzle.actor = actor; puzzle.catalog = catalog; puzzle.puzzleId = "fixture-puzzle";
                var switchOne = CreateInteraction("Puzzle switch one", new Vector3(5, 1, 5), InteractionCommand.PuzzleMechanism, "one"); switchOne.puzzle = puzzle;
                var switchTwo = CreateInteraction("Puzzle switch two", new Vector3(5, 1, 8), InteractionCommand.PuzzleMechanism, "two"); switchTwo.puzzle = puzzle;
                var cinematic = new GameObject("Fixture cinematic").AddComponent<ProgressionCinematicPlayer>(); cinematic.actor = actor; cinematic.catalog = catalog; cinematic.gameplayCamera = view;
                cinematic.cinematicCamera = new GameObject("Cinematic camera").AddComponent<Camera>(); cinematic.cinematicCamera.transform.position = new Vector3(0, 10, -5); cinematic.cinematicCamera.transform.LookAt(new Vector3(0, 0, 10)); cinematic.cinematicCamera.enabled = false;
                cinematic.director = cinematic.gameObject.AddComponent<PlayableDirector>(); cinematic.director.playOnAwake = false; cinematic.director.playableAsset = timeline;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name = "Engineering cinematic marker"; marker.transform.position = new Vector3(0, 3, 10); marker.SetActive(false); cinematic.director.SetGenericBinding(track, marker);
                var guard = actor.gameObject.AddComponent<ProgressionFixtureSafety>(); guard.motor = motor; cinematic.safety = guard;
                var journal = actor.gameObject.AddComponent<ProgressionJournal>(); journal.actor = actor; journal.catalog = catalog; journal.interaction = interactions; journal.cinematics = cinematic;
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (emptyUntitled) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous); }
            }
            Debug.Log("Created isolated progression fixture. Open " + scenePath + " alone; it is not added to shipping build settings.");
        }
        private static ProgressionInteraction CreateInteraction(string name, Vector3 position, InteractionCommand command, string definition)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.position = position;
            var interaction = obj.AddComponent<ProgressionInteraction>(); interaction.stableId = name; interaction.prompt = name; interaction.command = command; interaction.definitionId = definition; interaction.range = 5;
            return interaction;
        }
        public static void Populate(ProgressionDefinitions d)
        {
            d.rewards.Add(new RewardDefinition { id = "fixture-unlock", contentId = "engineering-only" });
            d.challenges.Add(new ChallengeDefinition { id = "fixture-course", displayName = "Engineering race", resultKind = FactKind.RaceCompleted, firstClearRewards = new List<string> { "fixture-unlock" } });
            d.challenges.Add(new ChallengeDefinition { id = "fixture-puzzle-challenge", displayName = "Engineering switches", resultKind = FactKind.PuzzleCompleted });
            d.races.Add(new RaceDefinition { id = "fixture-race", challengeId = "fixture-course", timeLimit = 30, checkpoints = new List<RaceStep> {
                new RaceStep { alternatives = new List<string> { "checkpoint" } }, new RaceStep { alternatives = new List<string> { "finish" } } } });
            d.puzzles.Add(new PuzzleDefinition { id = "fixture-puzzle", challengeId = "fixture-puzzle-challenge", mechanisms = new List<string> { "one", "two" }, ordered = true });
            foreach (string character in new[] { "a", "b" })
            {
                string prefix = "fixture-" + character;
                d.cutscenes.Add(new CutsceneDefinition { id = prefix + "-cutscene", displayName = "Engineering marker" });
                d.relationships.Add(new RelationshipDefinition { id = prefix, displayName = "Engineering character " + character, ranks = new List<string> { "Fixture rank" }, chapters = new List<ChapterDefinition> {
                    new ChapterDefinition { id = prefix + "-chapter", rank = 1, questId = prefix + "-quest", cutsceneId = prefix + "-cutscene" } } });
                var group = new ObjectiveGroup { id = "open", objectives = new List<ObjectiveDefinition> {
                    new ObjectiveDefinition { id = "switch", description = "Use the world switch", kind = FactKind.Interaction, definitionId = "fixture-switch" } } };
                if (character == "a") group.objectives.Add(new ObjectiveDefinition { id = "race", description = "Finish the engineering race", kind = FactKind.RaceCompleted, definitionId = "fixture-course" });
                d.quests.Add(new QuestDefinition { id = prefix + "-quest", displayName = "Engineering chapter " + character, description = "Test fixture, not story content", kind = QuestKind.Story,
                    characterId = prefix, chapterId = prefix + "-chapter", groups = new List<ObjectiveGroup> { group } });
            }
        }
    }
}
