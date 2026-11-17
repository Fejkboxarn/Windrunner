#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        static Color32[] PortraitPixels(RenderTexture target,string pair)
        {
            var previous=RenderTexture.active;
            var copy=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target;copy.ReadPixels(new Rect(0,0,target.width,target.height),0,0);copy.Apply();
                File.WriteAllBytes(Path.GetFullPath("Logs/ProductionMigration/dagger-portrait-"+pair+".png"),copy.EncodeToPNG());
                return copy.GetPixels32();
            }
            finally{RenderTexture.active=previous;Object.Destroy(copy);}
        }
        [UnityTest] public IEnumerator DaggerArsenalThroughProductionMenuAndSlot()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            string folder=Path.Combine(Path.GetTempPath(),"Windrunner-daggers-"+Guid.NewGuid().ToString("N"));
            var original=InputSystem.settings;var flags=original.hideFlags;
            if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
            var settings=Object.Instantiate(original);settings.hideFlags=HideFlags.DontSave;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=settings;
            var keys=InputSystem.AddDevice<Keyboard>();var pad=InputSystem.AddDevice<Gamepad>();
            void Keys(params Key[] pressed)=>InputSystem.QueueStateEvent(keys,new KeyboardState(pressed));
            var playerType=Type.GetType("Windrunner.Daggers.DaggerPlayer, Assembly-CSharp",true);
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                var title=Session();Call(title,"UseTestDirectory",folder);Submit("New game");Submit("Start a new playthrough");
                yield return Changed(World,title);
                var session=Session();var slots=Prop<PlaythroughSlots>(session,"Slots");string id=Prop<string>(session,"SlotId");
                var player=(Component)Object.FindAnyObjectByType(playerType);var power=(Component)playerType.GetField("power").GetValue(player);
                var progression=Prop<ProgressionService>(session,"Progression");var host=WindrunnerInputHost.Active;
                host.Input.Actions.RemoveAllBindingOverrides();
                Assert.AreEqual("training",progression.EquippedDagger);
                var other=slots.Create("Independent test slot",world:PlaythroughSlots.Zone01);Assert.AreNotEqual("farseer",slots.Open(other.slotId).Load().daggers?.equipped);
                var oldLeft=Prop<GameObject>(player,"LeftModel");
                int listeners=Object.FindObjectsByType<AudioListener>().Length;
                var start=player.transform.position;
                Keys(Key.I);yield return null;yield return null;Keys();yield return null;
                Assert.IsTrue(host.IsPaused);Assert.IsTrue(host.Input.IsGameplayBlocked);
                var surface=Object.FindObjectsByType<RawImage>().Single(r=>r.name=="Dagger portrait");
                var target=(RenderTexture)surface.texture;
                bool rendered=SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null;
                Color32[] training=null;
                if(rendered)
                {
                    Assert.IsTrue(target.IsCreated());training=PortraitPixels(target,"training");
                    Assert.Greater(training.Count(pixel=>!pixel.Equals(training[0])),training.Length/30,"Expected a visible full-body subject.");
                }
                Submit("Farseer's Daggers");yield return null;
                if(rendered)
                {
                    var wind=PortraitPixels(target,"farseer");
                    Assert.Greater(wind.Where((pixel,i)=>!pixel.Equals(training[i])).Count(),30,"Selecting a different pair must change the rendered portrait.");
                    string capture=Path.GetFullPath("Logs/ProductionMigration/dagger-arsenal-"+DateTime.UtcNow.Ticks+".png");
                    var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    view.Show();view.Repaint();yield return null;
                    ScreenCapture.CaptureScreenshot(capture);
                    for(int i=0;i<60 && !File.Exists(capture);i++){view.Repaint();yield return null;}
                    Assert.IsTrue(File.Exists(capture));
                }
                Assert.AreEqual("training",progression.EquippedDagger,"Preview selection cannot equip.");
                Submit("Rotate character");Submit("Equip");yield return null;
                Assert.AreEqual("farseer",progression.EquippedDagger);Assert.IsTrue(oldLeft==null);
                Assert.IsNotNull(Prop<GameObject>(player,"RightModel"));Assert.AreEqual(listeners,Object.FindObjectsByType<AudioListener>().Length);
                var animator=(Animator)playerType.GetField("rig").GetValue(player);
                Assert.IsTrue(Prop<GameObject>(player,"LeftModel").transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
                Assert.IsTrue(Prop<GameObject>(player,"RightModel").transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)));
                Assert.AreEqual(start.x,player.transform.position.x,.02f);
                Submit("Back");yield return null;yield return null;
                Assert.IsFalse(host.IsPaused);Assert.IsTrue(surface==null);
                Keys(Key.R);yield return null;yield return null;
                Assert.AreEqual(0,progression.DaggerCooldownRemaining("second-wind"),"Full AirPower rejects the press.");
                Keys();yield return null;
                Call(power,"Spend",Prop<float>(power,"Max"));
                Assert.IsTrue(Prop<bool>(power,"IsDepletedLocked"));
                using(var block=host.Input.BlockGameplay())
                {
                    Keys(Key.R);yield return null;yield return null;
                    Assert.Zero(progression.DaggerCooldownRemaining("second-wind"));
                    Keys();yield return null;
                }
                yield return null;
                int bursts()=>Object.FindObjectsByType<ParticleSystem>().Count(ps=>ps.name.StartsWith("Second Wind burst"));
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadLeft));yield return null;yield return null;
                Assert.AreEqual(Prop<float>(power,"Max"),Prop<float>(power,"Current"),.01f);Assert.IsFalse(Prop<bool>(power,"IsDepletedLocked"));
                Assert.Greater(progression.DaggerCooldownRemaining("second-wind"),28);Assert.AreEqual(1,bursts());
                Call(power,"Spend",15f);Keys(Key.R);yield return null;yield return null;
                Assert.Less(Prop<float>(power,"Current"),Prop<float>(power,"Max")-10);Assert.AreEqual(1,bursts());
                InputSystem.RemoveDevice(pad);yield return null;Keys();yield return null;
                pad=InputSystem.AddDevice<Gamepad>();yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadLeft));yield return null;
                Assert.Less(Prop<float>(power,"Current"),Prop<float>(power,"Max"));
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                host.SetPaused(true);yield return null;Submit("Dagger Arsenal");yield return null;
                var frozen=progression.DaggerCooldownRemaining("second-wind");
                Submit("Training Daggers");Submit("Equip");yield return null;
                Submit("Farseer's Daggers");Submit("Equip");yield return null;
                Assert.AreEqual(frozen,progression.DaggerCooldownRemaining("second-wind"));
                Submit("Back");Submit("Resume");yield return null;
                using(var slow=UnityTimeAuthority.Slowdown(.2f))
                {
                    float before=progression.DaggerCooldownRemaining("second-wind"),scaled=Time.time;
                    for(int i=0;i<7;i++)yield return null;
                    Assert.AreEqual(Time.time-scaled,before-progression.DaggerCooldownRemaining("second-wind"),.035f);
                }
                Call(session,"Save");var saved=slots.Open(id).Load().daggers;Assert.AreEqual("farseer",saved.equipped);
                Assert.Greater(saved.Remaining("second-wind"),25);Assert.AreEqual(2,saved.owned.Distinct().Count());
                Call(session,"ReturnToTitle");yield return Changed(Title,session);title=Session();Submit("Continue");yield return Changed(World,title);
                var restored=Prop<ProgressionService>(Session(),"Progression");
                Assert.AreEqual("farseer",restored.EquippedDagger);Assert.AreEqual(saved.Remaining("second-wind"),restored.DaggerCooldownRemaining("second-wind"),.7f);
            }
            finally
            {
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                if(keys.added)InputSystem.RemoveDevice(keys);if(pad.added)InputSystem.RemoveDevice(pad);
                InputSystem.settings=original;original.hideFlags=flags;Object.Destroy(settings);
                if(Directory.Exists(folder))Directory.Delete(folder,true);
            }
            yield return null;var prev=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Arsenal teardown"));
            yield return SceneManager.UnloadSceneAsync(prev);
        }
    }
}
#endif
