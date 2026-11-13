using System;
using System.Collections;
using System.Reflection;
using Windrunner.Production.Animation;
using Windrunner.Production.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Unity;
namespace Windrunner.Production.Tests
{
    public sealed class ProductionOnlyTests
    {
        [UnityTest] public IEnumerator RecreatedGraphRecoversAuthoritativeSlingPhasesWithoutGameplayReplay()
        {
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");yield return null;
            var bridgeType=Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true);
            var slingType=Type.GetType("AirflowPrototype.AirSlingController, Assembly-CSharp",true);
            var bridge=(Behaviour)UnityEngine.Object.FindAnyObjectByType(bridgeType);
            var sling=bridge.GetComponent(slingType);
            var flow=slingType.GetProperty("FlowState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sling);
            var original=(WindrunnerAnimationProfile)bridgeType.GetProperty("Profile").GetValue(bridge);
            var profile=UnityEngine.Object.Instantiate(original);
            var actions=UnityEngine.Object.Instantiate(original.standaloneActionProfile);profile.standaloneActionProfile=actions;
            // Runtime-only use of an existing compatible clip tests recovery of
            // reserved charge slots without changing authored content.
            actions.Action(6).clip=actions.Action(1).clip;
            var profileField=bridgeType.GetField("profile",BindingFlags.NonPublic|BindingFlags.Instance);
            var powerType=Type.GetType("AirflowPrototype.AirPower, Assembly-CSharp",true);var power=bridge.GetComponent(powerType);
            float before=(float)powerType.GetProperty("Current").GetValue(power);
            try
            {
                using(var pause=UnityTimeAuthority.Pause())
                {
                    foreach(string phase in new[]{"Charging","DelayBeforeDash","DashingToNode","ImpactHold","Idle"})
                    {
                        flow.GetType().GetMethod("Reset").Invoke(flow,null);
                        if(phase=="Charging")flow.GetType().GetMethod("BeginCharge").Invoke(flow,null);
                        if(phase=="DelayBeforeDash")flow.GetType().GetMethod("BeginDelayBeforeDash").Invoke(flow,new object[]{1f});
                        if(phase=="DashingToNode")flow.GetType().GetMethod("BeginDash").Invoke(flow,new[]{slingType.GetProperty("Settings").GetValue(sling)});
                        if(phase=="ImpactHold"){flow.GetType().GetMethod("SetImpactHold").Invoke(flow,new object[]{1f});flow.GetType().GetMethod("EnterImpactHold").Invoke(flow,null);}
                        int observed=WindrunnerFeedbackHost.Active.Service.ObservedCount;
                        bridge.enabled=false;profileField.SetValue(bridge,profile);bridge.enabled=true;
                        var session=(WindrunnerAnimationSession)bridgeType.GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bridge);
                        string expected=phase=="DashingToNode"?"SlingTravel":phase=="ImpactHold"?"SlingImpact":phase=="Idle"?"None":"SlingCharge";
                        Assert.That(session.ActiveMontage,Is.EqualTo(expected),phase);
                        var frame=new WindrunnerMotionFrame(Time.frameCount,0.05f,Vector3.zero,0,0,0,0,0,true,false,phase!="Idle");session.Evaluate(in frame);
                        var graph=session.GetType().GetField("standaloneGraph",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(session);
                        var playback=(PortableAnimation.PortableActionPlayback)graph.GetType().GetField("playback",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(graph);
                        if(phase!="Idle")Assert.That(playback.Weight(playback.ActiveIndex),Is.GreaterThan(0f),"Recovered action must contribute to the live graph.");
                        if(phase=="DashingToNode")
                        {
                            session.RecoverSustainedActions(FeedbackKey.None,false,false);
                            Assert.That(session.ActiveMontage,Is.EqualTo("None"),"A recovery snapshot must clear a missed sustained-action end.");
                        }
                        Assert.That(WindrunnerFeedbackHost.Active.Service.ObservedCount,Is.EqualTo(observed),"Recovery must not publish semantic/gameplay events.");
                        Assert.That((float)powerType.GetProperty("Current").GetValue(power),Is.EqualTo(before));
                        Assert.That(slingType.GetProperty("PresentationPhase").GetValue(sling).ToString(),Is.EqualTo(phase));
                        yield return null;
                    }
                    var casterType=Type.GetType("AirflowPrototype.AirCaster, Assembly-CSharp",true);
                    var dashType=Type.GetType("AirflowPrototype.AirDash, Assembly-CSharp",true);
                    var caster=bridge.GetComponent(casterType);var dash=bridge.GetComponent(dashType);
                    var castState=casterType.GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(caster);
                    var dashState=dashType.GetProperty("Session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(dash);
                    actions.Action(3).clip=actions.Action(1).clip;actions.Action(5).clip=actions.Action(1).clip;
                    foreach(bool casting in new[]{true,false})
                    {
                        if(casting)castState.GetType().GetMethod("Begin").Invoke(castState,new object[]{null});
                        else dashState.GetType().GetMethod("SetDashing").Invoke(dashState,new object[]{true});
                        int observed=WindrunnerFeedbackHost.Active.Service.ObservedCount;
                        bridge.enabled=false;bridge.enabled=true;
                        var session=(WindrunnerAnimationSession)bridgeType.GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bridge);
                        Assert.That(session.ActiveMontage,Is.EqualTo(casting?"CastCharge":"AirDash"));
                        Assert.That(WindrunnerFeedbackHost.Active.Service.ObservedCount,Is.EqualTo(observed));
                        Assert.That((float)powerType.GetProperty("Current").GetValue(power),Is.EqualTo(before));
                        castState.GetType().GetMethod("Cancel").Invoke(castState,null);dashState.GetType().GetMethod("SetDashing").Invoke(dashState,new object[]{false});
                    }
                }
            }
            finally
            {
                flow.GetType().GetMethod("Reset").Invoke(flow,null);bridge.enabled=false;profileField.SetValue(bridge,original);
                UnityEngine.Object.Destroy(actions);UnityEngine.Object.Destroy(profile);
                if(ProjectRoot.Active!=null)UnityEngine.Object.Destroy(ProjectRoot.Active.gameObject);
            }
            var empty=SceneManager.CreateScene("Phase recovery cleanup");SceneManager.SetActiveScene(empty);yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
        }

        [UnityTest] public IEnumerator PortableGraphOperatesWithLegacyWritersPhysicallyAbsent()
        {
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");yield return null;
            var bridgeType=Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true);
            var bridge=(Behaviour)UnityEngine.Object.FindAnyObjectByType(bridgeType);
            try
            {
                Assert.That((bool)bridgeType.GetProperty("ProductionOnlyReady").GetValue(bridge),Is.True);
                foreach(string name in new[]{"PlayerHumanoidAnimatorDriver","PlayerCustomAnimationDriver","PlayerRunningArmPoseController","PlayerProceduralKnifeFlick","PlayerDualThrowPose","PlayerGroundedFootIK","PlayerInputStartupRepair","AirCastFeedback","AirTargetIndicator","AirChargeVisual"})
                    Assert.That(Type.GetType("AirflowPrototype."+name+", Assembly-CSharp",false),Is.Null,name+" source type must be retired, not merely disabled.");
                bridgeType.GetMethod("StopPortable").Invoke(bridge,null);
                Assert.That((bool)bridgeType.GetMethod("TryStartPortable").Invoke(bridge,null),Is.True);
                var animator=(Animator)bridgeType.GetProperty("HumanoidAnimator").GetValue(bridge);
                for(int i=0;i<30;i++)yield return null;
                Assert.That(animator.runtimeAnimatorController,Is.Null);
                Assert.That(animator.applyRootMotion,Is.False);
                Assert.That((bool)bridgeType.GetProperty("ProductionOnlyReady").GetValue(bridge),Is.True);
                bridge.enabled=false;yield return null;
                Assert.That(animator.runtimeAnimatorController,Is.Null,"Disabling production graph must not activate a hidden legacy controller.");
                bridge.enabled=true;yield return null;
                Assert.That((bool)bridgeType.GetProperty("ProductionOnlyReady").GetValue(bridge),Is.True);
            }
            finally {if(ProjectRoot.Active!=null)UnityEngine.Object.Destroy(ProjectRoot.Active.gameObject);}
            var empty=SceneManager.CreateScene("Production-only cleanup");SceneManager.SetActiveScene(empty);yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
        }
    }
}
