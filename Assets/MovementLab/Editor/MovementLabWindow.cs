using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using AirflowPrototype;
using Windrunner.Production.Input;

namespace Windrunner.MovementLab.Editor
{
    public sealed class MovementLabWindow : EditorWindow
    {
        public const string Folder = "Assets/MovementLab";
        public const string ScenePath = Folder + "/MovementGym.unity";
        private GymLane lane;
        private Vector2 scroll;
        [MenuItem("Tools/Windrunner/Movement Lab")]
        public static void Open() => GetWindow<MovementLabWindow>("Movement Lab");
        private void OnGUI()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
            {
                if (GUILayout.Button("Open Movement Gym") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            var player = FindAnyObjectByType<MovementLabPlayer>();
            if (player == null) return;
            var lanes = player.lanes;
            int selected = Mathf.Max(0, Array.IndexOf(lanes, lane));
            lane = lanes[EditorGUILayout.Popup("Testing section", selected, lanes.Select(x=>x.name).ToArray())];
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Teleport to selected section")) player.Teleport(Array.IndexOf(lanes,lane));
                if (GUILayout.Button("Reset measurements")) player.ResetSession();
                EditorGUILayout.HelpBox("Exit Play Mode to edit saved geometry and technique profiles.",MessageType.Info);
                return;
            }
            if (GUILayout.Button("Duplicate section at offset"))
            {
                Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
                var copy=Instantiate(lane,lane.transform.parent);copy.name=lane.name+" extension";
                copy.transform.localPosition+=Vector3.right*45;
                Undo.RegisterCreatedObjectUndo(copy.gameObject,"Duplicate movement module");
                Undo.RecordObject(player,"Bind duplicated module");
                player.lanes=player.lanes.Concat(new[]{copy}).ToArray();
                player.volumes=player.volumes.Concat(copy.GetComponentsInChildren<WindTunnel>(true)).ToArray();
                lane=copy; Selection.activeGameObject=copy.gameObject;
                Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(copy.gameObject.scene);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            Vector3 anchor = EditorGUILayout.Vector3Field("Section origin",lane.transform.localPosition);
            LanePreset preset = (LanePreset)EditorGUILayout.EnumPopup("Configuration",lane.preset);
            float spacing = EditorGUILayout.FloatField("Obstacle spacing (m)",lane.spacing);
            float runUp = EditorGUILayout.FloatField("Run-up length (m)",lane.runUp);
            bool isSlope=lane.kind==LaneKind.Slopes;
            float rise=isSlope?lane.elevation:EditorGUILayout.FloatField("Elevation change (m)",lane.elevation);
            float approach = EditorGUILayout.Slider("Approach angle",lane.approachAngle,-75,75);
            float decline=isSlope?EditorGUILayout.Slider("Descent inclination",lane.declineAngle,2,45):lane.declineAngle;
            float slope=isSlope?EditorGUILayout.Slider("Incline angle",lane.slopeAngle,2,45):lane.slopeAngle;
            var target=lane.kind==LaneKind.Sling || lane.kind==LaneKind.Combined ? EditorGUILayout.Vector3Field("Sling target offset",lane.targetOffset):lane.targetOffset;
            var size=lane.kind==LaneKind.Wind || lane.kind==LaneKind.Combined ? EditorGUILayout.Vector3Field("Tunnel dimensions",lane.tunnelSize):lane.tunnelSize;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RegisterFullObjectHierarchyUndo(lane.gameObject,"Adjust movement lane");
                lane.transform.localPosition=anchor;
                lane.runUp=Mathf.Max(15,runUp); lane.spacing=Mathf.Max(.5f,spacing); lane.elevation=rise; lane.approachAngle=approach;
                lane.declineAngle=decline; lane.slopeAngle=slope; lane.targetOffset=target; lane.tunnelSize=Vector3.Max(Vector3.one,size);
                if(preset!=lane.preset) lane.SetPreset(preset);
                lane.Apply(); EditorSceneManager.MarkSceneDirty(lane.gameObject.scene);
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Experimental profile", EditorStyles.boldLabel);
            if(GUILayout.Button("Regenerate selected layout"))
            {
                Undo.RegisterFullObjectHierarchyUndo(lane.gameObject,"Regenerate gym station");lane.Apply();
                EditorSceneManager.MarkSceneDirty(lane.gameObject.scene);
            }
            if(GUILayout.Button("Validate station pads")) Validate(player);
            foreach(bool burst in new[]{false,true})
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(burst?"Entry impulse":"Continuous wind",GUILayout.Width(100));
                foreach(var level in (MovementLabProfile.WindLevel[])Enum.GetValues(typeof(MovementLabProfile.WindLevel)))
                {
                    if(!GUILayout.Button(level.ToString()))continue;
                    Undo.RecordObject(player.profile,"Change experimental wind preset");
                    if(burst)player.profile.impulse=MovementLabProfile.Preset(true,level);
                    else player.profile.continuous=MovementLabProfile.Preset(false,level);
                    EditorUtility.SetDirty(player.profile);
                }
                EditorGUILayout.EndHorizontal();
            }
            var serialized = new SerializedObject(player.profile);
            foreach (var field in new[]{"continuous","impulse","combinedSpeedCap","flightSpeed","flightSprint"})
                EditorGUILayout.PropertyField(serialized.FindProperty(field),true);
            serialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private static void Validate(MovementLabPlayer player)
        {
            Physics.SyncTransforms();
            foreach(var lane in player.lanes)
            {
                if(!Physics.Raycast(lane.destination.position,Vector3.down,out var hit,3) || hit.transform!=lane.launch)
                    throw new InvalidOperationException("Spawn misses approach deck: "+lane.name);
                if(!Physics.Raycast(lane.endGate.position+Vector3.up,Vector3.down,out hit,3) || hit.transform!=lane.landing)
                    throw new InvalidOperationException("Finish misses landing: "+lane.name);
            }
            Debug.Log("Movement Lab: all station spawns and finish stripes have solid supporting decks.");
        }
        public static void RedesignGym()
        {
            if(Application.isPlaying || Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes before baking the gym.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var player=FindAnyObjectByType<MovementLabPlayer>();
            ConfigureLayouts(player);
            player.profile.continuous=MovementLabProfile.Preset(false,MovementLabProfile.WindLevel.Standard);
            player.profile.impulse=MovementLabProfile.Preset(true,MovementLabProfile.WindLevel.Standard);
            player.motor.transform.position=player.lanes[0].destination.position;
            Validate(player);
            EditorUtility.SetDirty(player.profile);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Gym save failed");
        }
        private static void ConfigureLayouts(MovementLabPlayer player)
        {
            using var motorFields=new SerializedObject(player.motor);
            player.facingRoot=(Transform)motorFields.FindProperty("visualRoot").objectReferenceValue;
            player.dash=player.motor.GetComponent<AirDash>();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Deck.mat");
            foreach(var lane in player.lanes)
            {
                int count=lane.kind==LaneKind.Combined?6:lane.kind==LaneKind.Slopes?2:0;
                if(lane.sections.Length!=count)
                {
                    if(lane.sections.Length!=0) throw new InvalidOperationException("Inspect custom sections first.");
                    lane.sections=new Transform[count];
                    for(int i=0;i<count;i++)lane.sections[i]=Block(lane.transform,"Course surface "+(i+1),mat,Vector3.zero,Vector3.one);
                }
                lane.SetPreset(lane.kind==LaneKind.Combined?LanePreset.Long:LanePreset.Short);
                lane.Apply();
            }
        }
        // One-shot authoring entry point. Save an independent copy before editing any scene object.
        public static void CreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Gym already exists.");
            if (Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes first.");
            if (!AssetDatabase.CopyAsset("Assets/AirflowPrototype/Generated/Batch1_Playground.unity", ScenePath)) throw new InvalidOperationException("Cannot copy production composition.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var motor = FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Single();
            var host = FindObjectsByType<WindrunnerInputHost>(FindObjectsSortMode.None).Single();
            host.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>(Folder+"/MovementLabGameplay.inputactions"),host.PauseView,host.UiModule);
            var profile = ScriptableObject.CreateInstance<MovementLabProfile>();
            AssetDatabase.CreateAsset(profile,Folder+"/LabProfile.asset");
            var platform = Material("Deck",new Color(.15f,.26f,.34f));
            var accent = Material("Launch", new Color(.1f,.7f,.6f));
            var gold = Material("Destination", new Color(.93f,.61f,.12f));
            var root = new GameObject("Movement Gym"); root.transform.position = new Vector3(2400,0,0);
            Block(root.transform,"Recovery floor",platform,new Vector3(0,-5,0),new Vector3(1000,2,1000));
            var player = motor.gameObject.AddComponent<MovementLabPlayer>();
            player.motor=motor; player.power=motor.GetComponent<AirPower>(); player.controller=motor.GetComponent<CharacterController>();
            player.sling=motor.GetComponent<AirSlingController>(); player.inputHost=host; player.profile=profile;
            player.trail=new GameObject("Measured trajectory").AddComponent<LineRenderer>(); player.trail.transform.SetParent(root.transform,false);
            player.trail.sharedMaterial=gold; player.trail.widthMultiplier=.18f; player.trail.positionCount=0;
            var kinds = (LaneKind[])Enum.GetValues(typeof(LaneKind));
            player.lanes=new GymLane[kinds.Length];
            for(int i=0;i<kinds.Length;i++)
            {
                var module = Child(root.transform,kinds[i].ToString());
                module.localPosition = new Vector3(-375+(i%5)*165,i==9?200:i==4?70:0,i<5?-420:35);
                var lane=module.gameObject.AddComponent<GymLane>(); player.lanes[i]=lane; lane.kind=kinds[i];
                lane.SetPreset(i==9?LanePreset.Extreme:i==7?LanePreset.Long:i%2==0?LanePreset.Short:LanePreset.Medium);
                lane.destination=Child(module,"Respawn");
                lane.launch=Block(module,"Approach deck",platform,Vector3.zero,Vector3.one);
                lane.landing=Block(module,"Landing deck",gold,Vector3.zero,Vector3.one);
                lane.slope=Block(module,"Launch ramp",accent,Vector3.zero,Vector3.one);
                lane.target=GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; lane.target.SetParent(module,false);lane.target.name="Sling target";
                lane.target.GetComponent<Renderer>().sharedMaterial=gold;lane.target.gameObject.AddComponent<AirSlingNode>();
                lane.startGate=Block(module,"Launch stripe",gold,Vector3.zero,new Vector3(18,.08f,.5f),false);
                lane.endGate=Block(module,"Landing stripe",accent,Vector3.zero,new Vector3(26,.08f,.5f),false);
                lane.sign=Child(module,"Station label");lane.label=lane.sign.gameObject.AddComponent<TextMesh>();
                lane.label.fontSize=64;lane.label.characterSize=.12f;lane.label.anchor=TextAnchor.MiddleCenter;
                lane.label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");lane.label.GetComponent<Renderer>().sharedMaterial=lane.label.font.material;
                lane.continuous=Tunnel(module,false,accent,profile); lane.impulse=Tunnel(module,true,gold,profile);
                lane.Apply();
            }
            ConfigureRunways(player);
            ConfigureLayouts(player);
            player.volumes=root.GetComponentsInChildren<WindTunnel>(true);
            motor.transform.position=player.lanes[0].destination.position;
            KeepGymComposition(scene, player);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Gym save failed.");
            AssetDatabase.SaveAssets();
        }
        private static void ConfigureRunways(MovementLabPlayer player)
        {
            foreach(var lane in player.lanes)
            {
                if(lane.kind==LaneKind.Combined && lane.relayTargets.Length==0)
                {
                    lane.relayTargets=new Transform[7];
                    for(int i=0;i<lane.relayTargets.Length;i++)
                    {
                        var relay=Instantiate(lane.target,lane.transform);
                        relay.name="Sling relay "+(i+1);lane.relayTargets[i]=relay;
                    }
                }
                if(lane.distanceMarkers.Length==0)
                {
                    lane.distanceMarkers=new TextMesh[4];
                    for(int i=0;i<4;i++)
                    {
                        var marker=Instantiate(lane.label,lane.transform);
                        marker.name="Distance marker "+(i+1);marker.characterSize=.08f;
                        lane.distanceMarkers[i]=marker;
                    }
                }
                if(lane.kind==LaneKind.Combined)lane.transform.localPosition=new Vector3(140,200,20);
                if(lane.kind==LaneKind.HighSpeed)lane.transform.localPosition=new Vector3(-460,55,80);
                lane.Apply();
            }
        }
        // Keep serialized dependencies transitively, including references into another scene root.
        // Only the independent gym copy can enter this path.
        private static void KeepGymComposition(UnityEngine.SceneManagement.Scene scene, MovementLabPlayer player)
        {
            if (scene.path!=ScenePath) throw new InvalidOperationException("Expected the movement gym copy.");
            var roots=scene.GetRootGameObjects();
            var retained=new HashSet<GameObject>();
            var pending=new Queue<GameObject>();
            void Retain(GameObject obj)
            {
                if (obj!=null && obj.scene==scene && retained.Add(obj.transform.root.gameObject))
                    pending.Enqueue(obj.transform.root.gameObject);
            }
            Retain(player.gameObject);Retain(player.motor.ViewCamera.gameObject);Retain(player.inputHost.gameObject);
            Retain(player.lanes[0].gameObject);
            foreach(var root in roots)
                if(root.GetComponentInChildren<Light>(true)?.type==LightType.Directional ||
                    root.GetComponentInChildren<Canvas>(true)!=null ||
                    root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)!=null ||
                    root.GetComponent<UnityEngine.Rendering.Volume>()!=null) Retain(root);
            while(pending.Count>0)
            {
                foreach(var component in pending.Dequeue().GetComponentsInChildren<Component>(true))
                {
                    if(component==null) continue;
                    using var serialized=new SerializedObject(component);var it=serialized.GetIterator();
                    while(it.Next(true))
                    {
                        if(it.propertyType!=SerializedPropertyType.ObjectReference) continue;
                        var reference=it.objectReferenceValue;
                        if(reference is Component c) Retain(c.gameObject);
                        else if(reference is GameObject go) Retain(go);
                    }
                }
            }
            foreach(var root in roots) if(!retained.Contains(root)) DestroyImmediate(root);
        }
        private static WindTunnel Tunnel(Transform parent,bool impulse,Material material, MovementLabProfile profile)
        {
            var t=Child(parent,impulse?"Entry impulse":"Continuous wind");
            var volume=t.gameObject.AddComponent<WindTunnel>(); volume.entryImpulse=impulse; volume.profile=profile;
            foreach(float z in new[]{-.5f,.5f})
            {
                foreach(float x in new[]{-.5f,.5f})
                    Block(t,"Boundary",material,new Vector3(x,0,z),new Vector3(.012f,1,.008f),false);
                Block(t,"Canopy",material,new Vector3(0,.5f,z),new Vector3(1,.014f,.008f),false);
            }
            return volume;
        }
        private static Transform Child(Transform parent,string name)
        { var child=new GameObject(name).transform;child.SetParent(parent,false);return child; }
        private static Transform Block(Transform parent,string name,Material mat,Vector3 pos,Vector3 size,bool solid=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=pos;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid) DestroyImmediate(obj.GetComponent<Collider>());
            return obj.transform;
        }
        private static Material Material(string name, Color color)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;
            AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");return mat;
        }
    }
}
