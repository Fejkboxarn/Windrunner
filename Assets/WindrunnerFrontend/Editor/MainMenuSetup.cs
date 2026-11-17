using System;
using System.Linq;
using AirflowPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Audio;
using Windrunner.Production.Input;
using Windrunner.Production.Progression;
using Windrunner.Production.Unity;

namespace Windrunner.Frontend.Editor
{
    public static class MainMenuSetup
    {
        public const string Source="Assets/AirflowPrototype/Generated/Batch1_Playground.unity";
        [MenuItem("Tools/Windrunner/Open Main Menu")]
        public static void Open()
        {
            if(!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PlayerSession.TitleScene);
        }
        public static void Create()
        {
            if(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes before composing the menu.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerSession.WorldScene)!=null || AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerSession.TitleScene)!=null)
                throw new InvalidOperationException("Frontend scenes already exist. Open them through Tools > Windrunner.");
            if(!AssetDatabase.CopyAsset(Source,PlayerSession.WorldScene))throw new InvalidOperationException("Could not copy production world.");
            var world=EditorSceneManager.OpenScene(PlayerSession.WorldScene);
            var root=UnityEngine.Object.FindAnyObjectByType<ProjectRoot>();
            var host=root.GetComponent<WindrunnerInputHost>();
            var controller=UnityEngine.Object.FindAnyObjectByType<PlayerMotor>().GetComponent<CharacterController>();
            var template=host.AuthoringActions;
            var catalog=ScriptableObject.CreateInstance<ProgressionCatalog>();
            AssetDatabase.CreateAsset(catalog,"Assets/WindrunnerFrontend/ProductionCatalog.asset");
            if(host.PauseView!=null && host.PauseView.Panel!=null)host.PauseView.Panel.SetActive(false);
            host.Configure(template,null,host.UiModule);
            var session=Compose(root,host,catalog);
            BindAudio(session);
            session.character=controller;session.arrival=new GameObject("Starting clearing arrival").transform;
            session.arrival.SetPositionAndRotation(controller.transform.position,controller.transform.rotation);
            session.orbit=UnityEngine.Object.FindAnyObjectByType<PlayerOrbitCamera>();
            var progression=root.gameObject.AddComponent<ProgressionHost>();progression.project=root;progression.catalog=catalog;
            var actor=controller.gameObject.AddComponent<ProgressionActor>();actor.host=progression;actor.inputHost=host;actor.interactionOrigin=controller.transform;
            if(!EditorSceneManager.SaveScene(world))throw new InvalidOperationException("Could not save integration world.");
            var title=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Menu camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.034f,.080f,.087f);camera.cullingMask=0;
            var module=new GameObject("Menu events",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>();
            root=new GameObject("Menu services").AddComponent<ProjectRoot>();host=root.gameObject.AddComponent<WindrunnerInputHost>();
            host.Configure(template,null,module);Compose(root,host,catalog);
            if(!EditorSceneManager.SaveScene(title,PlayerSession.TitleScene))throw new InvalidOperationException("Could not save title scene.");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(PlayerSession.TitleScene,true),new EditorBuildSettingsScene(PlayerSession.WorldScene,true)}
                .Concat(EditorBuildSettings.scenes).ToArray();
            AssetDatabase.SaveAssets();
        }
        private static PlayerSession Compose(ProjectRoot root,WindrunnerInputHost host,ProgressionCatalog catalog)
        {
            using var fields=new SerializedObject(root);fields.FindProperty("persistAcrossScenes").boolValue=false;fields.ApplyModifiedPropertiesWithoutUndo();
            var session=root.gameObject.AddComponent<PlayerSession>();session.project=root;session.input=host;session.catalog=catalog;
            var menu=root.gameObject.AddComponent<SessionMenu>();menu.session=session;
            menu.worldMix=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/WindrunnerFrontend/WorldAudio.mixer");
            return session;
        }
        public static void IntegrateAudio()
        {
            foreach(string path in new[]{PlayerSession.TitleScene,PlayerSession.WorldScene})
            {
                if(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                    throw new InvalidOperationException("Save open scenes before updating audio composition.");
                var scene=EditorSceneManager.OpenScene(path);
                var session=UnityEngine.Object.FindAnyObjectByType<PlayerSession>();
                session.GetComponent<SessionMenu>().worldMix=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/WindrunnerFrontend/WorldAudio.mixer");
                if(path==PlayerSession.WorldScene)BindAudio(session);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save audio composition.");
            }
        }
        private static void BindAudio(PlayerSession session)
        {
            var owned=session.GetComponent<SessionMenu>().worldMix;
            var original=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Raygeas/Grasslands - Stylized Nature/Assets/Audio/SceneMixer.mixer");
            if(!owned || !original)throw new InvalidOperationException("Missing world mixer.");
            var groups=owned.FindMatchingGroups("").ToDictionary(g=>g.name);
            foreach(var source in session.gameObject.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<AudioSource>(true)))
            {
                var group=source.outputAudioMixerGroup;
                if(group==null || group.audioMixer!=original)continue;
                source.outputAudioMixerGroup=groups[group.name];
                PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            }
        }
    }
}
