using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Windrunner.Production.Tests
{
    public sealed class ZoneWorldBuildingTests
    {
        [Test] public void ProductionNodeRoutesAndWorldBuildingHierarchyRemainValid()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene("Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity",OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);Physics.SyncTransforms();
                Type.GetType("Windrunner.Frontend.Editor.Zone01WorldBuilding, Assembly-CSharp-Editor",true).GetMethod("Validate").Invoke(null,null);
                var root=scene.GetRootGameObjects().Single().transform;
                foreach(var name in new[]{"Rocks","Cliffs","Trees","Bushes","Grass","GroundDetails","Water","Decorations"})
                {var t=root.Find("Environment/"+name);Assert.IsNotNull(t);Assert.AreEqual(Vector3.zero,t.localPosition);Assert.AreEqual(Vector3.one,t.localScale);}
                var air=root.Find("Traversal/AirNodes");
                CollectionAssert.AreEqual(new[]{2,3,3},air.Cast<Transform>().Select(t=>t.childCount).ToArray());
                foreach(Transform route in air)foreach(Transform node in route)
                    Assert.AreEqual("Assets/WindrunnerProduction/World/Prefabs/ZoneAirNode.prefab",PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject));
                Assert.AreEqual(3,root.Find("Enemies").childCount);
                Assert.IsNotNull(root.Find("Characters/Waykeeper"));Assert.IsNotNull(root.Find("Collectibles/Ridge cache"));
            }
            finally {SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
