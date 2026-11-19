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
using Windrunner.Production.Progression;
using Windrunner.Production.Progression.Activities;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;
namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        static Type ActivityType(string name)=>Type.GetType("Windrunner.Activities."+name+", Assembly-CSharp",true);
        static object Value(object o,string field)=>o.GetType().GetField(field).GetValue(o);
        static Component Stage(string objective)=>(Component)Object.FindObjectsByType(ActivityType("ZoneQuestActivity"),FindObjectsSortMode.None).Single(b=>(string)Value(b,"objectiveId")==objective);
        static PursuitSession Pursuit(Component host)=>(PursuitSession)host.GetType().GetProperty("Session").GetValue(host);
        static WindRelaySession Relay(Component host)=>(WindRelaySession)host.GetType().GetProperty("Session").GetValue(host);
        static Camera FixedCamera(ProgressionActor actor)
        {
            var camera=actor.GetComponent<PlayerInteractionController>().view;var orbit=camera.GetComponent(Type.GetType("AirflowPrototype.PlayerOrbitCamera, Assembly-CSharp",true)) as Behaviour;if(orbit)orbit.enabled=false;return camera;
        }
        static void Place(ProgressionActor actor,Vector3 p)
        {var cc=actor.GetComponent<CharacterController>();cc.enabled=false;p.y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y+.12f;actor.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
        static void Photo(string name,Vector3 focus)
        {
            var camera=Object.FindAnyObjectByType<ProgressionActor>().GetComponent<PlayerInteractionController>().view;var pos=camera.transform.position;var rot=camera.transform.rotation;var prior=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(960,540,24);var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.transform.position=focus+new Vector3(7,5,-12);camera.transform.LookAt(focus);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();Directory.CreateDirectory("Logs/ZoneActivities");File.WriteAllBytes("Logs/ZoneActivities/"+name+".png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;camera.transform.SetPositionAndRotation(pos,rot);rt.Release();Object.Destroy(rt);Object.Destroy(pixels);}
        }
        static IEnumerator CastAt(ProgressionActor actor,Component node,Gamepad pad,Func<bool> completed)
        {
            var camera=FixedCamera(actor);Vector3 approach=node.transform.position-Vector3.forward*12;
            if(node.name.StartsWith("Orange")){var host=(Component)Value(Stage("air"),"pursuit");var direction=host.transform.TransformDirection(Pursuit(host).Tangent);direction.y=0;approach=node.transform.position+direction.normalized*3;}
            Place(actor,approach);InputSystem.QueueStateEvent(pad,new GamepadState());
            for(int i=0;i<16;i++){camera.transform.position=actor.Position+new Vector3(0,2,-4);camera.transform.LookAt(node.transform.position);yield return null;}
            var targeter=actor.GetComponent(Type.GetType("AirflowPrototype.AirTargeter, Assembly-CSharp",true));Assert.AreSame(node,targeter.GetType().GetProperty("CurrentTarget").GetValue(targeter),"Actual production target acquisition.");
            InputSystem.QueueStateEvent(pad,new GamepadState{leftTrigger=1});for(int i=0;i<24;i++){camera.transform.LookAt(node.transform.position);yield return null;}
            InputSystem.QueueStateEvent(pad,new GamepadState());float deadline=Time.realtimeSinceStartup+8;while(!completed()&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(completed(),"Real Cast projectile must credit the configured activity.");
        }
        static IEnumerator TurnInAndResolve(RelationshipContact npc)
        {
            Approach(npc.owner,npc.transform);for(int i=0;i<40;i++)yield return null;Assert.IsTrue(npc.TryUse(npc.owner,InteractionChannel.Input));
            float deadline=Time.realtimeSinceStartup+5;while(!npc.cinematics.IsPlaying&&Time.realtimeSinceStartup<deadline){yield return null;npc.TryUse(npc.owner,InteractionChannel.Input);}
            Assert.IsTrue(npc.cinematics.IsPlaying,"Turn-in milestone must use existing cinematic lifecycle.");Assert.IsTrue(npc.cinematics.IsPlaceholderPlaying);yield return ZoneUIFrame("placeholder-warning");Assert.IsTrue(npc.cinematics.Skip());Assert.IsFalse(npc.cinematics.IsPlaceholderPlaying);yield return null;
        }
        [UnityTest] public IEnumerator FourZoneQuestsUseNpcAbilitiesRetriesAndRealSaveReload()
        {
            Assert.AreEqual("0.5.0",Application.version,"Integrated development version");
            bool oldGuide=WindrunnerUITheme.ObjectiveWayfinder;WindrunnerUITheme.ObjectiveWayfinder=true;
            string temp=Path.Combine(Path.GetTempPath(),"Windrunner-zone-activities-"+Guid.NewGuid().ToString("N"));var original=InputSystem.settings;var flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
            var settings=Object.Instantiate(original);settings.hideFlags=HideFlags.DontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=settings;var pad=InputSystem.AddDevice<Gamepad>();float dt=Time.captureDeltaTime;
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;var session=Session();Call(session,"UseTestDirectory",temp);Submit("Settings");Submit("Back");yield return ZoneUIFrame("01-main");Submit("New game");Submit("Start a new playthrough");yield return Changed(World,session);Time.captureDeltaTime=.02f;
                var npc=Object.FindAnyObjectByType<RelationshipContact>();var actor=npc.owner;var service=actor.Service;string slot=Prop<string>(Session(),"SlotId");Assert.IsNotNull(slot);
                Assert.IsFalse((bool)Call(Stage("air"),"RestartEncounter"));Assert.AreEqual(QuestStatus.Locked,service.Status("zone-wind-relay"));Approach(actor,npc.transform);yield return null;Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));
                foreach(Component enemy in Object.FindObjectsByType(Type.GetType("AirflowPrototype.EnemyProgressionAdapter, Assembly-CSharp",true),FindObjectsSortMode.None))
                {var sling=(Component)Value(enemy,"sling");sling.GetType().GetMethod("DamageTarget",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(sling,new[]{Value(enemy,"node"),(object)10});StrikeCallback(enemy);}
                Assert.AreEqual(QuestStatus.ReadyToTurnIn,service.Status("basin-hunt"));yield return TurnInAndResolve(npc);Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));
                Approach(actor,npc.retrieval.transform);Assert.IsTrue(npc.retrieval.TryUse(actor,InteractionChannel.Input));yield return TurnInAndResolve(npc);Assert.AreEqual(2,service.Snapshot().relationships.Single().rank);
                Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));Assert.AreEqual("zone-wind-relay",service.Snapshot().trackedQuestId);yield return null;yield return null;
                var relay=(Component)Value(Stage("relay"),"relay");Assert.AreEqual(ActivityState.Active,Relay(relay).State);var nodes=(Array)Value(relay,"nodes");
                Photo("relay-available",((Component)nodes.GetValue(0)).transform.position);yield return AssertZoneGuide("relay");yield return ZoneUIFrame("07-relay-guidance");yield return ZoneMenuFrames();
                using(actor.Input.BlockGameplay()){float elapsed=Relay(relay).Elapsed;for(int i=0;i<5;i++)yield return null;Assert.AreEqual(elapsed,Relay(relay).Elapsed);}
                yield return CastAt(actor,(Component)Value(nodes.GetValue(0),"castNode"),pad,()=>Relay(relay).NodeState(0)==RelayState.Activated);
                Call(Session(),"Save");session=Session();Call(session,"Load",slot);yield return Changed(World,session);npc=Object.FindAnyObjectByType<RelationshipContact>();actor=npc.owner;service=actor.Service;yield return null;
                relay=(Component)Value(Stage("relay"),"relay");Assert.AreEqual(RelayState.Available,Relay(relay).NodeState(0),"Mid-relay restore restarts only the attempt.");
                Time.captureDeltaTime=.25f;for(int i=0;i<180&&Relay(relay).State==ActivityState.Active;i++)yield return null;Time.captureDeltaTime=.02f;Assert.AreEqual(ActivityState.Failed,Relay(relay).State);Photo("relay-expired",relay.transform.position);Assert.IsTrue((bool)Call(Stage("relay"),"RestartEncounter"));yield return AssertZoneGuide("relay");
                nodes=(Array)Value(relay,"nodes");for(int i=0;i<nodes.Length;i++){int index=i;yield return CastAt(actor,(Component)Value(nodes.GetValue(i),"castNode"),pad,()=>Relay(relay).NodeState(index)==RelayState.Activated||Relay(relay).State==ActivityState.Completed);if(i+1<nodes.Length){yield return AssertZoneGuide("relay");Assert.AreSame(((Component)nodes.GetValue(i+1)).transform,Object.FindAnyObjectByType<ProgressionJournal>().Wayfinder.Target);}}
                Assert.AreEqual(QuestStatus.ReadyToTurnIn,service.Status("zone-wind-relay"));Assert.AreEqual(2,service.Snapshot().relationships.Single().rank);Photo("relay-completed",actor.Position);yield return TurnInAndResolve(npc);
                Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));yield return null;yield return null;var ground=(Component)Value(Stage("ground"),"pursuit");var air=(Component)Value(Stage("air"),"pursuit");Assert.IsFalse((bool)Call(air,"Begin"));Assert.IsNull(Pursuit(air));
                while(Pursuit(ground).State==PursuitState.Waiting)yield return null;
                var target=(Transform)Value(ground,"target");var camera=FixedCamera(actor);Place(actor,target.position+new Vector3(2.6f,0,-8));camera.transform.rotation=Quaternion.identity;
                float deadline=Time.realtimeSinceStartup+18;while(Pursuit(ground).Reactions==0&&Time.realtimeSinceStartup<deadline){InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;}InputSystem.QueueStateEvent(pad,new GamepadState());Assert.Greater(Pursuit(ground).Reactions,0,$"Near miss: player={actor.Position}, target={target.position}, state={Pursuit(ground).State}, dt={Time.deltaTime}");Assert.AreNotEqual(PursuitState.Captured,Pursuit(ground).State);
                deadline=Time.realtimeSinceStartup+4;while(Pursuit(ground).Direction!=-1&&Time.realtimeSinceStartup<deadline)yield return null;Assert.AreEqual(-1,Pursuit(ground).Direction);Photo("ground-near-miss",target.position);yield return AssertZoneGuide("ground");yield return ZoneUIFrame("08-ground-tracking");
                deadline=Time.realtimeSinceStartup+15;while(Pursuit(ground).State!=PursuitState.Captured&&Time.realtimeSinceStartup<deadline){Vector3 direction=target.position-actor.Position;direction.y=0;if(direction.sqrMagnitude>.001f)camera.transform.rotation=Quaternion.LookRotation(direction);InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;}InputSystem.QueueStateEvent(pad,new GamepadState());Assert.AreEqual(PursuitState.Captured,Pursuit(ground).State);Photo("ground-captured",actor.Position);yield return null;
                Assert.IsTrue(service.ObjectiveOpen("zone-pursuit","air"));Call(Session(),"Save");session=Session();Call(session,"Load",slot);yield return Changed(World,session);npc=Object.FindAnyObjectByType<RelationshipContact>();actor=npc.owner;service=actor.Service;yield return null;ground=(Component)Value(Stage("ground"),"pursuit");air=(Component)Value(Stage("air"),"pursuit");Assert.IsFalse(((Transform)Value(ground,"target")).gameObject.activeSelf);Assert.IsFalse((bool)Call(Stage("ground"),"RestartEncounter"));Assert.IsNotNull(Pursuit(air));
                Assert.IsFalse((bool)Call(air,"Capture",ActivationMethod.Proximity,true));Pursuit(air).Tick(151,Vector3.zero);Assert.AreEqual(PursuitState.Escaped,Pursuit(air).State);Assert.IsTrue((bool)Call(Stage("air"),"RestartEncounter"));while(Pursuit(air).State==PursuitState.Waiting)yield return null;
                var flying=(Transform)Value(air,"target");Vector3 flightStart=flying.position;for(int i=0;i<100;i++)yield return null;Assert.Greater(Vector3.Distance(flightStart,flying.position),1);Assert.Greater(Mathf.Abs(flightStart.y-flying.position.y),.05f);
                Photo("air-active",flying.position);yield return AssertZoneGuide("air");yield return ZoneUIFrame("09-air-tracking");
                yield return CastAt(actor,(Component)Value(air,"castNode"),pad,()=>Pursuit(air).State==PursuitState.Captured);Assert.AreEqual(QuestStatus.ReadyToTurnIn,service.Status("zone-pursuit"));Assert.AreEqual(3,service.Snapshot().relationships.Single().rank);yield return null;Assert.AreSame(npc.transform,Object.FindAnyObjectByType<ProgressionJournal>().Wayfinder.Target);yield return TurnInAndResolve(npc);
                Assert.AreEqual(4,service.Snapshot().relationships.Single().rank);Assert.AreEqual(2,service.Snapshot().rewards.Count);Assert.AreEqual(4,service.Snapshot().quests.Count(q=>q.status==QuestStatus.Completed));Assert.IsFalse(service.TurnIn("zone-pursuit"));Photo("four-quests-complete",npc.transform.position);yield return ZoneUIFrame("10-completed-hud");if(zoneCaptureView){var journal=Object.FindAnyObjectByType<ProgressionJournal>();Assert.IsTrue(journal.SetOpen(true));yield return ZoneUIFrame("11-four-quest-journal");journal.SetOpen(false);}
                Call(Session(),"Save");session=Session();Call(session,"Load",slot);yield return Changed(World,session);yield return null;service=Prop<ProgressionService>(Session(),"Progression");Assert.AreEqual(4,service.Snapshot().relationships.Single().rank);Assert.AreEqual(4,service.Snapshot().quests.Count(q=>q.status==QuestStatus.Completed));
                foreach(string phase in new[]{"relay","ground","air"})Assert.IsFalse((bool)Call(Stage(phase),"RestartEncounter"));Assert.AreEqual(3,service.Snapshot().challenges.Count(c=>c.completed&&c.successfulAttempts.Count==1));
            }
            finally{WindrunnerUITheme.ObjectiveWayfinder=oldGuide;Time.captureDeltaTime=dt;if(pad.added)InputSystem.RemoveDevice(pad);InputSystem.settings=original;original.hideFlags=flags;Object.Destroy(settings);if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);}
            yield return null;var current=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Zone activities cleanup"));yield return SceneManager.UnloadSceneAsync(current);if(Directory.Exists(temp))Directory.Delete(temp,true);
        }
    }
}
#endif
