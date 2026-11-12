using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AirflowPrototype.Editor
{
    public static class WindrunnerAcceptanceTools
    {
        [MenuItem("Tools/Windrunner/Acceptance/Select player")]
        public static void SelectPlayer() { var motor=Object.FindAnyObjectByType<PlayerMotor>(); if(motor!=null) Selection.activeGameObject=motor.gameObject; }
        [MenuItem("Tools/Windrunner/Acceptance/Start portable animation (Play Mode only)")]
        public static void StartPortable() { if(!Application.isPlaying) return; var bridge=Object.FindAnyObjectByType<AirflowPortableAnimationBridge>(); if(bridge!=null) bridge.TryStartPortable(); }
        [MenuItem("Tools/Windrunner/Acceptance/Stop portable animation (Play Mode only)")]
        public static void StopPortable() { if(!Application.isPlaying) return; var bridge=Object.FindAnyObjectByType<AirflowPortableAnimationBridge>(); if(bridge!=null) bridge.StopPortable(); }
        [MenuItem("Tools/Windrunner/Acceptance/Reload gameplay scene (Play Mode only)")]
        public static void Reload() { if(Application.isPlaying && SceneManager.GetActiveScene().path=="Assets/AirflowPrototype/Generated/Batch1_Playground.unity") SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path); }
    }
}
