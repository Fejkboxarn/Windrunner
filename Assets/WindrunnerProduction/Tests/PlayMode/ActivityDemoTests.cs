#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;
namespace Windrunner.Production.Tests
{
    public sealed class ActivityDemoTests
    {
        Gamepad pad;InputSettings originalInput,testInput;PursuitProfile runtimeProfile;HideFlags originalFlags;
        const string Scene="Assets/WindrunnerActivities/Demonstration/RelayPursuitDemo.unity";
        static Type T(string n)=>Type.GetType("Windrunner.Activities."+n+", Assembly-CSharp",true);
        static object Field(object o,string n)=>o.GetType().GetField(n).GetValue(o);
        static object Property(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
        static bool Begin(object o)=>(bool)o.GetType().GetMethod("Begin").Invoke(o,null);
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(originalInput){InputSystem.settings=originalInput;originalInput.hideFlags=originalFlags;}if(testInput)Object.Destroy(testInput);if(runtimeProfile)Object.Destroy(runtimeProfile);
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            var prior=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Activity test teardown"));if(prior.IsValid()&&prior.isLoaded)yield return SceneManager.UnloadSceneAsync(prior);
        }
        [UnityTest] public IEnumerator DemoLoadsAllFourStationsGroundTracksAndPauseStopsTimers()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));for(int i=0;i<20;i++)yield return null;
            var relay=(Component)Object.FindAnyObjectByType(T("WindRelayHost"));Assert.IsNotNull(relay);var actor=(ProgressionActor)Field(relay,"actor");Assert.IsNotNull(actor.Service);
            var session=Object.FindAnyObjectByType(Type.GetType("Windrunner.Frontend.PlayerSession, Assembly-CSharp",true));Assert.IsNull(Property(session,"SlotId"));
            Assert.IsTrue(Begin(relay));var rules=(WindRelaySession)Property(relay,"Session");
            using(actor.Input.BlockGameplay()){float elapsed=rules.Elapsed;for(int i=0;i<5;i++)yield return null;Assert.AreEqual(elapsed,rules.Elapsed);}
            Capture("relay-available",((Component)((Array)Field(relay,"nodes")).GetValue(0)).transform.position);
            rules.Tick(100);Assert.AreEqual(ActivityState.Failed,rules.State);yield return null;Capture("relay-expired",((Component)((Array)Field(relay,"nodes")).GetValue(0)).transform.position);Assert.IsTrue(Begin(relay));Capture("relay-retry",((Component)((Array)Field(relay,"nodes")).GetValue(0)).transform.position);
            var hosts=Object.FindObjectsByType(T("PursuitHost"),FindObjectsSortMode.None);Assert.AreEqual(3,hosts.Length);
            foreach(Component host in hosts)
            {
                Assert.IsTrue(Begin(host));var profile=(PursuitProfile)Field(host,"profile");var target=(Transform)Field(host,"target");var pursuit=(PursuitSession)Property(host,"Session");
                Vector3 before=target.position;float deadline=Time.realtimeSinceStartup+8;while(Vector3.Distance(before,target.position)<.1f&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsNull(Property(host,"Error"));Assert.AreNotEqual(before,target.position,"Target must advance after its start delay.");
                if(profile.definition.mode==PursuitMode.Ground)Assert.AreEqual(1000.6f,target.position.y,.03f,"Ground probe must ignore the target's own collider.");
                Capture(profile.definition.mode.ToString(),target.position);
                var behaviour=(Behaviour)host;behaviour.enabled=false;Assert.AreEqual(PursuitState.Canceled,pursuit.State);behaviour.enabled=true;Assert.IsTrue(Begin(host));
            }
            // Explicit rule-driver evidence is separate from the four actual Cast/Sling integration tests.
            var nodes=(Array)Field(relay,"nodes");foreach(var item in nodes)
            {
                Assert.IsTrue((bool)relay.GetType().GetMethod("Activate").Invoke(relay,new[]{item,(object)ActivationMethod.Cast,Guid.NewGuid().ToString("N")}));
                yield return null;
            }
            Assert.AreEqual(ActivityState.Completed,rules.State);Assert.AreEqual(QuestStatus.Completed,actor.Service.Status("demo-relay-quest"));Capture("relay-complete",((Component)nodes.GetValue(2)).transform.position);
        }
        [UnityTest] public IEnumerator ActualProductionMovementCanProvokeNearMissThenCapture()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            originalInput=InputSystem.settings;originalFlags=originalInput.hideFlags;if(originalFlags==HideFlags.HideAndDontSave)originalInput.hideFlags=HideFlags.DontSave;testInput=Object.Instantiate(originalInput);testInput.hideFlags=HideFlags.DontSave;testInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;testInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=testInput;
            pad=InputSystem.AddDevice<Gamepad>();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));for(int i=0;i<20;i++)yield return null;
            var host=(Component)Object.FindObjectsByType(T("PursuitHost"),FindObjectsSortMode.None).First(h=>((PursuitProfile)Field(h,"profile")).definition.mode==PursuitMode.Ground);
            runtimeProfile=Object.Instantiate((PursuitProfile)Field(host,"profile"));var d=runtimeProfile.definition;d.startDelay=0;d.startDistance=20;d.speed=4;d.maxSeconds=0;d.nearMiss=true;d.capture=ActivationMethod.Proximity;d.reactionProbability=1;d.points.Clear();d.points.Add(new PursuitWaypoint());d.points.Add(new PursuitWaypoint{position=Vector3.forward*100});host.GetType().GetField("profile").SetValue(host,runtimeProfile);
            var actor=(ProgressionActor)Field(host,"actor");var controller=actor.GetComponent<CharacterController>();var orbit=(Behaviour)Object.FindAnyObjectByType(Type.GetType("AirflowPrototype.PlayerOrbitCamera, Assembly-CSharp",true));orbit.enabled=false;var camera=orbit.GetComponent<Camera>();
            controller.enabled=false;actor.transform.position=host.transform.position+new Vector3(2.5f,1,0);controller.enabled=true;Physics.SyncTransforms();camera.transform.rotation=Quaternion.identity;
            Assert.IsTrue(Begin(host));var session=(PursuitSession)Property(host,"Session");Vector3 start=actor.transform.position;
            float deadline=Time.realtimeSinceStartup+18;
            while(session.Reactions==0&&Time.realtimeSinceStartup<deadline)
            {InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;}
            InputSystem.QueueStateEvent(pad,new GamepadState());Assert.Greater(Vector3.Distance(start,actor.transform.position),10,"Must use actual player movement, not a teleported interception.");Assert.AreEqual(1,session.Reactions);Assert.AreNotEqual(PursuitState.Captured,session.State);
            deadline=Time.realtimeSinceStartup+3;while(session.Direction!= -1&&Time.realtimeSinceStartup<deadline)yield return null;Assert.AreEqual(-1,session.Direction);Capture("production-near-miss-reversal",((Transform)Field(host,"target")).position);
            deadline=Time.realtimeSinceStartup+10;
            while(session.State!=PursuitState.Captured&&Time.realtimeSinceStartup<deadline)
            {
                Vector3 direction=((Transform)Field(host,"target")).position-actor.transform.position;direction.y=0;if(direction.sqrMagnitude>.001f)camera.transform.rotation=Quaternion.LookRotation(direction);
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;
            }
            InputSystem.QueueStateEvent(pad,new GamepadState());Assert.AreEqual(PursuitState.Captured,session.State);Assert.AreEqual(QuestStatus.Completed,actor.Service.Status("demo-ground-quest"));Capture("production-capture",actor.transform.position);
        }
        [UnityTest] public IEnumerator HybridRouteUsesContinuousGroundAirAndLandingSegments()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));for(int i=0;i<20;i++)yield return null;
            var host=(Component)Object.FindObjectsByType(T("PursuitHost"),FindObjectsSortMode.None).First(h=>((PursuitProfile)Field(h,"profile")).definition.mode==PursuitMode.Hybrid);
            runtimeProfile=Object.Instantiate((PursuitProfile)Field(host,"profile"));runtimeProfile.definition.speed=30;runtimeProfile.definition.acceleration=1000;runtimeProfile.definition.startDelay=0;host.GetType().GetField("profile").SetValue(host,runtimeProfile);
            Assert.IsTrue(Begin(host));var session=(PursuitSession)Property(host,"Session");var target=(Transform)Field(host,"target");bool air=false,peak=false,landed=false;float priorY=target.position.y,priorTime=session.Elapsed;
            float deadline=Time.realtimeSinceStartup+15;
            while(!landed&&Time.realtimeSinceStartup<deadline)
            {
                yield return null;float elapsed=session.Elapsed-priorTime;Assert.LessOrEqual(Mathf.Abs(target.position.y-priorY),30*elapsed+.04f,"Ground/air boundary must not snap height.");priorY=target.position.y;priorTime=session.Elapsed;
                air|=session.Airborne;
                if(!peak&&target.position.y>1010){peak=true;Capture("hybrid-air-peak",target.position);}
                if(air&&peak&&!session.Airborne){landed=true;Capture("hybrid-ground-return",target.position);}
            }
            Assert.IsTrue(air&&peak&&landed,"Hybrid demo must ascend and return through the same pursuit host.");Assert.AreEqual(1000.6f,target.position.y,.03f);Assert.IsNull(Property(host,"Error"));
        }
        [UnityTest] public IEnumerator StartInteractionAndTargetLossUseProductionLifecycle()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));for(int i=0;i<20;i++)yield return null;
            var host=(Component)Object.FindObjectsByType(T("PursuitHost"),FindObjectsSortMode.None).First(h=>((PursuitProfile)Field(h,"profile")).definition.mode==PursuitMode.Ground);
            var actor=(ProgressionActor)Field(host,"actor");var start=(ProgressionInteraction)Object.FindObjectsByType(T("ActivityStartInteraction"),FindObjectsSortMode.None).First(x=>(Component)Field(x,"pursuit")==host);
            var interaction=actor.GetComponent<PlayerInteractionController>();Assert.IsNotNull(interaction);
            var controller=actor.GetComponent<CharacterController>();controller.enabled=false;actor.transform.position=start.transform.position+new Vector3(0,0,-2);controller.enabled=true;
            interaction.view.transform.position=start.transform.position+new Vector3(0,1,-7);interaction.view.transform.LookAt(start.transform);Physics.SyncTransforms();interaction.Probe();
            Assert.AreSame(start,interaction.Target,"Production camera ray must reach the pedestal while actor range remains enforced.");Assert.IsTrue(interaction.Target.TryUse(actor,InteractionChannel.Input));Assert.IsFalse(Begin(host),"An active attempt cannot create a duplicate session.");
            var target=(Transform)Field(host,"target");target.gameObject.SetActive(false);yield return null;
            Assert.AreEqual(PursuitState.Canceled,((PursuitSession)Property(host,"Session")).State);Assert.IsNull(Property(host,"ObjectiveAnchor"));
            Assert.IsTrue(Begin(host));Assert.IsTrue(target.gameObject.activeSelf);Object.Destroy(target.gameObject);yield return null;yield return null;
            Assert.AreEqual(PursuitState.Canceled,((PursuitSession)Property(host,"Session")).State);Assert.IsNull(Property(host,"ObjectiveAnchor"));
        }
        static void Capture(string name,Vector3 focus)
        {
            var camera=Camera.main;if(!camera)camera=Object.FindAnyObjectByType<Camera>();Assert.IsNotNull(camera);
            var prior=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var rt=new RenderTexture(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.transform.position=focus+new Vector3(6,5,-12);camera.transform.LookAt(focus);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();Directory.CreateDirectory("Logs/RelayPursuit");File.WriteAllBytes("Logs/RelayPursuit/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=prior;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
        }
    }
}
#endif
