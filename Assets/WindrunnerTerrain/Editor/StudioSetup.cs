using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Object=UnityEngine.Object;
namespace Windrunner.TerrainAuthoring.Editor
{
    public static class StudioSetup
    {
        public const string Folder="Assets/WindrunnerTerrain/Presets/";
        const string Vendor="Assets/Raygeas/Grasslands - Stylized Nature/Assets/Terrain Layers/";
        public static T Preset<T>(string name) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(Folder+name+".asset");
        public static void CreatePresets()
        {
            if(Directory.GetFiles(Folder).Any(f=>!f.EndsWith(".meta")))throw new IOException("Preset assets already exist.");
            TerrainLayer Copy(string source,string name,float scale,float normal)
            {
                var sourceLayer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(Vendor+source+".terrainlayer");
                if(!sourceLayer)throw new IOException("Missing stylized terrain layer "+source);
                var l=Object.Instantiate(sourceLayer);l.name=name;l.tileSize=Vector2.one*scale;l.normalScale=normal;
                AssetDatabase.CreateAsset(l,Folder+name+".terrainlayer");return l;
            }
            var surface=ScriptableObject.CreateInstance<SurfaceProfile>();
            surface.grass=Copy("Grass","Meadow",4,.85f);surface.soil=Copy("Mud","Earth",4,.75f);surface.rock=Copy("MudStones","Broken stone",8,1.2f);
            AssetDatabase.CreateAsset(surface,Folder+"Basin surfaces.asset");
            var mat=new Material(Shader.Find("Windrunner/Terrain/Wheel Wear"));
            AssetDatabase.CreateAsset(mat,Folder+"Wheel wear.mat");
            foreach(var name in new[]{"Dirt road","Footpath","Carriage road","Gravel road"})
            {
                var r=ScriptableObject.CreateInstance<RoadProfile>();
                r.surface=name=="Gravel road"?surface.rock:surface.soil;r.trackMaterial=mat;
                r.width=name=="Footpath"?2.4f:name=="Carriage road"?8:6;
                r.shoulderMetres=name=="Footpath"?1.6f:3;
                r.vegetation=name=="Footpath"?.45f:.12f;r.carriage=name=="Carriage road";r.priority=r.carriage?2:1;
                AssetDatabase.CreateAsset(r,Folder+name+".asset");
            }
            foreach(string file in AssetDatabase.FindAssets(string.Empty,new[]{Folder.TrimEnd('/')}))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(file)));
        }
        public static StudioTile Attach(Terrain terrain,SurfaceProfile profile,string folder)
        {
            if(!terrain||!terrain.terrainData||!profile||!AssetDatabase.IsValidFolder(folder))throw new ArgumentException("Choose a terrain, surface profile and project folder.");
            if(terrain.GetComponentInChildren<StudioTile>(true))throw new InvalidOperationException("This terrain already has a Studio binding.");
            var collider=terrain.GetComponent<TerrainCollider>();
            if(!collider||collider.terrainData!=terrain.terrainData)throw new InvalidOperationException("Verify the TerrainCollider binding first.");
            var original=terrain.terrainData;
            if(original.terrainLayers.Length==0||original.terrainLayers.Any(l=>!l))throw new InvalidOperationException("Assign a valid starting TerrainLayer before capturing source paint.");
            var data=Object.Instantiate(original);
            string Asset(string name)=>AssetDatabase.GenerateUniqueAssetPath(folder+"/"+original.name+" "+name+".asset");
            AssetDatabase.CreateAsset(data,Asset("working"));
            var state=ScriptableObject.CreateInstance<TilePaintState>();state.working=data;state.profile=profile;
            state.original=TerrainPainter.Capture(original,TerrainPainter.Full(original));state.lastApplied=TerrainPainter.Capture(original,TerrainPainter.Full(original));
            state.dirty=TerrainPainter.Full(data);AssetDatabase.CreateAsset(state,Asset("paint history"));
            var mesh=new Mesh();AssetDatabase.CreateAsset(mesh,Asset("wheel ribbons"));
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Attach Terrain Studio working copy");
            Undo.RecordObjects(new Object[]{terrain,collider},"Assign editable terrain copy");
            terrain.terrainData=data;collider.terrainData=data;
            var binding=new GameObject("Terrain Studio authoring"){tag="EditorOnly"};
            binding.transform.SetParent(terrain.transform,false);var tile=binding.AddComponent<StudioTile>();
            tile.state=state;tile.terrain=terrain;
            var detail=new GameObject("Baked wheel wear",typeof(MeshFilter),typeof(MeshRenderer));
            detail.transform.SetParent(terrain.transform,false);
            tile.ribbons=detail.GetComponent<MeshFilter>();tile.ribbons.sharedMesh=mesh;
            detail.GetComponent<MeshRenderer>().enabled=false;
            Undo.RegisterCreatedObjectUndo(detail,"Create baked renderer");Undo.RegisterCreatedObjectUndo(binding,"Create authoring binding");
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssetIfDirty(state);AssetDatabase.SaveAssetIfDirty(data);AssetDatabase.SaveAssetIfDirty(mesh);
            return tile;
        }
        public static StudioRoad CreateRoad(StudioTile tile,Vector2 start,RoadProfile profile)
        {
            if(!profile)throw new InvalidOperationException("Choose a road profile.");
            var go=new GameObject(profile.name,typeof(SplineContainer),typeof(StudioRoad)){tag="EditorOnly"};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,tile.gameObject.scene);
            var parent=tile.terrain.transform.parent;
            if(parent)go.transform.SetParent(parent,true);
            var heights=new TerrainPainter.Heights(tile.terrain);
            go.transform.position=new Vector3(start.x,heights.At(start),start.y);
            var spline=go.GetComponent<SplineContainer>().Spline;spline.Clear();
            foreach(var delta in new[]{new Vector2(-22,-12),Vector2.zero,new Vector2(22,16),new Vector2(44,14)})
            {
                var p=start+delta;
                spline.Add(new BezierKnot(new float3(delta.x,heights.At(p)-go.transform.position.y,delta.y)),TangentMode.AutoSmooth);
            }
            var road=go.GetComponent<StudioRoad>();road.profile=profile;
            Undo.RegisterCreatedObjectUndo(go,"Create spline road");Selection.activeGameObject=go;StudioUpdates.Schedule();return road;
        }
    }
}
