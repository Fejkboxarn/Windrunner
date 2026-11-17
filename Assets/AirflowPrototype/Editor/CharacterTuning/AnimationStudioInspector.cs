using System;
using System.Linq;
using PortableAnimation;
using PortableAnimation.Authoring;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview
    {
        AuthoringBodyRegion selectedRegion=AuthoringBodyRegion.RightUpperArm;
        HumanBodyBones selectedBone=HumanBodyBones.RightHand;
        string maskClipboard;
        AvatarMask copiedAvatar;
        int preset;
        bool channels;
        int contributionView;
        static readonly string[] Presets={"Full Body","Upper Body","Arms Only","Right Arm","Left Arm","Torso and Arms","Airborne Action"};
        void InspectorPanel()
        {
            GUILayout.Label("Assignment & tuning",EditorStyles.boldLabel);
            using(var view=new EditorGUILayout.ScrollViewScope(editScroll))
            {
                editScroll=view.scrollPosition;
                int choice=EditorGUILayout.Popup("Destination",slot,slotLabels);
                if(choice!=slot)
                {
                    if(choice<groundSlots)SelectGround(choice);
                    else
                    {
                        int n=choice-groundSlots;SelectAction(n/3,n%3==0?1:n%3==1?0:2);
                    }
                }
                if(action>=0)
                {
                    int context=GUILayout.Toolbar(activeContext,ContextNames);
                    if(context!=activeContext)SelectAction(action,context);
                    int tab=GUILayout.Toolbar(settingsTab,Tabs);if(tab!=settingsTab && tab==1)skeleton=true;settingsTab=tab;
                }
                var dest=slots[slot];
                using var so=new SerializedObject(dest.entry.Draft);
                var field=so.FindProperty(dest.path);
                if(field==null)return;
                string path=action>=0?"actions.Array.data["+action+"]":"";
                string prefix=path+(activeContext==0?".stationary":activeContext==2?".airborne":"");
                bool contextual=action>=0 && activeContext!=1;
                void Prop(string key,string label)=>EditorGUILayout.PropertyField(so.FindProperty(key),new GUIContent(label));
                if(contextual)
                {
                    var enable=so.FindProperty(prefix+".enabled");
                    bool value=EditorGUILayout.Toggle("Customize context",enable.boolValue);
                    if(value!=enable.boolValue)
                    {
                        enable.boolValue=value;
                        if(value && !field.objectReferenceValue)
                            foreach(string flag in new[]{"inheritClip","inheritWeight","inheritMask"})so.FindProperty(prefix+"."+flag).boolValue=true;
                    }
                }
                bool inherited=contextual && !so.FindProperty(prefix+".enabled").boolValue;
                if(action<0 || settingsTab==0)
                {
                    var current=action>=0?Resolved(action,activeContext):field.objectReferenceValue as AnimationClip;
                    EditorGUILayout.LabelField("Assigned",current?current.name:"Empty",EditorStyles.wordWrappedLabel);
                    if(current)EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(current),EditorStyles.wordWrappedMiniLabel,GUILayout.Height(35));
                    GUILayout.Label("Browser candidate",EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(clip?clip.name:"Choose a clip",EditorStyles.wordWrappedLabel);
                    if(clip)EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(clip),EditorStyles.wordWrappedMiniLabel,GUILayout.Height(35));
                    if(GUILayout.Button("Apply candidate to draft") && clip)
                    {
                        StageCandidate(so);
                    }
                    if(contextual && !inherited)
                    {
                        Prop(prefix+".inheritClip","Inherit base clip");
                        Prop(prefix+".muteWhenMissing","Mute on missing clip");
                        Prop(prefix+".inheritWeight","Inherit global weight");
                    }
                    if(action>=0 && !inherited && (!contextual || !so.FindProperty(prefix+".inheritWeight").boolValue))
                        Prop(prefix+(contextual?".weight":".blendWeight"),"Global action weight");
                    if(inherited)EditorGUILayout.HelpBox("Uses the moving/base assignment, timing and mask.",MessageType.Info);
                    if(action>=0)
                    {
                        GUILayout.Label("Shared across actions",EditorStyles.miniBoldLabel);
                        Prop("stationaryEnterSpeed","Enter idle (m/s)");Prop("stationaryExitSpeed","Leave idle (m/s)");
                        Prop("contextFadeSeconds","Context blend (s)");
                    }
                }
                else if(inherited)EditorGUILayout.HelpBox("Enable Customize context to override this phase.",MessageType.Info);
                else if(settingsTab==1)MaskControls(so,prefix,contextual);
                else TimingControls(so,prefix,contextual);
                if(so.ApplyModifiedProperties()){rebuild=true;issue=null;}
            }
        }
        void StageCandidate(SerializedObject destination)
        {
            string path=slots[slot].path;
            destination.FindProperty(path).objectReferenceValue=clip;
            if(action>=0 && activeContext!=1)
            {
                string prefix=path.Substring(0,path.LastIndexOf('.'));
                destination.FindProperty(prefix+".enabled").boolValue=true;
                destination.FindProperty(prefix+".inheritClip").boolValue=false;
            }
            candidate=false;
            rebuild=true;
        }
        void TimingControls(SerializedObject so,string prefix,bool contextual)
        {
            bool own=!contextual || EditorGUILayout.Toggle("Override timing fields",so.FindProperty(prefix+".overrideTiming").boolValue);
            if(contextual)so.FindProperty(prefix+".overrideTiming").boolValue=own;
            if(!own){GUILayout.Label("Uses base phase timing.");return;}
            var root=prefix+(contextual?".timing":".segment");
            var flags=contextual?so.FindProperty(prefix+".timingFields"):null;
            void Field(VisualTimingFields bit,string label,string key,string legacy=null)
            {
                bool active=true;
                if(contextual)
                {
                    active=EditorGUILayout.ToggleLeft("Override "+label,(flags.intValue&(int)bit)!=0);
                    if(active)flags.intValue|=(int)bit;else flags.intValue&=~(int)bit;
                }
                if(!active)return;
                EditorGUILayout.PropertyField(so.FindProperty(!contextual && legacy!=null?prefix+"."+legacy:root+"."+key),new GUIContent(label));
                if(bit==VisualTimingFields.Segment)
                    EditorGUILayout.PropertyField(so.FindProperty(root+".end"),new GUIContent("Recovery end (normalized)"));
                if(bit==VisualTimingFields.Interruption)
                    EditorGUILayout.PropertyField(so.FindProperty(contextual?root+".restart":prefix+".allowSameActionRestart"),new GUIContent("Allow same-phase restart"));
            }
            Field(VisualTimingFields.Segment,"Release entry (normalized)","start");
            Field(VisualTimingFields.Speed,"Playback speed","speed","playbackSpeed");
            Field(VisualTimingFields.FadeIn,"Blend in (s)","fadeIn","blendInSeconds");
            Field(VisualTimingFields.FadeOut,"Recovery fade (s)","fadeOut","blendOutSeconds");
            Field(VisualTimingFields.Priority,"Priority","priority","priority");
            Field(VisualTimingFields.Interruption,"Interruptible","interruptible","interruptible");
            Field(VisualTimingFields.Recovery,"Loop until canceled","loop","loopUntilCanceled");
        }
        void MaskControls(SerializedObject so,string prefix,bool contextual)
        {
            if(contextual)EditorGUILayout.PropertyField(so.FindProperty(prefix+".inheritMask"),new GUIContent("Inherit base mask","Use base weights for regions without local overrides."));
            var body=so.FindProperty(prefix+".regions");
            bool parent=contextual && so.FindProperty(prefix+".inheritMask").boolValue;
            if(!parent)EditorGUILayout.PropertyField(so.FindProperty(prefix+".mask"),new GUIContent("Avatar mask (empty = full)"));
            contributionView=EditorGUILayout.Popup("Bone overlay",contributionView,new[]{"Selected context","Entire action phase","All action layers"});
            var enabled=body.FindPropertyRelative("enabled");
            enabled.boolValue=EditorGUILayout.Toggle("Weighted body regions",enabled.boolValue);
            bool disable=GUILayout.Toggle(noMask,"Compare full body in preview");
            if(disable!=noMask){noMask=disable;rebuild=true;}
            var mask=contextual?Variant.regions:Actions.Action(action).regions;
            var inherited=parent?Actions.Action(action).regions:null;
            var native=parent || !contextual?Actions.Action(action).mask:Variant.mask;
            MaskClipboard(so,prefix,contextual,mask,inherited,native);
            if(!enabled.boolValue)return;
            EditorGUILayout.PropertyField(body.FindPropertyRelative("source"),new GUIContent("Source bone mask","Optional shared path mask. Changes below are stored only on this assignment."));
            using(new EditorGUILayout.HorizontalScope())
            {
                preset=EditorGUILayout.Popup(preset,Presets);
                if(GUILayout.Button("Use preset",GUILayout.Width(74)))
                {
                    var rules=body.FindPropertyRelative("regions");rules.ClearArray();
                    body.FindPropertyRelative("bones").ClearArray();
                    foreach(AuthoringBodyRegion region in Enum.GetValues(typeof(AuthoringBodyRegion)))
                    {
                        if(region==AuthoringBodyRegion.Root)continue;
                        var part=RegionMaskSettings.Part(region);
                        bool left=part==AvatarMaskBodyPart.LeftArm || part==AvatarMaskBodyPart.LeftFingers;
                        bool right=part==AvatarMaskBodyPart.RightArm || part==AvatarMaskBodyPart.RightFingers;
                        bool upper=left||right||part==AvatarMaskBodyPart.Body||part==AvatarMaskBodyPart.Head;
                        float w=preset==0?1:preset==1?(upper?1:0):preset==2?(left||right?1:0):
                            preset==3?(right?1:0):preset==4?(left?1:0):preset==5?(left||right||part==AvatarMaskBodyPart.Body?1:0):upper?1:.4f;
                        WriteRule(rules,region,w);
                    }
                }
            }
            var regions=body.FindPropertyRelative("regions");
            float phase=0;
            if(composition!=null){composition.ReadRecordingPose(out var pose);phase=pose.phase;}
            BoneChannelWeights Value(AuthoringBodyRegion region)
            {
                var b=Representative(region);
                return mask.Resolve(region,b==HumanBodyBones.LastBone?"":FlattenedPoseStack.PathOf(previewRig.transform,previewRig.GetBoneTransform(b)),native,phase,b,inherited);
            }
            var all=(AuthoringBodyRegion[])Enum.GetValues(typeof(AuthoringBodyRegion));
            for(int i=1;i<all.Length;i+=2)
            using(new EditorGUILayout.HorizontalScope())
                for(int j=i;j<Math.Min(i+2,all.Length);j++)
                {
                    var region=all[j];var color=GUI.backgroundColor;
                    GUI.backgroundColor=Color.Lerp(new Color(.35f,.40f,.46f),new Color(.95f,.65f,.17f),Value(region).rotation);
                    if(GUILayout.Toggle(selectedRegion==region,ObjectNames.NicifyVariableName(region.ToString()),EditorStyles.miniButton))selectedRegion=region;
                    GUI.backgroundColor=color;
                }
            float current=Value(selectedRegion).rotation;
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(selectedRegion.ToString()),EditorStyles.boldLabel);
            float next=EditorGUILayout.Slider("Region influence (%)",current*100,0,100);
            if(Mathf.Abs(next-current*100)>.001f)WriteRule(regions,selectedRegion,next/100);
            channels=EditorGUILayout.Foldout(channels,"Position, rotation and scale",true);
            if(channels)
            {
                var original=Value(selectedRegion);
                float p=EditorGUILayout.Slider("Position",original.position,0,1);
                float r=EditorGUILayout.Slider("Rotation",original.rotation,0,1);
                float scale=EditorGUILayout.Slider("Scale",original.scale,0,1);
                if(p!=original.position || r!=original.rotation || scale!=original.scale)
                {
                    var item=WriteRule(regions,selectedRegion,r);
                    item.FindPropertyRelative("position").floatValue=p;item.FindPropertyRelative("scale").floatValue=scale;
                }
            }
            if(GUILayout.Button("Inherit region weight"))
                for(int i=regions.arraySize-1;i>=0;i--)
                    if(regions.GetArrayElementAtIndex(i).FindPropertyRelative("region").intValue==(int)selectedRegion)regions.DeleteArrayElementAtIndex(i);
            selectedBone=(HumanBodyBones)EditorGUILayout.EnumPopup("Individual bone",selectedBone);
            if(selectedBone!=HumanBodyBones.LastBone && previewRig.GetBoneTransform(selectedBone))
            {
                var bones=body.FindPropertyRelative("bones");
                SerializedProperty rule=null;
                for(int i=0;i<bones.arraySize;i++)
                    if(bones.GetArrayElementAtIndex(i).FindPropertyRelative("bone").intValue==(int)selectedBone)rule=bones.GetArrayElementAtIndex(i);
                bool set=EditorGUILayout.Toggle("Override bone",rule!=null);
                if(set && rule==null)
                {
                    bones.arraySize++;rule=bones.GetArrayElementAtIndex(bones.arraySize-1);
                    rule.FindPropertyRelative("bone").intValue=(int)selectedBone;
                    rule.FindPropertyRelative("weight").floatValue=Value(RegionMaskSettings.Region(selectedBone)).rotation;
                }
                if(set)EditorGUILayout.PropertyField(rule.FindPropertyRelative("weight"),new GUIContent("Bone weight"));
                else if(rule!=null)for(int i=bones.arraySize-1;i>=0;i--)
                    if(bones.GetArrayElementAtIndex(i).FindPropertyRelative("bone").intValue==(int)selectedBone)bones.DeleteArrayElementAtIndex(i);
            }
            var bone=Representative(selectedRegion);
            float global=contextual && !Variant.inheritWeight?Variant.weight:Actions.Action(action).blendWeight;
            GUILayout.Label($"Global weight   {global:P0}     Resolved region   {current:P0}",EditorStyles.miniLabel);
            if(composition!=null && bone!=HumanBodyBones.LastBone)
                GUILayout.Label($"Final rotation contribution   {composition.FinalRotationContribution(action,EffectiveContext,bone):P0}",EditorStyles.miniBoldLabel);
        }
        void MaskClipboard(SerializedObject so,string prefix,bool contextual,RegionMaskSettings mask,RegionMaskSettings inherited,AvatarMask native)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Copy mask"))
                {
                    var original=!mask.enabled && inherited!=null?inherited:mask;
                    var copy=JsonUtility.FromJson<RegionMaskSettings>(JsonUtility.ToJson(original));
                    if(mask.enabled && !mask.source && inherited!=null && inherited.enabled)
                    {
                        if(!copy.source)copy.source=inherited.source;
                        foreach(var r in inherited.regions)
                            if(!copy.regions.Any(x=>x.region==r.region))copy.regions.Add(r);
                        foreach(var b in inherited.bones)
                            if(!copy.bones.Any(x=>x.bone==b.bone) && !mask.regions.Any(x=>x.region==RegionMaskSettings.Region(b.bone)))copy.bones.Add(b);
                    }
                    maskClipboard=JsonUtility.ToJson(copy);copiedAvatar=native;
                }
                using(new EditorGUI.DisabledScope(string.IsNullOrEmpty(maskClipboard)))
                    if(GUILayout.Button("Paste mask"))
                    {
                        so.ApplyModifiedProperties();Undo.RecordObject(so.targetObject,"Paste assignment weights");
                        JsonUtility.FromJsonOverwrite(maskClipboard,mask);so.Update();
                        so.FindProperty(prefix+".mask").objectReferenceValue=copiedAvatar;
                        if(contextual)so.FindProperty(prefix+".inheritMask").boolValue=false;
                        rebuild=true;
                    }
            }
        }
        static SerializedProperty WriteRule(SerializedProperty list,AuthoringBodyRegion region,float value)
        {
            SerializedProperty item=null;
            for(int i=0;i<list.arraySize;i++)
                if(list.GetArrayElementAtIndex(i).FindPropertyRelative("region").intValue==(int)region)item=list.GetArrayElementAtIndex(i);
            if(item==null){list.arraySize++;item=list.GetArrayElementAtIndex(list.arraySize-1);item.FindPropertyRelative("clipInfluence").animationCurveValue=AnimationCurve.Linear(0,1,1,1);}
            item.FindPropertyRelative("region").intValue=(int)region;
            foreach(var key in new[]{"position","rotation","scale"})item.FindPropertyRelative(key).floatValue=value;
            return item;
        }
    }
}
