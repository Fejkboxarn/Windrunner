using System;
using System.Linq;
using System.IO;
using PortableAnimation;
using PortableAnimation.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Windrunner.Production.Animation;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview
    {
        UnityEngine.SceneManagement.Scene sourceScene;
        AnimationPlayableOutput isolatedOutput;
        GroundOnlyPipelineRuntime composition;
        GroundMotionProfile previewGround;
        PortableCharacterActionProfile previewActions;
        Vector2 browserScroll, editScroll, matrixScroll,pan;
        float zoom=1,pitch, evaluated=-1;
        bool initialized, composed=true, candidate, skeleton, noMask, rebuild=true;
        int action=-1, activeContext=1, settingsTab, categoryFilter, previewFrame;
        int groundSlots => slots.FindIndex(s=>s.path.StartsWith("actions.",StringComparison.Ordinal));
        static readonly string[] ContextNames={"Idle","Moving","Airborne"};
        static readonly string[] Categories={"All","Idle","Movement","Jump","Airborne","Cast","Dash","Sling","Combat"};
        static readonly string[] Tabs={"Assignment","Body mask","Timing & Speed"};
        ActionMotionContext Mode => activeContext==0?ActionMotionContext.Stationary:
            activeContext==2?ActionMotionContext.Airborne:ActionMotionContext.GroundedMoving;
        PortableCharacterActionProfile Actions => draft?.Entries.Find(e=>e.Source==draftProfile.standaloneActionProfile)?.Draft as PortableCharacterActionProfile;
        ActionPoseOverride Variant => action<0 || activeContext==1?null:activeContext==0?Actions.Action(action).stationary:Actions.Action(action).airborne;
        float Duration => (float)Math.Max(1.5, double.IsInfinity(timingSample.EstimatedSeconds) ? 10 :
            Math.Min(300, Math.Max(clip ? clip.length : 1, timingSample.EstimatedSeconds)));
        void UndoPreview(){rebuild=true;Repaint();}
        void DisposeComposition()
        {
            composition?.Dispose();composition=null;
            if(previewActions)DestroyImmediate(previewActions);
            if(previewGround)DestroyImmediate(previewGround);
        }
        void InitializeStudio()
        {
            initialized=true;Bind();
            if(!previewRig)return;
            try
            {
                if(AnimationLaneDraft.Pending)laneProfile=AnimationLaneDraft.Source;
                OpenDraft();
                if(AnimationLaneDraft.Pending)
                {
                    var modules=AnimationLaneDraft.Read();
                    try
                    {
                        EditorUtility.CopySerialized(modules.ground,draftGround);
                        EditorUtility.CopySerialized(modules.actions,Actions);
                        draftGround.hideFlags=Actions.hideFlags=HideFlags.HideAndDontSave;
                    }
                    finally{DestroyImmediate(modules.ground);DestroyImmediate(modules.actions);AnimationLaneDraft.Finish();}
                }
                SelectGround(Mathf.Clamp(EditorPrefs.GetInt("Windrunner.AnimationStudio.GroundSlot",0),0,groundSlots-1));
                StartIndex();
            }
            catch(Exception ex){issue=ex.Message;}
        }
        void StartIndex()
        {
            library.Clear();matches.Clear();indexed=0;showLibrary=true;
            projectPaths=paths.Concat(AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets"})
                .Concat(AssetDatabase.FindAssets("t:Model",new[]{"Assets"})).Distinct().Select(AssetDatabase.GUIDToAssetPath)).Distinct().ToArray();
        }
        bool InCategory(string name,string path)
        {
            string n=(name+" "+path).ToLowerInvariant();
            switch(categoryFilter)
            {
                case 1:return n.Contains("idle");
                case 2:return n.Contains("walk")||n.Contains("run")||n.Contains("turn");
                case 3:return n.Contains("jump")||n.Contains("land");
                case 4:return n.Contains("air")||n.Contains("fall");
                case 5:return n.Contains("throw")||n.Contains("cast")||n.Contains("wave")||n.Contains("block");
                case 6:return n.Contains("dash")||n.Contains("fast")||n.Contains("dodge");
                case 7:return n.Contains("sling")||n.Contains("custom");
                case 8:return n.Contains("attack")||n.Contains("combat");
                default:return true;
            }
        }
        void SelectGround(int index)
        {
            action=-1;slot=index;EditorPrefs.SetInt("Windrunner.AnimationStudio.GroundSlot",slot);
            activeContext=index==(int)GroundPose.Idle?0:index==(int)GroundPose.JumpStart||index==(int)GroundPose.Fall||index==(int)GroundPose.Rise?2:1;
            SelectAssigned();
        }
        void SelectAction(int index,int context)
        {
            action=index;activeContext=context;
            slot=groundSlots+index*3+(context==0?1:context==1?0:2);
            SelectAssigned();
        }
        AnimationClip Resolved(int index,int context)
        {
            var a=Actions.Action(index);
            if(!a.IsPlayable)return null;
            var v=context==0?a.stationary:context==2?a.airborne:null;
            return v!=null && v.enabled && !v.inheritClip && v.muteWhenMissing && !v.Available ? null : v!=null && v.Available && !v.inheritClip?v.clip:a.clip;
        }
        void SelectAssigned()
        {
            AnimationClip current;
            if(action>=0)current=Resolved(action,activeContext);
            else
            {
                using var so=new SerializedObject(draftGround);
                current=so.FindProperty(slots[slot].path)?.objectReferenceValue as AnimationClip;
            }
            issue=null;
            if(current)SetPreviewClip(current);
            else issue="This slot has no assigned clip. Choose a browser candidate.";
            candidate=false;playhead=0;rebuild=true;
        }
        void OnGUI()
        {
            if(Application.isPlaying){EditorGUILayout.HelpBox("Gameplay lane is running. Press Insert to inspect its evaluated pose. Stop Play to resume authoring.",MessageType.Info);return;}
            if(!initialized)InitializeStudio();
            if(!previewRig || draft==null)
            {
                EditorGUILayout.HelpBox(issue??"Load the production character to begin.",MessageType.Info);
                if(GUILayout.Button("Load production character"))InitializeStudio();
                return;
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Animation Studio",EditorStyles.boldLabel,GUILayout.Width(112));
                var root=(WindrunnerAnimationProfile)EditorGUILayout.ObjectField(draftProfile,typeof(WindrunnerAnimationProfile),false,GUILayout.Width(156));
                if(root && root!=draftProfile && root.productionOnly && root.useStandaloneCharacterGraph &&
                    root.standaloneGroundProfile && root.standaloneActionProfile &&
                    (!draftDirty || EditorUtility.DisplayDialog("Switch profile?","Discard the current unsaved drafts?","Switch","Keep editing")))
                {laneProfile=root;OpenDraft();SelectGround(0);}
                GUILayout.FlexibleSpace();
                bool dirty=draftDirty;
                GUILayout.Label(dirty?"Unsaved draft":"Saved",EditorStyles.miniLabel);
                using(new EditorGUI.DisabledScope(!dirty))
                {
                    if(GUILayout.Button("Revert",EditorStyles.toolbarButton)){draft.Revert();SelectAssigned();hasUnsavedChanges=false;}
                    if(GUILayout.Button("Save",EditorStyles.toolbarButton))
                        try{draft.Save();issue=null;rebuild=true;hasUnsavedChanges=false;}catch(Exception ex){issue=ex.Message;}
                }
                if(GUILayout.Button("Test draft in lane",EditorStyles.toolbarButton))LaunchDraft();
            }
            if(!string.IsNullOrEmpty(issue))
            {
                EditorGUILayout.HelpBox(issue,MessageType.Warning);
                if(AnimationLaneDraft.Pending && GUILayout.Button("Discard stored lane draft and reload profile"))
                {AnimationLaneDraft.Finish();InitializeStudio();}
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUILayout.VerticalScope("box",GUILayout.Width(Mathf.Clamp(positionWidth*.22f,200,300)),GUILayout.ExpandHeight(true)))Browser();
                using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true)))CenterPanel();
                using(new EditorGUILayout.VerticalScope("box",GUILayout.Width(Mathf.Clamp(positionWidth*.29f,280,405)),GUILayout.ExpandHeight(true)))InspectorPanel();
            }
        }
        public override void SaveChanges()
        {
            draft?.Save();rebuild=true;base.SaveChanges();
        }
        public override void DiscardChanges()
        {
            draft?.Revert();rebuild=true;base.DiscardChanges();
        }
        float positionWidth=>((EditorWindow)this).position.width;
        void Browser()
        {
            GUILayout.Label("Clip library",EditorStyles.boldLabel);
            string query=EditorGUILayout.TextField(projectSearch,EditorStyles.toolbarSearchField);
            int filter=EditorGUILayout.Popup(categoryFilter,Categories);
            if(query!=projectSearch || filter!=categoryFilter)
            {
                projectSearch=query;categoryFilter=filter;matches.Clear();browserScroll=default;
                for(int i=0;i<library.Count;i++)if(InCategory(library[i].name,library[i].path)&&Matches(library[i].name,library[i].path))matches.Add(i);
            }
            GUILayout.Label(projectPaths==null?$"{matches.Count} Humanoid clips":$"{matches.Count} clips · indexing {indexed}/{projectPaths.Length}",EditorStyles.miniLabel);
            using(var scroll=new EditorGUILayout.ScrollViewScope(browserScroll))
            {
                browserScroll=scroll.scrollPosition;
                // Only draw visible rows even with the full project index open.
                const int row=41;int first=Mathf.Clamp((int)(browserScroll.y/row)-1,0,matches.Count);
                int last=Math.Min(matches.Count,first+(int)(((EditorWindow)this).position.height/row)+4);
                GUILayout.Space(row*first);
                for(int i=first;i<last;i++)
                {
                    var item=library[matches[i]];
                    var r=GUILayoutUtility.GetRect(10,row,GUILayout.ExpandWidth(true));
                    if(clip && clip.name==item.name && AssetDatabase.GetAssetPath(clip)==item.path)EditorGUI.DrawRect(r,new Color(.12f,.38f,.43f));
                    GUI.Label(new Rect(r.x+4,r.y,r.width-8,21),new GUIContent(item.name,item.path),EditorStyles.boldLabel);
                    var folder=item.path.Split('/');
                    GUI.Label(new Rect(r.x+4,r.y+18,r.width-8,20),$"{folder[1]}  ·  {item.length:F2}s{(item.loop?"  ↻":"")}{(Used(item.path,item.name)?"  • assigned":"")}",EditorStyles.miniLabel);
                    if(Event.current.type==EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(item.path))
                            if(obj is AnimationClip c && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string _,out long id)&&id==item.id)
                            {SetPreviewClip(c);candidate=true;rebuild=true;break;}
                        Event.current.Use();
                    }
                }
                GUILayout.Space(row*(matches.Count-last));
            }
        }
        readonly System.Collections.Generic.HashSet<string> used=new();
        void RefreshUsed()
        {
            used.Clear();
            foreach(var entry in new ScriptableObject[]{draftGround,Actions})
            using(var so=new SerializedObject(entry))
            {
                var prop=so.GetIterator();
                while(prop.Next(true))if(prop.propertyType==SerializedPropertyType.ObjectReference && prop.objectReferenceValue is AnimationClip c)
                    used.Add(AssetDatabase.GetAssetPath(c)+"|"+c.name);
            }
        }
        bool Used(string path,string name)=>used.Contains(path+"|"+name);
        void CenterPanel()
        {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool next=GUILayout.Toolbar(composed?1:0,new[]{"Isolated clip","Production graph"},EditorStyles.toolbarButton)==1;
                if(next!=composed){composed=next;rebuild=true;}
                skeleton=GUILayout.Toggle(skeleton,"Bones",EditorStyles.toolbarButton);
            }
            int comparison = GUILayout.Toolbar(timingComparison, TimingModes);
            if (comparison != timingComparison) { timingComparison = comparison; rebuild = true; }
            if(rebuild || composition!=null && playhead+.00001f<evaluated)BuildPreview();
            if(!previewRig)return;
            var rect=GUILayoutUtility.GetRect(140,160,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            var ev=Event.current;
            if(rect.Contains(ev.mousePosition))
            {
                if(ev.type==EventType.ScrollWheel){zoom=Mathf.Clamp(zoom+ev.delta.y*.07f,.35f,2.5f);ev.Use();}
                if(ev.type==EventType.MouseDrag)
                {
                    if(ev.button==2 || ev.shift)pan+=ev.delta*.0025f;else{angle+=ev.delta.x;pitch=Mathf.Clamp(pitch+ev.delta.y,-60,60);}
                    ev.Use();
                }
            }
            if(ev.type==EventType.Repaint)
            {
                if(composition!=null)AdvancePreview();
                if(!composed)EvaluatePose(timingSample.SourceSeconds,mixer.GetInputWeight(1));
                Vector3 pivot=center+studio.camera.transform.right*(-pan.x)+Vector3.up*pan.y;
                studio.camera.transform.position=pivot+Quaternion.Euler(pitch,angle,0)*Vector3.forward*distance*zoom;
                studio.camera.transform.LookAt(pivot);
                studio.BeginPreview(rect,GUIStyle.none);studio.Render(true);GUI.DrawTexture(rect,studio.EndPreview(),ScaleMode.StretchToFill);
                if(skeleton)DrawBones(rect);
                GUI.Label(new Rect(rect.x+8,rect.y+7,rect.width-16,22),action<0?slots[slot].label:Actions.Action(action).id+"   /   "+ContextNames[activeContext]+(composed && noMask?"   ·   full-body comparison":""),EditorStyles.whiteBoldLabel);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(playing?"Pause":"Play"))playing=!playing;
                if(GUILayout.Button("< frame")){playing=false;playhead=Mathf.Max(0,playhead-previewStep);rebuild=true;}
                if(GUILayout.Button("frame >")){playing=false;playhead+=previewStep;}
                if(GUILayout.Button("Restart")){playhead=0;rebuild=true;}
                loop=GUILayout.Toggle(loop,"Loop");
                if(GUILayout.Button(candidate?"B · candidate":"A · assigned"))
                {candidate=!candidate;rebuild=true;}
            }
            playhead=EditorGUILayout.Slider("Time (s)",playhead,0,Duration);
            speed=EditorGUILayout.Slider("Preview time scale",speed,.1f,2);
            Matrix();
        }
        void Matrix()
        {
            GUILayout.Label("Action assignments",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope()){GUILayout.Label("Phase",GUILayout.Width(85));foreach(string c in ContextNames)GUILayout.Label(c,EditorStyles.miniBoldLabel);}
            using(var view=new EditorGUILayout.ScrollViewScope(matrixScroll,GUILayout.Height(134)))
            {
                matrixScroll=view.scrollPosition;
                for(int i=0;i<Actions.Count;i++)
                using(new EditorGUILayout.HorizontalScope())
                {
                    var a=Actions.Action(i);GUILayout.Label(a.id,GUILayout.Width(85));
                    for(int c=0;c<3;c++)
                    {
                        var v=c==0?a.stationary:c==2?a.airborne:null;var resolved=Resolved(i,c);
                        string label=(v!=null && (v.inheritClip || !v.Available && !(v.enabled && v.muteWhenMissing))?"↳ ":"")+(resolved?resolved.name:"Empty");
                        var old=GUI.backgroundColor;if(i==action && c==activeContext)GUI.backgroundColor=Color.cyan;
                        if(GUILayout.Button(new GUIContent(label,label+" · "+ContextNames[c]),EditorStyles.miniButton,GUILayout.MinWidth(15)))SelectAction(i,c);
                        GUI.backgroundColor=old;
                    }
                }
            }
        }
    }
}
