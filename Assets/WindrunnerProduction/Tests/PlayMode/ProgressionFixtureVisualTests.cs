#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionFixtureVisualTests
    {
        // Explicit rendered-only run. Screenshots are review evidence, not an automated aesthetic verdict.
        [UnityTest, Category("Profiling")]
        public IEnumerator FixtureJournalRenderedResolutionsAndLongContent()
        {
            if (ProjectRoot.Active != null) { Object.Destroy(ProjectRoot.Active.gameObject); yield return null; }
            var catalog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ProgressionCatalog>("Assets/WindrunnerProduction/Progression/Fixtures/FixtureCatalog.asset"));
            var rootObject = new GameObject("Visual test root"); rootObject.SetActive(false);
            var root = rootObject.AddComponent<ProjectRoot>(); var host = rootObject.AddComponent<WindrunnerInputHost>();
            host.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/WindrunnerProduction/Input/Generated/WindrunnerGameplay.inputactions"), null, null);
            rootObject.SetActive(true);
            var service = new ProgressionService(catalog.definitions, "engineering-fixture", new MemoryProgressionStore()); root.Runtime.Services.Register(service);
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            var selectedSize = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int previousSize = (int)selectedSize.GetValue(gameView);
            var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { GameViewSizeGroupType.Standalone });
            var groupType = group.GetType(); int originalCount = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ProgressionFixtureGuide.ScenePath, new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
                var journal = Object.FindFirstObjectByType<ProgressionJournal>(); journal.catalog = catalog;
                foreach (int width in new[] { 1280, 1600, 1920 })
                {
                    int height = width * 9 / 16;
                    var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var enumType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var size = Activator.CreateInstance(sizeType, Enum.Parse(enumType, "FixedResolution"), width, height, "Fixture validation");
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedSize.SetValue(gameView, (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1);
                    gameView.Repaint(); for (int n = 0; n < 10; n++) yield return null;
                    Assert.AreEqual(width, Screen.width); Assert.AreEqual(height, Screen.height);
                    yield return Capture("fixture-world-" + width);
                    Assert.IsTrue(journal.SetOpen(true)); yield return Capture("fixture-journal-" + width);
                    journal.SetOpen(false);
                }
                // Only the temporary catalog is expanded; authored content and real saves are never edited.
                catalog.definitions.quests[0].displayName = "A deliberately long engineering chapter title for localized text and narrow navigation columns";
                catalog.definitions.quests[0].description = string.Join("\n\n", Enumerable.Repeat("Long-form guidance: travel to the clearly labeled world switch, observe both independent chapter objectives, and return to the matching character after crossing the race finish. This temporary text verifies wrapping and scrolling without introducing story content.", 10));
                for (int i = 0; i < 20; i++) catalog.definitions.quests.Add(new QuestDefinition { id = "visual-only-" + i, kind = QuestKind.Optional, displayName = "Additional engineering navigation entry " + i, groups = new System.Collections.Generic.List<ObjectiveGroup> { new ObjectiveGroup { id = "open", objectives = new System.Collections.Generic.List<ObjectiveDefinition> { new ObjectiveDefinition { id = "switch", kind = FactKind.Interaction, definitionId = "fixture-switch", description = "Use the world switch" } } } } });
                journal.enabled = false;
                using var stressService = new ProgressionService(catalog.definitions, "engineering-fixture", new MemoryProgressionStore());
                typeof(ProgressionJournal).GetField("service", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(journal, stressService);
                journal.enabled = true;
                selectedSize.SetValue(gameView, originalCount); gameView.Repaint(); for (int n = 0; n < 10; n++) yield return null;
                Assert.AreEqual(1280, Screen.width); Assert.IsTrue(journal.SetOpen(true)); yield return Capture("fixture-journal-long-1280");
                for (int i = 0; i < 21; i++) journal.SelectNext(1);
                Assert.AreEqual("visual-only-19", journal.SelectedQuestId); yield return Capture("fixture-journal-many-1280");
                journal.SetOpen(false);
            }
            finally
            {
                selectedSize.SetValue(gameView, previousSize);
                for (int index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1; index >= originalCount; index--)
                    groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                if (rootObject != null) Object.Destroy(rootObject); Object.Destroy(catalog);
            }
            yield return null;
            var empty = SceneManager.CreateScene("Visual fixture teardown"); SceneManager.SetActiveScene(empty);
            var scene = SceneManager.GetSceneByPath(ProgressionFixtureGuide.ScenePath); if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private static IEnumerator Capture(string name)
        {
            for (int n = 0; n < 5; n++) yield return null;
            string path = Path.GetFullPath("Logs/ProductionMigration/" + name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            for (int n = 0; n < 10; n++) yield return null;
            Assert.IsTrue(File.Exists(path), path);
        }
    }
}
#endif
