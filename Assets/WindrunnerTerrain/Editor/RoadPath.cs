using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Windrunner.TerrainAuthoring.Editor
{
    public sealed class RoadPath
    {
        public readonly StudioRoad road;
        public readonly int splineIndex;
        public readonly bool closed;
        public readonly Vector2[] points;
        public readonly float[] lengths;
        public readonly Rect bounds;
        public readonly string key, signature;
        public RoadProfile Profile => road.profile;
        public RoadPath(StudioRoad road,int splineIndex)
        {
            this.road=road;
            var container=road.GetComponent<SplineContainer>();
            this.splineIndex=splineIndex;closed=container.Splines[splineIndex].Closed;
            float length=container.CalculateLength(splineIndex);
            int steps=Mathf.Clamp(Mathf.CeilToInt(length/.8f),1,65535);
            points=new Vector2[steps+1]; lengths=new float[steps+1];
            Vector2 lo=Vector2.positiveInfinity,hi=Vector2.negativeInfinity;
            for(int i=0;i<=steps;i++)
            {
                var p=container.EvaluatePosition(splineIndex,i/(float)steps);
                points[i]=new Vector2(p.x,p.z);
                if(i>0)lengths[i]=lengths[i-1]+Vector2.Distance(points[i],points[i-1]);
                lo=Vector2.Min(lo,points[i]); hi=Vector2.Max(hi,points[i]);
            }
            float margin=Profile.width/2+Profile.shoulderMetres*(1+Profile.edgeIrregularity)+Profile.trackWidth+Profile.trackSpacing;
            bounds=Rect.MinMaxRect(lo.x-margin,lo.y-margin,hi.x+margin,hi.y+margin);
            var id=GlobalObjectId.GetGlobalObjectIdSlow(road);
            key=(id.targetObjectId==0?road.GetEntityId().ToString():id.ToString())+"/"+splineIndex;
            signature=Signature(road);
            // A spatial hash bounds local sampling cost even on long, folded roads.
            for(int i=1;i<points.Length;i++)
            {
                Vector2 a=Vector2.Min(points[i-1],points[i])-Vector2.one*margin,b=Vector2.Max(points[i-1],points[i])+Vector2.one*margin;
                var first=Cell(a);var last=Cell(b);
                for(int y=first.y;y<=last.y;y++)for(int x=first.x;x<=last.x;x++)
                {
                    var cell=new Vector2Int(x,y);
                    if(!segments.TryGetValue(cell,out var list))segments.Add(cell,list=new List<int>());
                    list.Add(i);
                }
            }
        }
        readonly Dictionary<Vector2Int,List<int>> segments=new Dictionary<Vector2Int,List<int>>();
        static Vector2Int Cell(Vector2 p)=>Vector2Int.FloorToInt(p/8);
        public static string Signature(StudioRoad road)
        {
            var hash=new Hash128();
            hash.Append(EditorJsonUtility.ToJson(road.GetComponent<SplineContainer>()));
            hash.Append(EditorJsonUtility.ToJson(road.profile));
            var matrix=road.transform.localToWorldMatrix;hash.Append(ref matrix);
            return hash.ToString();
        }
        public float Coverage(Vector2 p)
        {
            if(!bounds.Contains(p)||!segments.TryGetValue(Cell(p),out var list))return 0;
            float distance=float.PositiveInfinity;
            foreach(int i in list)
            {
                Vector2 a=points[i-1],ab=points[i]-a;
                float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,.000001f));
                distance=Mathf.Min(distance,(a+t*ab-p).sqrMagnitude);
            }
            float noise=Mathf.PerlinNoise(p.x*.21f+17,p.y*.21f+5)-.5f;
            float edge=(Mathf.Sqrt(distance)-Profile.width*.5f)/Mathf.Max(.1f,Profile.shoulderMetres)+noise*Profile.edgeIrregularity;
            if(edge>=1)return 0;
            return Mathf.Clamp01(Profile.shoulderFade.Evaluate(Mathf.Clamp01(edge))*(1-Profile.vegetation)*Mathf.Lerp(.75f,1,Profile.wear));
        }
        public static List<RoadPath> InScene(StudioTile tile)
        {
            var paths=new List<RoadPath>();
            foreach(var root in tile.gameObject.scene.GetRootGameObjects())
                foreach(var road in root.GetComponentsInChildren<StudioRoad>())
                {
                    if(!road.isActiveAndEnabled||!road.profile)continue;
                    var container=road.GetComponent<SplineContainer>();
                    for(int i=0;i<container.Splines.Count;i++)
                        if(container.Splines[i].Count>=2)paths.Add(new RoadPath(road,i));
                }
            return paths.OrderBy(p=>p.Profile.priority).ThenBy(p=>p.key,System.StringComparer.Ordinal).ToList();
        }
    }
}
