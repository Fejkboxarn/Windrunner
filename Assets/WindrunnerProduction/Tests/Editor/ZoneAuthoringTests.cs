using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Windrunner.Production.Tests
{
    public sealed class ZoneAuthoringTests
    {
        [Test] public void DeliveryDeadlineIsExclusiveAndRecoveryUsesTheExpiryInstant()
        {
            var attempt=new Windrunner.Production.Progression.TimedCarryAttempt();
            Assert.IsFalse(attempt.Collect(0,0));
            Assert.IsTrue(attempt.Collect(8,4));Assert.IsFalse(attempt.Collect(11,4));
            Assert.AreEqual(.5,attempt.Remaining(11.5));Assert.IsFalse(attempt.Expire(11.999,2));
            Assert.IsTrue(attempt.Expire(12,2));Assert.IsFalse(attempt.Available(13.999));
            Assert.IsTrue(attempt.Collect(14,4));attempt.Delivered();
            Assert.AreEqual(0,attempt.Remaining(15));Assert.IsFalse(attempt.Expire(18,2));
        }
        [Test] public void EditableBasinSupportsClearingAndQuestComposition()
        {
            const string path="Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity";
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Type.GetType("Windrunner.Frontend.Editor.Zone01Setup, Assembly-CSharp-Editor",true).GetMethod("Validate").Invoke(null,null);
                var root=scene.GetRootGameObjects().Single().transform;
                var terrain=root.GetComponentInChildren<Terrain>();var data=terrain.terrainData;
                Assert.IsTrue(AssetDatabase.Contains(data));Assert.AreEqual(2048,data.size.x);Assert.AreEqual(2048,data.size.z);
                Assert.AreEqual(1025,data.heightmapResolution);Assert.IsNotEmpty(data.terrainLayers);
                var spawn=root.Find("PlayerSpawn").position;
                Assert.That(spawn.y-terrain.SampleHeight(spawn),Is.InRange(0f,1f));
                var heights=data.GetHeights(0,0,1025,1025).Cast<float>();
                Assert.Greater((heights.Max()-heights.Min())*data.size.y,150);
            }
            finally {SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
