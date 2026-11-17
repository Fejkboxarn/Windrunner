using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.Production.Progression;

namespace Windrunner.Production.Tests
{
    public sealed class FrontendContractTests
    {
        [Test] public void OwnedWorldMixerPreservesVendorSnapshotAndSignalChain()
        {
            const string vendor="Assets/Raygeas/Grasslands - Stylized Nature/Assets/Audio/SceneMixer.mixer";
            string source=File.ReadAllText(vendor).Replace("\r\n","\n");
            string copy=File.ReadAllText("Assets/WindrunnerFrontend/WorldAudio.mixer").Replace("\r\n","\n");
            Assert.That(copy,Does.Contain("094003c1be979604bb8eeb597eeb05ad: -15"));
            var normalized=Regex.Replace(copy,@"  m_ExposedParameters:\n(?:  - guid: [0-9a-f]+\n    name: Player\w+\n){4}","  m_ExposedParameters: []\n");
            Assert.AreEqual(source,normalized.Replace("  m_Name: WorldAudio","  m_Name: SceneMixer"));
        }
        [TestCase("Assets/WindrunnerFrontend/Scenes/MainMenu.unity")]
        [TestCase("Assets/WindrunnerFrontend/Scenes/StartingWorld.unity")]
        public void FrontendSceneSerializedReferences(string path)
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                foreach(var node in all)
                {
                    Assert.Zero(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject),node.name);
                    foreach(var c in node.GetComponents<Component>())
                    {
                        using var serialized=new SerializedObject(c);var field=serialized.GetIterator();
                        while(field.Next(true))
                            if(field.propertyType==SerializedPropertyType.ObjectReference && field.objectReferenceEntityIdValue!=default)
                                Assert.IsNotNull(field.objectReferenceValue,node.name+"/"+field.propertyPath);
                    }
                }
                var scripts=all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).ToList();
                Assert.AreEqual(1,scripts.Count(s=>s.GetType().Name=="PlayerSession"));
                Assert.AreEqual(1,scripts.Count(s=>s.GetType().Name=="WindrunnerInputHost"));
                Assert.AreEqual(path.EndsWith("StartingWorld.unity")?1:0,scripts.Count(s=>s.GetType().Name=="PlayerMotor"));
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void GalleryReplayCannotResolveOrRewardProgress()
        {
            var slot=new ProgressionState{playerId="local-player"};
            slot.cutscenes.Add(new CutsceneRecord{id="scene",status=CinematicStatus.Pending});
            int played=0;
            Assert.IsFalse(GalleryAccess.Replay(slot,"scene",()=>played++));
            slot.cutscenes[0].status=CinematicStatus.Watched;
            string before=ProgressionCodec.Encode(slot);
            Assert.IsTrue(GalleryAccess.Replay(slot,"scene",()=>played++));
            Assert.AreEqual(before,ProgressionCodec.Encode(slot));Assert.AreEqual(1,played);
            Assert.IsFalse(GalleryAccess.Unlocked(new ProgressionState(),"scene"));
        }
        [Test] public void DesktopPreferencesRoundTripIndependentOfSlots()
        {
            string prefix="Windrunner.Tests."+Guid.NewGuid().ToString("N");
            var type=Type.GetType("Windrunner.Frontend.DesktopPreferences, Assembly-CSharp",true);
            var settings=Activator.CreateInstance(type,prefix);float initial=AudioListener.volume;
            try
            {
                type.GetMethod("SetVolume").Invoke(settings,new object[]{.35f});
                var optionsType=Type.GetType("Windrunner.Frontend.DesktopOptions, Assembly-CSharp",true);
                var options=optionsType.GetMethod("Current").Invoke(null,null);
                optionsType.GetField("width").SetValue(options,1280);optionsType.GetField("height").SetValue(options,720);
                optionsType.GetField("mode").SetValue(options,FullScreenMode.Windowed);optionsType.GetField("limit").SetValue(options,90);
                type.GetMethod("Confirm").Invoke(settings,new[]{options});
                var reloaded=Activator.CreateInstance(type,prefix);
                Assert.AreEqual(.35f,type.GetProperty("Volume").GetValue(reloaded));
                Assert.AreEqual(90,optionsType.GetField("limit").GetValue(type.GetMethod("Confirmed").Invoke(reloaded,null)));
                optionsType.GetField("width").SetValue(options,-100);
                Assert.Throws<TargetInvocationException>(()=>type.GetMethod("Confirm").Invoke(settings,new[]{options}));
            }
            finally{AudioListener.volume=initial;PlayerPrefs.DeleteKey(prefix+"master");PlayerPrefs.DeleteKey(prefix+"display");PlayerPrefs.Save();}
        }
    }
}
