using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AirflowPrototype.Editor
{
    public static class CharacterProductionDiagnostics
    {
        private static readonly HashSet<string> Retired=new HashSet<string>{"PlayerHumanoidAnimatorDriver","PlayerCustomAnimationDriver","PlayerRunningArmPoseController","PlayerProceduralKnifeFlick","PlayerDualThrowPose","PlayerGroundedFootIK","PlayerInputStartupRepair","AirCastFeedback","AirTargetIndicator","AirChargeVisual"};
        public static List<string> Validate(GameObject character)
        {
            var errors=new List<string>();
            if(character==null){errors.Add("Select a player root.");return errors;}
            var components=character.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null).ToArray();
            foreach(var c in components)if(Retired.Contains(c.GetType().Name))errors.Add("Retired component still serialized: "+c.GetType().Name+" (enabled="+c.enabled+")");
            foreach(string name in new[]{"PlayerMotor","AirDash","AirCaster","AirSlingController","AirPower","PlayerInputReader"})if(components.Count(c=>c.GetType().Name==name)!=1)errors.Add("Expected one "+name+" authority.");
            var bridge=character.GetComponent<AirflowPortableAnimationBridge>();
            if(bridge==null || bridge.Profile==null || !bridge.Profile.productionOnly || !bridge.Profile.automaticallyStartPortableInPlayMode)errors.Add("Portable production-only automatic ownership is not configured.");
            if(bridge!=null && bridge.HumanoidAnimator!=null && bridge.HumanoidAnimator.runtimeAnimatorController!=null)errors.Add("Legacy Animator controller is still assigned.");
            if(Application.isPlaying && (bridge==null || !bridge.ProductionOnlyReady))errors.Add("Portable graph is not the sole active pose owner.");
            return errors;
        }
        [MenuItem("Tools/Windrunner/Acceptance/Validate selected production character")]
        public static void Report()
        {
            var errors=Validate(Selection.activeGameObject);
            if(errors.Count==0)Debug.Log("[Production-only] Character composition is valid"+(Application.isPlaying?" and portable graph owns the pose.":". Enter Play to validate live ownership."));
            else Debug.LogWarning("[Production-only] "+string.Join("\n",errors));
        }
    }
}
