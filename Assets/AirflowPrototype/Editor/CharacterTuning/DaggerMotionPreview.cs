using System;
using System.IO;
using System.Linq;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview : EditorWindow
    {
        const string Folder="Assets/Dagger Animation Pack/Animations/Humanoid";
        string[] paths;
        PreviewRenderUtility studio;
        PlayableGraph graph;
        AnimationLayerMixerPlayable mixer;
        AvatarMask entireBody;
        AnimationClipPlayable idle, selected;
        AnimationClip clip;
        Animator previewRig;
        AirflowPortableAnimationBridge bridge;
        float playhead, speed=1, angle=22;
        bool playing=true, loop=true;
        double last;
        string issue;

        [MenuItem("Tools/Windrunner/Animation Authoring Studio V2")]
        [MenuItem("Tools/Windrunner/Acceptance/Dagger clip preview")]
        public static void Open() => GetWindow<DaggerMotionPreview>("Animation Studio V2");
        void OnEnable()
        {
            minSize=new Vector2(940,540);
            Undo.undoRedoPerformed+=UndoPreview;
            paths=!AssetDatabase.IsValidFolder(Folder)?Array.Empty<string>():AssetDatabase.FindAssets("t:AnimationClip",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith(".anim",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            EditorApplication.playModeStateChanged+=ModeChanged;
        }
        void ModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode && draft!=null && draftDirty && !AnimationLaneDraft.Pending)
                AnimationLaneDraft.Store(draftProfile,draftGround,Actions,false);
            CloseDraft();Release();initialized=false;hasUnsavedChanges=false;
        }
        void OnDisable()
        {
            Undo.undoRedoPerformed-=UndoPreview;
            CloseDraft();
            EditorApplication.playModeStateChanged-=ModeChanged;
            Release();
        }
        void Release()
        {
            DisposeComposition();
            if(graph.IsValid()) graph.Destroy();
            if(entireBody) DestroyImmediate(entireBody);
            studio?.Cleanup();
            if(sourceScene.IsValid())UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(sourceScene);
            sourceScene=default;
            studio=null; previewRig=null; playhead=0; issue=null; clip=null;
        }
        void Bind()
        {
            Release();
            try
            {
                bridge=FindAnyObjectByType<AirflowPortableAnimationBridge>();
                if(!bridge && !Application.isPlaying)
                {
                    sourceScene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity");
                    bridge=sourceScene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<AirflowPortableAnimationBridge>(true)).FirstOrDefault();
                }
                if(!bridge || !bridge.HumanoidAnimator || !bridge.Profile)
                    throw new InvalidOperationException("Open StartingWorld or Zone 01 to bind the production Avatar.");
                var avatar=bridge.HumanoidAnimator.avatar;
                var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(bridge.HumanoidAnimator);
                var path=source ? AssetDatabase.GetAssetPath(source) : AssetDatabase.GetAssetPath(avatar);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter)) path=AssetDatabase.GetAssetPath(avatar);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!model || !(AssetImporter.GetAtPath(path) is ModelImporter))
                    throw new InvalidOperationException("The bound Avatar requires a previewable imported model.");
                clip=paths.Length>0?AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[0]):bridge.Profile.standaloneGroundProfile.Clip(GroundPose.Idle);
                if(!clip || !clip.humanMotion) throw new InvalidOperationException("This clip has no Humanoid motion.");
                studio=new PreviewRenderUtility();
                var subject=studio.InstantiatePrefabInScene(model);
                var rig=subject.GetComponentInChildren<Animator>();
                previewRig=rig;
                rig.avatar=avatar;rig.runtimeAnimatorController=null;rig.applyRootMotion=false;rig.fireEvents=false;
                MountPreviewDaggers(rig);
                rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Windrunner.EditorClipPreview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer=AnimationLayerMixerPlayable.Create(graph,2);
                entireBody=new AvatarMask {hideFlags=HideFlags.HideAndDontSave};
                for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)
                    entireBody.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,true);
                entireBody.AddTransformPath(rig.transform,true);
                mixer.SetLayerMaskFromAvatarMask(1,entireBody);
                idle=AnimationClipPlayable.Create(graph,bridge.Profile.standaloneGroundProfile.Clip(GroundPose.Idle));
                graph.Connect(idle,0,mixer,0);
                SetPreviewClip(clip);
                isolatedOutput=AnimationPlayableOutput.Create(graph,"Preview rig only",rig);isolatedOutput.SetSourcePlayable(mixer);
                idle.SetSpeed(0);selected.SetSpeed(0);graph.Play();
                studio.camera.fieldOfView=32;
                studio.camera.nearClipPlane=.01f;
                studio.lights[0].intensity=1.2f;studio.lights[0].transform.rotation=Quaternion.Euler(35,30,0);
                studio.lights[1].intensity=.8f;
                var head=rig.GetBoneTransform(HumanBodyBones.Head);
                float height=Mathf.Max(.1f,head.position.y-subject.transform.position.y);
                center=subject.transform.position+Vector3.up*(height*.55f);
                distance=height*2.6f;
                last=EditorApplication.timeSinceStartup;
            }
            catch(Exception e){Release();issue=e.Message;}
        }
        void MountPreviewDaggers(Animator rig)
        {
            var equipment=bridge.transform.root.GetComponentInChildren<Windrunner.Daggers.DaggerPlayer>(true);
            if(!equipment || !equipment.catalog || !equipment.catalog.initialPair)return;
            var pair=equipment.Equipment?.Equipped ?? equipment.catalog.initialPair;
            foreach(bool left in new[]{true,false})
            {
                var hand=rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var sourceHand=equipment.rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var original=equipment.originalA.transform.IsChildOf(sourceHand)?equipment.originalA:equipment.originalB;
                string path=AnimationUtility.CalculateTransformPath(original.transform,equipment.rig.transform);
                var copy=rig.transform.Find(path);
                if(copy && copy.TryGetComponent<Renderer>(out var renderer))renderer.enabled=false;
                var grip=Windrunner.Daggers.DaggerPlayer.CreateHandGrip(rig,hand,left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal,copy?copy:hand);
                Windrunner.Daggers.DaggerPlayer.Mount(left?pair.leftModel:pair.rightModel,grip,left?pair.leftOffset:pair.rightOffset,left?pair.leftEuler:pair.rightEuler);
            }
        }
        void SetPreviewClip(AnimationClip replacement)
        {
            if(selected.IsValid())
            {
                graph.Disconnect(mixer,1);
                graph.DestroyPlayable(selected);
            }
            clip=replacement;
            selected=AnimationClipPlayable.Create(graph,clip);
            graph.Connect(selected,0,mixer,1);
            selected.SetSpeed(0);
            playhead=0;
        }
        Vector3 center;
        float distance;
        void Update()
        {
            double now=EditorApplication.timeSinceStartup;
            IndexProjectBatch();
            if(graph.IsValid() && playing)
            {
                playhead+=(float)Math.Min(.1,now-last)*speed;
                if(playhead>=Duration){playhead=loop?Mathf.Repeat(playhead,Duration):Duration;if(!loop)playing=false;}
                Repaint();
            }
            last=now;
        }
        void EvaluatePose(float seconds,float blend)
        {
            selected.SetTime(seconds);
            var baseClip=idle.GetAnimationClip();
            idle.SetTime(Mathf.Repeat(seconds,baseClip.length));
            mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,blend);graph.Evaluate(0);
        }
    }
}
