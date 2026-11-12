using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Windrunner.Production.Tests
{
    public sealed class ContentIntegrityTests
    {
        [Test]
        public void GameplaySceneAndOwnedPrefabsHaveOnlyRecordedBaselineReferenceIssues()
        {
            var failures = new List<string>();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled) continue;
                    var loaded = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                    foreach (var root in loaded.GetRootGameObjects()) Audit(root, scene.path, failures);
                }
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AirflowPrototype", "Assets/WindrunnerProduction" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try { Audit(root, path, failures); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            // Explicit 5P content debt, not a claim that references are clean.
            // Multiset equality detects additions AND removal (which requires updating this record).
            const string scenePath = "Assets/AirflowPrototype/Generated/Batch1_Playground.unity: ";
            var known = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                known.Add(scenePath + "Waterfall_Splashes_1/AudioSource.m_Resource");
                known.Add(scenePath + "Waterfall_Splashes_2/AudioSource.m_Resource");
            }
            foreach (var name in new[] { "Birds", "Birds (1)", "Birds (2)" })
                for (int i = 0; i < 2; i++)
                    known.Add(scenePath + name + "/ParticleSystem.UVModule.sprites.Array.data[" + i + "].sprite");
            CollectionAssert.AreEquivalent(known, failures, "Reference debt must match the recorded 5P baseline exactly.");
        }

        private static void Audit(GameObject root, string path, List<string> failures)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    failures.Add(path + ": " + transform.name + " missing script");
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component == null) continue;
                    using (var serialized = new SerializedObject(component))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
                                failures.Add(path + ": " + transform.name + "/" + component.GetType().Name + "." + property.propertyPath);
                        }
                    }
                }
            }
        }
    }
}
