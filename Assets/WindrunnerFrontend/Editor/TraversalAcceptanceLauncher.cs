#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Windrunner.Frontend.Editor
{
    [InitializeOnLoad]
    static class TraversalAcceptanceLauncher
    {
        const string Menu="Tools/Windrunner/Acceptance/Play traversal and rewards lane";
        const string Pending="Windrunner.Acceptance.Launch";
        const string Previous="Windrunner.Acceptance.StartScene";
        static TraversalAcceptanceLauncher() => EditorApplication.playModeStateChanged += OnPlayMode;
        [MenuItem(Menu,true)] static bool CanLaunch() => !EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem(Menu)] static void Launch()
        {
            SessionState.SetString(Previous,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Pending,true);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerSession.WorldScene);
            EditorApplication.EnterPlaymode();
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Pending,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode)
                TraversalAcceptance.Install(Object.FindAnyObjectByType<PlayerSession>());
            if(state!=PlayModeStateChange.EnteredEditMode) return;
            var path=SessionState.GetString(Previous,"");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.EraseBool(Pending);
            SessionState.EraseString(Previous);
        }
    }
}
#endif
