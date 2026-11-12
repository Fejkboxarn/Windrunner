using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AirflowPrototype.Editor
{
    public static class CharacterCompositionAudit
    {
        [Serializable] private class Item { public string type,path,classification,reason;public bool enabled;public string[] profiles; }
        [Serializable] private class Report { public Item[] components;public string[] animatorControllers; }
        private static string PathOf(Transform t)=>t.parent!=null?PathOf(t.parent)+"/"+t.name:t.name;
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/AirflowPrototype/Generated/Batch1_Playground.unity");
            var report=new Report { components=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c!=null).Select(c=>{
                string name=c.GetType().Name;
                bool legacy=new[]{"PlayerHumanoidAnimatorDriver","PlayerCustomAnimationDriver","PlayerRunningArmPoseController","PlayerProceduralKnifeFlick","PlayerDualThrowPose","PlayerGroundedFootIK","PlayerInputStartupRepair","AirCastFeedback","AirTargetIndicator"}.Contains(name);
                var profiles=new System.Collections.Generic.List<string>();using(var so=new SerializedObject(c)){var p=so.GetIterator();while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue is ScriptableObject)profiles.Add(p.propertyPath+" -> "+AssetDatabase.GetAssetPath(p.objectReferenceValue));}
                return new Item{type=c.GetType().FullName,path=PathOf(c.transform),enabled=c.enabled && c.gameObject.activeInHierarchy,classification=legacy?"replaceable":"required",reason=legacy?"Serialized compatibility path; prove production replacement and audit callers before removal.":"Installed scene dependency; name alone is not an obsolete verdict.",profiles=profiles.ToArray()};
            }).ToArray(),animatorControllers=UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(a=>PathOf(a.transform)+" -> "+AssetDatabase.GetAssetPath(a.runtimeAnimatorController)).ToArray()};
            Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/CharacterCompositionAudit.json",JsonUtility.ToJson(report,true));
            CharacterTuningChecks.CapturePetra();
        }
    }
}
