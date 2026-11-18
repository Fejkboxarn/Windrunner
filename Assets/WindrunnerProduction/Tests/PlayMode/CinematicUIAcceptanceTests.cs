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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Windrunner.Production;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;
namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        [UnityTest] public IEnumerator CinematicNavigationRemembersPagesAndSliderRespondsWithoutRebuilding()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            string directory=Path.Combine(Path.GetTempPath(),"Windrunner-ui-nav-"+Guid.NewGuid().ToString("N"));
            bool reduced=WindrunnerUITheme.ReducedMotion;float scale=WindrunnerUITheme.Scale;
            Slider volume=null;float priorVolume=0;
            try
            {
                WindrunnerUITheme.ReducedMotion=true;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                Call(Session(),"UseTestDirectory",directory);Submit("Settings");Submit("Back");
                Assert.IsFalse(Choice("Continue").interactable);
                Assert.AreNotEqual(Choice("Continue").gameObject,EventSystem.current.currentSelectedGameObject);
                Choice("Settings").Select();Submit("Settings");Choice("Display").Select();Submit("Display");Submit("Back");
                Assert.AreEqual("Display",EventSystem.current.currentSelectedGameObject.name);
                Submit("Back");Assert.AreEqual("Settings",EventSystem.current.currentSelectedGameObject.name);
                Submit("Settings");Choice("Sound").Select();Submit("Sound");yield return null;
                volume=Object.FindObjectsByType<Slider>().First(x=>x.name=="Master");priorVolume=volume.value;
                volume.Select();var identity=volume.GetEntityId();
                var direction=volume.value>0?MoveDirection.Left:MoveDirection.Right;
                float expected=volume.value+(direction==MoveDirection.Left?-.1f:.1f);
                ExecuteEvents.Execute(volume.gameObject,new AxisEventData(EventSystem.current){moveDir=direction},ExecuteEvents.moveHandler);
                Assert.AreEqual(expected,volume.value,.0001f);Assert.AreEqual(identity,volume.GetEntityId());
                volume.value=priorVolume;volume=null;
                Submit("Back");Assert.AreEqual("Sound",EventSystem.current.currentSelectedGameObject.name);
                Submit("Accessibility");yield return null;
                var group=GameObject.Find("Backdrop").GetComponent<CanvasGroup>();Assert.AreEqual(1,group.alpha);
                Submit("Back");Submit("Back");Submit("Quit game");Assert.AreEqual("Cancel",EventSystem.current.currentSelectedGameObject.name);Choice("Confirm").Select();Submit("Cancel");Submit("Quit game");Assert.AreEqual("Cancel",EventSystem.current.currentSelectedGameObject.name);Submit("Cancel");
            }
            finally
            {
                if(volume)volume.value=priorVolume;
                WindrunnerUITheme.ReducedMotion=reduced;WindrunnerUITheme.Scale=scale;
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
            yield return null;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("UI navigation teardown"));yield return SceneManager.UnloadSceneAsync(old);
        }
        [UnityTest,Category("Profiling")] public IEnumerator CinematicScreensRenderWithNavigationAndPortrait()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            string directory=Path.Combine(Path.GetTempPath(),"Windrunner-ui-"+Guid.NewGuid().ToString("N"));
            var gameViewType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var view=EditorWindow.GetWindow(gameViewType);view.Show();view.Focus();
            var selectedSize=gameViewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            int priorSize=(int)selectedSize.GetValue(view);
            var sizesType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{GameViewSizeGroupType.Standalone});var gt=group.GetType();
            int originalCount=(int)gt.GetMethod("GetTotalCount").Invoke(group,null);
            bool reduced=WindrunnerUITheme.ReducedMotion;float scale=WindrunnerUITheme.Scale;
            IEnumerator Capture(string name)
            {
                yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();view.Repaint();yield return null;
                if(name=="main")
                {
                    var art=GameObject.Find("Wind and slate").GetComponent<Graphic>();var mesh=art.canvasRenderer.GetMesh();
                    Assert.IsNotNull(mesh);Assert.Greater(mesh.vertexCount,100);
                    var quit=Choice("Quit game").GetComponent<RectTransform>();var viewport=(RectTransform)quit.parent.parent;
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,quit);
                    Assert.GreaterOrEqual(bounds.min.y,viewport.rect.yMin-1,"All six title choices must fit at default scale.");
                }
                string file=Path.GetFullPath("Logs/ProductionMigration/cinematic-"+name+"-"+Screen.width+"x"+Screen.height+".png");
                if(File.Exists(file))File.Delete(file);
                ScreenCapture.CaptureScreenshot(file);
                for(int i=0;i<90&&!File.Exists(file);i++){view.Repaint();yield return null;}
                Assert.IsTrue(File.Exists(file));
                foreach(var b in Object.FindObjectsByType<Button>().Where(b=>b.isActiveAndEnabled&&b.interactable))
                    Assert.IsTrue(b.GetComponentInChildren<Text>()?.font!=null,b.name);
            }
            void Resize(int w,int h)
            {
                var sizeType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
                var enumType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                var size=Activator.CreateInstance(sizeType,Enum.Parse(enumType,"FixedResolution"),w,h,"Cinematic UI validation");
                gt.GetMethod("AddCustomSize").Invoke(group,new[]{size});selectedSize.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);
            }
            try
            {
                WindrunnerUITheme.Scale=1;WindrunnerUITheme.ReducedMotion=false;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                Call(Session(),"UseTestDirectory",directory);Submit("Settings");Submit("Back");
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(2560,1080)})
                {
                    Resize(size.x,size.y);yield return new WaitForSecondsRealtime(.1f);yield return Capture("main");
                    Assert.IsNotNull(EventSystem.current.currentSelectedGameObject);
                    Submit("Settings");yield return Capture("settings");Submit("Back");
                }
                Resize(1920,1080);yield return null;
                Submit("Settings");Submit("Sound");yield return Capture("sound");Submit("Back");
                Submit("Display");yield return Capture("display");Submit("Back");
                Submit("Controls");yield return Capture("controls");Submit("Back");
                Submit("Accessibility");yield return Capture("accessibility");Submit("Back");Submit("Back");
                Submit("Quit game");yield return Capture("confirmation");Submit("Cancel");
                Submit("Gallery");yield return Capture("gallery");Submit("Back");
                Submit("New game");yield return Capture("new-game");var prior=Session();Submit("Start a new playthrough");yield return Changed(World,prior);
                WindrunnerInputHost.Active.SetPaused(true);yield return null;yield return Capture("pause");
                Submit("Dagger Arsenal");yield return Capture("arsenal-training");
                var choice=Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name.Contains("Farseer"));Assert.IsNotNull(choice);
                choice.Select();choice.onClick.Invoke();yield return Capture("arsenal-farseer");Submit("Equip");yield return Capture("arsenal-equipped");
                Assert.IsNotNull(Object.FindObjectsByType<RawImage>().First(x=>x.name=="Dagger portrait").texture);
                Submit("Back");Submit("Save game");yield return Capture("save");Submit("Save an independent copy");yield return Capture("save-copy");Submit("Back");
                Submit("Load game");yield return Capture("load");Submit("Back");Submit("Resume");yield return null;
                var journal=Object.FindAnyObjectByType<ProgressionJournal>();Assert.IsTrue(journal.SetOpen(true));yield return Capture("journal");journal.SetOpen(false);
                yield return Capture("hud");
                Resize(1280,720);yield return null;
                Assert.IsTrue(journal.SetOpen(true));yield return Capture("journal");journal.SetOpen(false);
                WindrunnerInputHost.Active.SetPaused(true);yield return null;Submit("Dagger Arsenal");yield return Capture("arsenal-farseer");Submit("Back");
                Submit("Save game");yield return Capture("save");Submit("Back");Submit("Resume");yield return null;
                WindrunnerInputHost.Active.SetPaused(true);yield return null;
                WindrunnerUITheme.ReducedMotion=true;WindrunnerUITheme.Scale=1.2f;Submit("Settings");yield return Capture("reduced-motion-large-text");
            }
            finally
            {
                WindrunnerUITheme.ReducedMotion=reduced;WindrunnerUITheme.Scale=scale;
                selectedSize.SetValue(view,priorSize);
                for(int i=(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1;i>=originalCount;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
            yield return null;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Cinematic UI teardown"));yield return SceneManager.UnloadSceneAsync(old);
        }
    }
}
#endif
