using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Windrunner.TerrainAuthoring.Editor
{
    public sealed class TerrainStudioWindow : EditorWindow
    {
        static readonly string[] Sections={"Automatic Terrain Painting","Terrain Materials","Height and Slope Rules","Road and Path Authoring","Carriage Road Wear","Manual Artistic Overrides","Automatic Updates","Walkability Visualization","Diagnostics and Validation"};
        [SerializeField] Terrain terrain;
        [SerializeField] SurfaceProfile surface;
        [SerializeField] RoadProfile roadProfile;
        [SerializeField] StudioRoad road;
        [SerializeField] CharacterController player;
        [SerializeField] Vector2 center;
        [SerializeField] Vector2 size=new Vector2(80,80);
        [SerializeField] int section;
        bool pick,walk;
        float opacity=1;
        string message;
        Vector2 scroll;
        Texture2D preview;
        [MenuItem("Tools/Windrunner/Terrain Studio")]
        public static void Open()=>GetWindow<TerrainStudioWindow>("Terrain Studio");
        void OnEnable()
        {
            SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=Repaint;
            if(!surface)surface=StudioSetup.Preset<SurfaceProfile>("Basin surfaces");
            if(!roadProfile)roadProfile=StudioSetup.Preset<RoadProfile>("Carriage road");
            if(!terrain)SetTerrain(Selection.activeGameObject?Selection.activeGameObject.GetComponent<Terrain>():null);
            if(!terrain)SetTerrain(Terrain.activeTerrain);
        }
        void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=Repaint;if(preview)DestroyImmediate(preview);SceneView.RepaintAll();}
        void SetTerrain(Terrain t)
        {
            terrain=t;if(!t||!t.terrainData)return;
            var p=t.GetPosition();var s=t.terrainData.size;center=new Vector2(p.x+s.x/2,p.z+s.z/2);
            player=t.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CharacterController>()).FirstOrDefault(c=>c.GetComponent("PlayerMotor"));
        }
        StudioTile Tile=>terrain?terrain.GetComponentInChildren<StudioTile>(true):null;
        Rect WorldRect=>new Rect(center-size*.5f,size);
        RectInt Region=>TerrainPainter.Pixels(terrain,WorldRect);
        static bool Button(string text,string tip)=>GUILayout.Button(new GUIContent(text,tip));
        void OnGUI()
        {
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var next=(Terrain)EditorGUILayout.ObjectField("Target terrain",terrain,typeof(Terrain),true);if(next!=terrain)SetTerrain(next);
                if(!terrain||!terrain.terrainData){EditorGUILayout.HelpBox("Open Zone 01 and select its Terrain.",MessageType.Info);return;}
                var tile=Tile;
                scroll=EditorGUILayout.BeginScrollView(scroll);
                try
                {
                    if(!tile)
                    {
                        surface=(SurfaceProfile)EditorGUILayout.ObjectField("Surface preset",surface,typeof(SurfaceProfile),false);
                        EditorGUILayout.HelpBox("Set up a working terrain copy. Setup retains the original asset and connects both Terrain and TerrainCollider to the copy.",MessageType.Info);
                        if(Button("Attach editable working copy","Creates new assets beside this TerrainData; no painting yet."))
                            StudioSetup.Attach(terrain,surface,System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(terrain.terrainData)).Replace('\\','/'));
                    }
                    else DrawStudio(tile);
                    if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
                }
                catch(ExitGUIException){throw;}
                catch(Exception e){message=e.Message;}
                finally{EditorGUILayout.EndScrollView();}
            }
        }
        void DrawStudio(StudioTile tile)
        {
            var s=tile.state;
            if(!s||s.working!=terrain.terrainData){EditorGUILayout.HelpBox("Repair Studio working-copy references before editing.",MessageType.Error);return;}
            section=EditorGUILayout.Popup("Workspace",section,Sections);
            EditorGUILayout.Space();EditorGUILayout.LabelField(Sections[section],EditorStyles.boldLabel);
            if(!TerrainPainter.Empty(s.nativeDirty))EditorGUILayout.HelpBox("Unresolved native paint "+s.nativeDirty+". Capture or release it in Manual Artistic Overrides.",MessageType.Warning);
            switch(section)
            {
                case 0:
                    Area();
                    if(Button("Preview selected paint","Calculates material coverage without writing TerrainData."))Preview(tile);
                    if(preview)GUILayout.Box(preview,GUILayout.Width(220),GUILayout.Height(220));
                    if(Button("Rebuild selected region","Updates the selection and changed road footprints; leaves unrelated rule changes pending."))Rebuild(tile,Region);
                    if(Button("Rebuild entire terrain","Re-evaluates the full working paint map."))Rebuild(tile,TerrainPainter.Full(s.working));
                    if(Button("Revert generated paint and wear","Restores the captured source paint plus protected overrides. Sculpting survives."))
                        Rebuild(tile,TerrainPainter.Full(s.working),true);
                    break;
                case 1:case 2:
                    var profile=(SurfaceProfile)EditorGUILayout.ObjectField("Surface profile",s.profile,typeof(SurfaceProfile),false);
                    if(profile && profile!=s.profile){Undo.RecordObject(s,"Change surface profile");s.profile=profile;EditorUtility.SetDirty(s);StudioUpdates.Schedule();}
                    Fields(s.profile,section==1?new[]{"grass","soil","rock"}:new[]{"rockBySlope","grassBySlope","grassByElevation","seed","patchMetres","breakupMetres","variation","curvatureInfluence","curvatureRadius","exposureDirection","useSoilRegion","soilRegion","regionFeather","regionStrength"});
                    if(section==1)EditorGUILayout.HelpBox("Select a TerrainLayer in the Project to edit tile size, normal strength, diffuse and mask maps. Duplicate shared profiles or layers for local variations.",MessageType.Info);
                    break;
                case 3:case 4:
                    Area();
                    road=(StudioRoad)EditorGUILayout.ObjectField("Spline road",road,typeof(StudioRoad),true);
                    if(Button("Use Hierarchy selection","Use the selected StudioRoad."))road=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<StudioRoad>():null;
                    roadProfile=(RoadProfile)EditorGUILayout.ObjectField("Road preset",roadProfile,typeof(RoadProfile),false);
                    if(section==3 && Button("Create curved road at centre","Select the Splines tool to move, insert and extend knots."))road=StudioSetup.CreateRoad(tile,center,roadProfile);
                    if(road && roadProfile && Button("Assign preset to selected road","Switch this road to a different width, material or wear treatment."))
                    {Undo.RecordObject(road,"Change road preset");road.profile=roadProfile;EditorUtility.SetDirty(road);StudioUpdates.Schedule();}
                    var roadSettings=road?road.profile:roadProfile;
                    Fields(roadSettings,section==3?new[]{"surface","width","shoulderMetres","shoulderFade","edgeIrregularity","vegetation","priority"}:new[]{"carriage","wear","trackSpacing","trackWidth","textureRepeatMetres","trackBreakup","trackMaterial"});
                    if(road && Button("Rebuild selected road","Repairs its old and new footprint on every attached tile it crossed."))RoadRebuild(tile);
                    EditorGUILayout.HelpBox("Use Unity's Spline tool for knots/tangents and ordinary Hierarchy duplicate/delete. Carriage wear uses draped render-only ribbons.",MessageType.Info);
                    break;
                case 5:
                    var issue=PaintHistory.Invalid(s,s.working.terrainLayers);
                    if(issue!=null){EditorGUILayout.HelpBox(issue,MessageType.Error);break;}
                    Area();opacity=EditorGUILayout.Slider("Protection / blend",opacity,0,1);
                    if(Button("Find unprotected edits","Compare the whole tile with its paint history and select pending native edits for review."))
                    {
                        var found=PaintHistory.ReviewNativeEdits(tile);
                        message=TerrainPainter.Empty(found)?"No unprotected edits found.":"Review native paint "+found+" before protecting or releasing it.";
                    }
                    if(Button("Capture selected paint","Snapshots current native paint on this rectangle."))PaintHistory.Resolve(tile,Region,true,opacity);
                    if(!TerrainPainter.Empty(s.nativeDirty))
                    {
                        if(Button("Protect pending native edits","Stores the full pending native region."))PaintHistory.Resolve(tile,s.nativeDirty,true,1);
                        if(Button("Release pending edits","Allows generated paint to replace the pending native rectangle.")&&Confirm("Release these unprotected native edits?"))PaintHistory.Resolve(tile,s.nativeDirty,false,1);
                    }
                    for(int i=0;i<s.protectedPaint.Count;i++)
                    {
                        var patch=s.protectedPaint[i];
                        EditorGUILayout.BeginHorizontal();GUILayout.Label(patch.rect+" · "+patch.opacity.ToString("P0"));
                        if(GUILayout.Button("Clear",GUILayout.Width(60))&&Confirm("Return this captured region to automatic painting?"))
                        {PaintHistory.Clear(tile,i--);}
                        EditorGUILayout.EndHorizontal();
                    }
                    break;
                case 6:
                    bool auto=EditorGUILayout.Toggle(new GUIContent("Automatic update","Defers changes until sculpting/dragging stops."),s.automatic);
                    if(auto!=s.automatic&&(!auto||Confirm("Enable automatic replacement of generated paint outside protected areas?")))
                    {Undo.RecordObject(s,"Toggle terrain paint updates");s.automatic=auto;EditorUtility.SetDirty(s);StudioUpdates.Schedule();}
                    EditorGUILayout.HelpBox("Rule changes affect the full tile. Sculpting and spline edits queue a padded rectangle. Native painting suspends writes until resolved.",MessageType.Info);break;
                case 7:
                    Area();player=(CharacterController)EditorGUILayout.ObjectField("Player controller",player,typeof(CharacterController),true);
                    walk=EditorGUILayout.Toggle("Show slope overlay",walk);
                    if(player)GUILayout.Label($"Slope limit {player.slopeLimit:F0}°   Step offset {player.stepOffset:F2} m");
                    EditorGUILayout.HelpBox("Green: below limit. Amber: within 5° of limit. Red: above limit, aerial traversal may be needed. Samples approximate grounded slopes; obstacles, momentum and step contacts require gameplay testing.",MessageType.Info);
                    SceneView.RepaintAll();break;
                case 8:
                    if(Button("Validate terrain setup","Read-only check of native paint, protected data, roads, seams and pending work."))message=SetupValidation.Inspect(tile,player);
                    break;
            }
            if(!string.IsNullOrEmpty(StudioUpdates.LastResult))GUILayout.Label("Last update · "+StudioUpdates.LastResult);
        }
        static void Fields(UnityEngine.Object target,string[] names)
        {
            if(!target)return;
            using var serialized=new SerializedObject(target);
            foreach(string name in names)EditorGUILayout.PropertyField(serialized.FindProperty(name),true);
            if(serialized.ApplyModifiedProperties())StudioUpdates.Schedule();
        }
        void Area()
        {
            center=EditorGUILayout.Vector2Field("World centre X/Z (m)",center);
            size=Vector2.Max(Vector2.one,EditorGUILayout.Vector2Field("Area width/depth (m)",size));
            if(Button(pick?"Cancel picking":"Pick centre in Scene view","Click the target terrain.")){pick=!pick;SceneView.RepaintAll();}
        }
        static bool Confirm(string text)=>EditorUtility.DisplayDialog("Terrain Studio",text,"Continue","Cancel");
        void Rebuild(StudioTile t,RectInt rect,bool revert=false)
        {
            var paths=RoadPath.InScene(t);bool local=rect!=TerrainPainter.Full(t.state.working);
            string scope=SetupValidation.Scope(t,paths,rect,local);
            if(!Confirm(scope+"\n"+(revert?"Restore source paint, remove wheel wear and turn off automatic updates?":"Replace unprotected coverage and update affected wheel wear?")))return;
            StudioUpdates.Rebuild(t,rect,paths,true,revert,local);message=StudioUpdates.LastResult;SceneView.RepaintAll();
        }
        void RoadRebuild(StudioTile tile)
        {
            var plans=StudioUpdates.Tiles().Where(t=>t.gameObject.scene==tile.gameObject.scene).Select(t=>
            {
                var paths=RoadPath.InScene(t);
                var r=paths.Where(p=>p.road==road).Select(p=>TerrainPainter.Pixels(t.terrain,p.bounds)).Aggregate(default(RectInt),TerrainPainter.Union);
                return (tile:t,paths,region:r,scope:SetupValidation.Scope(t,paths,r,true));
            }).ToList();
            if(!Confirm(string.Join("\n",plans.Select(p=>p.scope))+"\nRebuild these road footprints?"))return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            try
            {
                foreach(var plan in plans)StudioUpdates.Rebuild(plan.tile,plan.region,plan.paths,true,false,true);
                Undo.CollapseUndoOperations(group);
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
        void Preview(StudioTile t)
        {
            var paths=RoadPath.InScene(t);var r=Region;if(TerrainPainter.Empty(r))throw new ArgumentException("Region misses terrain.");
            var layers=TerrainPainter.Palette(t,paths);var map=TerrainPainter.Calculate(t,r,paths,layers);
            if(preview)DestroyImmediate(preview);preview=new Texture2D(r.width,r.height){hideFlags=HideFlags.HideAndDontSave};
            int grass=Array.IndexOf(layers,t.state.profile.grass),rock=Array.IndexOf(layers,t.state.profile.rock);
            var pixels=new Color[r.width*r.height];
            for(int y=0;y<r.height;y++)for(int x=0;x<r.width;x++)pixels[y*r.width+x]=Color.green*map[y,x,grass]+Color.gray*map[y,x,rock]+new Color(.48f,.28f,.14f)*(1-map[y,x,grass]-map[y,x,rock]);
            preview.SetPixels(pixels);preview.Apply();message="Material coverage preview · green meadow, brown soil/road, grey stone.";
        }
        void SceneGUI(SceneView scene)
        {
            if(!terrain||!terrain.terrainData||EditorApplication.isPlaying)return;
            var tile=Tile;if(!tile)return;
            var h=new TerrainPainter.Heights(terrain);
            Vector3 At(Vector2 p)=>new Vector3(p.x,h.At(p)+.15f,p.y);
            void Outline(Rect rect,Color color)
            {
                Handles.color=color;Handles.DrawAAPolyLine(2,At(rect.min),At(new Vector2(rect.xMax,rect.yMin)),At(rect.max),At(new Vector2(rect.xMin,rect.yMax)),At(rect.min));
            }
            Outline(WorldRect,Color.cyan);
            if(section==5 && tile.state && tile.state.protectedPaint!=null)foreach(var p in tile.state.protectedPaint)
            {
                if(p==null)continue;
                var a=TerrainPainter.World(terrain,p.rect.x,p.rect.y);var b=TerrainPainter.World(terrain,p.rect.xMax-1,p.rect.yMax-1);Outline(new Rect(a,b-a),Color.magenta);
            }
            if(walk&&section==7&&player && Event.current.type==EventType.Repaint)
                for(int y=0;y<20;y++)for(int x=0;x<20;x++)
                {
                    var pos=WorldRect.min+Vector2.Scale(size,new Vector2((x+.5f)/20,(y+.5f)/20));
                    if(!TerrainPainter.Heights.Contains(terrain,pos))continue;
                    var n=h.Normal(pos,1,out _);
                    var grade=TileDiagnostics.Grade(n,player);
                    Handles.color=grade==GroundGrade.Aerial?Color.red:grade==GroundGrade.Marginal?Color.yellow:Color.green;
                    Handles.DrawSolidDisc(At(pos),n,Mathf.Min(size.x,size.y)/75);
                }
            if(!pick)return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&!e.alt&&terrain.GetComponent<TerrainCollider>().Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit,100000))
            {center=new Vector2(hit.point.x,hit.point.z);pick=false;e.Use();Repaint();}
        }
    }
}
