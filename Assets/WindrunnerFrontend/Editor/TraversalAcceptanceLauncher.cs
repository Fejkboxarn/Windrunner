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
            {
                var session=Object.FindAnyObjectByType<PlayerSession>();
                TraversalAcceptance.Install(session);
                string guid=SessionState.GetString("Windrunner.Acceptance.MotionProfile","");
                SessionState.EraseString("Windrunner.Acceptance.MotionProfile");
                if (!string.IsNullOrEmpty(guid))
                {
                    var profile=AssetDatabase.LoadAssetAtPath<Windrunner.Production.Animation.WindrunnerAnimationProfile>(AssetDatabase.GUIDToAssetPath(guid));
                    var bridge=session.character.GetComponentInChildren<AirflowPrototype.AirflowPortableAnimationBridge>(true);
                    if (!profile || !profile.productionOnly || !profile.useStandaloneCharacterGraph || !profile.ValidateForLiveGraph(out _) || !bridge)
                        Debug.LogError("Lane comparison requires a valid production-only standalone profile and player.");
                    else
                    {
                        if(AirflowPrototype.Editor.AnimationLaneDraft.Pending)
                        {
                            var modules=AirflowPrototype.Editor.AnimationLaneDraft.Read();
                            if (SessionState.GetBool("Windrunner.AnimationStudio.SavedTiming",false))
                            {
                                Object.Destroy(modules.ground);Object.Destroy(modules.actions);
                                modules=(Object.Instantiate(profile.standaloneGroundProfile),Object.Instantiate(profile.standaloneActionProfile));
                            }
                            SessionState.EraseBool("Windrunner.AnimationStudio.SavedTiming");
                            profile=Object.Instantiate(profile);
                            profile.previewOriginalTiming=SessionState.GetBool("Windrunner.AnimationStudio.OriginalTiming",false);
                            SessionState.EraseBool("Windrunner.AnimationStudio.OriginalTiming");
                            profile.standaloneGroundProfile=modules.ground;profile.standaloneActionProfile=modules.actions;
                            session.GetComponent<TraversalAcceptance>().OwnDraft(profile,modules.ground,modules.actions);
                        }
                        bridge.enabled=false;
                        using(var serialized=new SerializedObject(bridge))
                        {
                            serialized.FindProperty("profile").objectReferenceValue=profile;
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        bridge.enabled=true;
                    }
                }
            }
            if(state!=PlayModeStateChange.EnteredEditMode) return;
            var path=SessionState.GetString(Previous,"");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.EraseBool(Pending);
            SessionState.EraseString(Previous);
        }
    }
}
#endif
