using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Linq;
namespace Windrunner.MovementLab.Tests
{
    public sealed class LabRuleTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void OneImpulsePerVisitAndNoStationaryOrPausedBoost(int fps)
        {
            var s=new WindSettings {mode=WindMode.EntryImpulse,strength=35,rampSeconds=.25f};
            var contact=new WindContact();
            float dt=1f/fps;
            Assert.AreEqual(Vector3.zero,contact.Sample(s,Vector3.zero,false,1,dt));
            Assert.AreEqual(Vector3.zero,contact.Sample(s,Vector3.down*6,true,1,dt));
            Assert.IsFalse(contact.Eligible,"Ground stick velocity must not count as moving entry.");
            var velocity = new Vector3(-5,3,8);
            Assert.AreEqual(Vector3.zero, contact.Sample(s,velocity,false,1,dt),"An invalid impulse entry must exit before rearming.");
            contact.Sample(s,velocity,false,-1,dt);
            float sum=0;
            for(int i=0;i<fps;i++)
            {
                var boost=contact.Sample(s,velocity,false,1,dt);
                Assert.That(Vector3.Cross(boost,velocity).magnitude,Is.LessThan(.001f));
                sum+=boost.magnitude;
            }
            Assert.AreEqual(35,sum,.001);
            Assert.AreEqual(Vector3.zero,contact.Sample(s,velocity,false,1,dt));
            contact.Sample(s,velocity,false,-1,dt);
            Assert.AreEqual(Vector3.zero,contact.Sample(s,velocity,false,1,0));
            Assert.Greater(contact.Sample(s,velocity,false,1,dt).magnitude,0);
        }
        [Test] public void WindFollowsTurnsAndFallDirectionAndCapsOnlyAddedSpeed()
        {
            var settings=new WindSettings {strength=60, speedCap=40,rampSeconds=0};
            var c=new WindContact();
            Vector3 delta=c.Sample(settings,Vector3.right*39,false,1,.1f);
            Assert.AreEqual(Vector3.right,delta);
            Assert.AreEqual(delta,LabVelocity.Strongest(delta,delta));
            Assert.Less(c.Sample(settings,Vector3.down*10,false,1,.1f).y,0);
            Assert.AreEqual(0,c.Sample(settings,new Vector3(5,-4,0),true,1,.1f).y);
            Assert.AreEqual(Vector3.zero,c.Sample(settings,Vector3.left*55,false,1,.1f));
            Assert.AreEqual(Vector3.zero,c.Sample(settings,Vector3.right*10,false,-1,.1f));
        }
        [Test] public void OverdriveChargesOnlyPaidTimeAndRequiresReleaseAfterInterruptionOrDepletion()
        {
            var s=new OverdriveSettings {drain=10,activationPower=1,rampSeconds=0,acceleration=50};
            var session=new OverdriveSession();
            Assert.AreEqual(0,session.Request(s,false,true,40,.1f));
            Assert.AreEqual(0,session.Request(s,true,true,.5f,.1f));
            Assert.AreEqual(1,session.Request(s,true,true,1,.2f));
            Assert.AreEqual(5,session.Apply(s,Vector3.forward*10,true,1,false).z,.001);
            Assert.IsFalse(session.Active);
            Assert.AreEqual(0,session.Request(s,true,true,40,.2f));
            session.Request(s,false,true,40,.2f);
            Assert.AreEqual(0,session.Request(s,true,true,40,0));
            Assert.AreEqual(2,session.Request(s,true,true,40,.2f));
            session.Interrupt(); Assert.AreEqual(0,session.Request(s,true,true,40,.2f));
        }
        [Test] public void EveryWindPresetHasAProgressiveCurveAndOneImmediateRearmingImpulse()
        {
            var profile=System.Type.GetType("Windrunner.MovementLab.MovementLabProfile, Assembly-CSharp",true);
            var preset=profile.GetMethod("Preset");
            float previousAcceleration=0, previousImpulse=0;
            foreach(var level in System.Enum.GetValues(preset.GetParameters()[1].ParameterType))
            {
                var continuous=(WindSettings)preset.Invoke(null,new object[]{false,level});
                var burst=(WindSettings)preset.Invoke(null,new object[]{true,level});
                Assert.Greater(continuous.strength,previousAcceleration);Assert.Greater(burst.strength,previousImpulse);
                previousAcceleration=continuous.strength;previousImpulse=burst.strength;
                Vector3 velocity=Vector3.forward*8;
                var c=new WindContact();var b=new WindContact();
                float first=c.Sample(continuous,velocity,true,1,.05f).z;
                Assert.That(first,Is.InRange(0.001f,.2f));
                Assert.Greater(c.Sample(continuous,velocity,true,1,.05f).z,first);
                Assert.AreEqual(burst.strength,b.Sample(burst,velocity,true,1,.05f).z,.001f);
                Assert.AreEqual(0,b.Sample(burst,velocity,true,1,.05f).z);
                b.Sample(burst,velocity,true,-1,.01f);
                Assert.AreEqual(0,b.Sample(burst,velocity,true,1,.01f).z);
                b.Sample(burst,velocity,true,-1,burst.rearmSeconds+.01f);
                Assert.AreEqual(burst.strength,b.Sample(burst,velocity,true,1,.05f).z,.001f);
            }
        }
        [Test] public void SerializedGymReferencesAndUniqueProductionPlayer()
        {

            var scene=EditorSceneManager.OpenScene("Assets/MovementLab/MovementGym.unity",OpenSceneMode.Additive);
            try
            {
                var components=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                Assert.AreEqual(1,components.Count(c=>c!=null && c.GetType().Name=="PlayerMotor"));
                Assert.AreEqual(10,components.Count(c=>c!=null && c.GetType().Name=="GymLane"));
                foreach(var root in scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreEqual(0,GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),t.name);
                    foreach(var c in t.GetComponents<Component>())
                    {
                        using var so=new SerializedObject(c);var property=so.GetIterator();
                        while(property.Next(true))
                            if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceEntityIdValue!=default)
                                Assert.IsNotNull(property.objectReferenceValue,t.name+"/"+property.propertyPath);
                    }
                }
                foreach(var c in components.Where(c=>c!=null && c.GetType().Namespace=="Windrunner.MovementLab"))
                {
                    using var so=new SerializedObject(c);var it=so.GetIterator();
                    while(it.Next(true)) if(it.propertyType==SerializedPropertyType.ObjectReference && !it.propertyPath.StartsWith("m_"))
                        Assert.IsNotNull(it.objectReferenceValue,c.name+"/"+it.propertyPath);
                }
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
