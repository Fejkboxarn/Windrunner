using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Windrunner.TerrainAuthoring.Editor
{
    public static class TerrainPainter
    {
        public static bool Writing {get;private set;}
        public static RectInt Full(TerrainData d)=>new RectInt(0,0,d.alphamapWidth,d.alphamapHeight);
        public static bool Empty(RectInt r)=>r.width<=0||r.height<=0;
        public static RectInt Bounds(int x,int y,int right,int top)=>new RectInt(x,y,Mathf.Max(0,right-x),Mathf.Max(0,top-y));
        public static RectInt Union(RectInt a,RectInt b)=>Empty(a)?b:Empty(b)?a:Bounds(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Max(a.xMax,b.xMax),Math.Max(a.yMax,b.yMax));
        public static RectInt Clip(RectInt a,RectInt b)=>Bounds(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Min(a.xMax,b.xMax),Math.Min(a.yMax,b.yMax));
        public static RectInt Pixels(Terrain t,Rect world)
        {
            var d=t.terrainData;var o=t.GetPosition();var s=d.size;
            return Clip(Full(d),Bounds(Mathf.FloorToInt((world.xMin-o.x)/s.x*(d.alphamapWidth-1)),Mathf.FloorToInt((world.yMin-o.z)/s.z*(d.alphamapHeight-1)),
                Mathf.CeilToInt((world.xMax-o.x)/s.x*(d.alphamapWidth-1))+1,Mathf.CeilToInt((world.yMax-o.z)/s.z*(d.alphamapHeight-1))+1));
        }
        public static Vector2 World(Terrain t,int x,int y)
        {
            var d=t.terrainData; var o=t.GetPosition();
            return new Vector2(o.x+x*d.size.x/(d.alphamapWidth-1),o.z+y*d.size.z/(d.alphamapHeight-1));
        }
        public static PaintPatch Capture(TerrainData d,RectInt rect)
        {
            rect=Clip(rect,Full(d));
            if(Empty(rect))throw new ArgumentException("Select a region inside the terrain.");
            var data=d.GetAlphamaps(rect.x,rect.y,rect.width,rect.height);
            var values=new float[data.Length];Buffer.BlockCopy(data,0,values,0,values.Length*sizeof(float));
            return new PaintPatch{rect=rect,layers=d.terrainLayers,weights=values};
        }
        static void Overlay(float[,,] output,RectInt rect,PaintPatch patch,TerrainLayer[] layers)
        {
            var overlap=Clip(rect,patch.rect);
            if(Empty(overlap))return;
            var indices=patch.layers.Select(l=>Array.IndexOf(layers,l)).ToArray();
            if(indices.Any(i=>i<0))throw new InvalidOperationException("Restore the captured TerrainLayers before painting.");
            for(int y=overlap.y;y<overlap.yMax;y++)for(int x=overlap.x;x<overlap.xMax;x++)
            {
                int row=y-rect.y,col=x-rect.x,index=((y-patch.rect.y)*patch.rect.width+x-patch.rect.x)*indices.Length;
                for(int c=0;c<layers.Length;c++)output[row,col,c]*=1-patch.opacity;
                for(int c=0;c<indices.Length;c++)output[row,col,indices[c]]+=patch.weights[index+c]*patch.opacity;
            }
        }
        public static string Validate(StudioTile tile,IEnumerable<RoadPath> paths)
        {
            if(!tile||!tile.terrain||!tile.state||!tile.state.working||tile.terrain.terrainData!=tile.state.working)return "Working TerrainData binding changed.";
            var state=tile.state;var p=state.profile;
            if(!p||!p.grass||!p.soil||!p.rock)return "Assign grass, soil and rock TerrainLayers.";
            if(state.original==null||state.original.rect!=Full(state.working))return "Alphamap dimensions changed. Restore resolution before rebuilding.";
            if(StudioUpdates.Tiles().Any(other=>other!=tile&&other.terrain&&other.terrain.terrainData==state.working))
                return "Multiple Studio terrains share working data. Assign independent copies before painting.";
            if(tile.terrain.transform.lossyScale!=Vector3.one||Quaternion.Angle(tile.terrain.transform.rotation,Quaternion.identity)>.01f)
                return "Unity Terrain must be unrotated with unit world scale.";
            var layers=state.working.terrainLayers;
            if(layers.Length==0||layers.Any(l=>!l)||layers.Distinct().Count()!=layers.Length)return "Terrain palette contains a missing or duplicate layer.";
            if(state.protectedPaint.Concat(new[]{state.original}).Any(patch=>patch.layers.Any(l=>!layers.Contains(l))))return "Captured palette layer removed; restore it before rebuilding.";
            if(paths.Any(r=>!r.Profile.surface || r.Profile.carriage&&!r.Profile.trackMaterial))return "Road surface or wheel-wear material missing.";
            var collider=tile.terrain.GetComponent<TerrainCollider>();
            if(!collider||collider.terrainData!=state.working)return "TerrainCollider and Terrain must share the working TerrainData.";
            return null;
        }
        // Uses adjacent terrain at shared borders; never wraps a height sample onto an unrelated tile.
        public sealed class Heights
        {
            readonly Terrain[] terrains;
            readonly Terrain home;
            public Heights(Terrain home){this.home=home;terrains=home.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Where(t=>t.terrainData).ToArray();}
            public static bool Contains(Terrain t,Vector2 p)
            {var o=t.GetPosition();var s=t.terrainData.size;return p.x>=o.x&&p.y>=o.z&&p.x<=o.x+s.x&&p.y<=o.z+s.z;}
            public float At(Vector2 p)
            {
                Terrain t=home;
                if(!Contains(t,p))foreach(var other in terrains)if(Contains(other,p)){t=other;break;}
                var o=t.GetPosition();var d=t.terrainData;
                return o.y+d.GetInterpolatedHeight(Mathf.Clamp01((p.x-o.x)/d.size.x),Mathf.Clamp01((p.y-o.z)/d.size.z));
            }
            public Vector3 Normal(Vector2 p,float radius,out float curvature)
            {
                radius=Mathf.Max(1,radius);
                float l=At(p-Vector2.right*radius),r=At(p+Vector2.right*radius),b=At(p-Vector2.up*radius),f=At(p+Vector2.up*radius);
                curvature=Mathf.Clamp((At(p)-(l+r+b+f)*.25f)/radius,-1,1);
                return new Vector3(l-r,2*radius,b-f).normalized;
            }
        }
        public static TerrainLayer[] Palette(StudioTile tile,List<RoadPath> paths)
        {
            var p=tile.state.profile;
            return tile.state.working.terrainLayers.Concat(new[]{p.grass,p.soil,p.rock}).Concat(paths.Where(r=>!Empty(Pixels(tile.terrain,r.bounds))).Select(r=>r.Profile.surface)).Distinct().ToArray();
        }
        public static float[,,] Calculate(StudioTile tile,RectInt rect,List<RoadPath> paths,TerrainLayer[] layers,bool revert=false,Func<float,bool> cancel=null)
        {
            var error=Validate(tile,paths);if(error!=null)throw new InvalidOperationException(error);
            var state=tile.state;var profile=state.profile;
            var output=new float[rect.height,rect.width,layers.Length];
            if(revert)Overlay(output,rect,state.original,layers);
            else
            {
                var heights=new Heights(tile.terrain);
                int grass=Array.IndexOf(layers,profile.grass),soil=Array.IndexOf(layers,profile.soil),rock=Array.IndexOf(layers,profile.rock);
                var local=paths.Where(r=>!Empty(Clip(rect,Pixels(tile.terrain,r.bounds)))).ToArray();
                var channels=local.Select(r=>Array.IndexOf(layers,r.Profile.surface)).ToArray();
                float[] bandWeights=new float[layers.Length];
                for(int y=0;y<rect.height;y++)
                {
                    if(y%16==0 && cancel!=null && cancel(y/(float)rect.height))throw new OperationCanceledException();
                    for(int x=0;x<rect.width;x++)
                    {
                        var pos=World(tile.terrain,x+rect.x,y+rect.y);
                        var normal=heights.Normal(pos,1,out _);
                        heights.Normal(pos,profile.curvatureRadius,out float curvature);
                        var w=profile.Evaluate(pos,heights.At(pos),normal,curvature);
                        output[y,x,grass]+=w.x;output[y,x,soil]+=w.y;output[y,x,rock]+=w.z;
                        int band=int.MinValue;float sum=0,coverage=0;
                        for(int i=0;i<local.Length;i++)
                        {
                            float weight=local[i].Coverage(pos);
                            if(weight<=0)continue;
                            if(local[i].Profile.priority!=band)
                            {
                                if(sum>0)Mix(output,y,x,bandWeights,sum,coverage);
                                Array.Clear(bandWeights,0,bandWeights.Length);
                                sum=coverage=0;band=local[i].Profile.priority;
                            }
                            bandWeights[channels[i]]+=weight;sum+=weight;coverage=Mathf.Max(coverage,weight);
                        }
                        if(sum>0)Mix(output,y,x,bandWeights,sum,coverage);
                    }
                }
            }
            foreach(var patch in state.protectedPaint)Overlay(output,rect,patch,layers);
            return output;
        }
        static void Mix(float[,,] map,int y,int x,float[] weights,float sum,float coverage)
        {
            for(int i=0;i<weights.Length;i++)map[y,x,i]=Mathf.Lerp(map[y,x,i],weights[i]/sum,coverage);
        }
        public static void Apply(StudioTile tile,RectInt rect,List<RoadPath> paths,bool revert=false,Func<float,bool> cancel=null)
        {
            var state=tile.state;var d=state.working;
            if(!Empty(state.nativeDirty))throw new InvalidOperationException("Native paint pending. Capture or explicitly release it first.");
            rect=Clip(rect,Full(d));if(Empty(rect))return;
            string error=Validate(tile,paths);if(error!=null)throw new InvalidOperationException(error);
            var layers=Palette(tile,paths);var output=Calculate(tile,rect,paths,layers,revert,cancel);
            Writing=true;
            try
            {
                Undo.RegisterCompleteObjectUndo(d,"Terrain Studio paint");
                foreach(var texture in d.alphamapTextures)Undo.RegisterCompleteObjectUndo(texture,"Terrain Studio paint");
                if(!layers.SequenceEqual(d.terrainLayers))d.terrainLayers=layers;
                d.SetAlphamaps(rect.x,rect.y,output);EditorUtility.SetDirty(d);
            }
            finally {Writing=false;}
        }
    }
}
