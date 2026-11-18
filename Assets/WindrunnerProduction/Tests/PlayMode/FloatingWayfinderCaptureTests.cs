#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;
namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        [UnityTest, Category("Profiling")] public IEnumerator FloatingWayfinderContrastCaptures()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            string folder=Path.GetFullPath("Logs/ProductionMigration/floating-wayfinder-"+DateTime.UtcNow.Ticks);Directory.CreateDirectory(folder);
            string temp=Path.Combine(Path.GetTempPath(),"Windrunner-floating-"+Guid.NewGuid().ToString("N"));
            const string key="Windrunner.UI.ObjectiveWayfinder";bool had=PlayerPrefs.HasKey(key);int preference=PlayerPrefs.GetInt(key);
            var viewType=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);view.Show();view.Focus();
            var selected=viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);int prior=(int)selected.GetValue(view);
            var sizesType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{GameViewSizeGroupType.Standalone});var gt=group.GetType();int count=(int)gt.GetMethod("GetTotalCount").Invoke(group,null);
            var sizeType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");var enumType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            gt.GetMethod("AddCustomSize").Invoke(group,new[]{Activator.CreateInstance(sizeType,Enum.Parse(enumType,"FixedResolution"),1920,1080,"Floating Wayfinder verification")});selected.SetValue(view,count);
            IEnumerator Capture(string name)
            {
                yield return new WaitForSecondsRealtime(.5f);view.Repaint();yield return null;
                string file=Path.Combine(folder,name+".png");ScreenCapture.CaptureScreenshot(file);
                for(int i=0;i<120&&!File.Exists(file);i++){view.Repaint();yield return null;}Assert.IsTrue(File.Exists(file));
            }
            bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            GameObject fixture=null;
            try
            {
                WindrunnerUITheme.ObjectiveWayfinder=true;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                var session=Session();Call(session,"UseTestDirectory",temp);Submit("New game");Submit("Start a new playthrough");yield return Changed(World,session);
                var journal=Object.FindAnyObjectByType<ProgressionJournal>();var actor=journal.actor;var service=actor.Service;
                Assert.IsTrue(service.Accept("basin-hunt"));Assert.IsTrue(service.Track("basin-hunt"));
                // The production camera owner is explicitly wired on the session.
                var sessionOwner=Session();((Behaviour)sessionOwner.GetType().GetField("orbit").GetValue(sessionOwner)).enabled=false;
                var camera=journal.interaction.view;var anchor=journal.objectiveAnchors[0].anchor;
                camera.transform.position=anchor.position-Vector3.forward*20+Vector3.up*5;camera.transform.LookAt(anchor.position+Vector3.up*2);
                yield return Capture("01-open-terrain");Assert.IsTrue(journal.Wayfinder.Visible);Assert.IsFalse(journal.Wayfinder.Offscreen);
                camera.transform.Rotate(0,140,0,Space.World);yield return Capture("02-offscreen");Assert.IsTrue(journal.Wayfinder.Offscreen);
                fixture=new GameObject("Disposable contrast fixture");var sky=new GameObject("Sky contrast anchor");sky.transform.SetParent(fixture.transform);
                sky.transform.position=camera.transform.position+Vector3.forward*30+Vector3.up*35;journal.objectiveAnchors[0].anchor=sky.transform;camera.transform.LookAt(sky.transform.position+Vector3.up*2);
                yield return Capture("03-bright-sky");Assert.IsTrue(journal.Wayfinder.Visible);Assert.IsFalse(journal.Wayfinder.Offscreen);
                journal.objectiveAnchors[0].anchor=anchor;
                camera.transform.position=anchor.position-Vector3.forward*16+Vector3.up*4;camera.transform.LookAt(anchor.position+Vector3.up*2);
                var tree=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Trees/P_FFE_Spruce_A1.Prefab");Assert.IsNotNull(tree);
                for(int z=0;z<3;z++)for(int x=-2;x<=2;x++)
                {
                    var clone=Object.Instantiate(tree,fixture.transform);clone.transform.position=anchor.position+new Vector3(x*7,0,3+z*7);
                    var renderers=clone.GetComponentsInChildren<Renderer>();var bounds=renderers.Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                    clone.transform.localScale*=18/Mathf.Max(.01f,bounds.size.y);
                    float bottom=renderers.Min(r=>r.bounds.min.y);clone.transform.position+=Vector3.up*(anchor.position.y-1-bottom);
                }
                foreach(var light in Object.FindObjectsByType<Light>())light.intensity*=.08f;
                RenderSettings.ambientIntensity=.12f;RenderSettings.ambientLight=new Color(.05f,.07f,.06f);
                yield return Capture("04-dark-forest-fixture");Assert.IsTrue(journal.Wayfinder.Visible);Assert.IsFalse(journal.Wayfinder.Offscreen);
                Debug.Log("Floating Wayfinder captures: "+folder);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation=asyncShaders;
                if(fixture)Object.Destroy(fixture);if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                selected.SetValue(view,prior);gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{count});
                if(had)PlayerPrefs.SetInt(key,preference);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
                if(Directory.Exists(temp))Directory.Delete(temp,true);
            }
            yield return null;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Floating capture teardown"));yield return SceneManager.UnloadSceneAsync(old);
        }
    }
}
#endif
