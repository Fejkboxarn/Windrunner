using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.TerrainAuthoring.Editor;
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;

namespace Windrunner.TerrainAuthoring.Tests
{
    public partial class TerrainStudioTests
    {
        protected Scene scene,previous;
        protected string folder;
        protected SurfaceProfile profile;
        protected RoadProfile carriage;
        [SetUp] public void Before()
        {
            folder="Assets/TerrainStudioTest-"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            previous=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(previous.path)&&previous.isDirty&&!Application.isBatchMode)Assert.Ignore("Save the open scene before this integration test.");
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            profile=StudioSetup.Preset<SurfaceProfile>("Basin surfaces");carriage=StudioSetup.Preset<RoadProfile>("Carriage road");
        }
        [TearDown] public void After()
        {
            if(previous.IsValid())SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)
            {
                if(SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.DeleteAsset(folder);
        }
        protected StudioTile Tile(float x=0,int res=64,float z=0,TerrainData source=null)
        {
            if(!source)
            {
                source=new TerrainData{heightmapResolution=65,alphamapResolution=res,size=new Vector3(128,80,128),terrainLayers=new[]{profile.grass}};
                var heights=new float[65,65];
                for(int r=0;r<65;r++)for(int c=0;c<65;c++)heights[r,c]=.15f+.17f*Mathf.Exp(-((c-32)*(c-32)+(r-46)*(r-46))/90f);
                source.SetHeights(0,0,heights);
                AssetDatabase.CreateAsset(source,AssetDatabase.GenerateUniqueAssetPath(folder+"/source.asset"));
            }
            var t=Terrain.CreateTerrainGameObject(source).GetComponent<Terrain>();t.transform.position=new Vector3(x,0,z);
            var tile=StudioSetup.Attach(t,profile,folder);
            Assert.AreNotSame(source,t.terrainData);Assert.AreSame(t.terrainData,t.GetComponent<TerrainCollider>().terrainData);return tile;
        }
        protected static void Bake(StudioTile tile,RectInt r=default)=>StudioUpdates.Rebuild(tile,r,RoadPath.InScene(tile),false);
        static float[] Weights(StudioTile t)=>TerrainPainter.Capture(t.state.working,TerrainPainter.Full(t.state.working)).weights;
        static void EqualMap(float[] a,float[] b)
        {Assert.AreEqual(a.Length,b.Length);Assert.That(a.Zip(b,(x,y)=>Mathf.Abs(x-y)).Max(),Is.LessThan(.0041f));}
        [Test] public void SlopesElevationAndSeedProduceNormalizedReadableFamilies()
        {
            var plain=profile.Evaluate(Vector2.zero,420,Vector3.up,0);
            var cliff=profile.Evaluate(Vector2.zero,15,new Vector3(.98f,.2f,0).normalized,0);
            Assert.Greater(plain.x,plain.z);Assert.Greater(cliff.z,.75f);
            Assert.AreEqual(plain,profile.Evaluate(Vector2.zero,420,Vector3.up,0));
            for(int i=0;i<91;i++){var w=profile.Evaluate(new Vector2(i*3,-i),i*4,Quaternion.Euler(i,0,0)*Vector3.up,Mathf.Sin(i));
                Assert.AreEqual(1,w.x+w.y+w.z,.00001);Assert.GreaterOrEqual(Mathf.Min(w.x,w.y,w.z),-.000001f);}
        }
        [Test] public void MovedDeletedAndCrossingSplinesRestorePaintWithoutChangingGround()
        {
            var tile=Tile();var data=tile.state.working;
            var before=data.GetHeights(0,0,65,65);Bake(tile);var natural=Weights(tile);
            var road=StudioSetup.CreateRoad(tile,new Vector2(37,40),carriage);Bake(tile);var first=Weights(tile);
            Assert.Greater(first.Zip(natural,(a,b)=>Mathf.Abs(a-b)).Max(),.3f);
            Assert.Greater(tile.ribbons.sharedMesh.vertexCount,10);Assert.IsEmpty(tile.ribbons.GetComponentsInChildren<Collider>());
            var branch=StudioSetup.CreateRoad(tile,new Vector2(37,40),carriage);branch.transform.Rotate(0,75,0);Bake(tile);
            Object.DestroyImmediate(branch.gameObject);
            road.transform.position+=Vector3.forward*65;Bake(tile);
            var shifted=Weights(tile);
            Assert.That(shifted.Take(64*data.alphamapLayers*32).Zip(natural,(a,b)=>Mathf.Abs(a-b)).Max(),Is.LessThan(.005f));
            Object.DestroyImmediate(road.gameObject);Bake(tile);EqualMap(natural,Weights(tile));Assert.AreEqual(0,tile.ribbons.sharedMesh.vertexCount);
            CollectionAssert.AreEqual(before,data.GetHeights(0,0,65,65));
        }
        [Test] public void NativePaintProtectionSurvivesSculptUndoRedoAndReopen()
        {
            var tile=Tile();Bake(tile);var data=tile.state.working;var r=new RectInt(24,20,8,8);
            var paint=data.GetAlphamaps(r.x,r.y,r.width,r.height);
            int rock=Array.IndexOf(data.terrainLayers,profile.rock);
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)for(int c=0;c<data.alphamapLayers;c++)paint[y,x,c]=c==rock?1:0;
            data.SetAlphamaps(r.x,r.y,paint);
            Assert.Throws<InvalidOperationException>(()=>Bake(tile,r));
            PaintHistory.Resolve(tile,r,true,1);
            data.SetHeights(25,25,new float[,]{{.6f,.55f},{.58f,.52f}});
            Bake(tile,r);Assert.AreEqual(1,data.GetAlphamaps(25,25,1,1)[0,0,rock],.004);
            var road=StudioSetup.CreateRoad(tile,new Vector2(50,44),carriage);var before=Weights(tile);
            Bake(tile);var after=Weights(tile);
            Assert.That(after.Zip(before,(a,b)=>Mathf.Abs(a-b)).Max(),Is.GreaterThan(.3f));
            Undo.PerformUndo();EqualMap(before,Weights(tile));Undo.PerformRedo();EqualMap(after,Weights(tile));
            var path=folder+"/Edited.unity";var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
            Assert.IsTrue(EditorSceneManager.SaveScene(scene,path));AssetDatabase.SaveAssets();
            if(SceneManager.sceneCount==1)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            else {EditorSceneManager.CloseScene(scene,true);scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);}
            tile=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StudioTile>()).Single();
            Assert.AreEqual(guid,AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile.state.working)));EqualMap(after,Weights(tile));
            Assert.Greater(tile.ribbons.sharedMesh.vertexCount,0);Assert.AreEqual(1,tile.state.protectedPaint.Count);
            Assert.AreEqual(.6,tile.state.working.GetHeights(25,25,1,1)[0,0],.0001);
        }
        [Test] public void DifferentTileResolutionsShareWorldSpaceRulesAndRoad()
        {
            var left=Tile();var right=Tile(128,128);
            left.terrain.SetNeighbors(null,null,right.terrain,null);right.terrain.SetNeighbors(left.terrain,null,null,null);
            StudioSetup.CreateRoad(left,new Vector2(128,64),carriage);
            Bake(left);Bake(right);
            int soil=Array.IndexOf(left.state.working.terrainLayers,profile.soil);
            var a=left.state.working.GetAlphamaps(63,32,1,1);var b=right.state.working.GetAlphamaps(0,65,1,1);
            Assert.AreEqual(a[0,0,soil],b[0,0,soil],.04);Assert.Greater(a[0,0,soil],.65);
            Assert.Greater(left.ribbons.sharedMesh.GetIndexCount(0),0);Assert.Greater(right.ribbons.sharedMesh.GetIndexCount(0),0);
        }
        [Test] public void MissingLayerAndCanceledCalculationLeaveWorkingDataUntouched()
        {
            var t=Tile();Bake(t);var before=Weights(t);
            Assert.Throws<OperationCanceledException>(()=>TerrainPainter.Apply(t,TerrainPainter.Full(t.state.working),new List<RoadPath>(),false,_=>true));
            var copy=Object.Instantiate(profile);copy.rock=null;t.state.profile=copy;
            Assert.Throws<InvalidOperationException>(()=>Bake(t,new RectInt(4,4,4,4)));EqualMap(before,Weights(t));Object.DestroyImmediate(copy);
        }
        [Test] public void RevertAndRepeatUseSourcePaintAndLeaveSculptAlone()
        {
            var tile=Tile();Bake(tile);var generated=Weights(tile);
            Bake(tile,TerrainPainter.Full(tile.state.working));EqualMap(generated,Weights(tile));
            tile.state.working.SetHeights(2,2,new float[,]{{.45f}});
            StudioUpdates.Rebuild(tile,TerrainPainter.Full(tile.state.working),RoadPath.InScene(tile),false,true);
            Assert.That(tile.state.working.GetAlphamaps(3,3,1,1)[0,0,0],Is.EqualTo(1).Within(.004));
            Assert.That(tile.state.working.GetHeights(2,2,1,1)[0,0],Is.EqualTo(.45).Within(.0001));
            Assert.IsFalse(tile.state.automatic);
            Assert.AreEqual(TerrainPainter.Full(tile.state.working),tile.state.dirty);
        }
        [Test] public void DisjointFootprintsSkipInterveningLandscapeAndWalkabilityUsesBoundController()
        {
            var t=Tile();Bake(t);
            var first=new RectInt(3,4,5,4);var last=new RectInt(48,48,5,4);
            t.state.footprints.Add(new RoadFootprint{key="road",signature="old",rect=first});
            var regions=StudioUpdates.Regions(t,new System.Collections.Generic.List<RoadFootprint>{new RoadFootprint{key="road",signature="moved",rect=last}},default);
            CollectionAssert.AreEquivalent(new[]{first,last},regions);
            var controller=new GameObject("diagnostic reference").AddComponent<CharacterController>();
            controller.slopeLimit=50;
            Assert.AreEqual(GroundGrade.Normal,TileDiagnostics.Grade(Vector3.up,controller));
            var n=Quaternion.Euler(48,0,0)*Vector3.up;
            Assert.AreEqual(GroundGrade.Marginal,TileDiagnostics.Grade(n,controller));
            controller.slopeLimit=40;Assert.AreEqual(GroundGrade.Aerial,TileDiagnostics.Grade(n,controller));
        }
        [Test] public void RealBasinFullAndBrushBenchmarksPreserveSource()
        {
            var source=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WindrunnerProduction/World/Terrain/CentralBasin.asset");
            var original=TerrainPainter.Capture(source,TerrainPainter.Full(source));var tile=Tile(-1024,512,-1024,source);
            StudioSetup.CreateRoad(tile,new Vector2(12,35),carriage);
            var clock=Stopwatch.StartNew();Bake(tile);double full=clock.Elapsed.TotalMilliseconds;var samples=new double[7];
            for(int i=0;i<samples.Length;i++){clock.Restart();Bake(tile,new RectInt(220+i,230,20,20));samples[i]=clock.Elapsed.TotalMilliseconds;}
            Array.Sort(samples);Debug.Log($"Terrain Studio benchmark: full {full:F1} ms, 20x20 median {samples[3]:F1} ms, max {samples[6]:F1} ms; layers {tile.state.working.alphamapLayers}, ribbon vertices {tile.ribbons.sharedMesh.vertexCount}.");
            EqualMap(original.weights,TerrainPainter.Capture(source,TerrainPainter.Full(source)).weights);
        }
    }
}
