using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace AirflowPrototype.Editor
{
    public sealed class CharacterTuningStudio : EditorWindow
    {
        private GameObject character;
        private CharacterTuningPreset preset;
        private CharacterTuningSession session;
        private string search="", category="All", message="";
        private Vector2 scroll;
        private bool changesOnly;
        private AirSlingDeathReward[] encounterRewards=Array.Empty<AirSlingDeathReward>();
        private static readonly string[] Categories={"All","Movement","Jumping","Aerial","Air Dash","Air Sling","Air Cast","Camera","Animation","Resources","Feedback","Water","Other"};
        [MenuItem("Tools/Windrunner/Character Tuning")]
        public static void Open() => GetWindow<CharacterTuningStudio>("Character Tuning");
        private void OnEnable() { minSize=new Vector2(960,640);EditorApplication.playModeStateChanged+=ModeChanged; Undo.undoRedoPerformed+=Repaint; }
        private void OnDisable() { EditorApplication.playModeStateChanged-=ModeChanged; Undo.undoRedoPerformed-=Repaint; session?.Dispose();session=null; }
        private void ModeChanged(PlayModeStateChange state)
        {
            // A play transition cannot silently persist a draft or leave scene references pointing at it.
            if(state==PlayModeStateChange.ExitingPlayMode || state==PlayModeStateChange.ExitingEditMode) {session?.Dispose();session=null;message="Mode changed: unsaved drafts discarded; saved assets are unchanged.";}
            Repaint();
        }
        private void Reload() { session?.Dispose();session=character!=null?new CharacterTuningSession(character):null;encounterRewards=Object.FindObjectsByType<AirSlingDeathReward>(FindObjectsInactive.Include,FindObjectsSortMode.None); }
        private void Do(Action action) {try {action();message="Operation completed.";}catch(Exception e){message=e.Message;Debug.LogWarning("[Character Tuning] "+e.Message);} }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit isolated drafts, preview supported live values, then explicitly Save. Existing module assets remain the runtime authorities. No asset scans run during gameplay. Select the player root, not its mesh.",MessageType.Info);
            var next=(GameObject)EditorGUILayout.ObjectField("Character",character,typeof(GameObject),true);
            if(next!=character){session?.Dispose();session=null;character=next;}
            preset=(CharacterTuningPreset)EditorGUILayout.ObjectField("Preset / character bindings",preset,typeof(CharacterTuningPreset),false);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Use selected player")){character=Selection.activeGameObject;Do(Reload);}
                if(GUILayout.Button("Load / discard drafts"))Do(Reload);
                if(GUILayout.Button("Apply preset") && preset!=null)Do(ApplyPreset);
            }
            if(GUILayout.Button("Animation Authoring Studio V2")) DaggerMotionPreview.Open();
            if(session==null){EditorGUILayout.LabelField(message,EditorStyles.wordWrappedLabel);return;}
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Validate")) {var issues=session.Validate();message=issues.Count==0?"Profile ranges and cross-field checks passed. Missing action clips use production fallback; visual parity still requires testing.":string.Join("\n",issues);}
                if(GUILayout.Button("Production-only audit")){var issues=CharacterProductionDiagnostics.Validate(character);message=issues.Count==0?"Production composition valid"+(Application.isPlaying?"; portable owner active.":"; enter Play to check active ownership."):string.Join("\n",issues);}
                if(GUILayout.Button("Revert drafts"))Do(session.Revert);
                using(new EditorGUI.DisabledScope(session.SaveRestriction!=null))
                    if(GUILayout.Button("Save changes to assets"))Do(session.Save);
                if(GUILayout.Button("Capture preset"))Do(Capture);
                if(GUILayout.Button("Duplicate character profiles"))Do(Duplicate);
            }
            using(new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                bool preview=EditorGUILayout.Toggle("Live preview (safe modules only)",session.Previewing);
                if(preview!=session.Previewing)Do(()=>session.Preview(preview));
            }
            EditorGUILayout.HelpBox("Live: movement/jump/camera/resource values, Dash, Sling, Cast, targeting and water. Animation graphs, feedback/audio pools and other cached modules require explicit Save and a new Play session. Revert/closing restores references. Play-mode component and node overrides are never saved by this window. Baseline comparison is against the loaded authored values, not guessed C# defaults.",MessageType.None);
            if(!string.IsNullOrEmpty(session.PreviewIssue))EditorGUILayout.HelpBox("Preview stopped: "+session.PreviewIssue,MessageType.Warning);
            DrawScopeAndSceneControls();
            search=EditorGUILayout.TextField("Search name / owner / tooltip",search);
            changesOnly=EditorGUILayout.Toggle("Compare: changed values only",changesOnly);
            category=Categories[EditorGUILayout.Popup("Category",Array.IndexOf(Categories,category),Categories)];
            if(session.SaveRestriction!=null)EditorGUILayout.HelpBox(session.SaveRestriction,MessageType.Warning);
            if(!string.IsNullOrEmpty(message))EditorGUILayout.LabelField(message,EditorStyles.wordWrappedLabel);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            bool edited=false;
            foreach(var entry in session.Entries)
            {
                using(var serialized=new SerializedObject(entry.Draft))
                using(var baseline=new SerializedObject(entry.Baseline))
                {
                    serialized.Update(); var property=serialized.GetIterator();string section="";bool shown=false;
                    bool enter=true;
                    while(property.NextVisible(enter))
                    {
                        enter=false;
                        if(property.name=="m_Script")continue;
                        if(entry.Source is AirCastSettings && new[]{"unchargedRingRadius","chargedRingRadius","ringRotationSpeed","ringWidth"}.Contains(property.name))continue;
                        var field=entry.Draft.GetType().GetField(property.name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        var header=field?.GetCustomAttribute<HeaderAttribute>();if(header!=null)section=header.header;
                        string group=Group(entry.Source.GetType().Name,section);
                        var original=baseline.FindProperty(property.propertyPath);
                        bool changed=original!=null && !SerializedProperty.DataEquals(property,original);
                        if(changesOnly && !changed)continue;
                        if(category!="All" && category!=group)continue;
                        string owner=string.Join("; ",entry.Owners);
                        if(!string.IsNullOrWhiteSpace(search) && !(property.displayName+" "+property.tooltip+" "+entry.Source.name+" "+owner).ToLowerInvariant().Contains(search.ToLowerInvariant()))continue;
                        if(!shown){EditorGUILayout.Space();EditorGUILayout.LabelField(entry.Source.name+" â€” "+entry.Source.GetType().Name,EditorStyles.boldLabel);EditorGUILayout.LabelField(owner.Length>0?owner:"Referenced module of another profile",EditorStyles.wordWrappedMiniLabel);EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(entry.Source),EditorStyles.miniLabel);shown=true;}
                        string unit=Units(property.name,section);
                        string tooltip=property.tooltip+"\nOwner: "+owner+"\nUnits: "+unit+"\nLoaded baseline: "+Value(original);
                        using(new EditorGUI.DisabledScope(Application.isPlaying && !entry.PreviewSafe))
                            EditorGUILayout.PropertyField(property,new GUIContent(property.displayName+" ["+group+"]",tooltip),true);
                        if(changed)EditorGUILayout.LabelField("Loaded baseline: "+Value(original),EditorStyles.miniLabel);
                    }
                    edited|=serialized.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndScrollView();
            if(edited && session.Previewing)
            {
                var issues=session.Validate();
                if(issues.Count>0){session.Preview(false);message="Live preview stopped: "+string.Join("\n",issues);}
            }
        }
        private bool showScope;
        private void DrawScopeAndSceneControls()
        {
            showScope=EditorGUILayout.Foldout(showScope,"Effective values, encounter overrides and scene presentation",true);
            if(!showScope)return;
            var motor=character.GetComponent<PlayerMotor>();var reader=character.GetComponent<PlayerInputReader>();
            if(motor!=null && motor.Settings!=null && reader!=null)
                EditorGUILayout.LabelField($"Camera effective sensitivity: mouse {motor.Settings.mouseSensitivity*reader.MouseSensitivity:G4}; stick {motor.Settings.gamepadLookDegreesPerSecond*reader.StickSensitivity:G4} deg/s (profile × user preference).");
            EditorGUILayout.HelpBox("Draft reward policy applies only to listed character/camera owners. Encounter-owned node death rewards below are excluded from preview and presets. Node timing/health overrides remain encounter authoring. Global feedback catalog, voice limits and user input preferences remain service-owned.",MessageType.Info);
            foreach(var reward in encounterRewards.Where(r=>r!=null))
                using(var so=new SerializedObject(reward))
                {
                    var power=so.FindProperty("airPower").objectReferenceValue as AirPower;
                    if(power==null || power.gameObject!=character)continue;
                    var settings=so.FindProperty("flowSettings").objectReferenceValue as AirFlowHitSettings;
                    float multiplier=so.FindProperty("airPowerRewardMultiplier").floatValue;
                    EditorGUILayout.LabelField(reward.name+" — encounter reward: "+(settings!=null?(settings.airPowerRestore*multiplier).ToString("G4")+" AirPower; "+settings.name:"MISSING profile"));
                }
            var trails=character.GetComponentInChildren<PlayerHandAirTrails>(true);
            if(trails==null)return;
            EditorGUILayout.HelpBox("Hand-trail scene overrides: Edit Mode only. These edits use standard Unity Undo and require Save Scene/Prefab; they are separate from module draft Save and are not copied by profile presets.",MessageType.None);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            using(var so=new SerializedObject(trails))
            {
                foreach(string name in new[]{"airNodeEmitDuration","airNodeFadeDuration","slingImpactEmitDuration","slingImpactFadeDuration","trailMaterial","startWidth","endWidth","minimumVertexDistance","cornerVertices","capVertices","trailColor","scaleAirNodeWidthByRewardIntensity","minimumRewardWidthMultiplier"})
                    EditorGUILayout.PropertyField(so.FindProperty(name),true);
                if(!Application.isPlaying && so.ApplyModifiedProperties())PrefabUtility.RecordPrefabInstancePropertyModifications(trails);
            }
        }
        internal static string Group(string type,string section)
        {
            if(type=="AirflowMovementSettings")
            {
                if(section=="Jump")return "Jumping";if(section=="Air Movement")return "Aerial";
                if(section.StartsWith("Air Dash"))return "Air Dash";if(section=="Air Power")return "Resources";
                if(section=="Camera"||section=="Speed Feedback")return "Camera";return "Movement";
            }
            if(type.Contains("Animation")||type.Contains("GroundMotion")||type.Contains("CharacterAction")||type.Contains("Pose"))return "Animation";
            if(type.Contains("Sling"))return "Air Sling";
            if(type.Contains("Cast")||type.Contains("Targeting"))return "Air Cast";
            if(type.Contains("Water"))return "Water";
            if(type.Contains("FlowHit"))return "Resources";
            if(type.Contains("Feel")||type.Contains("Audio")||type.Contains("Feedback")||type.Contains("Presentation")||type.Contains("Glow")||type.Contains("Dust")||type.Contains("UI"))return "Feedback";
            return "Other";
        }
        private static string Units(string name,string section)
        {
            string n=name.ToLowerInvariant();
            if(n=="mousesensitivity")return "degrees per mouse input delta, before user sensitivity multiplier";
            if(n.Contains("degreespersecond"))return "degrees / second";
            if(n.Contains("playbackspeed"))return "clip playback multiplier";
            if(n.Contains("fraction")||n.Contains("multiplier")||n.Contains("weight")||n.EndsWith("01")||n.Contains("retention"))return "dimensionless ratio";
            if(n.Contains("drainpersecond")||n=="passiverecoverypersecond")return "AirPower units / second";
            if(n.Contains("cost")||n=="maxairpower"||n.Contains("airpowerreward"))return "AirPower units";
            if(n.Contains("duration")||n.Contains("delay")||n.Contains("time")||n.Contains("leeway"))return "seconds (timing values)";
            if(n.Contains("fov")||n.Contains("degrees")||n.Contains("pitch")||n.Contains("angle"))return "degrees; response fields are rates";
            if(n.Contains("acceleration")||n.Contains("deceleration")||n=="gravity")return "world units / second squared";
            if(n.Contains("sharpness")||n.Contains("response"))return "exponential response rate / second";
            if(n.Contains("distance")||n.Contains("height")||n.Contains("radius")||n.Contains("padding"))return "world units";
            if(n.Contains("speed")&&!n.Contains("multiplier"))return "world units / second for translation; see authored tooltip for playback/angular rates";
            return "authored units; range and meaning follow the module tooltip/header ("+section+")";
        }
        private static string Value(SerializedProperty p)
        {
            if(p==null)return "unavailable";
            switch(p.propertyType){case SerializedPropertyType.Float:return p.floatValue.ToString("G6");case SerializedPropertyType.Integer:return p.intValue.ToString();case SerializedPropertyType.Boolean:return p.boolValue.ToString();case SerializedPropertyType.ObjectReference:return p.objectReferenceValue!=null?p.objectReferenceValue.name:"None";case SerializedPropertyType.String:return p.stringValue;default:return "compound value â€” use Revert drafts for exact baseline";}
        }
        private void Capture()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Capture presets in Edit Mode after explicitly saving desired drafts.");
            string path=EditorUtility.SaveFilePanelInProject("Save character preset",character.name+"Tuning","asset","Presets index existing profiles; they do not copy gameplay logic.");if(string.IsNullOrEmpty(path))return;
            var asset=session.Capture(character.name);AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssetIfDirty(asset);preset=asset;
        }
        private void ApplyPreset()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Apply character binding presets only in Edit Mode.");
            if(character==null)throw new InvalidOperationException("Select the target character first.");
            if(session==null)Reload();
            session.ApplyPreset(preset);
            Reload();
        }
        private void Duplicate()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Duplicate presets in Edit Mode.");
            var errors=session.Validate();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            string path=EditorUtility.SaveFilePanelInProject("Duplicate character profile set",character.name+"Variant","asset","Creates a preset and independent module profiles beside it.");if(string.IsNullOrEmpty(path))return;
            preset=session.DuplicateProfiles(path);
        }
    }
}
