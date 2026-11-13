#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionFixtureLifecycleTests
    {
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
            var store = new MemoryProgressionStore();
            var service = new ProgressionService(catalog.definitions, "engineering-fixture", store); root.Runtime.Services.Register(service);
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
                var actor = Object.FindFirstObjectByType<ProgressionActor>(); Assert.AreSame(service, actor.Service);
                var interactions = Object.FindObjectsByType<ProgressionInteraction>(FindObjectsSortMode.None);
                Assert.IsTrue(interactions.Single(x => x.name == "Accept character A").TryUse(actor, InteractionChannel.Input));
                Assert.IsTrue(interactions.Single(x => x.name == "Accept character B").TryUse(actor, InteractionChannel.Input));
                var journal = Object.FindFirstObjectByType<ProgressionJournal>(); Assert.IsTrue(journal.SetOpen(true));
                Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(inputHost.Input.IsGameplayBlocked);
                Assert.IsTrue(journal.SetOpen(false)); Assert.IsFalse(inputHost.Input.IsGameplayBlocked);
                var puzzle = Object.FindFirstObjectByType<PuzzleHost>(); Assert.IsTrue(puzzle.Activate("one", actor)); Assert.IsTrue(puzzle.Activate("two", actor));
                Assert.AreEqual(PuzzleStatus.Solved, puzzle.Session.Status);
                // Actual authored world interaction; move into its validated range, no evaluator injection.
                actor.transform.position = new Vector3(0, 0.2f, 3); Physics.SyncTransforms();
                Assert.IsTrue(interactions.Single(x => x.name == "World switch").TryUse(actor, InteractionChannel.Input));
                service.Track("fixture-a-quest"); service.Track("fixture-b-quest");
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, service.Status("fixture-b-quest"));
                actor.transform.position = new Vector3(2, 0.2f, 23); Physics.SyncTransforms(); yield return new WaitForSeconds(0.2f);
                Assert.IsTrue(interactions.Single(x => x.name == "Turn in B").TryUse(actor, InteractionChannel.Input));
                var cinematic = Object.FindFirstObjectByType<ProgressionCinematicPlayer>();
                float deadline = Time.realtimeSinceStartup + 2;
                while (!cinematic.CanStart && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(cinematic.CanStart, "Cinematic safety: blocked=" + actor.Input.IsGameplayBlocked + " paused=" + UnityTimeAuthority.IsPaused + " guard=" + ((IProgressionControlSafety)cinematic.safety).CanStartCinematic + " position=" + actor.transform.position);
                Assert.IsTrue(cinematic.Play("fixture-b-cutscene")); yield return new WaitForSecondsRealtime(1.4f);
                Assert.IsFalse(cinematic.IsPlaying); Assert.AreEqual(CinematicStatus.Watched, service.Snapshot().cutscenes.Single().status);
                Assert.IsFalse(inputHost.Input.IsGameplayBlocked); Assert.Greater(Time.timeScale, 0);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
                Assert.AreSame(service, Object.FindFirstObjectByType<ProgressionActor>().Service);
                Assert.AreEqual(1, Object.FindObjectsByType<ProjectRoot>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(PuzzleStatus.Solved, Object.FindFirstObjectByType<PuzzleHost>().Session.Status);
                Assert.AreEqual(QuestStatus.Completed, service.Status("fixture-b-quest")); Assert.IsFalse(service.TurnIn("fixture-b-quest"));
                using var loaded = new ProgressionService(catalog.definitions, "engineering-fixture", store);
                Assert.AreEqual(service.Snapshot().relationships.Count, loaded.Snapshot().relationships.Count);
            }
            finally { if (rootObject != null) Object.Destroy(rootObject); }
            yield return null;
            var empty = SceneManager.CreateScene("Progression fixture teardown"); SceneManager.SetActiveScene(empty);
            var scene = SceneManager.GetSceneByPath(path); if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
