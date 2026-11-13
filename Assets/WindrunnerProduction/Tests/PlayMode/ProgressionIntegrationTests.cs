using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionTestSafety : MonoBehaviour, IProgressionControlSafety { public bool CanStartCinematic => true; }
    public sealed class ProgressionTestPlayable : PlayableAsset
    {
        public override double duration => 1;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
    }
    public sealed class ProgressionIntegrationTests
    {
        private readonly List<Object> objects = new List<Object>();
        private WindrunnerInputService input;
        private ProgressionService service;
        private T Keep<T>(T obj) where T : Object { objects.Add(obj); return obj; }
        private GameObject New(string name) => Keep(new GameObject(name));
        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); input?.Dispose(); service?.Dispose(); input = null; service = null;
        }
        private ProgressionActor Actor(ProgressionDefinitions definitions)
        {
            var template = Keep(ScriptableObject.CreateInstance<InputActionAsset>());
            template.AddActionMap("Gameplay").AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick").expectedControlType = "Vector2";
            template.AddActionMap("System").AddAction("Pause", binding: "<Keyboard>/escape"); template.AddActionMap("UI").AddAction("Cancel");
            input = new WindrunnerInputService(template); service = new ProgressionService(definitions, "test", new MemoryProgressionStore());
            var actor = New("Progression actor").AddComponent<ProgressionActor>(); actor.Bind(service, input); return actor;
        }
        [UnityTest] public IEnumerator WorldInteractionAndPhysicsRaceReachQuestEvaluator()
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ProgressionCatalog>());
            catalog.definitions.challenges.Add(new ChallengeDefinition { id = "course", resultKind = FactKind.RaceCompleted });
            catalog.definitions.races.Add(new RaceDefinition { id = "race", challengeId = "course", checkpoints = new List<RaceStep> {
                new RaceStep { alternatives = new List<string> { "finish" } } } });
            catalog.definitions.quests.Add(new QuestDefinition { id = "quest", kind = QuestKind.Optional, groups = new List<ObjectiveGroup> {
                new ObjectiveGroup { id = "g", objectives = new List<ObjectiveDefinition> {
                    new ObjectiveDefinition { id = "interact", kind = FactKind.Interaction, definitionId = "switch" },
                    new ObjectiveDefinition { id = "race", kind = FactKind.RaceCompleted, definitionId = "course" } } } } });
            var actor = Actor(catalog.definitions); service.Accept("quest");
            var interaction = New("Switch").AddComponent<ProgressionInteraction>(); interaction.stableId = "switch-1"; interaction.definitionId = "switch";
            actor.transform.position = Vector3.right * 8; Assert.IsFalse(interaction.TryUse(actor, InteractionChannel.Input));
            actor.transform.position = Vector3.zero; Assert.IsTrue(interaction.TryUse(actor, InteractionChannel.Input, "use")); Assert.IsFalse(interaction.TryUse(actor, InteractionChannel.Input, "use"));
            var race = New("Race").AddComponent<RaceHost>(); race.catalog = catalog; race.raceId = "race"; Assert.IsTrue(race.StartAttempt(actor));
            var body = actor.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; actor.gameObject.AddComponent<SphereCollider>();
            var gate = New("Finish"); gate.transform.position = new Vector3(0, 0, 5); var box = gate.AddComponent<BoxCollider>(); box.isTrigger = true;
            var trigger = gate.AddComponent<ProgressionTrigger>(); trigger.actor = actor; trigger.race = race; trigger.checkpointId = "finish";
            yield return new WaitForFixedUpdate(); body.position = gate.transform.position; Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.AreEqual(AttemptStatus.Succeeded, race.Session.Status); Assert.AreEqual(QuestStatus.ReadyToTurnIn, service.Status("quest"));
            Assert.IsTrue(service.TurnIn("quest")); Assert.IsFalse(service.TurnIn("quest")); Assert.IsEmpty(service.Snapshot().relationships);
        }
        [UnityTest] public IEnumerator CinematicSkipInterruptionAndNestedPauseRestoreAuthority()
        {
            var catalog = Keep(ScriptableObject.CreateInstance<ProgressionCatalog>());
            var clip = Keep(ScriptableObject.CreateInstance<ProgressionTestPlayable>());
            catalog.definitions.cutscenes.Add(new CutsceneDefinition { id = "cutscene", content = clip });
            // Authoritative pending record models a completed rank-up transaction, not a fabricated final cinematic.
            var actor = Actor(catalog.definitions);
            var seed = new ProgressionState { playerId = "test" }; seed.cutscenes.Add(new CutsceneRecord { id = "cutscene" });
            var store = new MemoryProgressionStore(); store.Commit(seed); service.Dispose(); service = new ProgressionService(catalog.definitions, "test", store); actor.Bind(service, input);
            var player = New("Cinematic").AddComponent<ProgressionCinematicPlayer>(); player.actor = actor; player.catalog = catalog;
            player.director = player.gameObject.AddComponent<PlayableDirector>(); player.safety = actor.gameObject.AddComponent<ProgressionTestSafety>();
            player.gameplayCamera = New("Gameplay camera").AddComponent<Camera>(); player.cinematicCamera = New("Cinematic camera").AddComponent<Camera>(); player.cinematicCamera.enabled = false;
            float scale = Time.timeScale;
            Assert.IsTrue(player.Play("cutscene")); Assert.IsTrue(input.IsGameplayBlocked); Assert.IsFalse(player.gameplayCamera.enabled); Assert.AreEqual(0, Time.timeScale);
            using (var pause = UnityTimeAuthority.Pause())
            {
                input.SetMenuOpen(true); Assert.IsTrue(player.Skip()); Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(input.IsGameplayBlocked);
                input.SetMenuOpen(false); Assert.IsFalse(input.IsGameplayBlocked);
            }
            Assert.AreEqual(scale, Time.timeScale); Assert.IsTrue(player.gameplayCamera.enabled); Assert.IsFalse(player.cinematicCamera.enabled);
            Assert.AreEqual(CinematicStatus.Skipped, service.Snapshot().cutscenes.Single().status);
            Assert.IsTrue(player.Play("cutscene", true)); player.enabled = false;
            Assert.AreEqual(scale, Time.timeScale); Assert.IsFalse(input.IsGameplayBlocked); Assert.AreEqual(CinematicStatus.Skipped, service.Snapshot().cutscenes.Single().status);
            yield return null;
        }
        [Test] public void InputBlocksNestAndBindingsRemainStableAcrossServiceLifetimes()
        {
            Actor(new ProgressionDefinitions());
            var action = input.GetAction("Progression", "Interact"); var id = action.id;
            using (var first = input.BlockGameplay())
            {
                using (var second = input.BlockGameplay()) { input.SetMenuOpen(true); input.SetMenuOpen(false); Assert.IsFalse(input.GetAction("Gameplay", "Move").enabled); }
                Assert.IsTrue(input.IsGameplayBlocked);
            }
            Assert.IsTrue(input.GetAction("Gameplay", "Move").enabled); Assert.IsNotEmpty(input.ProgressionBindingLabel("Interact"));
            Assert.AreEqual(Guid.Parse("049b391b-de56-4308-97d9-5ed03661bda8"), id);
        }
        [UnityTest] public IEnumerator RealCastProjectilesReachBoundMechanismAndRespectAdapterLifetime()
        {
            var d = new ProgressionDefinitions(); d.quests.Add(new QuestDefinition { id = "cast-quest", kind = QuestKind.Optional,
                groups = new List<ObjectiveGroup> { new ObjectiveGroup { id = "g", objectives = new List<ObjectiveDefinition> {
                    new ObjectiveDefinition { id = "o", kind = FactKind.Interaction, definitionId = "cast-switch", count = 2 } } } } });
            var actor = Actor(d); service.Accept("cast-quest");
            Type T(string name) => Type.GetType("AirflowPrototype." + name + ", Assembly-CSharp", true);
            var caster = actor.gameObject.AddComponent(T("AirCaster")) as Behaviour; caster.enabled = false;
            var target = New("Cast switch"); target.transform.position = Vector3.forward;
            var node = target.AddComponent(T("AirNode")); var interaction = target.AddComponent<ProgressionInteraction>();
            interaction.stableId = "first-object"; interaction.definitionId = "cast-switch"; interaction.allowAbilityHit = true; interaction.persistent = true;
            var adapterType = T("AirflowProgressionAdapter"); var adapter = (Behaviour)actor.gameObject.AddComponent(adapterType); adapter.enabled = false;
            adapterType.GetField("actor").SetValue(adapter, actor); adapterType.GetField("caster").SetValue(adapter, caster);
            var bindingType = adapterType.GetNestedType("CastTarget"); var binding = Activator.CreateInstance(bindingType);
            bindingType.GetField("node").SetValue(binding, node); bindingType.GetField("interaction").SetValue(binding, interaction);
            var bindings = Array.CreateInstance(bindingType, 1); bindings.SetValue(binding, 0); adapterType.GetField("castTargets").SetValue(adapter, bindings); adapter.enabled = true;
            var settings = Keep(ScriptableObject.CreateInstance(T("AirCastSettings")));
            var spawn = T("AirPulseProjectile").GetMethod("Spawn");
            void Fire() => spawn.Invoke(null, new object[] { caster, node, Vector3.zero, Vector3.forward, 1f, true, settings });
            Fire(); Fire(); yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, service.Snapshot().quests.Single().objectives.Single().count);
            adapter.enabled = false; interaction.stableId = "second-object"; Fire(); yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, service.Snapshot().quests.Single().objectives.Single().count);
            adapter.enabled = true; Fire(); yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(QuestStatus.ReadyToTurnIn, service.Status("cast-quest"));
            yield return new WaitForSeconds(1);
            Assert.AreEqual(0, Object.FindObjectsByType(T("AirPulseProjectile"), FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator UniqueCollectionPresentationRestoresFromPersistentFacts()
        {
            var actor = Actor(new ProgressionDefinitions()); var collectible = New("Unique collectible");
            var interaction = collectible.AddComponent<ProgressionInteraction>(); interaction.stableId = "unique-object"; interaction.definitionId = "artifact";
            interaction.factKind = FactKind.ItemCollected; interaction.persistent = true;
            var view = New("Persistent collection observer").AddComponent<ProgressionFactView>(); view.actor = actor; view.stableObjectId = interaction.stableId; view.presentation = collectible;
            yield return null; Assert.IsTrue(collectible.activeSelf);
            Assert.IsTrue(interaction.TryUse(actor, InteractionChannel.Input)); Assert.IsFalse(collectible.activeSelf);
            view.enabled = false; collectible.SetActive(true); view.enabled = true; Assert.IsFalse(collectible.activeSelf);
            Assert.AreEqual(1, service.Snapshot().persistentFacts.Count);
            var store = new MemoryProgressionStore(); store.Commit(service.Snapshot()); using var restored = new ProgressionService(new ProgressionDefinitions(), "test", store);
            Assert.IsTrue(restored.HasPersistentFact(FactKind.ItemCollected, "unique-object"));
        }
    }
}
