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
            float capture=Time.captureDeltaTime, fixedStep=Time.fixedDeltaTime,scale=Time.timeScale;
            var results=new List<string>();
            const string path="Assets/MovementLab/MovementGym.unity";
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var lab=(MonoBehaviour)Object.FindAnyObjectByType(Type.GetType("Windrunner.MovementLab.MovementLabPlayer, Assembly-CSharp",true));
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
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Q));
                    for(int i=0;i<fps*3;i++) yield return null;
                    float peak=(float)Read(lab,"Peak");Assert.Greater(peak,50);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                    for(int i=0;i<fps;i++) yield return null;
                    Assert.IsFalse((bool)Read(lab,"Boosting"));
                    float progress=Vector3.Dot(lab.transform.position-start,forward);
                    Debug.Log($"Lab probe fps={fps} peak={peak} start={start} end={lab.transform.position} wall-local={barrier.transform.InverseTransformPoint(lab.transform.position)} speed={Read(motor,"Velocity")}");
                    Assert.That(progress,Is.InRange(300f,400.2f),"High-speed run must travel hundreds of meters and stay on the near side of the wall.");
                    Assert.Greater(lab.transform.position.y,start.y-2);
                    results.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F5},{2:F2},{3:F2},{4:F2},{5:F3}",fps,Time.fixedDeltaTime,peak,progress,(float)Read(lab,"WindGain"),Time.realtimeSinceStartupAsDouble-begin));
                }
                File.WriteAllLines("Logs/ProductionMigration/movement-gym-probes.csv",System.Linq.Enumerable.Concat(new[]{"capture_fps,fixed_dt,peak_mps,forward_m,wind_added_mps,wall_seconds"},results));
            }
            finally
            {
                Time.timeScale=scale;Time.captureDeltaTime=capture;Time.fixedDeltaTime=fixedStep;
                InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;Object.Destroy(settings);
                if(ProjectRoot.Active!=null) Object.Destroy(ProjectRoot.Active.gameObject);
            }
            yield return null;
            var empty=SceneManager.CreateScene("Gym test teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(path));
        }
    }
}
#endif
