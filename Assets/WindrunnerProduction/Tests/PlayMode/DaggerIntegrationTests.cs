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
                var menu=Object.FindAnyObjectByType(Type.GetType("Windrunner.Frontend.SessionMenu, Assembly-CSharp",true));
                var fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var preview=menu.GetType().GetField("portrait",fields).GetValue(menu);
                var previewModel=preview.GetType().GetField("leftModel",fields);
                var selectedModel=(GameObject)previewModel.GetValue(preview);
                Submit("Rotate character");Submit("Equip");yield return null;
                Assert.AreSame(selectedModel,previewModel.GetValue(preview),"Equipping the selection must retain its displayed portrait model.");
                Assert.AreEqual("farseer",progression.EquippedDagger);Assert.IsTrue(oldLeft==null);
                Assert.IsNotNull(Prop<GameObject>(player,"RightModel"));Assert.AreEqual(listeners,Object.FindObjectsByType<AudioListener>().Length);
                var animator=(Animator)playerType.GetField("rig").GetValue(player);
                Assert.IsTrue(Prop<GameObject>(player,"LeftModel").transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
                Assert.IsTrue(Prop<GameObject>(player,"RightModel").transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)));
                Assert.AreEqual(start.x,player.transform.position.x,.02f);
                Submit("Back");yield return null;yield return null;
                Assert.IsFalse(host.IsPaused);Assert.IsTrue(surface==null);
                float Stored()=>progression.StoredDaggerReserve("farseer");
                Type Runtime(string name)=>Type.GetType("AirflowPrototype."+name+", Assembly-CSharp",true);
                var meter=GameObject.Find("Second Wind reserve");
                Assert.IsNotNull(meter);var caption=meter.GetComponentInChildren<Text>();
                Assert.That(caption.text,Does.Contain("0 / 50"));
                var health=(Component)Object.FindAnyObjectByType(Runtime("AirSlingNodeHealth"));
                Assert.IsTrue((bool)Call(health,"TakeDamage",int.MaxValue));
                Assert.IsFalse((bool)Call(health,"TakeDamage",int.MaxValue));
                for(int i=0;i<240 && Stored()==0;i++)yield return null;
                float charged=Stored();Assert.Greater(charged,0);Assert.LessOrEqual(charged,50);
                Assert.That(caption.text,Does.Not.Contain("   0 /"));
                if(rendered)
                {
                    var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    string shot=Path.GetFullPath("Logs/ProductionMigration/reserve-hud-"+DateTime.UtcNow.Ticks+".png");
                    view.Repaint();yield return null;ScreenCapture.CaptureScreenshot(shot);
                    for(int i=0;i<60 && !File.Exists(shot);i++){view.Repaint();yield return null;}
                    Assert.IsTrue(File.Exists(shot));
                    var frame=new Texture2D(2,2);
                    try
                    {
                        Assert.IsTrue(frame.LoadImage(File.ReadAllBytes(shot)));
                        var fill=(RectTransform)meter.transform.Find("Stored AirPower");
                        var screen=RectTransformUtility.WorldToScreenPoint(null,fill.TransformPoint(
                            new Vector3(fill.rect.xMin+fill.rect.width*.1f,fill.rect.center.y)));
                        var pixel=frame.GetPixel(Mathf.RoundToInt(screen.x),Mathf.RoundToInt(screen.y));
                        Assert.Greater(pixel.g-pixel.r,.12f,"The cyan reserve must be visible over the world HUD.");
                    }
                    finally {Object.Destroy(frame);}
                }
                Keys(Key.R);yield return null;yield return null;
                Assert.AreEqual(charged,Stored(),"Full main bar preserves stored energy.");
                Keys();yield return null;Call(power,"Spend",10f);
                using(var block=host.Input.BlockGameplay())
                {
                    Keys(Key.R);yield return null;yield return null;Assert.AreEqual(charged,Stored());
                    Keys();yield return null;
                }
                yield return null;
                var film=Object.FindAnyObjectByType<ProgressionCinematicPlayer>();
                Assert.IsNotNull(film);
                using(film.Acquire())
                {
                    Assert.IsFalse(film.gameplayCamera.enabled);Assert.IsTrue(film.cinematicCamera.enabled);
                    Keys(Key.R);yield return null;yield return null;Assert.AreEqual(charged,Stored());
                }
                yield return null;Assert.AreEqual(charged,Stored(),"A held cinematic input cannot transfer on restoration.");
                Assert.IsTrue(film.gameplayCamera.enabled);Keys();yield return null;
                using(UnityTimeAuthority.Pause())
                {
                    Keys(Key.R);yield return null;yield return null;Assert.AreEqual(charged,Stored());
                }
                yield return null;Keys();yield return null;
                int bursts()=>Object.FindObjectsByType<ParticleSystem>().Count(ps=>ps.name.StartsWith("Second Wind burst"));
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadLeft));yield return null;yield return null;
                Assert.AreEqual(Prop<float>(power,"Max"),Prop<float>(power,"Current"),.01f);
                Assert.Zero(Stored());Assert.AreEqual(1,bursts());
                InputSystem.RemoveDevice(pad);yield return null;
                pad=InputSystem.AddDevice<Gamepad>();yield return null;
                Call(power,"Spend",15f);
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadLeft));yield return null;
                Assert.Less(Prop<float>(power,"Current"),Prop<float>(power,"Max"));Assert.AreEqual(1,bursts());
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                var caster=player.GetComponent(Runtime("AirCaster"));
                var node=new GameObject("Disposable return target").AddComponent(Runtime("AirNode"));
                node.transform.position=player.transform.position+Vector3.forward*2;
                caster.GetType().GetMethod("NotifyPulseHit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                    .Invoke(caster,new object[]{node,1f});
                // Change ownership while the first return is in flight.
                var equipment=Gear(player);InvokeRule(equipment,"Equip",Definition(equipment,"training"));
                yield return new WaitForSeconds(.35f);
                Assert.Zero(Stored());Assert.AreEqual(Prop<float>(power,"Max"),Prop<float>(power,"Current"),.01);
                InvokeRule(equipment,"Equip",Definition(equipment,"farseer"));
                caster.GetType().GetMethod("NotifyPulseHit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                    .Invoke(caster,new object[]{node,1f});
                for(int i=0;i<240 && Stored()==0;i++)yield return null;
                float bank=Stored();Assert.Greater(bank,0);
                host.SetPaused(true);yield return null;Submit("Dagger Arsenal");yield return null;
                Assert.IsFalse(meter.activeInHierarchy);
                Submit("Training Daggers");Submit("Equip");yield return null;Submit("Back");Submit("Resume");yield return null;
                Assert.IsFalse(meter.activeInHierarchy);
                host.SetPaused(true);yield return null;Submit("Dagger Arsenal");Submit("Farseer's Daggers");Submit("Equip");yield return null;
                Assert.AreEqual(bank,Stored());Submit("Back");Submit("Resume");yield return null;yield return null;
                Assert.IsTrue(meter.activeInHierarchy);Assert.AreEqual(bank,Stored());
                Call(session,"Save");Assert.AreEqual(bank,slots.Open(id).Load().daggers.Stored("farseer"));
                Assert.Zero(slots.Open(other.slotId).Load().daggers?.Stored("farseer")??0);
                Call(session,"ReturnToTitle");yield return Changed(Title,session);title=Session();Submit("Continue");yield return Changed(World,title);
                var restored=Prop<ProgressionService>(Session(),"Progression");
                Assert.AreEqual("farseer",restored.EquippedDagger);Assert.AreEqual(bank,restored.StoredDaggerReserve("farseer"));
                player=DaggerOwner();power=Resource(player);var dash=player.GetComponent(Runtime("AirDash"));
                var action=WindrunnerInputHost.Active.Input.GetAction("Gameplay","DaggerAbility");
                action.ApplyBindingOverride(0,"<Keyboard>/u"); // Runtime clone in a disposable session.
                Keys(Key.W,Key.LeftShift);yield return null;yield return null;yield return null;
                Assert.IsTrue(Prop<bool>(dash,"IsDashing"));Assert.Less(Prop<float>(power,"Current"),Prop<float>(power,"Max"));
                var duringDash=Gear(player);
                InvokeRule(duringDash,"Equip",Definition(duringDash,"training"));
                float missing=Prop<float>(power,"Max")-Prop<float>(power,"Current");
                NodeReturn(power,missing+6);
                Assert.AreEqual(bank,restored.StoredDaggerReserve("farseer"));Assert.IsTrue(Prop<bool>(dash,"IsDashing"));
                InvokeRule(duringDash,"Equip",Definition(duringDash,"farseer"));
                NodeReturn(power,3);bank=Mathf.Min(50,bank+3);
                Assert.AreEqual(bank,restored.StoredDaggerReserve("farseer"));
                Keys(Key.W,Key.LeftShift,Key.R);yield return null;Assert.AreEqual(bank,restored.StoredDaggerReserve("farseer"));
                using(UnityTimeAuthority.Slowdown(.12f))
                {
                    Keys(Key.W,Key.LeftShift,Key.U);yield return null;yield return null;
                    Assert.Zero(restored.StoredDaggerReserve("farseer"));Assert.IsTrue(Prop<bool>(dash,"IsDashing"));
                    Assert.LessOrEqual(Prop<float>(power,"Current"),Prop<float>(power,"Max"));
                }
                Keys();yield return null;
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
