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
    public sealed class DaggerMotionPreview : EditorWindow
    {
        const string Folder="Assets/Dagger Animation Pack/Animations/Humanoid";
        string[] paths, categories, choices, names;
        int category,index;
        PreviewRenderUtility studio;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idle, selected;
        AnimationClip clip;
        Animator previewRig;
        AirflowPortableAnimationBridge bridge;
        float position, speed=1, weight=1, angle=22;
        bool playing=true, loop=true;
        double last;
        string issue;

        [MenuItem("Tools/Windrunner/Acceptance/Dagger clip preview")]
        public static void Open() => GetWindow<DaggerMotionPreview>("Dagger clip preview");
        void OnEnable()
        {
            paths=AssetDatabase.FindAssets("t:AnimationClip",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith(".anim",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            categories=paths.Select(Category).Distinct().ToArray();
            RefreshList();
            EditorApplication.playModeStateChanged+=ModeChanged;
        }
        static string Category(string path)=>path.Substring(Folder.Length+1).Split('/')[0];
        void RefreshList()
        {
            choices=paths.Where(p=>Category(p)==categories[category]).ToArray();
            names=choices.Select(Path.GetFileNameWithoutExtension).ToArray();
            index=0;
            if(graph.IsValid()) LoadSelection();
        }
        void ModeChanged(PlayModeStateChange state) => Release();
        void OnDisable()
        {
            EditorApplication.playModeStateChanged-=ModeChanged;
            Release();
        }
        void Release()
        {
            if(graph.IsValid()) graph.Destroy();
            studio?.Cleanup();
            studio=null; previewRig=null; position=0; issue=null; clip=null;
        }
        void Bind()
        {
            Release();
            try
            {
                bridge=FindAnyObjectByType<AirflowPortableAnimationBridge>();
                if(!bridge || !bridge.HumanoidAnimator || !bridge.Profile)
                    throw new InvalidOperationException("Open StartingWorld or Zone 01 to bind the production Avatar.");
                var avatar=bridge.HumanoidAnimator.avatar;
                var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(bridge.HumanoidAnimator);
                var path=source ? AssetDatabase.GetAssetPath(source) : AssetDatabase.GetAssetPath(avatar);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter)) path=AssetDatabase.GetAssetPath(avatar);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!model || !(AssetImporter.GetAtPath(path) is ModelImporter))
                    throw new InvalidOperationException("The bound Avatar requires a previewable imported model.");
                clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(choices[index]);
                if(!clip || !clip.humanMotion) throw new InvalidOperationException("This clip has no Humanoid motion.");
                studio=new PreviewRenderUtility();
                var subject=studio.InstantiatePrefabInScene(model);
                var rig=subject.GetComponentInChildren<Animator>();
                previewRig=rig;
                rig.avatar=avatar;rig.runtimeAnimatorController=null;rig.applyRootMotion=false;
                rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Windrunner.EditorClipPreview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer=AnimationMixerPlayable.Create(graph,2);
                idle=AnimationClipPlayable.Create(graph,bridge.Profile.standaloneGroundProfile.Clip(GroundPose.Idle));
                graph.Connect(idle,0,mixer,0);
                SetPreviewClip(clip);
                AnimationPlayableOutput.Create(graph,"Preview rig only",rig).SetSourcePlayable(mixer);
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
        void LoadSelection()
        {
            if(!graph.IsValid()){Bind();return;}
            var replacement=AssetDatabase.LoadAssetAtPath<AnimationClip>(choices[index]);
            if(!replacement || !replacement.humanMotion){Release();issue="This clip has no Humanoid motion.";return;}
            SetPreviewClip(replacement);
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
            position=0;
        }
        Vector3 center;
        float distance;
        void Update()
        {
            double now=EditorApplication.timeSinceStartup;
            if(graph.IsValid() && playing)
            {
                position+=(float)Math.Min(.1,now-last)*speed;
                if(position>=clip.length){position=loop?Mathf.Repeat(position,clip.length):clip.length;if(!loop)playing=false;}
                Repaint();
            }
            last=now;
        }
        void EvaluatePose(float seconds,float blend)
        {
            selected.SetTime(seconds);idle.SetTime(seconds);
            mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);graph.Evaluate(0);
        }
        void OnGUI()
        {
            if(categories.Length==0){EditorGUILayout.HelpBox("No Dagger Pack clips found.",MessageType.Warning);return;}
            EditorGUILayout.LabelField("Clip preview · isolated production Avatar",EditorStyles.boldLabel);
            int next=EditorGUILayout.Popup("Category",category,categories);
            if(next!=category){category=next;RefreshList();}
            next=EditorGUILayout.Popup("Clip",index,names);
            if(next!=index){index=next;LoadSelection();}
            if(GUILayout.Button("Load clip on production model")) Bind();
            if(issue!=null)EditorGUILayout.HelpBox(issue,MessageType.Warning);
            if(graph.IsValid())
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(playing?"Pause":"Play"))playing=!playing;
                    if(GUILayout.Button("Restart")){position=0;playing=true;}
                    loop=GUILayout.Toggle(loop,"Repeat");
                }
                position=EditorGUILayout.Slider("Time (s)",position,0,clip.length);
                speed=EditorGUILayout.Slider("Preview speed",speed,.1f,3);
                weight=EditorGUILayout.Slider("Idle → selected pose",weight,0,1);
                angle=EditorGUILayout.Slider("Orbit",angle,-180,180);
                var rect=GUILayoutUtility.GetRect(120,240,GUILayout.ExpandHeight(true));
                if(Event.current.type==EventType.Repaint)
                {
                    EvaluatePose(position,weight);
                    studio.camera.transform.position=center+Quaternion.Euler(0,angle,0)*Vector3.forward*distance;
                    studio.camera.transform.LookAt(center);
                    studio.BeginPreview(rect,GUIStyle.none);
                    studio.Render(true);
                    GUI.DrawTexture(rect,studio.EndPreview(),ScaleMode.StretchToFill);
                }
            }
            EditorGUILayout.LabelField("Gameplay · production graph, masks and equipped weapons",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("Play traversal and rewards lane"))
                    EditorApplication.ExecuteMenuItem("Tools/Windrunner/Acceptance/Play traversal and rewards lane");
            if(Application.isPlaying && !bridge) bridge=FindAnyObjectByType<AirflowPortableAnimationBridge>();
            if(Application.isPlaying && bridge)
                EditorGUILayout.LabelField("Active action",bridge.ActiveAnimationAction);
        }
    }
}
