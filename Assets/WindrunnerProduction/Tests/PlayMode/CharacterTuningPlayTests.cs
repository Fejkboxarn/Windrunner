using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Unity;
namespace Windrunner.Production.Tests
{
    public sealed class CharacterTuningPlayTests
    {
        [UnityTest] public IEnumerator RuntimeDraftPreviewIsIsolatedAndRestoresOriginalBindings()
        {
#if UNITY_EDITOR
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");yield return null;
            var type=Type.GetType("AirflowPrototype.PlayerMotor, Assembly-CSharp",true);
            var motor=(Component)UnityEngine.Object.FindAnyObjectByType(type);
            var settings=type.GetProperty("Settings");var original=(ScriptableObject)settings.GetValue(motor);
            string originalJson=UnityEditor.EditorJsonUtility.ToJson(original);
            var sessionType=Type.GetType("AirflowPrototype.Editor.CharacterTuningSession, Assembly-CSharp-Editor",true);
            var session=Activator.CreateInstance(sessionType,new object[]{motor.gameObject});
            var other=new GameObject("Unbound other character");other.SetActive(false);var otherMotor=other.AddComponent(type);
            type.GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(otherMotor,original);
            try
            {
                sessionType.GetMethod("Preview").Invoke(session,new object[]{true});
                var draft=(ScriptableObject)settings.GetValue(motor);
                Assert.That(draft,Is.Not.SameAs(original));Assert.That(settings.GetValue(otherMotor),Is.SameAs(original));
                var speed=draft.GetType().GetField("maxGroundSpeed");float baseline=(float)speed.GetValue(original);speed.SetValue(draft,baseline+1);
                for(int i=0;i<4;i++)yield return null;
                Assert.That((float)speed.GetValue(settings.GetValue(motor)),Is.EqualTo(baseline+1));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(original),Is.EqualTo(originalJson));
                var entries=(System.Collections.IEnumerable)sessionType.GetField("Entries").GetValue(session);
                foreach(var entry in entries)
                {
                    var animation=(ScriptableObject)entry.GetType().GetField("Draft").GetValue(entry);
                    if(animation.GetType().Name!="WindrunnerAnimationProfile")continue;
                    animation.GetType().GetField("motionSpeedMultiplier").SetValue(animation,1.25f);
                }
                var failure=Assert.Throws<System.Reflection.TargetInvocationException>(()=>sessionType.GetMethod("Save").Invoke(session,null));
                Assert.That(failure.InnerException,Is.TypeOf<InvalidOperationException>());
                var bridgeType=Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true);
                var bridge=motor.GetComponent(bridgeType);
                yield return null;
                Assert.That((bool)bridgeType.GetProperty("ProductionOnlyReady").GetValue(bridge),Is.True,"Rejected save must leave active graph intact.");
                sessionType.GetMethod("Revert").Invoke(session,null);
                Assert.That((float)speed.GetValue(draft),Is.EqualTo(baseline));
                var powerType=Type.GetType("AirflowPrototype.AirPower, Assembly-CSharp",true);var power=motor.GetComponent(powerType);
                powerType.GetMethod("Refill").Invoke(power,null);
                int capacityNotifications=0;Action<float,float> changed=(current,max)=>capacityNotifications++;
                powerType.GetEvent("Changed").AddEventHandler(power,changed);
                draft.GetType().GetField("maxAirPower").SetValue(draft,40f);
                yield return null;
                Assert.That((float)powerType.GetProperty("Current").GetValue(power),Is.EqualTo(40f));
                Assert.That(capacityNotifications,Is.EqualTo(1));
                sessionType.GetMethod("Revert").Invoke(session,null);
                Assert.That((float)powerType.GetProperty("Current").GetValue(power),Is.EqualTo(40f),"Capacity restoration never refunds energy.");
                Assert.That(capacityNotifications,Is.EqualTo(2));
                powerType.GetEvent("Changed").RemoveEventHandler(power,changed);
                powerType.GetMethod("Spend").Invoke(power,new object[]{40f});
                powerType.GetMethod("Restore").Invoke(power,new object[]{10f});
                Assert.That((bool)powerType.GetProperty("IsDepletedLocked").GetValue(power),Is.True);
                int recovered=0;Action onRecovered=()=>recovered++;
                powerType.GetEvent("RecoveredFromDepletion").AddEventHandler(power,onRecovered);
                draft.GetType().GetField("maxAirPower").SetValue(draft,50f);yield return null;
                Assert.That((bool)powerType.GetProperty("IsDepletedLocked").GetValue(power),Is.False,"Capacity change must reconcile the restart threshold.");
                Assert.That(recovered,Is.EqualTo(1));
                sessionType.GetMethod("Revert").Invoke(session,null);
                Assert.That((float)powerType.GetProperty("Current").GetValue(power),Is.EqualTo(10f));
                Assert.That(recovered,Is.EqualTo(1));powerType.GetEvent("RecoveredFromDepletion").RemoveEventHandler(power,onRecovered);

                // Undo restores an invalid historical draft; validation must stop preview immediately.
                UnityEditor.Undo.FlushUndoRecordObjects();UnityEditor.Undo.IncrementCurrentGroup();
                var pitch=draft.GetType().GetField("minCameraPitch");pitch.SetValue(draft,100f);
                UnityEditor.Undo.RegisterCompleteObjectUndo(draft,"Correct invalid draft");pitch.SetValue(draft,-28f);UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo();
                Assert.That((bool)sessionType.GetProperty("Previewing").GetValue(session),Is.False);
                Assert.That(settings.GetValue(motor),Is.SameAs(original));
                UnityEditor.Undo.ClearUndo(draft);
                sessionType.GetMethod("Revert").Invoke(session,null);
                sessionType.GetMethod("Preview").Invoke(session,new object[]{true});
                pitch.SetValue(draft,100f); // External edit, no GUI/Undo callback.
                float deadline=Time.realtimeSinceStartup+1f;
                while((bool)sessionType.GetProperty("Previewing").GetValue(session) && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That((bool)sessionType.GetProperty("Previewing").GetValue(session),Is.False,"Editor watcher must stop invalid external draft changes.");
                Assert.That(settings.GetValue(motor),Is.SameAs(original));

                sessionType.GetMethod("Preview").Invoke(session,new object[]{false});Assert.That(settings.GetValue(motor),Is.SameAs(original));
            }
            finally {((IDisposable)session).Dispose();UnityEngine.Object.Destroy(other);if(ProjectRoot.Active!=null)UnityEngine.Object.Destroy(ProjectRoot.Active.gameObject);}
            var empty=SceneManager.CreateScene("Tuning cleanup");SceneManager.SetActiveScene(empty);yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
#else
            Assert.Ignore("Editor-only authoring preview.");yield break;
#endif
        }
    }
}
