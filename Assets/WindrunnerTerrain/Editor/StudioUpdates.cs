using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace Windrunner.TerrainAuthoring.Editor
{
    [InitializeOnLoad]
    public static class StudioUpdates
    {
        static double next;
        static bool inspect=true, undoHold;
        static readonly HashSet<StudioTile> blocked=new HashSet<StudioTile>();
        public static ulong CompletedRebuilds {get;private set;}
        public static string LastResult {get;private set;}
        public static StudioTile[] Tiles()=>UnityEngine.Object.FindObjectsByType<StudioTile>(FindObjectsInactive.Exclude)
            .Where(t=>t.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(t.gameObject.scene)).ToArray();
        static StudioUpdates()
        {
            TerrainCallbacks.heightmapChanged+=Height;
            TerrainCallbacks.textureChanged+=Texture;
            Spline.Changed+=(s,k,m)=>Schedule();
            EditorApplication.hierarchyChanged+=Schedule;
            EditorApplication.projectChanged+=Schedule;
            Undo.undoRedoPerformed+=()=>{undoHold=true;inspect=false;blocked.Clear();};
            Undo.postprocessModifications+=mods=>{if(mods.Any(m=>Relevant(m.currentValue.target)))Schedule();return mods;};
            EditorSceneManager.sceneOpened+=(s,m)=>Schedule();
            EditorSceneManager.sceneSaving+=SaveAuthoredTiles;
            EditorApplication.update+=Update;
        }
        static void SaveAuthoredTiles(Scene scene,string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            foreach(var t in Tiles().Where(t=>t.gameObject.scene==scene && t.state && t.ribbons))
            {
                var s=t.state;
                if(s.automatic && !undoHold && TerrainPainter.Empty(s.nativeDirty))
                {
                    try {Rebuild(t,default,RoadPath.InScene(t),false);}
                    catch(Exception e){LastResult=e.Message;Debug.LogWarning("Saved authoring inputs with pending paint: "+e.Message,t);}
                }
                AssetDatabase.SaveAssetIfDirty(s);
                if(s.working)AssetDatabase.SaveAssetIfDirty(s.working);
                if(t.ribbons.sharedMesh)AssetDatabase.SaveAssetIfDirty(t.ribbons.sharedMesh);
            }
        }
        static bool Relevant(UnityEngine.Object o)=>o is RoadProfile||o is SurfaceProfile||o is StudioRoad||o is SplineContainer||
            o is Transform t && t.GetComponentInChildren<StudioRoad>(true);
        public static void Schedule(){inspect=true;undoHold=false;blocked.Clear();next=EditorApplication.timeSinceStartup+.55;}
        static void Dirty(StudioTile t,RectInt region)
        {
            if(TerrainPainter.Empty(region))return;
            var s=t.state;var union=TerrainPainter.Union(s.dirty,region);
            if(union!=s.dirty){s.dirty=union;EditorUtility.SetDirty(s);}
        }
        static void Height(Terrain terrain,RectInt rect,bool synced)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||TerrainPainter.Writing)return;
            var d=terrain.terrainData;var s=d.size;var o=terrain.GetPosition();float step=1f/(d.heightmapResolution-1);
            foreach(var tile in Tiles())
            {
                if(!tile.state||!tile.terrain||tile.gameObject.scene!=terrain.gameObject.scene)continue;
                float pad=(tile.state.profile?tile.state.profile.curvatureRadius:8)+2;
                var world=Rect.MinMaxRect(o.x+rect.xMin*step*s.x-pad,o.z+rect.yMin*step*s.z-pad,o.x+rect.xMax*step*s.x+pad,o.z+rect.yMax*step*s.z+pad);
                Dirty(tile,TerrainPainter.Pixels(tile.terrain,world));
            }
            Schedule();
        }
        static void Texture(Terrain terrain,string name,RectInt rect,bool synced)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||TerrainPainter.Writing||name!=TerrainData.AlphamapTextureName)return;
            foreach(var tile in Tiles())if(tile.terrain==terrain&&tile.state)
            {
                tile.state.nativeDirty=TerrainPainter.Union(tile.state.nativeDirty,TerrainPainter.Clip(rect,TerrainPainter.Full(terrain.terrainData)));
                EditorUtility.SetDirty(tile.state);
            }
        }
        public static List<RoadFootprint> Stamps(StudioTile tile,List<RoadPath> paths)=>paths.Select(p=>new RoadFootprint{
            key=p.key,signature=p.signature,rect=TerrainPainter.Pixels(tile.terrain,p.bounds)}).Where(r=>!TerrainPainter.Empty(r.rect)).ToList();
        static string Rules(StudioTile t)=>Hash128.Compute(EditorJsonUtility.ToJson(t.state.profile)+t.terrain.GetPosition().ToString("R")).ToString();
        public static RectInt Pending(StudioTile t,List<RoadFootprint> now)
        {
            return Regions(t,now,default).Aggregate(default(RectInt),TerrainPainter.Union);
        }
        public static List<RectInt> Regions(StudioTile t,List<RoadFootprint> now,RectInt selected,bool selectionOnly=false)
        {
            var s=t.state;
            if(!selectionOnly && s.surfaceSignature!=Rules(t))return new List<RectInt>{TerrainPainter.Full(s.working)};
            var regions=new List<RectInt>();
            void Add(RectInt rect)
            {
                rect=TerrainPainter.Clip(rect,TerrainPainter.Full(s.working));
                if(TerrainPainter.Empty(rect))return;
                for(int i=0;i<regions.Count;i++)
                    if(rect.Overlaps(regions[i]))
                    {
                        rect=TerrainPainter.Union(rect,regions[i]);regions.RemoveAt(i);i=-1;
                    }
                regions.Add(rect);
            }
            if(!selectionOnly)Add(s.dirty);
            Add(selected);
            foreach(var old in s.footprints)if(!now.Any(n=>n.key==old.key && n.signature==old.signature && n.rect==old.rect))Add(old.rect);
            foreach(var n in now)if(!s.footprints.Any(old=>n.key==old.key && n.signature==old.signature && n.rect==old.rect))Add(n.rect);
            return regions;
        }
        static void Update()
        {
            if(!inspect||undoHold||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||
                EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next||GUIUtility.hotControl!=0)return;
            inspect=false;
            foreach(var t in Tiles())
            {
                if(!t.state||!t.terrain||!t.state.profile||!t.state.automatic||!TerrainPainter.Empty(t.state.nativeDirty)||blocked.Contains(t))continue;
                try
                {
                    var paths=RoadPath.InScene(t);
                    if(Regions(t,Stamps(t,paths),default).Count>0)Rebuild(t,default,paths,false);
                }
                catch(Exception e){blocked.Add(t);LastResult=e.Message;Debug.LogWarning("Terrain Studio paused: "+e.Message,t);}
            }
        }
        public static void Rebuild(StudioTile t,RectInt requested,List<RoadPath> paths,bool interactive,bool revert=false,bool selectionOnly=false)
        {
            if(!t.ribbons||!t.ribbons.sharedMesh)throw new InvalidOperationException("Missing baked wheel mesh.");
            var error=TerrainPainter.Validate(t,paths);
            if(error!=null)throw new InvalidOperationException(error);
            var stamps=Stamps(t,paths);
            var regions=Regions(t,stamps,requested,selectionOnly);
            if(regions.Count==0)return;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Terrain Studio rebuild");
            try
            {
                var changed=regions.Select(r=>PaintHistory.Differences(t.state,r)).Aggregate(default(RectInt),TerrainPainter.Union);
                if(!TerrainPainter.Empty(changed))
                {
                    t.state.nativeDirty=TerrainPainter.Union(t.state.nativeDirty,changed);
                    EditorUtility.SetDirty(t.state);
                    throw new InvalidOperationException("Detected new native terrain paint. Capture it or release it explicitly.");
                }
                Undo.RegisterCompleteObjectUndo(t.state,"Update terrain paint record");
                foreach(var region in regions)
                {
                    TerrainPainter.Apply(t,region,paths,revert,interactive ? v=>EditorUtility.DisplayCancelableProgressBar("Terrain Studio",t.terrain.name,v) : null);
                    PaintHistory.Remember(t.state,region);
                }
                bool affectsRoad=stamps.Concat(t.state.footprints).Any(stamp=>regions.Any(r=>r.Overlaps(stamp.rect)));
                if(revert || affectsRoad)WheelRibbons.Bake(t,revert?new List<RoadPath>():paths);
                t.state.footprints=stamps;
                if(!selectionOnly||regions.Contains(TerrainPainter.Full(t.state.working)))
                {t.state.surfaceSignature=Rules(t);t.state.dirty=default;}
                else if(regions.Any(r=>TerrainPainter.Clip(r,t.state.dirty)==t.state.dirty))t.state.dirty=default;
                if(revert){t.state.automatic=false;t.state.dirty=TerrainPainter.Full(t.state.working);}
                EditorUtility.SetDirty(t.state);EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                LastResult=$"{regions.Sum(r=>r.width*r.height):N0} cells across {regions.Count} region(s) in {watch.Elapsed.TotalMilliseconds:F0} ms";
                Undo.CollapseUndoOperations(group);CompletedRebuilds++;
            }
            catch {Undo.RevertAllDownToGroup(group);throw;}
            finally {if(interactive)EditorUtility.ClearProgressBar();}
        }
    }
}
