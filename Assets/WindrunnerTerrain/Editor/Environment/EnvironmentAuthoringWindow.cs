using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Editor
{
    public sealed class EnvironmentAuthoringWindow : EditorWindow
    {
        EnvironmentLibrarySettings settings;
        EnvironmentCatalog catalog;
        List<EnvironmentEntry> visible=new List<EnvironmentEntry>();
        readonly Dictionary<string,Object> gridAssets=new Dictionary<string,Object>();
        readonly System.Random placementRandom=new System.Random();
        string search="",category="",publisher="",biome="",selected="",message="";
        bool favorites,approved=true,recent,placing;
        int tab,collection=-1,typeFilter;
        float thumbnail=112;
        Vector2 gridScroll,inspectorScroll,otherScroll;
        Object asset; UnityEditor.Editor previewEditor;
        EnvironmentInspection inspection;
        readonly EnvironmentPlacementSettings placement=new EnvironmentPlacementSettings();
        Transform parent;Terrain target;
        PrototypeCategories prototypeCategory=PrototypeCategories.Trees;
        EnvironmentPrototypePreparation preparation;
        List<string> diagnostics=new List<string>();
        double lastPreviewRepaint;
        [MenuItem("Tools/Windrunner/Environment Authoring Studio")]
        public static void Open()
        { bool existed=HasOpenInstances<EnvironmentAuthoringWindow>();var window=GetWindow<EnvironmentAuthoringWindow>("Environment Studio");if(!existed)window.position=new Rect(80,80,1120,780); }
        void OnEnable()
        {
            minSize=new Vector2(860,560);settings=EnvironmentPreferences.instance.library;settings.Defaults();
            catalog=new EnvironmentCatalog(settings);catalog.Changed+=Filter;catalog.Rescan();
            SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload+=Cleanup;EditorApplication.playModeStateChanged+=PlayMode;
        }
        void OnDisable()
        {
            SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload-=Cleanup;EditorApplication.playModeStateChanged-=PlayMode;
            catalog?.Dispose();catalog=null;Cleanup();
        }
        void PlayMode(PlayModeStateChange change){Cleanup();}
        void Cleanup(){placing=false;preparation?.Dispose();preparation=null;if(previewEditor)DestroyImmediate(previewEditor);previewEditor=null;}
        void Invalidate(){preparation?.Dispose();preparation=null;message="Preview invalidated by Undo/Redo.";Repaint();}
        void Save(){EnvironmentPreferences.instance.Persist();Filter();}
        void Filter(){if(catalog==null)return;gridAssets.Clear();visible=catalog.Filter(search,category,publisher,biome,favorites,approved,collection>=0&&collection<settings.collections.Count?settings.collections[collection]:null,recent);if(typeFilter>0)visible=visible.Where(e=>e.type==new[]{"All assets","Prefab","Model","TerrainLayer","Texture2D","Material"}[typeFilter]).ToList();Repaint();}
        void Select(EnvironmentEntry entry)
        {
            selected=entry.guid;asset=entry.missing?null:AssetDatabase.LoadMainAssetAtPath(entry.path);
            if(previewEditor)DestroyImmediate(previewEditor);previewEditor=asset?UnityEditor.Editor.CreateEditor(asset):null;
            inspection=EnvironmentInspection.Asset(asset);inspectorScroll=Vector2.zero;Invalidate();
        }
        void OnGUI()
        {
            if(catalog==null)return;
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("ENVIRONMENT AUTHORING",EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();GUILayout.Label(catalog.Pending>0?$"Indexing · {catalog.Pending} pending":$"{catalog.entries.Count} assets",EditorStyles.miniLabel);
                if(GUILayout.Button("Refresh",EditorStyles.toolbarButton,GUILayout.Width(65)))catalog.Rescan();
            }
            tab=GUILayout.Toolbar(tab,new[]{"Library & placement","Sources","Collections & presets","Terrain workspace","Scene diagnostics"});
            if(tab==0)Library();else
            {
                otherScroll=EditorGUILayout.BeginScrollView(otherScroll);
                if(tab==1)Sources();if(tab==2)Collections();if(tab==3)TerrainWorkspace();if(tab==4)Diagnostics();
                EditorGUILayout.EndScrollView();
            }
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.None);
        }
        void Library()
        {
            EditorGUI.BeginChangeCheck();
            using(new EditorGUILayout.HorizontalScope())
            {
                search=EditorGUILayout.TextField(search,GUI.skin.FindStyle("ToolbarSearchTextField")??EditorStyles.textField);
                typeFilter=EditorGUILayout.Popup(typeFilter,new[]{"All assets","Prefabs","Models","TerrainLayers","Textures","Materials"},GUILayout.Width(110));
                favorites=GUILayout.Toggle(favorites,"Favorites",EditorStyles.miniButton,GUILayout.Width(75));approved=GUILayout.Toggle(approved,"Approved",EditorStyles.miniButton,GUILayout.Width(75));recent=GUILayout.Toggle(recent,"Recent first",EditorStyles.miniButton,GUILayout.Width(85));
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                var categories=new[]{"All categories"}.Concat(EnvironmentCatalog.Categories).Concat(settings.metadata.Where(m=>!string.IsNullOrEmpty(m.category)).Select(m=>m.category)).Distinct().ToArray();
                int ci=Array.IndexOf(categories,category);int picked=EditorGUILayout.Popup(Math.Max(0,ci),categories,GUILayout.Width(180));category=picked==0?"":categories[picked];
                publisher=EditorGUILayout.TextField("Source contains",publisher);biome=EditorGUILayout.TextField("Biome contains",biome);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                var names=new[]{"All collections"}.Concat(settings.collections.Select(c=>c.name)).ToArray();collection=EditorGUILayout.Popup("Collection",collection+1,names)-1;
                thumbnail=EditorGUILayout.Slider("Thumbnail",thumbnail,80,160);
            }
            if(EditorGUI.EndChangeCheck()){gridScroll=Vector2.zero;Filter();}
            using(new EditorGUILayout.HorizontalScope())
            {
                Grid(Mathf.Max(300,position.width-330));
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(310)))Inspector();
            }
        }
        void Grid(float width)
        {
            Rect viewport=GUILayoutUtility.GetRect(width,width,180,10000,GUILayout.ExpandHeight(true),GUILayout.Width(width));
            int columns=Mathf.Max(1,Mathf.FloorToInt((width-20)/(thumbnail+12)));float cell=(width-20)/columns,height=thumbnail+52;
            int rows=Mathf.CeilToInt(visible.Count/(float)columns);
            gridScroll=GUI.BeginScrollView(viewport,gridScroll,new Rect(0,0,width-20,Mathf.Max(viewport.height,rows*height)));
            int start=Mathf.Max(0,Mathf.FloorToInt(gridScroll.y/height)),end=Mathf.Min(rows,Mathf.CeilToInt((gridScroll.y+viewport.height)/height)+1);
            bool waiting=false;
            for(int row=start;row<end;row++)for(int col=0;col<columns;col++)
            {
                int i=row*columns+col;if(i>=visible.Count)break;var e=visible[i];var rect=new Rect(col*cell+4,row*height+4,cell-8,height-8);
                if(e.guid==selected)EditorGUI.DrawRect(rect,new Color(.18f,.37f,.48f,.5f));
                if(!gridAssets.TryGetValue(e.guid,out var obj)){obj=e.missing?null:AssetDatabase.LoadMainAssetAtPath(e.path);gridAssets[e.guid]=obj;}
                var texture=obj?AssetPreview.GetAssetPreview(obj):null;waiting|=obj&&!texture&&AssetPreview.IsLoadingAssetPreview(obj.GetEntityId());
                if(!texture && obj)texture=AssetPreview.GetMiniThumbnail(obj);
                if(texture)GUI.DrawTexture(new Rect(rect.x+4,rect.y+4,rect.width-8,thumbnail-8),texture,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(rect.x+4,rect.y+thumbnail,rect.width-8,18),e.name,EditorStyles.boldLabel);
                GUI.Label(new Rect(rect.x+4,rect.y+thumbnail+18,rect.width-8,18),e.missing?"Unavailable · metadata retained":e.type,EditorStyles.miniLabel);
                if(Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition)){Select(e);Event.current.Use();}
            }
            GUI.EndScrollView();
            if(visible.Count==0)GUI.Label(new Rect(viewport.x+20,viewport.y+20,viewport.width-40,70),"No matching assets. Adjust filters or register an approved source folder.\nMissing favorites remain available with Approved disabled.",EditorStyles.wordWrappedLabel);
            if(waiting && EditorApplication.timeSinceStartup-lastPreviewRepaint>.15){lastPreviewRepaint=EditorApplication.timeSinceStartup;Repaint();}
        }
        void Inspector()
        {
            inspectorScroll=EditorGUILayout.BeginScrollView(inspectorScroll);
            if(!catalog.entries.TryGetValue(selected,out var entry)){EditorGUILayout.HelpBox("Select an asset to inspect, classify, preview or place it. No selection changes scene content.",MessageType.Info);EditorGUILayout.EndScrollView();return;}
            GUILayout.Label(entry.name,EditorStyles.boldLabel);GUILayout.Label(entry.path,EditorStyles.wordWrappedMiniLabel);
            Rect rect=GUILayoutUtility.GetRect(200,190,GUILayout.ExpandWidth(true));
            if(previewEditor && previewEditor.HasPreviewGUI())previewEditor.OnInteractivePreviewGUI(rect,GUIStyle.none);
            else if(asset){var texture=AssetPreview.GetAssetPreview(asset)??AssetPreview.GetMiniThumbnail(asset);if(texture)GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit);}
            GUILayout.Label("Drag preview to orbit · wheel to zoom where supported",EditorStyles.wordWrappedMiniLabel);
            using(new EditorGUILayout.HorizontalScope())
            {if(GUILayout.Button("Ping"))EditorGUIUtility.PingObject(asset);if(GUILayout.Button("Select original"))Selection.activeObject=asset;}
            var m=settings.For(entry.guid);EditorGUI.BeginChangeCheck();
            m.favorite=EditorGUILayout.Toggle("Favorite",m.favorite);
            m.approval=EditorGUILayout.Popup("Approval",m.approval,new[]{"Approved registry default","Approved","Rejected"});
            GUILayout.Label("Source: "+entry.source,EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("GUID: "+entry.guid,EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("Suggestions: "+entry.category+" · "+entry.biomes,EditorStyles.wordWrappedMiniLabel);
            m.category=EditorGUILayout.TextField("Category override",m.category);
            m.biomes=EditorGUILayout.TextField("Biome tags",m.biomes);
            m.publisher=EditorGUILayout.TextField("Source override",m.publisher);
            if(inspection!=null)GUILayout.Label($"Structural candidates � Tree: {inspection.tree} � Mesh detail: {inspection.detail}",EditorStyles.wordWrappedMiniLabel);
            m.usage=EditorGUILayout.TextField("Preferred usage",m.usage);
            GUILayout.Label("Notes");m.notes=EditorGUILayout.TextArea(m.notes,GUILayout.MinHeight(38));
            if(EditorGUI.EndChangeCheck())Save();
            if(collection>=0 && GUILayout.Button("Add to selected collection")){var c=settings.collections[collection];if(!c.guids.Contains(selected))c.guids.Add(selected);Save();}
            if(inspection!=null)foreach(var line in inspection.messages.Take(30))GUILayout.Label(line,EditorStyles.wordWrappedMiniLabel);
            if(asset is GameObject)
            {
                GUILayout.Space(8);GUILayout.Label("Manual placement",EditorStyles.boldLabel);PlacementFields(placement);
                parent=(Transform)EditorGUILayout.ObjectField("Parent",parent,typeof(Transform),true);
                if(GUILayout.Button("Use selected hierarchy parent"))parent=Selection.activeTransform;
                if(GUILayout.Button("Find existing category group"))FindParent(string.IsNullOrEmpty(m.category)?entry.category:m.category);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button(placing?"Stop placement (Esc)":"Place in Scene view")){placing=!placing;SceneView.RepaintAll();}
                if(placing)EditorGUILayout.HelpBox("Click a surface to place one linked prefab. R rotates 15° (Shift reverses); Esc cancels. Alt keeps Scene navigation. Undo removes placement. Bounds outline is a guide; review collisions after placement.",MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }
        static void PlacementFields(EnvironmentPlacementSettings p)
        {
            p.snap=EditorGUILayout.Toggle("Snap to surfaces",p.snap);p.alignNormal=EditorGUILayout.Toggle("Align to normal",p.alignNormal);
            p.yaw=EditorGUILayout.FloatField("Y rotation",p.yaw);p.scale=EditorGUILayout.FloatField("Scale multiplier",p.scale);p.offset=EditorGUILayout.FloatField("Surface offset",p.offset);
            p.randomYaw=EditorGUILayout.Toggle("Random Y rotation",p.randomYaw);p.varyScale=EditorGUILayout.Toggle("Vary scale",p.varyScale);
            if(p.varyScale){p.minScale=EditorGUILayout.FloatField("Minimum scale",p.minScale);p.maxScale=EditorGUILayout.FloatField("Maximum scale",p.maxScale);}
            p.grid=EditorGUILayout.Toggle("Grid snap X/Z",p.grid);if(p.grid)p.gridSize=Mathf.Max(.01f,EditorGUILayout.FloatField("Grid metres",p.gridSize));
        }
        void FindParent(string c)
        {
            string path=c=="Buildings"||c=="Structures"?"Structures":"Environment/"+(new[]{"Trees","Rocks","Cliffs","Bushes","Grass","Water"}.Contains(c)?c:"GroundDetails");
            var root=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name=="WorldRoot");parent=root?root.transform.Find(path):null;
            message=parent?"Existing group selected: WorldRoot/"+path:"Group not found. Choose a parent manually; no roots were created.";
        }
        void SceneGUI(SceneView view)
        {
            if(!placing || !(asset is GameObject prefab) || EditorApplication.isPlayingOrWillChangePlaymode)return;
            Event e=Event.current;if(e.keyCode==KeyCode.Escape&&e.type==EventType.KeyDown){placing=false;Repaint();return;}
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.R){placement.yaw+=e.shift?-15:15;e.Use();Repaint();}
            if(e.alt)return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);Vector3 point,normal=Vector3.up;
            if(placement.snap){if(!Physics.Raycast(ray,out var hit,10000)){Handles.BeginGUI();GUI.Label(new Rect(12,12,380,30),"No collider hit. Enable terrain/object colliders or disable surface snap.");Handles.EndGUI();return;}point=hit.point;normal=hit.normal;}
            else{var plane=new Plane(Vector3.up,new Vector3(0,parent?parent.position.y:0,0));if(!plane.Raycast(ray,out float distance))return;point=ray.GetPoint(distance);}
            if(placement.grid){point.x=Mathf.Round(point.x/placement.gridSize)*placement.gridSize;point.z=Mathf.Round(point.z/placement.gridSize)*placement.gridSize;if(placement.snap && Physics.Raycast(point+Vector3.up*1000,Vector3.down,out var snapped,2000)){point=snapped.point;normal=snapped.normal;}}
            Handles.color=new Color(.25f,.85f,.85f,1);Handles.DrawWireCube(point+normal*placement.offset,inspection!=null?inspection.bounds.size*placement.scale:Vector3.one);
            Handles.Label(point,"Click to place · "+prefab.name);view.Repaint();
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                try{var go=EnvironmentPlacement.Place(prefab,SceneManager.GetActiveScene(),parent,point,normal,placement,(float)placementRandom.NextDouble(),(float)placementRandom.NextDouble());Selection.activeGameObject=go;settings.For(selected).recent=DateTime.UtcNow.Ticks;Save();message="Placed linked prefab. Undo supported; scene remains unsaved.";}
                catch(Exception ex){message=ex.Message;placing=false;}
                e.Use();Repaint();
            }
        }
        void Sources()
        {
            EditorGUILayout.HelpBox("Only explicitly approved folder roots enter the palette. Imports/moves/deletions under these roots update incrementally while the Studio is open; reopening performs a rescan. Add new packs here after verifying their publisher. Empty/missing roots are safe. Filters are semicolon-separated path fragments; categories are exact suggestions.",MessageType.Info);
            for(int i=0;i<settings.sources.Count;i++)
            {
                var s=settings.sources[i];using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();s.enabled=EditorGUILayout.Toggle("Enabled / approved",s.enabled);s.name=EditorGUILayout.TextField("Verified source",s.name);s.folder=EditorGUILayout.TextField("Project folder",s.folder.TrimEnd('/'));
                    s.biomes=EditorGUILayout.TextField("Default biomes",s.biomes);s.include=EditorGUILayout.TextField("Include path fragments",s.include);s.exclude=EditorGUILayout.TextField("Exclude path fragments",s.exclude);s.categories=EditorGUILayout.TextField("Allowed categories (blank=all)",s.categories);
                    if(EditorGUI.EndChangeCheck())Save();
                    GUILayout.Label(AssetDatabase.IsValidFolder(s.folder)?"Installed":"Not installed / invalid folder · metadata retained",EditorStyles.miniLabel);
                    if(GUILayout.Button("Remove source registration")){settings.sources.RemoveAt(i);Save();catalog.Rescan();break;}
                }
            }
            if(GUILayout.Button("Register approved folder")){settings.sources.Add(new EnvironmentSource{name="Verified publisher / package",folder="",enabled=false});Save();}
            if(GUILayout.Button("Apply registry and rescan"))catalog.Rescan();
        }
        void Collections()
        {
            EditorGUILayout.HelpBox("Collections are GUID lists and placement presets, never generated biomes. Empty collections and missing entries are retained. Selecting a collection does not change any scene or terrain.",MessageType.Info);
            if(GUILayout.Button("New empty collection")){settings.collections.Add(new EnvironmentCollection());Save();}
            for(int i=0;i<settings.collections.Count;i++)
            {
                var c=settings.collections[i];using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();c.name=EditorGUILayout.TextField("Name",c.name);c.biomes=EditorGUILayout.TextField("Biome tags",c.biomes);c.notes=EditorGUILayout.TextField("Notes / preferred variations",c.notes);
                    if(EditorGUI.EndChangeCheck())Save();
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        if(GUILayout.Button("Browse")){collection=i;tab=0;Filter();}
                        if(GUILayout.Button("Add current asset")&&!string.IsNullOrEmpty(selected)){if(!c.guids.Contains(selected))c.guids.Add(selected);Save();}
                        if(GUILayout.Button("Duplicate")){var copy=JsonUtility.FromJson<EnvironmentCollection>(JsonUtility.ToJson(c));copy.name+=" copy";settings.collections.Add(copy);Save();break;}
                        if(GUILayout.Button("Remove collection")){settings.collections.RemoveAt(i);collection=-1;Save();break;}
                    }
                    foreach(string guid in c.guids.ToArray())using(new EditorGUILayout.HorizontalScope())
                    {string path=AssetDatabase.GUIDToAssetPath(guid);GUILayout.Label(string.IsNullOrEmpty(path)?"Missing · "+guid:path,EditorStyles.wordWrappedMiniLabel);if(GUILayout.Button("Remove",GUILayout.Width(65))){c.guids.Remove(guid);Save();}}
                    EditorGUI.BeginChangeCheck();PlacementFields(c.placement);if(EditorGUI.EndChangeCheck())Save();
                    if(GUILayout.Button("Use placement preset")){CopyPlacement(c.placement);message="Placement settings loaded; no scene changes.";}
                }
            }
        }
        void CopyPlacement(EnvironmentPlacementSettings p)
        {placement.alignNormal=p.alignNormal;placement.randomYaw=p.randomYaw;placement.varyScale=p.varyScale;placement.snap=p.snap;placement.grid=p.grid;placement.yaw=p.yaw;placement.scale=p.scale;placement.minScale=p.minScale;placement.maxScale=p.maxScale;placement.offset=p.offset;placement.gridSize=p.gridSize;}
        void TerrainWorkspace()
        {
            EditorGUILayout.HelpBox("No terrain edits occur while browsing. Workflow: edit → save scene/data → capture Manual Artistic Overrides in Terrain Studio → preview procedural changes → deliberately apply → verify. This workspace only delegates append operations; it never paints, sculpts, reorders indices or changes road rules.",MessageType.Info);
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("Open Terrain Studio"))TerrainStudioWindow.Open();if(GUILayout.Button("Open Terrain Prototype Transfer"))TerrainPrototypeTransferWindow.Open();}
            EditorGUI.BeginChangeCheck();target=(Terrain)EditorGUILayout.ObjectField("Destination Terrain",target,typeof(Terrain),true);prototypeCategory=(PrototypeCategories)EditorGUILayout.EnumPopup("Prepare selected asset as",prototypeCategory);
            if(EditorGUI.EndChangeCheck())Invalidate();
            GUILayout.Label("Selected asset: "+(asset?AssetDatabase.GetAssetPath(asset):"none"),EditorStyles.wordWrappedLabel);
            if(GUILayout.Button("Inspect destination (read-only)"))diagnostics=EnvironmentInspection.Terrain(target);
            if(GUILayout.Button("Select destination for native painting")&&target)Selection.activeGameObject=target.gameObject;
            if(target&&target.terrainData)foreach(var layer in target.terrainData.terrainLayers)EditorGUILayout.ObjectField("Current layer (read-only)",layer,typeof(TerrainLayer),false);
            using(new EditorGUI.DisabledScope(!asset||!target||EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if(GUILayout.Button("Preview append of selected asset"))
                {
                    preparation?.Dispose();preparation=null;
                    try{preparation=new EnvironmentPrototypePreparation(asset,target,prototypeCategory);message=preparation.preview.Summary;diagnostics=EnvironmentInspection.Terrain(target);}
                    catch(Exception ex){message="Refused: "+ex.Message;}
                }
                using(new EditorGUI.DisabledScope(preparation?.preview==null||preparation.preview.Added==0))
                    if(GUILayout.Button("Apply previewed append · Undo supported"))
                    {try{preparation.Apply();message="Appended only. Existing paint and indices preserved. Save explicitly when satisfied.";}catch(Exception ex){message="Refused: "+ex.Message;}finally{preparation?.Dispose();preparation=null;}}
            }
            foreach(var line in diagnostics.Take(100))GUILayout.Label(line,EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("Terrain Studio owns editable working copies, road authoring, procedural texture previews, dirty-region updates and Manual Artistic Overrides. These remain in its existing window. Shared-data checks cover loaded scenes; inspect other scene usage before editing a shared asset. Prototype dimensions here are conservative defaults: review native tree/detail settings before painting.",MessageType.Warning);
        }
        void Diagnostics()
        {
            if(GUILayout.Button("Inspect active scene · read-only")){diagnostics=EnvironmentInspection.Scene(SceneManager.GetActiveScene());message="Inspection complete. No objects or assets were modified.";}
            if(GUILayout.Button("Copy diagnostic report"))EditorGUIUtility.systemCopyBuffer=string.Join("\n",diagnostics);
            foreach(var line in diagnostics.Take(500))EditorGUILayout.HelpBox(line,line.StartsWith("ERROR")?MessageType.Error:line.StartsWith("WARNING")?MessageType.Warning:MessageType.Info);
            if(diagnostics.Count>500)GUILayout.Label("First 500 shown; Copy includes the full report.");
        }
    }
}
