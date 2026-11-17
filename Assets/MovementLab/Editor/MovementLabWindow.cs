using System;
using System.Linq;
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
            lane = lanes[EditorGUILayout.Popup("Testing section", selected, lanes.Select(x=>x.kind.ToString()).ToArray())];
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Teleport to selected section")) player.Teleport(Array.IndexOf(lanes,lane));
                if (GUILayout.Button("Reset measurements")) player.ResetSession();
                player.experimentalPairEquipped = EditorGUILayout.Toggle("Equip Overdrive pair", player.experimentalPairEquipped);
                EditorGUILayout.HelpBox("Exit Play Mode to edit saved geometry and technique profiles.",MessageType.Info);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            LanePreset preset = (LanePreset)EditorGUILayout.EnumPopup("Configuration",lane.preset);
            float spacing = EditorGUILayout.FloatField("Obstacle spacing (m)",lane.spacing);
            float runUp = EditorGUILayout.FloatField("Run-up length (m)",lane.runUp);
            float rise = EditorGUILayout.FloatField("Elevation change (m)",lane.elevation);
            float approach = EditorGUILayout.Slider("Approach angle",lane.approachAngle,-75,75);
            float slope = EditorGUILayout.Slider("Launch slope",lane.slopeAngle,-60,75);
            var target = EditorGUILayout.Vector3Field("Sling target offset",lane.targetOffset);
            var size = EditorGUILayout.Vector3Field("Tunnel dimensions",lane.tunnelSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RegisterFullObjectHierarchyUndo(lane.gameObject,"Adjust movement lane");
                lane.runUp=Mathf.Max(15,runUp); lane.spacing=Mathf.Max(4,spacing); lane.elevation=rise; lane.approachAngle=approach;
                lane.slopeAngle=slope; lane.targetOffset=target; lane.tunnelSize=Vector3.Max(Vector3.one,size);
                if(preset!=lane.preset) lane.SetPreset(preset);
                lane.Apply(); EditorSceneManager.MarkSceneDirty(lane.gameObject.scene);
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Experimental profile", EditorStyles.boldLabel);
            var serialized = new SerializedObject(player.profile);
            foreach (var field in new[]{"continuous","impulse","overdrive","combinedSpeedCap"})
                EditorGUILayout.PropertyField(serialized.FindProperty(field),true);
            serialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
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
            Block(root.transform,"Recovery floor",platform,new Vector3(0,-14,0),new Vector3(1000,2,1000));
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
            player.volumes=root.GetComponentsInChildren<WindTunnel>(true);
            motor.transform.position=player.lanes[0].destination.position;
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Gym save failed.");
            AssetDatabase.SaveAssets();
        }
        public static void BakeRunways()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var player = FindAnyObjectByType<MovementLabPlayer>();
            foreach(var lane in player.lanes)
            {
                lane.SetPreset(lane.preset);
                if (lane.kind == LaneKind.Combined)
                    lane.transform.localPosition = new Vector3(140, 200, 20);
                if (lane.kind == LaneKind.HighSpeed)
                {
                    lane.approachAngle = 0;
                    lane.runUp = 370;
                    lane.transform.localPosition = new Vector3(-460, 55, 80);
                    lane.tunnelSize = new Vector3(16, 12, 100);
                }
                lane.Apply();
            }
            player.transform.position = player.lanes[0].destination.position;
            EditorSceneManager.SaveScene(scene);
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
