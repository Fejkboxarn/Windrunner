#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Input;
using Windrunner.Production.Unity;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class MovementGymIntegrationTests
    {
        private static object Read(Component c,string name) => c.GetType().GetProperty(name).GetValue(c);
        [UnityTest] public IEnumerator ProductionMotorTunnelOverdriveAndBarrier()
        {
            if(ProjectRoot.Active!=null) { Object.Destroy(ProjectRoot.Active.gameObject); yield return null; }
            var original=InputSystem.settings;var flags=original.hideFlags;
            if(flags==HideFlags.HideAndDontSave) original.hideFlags=HideFlags.DontSave;
            var settings=Object.Instantiate(original);settings.hideFlags=HideFlags.DontSave;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=settings;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var pad=InputSystem.AddDevice<Gamepad>();
            float capture=Time.captureDeltaTime, fixedStep=Time.fixedDeltaTime,scale=Time.timeScale;
            var results=new List<string>();
            const string path="Assets/MovementLab/MovementGym.unity";
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var lab=(MonoBehaviour)Object.FindAnyObjectByType(Type.GetType("Windrunner.MovementLab.MovementLabPlayer, Assembly-CSharp",true));
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    gameView.Show(); gameView.Repaint();
                    for(int i=0;i<15;i++) yield return null;
                    string shot=Path.GetFullPath("Logs/ProductionMigration/movement-gym-spawn-"+DateTime.UtcNow.Ticks+".png");
                    ScreenCapture.CaptureScreenshot(shot);
                    for(int i=0;i<20 && !File.Exists(shot);i++){ gameView.Repaint(); yield return null; }
                    Assert.IsTrue(File.Exists(shot),"Expected actual rendered Game View capture.");
                    Debug.Log($"Gym capture {Screen.width} x {Screen.height}: {shot}");
                }
                var input = WindrunnerInputHost.Active.Input;
                // Clear user overrides on the test session clone only; every event uses the lab defaults.
                input.Actions.RemoveAllBindingOverrides();
                var motor=(Component)lab.GetType().GetField("motor").GetValue(lab);
                var power=(Component)lab.GetType().GetField("power").GetValue(lab);
                var controller=lab.GetComponent<CharacterController>();
                var forward=Vector3.forward;
                var start=new Vector3(2400,420,-100);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=start+forward*260-Vector3.up*1.2f;
                floor.transform.rotation=Quaternion.LookRotation(forward);floor.transform.localScale=new Vector3(200,2,610);
                var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=start+forward*400+Vector3.up*3;
                barrier.transform.rotation=floor.transform.rotation;barrier.transform.localScale=new Vector3(200,100,.3f);
                var volumes=(Array)lab.GetType().GetField("volumes").GetValue(lab);
                var wind=(Component)volumes.GetValue(0);wind.gameObject.SetActive(true);
                wind.transform.SetPositionAndRotation(start+forward*165,Quaternion.LookRotation(forward));
                wind.transform.localScale=new Vector3(35,30,350);
                var equipped = lab.GetType().GetField("experimentalPairEquipped");
                equipped.SetValue(lab,false);
                float full=(float)Read(power,"Current");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));yield return null; yield return null;
                Assert.IsFalse((bool)Read(lab,"Boosting"));Assert.AreEqual(full,(float)Read(power,"Current"));
                equipped.SetValue(lab,true);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                using var marker = global::Unity.Profiling.ProfilerRecorder.StartNew(global::Unity.Profiling.ProfilerCategory.Scripts,"MovementLab.ResolveInfluences");
                foreach(int fps in new[]{30,60,120})
                {
                    float dt=1f/fps;Time.captureDeltaTime=dt;Time.fixedDeltaTime=fps==30?.033333f:fps==60?.02f:.01f;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                    lab.GetType().GetMethod("Teleport").Invoke(lab,new object[]{0});
                    controller.enabled=false;lab.transform.position=start+Vector3.up;controller.enabled=true; Physics.SyncTransforms();
                    lab.GetType().GetMethod("ResetSession").Invoke(lab,null);
                    for(int i=0;i<fps/2;i++) yield return null;
                    float before=(float)Read(power,"Current");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Q,Key.LeftShift));
                    double begin=Time.realtimeSinceStartupAsDouble;
                    for(int i=0;i<fps;i++) yield return null;
                    Assert.IsTrue((bool)Read(lab,"Boosting"));Assert.Greater((float)Read(lab,"WindGain"),1);
                    Assert.Less((float)Read(power,"Current"),before);
                    Time.timeScale=0;
                    float frozenPower=(float)Read(power,"Current");Vector3 frozenPosition=lab.transform.position;
                    for(int i=0;i<4;i++) yield return null;
                    Assert.AreEqual(frozenPower,(float)Read(power,"Current"));Assert.AreEqual(frozenPosition,lab.transform.position);
                    Time.timeScale=.2f;yield return null;Time.timeScale=1;
                    Assert.LessOrEqual(frozenPower-(float)Read(power,"Current"),2f,"One short scaled tick cannot spend a full second of power.");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Q));
                    var intervals = new double[fps*3];
                    var markerTimes = new long[intervals.Length];
                    double previous = Time.realtimeSinceStartupAsDouble;
                    for(int i=0;i<intervals.Length;i++)
                    {
                        yield return null;
                        double now = Time.realtimeSinceStartupAsDouble;
                        markerTimes[i] = marker.Valid ? marker.LastValue : 0;
                        intervals[i] = 1000 * (now - previous); previous = now;
                    }
                    Array.Sort(intervals);Array.Sort(markerTimes);
                    float peak=(float)Read(lab,"Peak");Assert.Greater(peak,50);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                    for(int i=0;i<fps;i++) yield return null;
                    Assert.IsFalse((bool)Read(lab,"Boosting"));
                    float progress=Vector3.Dot(lab.transform.position-start,forward);
                    Debug.Log($"Lab probe fps={fps} peak={peak} start={start} end={lab.transform.position} wall-local={barrier.transform.InverseTransformPoint(lab.transform.position)} speed={Read(motor,"Velocity")}");
                    Assert.That(progress,Is.InRange(300f,400.2f),"High-speed run must travel hundreds of meters and stay on the near side of the wall.");
                    Assert.Greater(lab.transform.position.y,start.y-2);
                    results.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F5},{2:F2},{3:F2},{4:F2},{5:F3},{6:F3},{7:F3},{8:F3}",fps,Time.fixedDeltaTime,peak,progress,(float)Read(lab,"WindGain"),Time.realtimeSinceStartupAsDouble-begin,intervals[intervals.Length/2],intervals[(int)(intervals.Length*.95)],markerTimes[markerTimes.Length/2]*.001));
                }
                string backend=SystemInfo.graphicsDeviceType.ToString();
                File.WriteAllLines("Logs/ProductionMigration/movement-gym-probes-"+backend+".csv",System.Linq.Enumerable.Concat(new[]{"capture_fps,fixed_dt,peak_mps,forward_m,wind_added_mps,wall_seconds,frame_interval_median_ms,frame_interval_p95_ms,influence_median_us"},results));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                lab.GetType().GetMethod("Teleport").Invoke(lab,new object[]{1});
                for(int i=0;i<30;i++) yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space,Key.Q));
                for(int i=0;i<6;i++) yield return null;
                Assert.IsTrue((bool)Read(lab,"Boosting")); Assert.Greater((float)Read(motor,"VerticalSpeed"),0,"Jump keeps the real upward movement while Overdrive is held.");
                // Hold across an actual menu interruption. A fresh press must be required after resume.
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q)); yield return null; yield return null;
                WindrunnerInputHost.Active.SetPaused(true);yield return null;
                Assert.IsFalse((bool)Read(lab,"Boosting"));
                WindrunnerInputHost.Active.SetPaused(false); yield return null;yield return null;
                Assert.IsFalse((bool)Read(lab,"Boosting"));
                // Exercise the authored extreme course with its real camera, targets, charge and node flight.
                Time.captureDeltaTime=1f/60; Time.fixedDeltaTime=.02f;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                lab.GetType().GetMethod("Teleport").Invoke(lab,new object[]{9});
                for(int i=0;i<8;i++) yield return null;
                var sling=(Component)lab.GetType().GetField("sling").GetValue(lab);
                var targeter=lab.GetComponent(Type.GetType("AirflowPrototype.AirSlingTargeter, Assembly-CSharp",true));
                var origin=lab.transform.position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Q));
                float extremePeak=0, maxRise=0, maxRun=0;
                int chargeFrame=-1;bool sawCharge=false, sawFlight=false;
                for(int i=0;i<360;i++)
                {
                    if(chargeFrame<0 && Read(targeter,"CurrentTarget")!=null)
                    {
                        chargeFrame=i;
                        InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightShoulder));
                    }
                    if(chargeFrame>=0 && i==chargeFrame+12) InputSystem.QueueStateEvent(pad,new GamepadState());
                    yield return null;
                    sawCharge |= (bool)Read(sling,"IsCharging");
                    sawFlight |= (bool)Read(sling,"IsDashingToNode");
                    if((bool)Read(sling,"IsDashingToNode")) Assert.IsFalse((bool)Read(lab,"Boosting"),"Sling owns its authored traversal.");
                    extremePeak=Mathf.Max(extremePeak,((Vector3)Read(motor,"Velocity")).magnitude);
                    Vector3 offset=lab.transform.position-origin;
                    maxRise=Mathf.Max(maxRise,offset.y);offset.y=0;maxRun=Mathf.Max(maxRun,offset.magnitude);
                }
                Debug.Log($"Extreme lane: run {maxRun}m, rise {maxRise}m, peak {extremePeak}m/s, charge frame {chargeFrame}, charge {sawCharge}, node dash {sawFlight}");
                Assert.IsTrue(sawCharge,"Extreme course must offer an accessible authored Sling target.");
                Assert.IsTrue(sawFlight,"A valid release must transition to real node traversal.");
                File.WriteAllText("Logs/ProductionMigration/movement-extreme-"+backend+".txt",FormattableString.Invariant($"run_m={maxRun:F2},rise_m={maxRise:F2},peak_mps={extremePeak:F2},charge_frame={chargeFrame},node_flight={sawFlight}"));
            }
            finally
            {
                Time.timeScale=scale;Time.captureDeltaTime=capture;Time.fixedDeltaTime=fixedStep;
                InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;Object.Destroy(settings);
                if(ProjectRoot.Active!=null) Object.Destroy(ProjectRoot.Active.gameObject);
            }
            yield return null;
            var empty=SceneManager.CreateScene("Gym test teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(path));
        }
    }
}
#endif
