#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        const string Title="Assets/WindrunnerFrontend/Scenes/MainMenu.unity", World="Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity", Legacy="Assets/WindrunnerFrontend/Scenes/StartingWorld.unity";
        static Type SessionType=>Type.GetType("Windrunner.Frontend.PlayerSession, Assembly-CSharp",true);
        static Component Session()=> (Component)Object.FindAnyObjectByType(SessionType);
        static object Call(Component c,string method,params object[] args)=>c.GetType().GetMethod(method).Invoke(c,args);
        static T Prop<T>(Component c,string name)=>(T)c.GetType().GetProperty(name).GetValue(c);
        static Button Choice(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.gameObject.activeInHierarchy && b.name==name);
        static void Submit(string name)
        {
            var button=Choice(name);Assert.IsTrue(button.interactable,name);
            ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
        }
        static IEnumerator Changed(string path,Component before)
        {
            for(int i=0;i<600 && (before!=null || SceneManager.GetActiveScene().path!=path);i++)yield return null;
            Assert.IsTrue(before==null);Assert.AreEqual(path,SceneManager.GetActiveScene().path);
            yield return null;yield return null;
            Assert.AreEqual(1,Object.FindObjectsByType<ProjectRoot>().Length);
            Assert.AreEqual(1,Object.FindObjectsByType<WindrunnerInputHost>().Length);
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>().Length);
        }

        [UnityTest, Category("Profiling")] public IEnumerator RenderedTitleAndScrollableSlots()
        {
            if(ProjectRoot.Active!=null){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            string directory=Path.Combine(Path.GetTempPath(),"Windrunner-menu-ui-"+Guid.NewGuid().ToString("N"));
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            var selectedSize = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int previousSize = (int)selectedSize.GetValue(gameView);
            var sizesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { GameViewSizeGroupType.Standalone });
            var groupType = group.GetType(); int originalCount = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var title=Session();Call(title,"UseTestDirectory",directory);var slots=Prop<PlaythroughSlots>(title,"Slots");
                for(int i=0;i<16;i++)slots.Create("An exceptionally long playthrough name to exercise text wrapping and controller focus "+i);
                Submit("Settings");Submit("Back");
                foreach (int width in new[] { 1280, 1600, 1920 })
                {
                    int height = width * 9 / 16;
                    var sizeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var enumType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var size = Activator.CreateInstance(sizeType, Enum.Parse(enumType, "FixedResolution"), width, height, "Main menu validation");
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedSize.SetValue(gameView, (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1);
                    gameView.Repaint(); for (int n = 0; n < 10; n++) yield return null;
                    Assert.AreEqual(width, Screen.width); Assert.AreEqual(height, Screen.height);
                    Assert.IsTrue(Choice("Continue").interactable);
                    Assert.AreEqual(Choice("Continue").gameObject,EventSystem.current.currentSelectedGameObject);
                    string file=Path.GetFullPath("Logs/ProductionMigration/main-menu-"+width+"-"+DateTime.UtcNow.Ticks+".png");
                    ScreenCapture.CaptureScreenshot(file);
                    for(int frame=0;frame<30&&!File.Exists(file);frame++)yield return null;
                    Assert.IsTrue(File.Exists(file));Debug.Log("Frontend capture "+file);
                    Submit("Load game");yield return null;
                    var scroll=Object.FindAnyObjectByType<ScrollRect>();
                    Assert.Greater(scroll.content.rect.height,scroll.viewport.rect.height);
                    var buttons=Object.FindObjectsByType<Button>().Where(x=>x.name.StartsWith("An exceptionally")).ToList();
                    Assert.AreEqual(16,buttons.Count);
                    var last=buttons.OrderBy(x=>x.transform.GetSiblingIndex()).Last();
                    EventSystem.current.SetSelectedGameObject(last.gameObject);yield return null;yield return null;
                    var corners=new Vector3[4];last.GetComponent<RectTransform>().GetWorldCorners(corners);
                    Assert.GreaterOrEqual(corners[0].y,-1);Assert.LessOrEqual(corners[2].y,height+1);
                    Submit("Back");yield return null;
                }
            }
            finally
            {
                selectedSize.SetValue(gameView, previousSize);
                for (int index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1; index >= originalCount; index--)
                    groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                if(ProjectRoot.Active!=null)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
            yield return null;
            var empty=SceneManager.CreateScene("Title screenshot teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(Title));
        }

        [UnityTest] public IEnumerator WorldMixerPreferencesPreserveTheAuthoredBalance()
        {
            var mix=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/WindrunnerFrontend/WorldAudio.mixer");
            var type=Type.GetType("Windrunner.Frontend.DesktopPreferences, Assembly-CSharp",true);
            string prefix="Windrunner.Tests.Mix."+Guid.NewGuid().ToString("N");
            var prefs=Activator.CreateInstance(type,prefix);
            try
            {
                type.GetMethod("RestoreMix").Invoke(prefs,new[]{(object)mix});yield return null;
                Assert.IsTrue(mix.GetFloat("PlayerMusic",out float music));Assert.AreEqual(-15,music,.02);
                Assert.IsTrue(mix.GetFloat("PlayerWater",out float water));
                type.GetMethod("SetMix").Invoke(prefs,new object[]{mix,true,.5f});
                type.GetMethod("SetMix").Invoke(prefs,new object[]{mix,false,.25f});yield return null;
                var reopened=Activator.CreateInstance(type,prefix);
                type.GetMethod("RestoreMix").Invoke(reopened,new[]{(object)mix});yield return null;
                Assert.IsTrue(mix.GetFloat("PlayerMusic",out float quieter));Assert.AreEqual(music-6.0206,quieter,.05);
                Assert.IsTrue(mix.GetFloat("PlayerWater",out float reduced));Assert.AreEqual(water-12.0412,reduced,.05);
            }
            finally
            {
                PlayerPrefs.DeleteKey(prefix+"music");PlayerPrefs.DeleteKey(prefix+"ambience");PlayerPrefs.Save();
                var userPrefs=Activator.CreateInstance(type,"Windrunner.Desktop.v1.");
                type.GetMethod("RestoreMix").Invoke(userPrefs,new[]{(object)mix});
            }
        }

        [UnityTest] public IEnumerator MouseGamepadCancelAndKeyboardRebindUseProductionActions()
        {
            if(ProjectRoot.Active!=null){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            var original=InputSystem.settings;var originalFlags=original.hideFlags;
            if(originalFlags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
            var settings=Object.Instantiate(original);settings.hideFlags=HideFlags.DontSave;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=settings;
            var mouse=InputSystem.AddDevice<Mouse>();var pad=InputSystem.AddDevice<Gamepad>();var keys=InputSystem.AddDevice<Keyboard>();
            string prefix="Windrunner.TestBindings."+Guid.NewGuid().ToString("N");
            var temp=Path.Combine(Path.GetTempPath(),"Menu-device-"+Guid.NewGuid().ToString("N"));
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                Call(Session(),"UseTestDirectory",temp);Submit("Settings");Submit("Back");
                var input=WindrunnerInputHost.Active.Input;
                input.Actions.RemoveAllBindingOverrides();
                var button=Choice("Settings").GetComponent<RectTransform>();
                var point=RectTransformUtility.WorldToScreenPoint(null,button.TransformPoint(button.rect.center));
                var m=new MouseState{position=point};InputSystem.QueueStateEvent(mouse,m);yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,m.WithButton(MouseButton.Left));yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,m.WithButton(MouseButton.Left,false));yield return null;yield return null;
                Assert.IsNotNull(Choice("Sound"));
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                Assert.IsNotNull(Object.FindObjectsByType<Slider>().FirstOrDefault(s=>s.name=="Music"));
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;
                Assert.IsNotNull(Choice("Sound"));InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                Submit("Display");int vsync=QualitySettings.vSyncCount;
                Submit(vsync==0?"VSync    Off":"VSync    On");Submit("Apply changes");Submit("Cancel");Assert.AreEqual(vsync,QualitySettings.vSyncCount);
                Submit("Back");Submit("Back");
                using(var isolated=new WindrunnerInputService(WindrunnerInputHost.Active.AuthoringActions,prefix))
                {
                    isolated.SetMenuOpen(true);bool? result=null;
                    Assert.IsTrue(isolated.BeginRebind("Gameplay","Jump",0,b=>result=b,path=>false));
                    InputSystem.QueueStateEvent(keys,new KeyboardState(Key.J));yield return new WaitForSecondsRealtime(.2f);
                    Assert.AreEqual(false,result);
                    InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;result=null;
                    Assert.IsTrue(isolated.BeginRebind("Gameplay","Jump",0,b=>result=b));
                    InputSystem.QueueStateEvent(keys,new KeyboardState(Key.K));yield return new WaitForSecondsRealtime(.2f);
                    Assert.AreEqual(true,result);Assert.IsFalse(isolated.GetAction("Gameplay","Jump").enabled);
                    isolated.SetInvertLookY(true);isolated.SetStickSensitivity(1.6f);
                }
                using(var reload=new WindrunnerInputService(WindrunnerInputHost.Active.AuthoringActions,prefix))
                {
                    Assert.AreEqual("<Keyboard>/k",reload.GetAction("Gameplay","Jump").bindings[0].effectivePath);
                    Assert.IsTrue(reload.InvertLookY);Assert.AreEqual(1.6f,reload.StickSensitivity);
                    reload.ResetBindings();Assert.IsNull(reload.GetAction("Gameplay","Jump").bindings[0].overridePath);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(pad);
                InputSystem.settings=original;original.hideFlags=originalFlags;Object.Destroy(settings);
                foreach(string suffix in new[]{"BindingOverrides.v1","StickSensitivity.v1","InvertLookY.v1"})PlayerPrefs.DeleteKey(prefix+suffix);
                PlayerPrefs.Save();if(ProjectRoot.Active!=null)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(temp))Directory.Delete(temp,true);
            }
            yield return null;
            var empty=SceneManager.CreateScene("Menu device teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(Title));
        }

        [UnityTest] public IEnumerator RealMenuNewSaveContinueAndRepeatedWorldTransitions()
        {
            if(ProjectRoot.Active!=null){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            string directory=Path.Combine(Path.GetTempPath(),"Windrunner-menu-"+Guid.NewGuid().ToString("N"));
            var original=InputSystem.settings;var originalFlags=original.hideFlags;
            if(originalFlags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
            var settings=Object.Instantiate(original);settings.hideFlags=HideFlags.DontSave;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=settings;
            var stick=InputSystem.AddDevice<Gamepad>();
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var title=Session();Call(title,"UseTestDirectory",directory);
                Submit("Settings");Submit("Sound");Submit("Back");Submit("Back");
                Assert.IsFalse(Choice("Continue").interactable);Assert.IsTrue(WindrunnerInputHost.Active.IsPaused);
                Submit("Gallery");Assert.IsTrue(Object.FindObjectsByType<Text>().Any(t=>t.text.Contains("Locked")));Submit("Back");
                Submit("New game");Submit("Start a new playthrough");
                yield return Changed(World,title);
                var session=Session();var id=Prop<string>(session,"SlotId");
                var charController=(CharacterController)SessionType.GetField("character").GetValue(session);
                var anchor=(Transform)SessionType.GetField("arrival").GetValue(session);
                Assert.Less(Vector3.Distance(charController.transform.position,anchor.position),2);
                var ground=Physics.RaycastAll(anchor.position+Vector3.up,Vector3.down,100,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.collider.transform.IsChildOf(charController.transform) && h.normal.y>.6f).OrderBy(h=>h.distance).FirstOrDefault();
                Assert.IsNotNull(ground.collider,"Arrival needs a walkable supporting surface.");
                Debug.Log("Arrival ground: "+ground.collider.name+" at "+ground.point+"; drop from anchor "+(ground.distance-1));
                Assert.Less(ground.distance,3,"Arrival must be within stepping distance of solid ground.");
                var service=Prop<ProgressionService>(session,"Progression");
                Assert.AreSame(service,ProjectRoot.Active.Runtime.Services.Get<ProgressionService>());
                Assert.AreSame(service,Object.FindAnyObjectByType<ProgressionActor>().Service);
                service.Observe(new GameplayFact{occurrenceId="test-item",playerId=service.PlayerId,verified=true,persistent=true,kind=FactKind.ItemCollected,definitionId="test",targetId="item"});
                charController.enabled=false;charController.transform.position+=Vector3.up*15;charController.enabled=true;
                WindrunnerInputHost.Active.Input.Actions.RemoveAllBindingOverrides();
                InputSystem.QueueStateEvent(stick,new GamepadState{leftStick=Vector2.up});yield return null;yield return null;
                Assert.Greater(WindrunnerInputHost.Active.Input.Axis("Move").sqrMagnitude,.5f);
                var position=charController.transform.position;
                var dash=charController.GetComponent(Type.GetType("AirflowPrototype.AirDash, Assembly-CSharp",true));
                Call(dash,"TriggerAutoDash");Assert.IsTrue(Prop<bool>(dash,"IsAutoDashLatched"));
                string oldRevision=Prop<PlaythroughSlots>(session,"Slots").Inspect(id).Record.revision;
                SessionType.GetField("nextSave",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,-1f);
                yield return null;Assert.Greater(charController.transform.position.y,position.y-2,"Autosave moved the airborne player.");
                var slots=Prop<PlaythroughSlots>(session,"Slots");
                Assert.AreNotEqual(oldRevision,slots.Inspect(id).Record.revision);
                Assert.IsTrue(Prop<bool>(dash,"IsAutoDashLatched"),"A background save must leave Dash ownership alone.");
                InputSystem.QueueStateEvent(stick,new GamepadState());
                Assert.AreEqual(1,slots.Open(id).Load().persistentFacts.Count);
                WindrunnerInputHost.Active.SetPaused(true);yield return null;
                Submit("Settings");Submit("Controls");
                Assert.IsTrue(WindrunnerInputHost.Active.IsPaused);Assert.IsTrue(WindrunnerInputHost.Active.Input.IsGameplayBlocked);
                Submit("Back");Submit("Back");Submit("Save game");Submit("Save current playthrough");Submit("Confirm");
                Assert.IsTrue(WindrunnerInputHost.Active.IsPaused);
                string invalid=slots.Create("Corrupt").slotId;
                File.WriteAllText(Path.Combine(directory,invalid,"session.json"),"broken");
                File.WriteAllText(Path.Combine(directory,invalid,"session.json.bak"),"broken");
                Assert.Throws<TargetInvocationException>(()=>Call(session,"Load",invalid));Assert.IsFalse(Prop<bool>(session,"Busy"));
                Submit("Return to main menu");Submit("Confirm");yield return Changed(Title,session);
                title=Session();Assert.IsTrue(Choice("Continue").interactable);Submit("Continue");yield return Changed(World,title);
                session=Session();Assert.AreEqual(id,Prop<string>(session,"SlotId"));
                Assert.AreEqual(1,Prop<ProgressionService>(session,"Progression").Snapshot().persistentFacts.Count);
                charController=(CharacterController)SessionType.GetField("character").GetValue(session);
                anchor=(Transform)SessionType.GetField("arrival").GetValue(session);
                Assert.Less(Vector3.Distance(charController.transform.position,anchor.position),2);
                Assert.IsFalse(WindrunnerInputHost.Active.IsPaused);Assert.IsFalse(WindrunnerInputHost.Active.Input.IsGameplayBlocked);
                Assert.Greater(Time.timeScale,0);Assert.IsTrue(charController.enabled);
                foreach(var transient in new[]{("AirDash","IsAutoDashLatched"),("AirSlingController","IsSlinging"),("AirCaster","IsCharging")})
                    Assert.IsFalse(Prop<bool>(charController.GetComponent(Type.GetType("AirflowPrototype."+transient.Item1+", Assembly-CSharp",true)),transient.Item2));
                Call(session,"Load",id);yield return Changed(World,session);
                Assert.AreEqual(1,Prop<ProgressionService>(Session(),"Progression").Snapshot().persistentFacts.Count);
                session=Session();
                string legacy=slots.Create("Pre-zone slot").slotId;
                Call(session,"Load",legacy);yield return Changed(Legacy,session);
                var routes=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)
                    .GroupBy(source=>source.outputAudioMixerGroup==null?"Direct":source.outputAudioMixerGroup.audioMixer.name+"/"+source.outputAudioMixerGroup.name);
                Debug.Log("Starting world audio routes: "+string.Join(", ",routes.Select(group=>group.Key+"="+group.Count())));
                var sourceGroups=routes.ToDictionary(group=>group.Key,group=>group.Count());
                Assert.AreEqual(1,sourceGroups["WorldAudio/Music"]);
                Assert.AreEqual(1,sourceGroups["WorldAudio/Waterfall"]);
                Assert.AreEqual(25,sourceGroups["Direct"]);
                Assert.IsFalse(sourceGroups.Keys.Any(k=>k.StartsWith("SceneMixer/")));
            }
            finally
            {
                if(ProjectRoot.Active!=null)Object.Destroy(ProjectRoot.Active.gameObject);
                InputSystem.RemoveDevice(stick);InputSystem.settings=original;original.hideFlags=originalFlags;Object.Destroy(settings);
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
            yield return null;
            var empty=SceneManager.CreateScene("Menu flow teardown");var previous=SceneManager.GetActiveScene();SceneManager.SetActiveScene(empty);
            if(previous.IsValid() && previous!=empty)yield return SceneManager.UnloadSceneAsync(previous);
        }
    }
}
#endif
