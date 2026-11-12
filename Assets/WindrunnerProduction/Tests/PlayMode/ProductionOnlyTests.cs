using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Unity;
namespace Windrunner.Production.Tests
{
    public sealed class ProductionOnlyTests
    {
        [UnityTest] public IEnumerator PortableGraphOperatesWithLegacyWritersPhysicallyAbsent()
        {
            yield return SceneManager.LoadSceneAsync("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");yield return null;
            var bridgeType=Type.GetType("AirflowPrototype.AirflowPortableAnimationBridge, Assembly-CSharp",true);
            var bridge=(Behaviour)UnityEngine.Object.FindAnyObjectByType(bridgeType);
            try
            {
                Assert.That((bool)bridgeType.GetProperty("ProductionOnlyReady").GetValue(bridge),Is.True);
                foreach(string name in new[]{"PlayerHumanoidAnimatorDriver","PlayerCustomAnimationDriver","PlayerRunningArmPoseController","PlayerProceduralKnifeFlick","PlayerDualThrowPose","PlayerGroundedFootIK","PlayerInputStartupRepair"})
                {
                    var type=Type.GetType("AirflowPrototype."+name+", Assembly-CSharp",true);
                    foreach(Behaviour legacy in UnityEngine.Object.FindObjectsByType(type,FindObjectsInactive.Include,FindObjectsSortMode.None))
                    { Assert.That(legacy.enabled,Is.False,name);UnityEngine.Object.Destroy(legacy); }
                }
                yield return null;
                foreach(string name in new[]{"PlayerHumanoidAnimatorDriver","PlayerCustomAnimationDriver","PlayerRunningArmPoseController","PlayerProceduralKnifeFlick","PlayerDualThrowPose","PlayerGroundedFootIK","PlayerInputStartupRepair","AirCastFeedback","AirTargetIndicator","AirChargeVisual"})
                    Assert.That(UnityEngine.Object.FindObjectsByType(Type.GetType("AirflowPrototype."+name+", Assembly-CSharp",true),FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty,name+" must be absent from the production scene.");
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
