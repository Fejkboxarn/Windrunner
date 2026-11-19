using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Windrunner.TerrainAuthoring.Editor
{
    public static class TerrainStampOperations
    {
        public sealed class Preview
        {
            internal StudioTile tile;internal TilePaintState state;internal Vector3 terrainPosition;internal TerrainData data;internal TerrainStamp brush;internal int recipeRevision;internal StampSettings settings;
            internal Vector2 center;internal RectInt region;internal float[,] beforeHeight,afterHeight;internal float[,,] beforePaint,afterPaint;
            internal TerrainLayer[] palette;internal int dataRevision,stateRevision,brushRevision;internal bool used;
            public RectInt Region=>region;
            public bool Sculpt=>beforeHeight!=null;
            public Vector3 PaintPoint(int x,int y){var point=TerrainPainter.World(tile.terrain,region.x+x,region.y+y);return new Vector3(point.x,tile.terrain.SampleHeight(new Vector3(point.x,0,point.y))+tile.terrain.GetPosition().y+.2f,point.y);}
            public float PaintChange(int x,int y){int layer=Array.IndexOf(palette,settings.layer);return Mathf.Abs(afterPaint[y,x,layer]-beforePaint[y,x,layer]);}
            public int ChangedCells {get;internal set;}
            public string Summary {get;internal set;}
            public Vector3 SurfacePoint(int x,int y)
            {
                var o=tile.terrain.GetPosition();var size=data.size;float n=data.heightmapResolution-1;
                return new Vector3(o.x+(region.x+x)*size.x/n,o.y+afterHeight[y,x]*size.y,o.z+(region.y+y)*size.z/n);
            }
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public static void Validate(StudioTile tile,bool sharedReferences=true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stamping is Edit Mode only.");
            if(!tile||!tile.state||!tile.terrain)throw new InvalidOperationException("Select a Terrain with its own Terrain Studio working copy.");
            var issue=TerrainPainter.Validate(tile,Array.Empty<RoadPath>());if(issue!=null)throw new InvalidOperationException(issue);
            var data=tile.state.working;string path=AssetDatabase.GetAssetPath(data),history=AssetDatabase.GetAssetPath(tile.state);
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!history.StartsWith("Assets/",StringComparison.Ordinal)||!AssetDatabase.IsOpenForEdit(data)||!AssetDatabase.IsOpenForEdit(tile.state))throw new InvalidOperationException("Working TerrainData and paint history must be editable project assets.");
            if(tile.state.automatic)throw new InvalidOperationException("Turn OFF Terrain Studio Automatic Updates first. Sculpt stamps never silently repaint.");
            if(!tile.gameObject.scene.IsValid()||EditorUtility.IsPersistent(tile))throw new InvalidOperationException("Select a scene Terrain, not a prefab asset.");
            if(!sharedReferences)return;
            var candidates=AssetDatabase.FindAssets("t:Scene t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .Where(other=>other!=tile.gameObject.scene.path&&other.StartsWith("Assets/",StringComparison.Ordinal)).ToArray();
            // One union query avoids re-traversing shared vendor dependencies for every prefab.
            // Recompute on every preview/apply; no stale ownership cache can authorize a write.
            if(!AssetDatabase.GetDependencies(candidates,true).Contains(path))return;
            foreach(string other in candidates)
                if(AssetDatabase.GetDependencies(other,true).Contains(path))throw new InvalidOperationException("Working data is also referenced by "+other+". Attach an independent copy before editing.");
        }
        static float Influence(TerrainStamp b,ushort[] values,StampSettings s,Vector2 p,Vector2 center)
        {
            Vector2 q=(p-center)/(s.size*.5f);float angle=-s.rotation*Mathf.Deg2Rad,c=Mathf.Cos(angle),sn=Mathf.Sin(angle);
            q=new Vector2(q.x*c-q.y*sn,q.x*sn+q.y*c);float radius=q.magnitude;if(radius>=1)return 0;
            float sample=TerrainStamp.Sample(values,b.field.width,q.x*.5f+.5f,q.y*.5f+.5f);if(s.invert)sample=1-sample;
            float fade=s.falloff<=0?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((1-radius)/s.falloff));return sample*fade;
        }
        static float Protected(TilePaintState s,int x,int y)
        {float writable=1;foreach(var p in s.protectedPaint)if(p.rect.Contains(new Vector2Int(x,y)))writable*=1-Mathf.Clamp01(p.opacity);return writable;}
        public static Preview Prepare(StudioTile tile,TerrainStamp brush,StampSettings settings,Vector2 center)
        {
            Validate(tile);
            if(!brush||!brush.field||brush.field.format!=TextureFormat.R16||!brush.field.isReadable||brush.field.width!=brush.field.height)throw new InvalidOperationException("Choose a readable, square, linear R16 library stamp.");
            var s=settings.Copy();if(!Finite(center.x)||!Finite(center.y)||!Finite(s.size)||!Finite(s.strength)||!Finite(s.rotation)||!Finite(s.falloff)||!Finite(s.height)||!Finite(s.absoluteHeight)||s.size<=0||s.strength<0||s.strength>1||s.falloff<0||s.falloff>1||s.height<0)throw new ArgumentException("Brush values must be finite, with positive size and normalized strength/falloff.");
            var data=tile.state.working;var terrain=tile.terrain;var origin=terrain.GetPosition();var size=data.size;bool sculpt=brush.mode==StampMode.Sculpt;
            int resolution=sculpt?data.heightmapResolution:data.alphamapResolution;
            var world=new Rect(center-Vector2.one*s.size*.5f,Vector2.one*s.size);
            var region=TerrainPainter.Clip(TerrainPainter.Bounds(Mathf.FloorToInt((world.xMin-origin.x)/size.x*(resolution-1)),Mathf.FloorToInt((world.yMin-origin.z)/size.z*(resolution-1)),Mathf.CeilToInt((world.xMax-origin.x)/size.x*(resolution-1))+1,Mathf.CeilToInt((world.yMax-origin.z)/size.z*(resolution-1))+1),new RectInt(0,0,resolution,resolution));
            if(TerrainPainter.Empty(region))throw new InvalidOperationException("The brush does not overlap the selected Terrain.");
            var plan=new Preview{tile=tile,state=tile.state,terrainPosition=tile.terrain.GetPosition(),recipeRevision=EditorUtility.GetDirtyCount(brush),data=data,brush=brush,settings=s,center=center,region=region,palette=data.terrainLayers,dataRevision=EditorUtility.GetDirtyCount(data),stateRevision=EditorUtility.GetDirtyCount(tile.state),brushRevision=EditorUtility.GetDirtyCount(brush.field)};
            var values=brush.Samples();
            if(sculpt)
            {
                plan.beforeHeight=data.GetHeights(region.x,region.y,region.width,region.height);plan.afterHeight=(float[,])plan.beforeHeight.Clone();
                for(int y=0;y<region.height;y++)for(int x=0;x<region.width;x++)
                {
                    var point=new Vector2(origin.x+(x+region.x)*size.x/(resolution-1),origin.z+(y+region.y)*size.z/(resolution-1));float influence=Influence(brush,values,s,point,center);if(influence<=0)continue;
                    float old=plan.beforeHeight[y,x];float next=s.operation==HeightOperation.Absolute?Mathf.Lerp(old,(s.absoluteHeight-origin.y)/size.y,s.strength*influence):old+(s.operation==HeightOperation.Lower?-1:1)*s.height*s.strength*influence/size.y;
                    next=Mathf.Clamp01(next);plan.afterHeight[y,x]=next;if(Mathf.Abs(next-old)>1f/65535)plan.ChangedCells++;
                }
            }
            else
            {
                int layer=Array.IndexOf(plan.palette,s.layer);if(layer<0||!s.layer)throw new InvalidOperationException("Choose an existing target TerrainLayer. Append missing layers explicitly with Terrain Prototype Transfer; indices will not be replaced here.");
                if(!TerrainPainter.Empty(tile.state.nativeDirty)||!TerrainPainter.Empty(PaintHistory.Differences(tile.state,TerrainPainter.Full(data))))throw new InvalidOperationException("Protect or release pending native paint in Terrain Studio before a new paint stamp.");
                plan.beforePaint=data.GetAlphamaps(region.x,region.y,region.width,region.height);plan.afterPaint=(float[,,])plan.beforePaint.Clone();
                for(int y=0;y<region.height;y++)for(int x=0;x<region.width;x++)
                {
                    float alpha=Influence(brush,values,s,TerrainPainter.World(terrain,x+region.x,y+region.y),center)*s.strength*Protected(tile.state,x+region.x,y+region.y);if(alpha<=0)continue;
                    for(int c=0;c<plan.palette.Length;c++)plan.afterPaint[y,x,c]=plan.beforePaint[y,x,c]*(1-alpha)+(c==layer?alpha:0);
                    if(Mathf.Abs(plan.afterPaint[y,x,layer]-plan.beforePaint[y,x,layer])>.00001f)plan.ChangedCells++;
                }
            }
            plan.Summary=$"{terrain.name} / {data.name}: {plan.ChangedCells:N0} cells. "+(sculpt?"Height only; generated paint remains pending. Absolute mode flattens toward the chosen world elevation through this mask.":"Paint only; existing weights blend toward the selected layer. Protected overrides are respected. New paint is pending protection in Terrain Studio.");return plan;
        }
        static bool Equal(Array a,Array b)
        {if(a.Length!=b.Length)return false;var x=a.GetEnumerator();var y=b.GetEnumerator();while(x.MoveNext()&&y.MoveNext())if(!x.Current.Equals(y.Current))return false;return true;}
        public static void Apply(Preview p)
        {
            if(p==null||p.used)throw new InvalidOperationException("Create a fresh preview first.");Validate(p.tile);
            var t=p.tile;var d=t.state.working;var r=p.region;
            if(t.state!=p.state||t.terrain.GetPosition()!=p.terrainPosition||EditorUtility.GetDirtyCount(p.brush)!=p.recipeRevision||d!=p.data||EditorUtility.GetDirtyCount(d)!=p.dataRevision||EditorUtility.GetDirtyCount(t.state)!=p.stateRevision||EditorUtility.GetDirtyCount(p.brush.field)!=p.brushRevision||!d.terrainLayers.SequenceEqual(p.palette))throw new InvalidOperationException("Preview is stale. Terrain, history, palette or brush changed; preview again.");
            if(!Equal(p.Sculpt?(Array)d.GetHeights(r.x,r.y,r.width,r.height):d.GetAlphamaps(r.x,r.y,r.width,r.height),p.Sculpt?(Array)p.beforeHeight:p.beforePaint))throw new InvalidOperationException("Preview is stale: terrain samples changed.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Terrain Studio manual "+p.brush.mode+" stamp");
            var objects=new[]{(Object)d,t.state}.Concat(p.Sculpt?Array.Empty<Object>():d.alphamapTextures.Cast<Object>()).ToArray();Undo.RegisterCompleteObjectUndo(objects,"Terrain stamp and paint protection state");
            try
            {
                TerrainPainter.WithoutNativePaintTracking(()=>{if(p.Sculpt)d.SetHeights(r.x,r.y,p.afterHeight);else d.SetAlphamaps(r.x,r.y,p.afterPaint);});
                var dirty=TerrainPainter.Pixels(t.terrain,new Rect(p.center-Vector2.one*(p.settings.size*.5f+t.state.profile.curvatureRadius+2),Vector2.one*(p.settings.size+2*t.state.profile.curvatureRadius+4)));
                t.state.dirty=TerrainPainter.Union(t.state.dirty,dirty);
                if(!p.Sculpt)t.state.nativeDirty=TerrainPainter.Union(t.state.nativeDirty,r);
                EditorUtility.SetDirty(d);EditorUtility.SetDirty(t.state);EditorSceneManager.MarkSceneDirty(t.gameObject.scene);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);p.used=true;
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
    }
}
