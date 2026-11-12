using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;

namespace Windrunner.Production.Tests
{
    public sealed class GameplayLifecycleTests
    {
        [UnityTest]
        public IEnumerator GameplaySceneStartsAndPlayerRemainsFinite()
        {
#if UNITY_EDITOR
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/INab Studio/2D Assets/Procedural Progress Bars/Demo Files/Demo Materials/Canvas/Canvas 4.mat");
            Assert.That(authored, Is.Not.Null);
            float originalFill = authored.GetFloat("_Fill_Amount");
            float originalMain = authored.GetFloat("_Main_Bar_Fill_Amount");
#endif
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity", LoadSceneMode.Single);
            var motorType = Type.GetType("AirflowPrototype.PlayerMotor, Assembly-CSharp", true);
            var motors = UnityEngine.Object.FindObjectsByType(motorType, FindObjectsSortMode.None);
            Assert.That(motors.Length, Is.EqualTo(1), "Gameplay scene must have one player motor.");
            var player = ((Component)motors[0]).transform;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                var position = player.position;
                Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                    float.IsNaN(position.z) || float.IsInfinity(position.z), Is.False);
            }
            Assert.That(WindrunnerInputHost.Active, Is.Not.Null);
            Assert.That(WindrunnerInputHost.Active.Input, Is.Not.Null);
#if UNITY_EDITOR
            Assert.That(authored.GetFloat("_Fill_Amount"), Is.EqualTo(originalFill), "Runtime UI must not mutate the authored fill material.");
            Assert.That(authored.GetFloat("_Main_Bar_Fill_Amount"), Is.EqualTo(originalMain));
            var bridgeType = Type.GetType("AirflowPrototype.AirPowerProgressBarUI, Assembly-CSharp", true);
            var bridge = UnityEngine.Object.FindAnyObjectByType(bridgeType);
            Assert.That(bridge, Is.Not.Null);
            var bar = (MonoBehaviour)bridgeType.GetField("proceduralProgressBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bridge);
            var runtimeMaterial = (Material)bar.GetType().GetField("progressBarMaterial").GetValue(bar);
            Assert.That(UnityEditor.EditorUtility.IsPersistent(runtimeMaterial), Is.False);
            Assert.That(runtimeMaterial, Is.Not.SameAs(authored));
            var empty = SceneManager.CreateScene("Migration teardown");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
            yield return null;
            Assert.That(runtimeMaterial == null, Is.True, "Runtime material must be destroyed on scene unload.");
            Assert.That(authored.GetFloat("_Fill_Amount"), Is.EqualTo(originalFill));
#endif
        }
    }
}
