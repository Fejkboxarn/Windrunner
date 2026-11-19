using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Windrunner.TerrainAuthoring.Editor
{
    public sealed class TerrainBrushWindow : EditorWindow
    {
        Terrain terrain; TerrainStamp brush; TerrainStampPreset preset; StampSettings settings=new StampSettings();
        TerrainStamp[] library; TerrainStampOperations.Preview preview; Vector2 center,scroll,libraryScroll; string search="",category="All",message=""; StampMode mode; bool favorites,pick;
        readonly Dictionary<TerrainStamp,Texture2D> thumbnails=new Dictionary<TerrainStamp,Texture2D>();
        StudioTile Tile=>terrain?terrain.GetComponentInChildren<StudioTile>(true):null;
        [MenuItem("Tools/Windrunner/Terrain Brush Library")]
        public static void Open()=>GetWindow<TerrainBrushWindow>("Terrain Brushes");
        void OnEnable(){library=AssetDatabase.FindAssets("t:TerrainStamp").Select(g=>AssetDatabase.LoadAssetAtPath<TerrainStamp>(AssetDatabase.GUIDToAssetPath(g))).Where(b=>b).ToArray();SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=Invalidate;}
        void OnDisable(){foreach(var texture in thumbnails.Values)if(texture)DestroyImmediate(texture);thumbnails.Clear();SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=Invalidate;}
        void Invalidate(){preview=null;Repaint();SceneView.RepaintAll();}
        void Select(TerrainStamp b){brush=b;settings.operation=b.name=="Broad Valley"||b.name=="Natural Gully"?HeightOperation.Lower:HeightOperation.Raise;Invalidate();}
        Texture2D Thumbnail(TerrainStamp b)
        {
            if(!b.field||!b.field.isReadable||b.field.format!=TextureFormat.R16)return Texture2D.grayTexture;
            if(thumbnails.TryGetValue(b,out var cached))return cached;var texture=new Texture2D(96,96,TextureFormat.RGBA32,false,true){hideFlags=HideFlags.HideAndDontSave};var pixels=new Color[96*96];var values=b.Samples();for(int y=0;y<96;y++)for(int x=0;x<96;x++){float value=TerrainStamp.Sample(values,b.field.width,x/95f,y/95f);pixels[y*96+x]=new Color(value,value,value,1);}texture.SetPixels(pixels);texture.Apply(false,true);thumbnails.Add(b,texture);return texture;
        }
        string Key(TerrainStamp b)=>"Windrunner.StampFavorite."+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(b));
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            try
            {
                EditorGUILayout.HelpBox("Manual stamps on Terrain Studio working copies only. Automatic Updates must be off. Click Preview, inspect the Scene overlay, then Apply. No automatic painting or road regeneration.",MessageType.Info);
                if(GUILayout.Button("Open Terrain Studio"))TerrainStudioWindow.Open();
                if(GUILayout.Button("Open Terrain Prototype Transfer"))EditorApplication.ExecuteMenuItem("Tools/Windrunner/Terrain Prototype Transfer");
                EditorGUI.BeginChangeCheck();
                var next=(Terrain)EditorGUILayout.ObjectField("Target Terrain",terrain,typeof(Terrain),true);
                if(next!=terrain){terrain=next;if(terrain&&terrain.terrainData){var p=terrain.GetPosition();center=new Vector2(p.x+terrain.terrainData.size.x/2,p.z+terrain.terrainData.size.z/2);}}
                mode=(StampMode)GUILayout.Toolbar((int)mode,new[]{"Sculpt","Paint"});search=EditorGUILayout.TextField("Search",search);favorites=EditorGUILayout.Toggle("Favorites only",favorites);
                var categories=new[]{"All"}.Concat(library.Where(b=>b.mode==mode).Select(b=>b.category).Distinct()).ToArray();int index=Array.IndexOf(categories,category);category=categories[EditorGUILayout.Popup("Category",Mathf.Max(0,index),categories)];
                libraryScroll=EditorGUILayout.BeginScrollView(libraryScroll,GUILayout.Height(250));
                foreach(var b in library.Where(b=>b.mode==mode&&(category=="All"||b.category==category)&&b.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0&&(!favorites||EditorPrefs.GetBool(Key(b)))))
                {
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        if(GUILayout.Button(new GUIContent(Thumbnail(b),b.description),GUILayout.Width(64),GUILayout.Height(64)))Select(b);
                        if(GUILayout.Button(b.name+(brush==b?" [selected]":""),GUILayout.Height(64)))Select(b);
                        bool old=EditorPrefs.GetBool(Key(b));bool star=GUILayout.Toggle(old,"★",GUILayout.Width(32));if(star!=old)EditorPrefs.SetBool(Key(b),star);
                    }
                }
                EditorGUILayout.EndScrollView();
                var selected=(TerrainStampPreset)EditorGUILayout.ObjectField("Load preset",preset,typeof(TerrainStampPreset),false);
                if(selected!=preset){preset=selected;if(preset){brush=preset.brush;settings=preset.settings?.Copy()??new StampSettings();if(brush)mode=brush.mode;}}
                EditorGUILayout.LabelField("Selected",brush?brush.name:"Choose a brush");
                settings.size=EditorGUILayout.FloatField("Diameter (metres)",settings.size);settings.strength=EditorGUILayout.Slider("Strength",settings.strength,0,1);settings.rotation=EditorGUILayout.Slider("Rotation",settings.rotation,0,360);settings.falloff=EditorGUILayout.Slider("Edge falloff",settings.falloff,0,1);settings.invert=EditorGUILayout.Toggle("Invert mask",settings.invert);
                if(mode==StampMode.Sculpt){settings.operation=(HeightOperation)EditorGUILayout.EnumPopup("Height operation",settings.operation);if(settings.operation==HeightOperation.Absolute)settings.absoluteHeight=EditorGUILayout.FloatField("World elevation",settings.absoluteHeight);else settings.height=EditorGUILayout.FloatField("Add/subtract metres",settings.height);}
                else settings.layer=(TerrainLayer)EditorGUILayout.ObjectField("Existing target layer",settings.layer,typeof(TerrainLayer),false);
                center=EditorGUILayout.Vector2Field("World X/Z centre",center);pick=EditorGUILayout.Toggle("Pick centre in Scene",pick);
                if(EditorGUI.EndChangeCheck())Invalidate();
                if(GUILayout.Button("Save settings as new preset")&&brush){string path=EditorUtility.SaveFilePanelInProject("Save brush preset","Custom brush","asset","Choose a new preset asset");if(!string.IsNullOrEmpty(path)&&!AssetDatabase.LoadMainAssetAtPath(path)){var p=CreateInstance<TerrainStampPreset>();p.brush=brush;p.settings=settings.Copy();AssetDatabase.CreateAsset(p,path);AssetDatabase.SaveAssetIfDirty(p);}}
                using(new EditorGUI.DisabledScope(!brush||brush.mode!=mode||!terrain||EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if(GUILayout.Button("Preview (no terrain changes)")){preview=TerrainStampOperations.Prepare(Tile,brush,settings,center);message=preview.Summary;SceneView.RepaintAll();}
                    using(new EditorGUI.DisabledScope(preview==null||preview.ChangedCells==0))if(GUILayout.Button("Apply preview"))
                    {
                        if(preview.Sculpt||EditorUtility.DisplayDialog("Blend terrain paint?","This blends existing unprotected texture weights. Protected overrides remain unchanged. Protect the resulting paint explicitly before procedural rebuilds.","Apply","Cancel")){TerrainStampOperations.Apply(preview);Invalidate();message="Applied. Undo/Redo is available. Save the scene and working assets normally.";}
                    }
                }
                if(Tile&&!TerrainPainter.Empty(Tile.state.nativeDirty)&&GUILayout.Button("Protect pending paint rectangle"))
                    if(EditorUtility.DisplayDialog("Protect paint","Protect all paint in the pending rectangle against procedural painting and subsequent stamps?","Protect","Cancel")){PaintHistory.Resolve(Tile,Tile.state.nativeDirty,true,1);Invalidate();}
                if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            }
            catch(ExitGUIException){throw;}catch(Exception e){message=e.Message;preview=null;}
            finally{EditorGUILayout.EndScrollView();}
        }
        void SceneGUI(SceneView view)
        {
            if(!terrain)return;var collider=terrain.GetComponent<TerrainCollider>();var e=Event.current;
            if(pick&&collider){HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));if(e.type==EventType.MouseDown&&e.button==0&&!e.alt&&collider.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit,100000)){center=new Vector2(hit.point.x,hit.point.z);Invalidate();e.Use();}}
            var p=new Vector3(center.x,terrain.SampleHeight(new Vector3(center.x,0,center.y))+terrain.GetPosition().y,center.y);Handles.color=Color.cyan;Handles.DrawWireDisc(p,Vector3.up,Mathf.Max(.1f,settings.size/2));
            if(preview==null)return;var r=preview.Region;int step=Mathf.Max(1,Mathf.Max(r.width,r.height)/24);
            for(int y=0;y<r.height;y+=step)for(int x=0;x<r.width;x+=step)
            {
                if(preview.Sculpt){if(x+step<r.width)Handles.DrawLine(preview.SurfacePoint(x,y),preview.SurfacePoint(x+step,y));if(y+step<r.height)Handles.DrawLine(preview.SurfacePoint(x,y),preview.SurfacePoint(x,y+step));}
                else{float change=preview.PaintChange(x,y);if(change>.01f){Handles.color=new Color(1,.6f,0,Mathf.Clamp01(change*3));Handles.DotHandleCap(0,preview.PaintPoint(x,y),Quaternion.identity,Mathf.Max(.3f,settings.size/80),EventType.Repaint);}}
            }
        }
    }
}
