using System.Collections.Generic;
using System;
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
        public void GameplayPlayerHasOneCoreOwnerAndConsistentMovementReferences()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/AirflowPrototype/Generated/Batch1_Playground.unity", OpenSceneMode.Single);
                var components = new Dictionary<string, Component>();
                foreach (var name in new[] { "PlayerMotor", "PlayerInputReader", "AirPower", "AirDash", "AirCaster", "AirTargeter", "AirSlingTargeter", "AirSlingController", "AirFlowHitSystem", "PlayerWaterSwim", "AirflowPortableAnimationBridge" })
                {
                    var type = Type.GetType("AirflowPrototype." + name + ", Assembly-CSharp", true);
                    var found = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Assert.That(found.Length, Is.EqualTo(1), name + " must have one owner in the gameplay scene.");
                    components.Add(name, (Component)found[0]);
                }
                var motor = components["PlayerMotor"];
                var controller = motor.GetComponent<CharacterController>();
                Assert.That(controller, Is.Not.Null);
                CheckReference(motor, "controller", controller);
                CheckReference(motor, "input", components["PlayerInputReader"]);
                foreach (var name in new[] { "AirSlingController", "PlayerWaterSwim" })
                {
                    CheckReference(components[name], "controller", controller);
                    CheckReference(components[name], "motor", motor);
                }
                CheckReference(components["AirDash"], "motor", motor);
                CheckReference(components["AirDash"], "input", components["PlayerInputReader"]);
                CheckReference(components["AirDash"], "airPower", components["AirPower"]);
                CheckReference(components["AirSlingController"], "airPower", components["AirPower"]);
                CheckReference(components["AirSlingController"], "slingTargeter", components["AirSlingTargeter"]);
                CheckReference(components["AirCaster"], "input", components["PlayerInputReader"]);
                CheckReference(components["AirCaster"], "targeter", components["AirTargeter"]);
                CheckReference(components["AirFlowHitSystem"], "motor", motor);
                CheckReference(components["AirFlowHitSystem"], "caster", components["AirCaster"]);
                CheckReference(components["AirFlowHitSystem"], "airPower", components["AirPower"]);
                CheckReference(components["AirflowPortableAnimationBridge"], "motor", motor);
                CheckReference(components["AirflowPortableAnimationBridge"], "sling", components["AirSlingController"]);
                var view = (Transform)motor.GetType().GetProperty("ViewTransform").GetValue(motor);
                Assert.That(view, Is.Not.Null);
                var camera = view.GetComponent<Camera>();
                Assert.That(camera, Is.Not.Null);
                foreach (var name in new[] { "AirCaster", "AirTargeter", "AirSlingTargeter", "AirSlingController" })
                    CheckReference(components[name], "targetCamera", camera);
                var rewardType = Type.GetType("AirflowPrototype.AirSlingDeathReward, Assembly-CSharp", true);
                var rewards = UnityEngine.Object.FindObjectsByType(rewardType, FindObjectsInactive.Include, FindObjectsSortMode.None);
                Assert.That(rewards.Length, Is.GreaterThan(0));
                foreach (Component reward in rewards)
                {
                    CheckReference(reward, "airPower", components["AirPower"]);
                    CheckReference(reward, "playerTransform", motor.transform);
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void CheckReference(Component owner, string field, UnityEngine.Object expected)
        {
            using (var serialized = new SerializedObject(owner))
            {
                var property = serialized.FindProperty(field);
                Assert.That(property, Is.Not.Null, owner.GetType().Name + "." + field);
                Assert.That(property.objectReferenceValue, Is.EqualTo(expected), owner.GetType().Name + "." + field);
            }
        }

        [Test]
        public void GameplaySceneAndOwnedPrefabsHaveNoBrokenReferences()
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
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
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
