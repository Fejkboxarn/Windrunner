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
        }
    }
}
