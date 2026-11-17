using System;
using System.Collections.Generic;
using System.Linq;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Animation;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview
    {
        CharacterTuningSession draft;
        WindrunnerAnimationProfile laneProfile, draftProfile;
        GroundMotionProfile draftGround;
        bool showAssignments;
        int slot;
        readonly List<(string label, CharacterTuningSession.Entry entry, string path)> slots = new();
        string[] slotLabels;
        string[] projectPaths;
        int indexed;
        readonly List<(string name,string path,long id)> library = new();
        readonly List<int> matches=new();
        string projectSearch="";
        bool Matches(string name,string path) =>
            name.IndexOf(projectSearch,StringComparison.OrdinalIgnoreCase)>=0 ||
            path.IndexOf(projectSearch,StringComparison.OrdinalIgnoreCase)>=0;
        bool showLibrary;
        void IndexProjectBatch()
        {
            if(projectPaths==null || !showLibrary || EditorApplication.isPlayingOrWillChangePlaymode)return;
            for(int end=Math.Min(indexed+4,projectPaths.Length);indexed<end;indexed++)
            {
                string path=projectPaths[indexed];
                if(AssetImporter.GetAtPath(path) is ModelImporter model &&
                    (!model.importAnimation || model.animationType!=ModelImporterAnimationType.Human))continue;
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if(asset is AnimationClip c && c.humanMotion && !c.legacy && !c.name.StartsWith("__preview__") &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string _,out long id))
                    {
                        if(Matches(c.name,path))matches.Add(library.Count);
                        library.Add((c.name,path,id));
                    }
            }
            if(indexed==projectPaths.Length)projectPaths=null;
            Repaint();
        }
        void DrawProjectSearch()
        {
            showLibrary=EditorGUILayout.Foldout(showLibrary,"Project Humanoid library",true);
            if(!showLibrary)return;
            if(GUILayout.Button(projectPaths==null?"Index project clips":"Pause indexing"))
            {
                if(projectPaths!=null){showLibrary=false;return;}
                library.Clear();matches.Clear();indexed=0;libraryPage=0;
                projectPaths=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets"})
                    .Concat(AssetDatabase.FindAssets("t:Model",new[]{"Assets"})).Distinct()
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            }
            EditorGUILayout.LabelField(projectPaths==null?$"{library.Count} compatible clips":$"{indexed} / {projectPaths.Length} assets inspected");
            string searchText=EditorGUILayout.TextField("Name or folder",projectSearch);
            if(searchText!=projectSearch)
            {
                projectSearch=searchText;libraryPage=0;matches.Clear();
                for(int i=0;i<library.Count;i++)
                    if(Matches(library[i].name,library[i].path))matches.Add(i);
            }
            const int perPage=12;
            for(int i=libraryPage*perPage;i<Math.Min((libraryPage+1)*perPage,matches.Count);i++)
            {
                var item=library[matches[i]];
                if(GUILayout.Button(new GUIContent(item.name,item.path),EditorStyles.miniButton))
                    foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(item.path))
                        if(obj is AnimationClip candidate && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string _,out long id) && id==item.id)
                        {SetPreviewClip(candidate);break;}
            }
            int pages=Math.Max(1,(matches.Count+perPage-1)/perPage);
            if(pages>1) libraryPage=EditorGUILayout.IntSlider("Page",libraryPage+1,1,pages)-1;
        }
        int libraryPage;
        void CloseDraft()
        {
            draft?.Dispose();
            draft=null;draftProfile=null;draftGround=null;
            slots.Clear();
            slot=0;
        }
        void OpenDraft()
        {
            CloseDraft();
            draft=new CharacterTuningSession(bridge.gameObject);
            draftProfile=laneProfile?laneProfile:bridge.Profile;
            draft.IncludeAnimationProfile(draftProfile);
            var ground=draft.Entries.First(e=>e.Source==draftProfile.standaloneGroundProfile);
            var actions=draft.Entries.First(e=>e.Source==draftProfile.standaloneActionProfile);
            draftGround=(GroundMotionProfile)ground.Draft;
            for(int i=0;i<(int)GroundPose.Count;i++)
                slots.Add(("Ground/"+(GroundPose)i,ground,$"clips.Array.data[{i}]"));
            foreach(string field in new[]{"runForwardRight","runBackwardRight","runBackwardLeft","runForwardLeft",
                "walkForward","walkRight","walkBackward","walkLeft","walkForwardRight","walkBackwardRight","walkBackwardLeft","walkForwardLeft",
                "stopBackward","stopLeft","stopRight"})
                slots.Add(("Ground/"+ObjectNames.NicifyVariableName(field),ground,field));
            var profile=(PortableCharacterActionProfile)actions.Draft;
            for(int i=0;i<profile.Count;i++)
            {
                string path=$"actions.Array.data[{i}]";
                foreach(string suffix in new[]{"clip","stationary.clip","airborne.clip"})
                    slots.Add((profile.Action(i).id+"/"+(suffix=="clip"?"Moving and fallback":suffix.Split('.')[0]),actions,path+"."+suffix));
            }
            slotLabels=slots.Select(s=>s.label).ToArray();
        }
        void DrawSlots()
        {
            showAssignments=EditorGUILayout.Foldout(showAssignments,"Production assignments and draft tuning",true);
            if(!showAssignments)return;
            if(Application.isPlaying){EditorGUILayout.HelpBox("Author in Edit Mode. Start a fresh lane to rebuild the cached graph.",MessageType.Info);return;}
            if(draft==null || draft.Character!=bridge.gameObject)
            {
                try{OpenDraft();}catch(Exception ex){CloseDraft();EditorGUILayout.HelpBox(ex.Message,MessageType.Warning);return;}
            }
            EditorGUILayout.LabelField("Editing modules of",draftProfile.name);
            slot=EditorGUILayout.Popup("Destination",slot,slotLabels);
            var dest=slots[slot];
            using var serialized=new SerializedObject(dest.entry.Draft);
            var field=serialized.FindProperty(dest.path);
            if(field==null){EditorGUILayout.HelpBox("Missing profile field "+dest.path,MessageType.Error);return;}
            var assigned=(AnimationClip)field.objectReferenceValue;
            using(var live=new SerializedObject(dest.entry.Source))
            {
                var persisted=live.FindProperty(dest.path)?.objectReferenceValue;
                EditorGUILayout.LabelField("Saved assignment",persisted?persisted.name:"Unassigned");
            }
            EditorGUILayout.LabelField("Draft assignment",assigned?assigned.name:"Unassigned");
            bool actionSlot=dest.entry.Draft is PortableCharacterActionProfile;
            string parentPath=actionSlot?dest.path.Substring(0,dest.path.LastIndexOf('.')):string.Empty;
            bool contextual=dest.path.EndsWith("stationary.clip") || dest.path.EndsWith("airborne.clip");
            string basePath=contextual?parentPath.Substring(0,parentPath.LastIndexOf('.')):parentPath;
            bool useOverride=contextual && assigned && serialized.FindProperty(parentPath+".enabled").boolValue && assigned.humanMotion && !assigned.legacy;
            var resolved=actionSlot && contextual && !useOverride ?
                serialized.FindProperty(basePath+".clip").objectReferenceValue as AnimationClip:assigned;
            if(assigned)EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(assigned),EditorStyles.miniLabel,GUILayout.Height(18));
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(!resolved))
                    if(GUILayout.Button("Audition slot"))
                    {
                        if(actionSlot)
                        {
                            string effective=useOverride?parentPath:basePath;
                            previewMask=(AvatarMask)serialized.FindProperty(effective+".mask").objectReferenceValue;
                            weight=serialized.FindProperty(effective+(useOverride?".weight":".blendWeight")).floatValue;
                            basePose=dest.path.EndsWith("airborne.clip")?2:dest.path.EndsWith("stationary.clip")?0:1;
                        }
                        else{previewMask=null;weight=1;basePose=0;}
                        ApplyPreviewMask();SetPreviewBase();SetPreviewClip(resolved);
                    }
                if(GUILayout.Button("Assign B to draft"))
                {
                    field.objectReferenceValue=clip;
                    int dot=dest.path.LastIndexOf('.');
                    if(dest.path.EndsWith("stationary.clip") || dest.path.EndsWith("airborne.clip"))
                        serialized.FindProperty(dest.path.Substring(0,dot)+".enabled").boolValue=true;
                }
            }
            if(dest.entry.Draft is PortableCharacterActionProfile)
            {
                string parent=dest.path.Substring(0,dest.path.LastIndexOf('.'));
                bool variant=parent.EndsWith("stationary") || parent.EndsWith("airborne");
                void Property(string path)=>EditorGUILayout.PropertyField(serialized.FindProperty(path));
                if(variant){Property(parent+".enabled");Property(parent+".weight");}
                else Property(parent+".blendWeight");
                Property(parent+".mask");
                string action=variant?parent.Substring(0,parent.LastIndexOf('.')):parent;
                foreach(string setting in new[]{"blendInSeconds","blendOutSeconds","playbackSpeed","priority","interruptible","loopUntilCanceled","allowSameActionRestart"})
                    Property(action+"."+setting);
                Property("stationaryEnterSpeed");Property("stationaryExitSpeed");Property("contextFadeSeconds");
            }
            serialized.ApplyModifiedProperties();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Save drafts"))
                {
                    try{draft.Save();issue=null;}catch(Exception ex){issue=ex.Message;}
                }
                if(GUILayout.Button("Revert drafts")) draft.Revert();
                if(GUILayout.Button("Close drafts")){CloseDraft();showAssignments=false;}
            }
            EditorGUILayout.HelpBox("Draft edits support Unity Undo. Save in Edit Mode, then restart the lane. Closing the window or entering Play discards unsaved drafts.",MessageType.Info);
        }
    }
}
