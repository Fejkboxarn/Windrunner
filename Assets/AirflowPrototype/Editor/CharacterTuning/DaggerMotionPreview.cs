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
        string[] paths, categories, choices, names;
        int category,index;
        PreviewRenderUtility studio;
        PlayableGraph graph;
        AnimationLayerMixerPlayable mixer;
        AvatarMask entireBody, previewMask;
        AnimationClip referenceClip;
        int basePose;
        string search="";
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
            categories=new[]{"All"}.Concat(paths.Select(Category).Distinct()).ToArray();
            RefreshList();
            EditorApplication.playModeStateChanged+=ModeChanged;
        }
        static string Category(string path)=>path.Substring(Folder.Length+1).Split('/')[0];
        void RefreshList()
        {
            choices=paths.Where(p=>(category==0 || Category(p)==categories[category]) && Path.GetFileNameWithoutExtension(p).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            names=choices.Select(Path.GetFileNameWithoutExtension).ToArray();
            index=0;
            if(graph.IsValid() && choices.Length>0) LoadSelection();
        }
        void ModeChanged(PlayModeStateChange state) { CloseDraft(); Release(); }
        void OnDisable()
        {
            CloseDraft();
            EditorApplication.playModeStateChanged-=ModeChanged;
            Release();
        }
        void Release()
        {
            if(graph.IsValid()) graph.Destroy();
            if(entireBody) DestroyImmediate(entireBody);
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
                clip=choices.Length>0?AssetDatabase.LoadAssetAtPath<AnimationClip>(choices[index]):bridge.Profile.standaloneGroundProfile.Clip(GroundPose.Idle);
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
                ApplyPreviewMask();
                idle=AnimationClipPlayable.Create(graph,bridge.Profile.standaloneGroundProfile.Clip(GroundPose.Idle));
                graph.Connect(idle,0,mixer,0);
                SetPreviewBase();
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
        void ApplyPreviewMask() => mixer.SetLayerMaskFromAvatarMask(1,previewMask?previewMask:entireBody);
        void SetPreviewBase()
        {
            var ground=draftGround?draftGround:(laneProfile && laneProfile.standaloneGroundProfile?laneProfile.standaloneGroundProfile:bridge.Profile.standaloneGroundProfile);
            var source=basePose==3 && referenceClip ? referenceClip :
                basePose==2 ? ground.Clip(GroundPose.Fall) :
                basePose==1 ? ground.RunDirectionalClip(0) : ground.Clip(GroundPose.Idle);
            graph.Disconnect(mixer,0);graph.DestroyPlayable(idle);
            idle=AnimationClipPlayable.Create(graph,source);idle.SetSpeed(0);
            graph.Connect(idle,0,mixer,0);
        }
        Vector3 center;
        float distance;
        float transitionSeconds=.2f;
        bool transitioning;
        void Update()
        {
            double now=EditorApplication.timeSinceStartup;
            IndexProjectBatch();
            if(graph.IsValid() && transitioning)
            {
                weight=Mathf.MoveTowards(weight,1f,(float)Math.Min(.1,now-last)/transitionSeconds);
                transitioning=weight<1;
                Repaint();
            }
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
            selected.SetTime(seconds);
            var baseClip=idle.GetAnimationClip();
            idle.SetTime(basePose==3 ? seconds/clip.length*baseClip.length : Mathf.Repeat(seconds,baseClip.length));
            mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,blend);graph.Evaluate(0);
        }
        Vector2 scroll;
        void OnGUI()
        {
            using(var content=new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll=content.scrollPosition;
                DrawWindow();
            }
        }
        void DrawWindow()
        {
            if(categories.Length==0){EditorGUILayout.HelpBox("No Dagger Pack clips found.",MessageType.Warning);return;}
            EditorGUILayout.LabelField("Clip preview · isolated production Avatar",EditorStyles.boldLabel);
            string filter=EditorGUILayout.TextField("Search pack",search);
            if(filter!=search){search=filter;RefreshList();}
            using(new EditorGUI.DisabledScope(draft!=null || Application.isPlaying))
                laneProfile=(Windrunner.Production.Animation.WindrunnerAnimationProfile)EditorGUILayout.ObjectField("Author / lane profile",laneProfile,typeof(Windrunner.Production.Animation.WindrunnerAnimationProfile),false);
            int next=EditorGUILayout.Popup("Category",category,categories);
            if(next!=category){category=next;RefreshList();}
            next=choices.Length>0?EditorGUILayout.Popup("Pack clip",index,names):index;
            if(next!=index){index=next;LoadSelection();}
            if(GUILayout.Button("Load clip on production model")) Bind();
            if(issue!=null)EditorGUILayout.HelpBox(issue,MessageType.Warning);
            if(graph.IsValid())
            {
                DrawProjectSearch();
                var picked=(AnimationClip)EditorGUILayout.ObjectField("Project clip B",clip,typeof(AnimationClip),false);
                if(picked && picked!=clip)
                {
                    if(picked.humanMotion && !picked.legacy) SetPreviewClip(picked);
                    else issue="Choose a Humanoid clip.";
                }
                EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(clip),EditorStyles.miniLabel,GUILayout.Height(18));
                if(GUILayout.Button("Pin current clip as A")) {referenceClip=clip;basePose=3;SetPreviewBase();}
                int baseChoice=EditorGUILayout.Popup("Base / comparison A",basePose,new[]{"Assigned idle","Assigned forward run","Assigned fall","Pinned A"});
                if(baseChoice!=basePose){basePose=baseChoice;SetPreviewBase();}
                var mask=(AvatarMask)EditorGUILayout.ObjectField("B mask (empty = full body)",previewMask,typeof(AvatarMask),false);
                if(mask!=previewMask){previewMask=mask;ApplyPreviewMask();}
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(playing?"Pause":"Play"))playing=!playing;
                    if(GUILayout.Button("Restart")){position=0;playing=true;}
                    loop=GUILayout.Toggle(loop,"Repeat");
                }
                position=EditorGUILayout.Slider("Time (s)",position,0,clip.length);
                speed=EditorGUILayout.Slider("Preview speed",speed,.1f,3);
                weight=EditorGUILayout.Slider("Base / A → clip B",weight,0,1);
                using(new EditorGUILayout.HorizontalScope())
                {
                    transitionSeconds=EditorGUILayout.Slider("Transition (s)",transitionSeconds,.04f,1.5f);
                    if(GUILayout.Button("Play A → B")){weight=0;position=0;playing=true;transitioning=true;}
                }
                angle=EditorGUILayout.Slider("Orbit",angle,-180,180);
                DrawSlots();
                var rect=GUILayoutUtility.GetRect(120,220,GUILayout.ExpandHeight(true));
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
                {
                    SessionState.SetString("Windrunner.Acceptance.MotionProfile",laneProfile?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(laneProfile)):"");
                    EditorApplication.ExecuteMenuItem("Tools/Windrunner/Acceptance/Play traversal and rewards lane");
                }
            if(Application.isPlaying && !bridge) bridge=FindAnyObjectByType<AirflowPortableAnimationBridge>();
            if(Application.isPlaying && bridge)
                EditorGUILayout.LabelField("Active action",bridge.ActiveAnimationAction);
        }
    }
}
