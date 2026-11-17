using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace Windrunner.TerrainAuthoring.Editor
{
    public enum GroundGrade { Normal, Marginal, Aerial }
    public static class TileDiagnostics
    {
        public static GroundGrade Grade(Vector3 normal,CharacterController controller)
        {
            float angle=Vector3.Angle(normal,Vector3.up);
            if(angle>controller.slopeLimit+.001f)return GroundGrade.Aerial;
            return angle>=controller.slopeLimit-5?GroundGrade.Marginal:GroundGrade.Normal;
        }
        public static List<string> Layout(StudioTile home,TerrainLayer[] palette)
        {
            var notes=new List<string>();
            var textures=palette.Where(l=>l).SelectMany(l=>new[]{l.diffuseTexture,l.normalMapTexture,l.maskMapTexture}).Where(t=>t).Distinct();
            long bytes=textures.Sum(t=>Profiler.GetRuntimeMemorySizeLong(t));
            notes.Add($"Referenced TerrainLayer textures: {bytes/1048576f:F1} MiB loaded (shared textures counted once).");
            if(palette.Length>4)notes.Add("Palette exceeds four layers; check extra URP terrain passes.");
            if(palette.Any(l=>!l.diffuseTexture))notes.Add("Palette includes a layer without a diffuse texture.");
            var terrain=home.terrain;var data=home.state.working;var origin=terrain.GetPosition();var size=data.size;
            foreach(var root in terrain.gameObject.scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Terrain>())
                {
                    if(t==terrain||!t.terrainData)continue;
                    var o=t.GetPosition();var s=t.terrainData.size;
                    bool right=Near(o.x,origin.x+size.x),left=Near(o.x+s.x,origin.x),top=Near(o.z,origin.z+size.z),bottom=Near(o.z+s.z,origin.z);
                    bool side=(right||left)&&Mathf.Min(o.z+s.z,origin.z+size.z)>Mathf.Max(o.z,origin.z);
                    bool end=(top||bottom)&&Mathf.Min(o.x+s.x,origin.x+size.x)>Mathf.Max(o.x,origin.x);
                    if(!side&&!end)continue;
                    Terrain neighbor=side?(right?terrain.rightNeighbor:terrain.leftNeighbor):(top?terrain.topNeighbor:terrain.bottomNeighbor);
                    if(neighbor!=t)notes.Add(t.name+": touching tile lacks matching Terrain neighbor link.");
                    float start=side?Mathf.Max(o.z,origin.z):Mathf.Max(o.x,origin.x);
                    float stop=side?Mathf.Min(o.z+s.z,origin.z+size.z):Mathf.Min(o.x+s.x,origin.x+size.x);
                    float fixedAxis=side?(right?o.x:origin.x):(top?o.z:origin.z);
                    float gap=0;
                    int samples=Mathf.Max(data.heightmapResolution,t.terrainData.heightmapResolution);
                    for(int i=0;i<samples;i++)
                    {
                        float along=Mathf.Lerp(start,stop,i/(samples-1f));
                        var p=new Vector3(side?fixedAxis:along,0,side?along:fixedAxis);
                        gap=Mathf.Max(gap,Mathf.Abs(terrain.SampleHeight(p)+origin.y-t.SampleHeight(p)-o.y));
                    }
                    if(gap>.02f)notes.Add($"{t.name}: sampled shared-edge height mismatch {gap:F2} m. Align adjoining heightmaps before painting.");
                    if(s!=size||t.terrainData.heightmapResolution!=data.heightmapResolution)
                        notes.Add(t.name+": different dimensions or height resolution; inspect edge sculpting and LOD.");
                    if(t.terrainData.alphamapResolution!=data.alphamapResolution)notes.Add(t.name+": different paint resolution; each side uses its own sampling grid.");
                    foreach(var layer in palette)
                    {
                        var tileSize=layer.tileSize;
                        if(!Repeats(o.x-origin.x,tileSize.x)||!Repeats(o.z-origin.z,tileSize.y))
                            notes.Add(layer.name+": texture phase differs across "+t.name+". Use a tile size dividing the origin spacing or a matching layer offset.");
                    }
                    var binding=t.GetComponentInChildren<StudioTile>(true);
                    if(!binding||!binding.state||binding.state.profile!=home.state.profile)notes.Add(t.name+": neighboring surface profile differs or has no Studio binding.");
                }
            return notes;
        }
        static bool Repeats(float d,float period)=>period>0&&Near(d/period,Mathf.Round(d/period));
        static bool Near(float a,float b)=>Mathf.Abs(a-b)<.001f;
    }
}
