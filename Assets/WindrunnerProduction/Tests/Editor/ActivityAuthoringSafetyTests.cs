#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Progression.Activities;
namespace Windrunner.Production.Tests
{
    public sealed class ActivityAuthoringSafetyTests
    {
        [Test] public void DraftPreviewAndExplicitSavePreserveAssetIdentityAndReleasePreviewObjects()
        {
            string path="Assets/RelayPursuitPreviewTest_"+Guid.NewGuid().ToString("N")+".asset";
            var profile=ScriptableObject.CreateInstance<PursuitProfile>();profile.name="Authored profile";profile.definition.challengeId="test";profile.definition.points.Add(new PursuitWaypoint());profile.definition.points.Add(new PursuitWaypoint{position=Vector3.forward*20});
            AssetDatabase.CreateAsset(profile,path);string originalName=profile.name;string guid=AssetDatabase.AssetPathToGUID(path),before=JsonUtility.ToJson(profile);var flags=profile.hideFlags;
            var type=Type.GetType("Windrunner.Activities.Editor.RelayPursuitStudio, Assembly-CSharp-Editor",true);
            int ghosts=Resources.FindObjectsOfTypeAll<GameObject>().Count(g=>g.name=="Isolated preview target");
            UnityEngine.Object window=null;
            try
            {
                for(int i=0;i<5;i++)
                {
                    window=ScriptableObject.CreateInstance(type);type.GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{profile});
                    var draft=(PursuitProfile)type.GetField("draft",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);draft.definition.speed=8;
                    type.GetMethod("BuildPreview",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{draft.definition});
                    Assert.AreEqual(before,JsonUtility.ToJson(profile),"Draft/preview must never persist edits.");
                    UnityEngine.Object.DestroyImmediate(window);window=null;
                }
                Assert.AreEqual(ghosts,Resources.FindObjectsOfTypeAll<GameObject>().Count(g=>g.name=="Isolated preview target"));
                window=ScriptableObject.CreateInstance(type);type.GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{profile});
                ((PursuitProfile)type.GetField("draft",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).definition.speed=9;
                Assert.IsTrue((bool)type.GetMethod("SaveDraft",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null));
                Assert.AreEqual(9,profile.definition.speed);Assert.AreEqual(flags,profile.hideFlags);Assert.AreEqual(originalName,profile.name);Assert.AreEqual(guid,AssetDatabase.AssetPathToGUID(path));
                Undo.PerformUndo();Assert.AreEqual(6,profile.definition.speed);
            }
            finally{if(window)UnityEngine.Object.DestroyImmediate(window);AssetDatabase.DeleteAsset(path);}
        }
    }
}
#endif
