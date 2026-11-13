#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionFixtureLifecycleTests
    {
        private static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private static IEnumerator Press(Gamepad gamepad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
        }
        private static IEnumerator UseStation(ProgressionActor actor, ProgressionInteraction station, Keyboard keyboard)
        {
            var interaction = actor.GetComponent<PlayerInteractionController>();
            var orbit = interaction.view.GetComponents<MonoBehaviour>().FirstOrDefault(c => c.GetType().Name == "PlayerOrbitCamera");
            bool enabled = orbit != null && orbit.enabled;
            if (orbit != null) orbit.enabled = false;
            bool usedReboundInput = false;
            var oldPosition = interaction.view.transform.position; var oldRotation = interaction.view.transform.rotation;
            try
            {
                actor.transform.position = station.transform.position + new Vector3(-1.4f, -.8f, -1.4f);
                interaction.view.transform.position = station.transform.position + new Vector3(-2.8f, 1, -2.8f);
                interaction.view.transform.LookAt(station.transform.position); Physics.SyncTransforms(); yield return null;
                interaction.Probe(); Assert.AreSame(station, interaction.Target, interaction.Rejection);
                StringAssert.Contains(station.prompt, interaction.Prompt);
                if (station.name == "Accept character A")
                {
                    float range = station.range; station.range = .1f; interaction.Probe(); Assert.IsNull(interaction.Target); StringAssert.Contains("Move closer", interaction.Rejection); station.range = range;
                    station.requiredUnlockId = "fixture-test-missing"; interaction.Probe(); Assert.IsNull(interaction.Target); StringAssert.Contains("unlock", interaction.Rejection); station.requiredUnlockId = null;
                    var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    try
                    {
                        blocker.name = "Occlusion regression wall"; blocker.transform.position = Vector3.Lerp(interaction.view.transform.position, station.transform.position, .5f);
                        Physics.SyncTransforms(); interaction.Probe(); Assert.IsNull(interaction.Target); StringAssert.Contains("blocked", interaction.Rejection);
                        blocker.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms(); interaction.Probe(); Assert.AreSame(station, interaction.Target, "Unrelated trigger must not hide the station.");
                    }
                    finally { Object.Destroy(blocker); }
                    var action = actor.Input.Actions.FindAction("Progression/Interact"); action.ApplyBindingOverride(0, "<Keyboard>/f");
                    StringAssert.Contains("F", interaction.Prompt); yield return Press(keyboard, Key.F); action.ApplyBindingOverride(0, "<Keyboard>/e"); usedReboundInput = true;
                }
                if (!usedReboundInput) yield return Press(keyboard, Key.E);
            }
            finally
            {
                interaction.view.transform.SetPositionAndRotation(oldPosition, oldRotation);
                if (orbit != null) orbit.enabled = enabled;
            }
        }
        [UnityTest] public IEnumerator AuthoredFixtureTurnInTimelineJournalAndReloadKeepOneService()
        {
            const string path = "Assets/WindrunnerProduction/Progression/Fixtures/ProgressionFixture.unity";
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>("Assets/WindrunnerProduction/Progression/Fixtures/FixtureCatalog.asset");
            Assert.IsNotNull(catalog);
            if (ProjectRoot.Active != null) { Object.Destroy(ProjectRoot.Active.gameObject); yield return null; }
            var rootObject = new GameObject("Progression test persistent root"); rootObject.SetActive(false);
            var root = rootObject.AddComponent<ProjectRoot>(); var inputHost = rootObject.AddComponent<WindrunnerInputHost>();
            inputHost.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/WindrunnerProduction/Input/Generated/WindrunnerGameplay.inputactions"), null, null);
            rootObject.SetActive(true);
            inputHost.Input.Actions.FindAction("Progression/Interact").ApplyBindingOverride(0, "<Keyboard>/e");
            inputHost.Input.Actions.FindAction("Progression/Journal").ApplyBindingOverride(0, "<Keyboard>/j");
            var store = new MemoryProgressionStore();
            var service = new ProgressionService(catalog.definitions, "engineering-fixture", store); root.Runtime.Services.Register(service);
            var originalSettings = InputSystem.settings; var flags = originalSettings.hideFlags;
            if (flags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
            var testSettings = Object.Instantiate(originalSettings); testSettings.hideFlags = HideFlags.DontSave;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings; var keyboard = InputSystem.AddDevice<Keyboard>(); var gamepad = InputSystem.AddDevice<Gamepad>();
            float capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
                var actor = Object.FindFirstObjectByType<ProgressionActor>(); Assert.AreSame(service, actor.Service);
                var interactions = Object.FindObjectsByType<ProgressionInteraction>(FindObjectsSortMode.None);
                yield return UseStation(actor, interactions.Single(x => x.name == "Accept character A"), keyboard);
                yield return UseStation(actor, interactions.Single(x => x.name == "Accept character B"), keyboard);
                Assert.AreEqual(QuestStatus.Active, service.Status("fixture-a-quest")); Assert.AreEqual(QuestStatus.Active, service.Status("fixture-b-quest"));
                var journal = Object.FindFirstObjectByType<ProgressionJournal>(); yield return Press(keyboard, Key.J); Assert.IsTrue(journal.IsOpen);
                Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(inputHost.Input.IsGameplayBlocked);
                journal.enabled = false; yield return null; Assert.IsFalse(inputHost.Input.IsGameplayBlocked); Assert.Greater(Time.timeScale, 0);
                journal.enabled = true; yield return null;
                var changed = (System.Action)typeof(ProgressionService).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(service);
                Assert.AreEqual(1, changed.GetInvocationList().Count(d => ReferenceEquals(d.Target, journal)), "Re-enable must restore exactly one UI subscription.");
                yield return Press(keyboard, Key.J); Assert.IsTrue(journal.IsOpen);
                var selected = journal.SelectedQuestId; yield return Press(keyboard, Key.DownArrow); Assert.AreNotEqual(selected, journal.SelectedQuestId);
                yield return Press(keyboard, Key.T); Assert.AreEqual(journal.SelectedQuestId, service.Snapshot().trackedQuestId);
                yield return Press(keyboard, Key.Escape); Assert.IsTrue(inputHost.Input.IsMenuOpen); Assert.IsTrue(journal.IsOpen); Assert.AreEqual(0, Time.timeScale);
                yield return Press(keyboard, Key.Escape); Assert.IsFalse(inputHost.Input.IsMenuOpen); Assert.AreEqual(0, Time.timeScale, "Journal retains its own pause request.");
                yield return Press(gamepad, GamepadButton.DpadDown);
                yield return Press(gamepad, GamepadButton.South); Assert.AreEqual(journal.SelectedQuestId, service.Snapshot().trackedQuestId);
                yield return Press(gamepad, GamepadButton.East); Assert.IsFalse(journal.IsOpen); Assert.IsFalse(inputHost.Input.IsGameplayBlocked);
                var puzzle = Object.FindFirstObjectByType<PuzzleHost>();
                yield return UseStation(actor, interactions.Single(x => x.name == "Puzzle switch one"), keyboard);
                yield return UseStation(actor, interactions.Single(x => x.name == "Puzzle switch two"), keyboard);
                Assert.AreEqual(PuzzleStatus.Solved, puzzle.Session.Status);
                // Actual authored world interaction; move into its validated range, no evaluator injection.
                actor.transform.position = new Vector3(0, 0.2f, 3); Physics.SyncTransforms();
                yield return UseStation(actor, interactions.Single(x => x.name == "World switch"), keyboard);
                var race = Object.FindFirstObjectByType<RaceHost>(); yield return UseStation(actor, interactions.Single(x => x.name == "Start race"), keyboard); Assert.AreEqual(AttemptStatus.Active, race.Session.Status);
                actor.transform.position = new Vector3(0, 0.2f, 7); Physics.SyncTransforms();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int frame = 0; frame < 240 && race.Session.Status == AttemptStatus.Active; frame++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.AreEqual(AttemptStatus.Succeeded, race.Session.Status, "Actual production motor must cross both authored trigger planes.");
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, service.Status("fixture-a-quest"));
                service.Track("fixture-a-quest"); service.Track("fixture-b-quest");
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, service.Status("fixture-b-quest"));
                actor.transform.position = new Vector3(2, 0.2f, 23); Physics.SyncTransforms(); yield return new WaitForSeconds(0.2f);
                yield return UseStation(actor, interactions.Single(x => x.name == "Turn in A"), keyboard);
                yield return UseStation(actor, interactions.Single(x => x.name == "Turn in B"), keyboard);
                Assert.AreEqual(2, service.Snapshot().relationships.Count(r => r.rank == 1));
                var cinematic = Object.FindFirstObjectByType<ProgressionCinematicPlayer>();
                float deadline = Time.realtimeSinceStartup + 2;
                while (!cinematic.CanStart && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(cinematic.CanStart, "Cinematic safety: blocked=" + actor.Input.IsGameplayBlocked + " paused=" + UnityTimeAuthority.IsPaused + " guard=" + ((IProgressionControlSafety)cinematic.safety).CanStartCinematic + " position=" + actor.transform.position);
                Assert.IsTrue(cinematic.Play("fixture-b-cutscene")); yield return new WaitForSecondsRealtime((float)cinematic.director.duration + 0.4f);
                Assert.IsFalse(cinematic.IsPlaying); Assert.AreEqual(CinematicStatus.Watched, service.Snapshot().cutscenes.Single(c => c.id == "fixture-b-cutscene").status);
                Assert.IsFalse(inputHost.Input.IsGameplayBlocked); Assert.Greater(Time.timeScale, 0);
                Assert.IsTrue(cinematic.Play("fixture-a-cutscene")); yield return null;
                cinematic.enabled = false; yield return null;
                Assert.AreEqual(CinematicStatus.Pending, service.Snapshot().cutscenes.Single(c => c.id == "fixture-a-cutscene").status);
                Assert.IsFalse(inputHost.Input.IsGameplayBlocked); cinematic.enabled = true;
                Assert.IsTrue(cinematic.Play("fixture-a-cutscene")); yield return Press(keyboard, Key.Enter);
                Assert.AreEqual(CinematicStatus.Skipped, service.Snapshot().cutscenes.Single(c => c.id == "fixture-a-cutscene").status);
                Assert.IsFalse(inputHost.Input.IsGameplayBlocked);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
                Assert.AreSame(service, Object.FindFirstObjectByType<ProgressionActor>().Service);
                Assert.AreEqual("", typeof(ProgressionJournal).GetField("notification", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(Object.FindFirstObjectByType<ProgressionJournal>()), "Reload must not replay old completion notifications.");
                Assert.AreEqual(1, Object.FindObjectsByType<ProjectRoot>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(PuzzleStatus.Solved, Object.FindFirstObjectByType<PuzzleHost>().Session.Status);
                Assert.AreEqual(QuestStatus.Completed, service.Status("fixture-b-quest")); Assert.IsFalse(service.TurnIn("fixture-b-quest"));
                using var loaded = new ProgressionService(catalog.definitions, "engineering-fixture", store);
                Assert.AreEqual(service.Snapshot().relationships.Count, loaded.Snapshot().relationships.Count);
            }
            finally
            {
                Time.captureDeltaTime = capture;
                InputSystem.RemoveDevice(gamepad); InputSystem.RemoveDevice(keyboard); InputSystem.settings = originalSettings; originalSettings.hideFlags = flags; Object.Destroy(testSettings);
                if (rootObject != null) Object.Destroy(rootObject);
            }
            yield return null;
            var empty = SceneManager.CreateScene("Progression fixture teardown"); SceneManager.SetActiveScene(empty);
            var scene = SceneManager.GetSceneByPath(path); if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
