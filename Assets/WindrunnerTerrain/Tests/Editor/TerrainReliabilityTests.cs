using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using Windrunner.TerrainAuthoring.Editor;
using Object=UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Tests
{
    public partial class TerrainStudioTests
    {
        static IEnumerator Settle(StudioTile t)
        {
            double deadline=EditorApplication.timeSinceStartup+12;
            while(!TerrainPainter.Empty(t.state.dirty) && EditorApplication.timeSinceStartup<deadline)yield return null;
            Assert.IsTrue(TerrainPainter.Empty(t.state.dirty),StudioUpdates.LastResult);
        }

        StudioTile ReloadTile()
        {
            var path=folder+"/Sculpt.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene,path));
            if(SceneManager.sceneCount==1)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            else {EditorSceneManager.CloseScene(scene,true);scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);}
            SceneManager.SetActiveScene(scene);
            return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StudioTile>()).Single();
        }

        [UnityTest] public IEnumerator BrushBurstsCoalesceAndProtectedJunctionPersistsAcrossReload()
        {
            var t=Tile();var road=StudioSetup.CreateRoad(t,new Vector2(48,48),carriage);
            StudioSetup.CreateRoad(t,new Vector2(48,48),carriage).transform.Rotate(0,70,0);
            Bake(t);var d=t.state.working;var rect=new RectInt(22,22,4,4);
            int rock=Array.IndexOf(d.terrainLayers,profile.rock);
            var art=new float[4,4,d.alphamapLayers];
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)art[y,x,rock]=1;
            d.SetAlphamaps(rect.x,rect.y,art);
            t.state.nativeDirty=default; // A paint importer can bypass the native Terrain callback.
            StringAssert.Contains("native paint",SetupValidation.Inspect(t));
            Assert.IsTrue(TerrainPainter.Empty(t.state.nativeDirty));
            Assert.AreEqual(rect,PaintHistory.ReviewNativeEdits(t));
            Assert.Throws<InvalidOperationException>(()=>Bake(t,rect));
            PaintHistory.Resolve(t,rect,true,1);
            var untouched=new RectInt(0,58,5,5);var outside=TerrainPainter.Capture(d,untouched).weights;
            t.state.automatic=true;
            for(int burst=0;burst<2;burst++)
            {
                ulong count=StudioUpdates.CompletedRebuilds;
                var heights=new float[17,17];
                for(int y=0;y<17;y++)for(int x=0;x<17;x++)heights[y,x]=.15f+(x+y+burst)*.012f;
                d.SetHeights(18,18,heights);d.SetHeights(28,30,new float[,]{{.61f,.65f},{.64f,.62f}});
                road.transform.position+=Vector3.forward*12;StudioUpdates.Schedule();
                var expected=d.GetHeights(0,0,65,65);
                yield return Settle(t);
                double stop=EditorApplication.timeSinceStartup+.8;
                while(EditorApplication.timeSinceStartup<stop)yield return null;
                Assert.AreEqual(count+1,StudioUpdates.CompletedRebuilds);
                CollectionAssert.AreEqual(expected,d.GetHeights(0,0,65,65));
                CollectionAssert.AreEqual(outside,TerrainPainter.Capture(d,untouched).weights);
                Assert.AreEqual(1,d.GetAlphamaps(23,23,1,1)[0,0,rock],.004);
                Assert.IsTrue(d==t.terrain.GetComponent<TerrainCollider>().terrainData);
            }
            t=ReloadTile();d=t.state.working;Assert.AreEqual(1,t.state.protectedPaint.Count);
            d.SetHeights(28,30,new float[,]{{.59f}});yield return Settle(t);
            var protectedWeights=Weights(t);
            Undo.IncrementCurrentGroup();PaintHistory.Clear(t,0);Bake(t);
            var released=Weights(t);Assert.Less(d.GetAlphamaps(23,23,1,1)[0,0,rock],.95f);
            Undo.PerformUndo();Undo.PerformUndo();Assert.AreEqual(1,t.state.protectedPaint.Count);EqualMap(protectedWeights,Weights(t));
            Undo.PerformRedo();Undo.PerformRedo();Assert.IsEmpty(t.state.protectedPaint);EqualMap(released,Weights(t));
            t.state.automatic=false;
        }

        [Test] public void SplitReshapeExtendAndDeleteMatchesFullBakeOnBothSides()
        {
            var tiles=new[]{Tile(),Tile(128)};var left=tiles[0];
            foreach(var tile in tiles)Bake(tile);
            var original=tiles.Select(Weights).ToArray();
            var road=StudioSetup.CreateRoad(left,new Vector2(112,68),carriage);
            var branch=StudioSetup.CreateRoad(left,new Vector2(120,70),carriage);branch.transform.Rotate(0,62,0);
            var container=road.GetComponent<SplineContainer>();
            void Check()
            {
                foreach(var tile in tiles)
                {
                    Bake(tile);var incremental=Weights(tile);Bake(tile,TerrainPainter.Full(tile.state.working));
                    EqualMap(incremental,Weights(tile));
                }
            }
            Check();
            var first=RoadPath.InScene(left).First(p=>p.road==road);
            Assert.AreSame(first,RoadPath.InScene(left).First(p=>p.road==road));
            var originalKnots=container.Spline.ToArray();
            Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(container,"Split and extend fixture");
            var a=new Spline();var b=new Spline();
            foreach(var k in originalKnots.Take(3))a.Add(k);
            foreach(var k in originalKnots.Skip(2))b.Add(k);
            b.Add(new BezierKnot(new float3(90,0,-25)),TangentMode.AutoSmooth);
            a[1]=new BezierKnot(new float3(7,0,16));
            container.Splines=new[]{a,b};Undo.FlushUndoRecordObjects();
            Assert.AreEqual(2,RoadPath.InScene(left).Count(p=>p.road==road));
            Undo.PerformUndo();Assert.AreEqual(1,RoadPath.InScene(left).Count(p=>p.road==road));
            Undo.PerformRedo();Check();Assert.AreEqual(2,RoadPath.InScene(left).Count(p=>p.road==road));
            road.transform.position+=Vector3.back*46;Check();
            Object.DestroyImmediate(branch.gameObject);Object.DestroyImmediate(road.gameObject);Check();
            for(int i=0;i<tiles.Length;i++)EqualMap(original[i],Weights(tiles[i]));
        }

        [Test] public void BrokenReferencesBlockWritesAndIdentifyRepairs()
        {
            var t=Tile();var r=StudioSetup.CreateRoad(t,new Vector2(45,45),carriage);Bake(t);
            var paint=Weights(t);int vertices=t.ribbons.sharedMesh.vertexCount;
            r.profile=null;StringAssert.Contains("Assign a RoadProfile",SetupValidation.Inspect(t));
            Assert.Throws<InvalidOperationException>(()=>Bake(t));r.profile=carriage;
            PaintHistory.Resolve(t,new RectInt(10,10,3,3),true,1);
            t.state.protectedPaint[0].weights=null;
            StringAssert.Contains("protected override 1",SetupValidation.Inspect(t));
            Assert.Throws<InvalidOperationException>(()=>Bake(t));
            Assert.AreEqual(vertices,t.ribbons.sharedMesh.vertexCount);EqualMap(paint,Weights(t));
            t.state.protectedPaint.Clear();
            var other=Tile(128);other.terrain.transform.position+=Vector3.up*4;
            StringAssert.Contains("height mismatch",SetupValidation.Inspect(t));
        }

        [Test] public void ChangedWheelGeometryOnGradePreservesMotorTraceAndContacts()
        {
            var tile=Tile();var data=tile.state.working;var heights=new float[65,65];
            for(int z=0;z<65;z++)for(int x=0;x<65;x++)heights[z,x]=Mathf.Clamp01(.12f+Mathf.Max(0,x-27)*.045f);
            data.SetHeights(0,0,heights);var actual=data.GetHeights(0,0,65,65);
            var motorType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AirflowPrototype.PlayerMotor")).First(t=>t!=null);
            var move=motorType.GetMethod("ApplyDisplacement");var phase=Enum.Parse(move.GetParameters()[1].ParameterType,"Locomotion");
            var collider=tile.terrain.GetComponent<TerrainCollider>();
            List<(Vector3,CollisionFlags,Vector3,Vector3)> Traverse()
            {
                var player=new GameObject("Disposable motor");var cc=player.AddComponent<CharacterController>();
                var motor=player.AddComponent(motorType);
                try
                {
                    var fields=new SerializedObject(motor);fields.FindProperty("controller").objectReferenceValue=cc;fields.ApplyModifiedPropertiesWithoutUndo();
                    player.transform.position=new Vector3(44,tile.terrain.SampleHeight(new Vector3(44,0,48))+1.02f,48);
                    Physics.SyncTransforms();
                    var trace=new List<(Vector3,CollisionFlags,Vector3,Vector3)>();
                    for(int i=0;i<85;i++)
                    {
                        var flags=(CollisionFlags)move.Invoke(motor,new object[]{new Vector3(.18f,-.22f,0),phase});
                        var p=player.transform.position;
                        Assert.IsTrue(collider.Raycast(new Ray(new Vector3(p.x,100,p.z),Vector3.down),out var hit,150));
                        trace.Add((p,flags,hit.point,hit.normal));
                    }
                    Assert.Greater(player.transform.position.x,53);Assert.IsTrue(trace.Any(step=>(step.Item2&CollisionFlags.Below)!=0));
                    return trace;
                }
                finally{Object.DestroyImmediate(player);}
            }
            var start=Traverse();var road=StudioSetup.CreateRoad(tile,new Vector2(52,48),carriage);
            var preset=Object.Instantiate(carriage);
            try
            {
                road.profile=preset;Bake(tile);CollectionAssert.AreEqual(start,Traverse());
                preset.trackSpacing*=.65f;preset.trackWidth*=1.4f;preset.wear*=.5f;Bake(tile);
                CollectionAssert.AreEqual(start,Traverse());Assert.IsEmpty(tile.ribbons.GetComponentsInChildren<Collider>());
                Assert.IsTrue(data==collider.terrainData);CollectionAssert.AreEqual(actual,data.GetHeights(0,0,65,65));
                Assert.Greater(tile.ribbons.sharedMesh.bounds.size.y,5);
            }
            finally{Object.DestroyImmediate(preset);}
        }
    }
}
