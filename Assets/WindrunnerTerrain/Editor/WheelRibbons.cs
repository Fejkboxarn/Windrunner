using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Windrunner.TerrainAuthoring.Editor
{
    public static class WheelRibbons
    {
        public static void Bake(StudioTile tile,List<RoadPath> paths)
        {
            var filter=tile.ribbons;
            if(!filter||!filter.sharedMesh)throw new System.InvalidOperationException("Restore the baked wheel mesh binding first.");
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color32>();
            var faces=new List<List<int>>();var materials=new List<Material>();
            var heights=new TerrainPainter.Heights(tile.terrain);
            foreach(var path in paths)
            {
                var p=path.Profile;
                if(!p.carriage||!p.trackMaterial||TerrainPainter.Empty(TerrainPainter.Pixels(tile.terrain,path.bounds)))continue;
                int m=materials.IndexOf(p.trackMaterial);
                if(m<0){m=materials.Count;materials.Add(p.trackMaterial);faces.Add(new List<int>());}
                foreach(int side in new[]{-1,1})
                {
                    int previous=-1;
                    for(int i=0;i<path.points.Length;i++)
                    {
                        Vector2 center=path.points[i],axis=(path.points[Mathf.Min(i+1,path.points.Length-1)]-path.points[Mathf.Max(0,i-1)]).normalized;
                        var lateral=new Vector2(-axis.y,axis.x);
                        float grain=Mathf.PerlinNoise(center.x*.29f+side*7,center.y*.29f);
                        center+=lateral*(side*p.trackSpacing*.5f+(grain-.5f)*p.trackBreakup*p.trackWidth*.6f);
                        int row=vertices.Count;
                        for(int j=0;j<3;j++)
                        {
                            var pt=center+lateral*((j-1)*p.trackWidth*.5f);
                            vertices.Add(filter.transform.InverseTransformPoint(new Vector3(pt.x,heights.At(pt)+.012f,pt.y)));
                            uv.Add(new Vector2(j*.5f,path.lengths[i]/Mathf.Max(.1f,p.textureRepeatMetres)));
                            float alpha=p.wear*(1-p.trackBreakup*(1-grain));
                            var origin=tile.terrain.GetPosition();var d=tile.state.working;
                            var cell=new Vector2Int(Mathf.RoundToInt((pt.x-origin.x)/d.size.x*(d.alphamapWidth-1)),Mathf.RoundToInt((pt.y-origin.z)/d.size.z*(d.alphamapHeight-1)));
                            foreach(var patch in tile.state.protectedPaint)if(patch.rect.Contains(cell))alpha*=1-patch.opacity;
                            foreach(var crossing in paths)if(crossing.Profile.priority>p.priority)alpha*=1-crossing.Coverage(pt);
                            if(!path.closed)
                                alpha*=Mathf.Clamp01(Mathf.Min(path.lengths[i],path.lengths[path.lengths.Length-1]-path.lengths[i])/1.5f);
                            colors.Add(new Color(p.trackBreakup,1,1,alpha));
                        }
                        if(previous>=0)
                        {
                            Vector2 mid=(path.points[i-1]+path.points[i])*.5f;
                            var o=tile.terrain.GetPosition();var s=tile.state.working.size;
                            var d=tile.state.working;
                            if(mid.x>=o.x&&mid.x<o.x+s.x&&mid.y>=o.z&&mid.y<o.z+s.z &&
                                !d.IsHole((int)((mid.x-o.x)/s.x*d.holesResolution),(int)((mid.y-o.z)/s.z*d.holesResolution)))
                                for(int j=0;j<2;j++)faces[m].AddRange(new[]{previous+j,row+j,previous+j+1,previous+j+1,row+j,row+j+1});
                        }
                        previous=row;
                    }
                }
            }
            var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
            Undo.RegisterCompleteObjectUndo(mesh,"Bake wheel wear");Undo.RecordObject(renderer,"Bake wheel wear");
            mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.subMeshCount=faces.Count;
            for(int i=0;i<faces.Count;i++)mesh.SetTriangles(faces[i],i,false);
            mesh.RecalculateBounds();renderer.sharedMaterials=materials.ToArray();renderer.enabled=faces.Exists(f=>f.Count>0);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            EditorUtility.SetDirty(mesh);
        }
    }
}
