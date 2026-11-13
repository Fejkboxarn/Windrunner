using System;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Windrunner.Production.Progression;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionAuthoringTests
    {
        [Test] public void FixtureResetPreservesNormalSaveAndRetainsPreviousEngineeringGeneration()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Windrunner-fixture-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string normal = Path.Combine(directory, "progression.json"), fixture = Path.Combine(directory, ProgressionFixtureGuide.Slot + ".json");
            try
            {
                File.WriteAllText(normal, "normal-player-save-sentinel");
                var previous = new ProgressionState { playerId = "engineering-fixture" };
                previous.relationships.Add(new RelationshipRecord { id = "fixture-a", rank = 1 });
                new FileProgressionStore(fixture).Commit(previous); string before = File.ReadAllText(fixture);
                Assert.Throws<InvalidOperationException>(() => ProgressionFixtureGuide.ResetIsolatedSave(directory, ProgressionFixtureGuide.ScenePath, "progression", "engineering-fixture"));
                Assert.AreEqual(before, File.ReadAllText(fixture));
                ProgressionFixtureGuide.ResetIsolatedSave(directory, ProgressionFixtureGuide.ScenePath, ProgressionFixtureGuide.Slot, "engineering-fixture");
                Assert.IsEmpty(new FileProgressionStore(fixture).Load().relationships);
                Assert.AreEqual(before, File.ReadAllText(fixture + ".bak")); Assert.AreEqual("normal-player-save-sentinel", File.ReadAllText(normal));
            }
            finally
            {
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
        [Test] public void FixtureUpgradePreservesStableIdentitiesAndRejectsNonFixtureReset()
        {
            Type("ProgressionFixtureUsabilitySetup").GetMethod("Upgrade").Invoke(null, null);
            Assert.IsTrue(ProgressionFixtureGuide.IsFixtureIdentity(ProgressionFixtureGuide.ScenePath, ProgressionFixtureGuide.Slot, "engineering-fixture"));
            Assert.IsFalse(ProgressionFixtureGuide.IsFixtureIdentity(ProgressionFixtureGuide.ScenePath, "progression", "engineering-fixture"));
            Assert.IsFalse(ProgressionFixtureGuide.IsFixtureIdentity("Assets/AirflowPrototype/Generated/Batch1_Playground.unity", ProgressionFixtureGuide.Slot, "engineering-fixture"));
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>("Assets/WindrunnerProduction/Progression/Fixtures/FixtureCatalog.asset");
            Assert.IsTrue(catalog.definitions.quests.Any(q => q.id == "fixture-a-quest"));
            Assert.IsTrue(catalog.definitions.quests.Any(q => q.id == "fixture-b-quest"));
        }
        [Test] public void AuthoredFixtureHasNoBrokenSerializedReferences()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/WindrunnerProduction/Progression/Fixtures/ProgressionFixture.unity", OpenSceneMode.Single);
                var issues = (System.Collections.Generic.List<string>)Type("ProgressionSceneValidation").GetMethod("Validate").Invoke(null, new object[] { scene });
                Assert.IsEmpty(issues, string.Join("\n", issues));
                foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        Assert.IsNotNull(component);
                        using var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                        while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceEntityIdValue != default)
                            Assert.IsNotNull(property.objectReferenceValue, transform.name + "/" + component.GetType().Name + "/" + property.propertyPath);
                    }
                }
            }
            finally
            {
                if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AirflowPrototype.EditorTools." + name)).First(t => t != null);
        [Test] public void FixtureAndStudioCopyHaveValidReferencesAndIndependentStableIds()
        {
            // Creates only a new engineering fixture; refuses overwrites and preserves the current scene.
            Type("ProgressionFixtureBuilder").GetMethod("Create").Invoke(null, null);
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>("Assets/WindrunnerProduction/Progression/Fixtures/FixtureCatalog.asset");
            Assert.IsNotNull(catalog); Assert.IsEmpty(ProgressionValidation.Validate(catalog.definitions, true));
            var copy = (ProgressionCatalog)Type("ProgressionStudio").GetMethod("CopyWithNewIds").Invoke(null, new object[] { catalog });
            try
            {
                Assert.IsEmpty(ProgressionValidation.Validate(copy.definitions, true));
                Assert.AreNotEqual(catalog.definitions.quests[0].id, copy.definitions.quests[0].id);
                Assert.AreEqual(copy.definitions.quests[0].characterId, copy.definitions.relationships[0].id);
                Assert.AreSame(catalog.definitions.cutscenes[0].content, copy.definitions.cutscenes[0].content);
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(copy, "fixture add");
                int count = copy.definitions.rewards.Count;
                Type("ProgressionStudio").GetMethod("AddRecord").Invoke(null, new object[] { copy, "rewards" });
                Undo.FlushUndoRecordObjects(); Assert.AreEqual(count + 1, copy.definitions.rewards.Count);
                Undo.PerformUndo(); Assert.AreEqual(count, copy.definitions.rewards.Count);
            }
            finally { Undo.ClearUndo(copy); Object.DestroyImmediate(copy); }
        }
    }
}
