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
