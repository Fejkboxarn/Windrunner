using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Windrunner.TerrainAuthoring.Editor;
using Debug=UnityEngine.Debug;

namespace Windrunner.TerrainAuthoring.Tests
{
    public partial class TerrainStudioTests
    {
        static void Measure(string label,Action operation)
        {
            long probe=GC.GetAllocatedBytesForCurrentThread();
            var sentinel=new byte[8192];bool supported=GC.GetAllocatedBytesForCurrentThread()-probe>=sentinel.Length;GC.KeepAlive(sentinel);
            long heap=Profiler.GetMonoUsedSizeLong();
            long used=Profiler.GetTotalAllocatedMemoryLong(),bytes=GC.GetAllocatedBytesForCurrentThread();
            var elapsed=Stopwatch.StartNew();
            operation();elapsed.Stop();
            long managed=GC.GetAllocatedBytesForCurrentThread()-bytes;
            Debug.Log($"EDIT-BENCH {label} ms={elapsed.Elapsed.TotalMilliseconds:F2} allocated={(supported?managed.ToString():"unavailable")} monoUsedDelta={Profiler.GetMonoUsedSizeLong()-heap} editorDelta={Profiler.GetTotalAllocatedMemoryLong()-used} editorUsed={Profiler.GetTotalAllocatedMemoryLong()}");
        }
        [UnityTest,Category("Profiling")] public IEnumerator IntersectingNetworkWorkload()
        {
            var source=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/WindrunnerProduction/World/Terrain/CentralBasin.asset");
            var t=Tile(-1024,512,-1024,source);
            var roads=new StudioRoad[16];
            for(int i=0;i<roads.Length;i++)
            {
                var start=i<8?new Vector2(0,(i-4)*85):new Vector2((i-12)*85,0);
                roads[i]=StudioSetup.CreateRoad(t,start,carriage);
                roads[i].transform.localScale=new Vector3(18,1,8);
                roads[i].transform.rotation=Quaternion.Euler(0,i<8?0:90,0);
            }
            System.Collections.Generic.List<RoadPath> paths=null;
            Measure("plan16",()=>paths=RoadPath.InScene(t));
            Measure("warmPlan16",()=>paths=RoadPath.InScene(t));
            Debug.Log($"EDIT-NETWORK metres={paths.Sum(p=>p.lengths.Last()):F1} samples={paths.Sum(p=>p.points.Length)}");
            Measure("full",()=>StudioUpdates.Rebuild(t,TerrainPainter.Full(t.state.working),paths,false));
            for(int i=0;i<5;i++)
            {
                t.state.working.SetHeights(500+i,500+i,new float[,]{{.042f,.043f},{.041f,.042f}});
                Measure("localSculpt"+i,()=>Bake(t));
                roads[i].transform.position+=new Vector3(8,0,14);
                Measure("moveRoad"+i,()=>Bake(t));
            }
            t.state.automatic=true;
            for(int i=0;i<4;i++)
            {
                var clock=Stopwatch.StartNew();
                t.state.working.SetHeights(515+i,515,new float[,]{{.048f,.051f},{.049f,.048f}});
                Assert.IsFalse(TerrainPainter.Empty(t.state.dirty));
                while(!TerrainPainter.Empty(t.state.dirty)&&clock.Elapsed.TotalSeconds<30)yield return null;
                Assert.IsTrue(TerrainPainter.Empty(t.state.dirty));
                Debug.Log($"EDIT-AUTO {i} latencyMs={clock.Elapsed.TotalMilliseconds:F1} {StudioUpdates.LastResult}");
            }
            t.state.automatic=false;
            // Frame counters include the Editor and test runner. Require a visible positive control.
            using var counter=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",16);
            if(counter.Valid)
            {
                yield return null;yield return null;
                long idle=counter.LastValue;
                var control=new byte[4*1024*1024];
                yield return null;yield return null;
                long peak=counter.ToArray().Select(s=>s.Value).DefaultIfEmpty().Max();GC.KeepAlive(control);
                if(idle<control.Length/4 && peak>=control.Length)
                {
                    counter.Reset();counter.Start();
                    t.state.working.SetHeights(507,507,new float[,]{{.071f,.074f},{.071f,.074f}});
                    Bake(t);yield return null;yield return null;
                    Debug.Log($"EDIT-FRAME idle={idle} controlPeak={peak} sculptFramePeak={counter.ToArray().Select(s=>s.Value).DefaultIfEmpty().Max()} bytes (whole Editor frames)");
                }
                else Debug.Log($"EDIT-FRAME unavailable: idle={idle}, controlPeak={peak} did not resolve the 4 MiB control.");
            }
            else Debug.Log("EDIT-FRAME unavailable: Unity frame counter invalid.");
        }
    }
}
