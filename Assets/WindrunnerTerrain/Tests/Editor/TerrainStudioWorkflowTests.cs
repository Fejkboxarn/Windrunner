using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Windrunner.TerrainAuthoring.Editor;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

namespace Windrunner.TerrainAuthoring.Tests
{
    public partial class TerrainStudioTests
    {
        [UnityTest] public IEnumerator DeferredUpdatesRepairOldRoadAndFollowASculpt()
        {
            var t=Tile();Bake(t);var road=StudioSetup.CreateRoad(t,new Vector2(35,32),carriage);
            t.state.automatic=true;StudioUpdates.Schedule();
            double until=EditorApplication.timeSinceStartup+12;
            while(t.state.footprints.Count==0&&EditorApplication.timeSinceStartup<until)yield return null;
            Assert.AreEqual(1,t.state.footprints.Count);
            var original=t.state.footprints[0].rect;
            Undo.RecordObject(road.transform,"Move test road");road.transform.position+=Vector3.forward*65;
            Undo.FlushUndoRecordObjects();
            until=EditorApplication.timeSinceStartup+12;
            while(t.state.footprints[0].rect==original&&EditorApplication.timeSinceStartup<until)yield return null;
            Assert.AreNotEqual(original,t.state.footprints[0].rect);
            var painted=Weights(t);
            t.state.working.SetHeights(5,5,new float[,]{{.85f,.8f},{.85f,.8f}});
            until=EditorApplication.timeSinceStartup+12;
            while(!TerrainPainter.Empty(t.state.dirty)&&EditorApplication.timeSinceStartup<until)yield return null;
            Assert.IsTrue(TerrainPainter.Empty(t.state.dirty));
            Assert.Greater(painted.Zip(Weights(t),(a,b)=>Mathf.Abs(a-b)).Max(),.05f);
            Object.DestroyImmediate(road.gameObject);
            until=EditorApplication.timeSinceStartup+12;
            while(t.state.footprints.Count!=0&&EditorApplication.timeSinceStartup<until)yield return null;
            Assert.IsEmpty(t.state.footprints);Assert.Zero(t.ribbons.sharedMesh.vertexCount);
        }

        [Test,Category("Profiling")] public void RenderedBasinRoadNearAndFar()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType,"Run with -Rendered.");
            var src=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WindrunnerProduction/World/Terrain/CentralBasin.asset");
            var tile=Tile(-1024,512,-1024,src);
            var road=StudioSetup.CreateRoad(tile,Vector2.zero,carriage);
            var lamp=new GameObject("Test sun").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.5f;lamp.transform.rotation=Quaternion.Euler(55,25,0);
            var camera=new GameObject("Survey camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=54;
            var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            camera.targetTexture=target;var old=RenderTexture.active;var ground=road.transform.position;
            var renderer=tile.ribbons.GetComponent<MeshRenderer>();
            void Frame()
            {
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,540),0,0,false);
            }
            double Median()
            {
                Frame();Frame();var times=new double[15];
                for(int i=0;i<times.Length;i++){var clock=Stopwatch.StartNew();Frame();times[i]=clock.Elapsed.TotalMilliseconds;}
                Array.Sort(times);return times[7];
            }
            void Position(float d)
            {
                camera.transform.position=ground+new Vector3(-d*.7f,d*.56f,-d*.9f);
                camera.transform.LookAt(ground+Vector3.right*3);
            }
            try
            {
                Position(16);double original=Median();Bake(tile);
                foreach(float distance in new[]{16f,72f})
                {
                    Position(distance);renderer.enabled=false;
                    double broad=Median();var off=pixels.GetPixels32();
                    renderer.enabled=true;double wear=Median();var on=pixels.GetPixels32();
                    int delta=0;
                    for(int i=0;i<on.Length;i++)if(Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b)>8)delta++;
                    var filename=$"Logs/ProductionMigration/terrain-studio-road-{distance:F0}m-{DateTime.UtcNow.Ticks}.png";
                    File.WriteAllBytes(filename,pixels.EncodeToPNG());
                    Debug.Log($"Terrain Studio rendered {distance:F0} m: original near {original:F2} ms; broad {broad:F2} ms; wear {wear:F2} ms; differing pixels {delta}; {SystemInfo.graphicsDeviceName}; {filename}");
                    Assert.Greater(delta,distance<20?120:5,"Wheel detail must actually contribute visible pixels.");
                }
            }
            finally {RenderTexture.active=old;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
