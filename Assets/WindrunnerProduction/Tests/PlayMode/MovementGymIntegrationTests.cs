#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Reflection;
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
        [UnityTest, Category("Profiling")]
        public IEnumerator StationHudAtDesktopResolutions()
        {
            const string path="Assets/MovementLab/MovementGym.unity";
            if(ProjectRoot.Active!=null){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var lab=(Component)Object.FindAnyObjectByType(Type.GetType("Windrunner.MovementLab.MovementLabPlayer, Assembly-CSharp",true));
            lab.GetType().GetMethod("Teleport").Invoke(lab,new object[]{6});
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
                foreach (int width in new[] { 1280, 1600, 1920 })
                {
                    int height = width * 9 / 16;
                    var sizeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var enumType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var size = Activator.CreateInstance(sizeType, Enum.Parse(enumType, "FixedResolution"), width, height, "Movement HUD");
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedSize.SetValue(gameView, (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1);
                    gameView.Repaint(); for (int n = 0; n < 10; n++) yield return null;
                    Assert.AreEqual(width, Screen.width); Assert.AreEqual(height, Screen.height);
                    var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                    string display=(string)lab.GetType().GetField("display",flags).GetValue(lab);
                    Assert.That(display,Does.Contain("F10"));Assert.That(display,Does.Contain("F12"));
                    var style=(GUIStyle)lab.GetType().GetField("diagnosticsStyle",flags).GetValue(lab);
                    Assert.Less(style.CalcHeight(new GUIContent(display),Mathf.Min(width-24,900)),height*.4f);
                    string file=Path.GetFullPath("Logs/ProductionMigration/gym-wind-"+width+"-"+DateTime.UtcNow.Ticks+".png");
                    ScreenCapture.CaptureScreenshot(file);
                    for(int frame=0;frame<30 && !File.Exists(file);frame++)yield return null;
                    Assert.IsTrue(File.Exists(file));Debug.Log("Movement HUD capture "+file);
                }
            }
            finally
            {
                selectedSize.SetValue(gameView, previousSize);
                for (int index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1; index >= originalCount; index--)
                    groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                if(ProjectRoot.Active!=null)Object.Destroy(ProjectRoot.Active.gameObject);
            }
            yield return null;
            var empty=SceneManager.CreateScene("Movement HUD teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(path));
        }
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
                Assert.AreEqual(1,Object.FindObjectsByType<ProjectRoot>().Length);
                Assert.AreEqual(1,Object.FindObjectsByType<WindrunnerInputHost>().Length);
                Assert.IsNull(Object.FindAnyObjectByType(Type.GetType("Windrunner.Frontend.PlayerSession, Assembly-CSharp",true)));
                var input = WindrunnerInputHost.Active.Input;
                // Clear user overrides on the test session clone only; every event uses the lab defaults.
                input.Actions.RemoveAllBindingOverrides();
                var motor=(Component)lab.GetType().GetField("motor").GetValue(lab);
                var power=(Component)lab.GetType().GetField("power").GetValue(lab);
                var controller=lab.GetComponent<CharacterController>();
                Time.captureDeltaTime=1f/60f;
                var lanes=(Array)lab.GetType().GetField("lanes").GetValue(lab);
                object Field(Component component,string name)=>component.GetType().GetField(name).GetValue(component);
                void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
                var ability=(Component)Field(lab,"sling");
                var aim=lab.GetComponent(Type.GetType("AirflowPrototype.AirSlingTargeter, Assembly-CSharp",true));
                var failures=new List<string>();
                foreach(bool extreme in new[]{false,true})
                for(int station=0;station<(extreme?9:10);station++)
                {
                    var lane=(Component)lanes.GetValue(station);
                    if(extreme)
                    {
                        var configure=lane.GetType().GetMethod("SetPreset");
                        configure.Invoke(lane,new object[]{Enum.ToObject(configure.GetParameters()[0].ParameterType,3)});
                        lane.GetType().GetMethod("Apply").Invoke(lane,null);Physics.SyncTransforms();
                    }
                    Keys((Key)((int)Key.F1+station));yield return null;yield return null;
                    Assert.AreEqual(station,Field(lab,"destinationIndex"),"direct key");
                    Keys();for(int t=0;t<24;t++)yield return null;
                    var gate=(Transform)Field(lane,"startGate");var spawn=(Transform)Field(lane,"destination");
                    var end=(Transform)Field(lane,"endGate");
                    for(int track=0;track<(station==6&&!extreme?3:1);track++)
                    {
                        if(track>0){Keys(Key.V);yield return null;yield return null;Keys();for(int t=0;t<22;t++)yield return null;}
                        bool jumped=false, traversed=false,paid=false,airborne=false;
                        int charge=-1;
                        var stationOrigin=lab.transform.position;float run=0,rise=0;
                        string stop="35 s input budget";

                        for(int tick=0;tick<2100;tick++)
                        {
                            float z=Vector3.Dot(lab.transform.position-gate.position,spawn.forward);
                            bool jump=(station==1||station==4||station==9) && !jumped && z>-.8f && z<1;
                            jumped|=jump;
                            bool dash=station==2 || (station==5 && z<10) || (station==9 && z<12);
                            bool boost=station==7||station==8;
                            var keys=new List<Key>{Key.W};
                            if(jump)keys.Add(Key.Space);
                            if(station==4 && jumped && Vector3.Dot(lab.transform.position-spawn.position,spawn.right)<4)keys.Add(Key.D);
                            if(extreme && station==2 && z>0 && Vector3.Dot(lab.transform.position-gate.position,gate.right)<-.3f) keys.Add(Key.D);
                            if(dash)keys.Add(Key.LeftShift);
                            if(boost)keys.Add(Key.Q);
                            Keys(keys.ToArray());
                            if((station==3||station==9) && charge<0 && Read(aim,"CurrentTarget")!=null)
                            {
                                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightShoulder));charge=tick;
                            }
                            if(charge>=0 && tick==charge+14)InputSystem.QueueStateEvent(pad,new GamepadState());
                            yield return null;
                            airborne|=!(bool)Read(motor,"IsGrounded");
                            traversed|=(bool)Read(ability,"IsDashingToNode");
                            paid|=(float)Read(power,"Current")<(float)Read(power,"Max")-.01f;
                            var offset=lab.transform.position-stationOrigin;
                            rise=Mathf.Max(rise,offset.y);offset.y=0;
                            if(extreme && run>4 && offset.magnitude<1){stop="fell below recovery plane and returned to pad";break;}
                            run=Mathf.Max(run,offset.magnitude);
                            if((bool)Read(lab,"Finished")){stop="grounded beyond finish stripe";break;}
                        }
                        string result=$"Station {station+1}/{track} {(extreme?"Extreme":"Authored")} run={run:F2} rise={rise:F2} stop={stop}: finish={Read(lab,"Finished")} position={lab.transform.position} stripe={end.position} peak={Read(lab,"Peak")} jump={jumped} sling={traversed} spent={paid} wind={Read(lab,"EntrySpeed")}/{Read(lab,"InsideSpeed")}/{Read(lab,"ExitSpeed")}";
                        Debug.Log(result);
                        if(!extreme && station==6)
                        {
                            float entry=(float)Read(lab,"EntrySpeed"),inside=(float)Read(lab,"InsideSpeed"),exit=(float)Read(lab,"ExitSpeed");
                            Assert.Greater(entry,7);Assert.Greater(exit,7);
                            if(track==0)Assert.AreEqual(entry,inside,.05f);
                            else {Assert.Greater(inside,entry+3);Assert.Greater(exit,entry+1);}
                        }
                        if(!extreme && (station==1||station==4)&&!(jumped&&airborne))failures.Add("Missing airborne jump: "+result);
                        if(!extreme && (!(bool)Read(lab,"Finished") || ((station==3||station==9) && !traversed) || (station==2||station==8) && !paid))failures.Add(result);
                        Keys(); InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                    }
                }
                Assert.IsEmpty(failures,string.Join("\n",failures));
                Keys(Key.F12);yield return null;yield return null;
                Assert.AreEqual(Read(power,"Max"),Read(power,"Current"));Keys();yield return null;
                power.GetType().GetMethod("Spend").Invoke(power,new object[]{12f});
                float stored=(float)Read(power,"Current");
                var orbitCamera=(Camera)Read(motor,"ViewCamera");
                var animator=lab.GetComponentInChildren<Animator>();
                Assert.IsTrue(orbitCamera.isActiveAndEnabled && animator.isActiveAndEnabled);
                Keys(Key.F11);yield return null;yield return null;
                Assert.IsFalse(orbitCamera.isActiveAndEnabled || animator.isActiveAndEnabled);
                Assert.IsFalse(lab.gameObject.activeSelf);
                var rig=(Component)Object.FindAnyObjectByType(Type.GetType("Windrunner.MovementLab.LabFlightRig, Assembly-CSharp",true));
                Assert.IsNotNull(rig);var flightOrigin=rig.transform.position;
                Keys(Key.W,Key.Space,Key.LeftShift);for(int i=0;i<30;i++)yield return null;
                Assert.Greater(Vector3.Distance(flightOrigin,rig.transform.position),3);
                Keys(Key.F11);yield return null;yield return null;
                Assert.IsTrue(lab.gameObject.activeSelf && controller.enabled);
                Assert.AreEqual(stored,(float)Read(power,"Current"),.01f);
                Assert.IsTrue(orbitCamera.isActiveAndEnabled && animator.isActiveAndEnabled);
                Keys();for(int i=0;i<30;i++)yield return null;
                Assert.IsTrue((bool)Read(motor,"IsGrounded"),"Flight exit must leave the production motor on its own pad.");
                var rebound=input.Actions.FindAction("Gameplay/Station1",true);
                rebound.ApplyBindingOverride(0,"<Keyboard>/k");
                Keys(Key.K);yield return null;yield return null;
                Assert.AreEqual(0,Field(lab,"destinationIndex"),"Station selection follows the rebound action on the session clone.");
                rebound.RemoveBindingOverride(0);
                Keys();for(int i=0;i<30;i++)yield return null;
                Assert.IsTrue((bool)Read(motor,"IsGrounded"));Keys(Key.F12);yield return null;yield return null;Keys();yield return null;
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
                    Assert.IsTrue((bool)Read(lab,"Boosting"));Assert.Greater((float)Read(lab,"WindGain"),0);
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
                var combined=(Component)lanes.GetValue(9);
                var presetMethod=combined.GetType().GetMethod("SetPreset");
                presetMethod.Invoke(combined,new object[]{Enum.ToObject(presetMethod.GetParameters()[0].ParameterType,3)});
                combined.GetType().GetMethod("Apply").Invoke(combined,null);Physics.SyncTransforms();
                lab.GetType().GetMethod("Teleport").Invoke(lab,new object[]{9});
                for(int i=0;i<8;i++) yield return null;
                var sling=(Component)lab.GetType().GetField("sling").GetValue(lab);
                var targeter=lab.GetComponent(Type.GetType("AirflowPrototype.AirSlingTargeter, Assembly-CSharp",true));
                var origin=lab.transform.position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Q));
                float extremePeak=0, maxRise=0, maxRun=0;
                int chargeFrame=-1;bool sawCharge=false, sawFlight=false;
                string extremeStop="60 s input budget";
                for(int i=0;i<3600;i++)
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
                    maxRise=Mathf.Max(maxRise,offset.y);offset.y=0;
                    if(maxRun>5 && offset.magnitude<1){extremeStop="returned to recovery pad";break;}
                    maxRun=Mathf.Max(maxRun,offset.magnitude);
                    if((bool)Read(lab,"Finished")){extremeStop="grounded beyond finish stripe";break;}
                }
                Debug.Log($"Extreme lane: run {maxRun}m, rise {maxRise}m, peak {extremePeak}m/s, charge frame {chargeFrame}, charge {sawCharge}, node dash {sawFlight}");
                Assert.IsTrue(sawCharge,"Extreme course must offer an accessible authored Sling target.");
                Assert.IsTrue(sawFlight,"A valid release must transition to real node traversal.");
                File.WriteAllText("Logs/ProductionMigration/movement-extreme-"+backend+".txt",FormattableString.Invariant($"run_m={maxRun:F2},rise_m={maxRise:F2},peak_mps={extremePeak:F2},charge_frame={chargeFrame},node_flight={sawFlight},finished={Read(lab,"Finished")},end={lab.transform.position},stop={extremeStop}"));
                Assert.IsTrue((bool)Read(lab,"Finished"),"Extreme combined lost its validated grounded finish: "+extremeStop);
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
            Assert.IsTrue(ProjectRoot.Active==null && WindrunnerInputHost.Active==null,"Standalone gym shutdown must release startup owners.");
            Assert.IsEmpty(Object.FindObjectsByType(Type.GetType("Windrunner.MovementLab.MovementLabPlayer, Assembly-CSharp",true)));

        }
    }
}
#endif
