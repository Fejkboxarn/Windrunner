using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Windrunner.Production.Tests
{
    public sealed class DisplacementAuthorityTests
    {
        [UnityTest]
        public IEnumerator OrderedRequestsMatchDirectControllerMovesIncludingCollisions()
        {
            var reference = new GameObject("Direct movement baseline");
            var migrated = new GameObject("Authority movement");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ground.transform.position = new Vector3(0, -0.5f, 0);
                ground.transform.localScale = new Vector3(100, 1, 100);
                wall.transform.position = new Vector3(0, 2, 4);
                wall.transform.localScale = new Vector3(100, 4, 1);
                reference.transform.position = new Vector3(-10, 1.1f, 0);
                migrated.transform.position = new Vector3(10, 1.1f, 0);
                var old = reference.AddComponent<CharacterController>();
                var current = migrated.AddComponent<CharacterController>();
                var authorityType = Type.GetType("AirflowPrototype.PlayerDisplacementAuthority, Assembly-CSharp", true);
                var phaseType = Type.GetType("AirflowPrototype.PlayerMovementPhase, Assembly-CSharp", true);
                var authority = Activator.CreateInstance(authorityType, new object[] { current });
                MethodInfo apply = authorityType.GetMethod("Apply");
                Physics.SyncTransforms();
                for (int frame = 0; frame < 120; frame++)
                {
                    var steps = new[] {
                        new Vector3(0, -0.05f, 0.08f), // motor integration
                        new Vector3(0, frame % 3 == 0 ? 0.02f : 0, 0.03f), // Sling path
                        new Vector3(0, -0.01f, -0.02f) // water drag/buoyancy correction
                    };
                    var phases = new[] { "Locomotion", "SlingPath", "Swimming" };
                    for (int step = 0; step < steps.Length; step++)
                    {
                        var expected = old.Move(steps[step]);
                        var actual = (CollisionFlags)apply.Invoke(authority, new[] { (object)steps[step], Enum.Parse(phaseType, phases[step]), frame });
                        Assert.That(actual, Is.EqualTo(expected));
                        Assert.That((migrated.transform.position - reference.transform.position - Vector3.right * 20f).sqrMagnitude,
                            Is.LessThan(0.000001f), "Every intermediate physics step must match, not just final position.");
                    }
                    Assert.That(authorityType.GetProperty("StepsThisFrame").GetValue(authority), Is.EqualTo(3));
                }
                Assert.That(current.transform.position.z, Is.LessThan(3.1f), "Wall must constrain movement.");
                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(reference);
                UnityEngine.Object.Destroy(migrated);
                UnityEngine.Object.Destroy(ground);
                UnityEngine.Object.Destroy(wall);
            }
            yield return null;
        }
    }
}
