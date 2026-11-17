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
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;
using Object=UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed partial class MainMenuFlowTests
    {
        static void Approach(ProgressionActor actor, Transform station)
        {
            var body=actor.GetComponent<CharacterController>();
            body.enabled=false;
            var p=station.position-Vector3.forward*3;
            p.y=Terrain.activeTerrain.SampleHeight(p)+.1f;
            actor.transform.position=p;
            body.enabled=true;Physics.SyncTransforms();
        }
        static void StrikeCallback(Component adapter)
        {
            var type=adapter.GetType();
            type.GetMethod("Impact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(adapter,new[]{type.GetField("node").GetValue(adapter)});
        }
        [UnityTest, Category("Profiling")] public IEnumerator RenderedZoneArrival()
        {
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(World,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var actor=Object.FindAnyObjectByType<ProgressionActor>();
            var camera=actor.GetComponent<PlayerInteractionController>().view;
            for(int i=0;i<12;i++)yield return null;
            Assert.IsTrue(camera.isActiveAndEnabled);Assert.IsNull(Prop<string>(Session(),"SlotId"));
            Assert.AreSame(actor.Service,Prop<ProgressionService>(Session(),"Progression"));
            var gameView=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show();gameView.Focus();gameView.Repaint();yield return null;
            string image=Path.GetFullPath("Logs/ProductionMigration/zone-arrival-"+DateTime.UtcNow.Ticks+".png");
            ScreenCapture.CaptureScreenshot(image);
            for(int i=0;i<150 && !File.Exists(image);i++){gameView.Repaint();yield return null;}
            Assert.IsTrue(File.Exists(image));Debug.Log("Zone arrival capture "+image);
            Object.Destroy(ProjectRoot.Active.gameObject);yield return null;
            var previous=SceneManager.GetActiveScene();var empty=SceneManager.CreateScene("Zone visual teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(previous);
        }

        [UnityTest] public IEnumerator ZoneChaptersPersistWhileInterruptedDeliveryRestartsAtSource()
        {
            const string hunt="basin-hunt", delivery="ridge-retrieval";
            float capture=Time.captureDeltaTime;
            string temp=Path.Combine(Path.GetTempPath(),"Windrunner-zone-"+Guid.NewGuid().ToString("N"));
            if(ProjectRoot.Active){Object.Destroy(ProjectRoot.Active.gameObject);yield return null;}
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Title,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                var session=Session();Call(session,"UseTestDirectory",temp);
                Submit("New game");Submit("Start a new playthrough");yield return Changed(World,session);
                Time.captureDeltaTime=.1f;
                var npc=Object.FindAnyObjectByType<RelationshipContact>();var actor=npc.owner;
                var item=npc.retrieval;var service=actor.Service;var film=npc.cinematics;
                var journal=Object.FindAnyObjectByType<ProgressionJournal>();
                Assert.IsTrue(journal.SetOpen(true));
                Assert.IsTrue(actor.Input.IsGameplayBlocked);
                Canvas.ForceUpdateCanvases();
                foreach(var reticleName in new[]{"AirTargetChargeUI","AirSlingTargetChargeUI"})
                {
                    var type=Type.GetType("AirflowPrototype."+reticleName+", Assembly-CSharp",true);
                    var reticle=Object.FindAnyObjectByType(type);
                    var group=(CanvasGroup)type.GetField("_canvasGroup",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(reticle);
                    Assert.AreEqual(0f,group.alpha,reticleName+" over Journal");
                }
                Assert.IsTrue(journal.SetOpen(false));
                Assert.IsFalse(item.TryUse(actor,InteractionChannel.Input));Assert.IsFalse(item.Carrying);
                Approach(actor,npc.transform);for(int i=0;i<8;i++)yield return null;
                var interaction=actor.GetComponent<PlayerInteractionController>();var view=interaction.view.transform;
                view.position=actor.Position+Vector3.up;view.LookAt(npc.transform.position);
                interaction.Probe();Assert.AreSame(npc,interaction.Target);Assert.That(interaction.Prompt,Does.Contain("Accept"));
                Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));
                Assert.AreEqual(hunt,service.Snapshot().trackedQuestId);
                var enemies=Object.FindObjectsByType(Type.GetType("AirflowPrototype.EnemyProgressionAdapter, Assembly-CSharp",true)).Cast<Component>().ToArray();
                Assert.AreEqual(3,enemies.Length);
                int Count()=>service.Snapshot().quests.Single(q=>q.id==hunt).objectives.Single().count;
                foreach(var enemy in enemies)
                {
                    var t=enemy.GetType();var health=(Component)t.GetField("health").GetValue(enemy);
                    var saved=enemy.transform.position;int before=Count();
                    var sling=(Component)t.GetField("sling").GetValue(enemy);
                    var damage=sling.GetType().GetMethod("DamageTarget",BindingFlags.Instance|BindingFlags.NonPublic);
                    var node=t.GetField("node").GetValue(enemy);
                    void Hit()=>damage.Invoke(sling,new[]{node,(object)10});
                    void Respawn()=>health.GetType().GetMethod("Respawn",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(health,null);
                    Call(health,"TakeDamage",10);Hit();Assert.AreEqual(before,Count(),"Foreign death cannot be claimed.");
                    Respawn();enemy.transform.position+=Vector3.right*200;Hit();Assert.AreEqual(before,Count());
                    Respawn();enemy.transform.position=saved;var identity=t.GetField("enemyDefinitionId");var valid=identity.GetValue(enemy);
                    identity.SetValue(enemy,"other");Hit();Assert.AreEqual(before,Count());
                    Respawn();identity.SetValue(enemy,valid);Hit();StrikeCallback(enemy);Assert.AreEqual(before+1,Count());
                }
                Assert.AreEqual(QuestStatus.ReadyToTurnIn,service.Status(hunt));
                Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));
                Assert.AreEqual(1,service.Snapshot().relationships.Single().rank);
                Assert.AreEqual(QuestStatus.Locked,service.Status(delivery));
                Assert.IsTrue(film.IsPlaying);Assert.IsFalse(film.gameplayCamera.enabled);
                Assert.IsTrue(film.Skip());Assert.IsTrue(film.gameplayCamera.enabled);
                Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));
                Assert.AreEqual(QuestStatus.Active,service.Status(delivery));Assert.IsFalse(item.Carrying);
                Approach(actor,item.transform);Assert.IsTrue(item.TryUse(actor,InteractionChannel.Input));
                Assert.IsFalse(item.TryUse(actor,InteractionChannel.Input));Assert.IsFalse(item.itemModel.activeSelf);
                using(var slow=UnityTimeAuthority.Slowdown(.25f))
                {
                    yield return null;double remaining=item.Remaining;
                    using(var pause=UnityTimeAuthority.Pause())
                    {
                        yield return null;remaining=item.Remaining;yield return null;Assert.AreEqual(remaining,item.Remaining,.001);
                    }
                    yield return null;Assert.Less(item.Remaining,remaining);
                    Assert.Less(remaining-item.Remaining,.08);
                }
                Assert.IsFalse(item.Deliver(actor,npc),"Out of range");
                session=Session();string id=Prop<string>(session,"SlotId");Call(session,"Save");Call(session,"ReturnToTitle");yield return Changed(Title,session);
                Assert.IsTrue(item==null,"Unloading the world must destroy the carried attempt owner.");
                session=Session();Call(session,"Load",id);yield return Changed(World,session);
                npc=Object.FindAnyObjectByType<RelationshipContact>();actor=npc.owner;item=npc.retrieval;service=actor.Service;
                Assert.AreEqual(QuestStatus.Completed,service.Status(hunt));Assert.AreEqual(QuestStatus.Active,service.Status(delivery));
                Assert.IsFalse(item.Carrying);Assert.IsTrue(item.itemModel.activeSelf);Assert.AreEqual(1,service.Snapshot().rewards.Count);
                Approach(actor,item.transform);Assert.IsTrue(item.TryUse(actor,InteractionChannel.Input));
                double deadline=Time.timeAsDouble+item.Remaining;
                while(Time.timeAsDouble < deadline+.1)yield return null;
                Assert.IsFalse(item.Carrying);Assert.IsFalse(item.itemModel.activeSelf);Assert.AreEqual(QuestStatus.Active,service.Status(delivery));
                Assert.AreEqual(0,item.Remaining);Assert.IsFalse(item.Deliver(actor,npc));
                Assert.AreEqual(1,service.Snapshot().rewards.Count,"Expiry must not grant delivery credit.");
                Assert.IsFalse(item.TryUse(actor,InteractionChannel.Input));
                yield return new WaitForSeconds(item.respawnSeconds+.25f);
                Assert.IsTrue(item.itemModel.activeSelf);Assert.IsTrue(item.TryUse(actor,InteractionChannel.Input));
                Approach(actor,npc.transform);for(int i=0;i<8;i++)yield return null;
                Assert.IsTrue(npc.TryUse(actor,InteractionChannel.Input));Assert.IsFalse(item.Carrying);Assert.AreEqual(0,item.Remaining);
                Assert.IsFalse(item.Deliver(actor,npc));Assert.AreEqual(QuestStatus.Completed,service.Status(delivery));
                Assert.IsTrue(npc.cinematics.IsPlaying);
                float timeout=Time.realtimeSinceStartup+10;
                while(npc.cinematics.IsPlaying && Time.realtimeSinceStartup<timeout)yield return null;
                Assert.AreEqual(CinematicStatus.Watched,service.Snapshot().cutscenes.Single(c=>c.id=="ridge-retrieval-milestone").status);
                Assert.IsTrue(npc.cinematics.gameplayCamera.enabled);
                Assert.IsTrue(npc.cinematics.Play("ridge-retrieval-milestone",true));Assert.IsTrue(npc.cinematics.Skip());
                Assert.AreEqual(2,service.Snapshot().relationships.Single().rank);Assert.AreEqual(2,service.Snapshot().rewards.Count);
                Assert.AreEqual(2,service.Snapshot().cutscenes.Count);
                session=Session();Call(session,"Load",id);yield return Changed(World,session);
                Assert.AreEqual(2,Prop<ProgressionService>(Session(),"Progression").Snapshot().rewards.Count);
            }
            finally
            {
                Time.captureDeltaTime=capture;
                if(ProjectRoot.Active)Object.Destroy(ProjectRoot.Active.gameObject);
                if(Directory.Exists(temp))Directory.Delete(temp,true);
            }
            yield return null;
            var previous=SceneManager.GetActiveScene();var empty=SceneManager.CreateScene("Zone teardown");SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(previous);
        }
    }
}
#endif
