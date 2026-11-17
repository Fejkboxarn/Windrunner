using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Windrunner.TerrainAuthoring.Editor
{
    public static class PaintHistory
    {
        internal static string Invalid(TilePaintState s,TerrainLayer[] palette)
        {
            var full=TerrainPainter.Full(s.working);
            string Check(PaintPatch p,string label,bool entire)
            {
                if(p==null || TerrainPainter.Empty(p.rect) || TerrainPainter.Clip(p.rect,full)!=p.rect || (entire && p.rect!=full) ||
                    p.layers==null || p.layers.Length==0 || p.weights==null || (long)p.rect.width*p.rect.height*p.layers.Length!=p.weights.LongLength ||
                    !(p.opacity>=0 && p.opacity<=1) || p.layers.Any(l=>!l||!palette.Contains(l)) || p.layers.Distinct().Count()!=p.layers.Length)
                    return $"{AssetDatabase.GetAssetPath(s)}: {label} is incomplete or incompatible. Restore the paint history and its captured TerrainLayers together.";
                return null;
            }
            string issue=Check(s.original,"original paint",true)??Check(s.lastApplied,"last applied paint",true);
            if(issue!=null)return issue;
            if(s.protectedPaint==null || s.footprints==null || s.footprints.Any(f=>f==null))
                return $"{s.name}: restore missing protection or footprint records from the matching checkpoint.";
            for(int i=0;i<s.protectedPaint.Count;i++)
            {
                issue=Check(s.protectedPaint[i],"protected override "+(i+1),false);
                if(issue!=null)return issue;
            }
            return null;
        }
        public static RectInt ReviewNativeEdits(StudioTile tile)
        {
            var s=tile.state;
            string issue=Invalid(s,s.working.terrainLayers);
            if(issue!=null)throw new InvalidOperationException(issue);
            var found=TerrainPainter.Union(s.nativeDirty,Differences(s,TerrainPainter.Full(s.working)));
            if(found!=s.nativeDirty)
            {
                Undo.RecordObject(s,"Review native paint changes");
                s.nativeDirty=found;EditorUtility.SetDirty(s);
            }
            return found;
        }
        public static void Clear(StudioTile tile,int index)
        {
            var s=tile.state;var patch=s.protectedPaint[index];
            Undo.RegisterCompleteObjectUndo(s,"Remove protected paint");
            s.protectedPaint.RemoveAt(index);s.dirty=TerrainPainter.Union(s.dirty,patch.rect);
            EditorUtility.SetDirty(s);StudioUpdates.Schedule();
        }
        public static RectInt Differences(TilePaintState state, RectInt rect)
        {
            var previous=state.lastApplied;
            var d=state.working;var layers=d.terrainLayers;
            var map=d.GetAlphamaps(rect.x,rect.y,rect.width,rect.height);
            var channels=layers.Select(l=>Array.IndexOf(previous.layers,l)).ToArray();
            int left=rect.xMax,top=rect.yMax,right=0,bottom=0;
            for(int y=0;y<rect.height;y++)for(int x=0;x<rect.width;x++)
            {
                int index=((y+rect.y)*previous.rect.width+x+rect.x)*previous.layers.Length;
                for(int c=0;c<layers.Length;c++)
                {
                    float expected=channels[c]<0?0:previous.weights[index+channels[c]];
                    if(Mathf.Abs(expected-map[y,x,c])<.001f)continue;
                    left=Math.Min(left,x+rect.x);right=Math.Max(right,x+rect.x+1);
                    top=Math.Min(top,y+rect.y);bottom=Math.Max(bottom,y+rect.y+1);break;
                }
            }
            return TerrainPainter.Bounds(left,top,right,bottom);
        }
        public static void Remember(TilePaintState state,RectInt rect)
        {
            var last=state.lastApplied;var patch=TerrainPainter.Capture(state.working,rect);
            if(!last.layers.SequenceEqual(patch.layers))
            {
                var remapped=new float[last.rect.width*last.rect.height*patch.layers.Length];
                for(int c=0;c<last.layers.Length;c++)
                {
                    int target=Array.IndexOf(patch.layers,last.layers[c]);if(target<0)continue;
                    for(int i=0;i<last.rect.width*last.rect.height;i++)remapped[i*patch.layers.Length+target]=last.weights[i*last.layers.Length+c];
                }
                last.weights=remapped;last.layers=patch.layers;
            }
            int stride=last.layers.Length;
            for(int y=0;y<rect.height;y++)
                Array.Copy(patch.weights,y*rect.width*stride,last.weights,((y+rect.y)*last.rect.width+rect.x)*stride,rect.width*stride);
        }
        public static void Resolve(StudioTile tile,RectInt rect,bool protect,float opacity)
        {
            var s=tile.state;
            var issue=Invalid(s,s.working.terrainLayers);
            if(issue!=null)throw new InvalidOperationException(issue);
            rect=TerrainPainter.Clip(rect,TerrainPainter.Full(s.working));
            if(TerrainPainter.Empty(rect))throw new ArgumentException("Select a region first.");
            Undo.RegisterCompleteObjectUndo(s,"Resolve terrain paint");
            if(protect)
            {
                var patch=TerrainPainter.Capture(s.working,rect);patch.opacity=Mathf.Clamp01(opacity);
                s.protectedPaint.Add(patch);
            }
            Remember(s,rect);
            if(TerrainPainter.Empty(s.nativeDirty)||TerrainPainter.Clip(rect,s.nativeDirty)==s.nativeDirty)s.nativeDirty=default;
            s.dirty=TerrainPainter.Union(s.dirty,rect);EditorUtility.SetDirty(s);StudioUpdates.Schedule();
        }
    }
}
