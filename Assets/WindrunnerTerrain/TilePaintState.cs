using System;
using System.Collections.Generic;
using UnityEngine;
namespace Windrunner.TerrainAuthoring
{
    [Serializable] public class PaintPatch
    {
        public RectInt rect;
        [Range(0,1)] public float opacity=1;
        public TerrainLayer[] layers;
        public float[] weights;
    }
    [Serializable] public class RoadFootprint
    {
        public string key;
        public string signature;
        public RectInt rect;
    }
    [PreferBinarySerialization]
    public sealed class TilePaintState : ScriptableObject
    {
        public TerrainData working;
        public SurfaceProfile profile;
        public PaintPatch original, lastApplied;
        public List<PaintPatch> protectedPaint=new List<PaintPatch>();
        public List<RoadFootprint> footprints=new List<RoadFootprint>();
        public bool automatic;
        public RectInt dirty, nativeDirty;
        public string surfaceSignature;
    }
}
